using System.Collections;
using UnityEngine;

namespace TurtleGame
{
    /// <summary>A short record scratch breaks the calm at the moon's final stop, before the car.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource), typeof(AudioDistortionFilter), typeof(AudioLowPassFilter))]
    public sealed class BeachMusic : MonoBehaviour
    {
        [Header("Música de la playa")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;
        [SerializeField, Min(0.01f)] private float fadeInDuration = 2.5f;

        [Header("Disco rayado antes del automóvil")]
        [SerializeField] private MoonFairyGuide moonGuide;
        [SerializeField, Min(0.1f)] private float scratchDuration = 0.75f;
        [SerializeField, Min(0.04f)] private float stutterInterval = 0.12f;
        [SerializeField, Range(0.05f, 1f)] private float finalPitch = 0.22f;
        [SerializeField, Range(0f, 1f)] private float maximumDistortion = 0.5f;
        [SerializeField, Range(200f, 22000f)] private float finalCutoff = 1600f;

        private AudioSource source;
        private AudioDistortionFilter distortion;
        private AudioLowPassFilter lowPass;
        private bool playing;
        private bool paused;
        private bool scratching;
        private bool stopped;
        private float fadeElapsed;
        private float scratchVolume;
        private float scratchElapsed;
        private int stutterSample;
        private int lastStutterCycle;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            distortion = GetComponent<AudioDistortionFilter>();
            lowPass = GetComponent<AudioLowPassFilter>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        private void OnEnable()
        {
            playing = paused = scratching = stopped = false;
            fadeElapsed = 0f;
            scratchElapsed = 0f;
            source.volume = 0f;
            source.pitch = 1f;
            distortion.enabled = lowPass.enabled = false;
            StartCoroutine(PlayWhenLoaded());
        }

        private IEnumerator PlayWhenLoaded()
        {
            AudioClip clip = source.clip;
            if (clip == null)
            {
                Debug.LogError("BeachMusic requires a music clip on its AudioSource.", this);
                yield break;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading)
                yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogError("Could not load the beach music.", this);
                yield break;
            }
            // A late load must never start the calm track over the car or ending.
            if (stopped)
                yield break;
            source.Play();
            playing = true;
        }

        private void Update()
        {
            if (!playing || stopped)
                return;
            if (Time.timeScale <= 0f)
            {
                if (!paused)
                {
                    source.Pause();
                    paused = true;
                }
                return;
            }
            if (paused)
            {
                source.UnPause();
                paused = false;
            }
            if (!scratching && moonGuide != null && moonGuide.HasReachedRoad)
                BeginRecordScratch();
            if (scratching)
            {
                scratchElapsed += Time.deltaTime;
                if (scratchElapsed >= scratchDuration)
                    StopMusic();
                else
                    ApplyScratch(scratchElapsed);
                return;
            }
            fadeElapsed += Time.deltaTime;
            source.volume = musicVolume * Mathf.SmoothStep(0f, 1f, fadeElapsed / fadeInDuration);
        }

        private void BeginRecordScratch()
        {
            if (scratching || stopped)
                return;
            scratching = true;
            if (!playing)
                return;
            if (paused)
            {
                source.UnPause();
                paused = false;
            }
            scratchVolume = source.volume;
            int fragmentSamples = Mathf.CeilToInt(stutterInterval * source.clip.frequency);
            stutterSample = Mathf.Clamp(source.timeSamples - fragmentSamples, 0,
                Mathf.Max(0, source.clip.samples - fragmentSamples - 1));
            lastStutterCycle = 0;
            distortion.enabled = lowPass.enabled = true;
            // Break the calm immediately, without waiting for a gradual build-up.
            source.timeSamples = stutterSample;
            ApplyScratch(0f);
        }

        private void ApplyScratch(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / scratchDuration);
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            source.pitch = Mathf.Lerp(0.72f, finalPitch, blend);
            distortion.distortionLevel = maximumDistortion * Mathf.Lerp(0.8f, 1f, blend);
            lowPass.cutoffFrequency = Mathf.Lerp(8000f, finalCutoff, blend);

            // Rewind the same short fragment, rather than merely saturating the whole song.
            int cycle = Mathf.FloorToInt(elapsed / stutterInterval);
            if (cycle != lastStutterCycle)
            {
                source.timeSamples = stutterSample;
                lastStutterCycle = cycle;
            }
            float phase = Mathf.Repeat(elapsed, stutterInterval) / stutterInterval;
            float flutter = 0.65f + 0.35f * Mathf.Sin(phase * Mathf.PI);
            float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, progress));
            source.volume = scratchVolume * flutter * fadeOut;
        }

        // The encounter calls this before starting the engine, including an early crossing.
        public void StopMusic()
        {
            stopped = true;
            playing = scratching = paused = false;
            source.Stop();
            source.volume = 0f;
            source.pitch = 1f;
            distortion.enabled = lowPass.enabled = false;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            StopMusic();
        }
    }
}
