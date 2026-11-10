using UnityEngine;
using UnityEngine.Rendering;

namespace TurtleGame
{
    /// <summary>Leaves world-space sand puffs behind actual grounded movement.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(TurtlePlayerController))]
    public sealed class TurtleSandTrail : MonoBehaviour
    {
        [SerializeField] private Material sandMaterial;
        [SerializeField, Min(1f)] private float particlesPerMeter = 18f;
        [SerializeField, Min(0.1f)] private float lifetime = 0.22f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.78f;

        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private TurtlePlayerController player;
        private CharacterController character;
        private ParticleSystem particles;
        private Vector3 previousPosition;
        private float emissionRemainder;
        private readonly Transform[] feet = new Transform[4];
        private Transform boundVisual;
        private float nextBindingTime;
        private int nextFoot;

        private void Awake()
        {
            player = GetComponent<TurtlePlayerController>();
            character = GetComponent<CharacterController>();
            if (sandMaterial == null)
                sandMaterial = Resources.Load<Material>("TurtleSandTrail");
            if (sandMaterial == null)
            {
                Debug.LogWarning("Missing TurtleSandTrail material.", this);
                enabled = false;
                return;
            }

            var source = new GameObject("Turtle sand trail");
            source.transform.SetParent(transform, false);
            particles = source.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 256;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.1f));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            }, new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(1f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
            color.color = gradient;
            var renderer = source.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sandMaterial;
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
        }

        private void OnDisable()
        {
            emissionRemainder = 0f;
            if (particles != null)
                particles.Clear();
        }

        private void LateUpdate()
        {
            Vector3 previous = previousPosition;
            Vector3 displacement = transform.position - previous;
            previousPosition = transform.position;
            float dt = Time.deltaTime;
            if (dt <= 0f || particles == null)
                return;
            displacement.y = 0f;
            float distance = displacement.magnitude;
            // Input against a wall, falling and teleports must not kick up sand.
            if (!player.isActiveAndEnabled || !character.enabled || !player.IsGrounded
                || distance < 0.0001f || distance > player.MoveSpeed * dt * 1.5f)
            {
                emissionRemainder = 0f;
                return;
            }

            BindFeet();
            Vector3 direction = displacement / distance;
            float radius = character.bounds.extents.x;
            float amount = emissionRemainder + distance * particlesPerMeter;
            int count = Mathf.Min(Mathf.FloorToInt(amount), 32);
            emissionRemainder = amount - Mathf.Floor(amount);
            Vector3 forward = player.Visual != null ? player.Visual.forward : direction;
            forward.y = 0f;
            forward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < count; i++)
            {
                int foot = nextFoot++ % feet.Length;
                // Use animated toe bones; simple replacement models get four lateral contact points.
                Vector3 contact = feet[foot] != null ? feet[foot].position
                    : transform.position + side * (foot % 2 == 0 ? -radius : radius) * 0.85f
                        + forward * (foot < 2 ? radius * 0.45f : -radius * 0.45f);
                contact.y = character.bounds.min.y;
                if (!TryGetGround(contact, out Vector3 ground))
                    continue;
                var puff = new ParticleSystem.EmitParams
                {
                    position = ground + Vector3.up * 0.025f
                        + side * Random.Range(-radius * 0.08f, radius * 0.08f),
                    velocity = -direction * Random.Range(0.02f, 0.06f)
                        + side * Random.Range(-0.04f, 0.04f)
                        + Vector3.up * Random.Range(0.02f, 0.04f),
                    startLifetime = lifetime * Random.Range(0.8f, 1.2f),
                    startSize = radius * Random.Range(0.3f, 0.55f),
                    startColor = new Color(1f, 1f, 1f, opacity),
                    rotation = Random.Range(0f, 360f)
                };
                particles.Emit(puff, 1);
            }
        }

        private void BindFeet()
        {
            if (boundVisual == player.Visual && Time.time < nextBindingTime)
                return;
            boundVisual = player.Visual;
            nextBindingTime = Time.time + 0.5f;
            System.Array.Clear(feet, 0, feet.Length);
            if (boundVisual == null)
                return;
            foreach (Transform bone in boundVisual.GetComponentsInChildren<Transform>())
            {
                switch (bone.name)
                {
                    case "Arm_3.L": case "aleta iz": feet[0] = bone; break;
                    case "Arm_3.R": case "aleta der": feet[1] = bone; break;
                    case "Foot_3.L": case "pata iz": feet[2] = bone; break;
                    case "Foot_3.R": case "pata iz001": feet[3] = bone; break;
                }
            }
        }

        private bool TryGetGround(Vector3 contact, out Vector3 ground)
        {
            int hits = Physics.RaycastNonAlloc(contact + Vector3.up * 0.4f, Vector3.down,
                groundHits, 0.85f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            ground = contact;
            bool foundGround = false;
            for (int i = 0; i < hits; i++)
            {
                var hit = groundHits[i];
                if (hit.transform.IsChildOf(transform) || hit.normal.y < 0.5f || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                ground = hit.point;
                foundGround = true;
            }
            return foundGround;
        }
    }
}
