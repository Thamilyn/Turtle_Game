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
                "TORTUGA · Prototipo de movimiento\nW A S D — moverse por la playa\nCubo provisional: sustituible por tu modelo", style);
        }
    }
}
