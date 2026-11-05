using UnityEngine;

namespace TurtleGame
{
    /// <summary>Animates the separate flippers of tortuguita without changing player physics.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TurtlePlayerController))]
    public sealed class TurtleWalkAnimation : MonoBehaviour
    {
        [Header("Pasos")]
        [SerializeField, Min(0.1f)] private float strideLength = 1.2f;
        [SerializeField, Range(0f, 45f)] private float flipperSweep = 22f;
        [SerializeField, Range(0f, 30f)] private float flipperLift = 12f;
        [SerializeField, Min(0.01f)] private float blendTime = 0.15f;

        [Header("Balanceo del modelo")]
        [SerializeField, Min(0f)] private float bodyBob = 0.008f;
        [SerializeField, Range(0f, 10f)] private float headNod = 3f;
        [SerializeField, Range(0f, 20f)] private float tailSwing = 6f;

        private TurtlePlayerController player;
        private CharacterController character;
        private Transform boundVisual;
        private Transform model;
        private int visualChildCount;
        private Vector3 modelRestPosition;
        private Vector3 previousPosition;
        private float phase;
        private float blend;
        private float nextBindingTime;
        private Part[] flippers;
        private Part head;
        private Part leftEye;
        private Part rightEye;
        private Part tail;

        private sealed class Part
        {
            public readonly Transform Transform;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Pivot;
            public readonly float Side;

            public Part(Transform target, Vector3 pivot)
            {
                Transform = target;
                Position = target.localPosition;
                Rotation = target.localRotation;
                Pivot = pivot;
                Side = Mathf.Sign(Position.x - pivot.x);
            }

            public void Apply(Quaternion offset)
            {
                if (Transform == null)
                    return;
                Transform.localPosition = Pivot + offset * (Position - Pivot);
                Transform.localRotation = offset * Rotation;
            }
        }

        private void Awake()
        {
            player = GetComponent<TurtlePlayerController>();
            character = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            BindModel();
        }

        private void OnDisable()
        {
            RestorePose();
            phase = 0f;
            blend = 0f;
        }

        private void LateUpdate()
        {
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            if (player.Visual != boundVisual || (boundVisual != null
                && (boundVisual.childCount != visualChildCount
                    || (model == null && Time.unscaledTime >= nextBindingTime)
                    || (model != null && !model.IsChildOf(boundVisual)))))
                BindModel();
            if (model == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            if (!player.isActiveAndEnabled || !character.enabled)
            {
                RestorePose();
                blend = 0f;
                return;
            }

            displacement.y = 0f;
            float distance = displacement.magnitude;
            // Ignore teleports and animate only actual grounded travel, including braking.
            bool walking = player.IsGrounded && distance > 0.0001f
                && distance <= player.MoveSpeed * dt * 1.5f;
            float targetBlend = walking ? Mathf.Clamp01(distance / dt / player.MoveSpeed) : 0f;
            blend = Mathf.MoveTowards(blend, targetBlend, dt / blendTime);
            if (walking)
                phase = Mathf.Repeat(phase + distance / strideLength * Mathf.PI * 2f, Mathf.PI * 2f);
            if (blend == 0f)
                phase = 0f;
            ApplyPose();
        }

        private void BindModel()
        {
            RestorePose();
            boundVisual = player.Visual;
            visualChildCount = boundVisual != null ? boundVisual.childCount : 0;
            model = null;
            flippers = null;
            head = leftEye = rightEye = tail = null;
            phase = blend = 0f;
            nextBindingTime = Time.unscaledTime + 0.5f;
            if (boundVisual == null)
                return;

            // Scope bindings to the model that owns all four limbs; other models stay untouched.
            foreach (Transform candidate in boundVisual.GetComponentsInChildren<Transform>())
            {
                Transform frontLeft = candidate.Find("aleta iz");
                Transform frontRight = candidate.Find("aleta der");
                Transform rearLeft = candidate.Find("pata iz");
                Transform rearRight = candidate.Find("pata iz001");
                Transform headTransform = candidate.Find("cabeza");
                Transform tailTransform = candidate.Find("cola");
                if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null
                    || headTransform == null || tailTransform == null)
                    continue;

                model = candidate;
                modelRestPosition = model.localPosition;
                float centerX = headTransform.localPosition.x;
                flippers = new[]
                {
                    Flipper(frontLeft, centerX), Flipper(frontRight, centerX),
                    Flipper(rearLeft, centerX), Flipper(rearRight, centerX)
                };
                Vector3 neck = headTransform.localPosition + Vector3.forward * 0.06f;
                head = new Part(headTransform, neck);
                Transform eye = candidate.Find("ojoiz");
                if (eye != null) leftEye = new Part(eye, neck);
                eye = candidate.Find("ojod");
                if (eye != null) rightEye = new Part(eye, neck);
                tail = new Part(tailTransform, tailTransform.localPosition);
                break;
            }
        }

        private static Part Flipper(Transform limb, float centerX)
        {
            Vector3 pivot = limb.localPosition;
            pivot.x = Mathf.Lerp(pivot.x, centerX, 0.4f);
            return new Part(limb, pivot);
        }

        private void ApplyPose()
        {
            for (int i = 0; i < flippers.Length; i++)
            {
                // Front left + rear right alternate with the other diagonal pair.
                float step = phase + (i == 1 || i == 2 ? Mathf.PI : 0f);
                Part limb = flippers[i];
                float sweep = Mathf.Sin(step) * flipperSweep * limb.Side * blend;
                float lift = Mathf.Max(0f, Mathf.Cos(step)) * flipperLift * limb.Side * blend;
                limb.Apply(Quaternion.Euler(0f, sweep, lift));
            }

            float bob = (1f - Mathf.Cos(phase * 2f)) * 0.5f * bodyBob * blend;
            model.localPosition = modelRestPosition + Vector3.up * bob;
            Quaternion nod = Quaternion.Euler(Mathf.Sin(phase * 2f) * headNod * blend, 0f, 0f);
            head.Apply(nod);
            leftEye?.Apply(nod);
            rightEye?.Apply(nod);
            tail.Apply(Quaternion.Euler(0f, Mathf.Sin(phase) * tailSwing * blend, 0f));
        }

        private void RestorePose()
        {
            if (model != null)
                model.localPosition = modelRestPosition;
            if (flippers != null)
                foreach (Part limb in flippers) limb.Apply(Quaternion.identity);
            head?.Apply(Quaternion.identity);
            leftEye?.Apply(Quaternion.identity);
            rightEye?.Apply(Quaternion.identity);
            tail?.Apply(Quaternion.identity);
        }
    }
}
