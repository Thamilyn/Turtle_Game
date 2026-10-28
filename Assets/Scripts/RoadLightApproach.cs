using System.Collections.Generic;
using UnityEngine;

namespace TurtleGame
{
    /// <summary>Fades the road lights and their visible bulbs as the turtle approaches.</summary>
    [DisallowMultipleComponent]
    public sealed class RoadLightApproach : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 destination = new Vector3(0f, 0f, 124f);
        [SerializeField, Min(1f)] private float fadeStartDistance = 30f;
        [SerializeField, Min(0f)] private float fullIntensityDistance = 12f;
        [SerializeField, Range(0f, 1f)] private float initialBrightness = 0f;
        private readonly List<Light> lights = new List<Light>();
        private readonly List<float> intensities = new List<float>();
        private readonly List<Renderer> bulbs = new List<Renderer>();
        private readonly List<Color> emissions = new List<Color>();
        private MaterialPropertyBlock properties;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void OnEnable()
        {
            if (target == null && TryGetComponent<TurtleFollowCamera>(out var follow))
                target = follow.Target;
            lights.Clear();
            intensities.Clear();
            bulbs.Clear();
            emissions.Clear();
            properties = new MaterialPropertyBlock();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.name != "Warm road light")
                        continue;
                    lights.Add(light);
                    intensities.Add(light.intensity);
                }
                foreach (var bulb in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (bulb.name != "Distant light source" || bulb.sharedMaterial == null ||
                        !bulb.sharedMaterial.HasProperty(EmissionColor))
                        continue;
                    bulbs.Add(bulb);
                    emissions.Add(bulb.sharedMaterial.GetColor(EmissionColor));
                }
            }
            UpdateBrightness();
        }

        private void LateUpdate() => UpdateBrightness();

        private void UpdateBrightness()
        {
            if (target == null)
                return;
            Vector2 offset = new Vector2(target.position.x - destination.x, target.position.z - destination.z);
            float start = Mathf.Max(fadeStartDistance, fullIntensityDistance + 0.01f);
            float progress = Mathf.InverseLerp(start, fullIntensityDistance, offset.magnitude);
            float brightness = Mathf.Lerp(initialBrightness, 1f, Mathf.SmoothStep(0f, 1f, progress));
            for (int i = 0; i < lights.Count; i++)
                if (lights[i] != null)
                {
                    lights[i].enabled = brightness > 0f;
                    lights[i].intensity = intensities[i] * brightness;
                }
            for (int i = 0; i < bulbs.Count; i++)
            {
                if (bulbs[i] == null)
                    continue;
                bulbs[i].enabled = brightness > 0f;
                bulbs[i].GetPropertyBlock(properties);
                properties.SetColor(EmissionColor, emissions[i] * brightness);
                bulbs[i].SetPropertyBlock(properties);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < lights.Count; i++)
                if (lights[i] != null)
                {
                    lights[i].enabled = true;
                    lights[i].intensity = intensities[i];
                }
            for (int i = 0; i < bulbs.Count; i++)
            {
                if (bulbs[i] == null)
                    continue;
                bulbs[i].enabled = true;
                bulbs[i].GetPropertyBlock(properties);
                properties.SetColor(EmissionColor, emissions[i]);
                bulbs[i].SetPropertyBlock(properties);
            }
        }
    }
}
