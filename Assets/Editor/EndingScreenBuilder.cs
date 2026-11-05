using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace TurtleGame.Editor
{
    public static class EndingScreenBuilder
    {
        public const string ScenePath = "Assets/Scenes/Ending_Scene.unity";
        private const string AnimationPath = "Assets/Animations/Ending_Message.anim";
        private const string AudioPath = "Assets/Audio/IDNfinalaudio.mp3";

        [MenuItem("Turtle Game/Set Up Ending Screen")]
        public static void Create()
        {
            var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            if (audioClip == null)
                throw new InvalidOperationException("The ending narration is missing.");

            // Keep a backup of the existing ending layout.
            Directory.CreateDirectory("Logs");
            if (File.Exists(ScenePath))
                File.Copy(ScenePath, "Logs/Ending_Scene-before-setup-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", false);

            var animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationPath);
            if (animation == null)
            {
                animation = new AnimationClip();
                AssetDatabase.CreateAsset(animation, AnimationPath);
            }
            animation.ClearCurves();
            animation.legacy = false;
            animation.frameRate = 60f;
            animation.wrapMode = WrapMode.ClampForever;
            AnimationUtility.SetEditorCurve(animation,
                EditorCurveBinding.FloatCurve("", typeof(CanvasGroup), "m_Alpha"),
                AnimationCurve.EaseInOut(0f, 0f, 1.5f, 1f));
            var settings = AnimationUtility.GetAnimationClipSettings(animation);
            settings.startTime = 0f;
            settings.stopTime = 1.5f;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(animation, settings);
            EditorUtility.SetDirty(animation);

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            RenderSettings.skybox = null;
            foreach (var camera in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true)))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
            var text = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TextMeshProUGUI>(true))
                .FirstOrDefault(candidate => candidate.text.Contains("thousands of baby sea turtles"));
            if (text == null)
                throw new InvalidOperationException("The ending message was not found in the scene.");
            var canvas = text.canvas;
            var scaler = GetOrAdd<CanvasScaler>(canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // Remove the inherited menu scaling so the ending fits any viewport size.
            text.transform.SetParent(canvas.transform, false);
            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = new Vector2(0.12f, 0.16f);
            rect.anchorMax = new Vector2(0.88f, 0.84f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.margin = Vector4.zero;
            text.fontSize = 52f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 24f;
            text.fontSizeMax = 52f;
            text.alignment = TextAlignmentOptions.Center;
            text.text = text.text.Replace("unnecesary", "unnecessary");
            var messageObject = text.gameObject;
            var group = GetOrAdd<CanvasGroup>(messageObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            text.raycastTarget = false;
            GetOrAdd<Animator>(messageObject).cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var audio = GetOrAdd<AudioSource>(messageObject);
            audio.clip = audioClip;
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 0f;

            var blackObject = GetChild(canvas.transform, "Ending Transition");
            Stretch(blackObject.GetComponent<RectTransform>());
            var blackImage = GetOrAdd<Image>(blackObject);
            blackImage.color = Color.black;
            var blackGroup = GetOrAdd<CanvasGroup>(blackObject);
            blackGroup.alpha = 0f;
            blackGroup.interactable = false;
            blackGroup.blocksRaycasts = false;
            blackObject.transform.SetAsLastSibling();

            var menuObject = GetChild(canvas.transform, "Ending Menu");
            Stretch(menuObject.GetComponent<RectTransform>());
            GetOrAdd<Image>(menuObject).color = new Color(0.96f, 0.95f, 0.92f);
            var menuGroup = GetOrAdd<CanvasGroup>(menuObject);
            menuGroup.alpha = 0f;
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
            var menu = GetOrAdd<EndingMenu>(menuObject);
            menuObject.transform.SetAsLastSibling();

            var logoSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/UI/FinalLogo.png")
                .OfType<Sprite>().FirstOrDefault();
            if (logoSprite == null)
                throw new InvalidOperationException("The game logo sprite is missing.");
            var logoObject = GetChild(menuObject.transform, "Game Logo");
            var logoRect = logoObject.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.23f, 0.49f);
            logoRect.anchorMax = new Vector2(0.77f, 0.87f);
            logoRect.offsetMin = logoRect.offsetMax = Vector2.zero;
            var logo = GetOrAdd<Image>(logoObject);
            logo.sprite = logoSprite;
            logo.preserveAspect = true;
            logo.raycastTarget = false;

            Button restart = CreateButton(menuObject.transform, "Restart", 0.34f, text.font, menu.RestartGame);
            Button quit = CreateButton(menuObject.transform, "Quit", 0.23f, text.font, menu.QuitGame);
            var restartNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = quit, selectOnUp = quit };
            var quitNavigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = restart, selectOnUp = restart };
            restart.navigation = restartNavigation;
            quit.navigation = quitNavigation;
            var serializedMenu = new SerializedObject(menu);
            serializedMenu.FindProperty("restartButton").objectReferenceValue = restart;
            serializedMenu.ApplyModifiedPropertiesWithoutUndo();

            GetOrAdd<GraphicRaycaster>(canvas.gameObject);
            var eventSystem = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            GetOrAdd<InputSystemUIInputModule>(eventSystem.gameObject).AssignDefaultActions();

            var sequence = GetOrAdd<EndingSequence>(messageObject);
            var serializedSequence = new SerializedObject(sequence);
            serializedSequence.FindProperty("fadeInAnimation").objectReferenceValue = animation;
            serializedSequence.FindProperty("narration").objectReferenceValue = audio;
            serializedSequence.FindProperty("wordTimings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Audio/EndingNarrationTimings.json");
            serializedSequence.FindProperty("endingMenu").objectReferenceValue = menu;
            serializedSequence.FindProperty("transitionBlack").objectReferenceValue = blackGroup;
            serializedSequence.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(entry => entry.path == ScenePath);
            if (index < 0)
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else
                scenes[index].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = messageObject;
            Debug.Log("Ending screen saved: 1.5-second alpha fade synchronized with " +
                audioClip.name + " (" + audioClip.length.ToString("F2") + " seconds).");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent<T>(out var component) ? component : target.AddComponent<T>();
        }

        private static GameObject GetChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
                return child.gameObject;
            var target = new GameObject(name, typeof(RectTransform));
            target.layer = 5;
            target.transform.SetParent(parent, false);
            return target;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Button CreateButton(Transform parent, string label, float height,
            TMP_FontAsset font, UnityAction action)
        {
            var target = GetChild(parent, label + " Button");
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, height);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(380f, 84f);
            var image = GetOrAdd<Image>(target);
            image.color = Color.white;
            var button = GetOrAdd<Button>(target);
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.24f, 0.21f, 0.35f);
            colors.highlightedColor = new Color(0.39f, 0.34f, 0.52f);
            colors.pressedColor = new Color(0.16f, 0.14f, 0.25f);
            colors.selectedColor = new Color(0.34f, 0.29f, 0.47f);
            button.colors = colors;
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var labelObject = GetChild(target.transform, "Label");
            Stretch(labelObject.GetComponent<RectTransform>());
            var text = GetOrAdd<TextMeshProUGUI>(labelObject);
            text.font = font;
            text.text = label;
            text.fontSize = 36f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return button;
        }
    }
}

