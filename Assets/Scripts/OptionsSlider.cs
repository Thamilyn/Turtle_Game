using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TurtleGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class OptionsSlider : MonoBehaviour
    {
        public enum Setting
        {
            Volume,
            Brightness
        }

        [SerializeField] private Setting setting;

        private Slider slider;
        private GameSettings settings;
        private RectTransform sliderLabels;
        private TMP_Text valueLabel;

        public Setting ControlledSetting => setting;

        private void OnEnable()
        {
            slider = GetComponent<Slider>();
            settings = GameSettings.Instance;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            CreateSliderLabels();
            Refresh();
            settings.Changed += Refresh;
            slider.onValueChanged.AddListener(ApplyValue);
        }

        private void CreateSliderLabels()
        {
            if (sliderLabels != null || slider.handleRect == null)
                return;

            float width = ((RectTransform)transform).rect.width;
            float fontSize = width * 0.06f;
            Color secondaryColor = new Color(0.8f, 0.85f, 0.88f);
            var labelsObject = new GameObject("Slider labels", typeof(RectTransform));
            labelsObject.layer = gameObject.layer;
            sliderLabels = (RectTransform)labelsObject.transform;
            sliderLabels.SetParent(slider.handleRect.parent, false);
            sliderLabels.anchorMin = Vector2.zero;
            sliderLabels.anchorMax = Vector2.one;
            sliderLabels.offsetMin = sliderLabels.offsetMax = Vector2.zero;

            CreateLabel("Minimum value", "0%", new Vector2(0f, 0.25f), new Vector2(0f, 1f),
                -fontSize * 0.8f, TextAlignmentOptions.TopLeft, fontSize * 0.85f, secondaryColor);
            CreateLabel("Maximum value", "100%", new Vector2(1f, 0.25f), Vector2.one,
                -fontSize * 0.8f, TextAlignmentOptions.TopRight, fontSize * 0.85f, secondaryColor);
            valueLabel = CreateLabel("Current value", "", new Vector2(1f, 0.75f), new Vector2(1f, 0f),
                fontSize * 0.15f, TextAlignmentOptions.BottomRight, fontSize, new Color(0.72f, 0.9f, 0.95f));

            if (setting != Setting.Brightness)
                return;

            // Brightness has a reference midpoint; volume is simply a minimum-to-maximum scale.
            float defaultValue = GameSettings.DefaultBrightness;
            var markerObject = new GameObject("Default brightness marker", typeof(RectTransform), typeof(Image));
            markerObject.layer = gameObject.layer;
            var markerRect = (RectTransform)markerObject.transform;
            markerRect.SetParent(sliderLabels, false);
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(defaultValue, 0.25f);
            markerRect.pivot = new Vector2(0.5f, 1f);
            markerRect.anchoredPosition = new Vector2(0f, -fontSize * 0.1f);
            markerRect.sizeDelta = new Vector2(width * 0.005f, fontSize * 0.35f);
            var tick = markerObject.GetComponent<Image>();
            tick.color = secondaryColor;
            tick.raycastTarget = false;
            CreateLabel("Default brightness label", $"Default {defaultValue * 100f:0}%",
                new Vector2(defaultValue, 0.25f), new Vector2(0.5f, 1f), -fontSize * 0.8f,
                TextAlignmentOptions.Top, fontSize * 0.85f, secondaryColor);
        }

        private TMP_Text CreateLabel(string name, string text, Vector2 anchor, Vector2 pivot,
            float yOffset, TextAlignmentOptions alignment, float fontSize, Color color)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(sliderLabels, false);
            labelRect.anchorMin = labelRect.anchorMax = anchor;
            labelRect.pivot = pivot;
            labelRect.anchoredPosition = new Vector2(0f, yOffset);
            labelRect.sizeDelta = new Vector2(((RectTransform)transform).rect.width * 0.55f, fontSize * 1.5f);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            var menuLabel = transform.parent.GetComponentInChildren<TMP_Text>(true);
            if (menuLabel != null)
                label.font = menuLabel.font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private void Refresh()
        {
            float value = setting == Setting.Volume ? settings.MasterVolume : settings.Brightness;
            slider.SetValueWithoutNotify(value);
            if (valueLabel != null)
                valueLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private void ApplyValue(float value)
        {
            if (setting == Setting.Volume)
                settings.SetMasterVolume(value);
            else
                settings.SetBrightness(value);
        }

        private void OnDisable()
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(ApplyValue);
            if (settings != null)
            {
                settings.Changed -= Refresh;
                settings.Save();
            }
        }
    }
}
