using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurtleGame
{
    /// <summary>A soft stream of fireflies along a walkable path back to the waiting moon.</summary>
    internal sealed class MoonGuidanceParticles
    {
        private const int Capacity = 256;
        private const float VisibleLength = 24f;
        private const float MoteSpacing = 0.3f;
        private const float DriftSpeed = 0.2f;
        private readonly Terrain terrain;
        private readonly Transform player;
        private readonly MoonGuidePath navigation;
        private readonly ParticleSystem particles;
        private readonly ParticleSystem moonTrail;
        private readonly Material material;
        private readonly Texture2D texture;
        private readonly Color color;
        private readonly List<Vector2> path = new List<Vector2>();
        private readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[Capacity];
        private float refreshTimer;
        private float stalledTime;
        private float bestDistance = float.PositiveInfinity;
        private float visibility;
        private Vector2 previousPlayerPosition;

        public MoonGuidanceParticles(GameObject moon, Terrain terrain, Transform player,
            MoonGuidePath navigation, Color color)
        {
            this.terrain = terrain;
            this.player = player;
            this.navigation = navigation;
            this.color = color;
            previousPlayerPosition = new Vector2(player.position.x, player.position.z);
            var source = new GameObject("Moon guidance fireflies");
            source.transform.SetParent(moon.transform, false);
            particles = source.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = Capacity;
            main.startSpeed = 0f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Pause();

            // A radial sprite avoids hard square particles and needs no imported texture.
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "Moon firefly soft glow";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    // A delicate pinprick with a faint halo, rather than a large luminous cloud.
                    float edge = Mathf.Clamp01((1f - radius) * 5f);
                    float alpha = (Mathf.Exp(-radius * radius * 7f) * 0.85f +
                        Mathf.Pow(Mathf.Clamp01(1f - radius), 2f) * 0.2f) * edge;
                    pixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            // A referenced Resources material keeps the shader and its transparent
            // variant in player builds, where an unreferenced Shader.Find is stripped.
            material = new Material(Resources.Load<Material>("MoonGuidanceGlow"));
            material.name = "Moon guidance glow";
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(2f, 2f, 2f, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            moonTrail = CreateMoonTrail(moon);
        }

        private ParticleSystem CreateMoonTrail(GameObject moon)
        {
            var source = new GameObject("Moon stardust trail");
            source.transform.SetParent(moon.transform, false);
            var system = source.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.19f);
            main.startColor = new Color(color.r, color.g, color.b, 0.85f);
            main.gravityModifier = 0f;
            var emission = system.emission;
            emission.rateOverTime = 5f;
            emission.rateOverDistance = 7f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Simulated alongside the guide so pauses and verification steps stay in sync.
            system.Play();
            system.Pause();
            return system;
        }

        public void Advance(float deltaTime, float time, Vector3 moon, float helpDistance, float helpDelay)
        {
            if (deltaTime <= 0f)
                return;

            // The moon's own stardust remains visible even when the recovery path is hidden.
            moonTrail.Simulate(deltaTime, false, false, false);

            Vector2 start = new Vector2(player.position.x, player.position.z);
            Vector2 destination = new Vector2(moon.x, moon.z);
            float distance = Vector2.Distance(start, destination);
            float approach = Vector2.Dot(start - previousPlayerPosition, (destination - start).normalized);
            previousPlayerPosition = start;
            if (approach > deltaTime * 0.15f || distance < bestDistance - 0.25f || distance <= 2.5f)
            {
                stalledTime = 0f;
                bestDistance = distance;
            }
            else
            {
                stalledTime += deltaTime;
                // Reset the progress reference when the moon or the player moves farther away.
                if (distance > bestDistance + 0.75f)
                    bestDistance = distance;
            }

            bool needsHelp = distance >= helpDistance || (distance > 2.5f && stalledTime >= helpDelay);
            if (player.TryGetComponent<TurtlePlayerController>(out var controller) && !controller.isActiveAndEnabled)
                needsHelp = false;
            visibility = Mathf.MoveTowards(visibility, needsHelp ? 1f : 0f, deltaTime * 0.65f);
            if (visibility <= 0f)
            {
                particles.SetParticles(buffer, 0);
                refreshTimer = 0f;
                return;
            }

            refreshTimer -= deltaTime;
            if (refreshTimer <= 0f)
            {
                navigation.TryBuild(start, destination, path);
                refreshTimer = 0.75f;
            }
            // The player may have stepped behind a rock since the last path refresh.
            if (path.Count == 0 || !navigation.IsSegmentClear(start, path[0]))
            {
                particles.SetParticles(buffer, 0);
                refreshTimer = 0f;
                return;
            }

            float length = 0f;
            Vector2 previous = start;
            foreach (Vector2 point in path)
            {
                length += Vector2.Distance(previous, point);
                previous = point;
            }
            float visible = Mathf.Min(length, VisibleLength);
            int count = 0;
            // Stable identities let each mote drift and twinkle independently without popping at wraparound.
            float travel = time * DriftSpeed;
            int first = Mathf.CeilToInt((0.8f - travel) / MoteSpacing);
            for (int index = first; count < Capacity; index++)
            {
                float seed = Mathf.Repeat(index * 0.618034f, 1f);
                float along = index * MoteSpacing + travel + (seed - 0.5f) * 0.08f;
                if (along >= visible)
                    break;
                float remaining = along;
                previous = start;
                foreach (Vector2 point in path)
                {
                    float segment = Vector2.Distance(previous, point);
                    if (remaining <= segment)
                    {
                        Vector2 position = Vector2.Lerp(previous, point, remaining / Mathf.Max(segment, 0.001f));
                        // Larger near the turtle, tapering gently into tiny lights in the distance.
                        float sizeDistance = Mathf.Max(1f, Mathf.Min(visible, 12f));
                        float distanceBlend = Mathf.SmoothStep(0f, 1f,
                            Vector2.Distance(start, position) / sizeDistance);
                        float distanceSize = Mathf.Lerp(0.25f, 0.08f, distanceBlend);
                        float fade = Mathf.SmoothStep(0f, 1f, (along - 0.6f) / 1.5f) *
                            Mathf.SmoothStep(0f, 1f, (visible - along) / 2f);
                        for (int mote = 0; mote < 2 && count < Capacity; mote++)
                        {
                            float identity = Mathf.Repeat(seed + mote * 0.381966f, 1f);
                            float phase = time * 0.45f + identity * Mathf.PI * 2f;
                            float twinkle = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(
                                time * Mathf.Lerp(0.65f, 1.05f, identity) + identity * 19f), 2f);
                            Vector3 world = new Vector3(position.x, 0f, position.y);
                            // Tight, slow spirals keep the guiding ribbon compact.
                            world += new Vector3(Mathf.Sin(phase) * 0.025f, 0f, Mathf.Cos(phase) * 0.025f);
                            world.y = terrain.SampleHeight(world) + terrain.transform.position.y +
                                0.4f + mote * 0.05f + Mathf.Sin(phase) * 0.04f;
                            float size = distanceSize * Mathf.Lerp(0.85f, 1.1f, identity) *
                                (0.9f + twinkle * 0.1f);
                            buffer[count++] = new ParticleSystem.Particle
                            {
                                position = world,
                                startColor = new Color(color.r, color.g, color.b,
                                    visibility * fade * Mathf.Lerp(0.45f, 0.95f, twinkle)),
                                startSize = size,
                                startLifetime = 1f,
                                remainingLifetime = 1f
                            };
                        }
                        break;
                    }
                    remaining -= segment;
                    previous = point;
                }
            }
            particles.SetParticles(buffer, count);
        }

        public void Dispose()
        {
            if (particles != null) Object.Destroy(particles.gameObject);
            if (moonTrail != null) Object.Destroy(moonTrail.gameObject);
            Object.Destroy(material);
            Object.Destroy(texture);
        }
    }
}
