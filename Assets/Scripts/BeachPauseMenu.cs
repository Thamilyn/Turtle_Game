
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenuManager : MonoBehaviour
{
    [Header("Pause Menu UI")]
    [SerializeField] private CanvasGroup pauseCanvasGroup;

    [Header("Main Menu")]
    [Tooltip("Assign the CanvasGroup used by your main menu.")]
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;

    private bool isPaused;
    private float previousTimeScale = 1f;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    private void Start()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;
            pauseCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!EscapePressed())
            return;

        // Don't allow pausing while the main menu is still showing.
        if (mainMenuCanvasGroup != null &&
            mainMenuCanvasGroup.gameObject.activeInHierarchy)
            return;

        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (isPaused || pauseCanvasGroup == null)
            return;

        isPaused = true;

        previousTimeScale = Time.timeScale;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        Time.timeScale = 0f;

        pauseCanvasGroup.gameObject.SetActive(true);
        pauseCanvasGroup.alpha = 1f;
        pauseCanvasGroup.interactable = true;
        pauseCanvasGroup.blocksRaycasts = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;

        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.gameObject.SetActive(false);
        }

        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
    }

    public void QuitGame()
    {
        // Restore time before leaving.
        Time.timeScale = 1f;

        #if UNITY_EDITOR
        Debug.Log("Quit Game only works in a built game.");
        #else
        Application.Quit();
        #endif
    }

    private static bool EscapePressed()
    {
        #if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;
        #elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape) && Debug.Log("Escape Pressed");
        #else
        return false;
        #endif

        
    }

    private void OnDestroy()
    {
        // Prevent the game remaining paused if this manager is destroyed.
        if (isPaused)
            Time.timeScale = 1f;
    }
}
