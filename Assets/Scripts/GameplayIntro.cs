
using System.Collections;
using UnityEngine;

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
        // Show controls.
        yield return Fade(controlsGroup, 0f, 1f, controlsFadeIn);
        yield return new WaitForSeconds(controlsDisplayTime);
        yield return Fade(controlsGroup, 1f, 0f, controlsFadeOut);

        // Then show the objective.
        yield return Fade(objectiveGroup, 0f, 1f, objectiveFadeIn);
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
