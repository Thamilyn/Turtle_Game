using UnityEngine;

namespace TurtleGame
{
    public sealed class PrototypeHelp : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            GUI.Box(new Rect(16, 16, 390, 96), GUIContent.none);
            GUI.Label(new Rect(30, 26, 366, 78),
                "TURTLE · Movement prototype\n" +
                (MobileTouchControls.UsesTouchControls ? "Drag the joystick to move" : "W A S D — move along the beach") +
                "\nPlaceholder cube: replace with your model", style);
        }
    }
}
