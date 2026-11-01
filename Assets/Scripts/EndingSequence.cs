using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TurtleGame
{
    /// <summary>Plays the ending fade and narration from the same audio clock.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup), typeof(Animator), typeof(AudioSource))]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class EndingSequence : MonoBehaviour
    {
        [SerializeField] private AnimationClip fadeInAnimation;
        [SerializeField] private AudioSource narration;
        [SerializeField] private TextAsset wordTimings;
        [SerializeField] private Color highlightColor = new Color(1f, 0.76f, 0.24f, 1f);
        [SerializeField] private EndingMenu endingMenu;
        [SerializeField] private CanvasGroup transitionBlack;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 1.5f;

        [System.Serializable]
        private sealed class WordTiming
        {
            public string word;
            public float start;
            public float end;
        }

        [System.Serializable]
        private sealed class NarrationTimings
        {
            public WordTiming[] words;
        }

        private CanvasGroup messageGroup;
        private PlayableGraph graph;
        private AnimationClipPlayable fadePlayable;
        private double startTime;
        private bool started;
        private TMP_Text message;
        private WordTiming[] words;
        private int highlightedWord = -1;
        private float highlightStrength;

        private void Awake()
        {
            messageGroup = GetComponent<CanvasGroup>();
            messageGroup.alpha = 0f;
            if (narration == null)
                narration = GetComponent<AudioSource>();
            narration.playOnAwake = false;
            message = GetComponent<TMP_Text>();
            if (wordTimings != null)
                words = JsonUtility.FromJson<NarrationTimings>(wordTimings.text).words;
        }

        private void OnEnable()
        {
            messageGroup.alpha = 0f;
            highlightedWord = -1;
            if (endingMenu != null)
                endingMenu.Hide();
            if (transitionBlack != null)
            {
                transitionBlack.alpha = 0f;
                transitionBlack.blocksRaycasts = false;
            }
            if (message != null)
                message.OnPreRenderText += ColorSpokenWord;
            StartCoroutine(PlayWhenLoaded());
        }

        private IEnumerator PlayWhenLoaded()
        {
            if (fadeInAnimation == null || narration.clip == null)
            {
                Debug.LogError("EndingSequence requires a fade animation and narration clip.", this);
                yield break;
            }

            AudioClip clip = narration.clip;
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading)
                yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogError("Could not load the ending narration.", this);
                yield break;
            }

            graph = PlayableGraph.Create("Ending fade");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            fadePlayable = AnimationClipPlayable.Create(graph, fadeInAnimation);
            var output = AnimationPlayableOutput.Create(graph, "Message alpha", GetComponent<Animator>());
            output.SetSourcePlayable(fadePlayable);
            graph.Play();
            fadePlayable.SetTime(0d);
            graph.Evaluate(0f);
            if (message != null)
            {
                message.ForceMeshUpdate();
                if (words != null && words.Length != message.textInfo.wordCount)
                {
                    Debug.LogError("Ending narration timings do not match the displayed word count.", this);
                    words = null;
                }
                if (words != null)
                {
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (string.Equals(words[i].word.Trim('.', ',', '!', '?', ':', ';'),
                            message.textInfo.wordInfo[i].GetWord(), System.StringComparison.OrdinalIgnoreCase))
                            continue;
                        Debug.LogError("Ending narration timing does not match word " + (i + 1) + ".", this);
                        words = null;
                        break;
                    }
                }
            }

            // Leave enough scheduling time for the audio device before revealing the text.
            startTime = AudioSettings.dspTime + 0.1d;
            narration.PlayScheduled(startTime);
            started = true;
        }

        private void Update()
        {
            if (!started)
                return;

            // Absolute DSP time avoids accumulated frame drift and works at timeScale = 0.
            double elapsed = System.Math.Max(0d, AudioSettings.dspTime - startTime);
            fadePlayable.SetTime(System.Math.Min(elapsed, fadeInAnimation.length));
            graph.Evaluate(0f);
            UpdateSpokenWord((float)elapsed);
            if (elapsed >= narration.clip.length && endingMenu != null)
            {
                started = false;
                graph.Destroy();
                messageGroup.alpha = 1f;
                StartCoroutine(FadeToMenu());
            }
        }

        private IEnumerator FadeToMenu()
        {
            if (transitionBlack != null)
                transitionBlack.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / fadeOutDuration);
                if (transitionBlack != null)
                    transitionBlack.alpha = progress;
                else
                    messageGroup.alpha = 1f - progress;
                yield return null;
            }
            messageGroup.alpha = 0f;
            if (transitionBlack != null)
                transitionBlack.alpha = 1f;
            endingMenu.Show();
        }

        private void UpdateSpokenWord(float elapsed)
        {
            if (message == null || words == null)
                return;

            highlightedWord = -1;
            for (int i = 0; i < words.Length; i++)
            {
                if (elapsed >= words[i].start && elapsed < words[i].end)
                {
                    highlightedWord = i;
                    // A brief color ramp makes the word change feel smooth without moving the text.
                    highlightStrength = Mathf.SmoothStep(0.4f, 1f,
                        Mathf.Clamp01((elapsed - words[i].start) / 0.08f));
                    break;
                }
            }
            ColorSpokenWord(message.textInfo);
            message.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private void ColorSpokenWord(TMP_TextInfo info)
        {
            int first = -1;
            int last = -1;
            if (highlightedWord >= 0 && highlightedWord < info.wordCount)
            {
                first = info.wordInfo[highlightedWord].firstCharacterIndex;
                last = info.wordInfo[highlightedWord].lastCharacterIndex;
            }

            Color activeColor = Color.Lerp(message.color, highlightColor, highlightStrength);
            activeColor.a = message.color.a;
            for (int i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible)
                    continue;
                Color32 color = i >= first && i <= last ? activeColor : message.color;
                var colors = info.meshInfo[character.materialReferenceIndex].colors32;
                for (int vertex = 0; vertex < 4; vertex++)
                    colors[character.vertexIndex + vertex] = color;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            started = false;
            highlightedWord = -1;
            if (message != null)
            {
                message.OnPreRenderText -= ColorSpokenWord;
                ColorSpokenWord(message.textInfo);
                message.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
            narration.Stop();
            if (graph.IsValid())
                graph.Destroy();
            messageGroup.alpha = 0f;
        }
    }
}
