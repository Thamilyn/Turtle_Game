using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TurtleGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class EndingMenu : MonoBehaviour
    {
        [SerializeField] private string restartScene = "MoonBeach";
        [SerializeField, Min(0.01f)] private float fadeInDuration = 1.5f;
        [SerializeField] private Button restartButton;

        private CanvasGroup group;
        private bool showing;
        private bool leaving;

        private void Awake()
        {
            Hide();
        }

        public void Hide()
        {
            StopAllCoroutines();
            if (group == null)
                group = GetComponent<CanvasGroup>();
            showing = false;
            leaving = false;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void Show()
        {
            if (showing)
                return;
            showing = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.SmoothStep(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            if (EventSystem.current != null && restartButton != null)
                EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        public void RestartGame()
        {
            if (leaving || !group.interactable)
                return;
            if (!Application.CanStreamedLevelBeLoaded(restartScene))
            {
                Debug.LogError("The restart scene is missing from Build Settings: " + restartScene, this);
                return;
            }
            leaving = true;
            group.interactable = false;
            group.blocksRaycasts = false;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(restartScene);
        }

        public void QuitGame()
        {
            if (leaving || !group.interactable)
                return;
            GameExit.Quit();
        }
    }
}
