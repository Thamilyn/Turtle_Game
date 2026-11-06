using System;
using System.Collections;
using System.IO;
using System.Reflection;
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
            var engine = (AudioSource)serialized.FindProperty("engine").objectReferenceValue;
            var music = (BeachMusic)serialized.FindProperty("beachMusic").objectReferenceValue;
            Assert(music != null, "The encounter references the beach music");
            var musicSource = music.GetComponent<AudioSource>();
            var distortion = music.GetComponent<AudioDistortionFilter>();
            var lowPass = music.GetComponent<AudioLowPassFilter>();
            var guide = Object.FindFirstObjectByType<MoonFairyGuide>();
            Assert(guide != null && new SerializedObject(music).FindProperty("moonGuide").objectReferenceValue == guide,
                "The music references the beach's actual moon guide");
            Assert(AssetDatabase.GetAssetPath(musicSource.clip) == BeachMusicInstaller.ClipPath,
                "Dreamy Night is assigned to the beach");
            Assert(musicSource.loop && musicSource.spatialBlend == 0f,
                "Background music loops in stereo without distance attenuation");
            Assert(musicSource.clip.loadType == AudioClipLoadType.DecompressOnLoad,
                "Music is decoded for precise short-fragment rewinds");
            Assert(!distortion.enabled && !lowPass.enabled && musicSource.pitch == 1f,
                "The calm track starts without scratch effects");
            float musicDeadline = Time.realtimeSinceStartup + 10f;
            while ((!musicSource.isPlaying || musicSource.volume < 0.1f) &&
                Time.realtimeSinceStartup < musicDeadline)
                yield return null;
            Assert(musicSource.isPlaying && musicSource.volume >= 0.1f,
                "Music loads, plays and fades in");
            float initialVolume = musicSource.volume;
            float fadeDeadline = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < fadeDeadline)
                yield return null;
            Assert(musicSource.volume >= initialVolume && musicSource.volume <= 0.3f,
                "Fade-in stays within the configured background level");
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
            Assert(!musicSource.isPlaying, "Pausing gameplay also pauses the music");
            int pausedSample = musicSource.timeSamples;
            float pausedVolume = musicSource.volume;
            float pauseDeadline = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < pauseDeadline)
                yield return null;
            Assert(Mathf.Abs(musicSource.timeSamples - pausedSample) < 2048 &&
                Mathf.Approximately(musicSource.volume, pausedVolume),
                "Music position and fade remain frozen while paused");
            Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            Assert(musicSource.isPlaying && musicSource.pitch == 1f,
                "Resuming restores the calm track");

            // Simulate the real guide route while the turtle's collider cannot touch the road.
            // Earlier waiting points must leave the song clean; only the final stop triggers it.
            Time.timeScale = 0f;
            character.enabled = false;
            Vector3 sandPosition = player.transform.position;
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            var guidePosition = typeof(MoonFairyGuide).GetField("basePosition", privateInstance);
            var advanceGuide = typeof(MoonFairyGuide).GetMethod("Advance", privateInstance);
            Assert(!guide.HasReachedRoad && !distortion.enabled, "The song stays clean before the final moon stop");
            for (int i = 0; i < 6600 && !guide.HasReachedRoad; i++)
            {
                player.transform.position = (Vector3)guidePosition.GetValue(guide) + Vector3.back * 2f;
                advanceGuide.Invoke(guide, new object[] { 1f / 60f, i / 60f });
                if (i % 240 == 0)
                {
                    yield return null;
                    Assert(!distortion.enabled, "Intermediate moon stops never start the scratch");
                }
            }
            Assert(guide.HasReachedRoad, "The moon completes its real route and stops at the road");
            Teleport(character, sandPosition);
            for (int i = 0; i < 3; i++) yield return null;
            Assert(!distortion.enabled, "A paused game defers the moon-triggered scratch until resume");
            Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            Assert(distortion.enabled && lowPass.enabled && musicSource.pitch < 0.8f &&
                distortion.distortionLevel >= 0.3f && musicSource.volume > 0.04f &&
                !sequence.HasStarted && !car.gameObject.activeSelf,
                "The moon's final stop immediately breaks the calm with an audible scratch before the car");
            Time.timeScale = 0f;
            for (int i = 0; i < 3; i++) yield return null;
            Assert(!musicSource.isPlaying, "The brief scratch also respects pause");
            float pausedScratchPitch = musicSource.pitch;
            int pausedScratchSample = musicSource.timeSamples;
            float scratchPauseDeadline = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < scratchPauseDeadline)
                yield return null;
            Assert(Mathf.Abs(musicSource.timeSamples - pausedScratchSample) < 2048 &&
                Mathf.Approximately(musicSource.pitch, pausedScratchPitch),
                "Pausing freezes both the scratch fragment and its slowdown");
            Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            Assert(musicSource.isPlaying && distortion.enabled, "Resuming continues the brief scratch");
            bool rewoundFragment = false;
            int previousMusicSample = musicSource.timeSamples;
            float scratchDeadline = Time.realtimeSinceStartup + 1.2f;
            while ((musicSource.isPlaying || distortion.enabled) && Time.realtimeSinceStartup < scratchDeadline)
            {
                int sample = musicSource.timeSamples;
                rewoundFragment |= sample < previousMusicSample;
                previousMusicSample = sample;
                Assert(!sequence.HasStarted && !car.gameObject.activeSelf && !engine.isPlaying,
                    "The scratch breaks the peace before the car and engine start");
                yield return null;
            }
            Assert(rewoundFragment, "The brief scratch repeats the music fragment before the engine");
            Assert(!musicSource.isPlaying && musicSource.volume == 0f && !distortion.enabled && !lowPass.enabled,
                "The brief scratch ends in silence while the turtle is still on sand");
            float silenceDeadline = Time.realtimeSinceStartup + 0.2f;
            while (Time.realtimeSinceStartup < silenceDeadline)
            {
                Assert(!musicSource.isPlaying && !engine.isPlaying, "Silence separates the scratch from the engine");
                yield return null;
            }
            for (int i = 0; i < 120 && !sequence.HasStarted; i++)
            {
                player.Move(Vector2.up, 1f / 60f);
                yield return null;
            }
            Assert(sequence.HasStarted, "Walking onto the road starts the car on contact");
            Assert(!musicSource.isPlaying && !distortion.enabled && !lowPass.enabled && engine.isPlaying,
                "The engine starts alone with the scratch and music already stopped");
            Assert(engine.GetComponent<AudioDistortionFilter>() == null &&
                engine.GetComponent<AudioLowPassFilter>() == null,
                "The music filters never process the car engine");
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
                    Assert(!musicSource.isPlaying && musicSource.volume == 0f &&
                        !distortion.enabled && !lowPass.enabled,
                        "Music and scratch stop at blackout before narration");
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
                if (overlay != null && !overlay.gameObject.activeSelf)
                {
                    Assert(!musicSource.isPlaying && musicSource.volume == 0f &&
                        !distortion.enabled && !lowPass.enabled,
                        "Scratch and music remain silent throughout the car approach");
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
            Assert(Object.FindFirstObjectByType<BeachMusic>() == null,
                "Beach music does not leak into the ending scene");
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
