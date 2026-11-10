using System.Collections;
using UnityEngine;

namespace TurtleGame
{
    /// <summary>Keeps the beach calm until road contact cuts the music for the passing car.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource), typeof(AudioDistortionFilter), typeof(AudioLowPassFilter))]
    public sealed class BeachMusic : MonoBehaviour
    {
        [Header("Música de la playa")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;
        [SerializeField, Min(0.01f)] private float fadeInDuration = 2.5f;

        private AudioSource source;
        private AudioDistortionFilter distortion;
        private AudioLowPassFilter lowPass;
        private bool playing;
        private bool paused;
        private bool stopped;
        private float fadeElapsed;
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
            playing = paused = stopped = false;
            fadeElapsed = 0f;
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
            fadeElapsed += Time.deltaTime;
            source.volume = musicVolume * Mathf.SmoothStep(0f, 1f, fadeElapsed / fadeInDuration);
        }

        // The encounter calls this before starting the engine, including an early crossing.
        public void StopMusic()
        {
            stopped = true;
            playing = paused = false;
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
