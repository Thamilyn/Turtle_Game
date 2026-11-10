
using UnityEngine;

namespace TurtleGame
{
    [RequireComponent(typeof(CharacterController))]
    public class TurtleAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator turtleAnimator;
        [SerializeField] private bool matchPlaybackToMovement;
        [SerializeField, Min(0.01f)] private float referenceWalkSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float playbackBlendTime = 0.15f;

        private static readonly int Speed =
            Animator.StringToHash("Speed");

        private Vector3 previousPosition;

        private void Awake()
        {
            if (turtleAnimator == null)
                turtleAnimator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (turtleAnimator == null)
                return;

            Vector3 movement =
                transform.position - previousPosition;

            previousPosition = transform.position;

            movement.y = 0f;

            float speed = Time.deltaTime > 0f
                ? movement.magnitude / Time.deltaTime
                : 0f;

            turtleAnimator.SetFloat(
                Speed,
                speed,
                0.1f,
                Time.deltaTime
            );

            if (matchPlaybackToMovement && Time.deltaTime > 0f)
            {
                // Keep idle breathing at its authored pace; slow the steps during starts and stops.
                float targetPlayback = speed > 0.02f
                    ? Mathf.Clamp(speed / referenceWalkSpeed, 0.15f, 1.5f)
                    : 1f;
                float blend = 1f - Mathf.Exp(-Time.deltaTime / playbackBlendTime);
                turtleAnimator.speed = Mathf.Lerp(turtleAnimator.speed, targetPlayback, blend);
            }
        }
    }
}
