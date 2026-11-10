using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    [InitializeOnLoad]
    public static class MoonGuidanceParticlesVerification
    {
        private const string PendingKey = "TurtleGame.GuidanceParticlesVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static MoonGuidanceParticlesVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Turtle Game/Verificar partículas de orientación")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run particle verification from Edit Mode.");
            if (Application.isBatchMode)
                EditorSceneManager.OpenScene("Assets/Scenes/MoonBeach.unity");
            else if (Object.FindFirstObjectByType<MoonFairyGuide>() == null)
                throw new InvalidOperationException("Open a beach scene with a moon guide first.");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;
            SessionState.SetBool(PendingKey, false);
            EditorApplication.delayCall += Verify;
        }

        private static void Verify()
        {
            bool success = false;
            string result;
            try
            {
                var guide = Object.FindFirstObjectByType<MoonFairyGuide>();
                var target = (Transform)typeof(MoonFairyGuide).GetField("target", Private).GetValue(guide);
                var moon = (GameObject)typeof(MoonFairyGuide).GetField("fairy", Private).GetValue(guide);
                var advance = typeof(MoonFairyGuide).GetMethod("Advance", Private);
                var controller = target.GetComponent<TurtlePlayerController>();
                controller.ReadKeyboard = false;
                controller.enabled = true;
                target.GetComponent<CharacterController>().enabled = false;
                Time.timeScale = 0f;
                Vector3 destination = (Vector3)typeof(MoonFairyGuide).GetField("basePosition", Private).GetValue(guide);
                var particles = moon.GetComponentInChildren<ParticleSystem>();
                Assert(particles != null, "Moon creates guidance particles automatically");
                target.position = destination + Vector3.back * 7f;
                for (int i = 0; i < 120; i++) advance.Invoke(guide, new object[] { 1f / 60f, i / 60f });
                Assert(particles.particleCount > 0, "A lost player receives a visible stream");
                Assert(moon.GetComponent<TrailRenderer>() == null, "The blue ribbon has been removed");
                var moonTrail = moon.transform.Find("Moon stardust trail").GetComponent<ParticleSystem>();
                Assert(moonTrail.particleCount > 0, "The moon leaves stardust independently of recovery guidance");
                Assert(moonTrail.GetComponent<ParticleSystemRenderer>().sharedMaterial ==
                    particles.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                    "Moon trail and recovery particles share their glow and texture");
                var buffer = new ParticleSystem.Particle[128];
                int count = particles.GetParticles(buffer);
                Vector3 before = buffer[0].position;
                advance.Invoke(guide, new object[] { 0f, 100f });
                particles.GetParticles(buffer);
                Assert(Vector3.Distance(before, buffer[0].position) < 0.0001f, "Pause freezes the stream");
                foreach (var particle in buffer)
                {
                    if (particle.remainingLifetime <= 0f) continue;
                    Assert(particle.position.z > target.position.z && particle.position.z < destination.z,
                        "Motes connect the player to the moon");
                }

                // Insert a tall obstacle directly between player and moon, then inspect the detour.
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.transform.position = Vector3.Lerp(target.position, destination, 0.5f);
                obstacle.transform.localScale = new Vector3(2f, 8f, 1f);
                Physics.SyncTransforms();
                for (int i = 0; i < 90; i++) advance.Invoke(guide, new object[] { 1f / 60f, 2f + i / 60f });
                count = particles.GetParticles(buffer);
                Assert(count > 0, "Guidance finds a detour around a blocking obstacle");
                Bounds bounds = obstacle.GetComponent<Collider>().bounds;
                for (int i = 0; i < count; i++)
                    Assert(!bounds.Contains(buffer[i].position), "Lights stay outside the obstacle");
                obstacle.SetActive(false);
                Object.Destroy(obstacle);

                // Keep the moon stationary to isolate fading as the player resumes following it.
                typeof(MoonFairyGuide).GetField("routeComplete", Private).SetValue(guide, true);
                target.position = destination + Vector3.back * 2f;
                for (int i = 0; i < 120; i++) advance.Invoke(guide, new object[] { 1f / 60f, 4f + i / 60f });
                Assert(particles.particleCount == 0, "Lights fade once the player catches up");
                var guidance = typeof(MoonFairyGuide).GetField("guidance", Private).GetValue(guide);
                var updateParticles = guidance.GetType().GetMethod("Advance");
                Vector3 followingStart = target.position;
                for (int i = 0; i < 420; i++)
                {
                    target.position = followingStart + Vector3.forward * (i + 1) * 0.01f;
                    updateParticles.Invoke(guidance, new object[] {
                        1f / 60f, 6f + i / 60f, target.position + Vector3.forward * 3f, 6f, 5f });
                }
                Assert(particles.particleCount == 0, "Keeping pace with the moon does not trigger help");
                target.position = followingStart;
                var navigation = typeof(MoonFairyGuide).GetField("navigation", Private).GetValue(guide);
                var isClear = navigation.GetType().GetMethod("IsSegmentClear");
                bool found = false;
                for (int angle = 0; angle < 360; angle += 30)
                {
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.back * 3f;
                    Vector3 candidate = destination + offset;
                    if (!(bool)isClear.Invoke(navigation, new object[] {
                        new Vector2(candidate.x, candidate.z), new Vector2(destination.x, destination.z) }))
                        continue;
                    target.position = candidate;
                    found = true;
                    break;
                }
                Assert(found, "A nearby walkable location is available for the stall check");
                for (int i = 0; i < 390; i++) advance.Invoke(guide, new object[] { 1f / 60f, 6f + i / 60f });
                Assert(particles.particleCount > 0, "Lights also help a nearby player who stops following");

                // Capture the stream from the gameplay camera for visual inspection.
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    canvas.gameObject.SetActive(false);
                target.position = destination + Vector3.back * 7f;
                for (int i = 0; i < 90; i++) advance.Invoke(guide, new object[] { 1f / 60f, 13f + i / 60f });
                var camera = guide.GetComponent<Camera>();
                camera.transform.position = target.position + new Vector3(3f, 4f, -5f);
                camera.transform.LookAt(Vector3.Lerp(target.position, destination, 0.55f));
                Directory.CreateDirectory("Logs");
                var render = new RenderTexture(1280, 720, 24);
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/moon-guidance-particles.png", image.EncodeToPNG());
                // Inspect a moving moon up close: old particles must stay behind in world space.
                moonTrail.Clear();
                for (int i = 0; i < 120; i++)
                {
                    moon.transform.position = destination + Vector3.right * (i * 0.02f);
                    updateParticles.Invoke(guidance, new object[] {
                        1f / 60f, 15f + i / 60f, moon.transform.position, 6f, 5f });
                }
                var tailBuffer = new ParticleSystem.Particle[96];
                int tailCount = moonTrail.GetParticles(tailBuffer);
                bool leavesParticlesBehind = false;
                for (int i = 0; i < tailCount; i++)
                    leavesParticlesBehind |= tailBuffer[i].position.x < moon.transform.position.x - 0.3f;
                Assert(leavesParticlesBehind, "Moving moon leaves individual particles behind in world space");
                camera.transform.position = moon.transform.position + new Vector3(0f, 0.6f, -3f);
                camera.transform.LookAt(moon.transform.position + Vector3.left * 0.65f);
                camera.Render();
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/moon-stardust-trail.png", image.EncodeToPNG());
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.Destroy(image);
                Object.Destroy(render);
                guide.enabled = false;
                success = true;
                result = "MOON_GUIDANCE_PARTICLES_PASS: distance, pause, direction, obstacle detour, recovery, stall and moon stardust trail checks passed.";
            }
            catch (Exception exception)
            {
                result = "MOON_GUIDANCE_PARTICLES_FAILED: " + exception;
            }
            Time.timeScale = 1f;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/moon-guidance-particles-result.txt", result);
            if (success) Debug.Log(result); else Debug.LogError(result);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
