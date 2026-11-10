
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TurtleGame
{
    public class TurtleHatchOnMove : MonoBehaviour
    {
        [Header("Egg Hatching")]
        [SerializeField] private Animator eggAnimator;

        [SerializeField, Min(0.01f)] private float hatchPlaybackSpeed = 1f;
        [SerializeField] private bool waitForHatch;
        [SerializeField] private bool waitForObjective;
        [Tooltip("Seconds until the turtle can leave the egg while debris keeps animating. Zero waits for the full clip.")]
        [SerializeField, Min(0f)] private float hatchReleaseDelay;

        [Header("Shell Fragments")]
        [SerializeField, Range(0f, 1f)] private float fragmentTravelMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float fragmentLiftMultiplier = 1f;
        [Tooltip("Maximum fragment rotation in degrees per second. Zero keeps the authored rotation.")]
        [SerializeField, Min(0f)] private float fragmentTurnSpeed;

        [Header("First Steps")]
        [SerializeField, Min(0f)] private float firstStepsDuration;
        [SerializeField, Range(0f, 1f)] private float initialSpeedMultiplier = 0.35f;

        private TurtlePlayerController player;
        private TurtleAnimatorDriver animationDriver;

        private bool gameplayStarted;
        private bool hasHatched;
        private bool objectiveShown;
        private readonly List<ShellFragment> shellFragments = new List<ShellFragment>();

        private sealed class ShellFragment
        {
            public Transform Transform;
            public Vector3 RestPosition;
            public Quaternion Rotation;
        }

        public bool HasFinishedHatching { get; private set; }

        private void Awake()
        {
            player = GetComponent<TurtlePlayerController>();
            animationDriver = GetComponent<TurtleAnimatorDriver>();

            // Keep the player inactive during the main menu.
            if (player != null)
                player.enabled = false;

            if (animationDriver != null)
                animationDriver.enabled = false;

            // Keep the egg visible without playing its animation.
            if (eggAnimator != null)
                eggAnimator.enabled = false;
        }

        // Called when the Play Game button is pressed.
        public void BeginGameplay()
        {
            if (gameplayStarted)
                return;

            gameplayStarted = true;
            StartHatching();
        }

        // Called when the moonlight objective has become visible.
        public void AllowWalking()
        {
            objectiveShown = true;
        }

        private void StartHatching()
        {
            if (hasHatched)
                return;

            if (eggAnimator == null)
            {
                Debug.LogError(
                    "TurtleHatchOnMove: Egg Animator is missing!",
                    this
                );
                return;
            }

            hasHatched = true;
            StartCoroutine(HatchAndTakeFirstSteps());
        }

        private IEnumerator HatchAndTakeFirstSteps()
        {
            eggAnimator.speed = hatchPlaybackSpeed;
            eggAnimator.enabled = true;

            eggAnimator.Play(
                "Base Layer.Hatch",
                0,
                0f
            );

            eggAnimator.Update(0f);
            BindShellFragments();

            if (waitForHatch || waitForObjective)
            {
                // Shell fragments can keep falling after the turtle is ready to leave the egg.
                float elapsedHatch = 0f;
                while (eggAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                {
                    if (hatchReleaseDelay > 0f && elapsedHatch >= hatchReleaseDelay)
                        break;
                    yield return null;
                    elapsedHatch += Time.deltaTime;
                }
            }

            HasFinishedHatching = true;
            while (waitForObjective && !objectiveShown)
                yield return null;

            if (player != null)
            {
                player.MovementSpeedMultiplier = firstStepsDuration > 0f ? initialSpeedMultiplier : 1f;
                player.enabled = true;
            }

            if (animationDriver != null)
                animationDriver.enabled = true;

            float elapsed = 0f;
            while (elapsed < firstStepsDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / firstStepsDuration);
                if (player != null)
                    player.MovementSpeedMultiplier = Mathf.Lerp(initialSpeedMultiplier, 1f, progress);
                yield return null;
            }

            if (player != null)
                player.MovementSpeedMultiplier = 1f;
        }

        private void BindShellFragments()
        {
            shellFragments.Clear();
            foreach (Transform fragment in eggAnimator.GetComponentsInChildren<Transform>(true))
            {
                // The imported egg names its individual shell meshes with this suffix.
                if (!fragment.name.Contains("_cell."))
                    continue;

                shellFragments.Add(new ShellFragment
                {
                    Transform = fragment,
                    RestPosition = fragment.localPosition,
                    Rotation = fragment.localRotation
                });
            }
        }

        private void LateUpdate()
        {
            if (!hasHatched || eggAnimator == null || !eggAnimator.enabled || Time.deltaTime <= 0f)
                return;

            foreach (ShellFragment fragment in shellFragments)
            {
                if (fragment.Transform == null)
                    continue;

                // Keep the authored fall, but reduce the outward impulse and upward launch.
                Vector3 offset = fragment.Transform.localPosition - fragment.RestPosition;
                offset.x *= fragmentTravelMultiplier;
                offset.z *= fragmentTravelMultiplier;
                if (offset.y > 0f)
                    offset.y *= fragmentLiftMultiplier;
                fragment.Transform.localPosition = fragment.RestPosition + offset;

                if (fragmentTurnSpeed > 0f)
                {
                    fragment.Rotation = Quaternion.RotateTowards(fragment.Rotation,
                        fragment.Transform.localRotation, fragmentTurnSpeed * Time.deltaTime);
                    fragment.Transform.localRotation = fragment.Rotation;
                }
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (player != null)
                player.MovementSpeedMultiplier = 1f;
        }
    }
}
