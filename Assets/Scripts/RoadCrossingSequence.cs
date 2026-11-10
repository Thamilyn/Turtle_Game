using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TurtleGame
{
    /// <summary>One-shot road encounter, followed by a heartbeat and the ending message.</summary>
    [DisallowMultipleComponent]
    public sealed class RoadCrossingSequence : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private TurtlePlayerController player;
        [SerializeField] private BoxCollider roadSurface;
        [SerializeField] private Transform car;
        [SerializeField] private Transform[] wheels;
        [SerializeField] private AudioSource engine;
        [SerializeField] private BeachMusic beachMusic;
        [SerializeField] private Camera viewingCamera;
        [SerializeField] private Image screenOverlay;
        [SerializeField] private string endingScene = "Ending_Scene";

        [Header("Pasada del automóvil")]
        [SerializeField, Min(0.1f)] private float approachDistance = 2.2f;
        [SerializeField, Min(1f)] private float carSpeed = 22f;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.95f;
        [SerializeField, Min(0.01f)] private float wheelRadius = 0.34f;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 3f, -7f);
        [SerializeField, Min(0.1f)] private float automaticApproachDistance = 0.1f;

        [Header("Palpito negro y rojo")]
        [SerializeField, Min(1)] private int heartbeatCount = 3;
        [SerializeField, Min(0.5f)] private float heartbeatDuration = 1f;
        [SerializeField, Min(0f)] private float finalBlackDuration = 0.6f;
        [SerializeField] private Color heartbeatRed = new Color(0.55f, 0f, 0.015f, 1f);

        public bool HasStarted { get; private set; }

        private CharacterController character;
        private TurtleFollowCamera followCamera;
        private global::PauseMenuManager[] pauseMenus;
        private bool[] pauseEnabled;
        private bool playerWasEnabled;
        private bool followWasEnabled;
        private Vector3 carStart;
        private Vector3 cameraStart;
        private Quaternion cameraRotation;
        private Vector3 crossingPoint;
        private float previousCarDistance;
        private MoonFairyGuide moonGuide;
        private bool automaticApproach;
        private bool playerReadKeyboard;

        private void Awake()
        {
            if (player == null || roadSurface == null || car == null || screenOverlay == null)
            {
                Debug.LogError("RoadCrossingSequence requires the turtle, road, car and screen overlay.", this);
                enabled = false;
                return;
            }
            character = player.GetComponent<CharacterController>();
            if (viewingCamera != null)
            {
                followCamera = viewingCamera.GetComponent<TurtleFollowCamera>();
                moonGuide = viewingCamera.GetComponent<MoonFairyGuide>();
            }
            car.gameObject.SetActive(false);
            screenOverlay.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (HasStarted || Time.timeScale <= 0f || !player.isActiveAndEnabled || !character.enabled)
                return;

            Bounds road = roadSurface.bounds;
            Bounds feet = character.bounds;
            if (!automaticApproach && moonGuide != null && moonGuide.HasReachedRoad &&
                player.IsGrounded && feet.center.x > road.min.x && feet.center.x < road.max.x &&
                feet.max.z >= road.min.z - automaticApproachDistance && feet.min.z < road.min.z)
            {
                automaticApproach = true;
                playerReadKeyboard = player.ReadKeyboard;
                player.ReadKeyboard = false;
                player.BeginAutomaticMovement(Vector2.up);
            }

            // A collider grazing the edge is not a visible step onto the asphalt.
            // Keep walking until the turtle's center is clearly inside the road.
            const float stepOntoAsphalt = 0.25f;
            bool onAsphalt = feet.center.x > road.min.x + stepOntoAsphalt &&
                             feet.center.x < road.max.x - stepOntoAsphalt &&
                             feet.center.z > road.min.z + stepOntoAsphalt &&
                             feet.center.z < road.max.z - stepOntoAsphalt;
            if (onAsphalt && player.IsGrounded && Mathf.Abs(feet.min.y - road.max.y) < 0.3f)
                BeginCrossing();
        }

        private void BeginCrossing()
        {
            if (!Application.CanStreamedLevelBeLoaded(endingScene))
            {
                Debug.LogError("Add the ending message scene to the active Build Profile: " + endingScene, this);
                enabled = false;
                return;
            }
            HasStarted = true;
            if (beachMusic != null)
                beachMusic.StopMusic();
            playerWasEnabled = player.enabled;
            player.enabled = false;
            pauseMenus = FindObjectsByType<global::PauseMenuManager>(FindObjectsSortMode.None);
            pauseEnabled = new bool[pauseMenus.Length];
            for (int i = 0; i < pauseMenus.Length; i++)
            {
                pauseEnabled[i] = pauseMenus[i].enabled;
                pauseMenus[i].enabled = false;
            }
            if (followCamera != null)
            {
                followWasEnabled = followCamera.enabled;
                followCamera.enabled = false;
            }
            if (viewingCamera != null)
            {
                cameraStart = viewingCamera.transform.position;
                cameraRotation = viewingCamera.transform.rotation;
            }

            Bounds road = roadSurface.bounds;
            crossingPoint = new Vector3(player.transform.position.x, road.max.y,
                Mathf.Clamp(player.transform.position.z, road.min.z + 1f, road.max.z - 1f));
            carStart = crossingPoint + Vector3.left * approachDistance;
            car.position = carStart;
            car.rotation = Quaternion.LookRotation(Vector3.right);
            car.gameObject.SetActive(true);
            if (engine != null)
            {
                engine.volume = engineVolume;
                engine.timeSamples = 0;
                engine.Play();
            }
            StartCoroutine(PlayEncounter());
        }

        private IEnumerator PlayEncounter()
        {
            float elapsed = 0f;
            float arrivalTime = approachDistance / carSpeed;
            while (elapsed < arrivalTime)
            {
                elapsed += Time.unscaledDeltaTime;
                MoveCar(elapsed);
                if (viewingCamera != null)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, elapsed / 0.08f);
                    Vector3 destination = player.transform.position + cameraOffset;
                    viewingCamera.transform.position = Vector3.Lerp(cameraStart, destination, blend);
                    Quaternion rotation = Quaternion.LookRotation(
                        crossingPoint + Vector3.up * 0.5f - destination);
                    viewingCamera.transform.rotation = Quaternion.Slerp(cameraRotation, rotation, blend);
                }
                yield return null;
            }

            screenOverlay.color = Color.black;
            screenOverlay.gameObject.SetActive(true);
            if (beachMusic != null)
                beachMusic.StopMusic();
            float pulseElapsed = 0f;
            float pulseDuration = heartbeatCount * heartbeatDuration;
            while (pulseElapsed < pulseDuration)
            {
                pulseElapsed += Time.unscaledDeltaTime;
                elapsed += Time.unscaledDeltaTime;
                MoveCar(elapsed);
                float phase = Mathf.Repeat(pulseElapsed, heartbeatDuration) / heartbeatDuration;
                // Two soft beats per heartbeat; return completely to black between cycles.
                float first = Beat(phase, 0.08f, 0.14f);
                float second = Beat(phase, 0.32f, 0.18f) * 0.65f;
                screenOverlay.color = Color.Lerp(Color.black, heartbeatRed, Mathf.Max(first, second));
                if (engine != null)
                {
                    engine.volume = Mathf.Lerp(engineVolume, 0f, Mathf.Clamp01(pulseElapsed / 0.7f));
                    if (pulseElapsed >= 0.7f && engine.isPlaying)
                        engine.Stop();
                }
                yield return null;
            }
            screenOverlay.color = Color.black;
            if (engine != null)
                engine.Stop();
            yield return new WaitForSecondsRealtime(finalBlackDuration);
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(endingScene);
        }

        private static float Beat(float phase, float center, float width)
        {
            float distance = Mathf.Abs(phase - center) / width;
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
        }

        private void MoveCar(float elapsed)
        {
            float distance = Mathf.Min(elapsed * carSpeed, approachDistance * 2f);
            car.position = carStart + Vector3.right * distance;
            float rotation = (distance - previousCarDistance) / wheelRadius * Mathf.Rad2Deg;
            if (wheels != null)
                foreach (Transform wheel in wheels)
                    if (wheel != null)
                        wheel.Rotate(Vector3.right, rotation, Space.Self);
            previousCarDistance = distance;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (automaticApproach && player != null)
            {
                player.EndAutomaticMovement();
                player.ReadKeyboard = playerReadKeyboard;
            }
            if (!HasStarted)
                return;
            if (beachMusic != null)
                beachMusic.StopMusic();
            if (player != null)
                player.enabled = playerWasEnabled;
            if (followCamera != null)
                followCamera.enabled = followWasEnabled;
            if (pauseMenus != null)
                for (int i = 0; i < pauseMenus.Length; i++)
                    if (pauseMenus[i] != null)
                        pauseMenus[i].enabled = pauseEnabled[i];
            if (engine != null)
                engine.Stop();
            if (screenOverlay != null)
                screenOverlay.gameObject.SetActive(false);
        }
    }
}
