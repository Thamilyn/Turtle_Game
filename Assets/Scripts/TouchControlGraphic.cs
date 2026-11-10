using UnityEngine;
using UnityEngine.UI;

namespace TurtleGame
{
    /// <summary>Resolution-independent discs and rings for the touch controls.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TouchControlGraphic : MaskableGraphic
    {
        public float RingWidth { get; set; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            float innerRadius = RingWidth > 0f ? Mathf.Max(0f, radius - RingWidth) : 0f;
            const int segments = 64;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * radius, color, Vector2.zero);
                mesh.AddVert(center + direction * innerRadius, color, Vector2.zero);
                if (i == 0) continue;
                int index = i * 2;
                mesh.AddTriangle(index - 2, index + 1, index);
                mesh.AddTriangle(index - 2, index - 1, index + 1);
            }
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out Vector2 local))
                return false;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            return (local - rectTransform.rect.center).sqrMagnitude <= radius * radius;
        }
    }
}
