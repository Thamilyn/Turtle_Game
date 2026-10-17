using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TurtleGame
{
    /// <summary>World-space WASD movement. The root owns physics; Visual owns the model.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class TurtlePlayerController : MonoBehaviour
    {
        [Header("Movimiento en el mundo")]
        [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
        [SerializeField, Min(0.01f)] private float acceleration = 12f;
        [SerializeField, Min(0.01f)] private float deceleration = 18f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;

        [Header("Gravedad")]
        [SerializeField] private float gravity = -20f;
        [SerializeField, Min(0.1f)] private float terminalFallSpeed = 30f;

        [Header("Modelo intercambiable")]
        [Tooltip("Contenedor Visual. Reemplaza su hijo PlaceholderCube por tu modelo.")]
        [SerializeField] private Transform visual;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;

        public Transform Visual { get => visual; set => visual = value; }
        public bool ReadKeyboard { get; set; } = true;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void OnDisable()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        private void Update()
        {
            if (ReadKeyboard)
                Move(ReadMovementInput(), Time.deltaTime);
        }

        /// <summary>Useful for keyboard, future input sources, or automated play checks.</summary>
        public void Move(Vector2 input, float deltaTime)
        {
            if (!isActiveAndEnabled || deltaTime <= 0f || !controller.enabled)
                return;

            // Prevent diagonals from moving faster; preserve analog input magnitude.
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 direction = new Vector3(input.x, 0f, input.y);
            float rate = input.sqrMagnitude > 0f ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity,
                direction * moveSpeed, rate * deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            verticalVelocity = Mathf.Max(verticalVelocity + gravity * deltaTime,
                -terminalFallSpeed);
            CollisionFlags collisions = controller.Move(
                (planarVelocity + Vector3.up * verticalVelocity) * deltaTime);

            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;

            // Rotate only the model, keeping the collider and camera orientation stable.
            if (visual != null && direction.sqrMagnitude > 0.001f)
            {
                Quaternion heading = Quaternion.LookRotation(direction, Vector3.up);
                visual.rotation = Quaternion.RotateTowards(visual.rotation,
                    heading, turnSpeed * deltaTime);
            }
        }

        private static Vector2 ReadMovementInput()
        {
            Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return input;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed) input.y += 1f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A)) input.x -= 1f;
            if (Input.GetKey(KeyCode.D)) input.x += 1f;
            if (Input.GetKey(KeyCode.S)) input.y -= 1f;
            if (Input.GetKey(KeyCode.W)) input.y += 1f;
#endif
            return input;
        }
    }
}
