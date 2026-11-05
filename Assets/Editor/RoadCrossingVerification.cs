using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    [InitializeOnLoad]
    public static class RoadCrossingVerification
    {
        private const string PendingKey = "TurtleGame.RoadCrossingVerification";
        private static IEnumerator checks;
        private static int lastFrame;
        private static int passed;
        private static double deadline;

        static RoadCrossingVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Turtle Game/Verificar automóvil y transición final")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run road verification from Edit Mode.");
            if (Object.FindFirstObjectByType<RoadCrossingSequence>() == null)
                throw new InvalidOperationException("Open MoonBeach and install the road sequence first.");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && checks != null)
                Finish(false, "Verification interrupted.");
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;
            SessionState.SetBool(PendingKey, false);
            passed = 0;
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 90d;
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
                    Finish(true, passed + " road encounter checks passed.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Verify()
        {
            yield return null;
            var sequence = Object.FindFirstObjectByType<RoadCrossingSequence>();
            var player = Object.FindFirstObjectByType<TurtlePlayerController>();
            var character = player.GetComponent<CharacterController>();
            var road = GameObject.Find("Coastal road").GetComponent<BoxCollider>();
            var serialized = new SerializedObject(sequence);
            var car = (Transform)serialized.FindProperty("car").objectReferenceValue;
            var overlay = (Image)serialized.FindProperty("screenOverlay").objectReferenceValue;
            var pause = Object.FindFirstObjectByType<global::PauseMenuManager>();
            var menu = Object.FindFirstObjectByType<global::BeachMainMenu>();
            if (menu != null)
                menu.PlayGame();
            player.ReadKeyboard = false;
            Assert(!sequence.HasStarted && !car.gameObject.activeSelf, "Car waits before road contact");
            Assert(car.GetComponentsInChildren<Collider>(true).Length == 0,
                "Scripted car cannot push the turtle or block the road");
            foreach (var renderer in car.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert(material != null && material.shader.name.StartsWith("Universal Render Pipeline/"),
                        "Car material supports URP");

            Teleport(character, new Vector3(0f, road.bounds.max.y + 5f, road.bounds.min.z + 1f));
            for (int i = 0; i < 3; i++) { player.Move(Vector2.zero, 1f / 60f); yield return null; }
            Assert(!sequence.HasStarted, "Being above the asphalt does not trigger the encounter");
            Teleport(character, new Vector3(road.bounds.max.x + 3f, road.bounds.max.y, road.bounds.min.z + 1f));
            for (int i = 0; i < 3; i++) { player.Move(Vector2.zero, 1f / 60f); yield return null; }
            Assert(!sequence.HasStarted, "Being beyond the road ends does not trigger the encounter");

            // Start on the real sand, then walk across the real asphalt edge.
            Teleport(character, new Vector3(0f, road.bounds.max.y + 0.08f, road.bounds.min.z - 1.5f));
            for (int i = 0; i < 30; i++) { player.Move(Vector2.zero, 1f / 60f); yield return null; }
            Assert(!sequence.HasStarted, "Approaching on sand leaves the car hidden");
            Time.timeScale = 0f;
            for (int i = 0; i < 3; i++) yield return null;
            Assert(!sequence.HasStarted && !car.gameObject.activeSelf, "Paused gameplay cannot start the encounter");
            Time.timeScale = 1f;
            for (int i = 0; i < 120 && !sequence.HasStarted; i++)
            {
                player.Move(Vector2.up, 1f / 60f);
                yield return null;
            }
            Assert(sequence.HasStarted, "Walking onto the road starts the car on contact");
            Assert(!player.enabled && (pause == null || !pause.enabled), "Movement and pause stop during the encounter");
            Vector3 turtlePosition = player.transform.position;
            float firstX = car.position.x;
            bool capturedCar = false;
            int beats = 0;
            bool red = false;
            bool sawBlack = false;
            while (SceneManager.GetActiveScene().name != "Ending_Scene")
            {
                if (overlay != null && overlay.gameObject.activeSelf)
                {
                    Assert(overlay.color.a > 0.99f, "Heartbeat fully covers the gameplay");
                    bool nextRed = overlay.color.r > 0.2f;
                    if (nextRed && !red) beats++;
                    red = nextRed;
                    sawBlack |= overlay.color.r < 0.02f;
                    if (beats == 1 && red && overlay.color.r > 0.4f)
                        ScreenCapture.CaptureScreenshot("Logs/road-crossing-heartbeat.png");
                }
                else if (!capturedCar && car.position.x > turtlePosition.x - 4.5f)
                {
                    Assert(car.position.x > firstX + 1f, "Car travels across the road");
                    ScreenCapture.CaptureScreenshot("Logs/road-crossing-car.png");
                    capturedCar = true;
                }
                if (player != null)
                    Assert(Vector3.Distance(player.transform.position, turtlePosition) < 0.01f,
                        "Turtle remains still during the encounter");
                yield return null;
            }
            Assert(capturedCar, "Car appears before the blackout");
            Assert(sawBlack && beats >= 6, "Three double heartbeats alternate red and black");
            Assert(Time.timeScale == 1f, "Message scene loads with normal time");
            Assert(Object.FindFirstObjectByType<EndingSequence>() != null, "Existing narrated ending loads successfully");
            yield return null;
            ScreenCapture.CaptureScreenshot("Logs/road-crossing-ending.png");
            yield return null;
        }

        private static void Teleport(CharacterController character, Vector3 position)
        {
            character.enabled = false;
            character.transform.position = position;
            character.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Assert(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException(label);
            passed++;
        }

        private static void Finish(bool success, string message)
        {
            checks = null;
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/road-crossing-verification-result.txt", (success ? "PASS: " : "FAIL: ") + message);
            if (success) Debug.Log(message); else Debug.LogError(message);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
            else
                EditorApplication.isPlaying = false;
        }
    }
}
