
using System.Collections;
using UnityEngine;
using TurtleGame;

public class GameplayIntroUI : MonoBehaviour
{
    [Header("Controls Image")]
    [SerializeField] private CanvasGroup controlsGroup;
    [SerializeField] private float controlsFadeIn = 1f;
    [SerializeField] private float controlsDisplayTime = 4f;
    [SerializeField] private float controlsFadeOut = 1f;

    [Header("Objective UI")]
    [SerializeField] private CanvasGroup objectiveGroup;
    [SerializeField] private float objectiveFadeIn = 1f;
    [SerializeField] private float objectiveDisplayTime = 5f;
    [SerializeField] private float objectiveFadeOut = 1f;

    [Header("Hatching and Movement")]
    [SerializeField] private TurtleHatchOnMove hatchSequence;

    private bool sequenceStarted;

    private void Start()
    {
        SetInvisible(controlsGroup);
        SetInvisible(objectiveGroup);
    }

    public void PlaySequence()
    {
        if (sequenceStarted)
            return;

        sequenceStarted = true;
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        // Mobile players learn directly from the on-screen joystick.
        if (!MobileTouchControls.UsesTouchControls)
        {
            yield return Fade(controlsGroup, 0f, 1f, controlsFadeIn);
            yield return new WaitForSeconds(controlsDisplayTime);
            yield return Fade(controlsGroup, 1f, 0f, controlsFadeOut);
        }

        // Let the hatch finish before inviting the player to move.
        while (hatchSequence != null && !hatchSequence.HasFinishedHatching)
            yield return null;

        yield return Fade(objectiveGroup, 0f, 1f, objectiveFadeIn);
        if (hatchSequence != null)
            hatchSequence.AllowWalking();
        yield return new WaitForSeconds(objectiveDisplayTime);
        yield return Fade(objectiveGroup, 1f, 0f, objectiveFadeOut);
    }

    private IEnumerator Fade(
        CanvasGroup group,
        float startAlpha,
        float endAlpha,
        float duration)
    {
        if (group == null)
            yield break;

        group.gameObject.SetActive(true);
        group.alpha = startAlpha;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(
                startAlpha,
                endAlpha,
                Mathf.Clamp01(elapsed / duration));

            yield return null;
        }

        group.alpha = endAlpha;
    }

    private void SetInvisible(CanvasGroup group)
    {
        if (group == null)
            return;

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
