
using UnityEngine;
using System.Collections;
using TurtleGame;

public class BeachMainMenu : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Turtle")]
    [SerializeField] private TurtlePlayerController turtleController;

    private bool gameStarted;

    private void Start()
    {
        Time.timeScale = 1f;

        // Prevent movement until Play is clicked.
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
    }
}
