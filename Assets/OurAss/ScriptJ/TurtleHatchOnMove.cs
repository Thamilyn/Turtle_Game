
using UnityEngine;
using UnityEngine.InputSystem;

namespace TurtleGame
{
    public class TurtleHatchOnMove : MonoBehaviour
    {
        [Header("Egg Hatching")]
        [SerializeField] private Animator eggAnimator;

        [Header("Movement Detection")]
        [SerializeField, Min(0.001f)]
        private float movementThreshold = 0.01f;

        private TurtlePlayerController player;
        private TurtleAnimatorDriver animationDriver;

        private Vector3 previousPosition;
        private bool gameplayStarted;
        private bool hasHatched;

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

            previousPosition = transform.position;
        }

        // Called when the Play Game button is pressed.
        public void BeginGameplay()
        {
            if (gameplayStarted)
                return;

            gameplayStarted = true;
            previousPosition = transform.position;

            // Enable normal player movement.
            if (player != null)
                player.enabled = true;

            if (animationDriver != null)
                animationDriver.enabled = true;
        }

        private void Update()
        {
            // Don't trigger hatching before Play Game
            // or after hatching has already started.
            if (!gameplayStarted || hasHatched)
                return;

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            // Detect the first WASD keypress.
            if (keyboard.wKey.wasPressedThisFrame ||
                keyboard.aKey.wasPressedThisFrame ||
                keyboard.sKey.wasPressedThisFrame ||
                keyboard.dKey.wasPressedThisFrame)
            {
                Debug.Log("WASD detected! Starting egg hatch.");
                StartHatching();
            }
        }



        private void StartHatching()
        {
            hasHatched = true;

            if (eggAnimator == null)
            {
                Debug.LogError(
                    "TurtleHatchOnMove: Egg Animator is missing!",
                    this
                );
                return;
            }

            eggAnimator.enabled = true;

            eggAnimator.Play(
                "Base Layer.Hatch",
                0,
                0f
            );

            Debug.Log("Turtle started moving! Egg hatching!");
        }
    }
}
