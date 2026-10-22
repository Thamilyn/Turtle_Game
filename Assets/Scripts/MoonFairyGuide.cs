using UnityEngine;

namespace TurtleGame
{
    /// <summary>A small floating guide that waits ahead along the beach route.</summary>
    [DisallowMultipleComponent]
    public sealed class MoonFairyGuide : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float speed = 2.4f;
        [SerializeField, Min(1f)] private float waitDistance = 4f;
        [SerializeField, Min(0.3f)] private float hoverHeight = 1.6f;
        [SerializeField] private Color glowColor = new Color(0.4f, 0.8f, 1f);
        private GameObject fairy;
        private Material glowMaterial;
        private Material trailMaterial;
        private Terrain beach;
        private Light glow;
        private Vector3 basePosition;
        private int waypoint;
        // Side steps take the guide around the three fixed route obstacles.
        private static readonly Vector2[] Route =
        {
            new Vector2(2f, 8f), new Vector2(4f, 21f),
            new Vector2(7.5f, 28f), new Vector2(5f, 35f),
            new Vector2(0f, 49f), new Vector2(-3.5f, 59f),
            new Vector2(-3f, 68f), new Vector2(-7.8f, 85f),
            new Vector2(-5f, 94f), new Vector2(0f, 110f),
            new Vector2(0f, 120f)
        };

        private void OnEnable()
        {
            if (target == null && TryGetComponent<TurtleFollowCamera>(out var follow))
                target = follow.Target;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.TryGetComponent<Terrain>(out var candidate))
                    beach = candidate;
            if (target == null || beach == null)
                return;

            waypoint = 0;
            basePosition = PositionAt(Route[0]);
            fairy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fairy.name = "Moon fairy - guiding glow";
            Destroy(fairy.GetComponent<Collider>());
            fairy.transform.localScale = Vector3.one * 0.14f;
            fairy.transform.position = basePosition;
            glowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glowMaterial.color = glowColor;
            glowMaterial.EnableKeyword("_EMISSION");
            glowMaterial.SetColor("_EmissionColor", glowColor * 4f);
            fairy.GetComponent<Renderer>().sharedMaterial = glowMaterial;
            glow = fairy.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.range = 3f;
            glow.intensity = 1.2f;
            var trail = fairy.AddComponent<TrailRenderer>();
            trailMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            trailMaterial.SetFloat("_Surface", 1f);
            trailMaterial.SetFloat("_Blend", 0f);
            trailMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            trailMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            trailMaterial.SetFloat("_ZWrite", 0f);
            trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trailMaterial.renderQueue = 3000;
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.8f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0f;
            trail.startColor = glowColor;
            trail.endColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            trail.minVertexDistance = 0.03f;
        }

        private Vector3 PositionAt(Vector2 point)
        {
            var position = new Vector3(point.x, 0f, point.y);
            position.y = beach.SampleHeight(position) + beach.transform.position.y + hoverHeight;
            return position;
        }

        private void Update()
        {
            if (fairy == null || target == null)
                return;
            Vector2 separation = new Vector2(basePosition.x - target.position.x, basePosition.z - target.position.z);
            if (separation.magnitude < waitDistance)
            {
                Vector3 next = PositionAt(Route[waypoint]);
                basePosition = Vector3.MoveTowards(basePosition, next, speed * Time.deltaTime);
                if (Vector3.Distance(basePosition, next) < 0.05f && waypoint < Route.Length - 1)
                    waypoint++;
            }
            float time = Time.time;
            fairy.transform.position = basePosition + new Vector3(Mathf.Sin(time * 1.7f) * 0.16f,
                Mathf.Sin(time * 2.8f) * 0.12f, 0f);
            glow.intensity = 1.2f + Mathf.Sin(time * 3f) * 0.25f;
        }

        private void OnDisable()
        {
            if (fairy != null) Destroy(fairy);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (trailMaterial != null) Destroy(trailMaterial);
        }
    }
}
