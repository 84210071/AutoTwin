using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoTwin
{
    [DefaultExecutionOrder(30)]
    public sealed class VehicleCallouts : MonoBehaviour
    {
        private const string MotorId = "EV-MOTOR-F";
        private static readonly Color Cyan = new Color32(141, 190, 239, 255);
        private static readonly Color Warm = new Color32(255, 146, 105, 255);
        [SerializeField] private VehicleRig vehicle;
        [SerializeField] private VehicleViewport viewport;
        [SerializeField] private TelemetrySimulation telemetry;
        [SerializeField] private TMP_FontAsset font;
        private Pin battery, motor, selection;

        private sealed class Pin
        {
            public RectTransform Root, Caption, FirstLine, SecondLine;
            public Image Point, Halo, FirstStroke, SecondStroke, Rule;
            public TMP_Text Label, Value;
        }

        private void Awake()
        {
            battery = CreatePin("Battery callout");
            motor = CreatePin("Drive motor callout");
            selection = CreatePin("Part selection callout");
        }

        private void Start()
        {
            telemetry.Changed += UpdateTelemetry;
            UpdateTelemetry();
        }

        private void OnDestroy()
        {
            if (telemetry != null) telemetry.Changed -= UpdateTelemetry;
        }

        private void UpdateTelemetry()
        {
            var frame = telemetry.Current;
            vehicle.SetBatteryAlert(telemetry.HeatFaultActive);
            battery.Label.text = telemetry.HeatFaultActive ? "动力电池 · 过热" : "动力电池";
            battery.Value.text = FormattableString.Invariant($"{frame.BatteryTemperature:0.0} °C  /  {frame.Soc:0}%");
            motor.Label.text = "前驱电机";
            motor.Value.text = FormattableString.Invariant($"{frame.MotorRpm:0} rpm");
        }

        private void LateUpdate()
        {
            // 各标注根据模型世界位置和当前相机重新投影，平移、缩放与模型动画时保持关联。
            string active = vehicle.Selection?.id;
            Place(battery, TelemetrySimulation.BatteryPartId, vehicle.PowertrainVisible || vehicle.BatteryAlert, 1, .24f,
                vehicle.BatteryAlert ? Warm : Cyan, active == TelemetrySimulation.BatteryPartId || vehicle.BatteryAlert);
            Place(motor, MotorId, vehicle.PowertrainVisible || vehicle.HoodOpen, -1, .185f, Cyan, active == MotorId);
            var profile = vehicle.Selection;
            if (profile != null && profile.id != TelemetrySimulation.BatteryPartId && profile.id != MotorId)
            {
                if (selection.Label.text != profile.displayName) selection.Label.text = profile.displayName;
                if (selection.Value.text != profile.id) selection.Value.text = profile.id;
                Place(selection, profile.id, true, 1, -.27f, Cyan, true);
            }
            else selection.Root.gameObject.SetActive(false);
        }

        private void Place(Pin pin, string id, bool visible, int side, float height, Color color, bool emphasized)
        {
            if (!visible || !vehicle.TryPartAnchor(id, out var anchor) || !viewport.TryProject(anchor, out var point))
            {
                pin.Root.gameObject.SetActive(false);
                return;
            }
            pin.Root.gameObject.SetActive(true);

            var area = ((RectTransform)transform).rect.size;
            Vector2 card = new(side * (area.x * .5f - 114f), height * area.y);
            Vector2 end = card - Vector2.right * (side * 69f) + Vector2.up * 4f;
            Vector2 elbow = new(end.x - side * 33f, end.y);
            pin.Caption.anchoredPosition = card;
            pin.Point.rectTransform.anchoredPosition = point;
            pin.Halo.rectTransform.anchoredPosition = point;
            pin.Point.color = color;
            pin.Halo.color = WithAlpha(color, emphasized ? .32f : .16f);
            pin.Rule.color = WithAlpha(color, .52f);
            pin.FirstStroke.color = pin.SecondStroke.color = WithAlpha(color, emphasized ? .69f : .32f);
            pin.Label.color = emphasized ? color : new Color32(192, 215, 237, 255);
            pin.Value.color = color;
            Line(pin.FirstLine, point, elbow);
            Line(pin.SecondLine, elbow, end);
        }

        private Pin CreatePin(string name)
        {
            var group = Rect(transform, name, Vector2.zero);
            var first = Fill(group, "Leader diagonal", new Vector2(1, 1));
            var second = Fill(group, "Leader horizontal", new Vector2(1, 1));
            var mark = Fill(group, "Part anchor", new Vector2(5, 5));
            var halo = Fill(group, "Anchor halo", new Vector2(11, 11));
            mark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            halo.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var card = Fill(group, "Caption", new Vector2(149, 47));
            card.color = new Color32(17, 23, 35, 148);
            var rule = Fill(card.transform, "Caption accent", new Vector2(1, 29));
            rule.rectTransform.anchoredPosition = new Vector2(-74, 0);
            var label = Text(card.transform, "Component", 15, 10);
            var value = Text(card.transform, "Live value", 13, -10);
            return new Pin { Root = group, Caption = card.rectTransform, Point = mark, Halo = halo,
                FirstLine = first.rectTransform, SecondLine = second.rectTransform, FirstStroke = first,
                SecondStroke = second, Rule = rule, Label = label, Value = value };
        }

        private TMP_Text Text(Transform parent, string name, int size, float y)
        {
            var rect = Rect(parent, name, new Vector2(126, 22));
            rect.anchoredPosition = new Vector2(2, y);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void Line(RectTransform rect, Vector2 a, Vector2 b)
        {
            Vector2 direction = b - a;
            rect.anchoredPosition = (a + b) * .5f;
            rect.sizeDelta = new Vector2(direction.magnitude, 1);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Fill(Transform parent, string name, Vector2 size)
        {
            var item = Rect(parent, name, size).gameObject.AddComponent<Image>();
            item.raycastTarget = false;
            return item;
        }

#if UNITY_EDITOR
        public void Configure(VehicleRig rig, VehicleViewport controls, TelemetrySimulation source, TMP_FontAsset typeface)
        {
            vehicle = rig;
            viewport = controls;
            telemetry = source;
            font = typeface;
        }
#endif
    }
}
