using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    [InitializeOnLoad]
    public static class OptionsVerification
    {
        private const string PendingKey = "TurtleGame.OptionsVerification";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/MenuScene.unity", "Assets/Scenes/PauseScene.unity",
            "Assets/Scenes/Ending_Scene.unity", "Assets/Scenes/MoonBeach.unity"
        };

        private static IEnumerator checks;
        private static int lastFrame;
        private static int passed;
        private static double deadline;

        static OptionsVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Turtle Game/Verify volume and brightness")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run options verification from Edit Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            passed = 0;
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var slider in root.GetComponentsInChildren<Slider>(true))
                    {
                        var binding = slider.GetComponent<OptionsSlider>();
                        Assert(binding != null, path + ": slider is connected");
                        bool brightness = slider.name.Contains("Brightness");
                        Assert(binding.ControlledSetting == (brightness
                            ? OptionsSlider.Setting.Brightness : OptionsSlider.Setting.Volume),
                            path + ": slider controls the correct setting");
                        count++;
                    }
                }
                Assert(count == (path.Contains("MoonBeach") ? 4 : 1), path + ": all options are covered");
            }

            BackupPreference(GameSettings.VolumeKey);
            BackupPreference(GameSettings.BrightnessKey);
            SessionState.SetInt(PendingKey + ".Passed", passed);
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
            passed = SessionState.GetInt(PendingKey + ".Passed", 0);
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 120d;
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
                    Finish(true, passed + " options checks passed.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Verify()
        {
            yield return null;
            var settings = GameSettings.Instance;
            Assert(Mathf.Approximately(settings.MasterVolume, 1f) &&
                Mathf.Approximately(settings.Brightness, 0.5f), "First launch uses full volume and neutral brightness");
            Assert(Mathf.Approximately(AudioListener.volume, 1f), "Master audio applies at startup");

            var mainGroup = GameObject.Find("MainMenuGroup").transform;
            mainGroup.Find("MainMenu/OptionsButton").GetComponent<Button>().onClick.Invoke();
            var options = mainGroup.Find("OptionsMenu");
            var volume = options.Find("Slider Volume").GetComponent<Slider>();
            var brightness = options.Find("Slider Brightness").GetComponent<Slider>();
            yield return null;
            Assert(options.gameObject.activeInHierarchy && volume.value == 1f && brightness.value == 0.5f,
                "Opening options initializes both sliders from settings");

            Canvas.ForceUpdateCanvases();
            DragSlider(volume, 0.3f);
            Assert(Mathf.Abs(settings.MasterVolume - 0.3f) < 0.02f &&
                Mathf.Abs(AudioListener.volume - 0.3f) < 0.02f, "Dragging volume adjusts master audio immediately");
            float before = volume.value;
            volume.OnMove(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right });
            Assert(volume.value > before && Mathf.Approximately(AudioListener.volume, volume.value),
                "Keyboard navigation adjusts the same setting");
            volume.value = 0f;
            Assert(AudioListener.volume == 0f, "Zero volume mutes all listener audio");
            volume.value = 1f;
            Assert(AudioListener.volume == 1f, "Maximum volume restores the authored audio levels");
            volume.value = 0.35f;
            DragSlider(brightness, 0.7f);
            Assert(Mathf.Abs(settings.Brightness - 0.7f) < 0.02f, "Dragging brightness applies immediately");

            options.Find("BackButton").GetComponent<Button>().onClick.Invoke();
            Assert(!options.gameObject.activeSelf && Mathf.Approximately(PlayerPrefs.GetFloat(GameSettings.VolumeKey), 0.35f),
                "Back closes options and retains the chosen settings");
            mainGroup.Find("MainMenu/OptionsButton").GetComponent<Button>().onClick.Invoke();
            Assert(Mathf.Approximately(volume.value, 0.35f) && Mathf.Abs(brightness.value - 0.7f) < 0.02f,
                "Reopening options restores both values");

            mainGroup.gameObject.SetActive(false);
            var pause = Object.FindFirstObjectByType<global::PauseMenuManager>();
            pause.PauseGame();
            var pauseGroup = GameObject.Find("PauseMenuGroup").transform;
            pauseGroup.Find("PauseMenu/OptionsButton").GetComponent<Button>().onClick.Invoke();
            var pauseOptions = pauseGroup.Find("OptionsMenu");
            var pauseVolume = pauseOptions.Find("Slider Volume").GetComponent<Slider>();
            var pauseBrightness = pauseOptions.Find("Slider Brightness").GetComponent<Slider>();
            Assert(Time.timeScale == 0f && Mathf.Approximately(pauseVolume.value, 0.35f) &&
                Mathf.Abs(pauseBrightness.value - 0.7f) < 0.02f, "Pause options share the main menu settings");
            pauseVolume.value = 0.25f;
            pauseBrightness.value = 0f;
            Assert(AudioListener.volume == 0.25f && settings.Brightness == 0f,
                "Both options still apply while time is paused");

            var camera = Camera.main;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            Assert(cameraData.renderPostProcessing && (cameraData.volumeLayerMask.value & 1) != 0,
                "The beach camera renders the brightness volume");
            var target = new RenderTexture(480, 540, 24);
            target.Create();
            var previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            float[] luminances = new float[3];
            try
            {
                for (int level = 0; level < 3; level++)
                {
                    pauseBrightness.value = level * 0.5f;
                    for (int frame = 0; frame < 8; frame++)
                        yield return null;
                    var stack = cameraData.volumeStack ?? VolumeManager.instance.stack;
                    Assert(Mathf.Abs(stack.GetComponent<ColorAdjustments>().postExposure.value - (level * 2f - 2f)) < 0.01f,
                        "URP receives the requested exposure level " + level);
                    luminances[level] = CaptureLuminance(target, "Logs/options-brightness-" + level + ".png");
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.Destroy(target);
            }
            Assert(luminances[1] > luminances[0] * 1.1f && luminances[2] > luminances[1] * 1.1f,
                "Rendered image brightens across all three levels: " + string.Join(", ", luminances));

            pauseBrightness.value = 0.65f;
            pauseOptions.Find("BackButton").GetComponent<Button>().onClick.Invoke();
            pause.ResumeGame();
            Assert(Time.timeScale == 1f, "Options allow returning to gameplay");
            settings.SetMasterVolume(-1f);
            settings.SetBrightness(2f);
            Assert(settings.MasterVolume == 0f && settings.Brightness == 1f, "Settings clamp to the supported range");
            settings.SetMasterVolume(float.NaN);
            settings.SetBrightness(float.PositiveInfinity);
            Assert(settings.MasterVolume == 0f && settings.Brightness == 1f, "Non-finite input cannot corrupt settings");
            settings.SetMasterVolume(0.25f);
            settings.SetBrightness(0.65f);
            settings.Save();

            foreach (string sceneName in new[] { "MenuScene", "Ending_Scene", "MoonBeach" })
            {
                SceneManager.LoadScene(sceneName);
                for (int frame = 0; frame < 5; frame++)
                    yield return null;
                Assert(GameSettings.Instance == settings && AudioListener.volume == 0.25f && settings.Brightness == 0.65f,
                    sceneName + ": settings survive scene transitions");
                Assert(Object.FindObjectsByType<GameSettings>(FindObjectsSortMode.None).Length == 1,
                    sceneName + ": only one settings service exists");
                Assert(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing,
                    sceneName + ": the new camera renders brightness");
            }

            // Recreate the service to exercise the same preference-loading path as the next launch.
            Object.Destroy(settings.gameObject);
            yield return null;
            var reloaded = GameSettings.Instance;
            Assert(reloaded.MasterVolume == 0.25f && reloaded.Brightness == 0.65f && AudioListener.volume == 0.25f,
                "Saved preferences reload into a fresh settings service");
        }

        private static void DragSlider(Slider slider, float normalizedValue)
        {
            var canvas = slider.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var track = (RectTransform)slider.handleRect.parent;
            Vector3 local = new Vector3(Mathf.Lerp(track.rect.xMin, track.rect.xMax, normalizedValue), track.rect.center.y);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, track.TransformPoint(local)),
                pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() }
            };
            slider.OnPointerDown(pointer);
            slider.OnDrag(pointer);
            slider.OnPointerUp(pointer);
        }

        private static float CaptureLuminance(RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(path, image.EncodeToPNG());
            double total = 0d;
            foreach (var pixel in image.GetPixels32())
                total += (0.2126d * pixel.r + 0.7152d * pixel.g + 0.0722d * pixel.b) / 255d;
            Object.Destroy(image);
            return (float)(total / (target.width * target.height));
        }

        private static void BackupPreference(string key)
        {
            SessionState.SetBool(PendingKey + key + ".Exists", PlayerPrefs.HasKey(key));
            SessionState.SetFloat(PendingKey + key, PlayerPrefs.GetFloat(key));
            PlayerPrefs.DeleteKey(key);
        }

        private static void RestorePreference(string key)
        {
            if (SessionState.GetBool(PendingKey + key + ".Exists", false))
                PlayerPrefs.SetFloat(key, SessionState.GetFloat(PendingKey + key, 0f));
            else
                PlayerPrefs.DeleteKey(key);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            passed++;
            Debug.Log("OPTIONS_CHECK: " + message);
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            checks = null;
            Time.timeScale = 1f;
            RestorePreference(GameSettings.VolumeKey);
            RestorePreference(GameSettings.BrightnessKey);
            PlayerPrefs.Save();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/options-verification-result.txt", (success ? "PASS: " : "FAIL: ") + message);
            Debug.Log((success ? "OPTIONS_VERIFICATION_PASS: " : "OPTIONS_VERIFICATION_FAIL: ") + message);
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
            else
                EditorApplication.isPlaying = false;
        }
    }
}
