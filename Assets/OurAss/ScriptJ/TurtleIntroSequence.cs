
using System.Collections;
using UnityEngine;

namespace TurtleGame
{
    public class TurtleIntroSequence : MonoBehaviour
    {
        [Header("Egg")]
        [SerializeField] private GameObject egg;
        [SerializeField] private Animator eggAnimator;
        [SerializeField, Min(0f)]
        private float hatchDuration = 3f;

        [Header("Turtle")]
        [SerializeField] private GameObject turtleModel;
        [SerializeField] private Animator turtleAnimator;

        [Header("First Steps")]
        [SerializeField, Min(0f)]
        private float idleBeforeWalk = 2f;

        [SerializeField, Min(0f)]
        private float walkDuration = 3f;

        [SerializeField, Min(0f)]
        private float walkSpeed = 1.5f;

        [SerializeField]
        private Vector3 walkDirection = Vector3.forward;

        private TurtlePlayerController player;
        private TurtleAnimatorDriver animationDriver;
        private CharacterController character;

        private bool introStarted;

        private void Awake()
        {
            player = GetComponent<TurtlePlayerController>();
            animationDriver =
                GetComponent<TurtleAnimatorDriver>();
            character = GetComponent<CharacterController>();

            // Disable player movement during the main menu.
            if (player != null)
                player.enabled = false;

            if (animationDriver != null)
                animationDriver.enabled = false;

            // Keep the egg visible but stop its animation.
            if (eggAnimator != null)
                eggAnimator.enabled = false;

            // Keep the turtle mesh active.
            if (turtleModel != null)
                turtleModel.SetActive(true);
        }

        // Called by the Play Game button.
        public void BeginIntro()
        {
            if (introStarted)
                return;

            if (egg == null || eggAnimator == null ||
                turtleModel == null || turtleAnimator == null ||
                character == null)
            {
                Debug.LogError(
                    "TurtleIntroSequence: Missing references!",
                    this
                );
                return;
            }

            introStarted = true;
            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            // 1. Start the egg-hatching animation.
            egg.SetActive(true);
            eggAnimator.enabled = true;
            eggAnimator.Play("Base Layer.Hatch", 0, 0f);
            eggAnimator.Update(0f);

            // Wait for hatching to finish.
            yield return new WaitForSeconds(hatchDuration);

            // 2. Keep the egg visible.
            // Do not deactivate EggsAndHatch.

            // 3. Play Idle before the first steps.
            turtleAnimator.SetFloat("Speed", 0f);
            turtleAnimator.Play("Base Layer.Idle", 0, 0f);

            // NEW: Adjustable waiting time.
            yield return new WaitForSeconds(idleBeforeWalk);

            // 4. Start the existing Walk animation.
            turtleAnimator.SetFloat("Speed", walkSpeed);
            turtleAnimator.Play("Base Layer.Walk", 0, 0f);

            Vector3 direction =
                transform.TransformDirection(walkDirection);

            direction.y = 0f;
            direction.Normalize();

            float elapsed = 0f;

            while (elapsed < walkDuration)
            {
                float dt = Mathf.Min(
                    Time.deltaTime,
                    walkDuration - elapsed
                );

                Vector3 motion =
                    direction * walkSpeed + Vector3.down * 2f;

                character.Move(motion * dt);

                elapsed += dt;
                yield return null;
            }

            // 5. Stop walking and return to Idle.
            turtleAnimator.SetFloat("Speed", 0f);
            turtleAnimator.Play("Base Layer.Idle", 0, 0f);

            // 6. Give control to the player.
            if (animationDriver != null)
                animationDriver.enabled = true;

            if (player != null)
                player.enabled = true;
        }
    }
}
