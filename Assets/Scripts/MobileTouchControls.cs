using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace TurtleGame
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MobileTouchControls : MonoBehaviour
    {
        [Tooltip("Enable in Play Mode to preview the mobile controls with the mouse.")]
        [SerializeField] private bool previewInEditor;
        [Tooltip("Optional override. Otherwise uses the same font as the scene's existing UI.")]
        [SerializeField] private TMP_FontAsset labelFont;

        private TurtlePlayerController player;
        private Canvas canvas;
        private RectTransform safeArea;
        private TouchJoystick joystick;
        private Button pauseButton;
        private global::PauseMenuManager pauseMenu;
        private bool hadFocus = true;

        public static bool UsesTouchControls
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Application.isMobilePlatform || Touchscreen.current != null;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        private void Awake() => player = GetComponent<TurtlePlayerController>();

        private void Update()
        {
            bool show = (UsesTouchControls || previewInEditor) && hadFocus &&
                player != null && player.AcceptsPlayerInput && Time.timeScale > 0f;
            if (show && canvas == null) BuildUI();
            if (canvas == null) return;
            if (canvas.gameObject.activeSelf != show)
            {
                canvas.gameObject.SetActive(show);
                joystick.ResetInput();
            }
            if (!show)
            {
                player.TouchMovement = Vector2.zero;
                return;
            }

            Rect safe = Screen.safeArea;
            safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            // A scene may enable its pause manager after the intro.
            if (pauseMenu == null || !pauseMenu.isActiveAndEnabled)
                pauseMenu = FindFirstObjectByType<global::PauseMenuManager>();
            pauseButton.gameObject.SetActive(pauseMenu != null && pauseMenu.isActiveAndEnabled);
            player.TouchMovement = joystick.Value;
        }

        private void BuildUI()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("Touch EventSystem", typeof(EventSystem));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, gameObject.scene);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }

            var root = new GameObject("Mobile touch controls", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            safeArea = CreateRect("Safe area", root.transform, Vector2.zero, Vector2.zero);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;

            var pad = Circle("Movement joystick", safeArea, new Vector2(210f, 210f),
                new Color(0f, 0f, 0f, 0.38f), true);
            pad.anchorMin = pad.anchorMax = Vector2.zero;
            pad.anchoredPosition = new Vector2(145f, 145f);
            var rim = Circle("Outer ring", pad, new Vector2(210f, 210f),
                new Color(1f, 1f, 1f, 0.85f));
            rim.GetComponent<TouchControlGraphic>().RingWidth = 3f;
            var inner = Circle("Travel ring", pad, new Vector2(138f, 138f),
                new Color(1f, 1f, 1f, 0.2f));
            inner.GetComponent<TouchControlGraphic>().RingWidth = 2f;
            var thumb = Circle("Thumb", pad, new Vector2(82f, 82f),
                new Color(1f, 1f, 1f, 0.95f));
            joystick = pad.gameObject.AddComponent<TouchJoystick>();
            joystick.Initialize(thumb);
            Label("Movement hint", pad, "DRAG TO MOVE", new Vector2(0f, -126f),
                new Vector2(260f, 28f), 17);

            var pause = Circle("Pause", safeArea, new Vector2(76f, 76f),
                new Color(0f, 0f, 0f, 0.5254902f), true);
            pause.anchorMin = pause.anchorMax = Vector2.one;
            pause.anchoredPosition = new Vector2(-64f, -64f);
            for (int i = 0; i < 2; i++)
            {
                var bar = CreateRect("Pause bar", pause, new Vector2(7f, 27f),
                    new Vector2(i == 0 ? -8f : 8f, 0f));
                var graphic = bar.gameObject.AddComponent<Image>();
                graphic.color = Color.white;
                graphic.raycastTarget = false;
            }
            pauseButton = pause.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pause.GetComponent<TouchControlGraphic>();
            pauseButton.onClick.AddListener(() =>
            {
                joystick.ResetInput();
                player.TouchMovement = Vector2.zero;
                if (pauseMenu != null && pauseMenu.isActiveAndEnabled) pauseMenu.PauseGame();
            });
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform Circle(string name, Transform parent, Vector2 size, Color color,
            bool raycast = false)
        {
            var rect = CreateRect(name, parent, size, Vector2.zero);
            var graphic = rect.gameObject.AddComponent<TouchControlGraphic>();
            graphic.color = color;
            graphic.raycastTarget = raycast;
            return rect;
        }

        private void Label(string name, Transform parent, string value, Vector2 position,
            Vector2 size, int fontSize)
        {
            // Menu and intro labels can be inactive by the time gameplay controls appear.
            if (labelFont == null)
            {
                foreach (var sceneText in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
                {
                    if (sceneText.gameObject.scene != gameObject.scene || sceneText.font == null)
                        continue;
                    labelFont = sceneText.font;
                    break;
                }
            }
            var text = CreateRect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void OnDisable()
        {
            if (player != null) player.TouchMovement = Vector2.zero;
            if (joystick != null) joystick.ResetInput();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            hadFocus = focused;
            if (!focused) OnDisable();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) OnDisable();
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
