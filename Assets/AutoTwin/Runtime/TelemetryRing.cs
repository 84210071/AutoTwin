using UnityEngine;
using UnityEngine.UI;

namespace AutoTwin
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TelemetryRing : MaskableGraphic
    {
        [SerializeField] private Color track = new(.11f, .20f, .32f, 1f);
        [SerializeField] private Color accent = new(.29f, .71f, 1f, 1f);
        [SerializeField] private float thickness = 6f;
        [SerializeField] private float sweep = 360f;
        [SerializeField] private float startAngle = 90f;
        [SerializeField, Range(0f, 1f)] private float normalized;

        public void Configure(Color rail, Color tint, float thickness = 6f, float sweep = 360f, float startAngle = 90f)
        {
            track = rail;
            accent = tint;
            this.thickness = thickness;
            this.sweep = sweep;
            this.startAngle = startAngle;
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void SetValue(float normalized, Color? accent = null)
        {
            float value = Mathf.Clamp01(normalized);
            if (this.normalized == value && (!accent.HasValue || this.accent == accent.Value)) return;
            this.normalized = value;
            this.accent = accent ?? this.accent;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f - 8f - thickness * .5f;
            if (radius <= thickness) return;

            float span = Mathf.Clamp(sweep, 0f, 360f);
            float travelled = span * normalized;
            PaintArc(vertices, rect.center, radius, thickness, span, track, track);
            if (travelled <= 0f) return;

            // 透明外晕使用同一参考圆弧，仪表在任意 Canvas 缩放下保持清晰。
            var glow = new Color(accent.r, accent.g, accent.b, accent.a * .12f);
            PaintArc(vertices, rect.center, radius, thickness + 8f, travelled, glow, glow);
            var gradient = new Color(accent.r * .52f, accent.g * .62f, accent.b * .82f, accent.a);
            var tip = Color.Lerp(accent, Color.white, .23f);
            PaintArc(vertices, rect.center, radius, thickness, travelled, gradient, tip);
        }

        private void PaintArc(VertexHelper vertices, Vector2 center, float radius, float width, float degrees, Color from, Color to)
        {
            if (degrees <= 0f) return;
            int steps = Mathf.CeilToInt(degrees / 3.5f);
            int root = vertices.currentVertCount;
            float feather = .85f / Mathf.Max(.65f, canvas == null ? 1f : canvas.scaleFactor);
            for (int i = 0; i <= steps; i++)
            {
                float ratio = i / (float)steps;
                float angle = (startAngle - degrees * ratio) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Color tint = Color.Lerp(from, to, ratio);
                var transparent = new Color(tint.r, tint.g, tint.b, 0f);
                float inner = radius - width * .5f;
                float outer = radius + width * .5f;
                vertices.AddVert(center + direction * (inner - feather), transparent, Vector2.zero);
                vertices.AddVert(center + direction * inner, tint, Vector2.zero);
                vertices.AddVert(center + direction * outer, tint, Vector2.zero);
                vertices.AddVert(center + direction * (outer + feather), transparent, Vector2.zero);
                if (i == 0) continue;
                int index = root + (i - 1) * 4;
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = index + edge;
                    vertices.AddTriangle(a, a + 1, a + 5);
                    vertices.AddTriangle(a, a + 5, a + 4);
                }
            }
        }
    }
}
