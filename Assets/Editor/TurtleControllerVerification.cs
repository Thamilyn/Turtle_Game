using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurtleGame.Editor
{
    /// <summary>Batch Play Mode checks with real CharacterController collision movement.</summary>
    [InitializeOnLoad]
    public static class TurtleControllerVerification
    {
        private const string PendingKey = "TurtleGame.VerificationPending";
        private static IEnumerator checks;
        private static TurtlePlayerController player;
        private static int lastFrame = -1;
        private static int passed;
        private static double deadline;

        static TurtleControllerVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Run()
        {
            string scenePath = File.Exists("Assets/Scenes/TurtlePrototype.unity")
                ? "Assets/Scenes/TurtlePrototype.unity" : TurtlePrototypeBuilder.ScenePath;
            if (!File.Exists(scenePath))
                throw new InvalidOperationException("Generate the prototype before verification.");
            EditorSceneManager.OpenScene(scenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;
            SessionState.SetBool(PendingKey, false);
            Time.captureDeltaTime = 1f / 60f;
            player = UnityEngine.Object.FindFirstObjectByType<TurtlePlayerController>();
            if (player == null)
            {
                Finish(false, "Player missing from scene.");
                return;
            }
            player.ReadKeyboard = false;
            passed = 0;
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 120;
            checks = Verify();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(false, "Verification timed out.");
                return;
            }
            if (Time.frameCount == lastFrame)
                return;
            lastFrame = Time.frameCount;
            try
            {
                if (!checks.MoveNext())
                    Finish(true, passed + " behavior checks passed.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Verify()
        {
            const float dt = 1f / 60f;
            Assert(player.Visual != null && player.Visual != player.transform
                && player.Visual.IsChildOf(player.transform)
                && player.Visual.gameObject.activeInHierarchy,
                "Active model rotates beneath the physics root");
            var directions = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            float straightDistance = 0f;
            foreach (Vector2 direction in directions)
            {
                ResetPlayer(new Vector3(0f, 0.05f, -4f));
                Vector3 start = player.transform.position;
                for (int frame = 0; frame < 60; frame++)
                {
                    player.Move(direction, dt);
                    yield return null;
                }
                Vector3 displacement = player.transform.position - start;
                Vector3 expected = new Vector3(direction.x, 0f, direction.y);
                float distance = Vector3.Dot(displacement, expected);
                Assert(distance > 2.5f && distance < 3.05f, "Cardinal movement " + direction);
                Assert(Mathf.Abs(Vector3.Dot(displacement, Vector3.Cross(expected, Vector3.up))) < 0.02f,
                    "No lateral drift " + direction);
                Assert(Quaternion.Angle(player.Visual.rotation, Quaternion.LookRotation(expected)) < 1f,
                    "Visual faces movement " + direction);
                AssertModelHeading(expected);
                Assert(Quaternion.Angle(player.transform.rotation, Quaternion.identity) < 0.01f,
                    "Physics root keeps world orientation");
                straightDistance = distance;
            }

            ResetPlayer(new Vector3(0f, 0.05f, -4f));
            Vector3 diagonalStart = player.transform.position;
            for (int frame = 0; frame < 60; frame++)
            {
                player.Move(Vector2.one, dt);
                yield return null;
            }
            Vector3 diagonalDisplacement = player.transform.position - diagonalStart;
            diagonalDisplacement.y = 0f;
            Assert(Mathf.Abs(diagonalDisplacement.magnitude - straightDistance) < 0.05f,
                "Diagonal speed matches cardinal speed");

            foreach (Vector2 direction in new[]
                { Vector2.one, new Vector2(-1f, 1f), -Vector2.one, new Vector2(1f, -1f) })
            {
                ResetPlayer(new Vector3(0f, 0.05f, -4f));
                for (int frame = 0; frame < 30; frame++)
                {
                    player.Move(direction, dt);
                    yield return null;
                }
                Assert(Quaternion.Angle(player.Visual.rotation,
                    Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y))) < 1f,
                    "Visual faces diagonal " + direction);
                AssertModelHeading(new Vector3(direction.x, 0f, direction.y));
            }

            Quaternion stoppedHeading = player.Visual.rotation;
            for (int frame = 0; frame < 20; frame++)
            {
                player.Move(Vector2.zero, dt);
                yield return null;
            }
            Vector3 stopped = player.transform.position;
            for (int frame = 0; frame < 20; frame++)
            {
                player.Move(Vector2.zero, dt);
                yield return null;
            }
            Assert(Vector3.Distance(stopped, player.transform.position) < 0.005f, "Braking reaches rest");
            Assert(player.IsGrounded, "Player stays grounded");
            Assert(Quaternion.Angle(player.Visual.rotation, stoppedHeading) < 0.01f,
                "Releasing WASD preserves the last heading");

            ResetPlayer(new Vector3(0f, 4f, -4f));
            for (int frame = 0; frame < 90; frame++)
            {
                player.Move(Vector2.zero, dt);
                yield return null;
            }
            Assert(player.IsGrounded && Mathf.Abs(player.transform.position.y) < 0.08f,
                "Gravity lands player on floor");

            ResetPlayer(new Vector3(3f, 0.05f, 1f));
            for (int frame = 0; frame < 120; frame++)
            {
                player.Move(Vector2.up, dt);
                yield return null;
            }
            Assert(player.transform.position.z > 2.8f && player.transform.position.z < 3f,
                "Obstacle blocks movement");
            Assert(player.transform.position.y < 0.08f, "Cannot step onto tall obstacle");

            ResetPlayer(new Vector3(0f, 0.05f, -4f));
            for (int frame = 0; frame < 60; frame++)
            {
                player.Move(Vector2.up, 1f / 30f);
                yield return null;
            }
            float distance30 = player.transform.position.z + 4f;
            ResetPlayer(new Vector3(0f, 0.05f, -4f));
            for (int frame = 0; frame < 120; frame++)
            {
                player.Move(Vector2.up, dt);
                yield return null;
            }
            float distance60 = player.transform.position.z + 4f;
            Assert(Mathf.Abs(distance30 - distance60) < 0.06f,
                "Movement consistent across 30 and 60 Hz steps");

            Transform originalVisual = player.Visual;
            UnityEngine.Object.DestroyImmediate(originalVisual.GetChild(0).gameObject);
            var replacement = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(replacement.GetComponent<Collider>());
            replacement.transform.SetParent(originalVisual, false);
            replacement.transform.localPosition = Vector3.up * 0.4f;
            ResetPlayer(new Vector3(0f, 0.05f, -4f));
            for (int frame = 0; frame < 60; frame++)
            {
                player.Move(Vector2.right, dt);
                yield return null;
            }
            Assert(player.transform.position.x > 2.5f && player.Visual == originalVisual,
                "Model replacement preserves movement and visual reference");
            var followCamera = UnityEngine.Object.FindFirstObjectByType<TurtleFollowCamera>();
            Assert(followCamera != null && followCamera.Target == player.transform,
                "Camera follows physics root after model replacement");
        }

        private static void ResetPlayer(Vector3 position)
        {
            player.enabled = false;
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            character.enabled = true;
            player.enabled = true;
            player.ReadKeyboard = false;
            Physics.SyncTransforms();
        }

        private static void AssertModelHeading(Vector3 expected)
        {
            Transform head = null;
            Transform tail = null;
            foreach (Transform child in player.Visual.GetComponentsInChildren<Transform>())
            {
                if (child.name == "cabeza") head = child;
                if (child.name == "cola") tail = child;
            }
            if (head == null || tail == null)
                return;
            Assert(Vector3.Dot(player.Visual.GetChild(0).up, Vector3.up) > 0.999f,
                "Turtle stays upright while turning");
            Vector3 forward = head.position - tail.position;
            forward.y = 0f;
            Assert(Vector3.Dot(forward.normalized, expected.normalized) > 0.999f,
                "Turtle head faces movement " + expected);
        }

        private static void Assert(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException("FAIL: " + description);
            passed++;
            Debug.Log("PASS: " + description);
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            Time.captureDeltaTime = 0f;
            string result = (success ? "TURTLE_VERIFICATION_OK: " : "TURTLE_VERIFICATION_FAILED: ") + message;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/controller-verification-result.txt", result);
            if (success) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.Exit(success ? 0 : 1);
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(TurtlePrototypeBuilder.ScenePath);
            Camera camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("Preview");
            File.WriteAllBytes("Preview/TurtlePrototype.png", pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("TURTLE_PREVIEW_OK");
        }
    }
}
