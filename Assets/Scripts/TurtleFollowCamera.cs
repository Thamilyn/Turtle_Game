using UnityEngine;

namespace TurtleGame
{
    [DisallowMultipleComponent]
    public sealed class TurtleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 7f, -8f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;
        [SerializeField] private float lookHeight = 0.4f;
        private Vector3 velocity;

        public Transform Target { get => target; set => target = value; }

        private void OnEnable()
        {
            velocity = Vector3.zero;
            if (target != null)
            {
                transform.position = target.position + offset;
                LookAtTarget();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            transform.position = Vector3.SmoothDamp(transform.position,
                target.position + offset, ref velocity, smoothTime);
            LookAtTarget();
        }

        private void LookAtTarget()
        {
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
