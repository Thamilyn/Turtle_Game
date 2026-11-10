using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    [InitializeOnLoad]
    public static class MoonProgressBarriersVerification
    {
        private const string PendingKey = "TurtleGame.MoonProgressBarriersVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static IEnumerator checks;
        private static double deadline;

        static MoonProgressBarriersVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Turtle Game/Verificar barreras de progreso")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run barrier verification from Edit Mode.");
            if (Application.isBatchMode)
                EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
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
            checks = Verify();
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Barrier verification timed out.");
                if (!checks.MoveNext()) Finish(true, "Physical blocking, lateral bypass, permanent release, road funnel, central passage and full moon route passed.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Verify()
        {
            var guide = Object.FindFirstObjectByType<MoonFairyGuide>();
            var player = (Transform)Get(guide, "target");
            var terrain = (Terrain)Get(guide, "beach");
            var barriers = (MoonProgressBarriers)Get(guide, "progressBarriers");
            player.GetComponent<TurtlePlayerController>().ReadKeyboard = false;
            player.GetComponent<CharacterController>().enabled = false;
            Time.timeScale = 0f;
            Assert(barriers != null && barriers.GetComponentsInChildren<Renderer>().Length == 0,
                "Barriers are created without visible geometry");
            var gates = barriers.GetComponentsInChildren<BoxCollider>();
            BoxCollider first = gates[0];
            BoxCollider roadGate = Array.Find(gates, gate => gate.name == "Road approach gate");
            Assert(first.enabled && roadGate.enabled && gates.Length > 5, "Several checkpoints start closed");

            var probe = new GameObject("Verification turtle collision probe");
            probe.transform.SetParent(player, false);
            probe.transform.localScale = new Vector3(1f / player.lossyScale.x,
                1f / player.lossyScale.y, 1f / player.lossyScale.z);
            var character = probe.AddComponent<CharacterController>();
            character.radius = 0.35f;
            character.height = 0.8f;
            character.center = Vector3.up * 0.4f;
            character.stepOffset = 0.1f;
            character.skinWidth = 0.02f;
            var leftBoundary = Array.Find(gates, gate => gate.name == "Beach left boundary");
            var rightBoundary = Array.Find(gates, gate => gate.name == "Beach right boundary");
            var rearBoundary = Array.Find(gates, gate => gate.name == "Beach rear boundary");
            var forwardBoundary = Array.Find(gates, gate => gate.name == "Beach forward boundary");
            Assert(leftBoundary.bounds.max.x >= terrain.transform.position.x + 7f &&
                rightBoundary.bounds.min.x <= terrain.transform.position.x + terrain.terrainData.size.x - 7f,
                "Side boundaries leave a safety margin before the visible terrain edges");
            float x = leftBoundary.bounds.max.x + 2f;
            Place(character, terrain, new Vector3(x, 0f, first.transform.position.z - 2f));
            for (int i = 0; i < 50; i++) character.Move(Vector3.forward * 0.1f);
            Assert(probe.transform.position.z < first.bounds.min.z, "Sprinting cannot pass a closed checkpoint");
            barriers.ReleaseReached(first.transform.position.z - 0.1f);
            Assert(first.enabled, "Checkpoint stays closed before the moon reaches it");
            barriers.ReleaseReached(first.transform.position.z);
            Physics.SyncTransforms();
            Assert(!first.enabled, "Moon crossing releases the checkpoint");
            for (int i = 0; i < 40; i++) character.Move(Vector3.forward * 0.1f);
            Assert(probe.transform.position.z > first.transform.position.z + 1f, "Player can cross an unlocked checkpoint");
            barriers.ReleaseReached(0f);
            Assert(!first.enabled, "An unlocked checkpoint never closes behind the player");

            Place(character, terrain, new Vector3(x, 0f, first.transform.position.z - 2f));
            for (int i = 0; i < 60; i++) character.Move(Vector3.left * 0.1f);
            Assert(probe.transform.position.x >= leftBoundary.bounds.max.x,
                "Left wall stops the turtle before the unsafe edge");
            Place(character, terrain, new Vector3(rightBoundary.bounds.min.x - 2f, 0f, first.transform.position.z - 2f));
            for (int i = 0; i < 60; i++) character.Move(Vector3.right * 0.1f);
            Assert(probe.transform.position.x <= rightBoundary.bounds.min.x,
                "Right wall stops the turtle before the unsafe edge");
            Place(character, terrain, new Vector3(0f, 0f, rearBoundary.bounds.max.z + 2f));
            for (int i = 0; i < 60; i++) character.Move(Vector3.back * 0.1f);
            Assert(probe.transform.position.z >= rearBoundary.bounds.max.z,
                "Rear wall prevents leaving the sand behind the starting area");
            var route = (Vector2[])typeof(MoonFairyGuide).GetField("Route", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            float moonX = route[route.Length - 1].x;
            Place(character, terrain, new Vector3(moonX, 0f, roadGate.transform.position.z - 2f));
            for (int i = 0; i < 50; i++) character.Move(Vector3.forward * 0.1f);
            Assert(probe.transform.position.z < roadGate.bounds.min.z, "Road remains inaccessible before the moon");
            barriers.ReleaseReached(roadGate.transform.position.z + 0.3f);
            Assert(roadGate.enabled, "Moon must enter the asphalt before the last gate opens");
            barriers.ReleaseReached(roadGate.transform.position.z + 0.51f);
            Physics.SyncTransforms();
            for (int i = 0; i < 35; i++) character.Move(Vector3.forward * 0.1f);
            Assert(!roadGate.enabled && probe.transform.position.z > roadGate.transform.position.z + 1f,
                "Road opens once the moon is on the asphalt");
            for (int side = -1; side <= 1; side += 2)
            {
                Place(character, terrain, new Vector3(moonX + side * 8f, 0f, roadGate.transform.position.z - 2f));
                for (int i = 0; i < 50; i++) character.Move(Vector3.forward * 0.1f);
                Assert(probe.transform.position.z < roadGate.transform.position.z,
                    "Sand/asphalt border stays closed outside the moon's entrance on side " + side);
                Place(character, terrain, new Vector3(moonX, 0f, roadGate.transform.position.z + 0.8f));
                for (int i = 0; i < 60; i++) character.Move(Vector3.right * side * 0.1f);
                Assert(Mathf.Abs(probe.transform.position.x - moonX) < 1.6f,
                    "Final corridor prevents wandering along the road edge on side " + side);
            }
            Place(character, terrain, new Vector3(moonX, 0f, forwardBoundary.bounds.min.z - 2f));
            for (int i = 0; i < 60; i++) character.Move(Vector3.forward * 0.1f);
            Assert(probe.transform.position.z <= forwardBoundary.bounds.min.z,
                "Forward wall closes the perimeter beyond the road");
            probe.SetActive(false);
            Object.Destroy(probe);

            // Fresh gates must not enter the moon's obstacle planner or deadlock its route.
            guide.enabled = false;
            yield return null;
            guide.enabled = true;
            var advance = typeof(MoonFairyGuide).GetMethod("Advance", Private);
            int frame = 0;
            while (!guide.HasReachedRoad && frame < 7200)
            {
                player.position = (Vector3)Get(guide, "basePosition") + Vector3.back * 2f;
                advance.Invoke(guide, new object[] { 1f / 60f, frame / 60f });
                frame++;
                if (frame % 240 == 0) yield return null;
            }
            Assert(guide.HasReachedRoad, "Moon completes the entire route with checkpoints present");
            barriers = (MoonProgressBarriers)Get(guide, "progressBarriers");
            foreach (var gate in barriers.GetComponentsInChildren<BoxCollider>())
                if (gate.name.StartsWith("Moon progress gate") || gate.name == "Road approach gate")
                    Assert(!gate.enabled, "All reached checkpoints are open at the road");
        }

        private static object Get(MoonFairyGuide guide, string field) =>
            typeof(MoonFairyGuide).GetField(field, Private).GetValue(guide);

        private static void Place(CharacterController character, Terrain terrain, Vector3 position)
        {
            character.enabled = false;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f;
            character.transform.position = position;
            character.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            checks = null;
            Time.timeScale = 1f;
            string result = (success ? "MOON_PROGRESS_BARRIERS_PASS: " : "MOON_PROGRESS_BARRIERS_FAILED: ") + message;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/moon-progress-barriers-result.txt", result);
            if (success) Debug.Log(result); else Debug.LogError(result);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
