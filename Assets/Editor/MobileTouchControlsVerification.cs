using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurtleGame.Editor
{
    [InitializeOnLoad]
    public static class MobileTouchControlsVerification
    {
        private const string PendingKey = "TurtleGame.MobileTouchVerification";
        private static IEnumerator checks;
        private static double deadline;
        private static int lastFrame;

        static MobileTouchControlsVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                    return;
                SessionState.SetBool(PendingKey, false);
                deadline = EditorApplication.timeSinceStartup + 90f;
                lastFrame = -1;
                checks = Verify();
                EditorApplication.update += Tick;
            };
        }

        [MenuItem("Turtle Game/Verificar controles touch")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run from Edit Mode.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Touch checks timed out.");
                if (lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                if (!checks.MoveNext()) Finish(true, "PASS: analog movement, dead zone, multitouch capture, release, pause, focus, menus, automatic movement and cleanup.");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static IEnumerator Verify()
        {
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.13f, 0.2f);
            var root = new GameObject("Touch verification turtle");
            var player = root.AddComponent<TurtlePlayerController>();
            var controls = root.GetComponent<MobileTouchControls>();
            Assert(controls != null, "Controls automatically attached");
            var settings = new SerializedObject(controls);
            settings.FindProperty("previewInEditor").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var pauseRoot = new GameObject("Pause verification");
            pauseRoot.SetActive(false);
            var pause = pauseRoot.AddComponent<global::PauseMenuManager>();
            var group = new GameObject("Pause panel", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var pauseSettings = new SerializedObject(pause);
            pauseSettings.FindProperty("pauseCanvasGroup").objectReferenceValue = group;
            pauseSettings.ApplyModifiedPropertiesWithoutUndo();
            pauseRoot.SetActive(true);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var joystick = Object.FindFirstObjectByType<TouchJoystick>();
            Assert(joystick != null && EventSystem.current != null, "Touch UI and event system created");
            var rect = (RectTransform)joystick.transform;
            var canvas = joystick.GetComponentInParent<Canvas>();
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            float travel = 64f * canvas.scaleFactor;
            var finger = new PointerEventData(EventSystem.current)
            {
                pointerId = 11, position = center + Vector2.right * travel
            };
            joystick.OnPointerDown(finger);
            Assert(joystick.Value.x > 0.99f, "Full analog travel");
            var other = new PointerEventData(EventSystem.current) { pointerId = 12, position = center - Vector2.right * travel };
            joystick.OnPointerDown(other);
            joystick.OnPointerUp(other);
            Assert(joystick.Value.x > 0.99f, "Second finger cannot steal or release joystick");
            finger.position = center + Vector2.one * travel * 3f;
            joystick.OnDrag(finger);
            Assert(Mathf.Abs(joystick.Value.magnitude - 1f) < 0.01f, "Diagonal input is clamped");
            finger.position = center + Vector2.right * travel * 0.5f;
            joystick.OnDrag(finger);
            Assert(joystick.Value.x > 0.3f && joystick.Value.x < 0.6f, "Partial travel preserves analog speed");
            yield return null;
            Assert(player.TouchMovement.x > 0.3f, "Joystick reaches player movement");
            finger.position = center + Vector2.right * travel * 0.05f;
            joystick.OnDrag(finger);
            Assert(joystick.Value == Vector2.zero, "Dead zone prevents drift");
            finger.position = center + Vector2.up * travel;
            joystick.OnDrag(finger);
            joystick.OnPointerUp(finger);
            Assert(joystick.Value == Vector2.zero, "Release recenters joystick");

            joystick.OnPointerDown(finger);
            var button = canvas.GetComponentInChildren<Button>();
            button.onClick.Invoke();
            yield return null;
            yield return null;
            Assert(Time.timeScale == 0f && !canvas.gameObject.activeSelf && player.TouchMovement == Vector2.zero,
                "Pause button opens menu and clears touch input");
            pause.ResumeGame();
            yield return null;
            yield return null;
            Assert(canvas.gameObject.activeSelf && joystick.Value == Vector2.zero, "Resume requires a fresh touch");
            joystick.OnPointerDown(finger);
            controls.SendMessage("OnApplicationFocus", false);
            Assert(player.TouchMovement == Vector2.zero && !canvas.gameObject.activeSelf, "Focus loss clears movement");
            controls.SendMessage("OnApplicationFocus", true);
            player.ReadKeyboard = false;
            yield return null;
            Assert(!canvas.gameObject.activeSelf, "Main-menu input lock also locks touch");
            player.ReadKeyboard = true;
            player.BeginAutomaticMovement(Vector2.up);
            yield return null;
            Assert(!canvas.gameObject.activeSelf, "Automatic sequences hide touch UI");
            player.EndAutomaticMovement();
            yield return null;
            Assert(canvas.gameObject.activeSelf, "Controls return after automatic sequence");
            Directory.CreateDirectory("Logs");
            ScreenCapture.CaptureScreenshot("Logs/mobile-touch-preview.png");
            for (int i = 0; i < 5; i++) yield return null;
            player.enabled = false;
            yield return null;
            Assert(!canvas.gameObject.activeSelf, "Disabled player hides controls");
            Object.Destroy(root);
            yield return null;
            Assert(canvas == null, "Destroying player removes touch canvas");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("PASS: " + message);
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/mobile-touch-verification-result.txt", message);
            Debug.Log(message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
