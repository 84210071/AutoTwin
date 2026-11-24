using UnityEngine;
using UnityEngine.UI;

namespace AutoTwin
{
    /// <summary>生成带倒角与亮边的原生 uGUI 面板，可在不同尺寸下保持线宽。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DashboardGlass : MaskableGraphic
    {
        public enum Shape { Panel, Crest, Horizontal }

        [SerializeField] private Color upper, lower, edge;
        [SerializeField] private float corner;
        [SerializeField] private Shape shape;

        public void Configure(Color upperColor, Color lowerColor, Color edgeColor, float cut, Shape profile = Shape.Panel)
        {
            upper = upperColor;
            lower = lowerColor;
            edge = edgeColor;
            corner = cut;
            shape = profile;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            var perimeter = Perimeter(bounds);
            if (shape == Shape.Panel && corner > 0f && edge.a > 0f)
                DrawShadow(mesh, bounds, perimeter);

            int origin = mesh.currentVertCount;
            mesh.AddVert(bounds.center, Gradient(bounds, bounds.center), Vector2.zero);
            foreach (Vector2 point in perimeter)
                mesh.AddVert(point, Gradient(bounds, point), Vector2.zero);

            for (int i = 0; i < perimeter.Length; i++)
            {
                int next = (i + 1) % perimeter.Length;
                mesh.AddTriangle(origin, origin + next + 1, origin + i + 1);
                Vector2 from = perimeter[i];
                Vector2 to = perimeter[next];
                if (edge.a <= 0f || (from - to).sqrMagnitude < .01f) continue;
                Vector2 direction = (to - from).normalized;
                Vector2 inset = new Vector2(-direction.y, direction.x) * 1.05f;
                float height = Mathf.InverseLerp(bounds.yMin, bounds.yMax, (from.y + to.y) * .5f);
                Color tint = new(edge.r, edge.g, edge.b, edge.a * Mathf.Lerp(.28f, .82f, height));
                int index = mesh.currentVertCount;
                mesh.AddVert(from, tint, Vector2.zero);
                mesh.AddVert(to, tint, Vector2.zero);
                mesh.AddVert(to + inset, tint, Vector2.zero);
                mesh.AddVert(from + inset, tint, Vector2.zero);
                mesh.AddTriangle(index, index + 2, index + 1);
                mesh.AddTriangle(index, index + 3, index + 2);
            }
        }

        private Color Gradient(Rect bounds, Vector2 point) => shape == Shape.Horizontal
            ? Color.Lerp(upper, lower, Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x))
            : Color.Lerp(lower, upper, Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y));

        private Vector2[] Perimeter(Rect bounds)
        {
            float x = bounds.xMin, y = bounds.yMin, r = bounds.xMax, t = bounds.yMax;
            if (shape == Shape.Crest)
            {
                float notch = Mathf.Min(corner, bounds.width * .2f);
                return new[]
                {
                    new Vector2(x, t), new Vector2(x + notch * .32f, y + 12), new Vector2(x + notch, y),
                    new Vector2(r - notch, y), new Vector2(r - notch * .32f, y + 12), new Vector2(r, t)
                };
            }

            float cut = Mathf.Clamp(corner, 0f, Mathf.Min(bounds.width, bounds.height) * .45f);
            float small = cut * .29f;
            return new[]
            {
                new Vector2(x, y + small), new Vector2(x + small, y), new Vector2(r - small, y),
                new Vector2(r, y + small), new Vector2(r, t - cut), new Vector2(r - cut, t),
                new Vector2(x + small, t), new Vector2(x, t - small)
            };
        }

        private static void DrawShadow(VertexHelper mesh, Rect bounds, Vector2[] inner)
        {
            // 很短的透明度过渡给玻璃底色一点离开背景的层次。
            Color shadow = new(.03f, .047f, .082f, .22f);
            for (int i = 0; i < inner.Length; i++)
            {
                Vector2 a = inner[i];
                Vector2 b = inner[(i + 1) % inner.Length];
                Vector2 outerA = a + (a - bounds.center).normalized * 7f - Vector2.up * 2f;
                Vector2 outerB = b + (b - bounds.center).normalized * 7f - Vector2.up * 2f;
                int index = mesh.currentVertCount;
                mesh.AddVert(a, shadow, Vector2.zero);
                mesh.AddVert(b, shadow, Vector2.zero);
                mesh.AddVert(outerB, Color.clear, Vector2.zero);
                mesh.AddVert(outerA, Color.clear, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
