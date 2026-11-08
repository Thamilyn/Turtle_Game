
using UnityEngine;

namespace TurtleGame
{
    [RequireComponent(typeof(CharacterController))]
    public class TurtleAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator turtleAnimator;

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
        }
    }
}
