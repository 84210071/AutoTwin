using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoTwin
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TelemetryChart : MaskableGraphic, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private TelemetrySimulation source;
        [SerializeField] private TelemetryMetric metric;
        [SerializeField] private float lower;
        [SerializeField] private float upper = 100f;
        [SerializeField] private Color trace = new(.34f, .76f, 1f, 1f);
        [SerializeField] private TMP_Text tooltip;

        private TelemetrySimulation subscribed;
        private float hoverPosition = -1f;
        private static readonly Color GridColor = new(.28f, .41f, .62f, .23f);

        public void Configure(TelemetrySimulation source, TelemetryMetric metric, float lower, float upper, Color trace, TMP_Text tooltip = null)
        {
            this.source = source;
            this.metric = metric;
            this.lower = lower;
            this.upper = Mathf.Max(upper, lower + .01f);
            this.trace = trace;
            this.tooltip = tooltip;
            raycastTarget = true;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Subscribe();
            Refresh();
        }

        protected override void OnDisable()
        {
            Unsubscribe();
            hoverPosition = -1f;
            if (tooltip != null) tooltip.enabled = false;
            base.OnDisable();
        }

        private void Subscribe()
        {
            if (!Application.isPlaying || source == subscribed) return;
            Unsubscribe();
            if (source == null) return;
            subscribed = source;
            subscribed.Changed += Refresh;
        }

        private void Unsubscribe()
        {
            if (subscribed != null) subscribed.Changed -= Refresh;
            subscribed = null;
        }

        public void OnPointerEnter(PointerEventData eventData) => MoveCursor(eventData);
        public void OnPointerMove(PointerEventData eventData) => MoveCursor(eventData);

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverPosition = -1f;
            Refresh();
        }

        private void MoveCursor(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.enterEventCamera, out var point)) return;
            hoverPosition = Mathf.InverseLerp(rectTransform.rect.xMin, rectTransform.rect.xMax, point.x);
            Refresh();
        }

        private void Refresh()
        {
            SetVerticesDirty();
            if (tooltip == null) return;
            int count = source == null ? 0 : source.History.Count;
            tooltip.enabled = count > 0 && hoverPosition >= 0f;
            if (!tooltip.enabled) return;
            var sample = source.History[Mathf.RoundToInt(hoverPosition * (count - 1))];
            tooltip.text = System.FormattableString.Invariant($"{TelemetrySimulation.FormatTime(sample.Time)}  /  {sample.Read(metric):0.0}{Unit(metric)}");
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect plot = GetPixelAdjustedRect();
            if (plot.width <= 1f || plot.height <= 1f) return;
            float feather = .85f / Mathf.Max(.65f, canvas == null ? 1f : canvas.scaleFactor);
            DrawGrid(vertices, plot, feather);
            if (source == null || source.History.Count < 2) return;

            var history = source.History;
            int last = history.Count - 1;
            Vector2 previous = PlotPoint(plot, 0, last, history[0].Read(metric));
            var area = new Color(trace.r, trace.g, trace.b, trace.a * .19f);
            var fade = new Color(trace.r, trace.g, trace.b, 0f);
            var glow = new Color(trace.r, trace.g, trace.b, trace.a * .18f);
            for (int i = 1; i <= last; i++)
            {
                Vector2 point = PlotPoint(plot, i, last, history[i].Read(metric));
                // 曲线、辉光和渐变面积共享同一组采样点，使用一个 uGUI 网格绘制。
                Quad(vertices, previous, point, new Vector2(point.x, plot.yMin), new Vector2(previous.x, plot.yMin), area, area, fade, fade);
                Line(vertices, previous, point, 4.5f, glow, feather);
                Line(vertices, previous, point, 1.55f, trace, feather);
                previous = point;
            }

            if (hoverPosition < 0f) return;
            int index = Mathf.RoundToInt(hoverPosition * last);
            Vector2 cursor = PlotPoint(plot, index, last, history[index].Read(metric));
            var cursorColor = Color.Lerp(trace, Color.white, .5f);
            var cursorLine = new Color(cursorColor.r, cursorColor.g, cursorColor.b, .46f);
            Line(vertices, new Vector2(cursor.x, plot.yMin), new Vector2(cursor.x, plot.yMax), .65f, cursorLine, feather);
            DrawMarker(vertices, cursor, cursorColor);
        }

        private Vector2 PlotPoint(Rect plot, int index, int last, float value) => new(
            Mathf.Lerp(plot.xMin + 1f, plot.xMax - 1f, index / (float)last),
            Mathf.Lerp(plot.yMin + 2.5f, plot.yMax - 2.5f, Mathf.InverseLerp(lower, upper, value)));

        private static void DrawGrid(VertexHelper vertices, Rect plot, float feather)
        {
            for (int line = 0; line <= 4; line++)
            {
                float y = Mathf.Lerp(plot.yMin, plot.yMax, line / 4f);
                Line(vertices, new Vector2(plot.xMin, y), new Vector2(plot.xMax, y), .68f, GridColor, feather);
            }

            Color dim = new(GridColor.r, GridColor.g, GridColor.b, GridColor.a * .62f);
            for (int line = 0; line <= 6; line++)
            {
                float x = Mathf.Lerp(plot.xMin, plot.xMax, line / 6f);
                Line(vertices, new Vector2(x, plot.yMin), new Vector2(x, plot.yMax), .68f, dim, feather);
            }
        }

        private static void DrawMarker(VertexHelper vertices, Vector2 center, Color tint)
        {
            const int segments = 12;
            int first = vertices.currentVertCount;
            vertices.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 4f, tint, Vector2.zero);
                if (i > 0) vertices.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private static void Line(VertexHelper vertices, Vector2 a, Vector2 b, float width, Color tint, float feather)
        {
            var difference = b - a;
            var normal = new Vector2(-difference.y, difference.x).normalized;
            var across = normal * (width * .5f);
            Quad(vertices, a - across, a + across, b + across, b - across, tint, tint, tint, tint);
            // 两侧按 Canvas 实际缩放增加约一个像素的透明过渡，不依赖相机 MSAA。
            var outside = normal * (width * .5f + feather);
            var transparent = new Color(tint.r, tint.g, tint.b, 0f);
            Quad(vertices, a + across, a + outside, b + outside, b + across, tint, transparent, transparent, tint);
            Quad(vertices, a - outside, a - across, b - across, b - outside, transparent, tint, tint, transparent);
        }

        private static void Quad(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colA, Color colB, Color colC, Color colD)
        {
            int index = vertices.currentVertCount;
            vertices.AddVert(a, colA, Vector2.zero);
            vertices.AddVert(b, colB, Vector2.zero);
            vertices.AddVert(c, colC, Vector2.zero);
            vertices.AddVert(d, colD, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }

        private static string Unit(TelemetryMetric value) => value switch
        {
            TelemetryMetric.Soc => "%",
            TelemetryMetric.BatteryTemperature or TelemetryMetric.MotorTemperature => " °C",
            TelemetryMetric.Power => " kW",
            TelemetryMetric.Current => " A",
            TelemetryMetric.Voltage => " V",
            TelemetryMetric.MotorRpm => " rpm",
            TelemetryMetric.Speed => " km/h",
            _ => ""
        };
    }
}
