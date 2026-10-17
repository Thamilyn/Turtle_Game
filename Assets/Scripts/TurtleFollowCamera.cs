using UnityEngine;

namespace TurtleGame
{
    [DisallowMultipleComponent]
    public sealed class TurtleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.621f, -1.281f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;
        [SerializeField] private float lookHeight = 0.43298f;
        private Vector3 velocity;

        public Transform Target { get => target; set => target = value; }

        private void OnEnable()
        {
            velocity = Vector3.zero;
            if (target != null)
            {
                transform.position = target.position + offset;
                ApplyViewingAngle();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            transform.position = Vector3.SmoothDamp(transform.position,
                target.position + offset, ref velocity, smoothTime);
            ApplyViewingAngle();
        }

        private void ApplyViewingAngle()
        {
            // Keep the reference angle while the follow position eases behind movement.
            Vector3 direction = Vector3.up * lookHeight - offset;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
