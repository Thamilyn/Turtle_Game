
using System.Collections;
using UnityEngine;
using TurtleGame;

public class BeachMainMenu : MonoBehaviour
{
    [Header("Menu UI")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Turtle")]
    [SerializeField] private TurtlePlayerController turtleController;

    [Header("Gameplay Intro")]
    [SerializeField] private GameplayIntroUI gameplayIntroUI;

    private bool gameStarted = false;

    private void Start()
    {
        Time.timeScale = 1f;

        if (turtleController != null)
            turtleController.ReadKeyboard = false;

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
        }
    }

    public void PlayGame()
    {
        if (gameStarted)
            return;

        gameStarted = true;

        if (turtleController != null)
            turtleController.ReadKeyboard = true;

        if (gameplayIntroUI != null)
            gameplayIntroUI.PlaySequence();

        StartCoroutine(FadeOutMenu());
    }

    private IEnumerator FadeOutMenu()
    {
        if (menuCanvasGroup == null)
            yield break;

        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            menuCanvasGroup.alpha =
                Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            yield return null;
        }

        menuCanvasGroup.alpha = 0f;
        menuCanvasGroup.gameObject.SetActive(false);
    }
}
