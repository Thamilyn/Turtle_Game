using System.Collections.Generic;
using UnityEngine;

namespace TurtleGame
{
    /// <summary>A floating moon that waits ahead along the moonlit beach route.</summary>
    [DisallowMultipleComponent]
    public sealed class MoonFairyGuide : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float speed = 2.4f;
        [SerializeField, Min(1f)] private float waitDistance = 4f;
        [SerializeField, Min(0.3f)] private float hoverHeight = 1.6f;
        [SerializeField, Min(0.4f)] private float obstacleClearance = 0.65f;
        [SerializeField] private Material moonMaterial;
        [SerializeField] private Color glowColor = new Color(0.4f, 0.8f, 1f);
        [SerializeField, Min(0f)] private float groundLightIntensity = 3.5f;
        [Header("Luces de orientación")]
        [SerializeField, Min(1f)] private float guidanceDistance = 6f;
        [SerializeField, Min(1f)] private float guidanceDelay = 5f;
        [Header("Progreso siguiendo a la luna")]
        [SerializeField, Min(3f)] private float barrierSpacing = 12f;
        private MoonProgressBarriers progressBarriers;
        private MoonGuidanceParticles guidance;
        private GameObject fairy;
        private Material glowMaterial;
        private Material haloMaterial;
        private Terrain beach;
        private Light glow;
        private Vector3 basePosition;
        private int waypoint;
        private MoonGuidePath navigation;
        private readonly List<Vector2> detour = new List<Vector2>();
        private int detourPoint;
        private float refreshTimer;
        private bool routeComplete;
        private Vector2 travelDirection;
        private float retryTimer;

        public Material MoonMaterial { get => moonMaterial; set => moonMaterial = value; }
        public bool HasReachedRoad => routeComplete;

        // Side steps take the guide around the three fixed route obstacles.
        private static readonly Vector2[] Route =
        {
            new Vector2(2f, 8f), new Vector2(4f, 21f),
            new Vector2(7.5f, 28f), new Vector2(5f, 35f),
            new Vector2(0f, 49f), new Vector2(-3.5f, 59f),
            new Vector2(-3f, 68f), new Vector2(-7.8f, 85f),
            new Vector2(-5f, 94f), new Vector2(0f, 110f),
            new Vector2(0f, 122f)
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

            ConfigureMoonlight();
            waypoint = 0;
            routeComplete = false;
            travelDirection = Vector2.zero;
            retryTimer = 0f;
            detour.Clear();
            detourPoint = 0;
            refreshTimer = 0f;
            float playerRadius = target.TryGetComponent<CharacterController>(out var character)
                ? character.radius * Mathf.Max(target.lossyScale.x, target.lossyScale.z) + 0.2f : 0.65f;
            navigation = new MoonGuidePath(beach, target, Mathf.Max(obstacleClearance, playerRadius));
            navigation.Refresh(gameObject.scene);
            basePosition = PositionAt(Route[0]);
            // Recover gracefully if the first waypoint was covered while editing the beach.
            if (!navigation.IsPointClear(Route[0]))
            {
                Vector2 start = new Vector2(target.position.x, target.position.z);
                if (!navigation.TryBuild(start, Route[0], detour))
                    return;
                basePosition = PositionAt(start);
            }
            fairy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fairy.name = "Guiding moon";
            var sphere = fairy.GetComponent<Collider>();
            sphere.enabled = false;
            Destroy(sphere);
            fairy.transform.localScale = Vector3.one * 0.4f;
            fairy.transform.position = basePosition;
            var moonRenderer = fairy.GetComponent<Renderer>();
            if (moonMaterial != null)
            {
                moonRenderer.sharedMaterial = moonMaterial;
            }
            else
            {
                glowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                glowMaterial.color = glowColor;
                glowMaterial.EnableKeyword("_EMISSION");
                glowMaterial.SetColor("_EmissionColor", glowColor * 1.5f);
                moonRenderer.sharedMaterial = glowMaterial;
            }
            moonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moonRenderer.receiveShadows = false;
            CreateHalo();
            glow = fairy.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.range = 10f;
            glow.intensity = groundLightIntensity;
            glow.shadows = LightShadows.Soft;
            glow.bounceIntensity = 0f;
            Color guidanceColor = moonMaterial != null && moonMaterial.HasProperty("_HaloColor")
                ? moonMaterial.GetColor("_HaloColor") : glowColor;
            guidance = new MoonGuidanceParticles(fairy, beach, target, navigation, guidanceColor);
            var barriers = new GameObject("Moon progress barriers");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(barriers, gameObject.scene);
            progressBarriers = barriers.AddComponent<MoonProgressBarriers>();
            progressBarriers.Initialize(beach, basePosition.z, Route[Route.Length - 1], barrierSpacing);
        }

        private void CreateHalo()
        {
            if (moonMaterial == null || !moonMaterial.HasProperty("_HaloStrength"))
                return;

            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "Moon glow halo";
            halo.transform.SetParent(fairy.transform, false);
            halo.transform.localScale = Vector3.one * 3f;
            var collider = halo.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            haloMaterial = new Material(moonMaterial);
            haloMaterial.EnableKeyword("_MOON_HALO");
            haloMaterial.SetOverrideTag("RenderType", "Transparent");
            haloMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            haloMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            haloMaterial.SetFloat("_ZWrite", 0f);
            haloMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            haloMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            var renderer = halo.GetComponent<Renderer>();
            renderer.sharedMaterial = haloMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Apply at Play startup as well, so an already-open scene receives the lighting update.
        private void ConfigureMoonlight()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.24f, 0.32f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0f;
            Light moonlight = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.name == "Moonlight" || light.name == "Cool night fill")
                    {
                        moonlight = light;
                        break;
                    }
                }
                if (moonlight != null)
                    break;
            }
            if (moonlight == null)
            {
                var source = new GameObject("Moonlight");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source, gameObject.scene);
                moonlight = source.AddComponent<Light>();
            }
            moonlight.gameObject.SetActive(true);
            moonlight.enabled = true;
            moonlight.type = LightType.Directional;
            moonlight.color = new Color(0.58f, 0.71f, 1f);
            moonlight.intensity = 0.55f;
            moonlight.shadows = LightShadows.Soft;
            moonlight.transform.rotation = Quaternion.Euler(28f, -130f, 0f);
            RenderSettings.sun = moonlight;
        }

        private Vector3 PositionAt(Vector2 point)
        {
            var position = new Vector3(point.x, 0f, point.y);
            position.y = beach.SampleHeight(position) + beach.transform.position.y + hoverHeight;
            return position;
        }

        private void Update()
        {
            Advance(Time.deltaTime, Time.time);
        }

        private void Advance(float deltaTime, float time)
        {
            if (fairy == null || target == null)
                return;
            if (deltaTime <= 0f)
                return;
            refreshTimer -= deltaTime;
            retryTimer -= deltaTime;
            if (refreshTimer <= 0f)
            {
                navigation.Refresh(gameObject.scene);
                refreshTimer = 0.75f;
                if (detourPoint < detour.Count && !navigation.IsSegmentClear(
                    new Vector2(basePosition.x, basePosition.z), detour[detourPoint]))
                    detour.Clear();
            }
            Vector2 separation = new Vector2(basePosition.x - target.position.x, basePosition.z - target.position.z);
            if (!routeComplete && separation.magnitude < waitDistance)
            {
                Vector2 position = new Vector2(basePosition.x, basePosition.z);
                if (detour.Count == 0 && retryTimer <= 0f)
                {
                    detourPoint = 0;
                    if (!navigation.TryBuild(position, Route[waypoint], detour))
                        retryTimer = 0.75f;
                }
                if (detourPoint < detour.Count)
                {
                    // Move on a smoothed, visible segment, never through a rock to cut a corner.
                    while (detourPoint + 1 < detour.Count &&
                        navigation.IsSegmentClear(position, detour[detourPoint + 1]))
                        detourPoint++;
                    Vector2 direction = (detour[detourPoint] - position).normalized;
                    travelDirection = Vector2.Lerp(travelDirection, direction,
                        1f - Mathf.Exp(-6f * deltaTime)).normalized;
                    float step = Mathf.Min(speed * deltaTime, Vector2.Distance(position, detour[detourPoint]));
                    Vector2 next = position + travelDirection * step;
                    if (step < speed * deltaTime || !navigation.IsSegmentClear(position, next) ||
                        !navigation.IsSegmentClear(next, detour[detourPoint]))
                        next = Vector2.MoveTowards(position, detour[detourPoint], speed * deltaTime);
                    if (navigation.IsSegmentClear(position, next))
                        basePosition = PositionAt(next);
                    if (Vector2.Distance(next, detour[detourPoint]) < 0.001f)
                    {
                        detourPoint++;
                        if (detourPoint == detour.Count)
                        {
                            detour.Clear();
                            if (waypoint < Route.Length - 1) waypoint++;
                            else routeComplete = true;
                        }
                    }
                }
            }
            fairy.transform.position = basePosition + new Vector3(Mathf.Sin(time * 1.7f) * 0.16f,
                Mathf.Sin(time * 2.8f) * 0.12f, 0f);
            glow.intensity = groundLightIntensity * (1f + Mathf.Sin(time * 1.5f) * 0.06f);
            guidance.Advance(deltaTime, time, basePosition, guidanceDistance, guidanceDelay);
            progressBarriers.ReleaseReached(basePosition.z);
        }

        private void OnDisable()
        {
            guidance?.Dispose();
            guidance = null;
            if (progressBarriers != null)
            {
                progressBarriers.gameObject.SetActive(false);
                Destroy(progressBarriers.gameObject);
                progressBarriers = null;
            }
            if (fairy != null) Destroy(fairy);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }
    }
}
