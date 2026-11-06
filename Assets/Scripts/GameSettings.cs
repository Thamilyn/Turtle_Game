using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TurtleGame
{
    /// <summary>Persistent master volume and scene exposure, shared by every options menu.</summary>
    [DisallowMultipleComponent]
    public sealed class GameSettings : MonoBehaviour
    {
        public const string VolumeKey = "TurtleGame.MasterVolume";
        public const string BrightnessKey = "TurtleGame.Brightness";
        public const float DefaultVolume = 1f;
        public const float DefaultBrightness = 0.5f;

        private static GameSettings instance;
        private VolumeProfile brightnessProfile;
        private ColorAdjustments colorAdjustments;
        private bool hasUnsavedChanges;
        private float previousListenerVolume;

        public static GameSettings Instance
        {
            get
            {
                if (instance == null)
                    new GameObject("Game settings").AddComponent<GameSettings>();
                return instance;
            }
        }

        public float MasterVolume { get; private set; }
        public float Brightness { get; private set; }
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _ = Instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            previousListenerVolume = AudioListener.volume;
            MasterVolume = ReadPreference(VolumeKey, DefaultVolume);
            Brightness = ReadPreference(BrightnessKey, DefaultBrightness);

            // Only exposure is overridden. Existing bloom, tonemapping and colors stay in their profiles.
            brightnessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            brightnessProfile.name = "Player brightness (runtime)";
            colorAdjustments = brightnessProfile.Add<ColorAdjustments>(false);
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = brightnessProfile;
            Apply();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void SetMasterVolume(float value)
        {
            if (!IsFinite(value))
                return;
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            SettingsChanged();
        }

        public void SetBrightness(float value)
        {
            if (!IsFinite(value))
                return;
            Brightness = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BrightnessKey, Brightness);
            SettingsChanged();
        }

        private void SettingsChanged()
        {
            hasUnsavedChanges = true;
            Apply();
            Changed?.Invoke();
        }

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
            // Middle is the original scene; endpoints are two exposure stops darker/brighter.
            colorAdjustments.postExposure.Override(Mathf.Lerp(-2f, 2f, Brightness));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.cameraType != CameraType.Game)
                        continue;
                    var data = camera.GetUniversalAdditionalCameraData();
                    if (data.renderType != CameraRenderType.Base)
                        continue;
                    data.renderPostProcessing = true;
                    data.volumeLayerMask |= 1 << gameObject.layer;
                }
            }
        }

        public void Save()
        {
            if (!hasUnsavedChanges)
                return;
            PlayerPrefs.Save();
            hasUnsavedChanges = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;
            Save();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AudioListener.volume = previousListenerVolume;
            if (brightnessProfile != null)
            {
                foreach (var component in brightnessProfile.components)
                    Destroy(component);
                Destroy(brightnessProfile);
            }
            instance = null;
        }

        private static float ReadPreference(string key, float defaultValue)
        {
            float value = PlayerPrefs.GetFloat(key, defaultValue);
            return IsFinite(value) ? Mathf.Clamp01(value) : defaultValue;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
