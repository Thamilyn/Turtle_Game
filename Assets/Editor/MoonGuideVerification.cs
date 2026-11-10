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
    /// <summary>Exercises the guide in the saved beach without saving any verification objects.</summary>
    [InitializeOnLoad]
    public static class MoonGuideVerification
    {
        private const string PendingKey = "TurtleGame.MoonGuideVerification";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly MethodInfo Advance = typeof(MoonFairyGuide).GetMethod("Advance", Private);
        private static IEnumerator checks;
        private static double deadline;
        private static int assertions;
        private static MoonFairyGuide guide;
        private static Transform player;
        private static Terrain terrain;

        static MoonGuideVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Turtle Game/Verificar desvíos de la luna")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run moon verification from Edit Mode.");
            if (Application.isBatchMode)
                EditorSceneManager.OpenScene(MoonBeachBuilder.ScenePath);
            else if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MoonBeachBuilder.ScenePath)
                throw new InvalidOperationException("Open MoonBeach before running verification.");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;
            SessionState.SetBool(PendingKey, false);
            deadline = EditorApplication.timeSinceStartup + 120;
            assertions = 0;
            checks = Verify();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Moon verification timed out.");
                if (!checks.MoveNext()) Finish(true, assertions + " checks passed.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Verify()
        {
            guide = Object.FindFirstObjectByType<MoonFairyGuide>();
            Assert(guide != null, "Beach has a moon guide");
            player = (Transform)Get("target");
            terrain = (Terrain)Get("beach");
            player.GetComponent<TurtlePlayerController>().ReadKeyboard = false;
            player.GetComponent<CharacterController>().enabled = false;
            Time.timeScale = 0f;

            Vector3 waiting = (Vector3)Get("basePosition");
            player.position = waiting + Vector3.back * 20f;
            for (int i = 0; i < 120; i++) Step(1f / 60f, i / 60f);
            Assert(Vector3.Distance(waiting, (Vector3)Get("basePosition")) < 0.0001f,
                "Moon waits when the turtle is far away");
            Vector3 paused = ((GameObject)Get("fairy")).transform.position;
            Step(0f, 100f);
            Assert(Vector3.Distance(paused, ((GameObject)Get("fairy")).transform.position) < 0.0001f,
                "Pause freezes both travel and sway");

            // Low obstacles must cause lateral detours, even when the moon could fly above them.
            GameObject log = Obstacle("Verification low log", new Vector2(3f, 14f), new Vector3(4f, 0.7f, 2f));
            GameObject covered = Obstacle("Verification covered waypoint", new Vector2(4f, 21f), new Vector3(2f, 3f, 2f));
            GameObject trigger = Obstacle("Verification ignored trigger", new Vector2(2f, 10f), new Vector3(30f, 4f, 1f));
            trigger.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();

            foreach (int fps in new[] { 30, 60, 120 })
            {
                log.SetActive(fps != 30);
                covered.SetActive(fps != 30);
                trigger.SetActive(fps != 30);
                Physics.SyncTransforms();
                guide.enabled = false;
                yield return null;
                guide.enabled = true;
                float dt = 1f / fps;
                float maxSideStep = 0f;
                int frame = 0;
                while (!(bool)Get("routeComplete") && frame < fps * 110)
                {
                    Vector3 before = (Vector3)Get("basePosition");
                    player.position = before + Vector3.back * 2f;
                    Step(dt, frame * dt);
                    Vector3 after = (Vector3)Get("basePosition");
                    Vector2 movement = new Vector2(after.x - before.x, after.z - before.z);
                    Assert(movement.magnitude <= 2.4f * dt + 0.0001f, "Travel speed at " + fps + " FPS");
                    CheckClearance(((GameObject)Get("fairy")).transform.position, 0.21f);
                    Vector3 ground = after - Vector3.up * 1.6f;
                    CheckClearance(ground + Vector3.up * 0.4f, 0.35f, true);
                    if (after.z > 11f && after.z < 17f)
                        maxSideStep = Mathf.Max(maxSideStep, Mathf.Abs(after.x - 3f));
                    frame++;
                    if (frame % 240 == 0) yield return null;
                }
                Assert((bool)Get("routeComplete"), "Full beach route completes at " + fps + " FPS");
                if (fps != 30)
                    Assert(maxSideStep > 2.5f, "Moon goes around the low log at " + fps + " FPS");
                Vector3 end = (Vector3)Get("basePosition");
                Assert(Vector2.Distance(new Vector2(end.x, end.z), new Vector2(0f, 122f)) < 0.01f,
                    "Moon reaches the final waypoint");
                Bounds road = GameObject.Find("Coastal road").GetComponent<BoxCollider>().bounds;
                Assert(end.x > road.min.x && end.x < road.max.x &&
                    end.z > road.min.z + 2f && end.z < road.max.z,
                    "Moon stops visibly inside the asphalt, beyond the sand boundary");
                Debug.Log("MOON_GUIDE_ROUTE_OK: " + fps + " FPS; " + frame + " simulated frames.");
            }

            log.SetActive(false);
            covered.SetActive(false);
            trigger.SetActive(false);
            guide.enabled = false;
            yield return null;
            guide.enabled = true;
            GameObject wall = Obstacle("Verification impassable wall", new Vector2(0f, 14f), new Vector3(110f, 4f, 2f));
            Physics.SyncTransforms();
            for (int i = 0; i < 180; i++)
            {
                player.position = (Vector3)Get("basePosition") + Vector3.back * 2f;
                Step(1f / 60f, i / 60f);
            }
            Assert(((Vector3)Get("basePosition")).z < 12f, "No available path makes the moon wait safely");
            wall.SetActive(false);
            for (int i = 0; i < 180; i++)
            {
                player.position = (Vector3)Get("basePosition") + Vector3.back * 2f;
                Step(1f / 60f, i / 60f);
            }
            Assert(((Vector3)Get("basePosition")).z > 12f, "Moon retries after the obstruction is removed");
        }

        private static GameObject Obstacle(string name, Vector2 point, Vector3 scale)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            float ground = terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) + terrain.transform.position.y;
            item.transform.position = new Vector3(point.x, ground + scale.y / 2f, point.y);
            item.transform.localScale = scale;
            return item;
        }

        private static void CheckClearance(Vector3 position, float radius, bool allowWalkableSurface = false)
        {
            foreach (Collider collider in Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                float ground = terrain.SampleHeight(collider.bounds.center) + terrain.transform.position.y;
                Assert(collider is TerrainCollider || collider.transform.IsChildOf(player) ||
                    collider.GetComponentInParent<MoonProgressBarriers>() != null ||
                    (allowWalkableSurface && collider.bounds.max.y <= ground + 0.15f),
                    "No intersection with " + collider.name + " at " + position);
            }
        }

        private static object Get(string name) => typeof(MoonFairyGuide).GetField(name, Private).GetValue(guide);
        private static void Step(float dt, float time) => Advance.Invoke(guide, new object[] { dt, time });

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            assertions++;
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            checks = null;
            Time.timeScale = 1f;
            string result = (success ? "MOON_GUIDE_PASS: " : "MOON_GUIDE_FAILED: ") + message;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/moon-guide-verification-result.txt", result);
            if (success) Debug.Log(result); else Debug.LogError(result);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
