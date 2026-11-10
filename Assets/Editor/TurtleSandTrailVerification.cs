using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    [InitializeOnLoad]
    public static class TurtleSandTrailVerification
    {
        private const string PendingKey = "TurtleGame.SandTrailVerification";
        private static IEnumerator checks;
        private static int lastFrame;
        private static double deadline;
        private static GameObject fixture;

        static TurtleSandTrailVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                    return;
                SessionState.SetBool(PendingKey, false);
                Time.captureDeltaTime = 1f / 60f;
                deadline = EditorApplication.timeSinceStartup + 90f;
                lastFrame = -1;
                checks = Verify();
                EditorApplication.update += Tick;
            };
        }

        [MenuItem("Turtle Game/Verificar estela de arena")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run from Edit Mode.");
            if (Application.isBatchMode)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Sand verification timed out.");
                if (Time.frameCount == lastFrame)
                    return;
                lastFrame = Time.frameCount;
                if (!checks.MoveNext()) Finish(true, "PASS: shader, automatic setup, movement, world space, stopping, teleport and disable.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Verify()
        {
            var material = Resources.Load<Material>("TurtleSandTrail");
            Assert(material != null && material.shader.isSupported, "Sand shader is supported");
            ShaderUtil.CompilePass(material, 0, true);
            Assert(!ShaderUtil.ShaderHasError(material.shader), "Sand shader compiles");
            fixture = new GameObject("Sand verification fixture");
            fixture.transform.position = new Vector3(2000f, 100f, 2000f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(fixture.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            var root = new GameObject("Verification turtle");
            root.transform.SetParent(fixture.transform, false);
            var character = root.AddComponent<CharacterController>();
            character.height = 0.8f;
            character.radius = 0.35f;
            character.center = Vector3.up * 0.4f;
            var player = root.AddComponent<TurtlePlayerController>();
            player.ReadKeyboard = false;
            Assert(root.GetComponent<TurtleSandTrail>() != null, "Automatically attached");
            var particles = root.GetComponentInChildren<ParticleSystem>();
            Assert(particles != null && particles.main.simulationSpace == ParticleSystemSimulationSpace.World,
                "Dust stays in world space");
            Physics.SyncTransforms();
            for (int i = 0; i < 30; i++)
            {
                player.Move(Vector2.zero, 1f / 60f);
                yield return null;
            }
            Assert(particles.particleCount == 0, "Idle emits no dust");
            for (int i = 0; i < 60; i++)
            {
                player.Move(Vector2.up, 1f / 60f);
                yield return null;
            }
            Assert(particles.particleCount > 0, "Grounded travel emits dust");
            Assert(particles.GetComponent<ParticleSystemRenderer>().renderMode
                == ParticleSystemRenderMode.HorizontalBillboard, "Dust lies along the ground");
            var buffer = new ParticleSystem.Particle[256];
            int count = particles.GetParticles(buffer);
            for (int i = 0; i < count; i++)
            {
                Assert(Mathf.Abs(buffer[i].position.x - root.transform.position.x) > 0.2f,
                    "Dust originates at lateral feet, away from the tail");
                Assert(buffer[i].position.y < fixture.transform.position.y + 0.06f,
                    "Dust stays close to ground");
                Assert(buffer[i].startSize < 0.25f, "Dust puffs stay small");
            }
            for (int i = 0; i < 120; i++)
            {
                player.Move(Vector2.zero, 1f / 60f);
                yield return null;
            }
            Assert(particles.particleCount == 0, "Dust fades after stopping");
            character.enabled = false;
            root.transform.position += Vector3.forward * 4f;
            character.enabled = true;
            yield return null;
            yield return null;
            Assert(particles.particleCount == 0, "Teleport emits no dust");
            root.GetComponent<TurtleSandTrail>().enabled = false;
            Assert(particles.particleCount == 0, "Disabling clears particles");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("PASS: " + message);
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            checks = null;
            Time.captureDeltaTime = 0f;
            if (fixture != null) Object.Destroy(fixture);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/sand-trail-verification-result.txt", message);
            Debug.Log(message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
