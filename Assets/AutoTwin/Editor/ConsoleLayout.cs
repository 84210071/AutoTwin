using System.Collections.Generic;
using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static AutoTwin.Editor.SceneAssets;

namespace AutoTwin.Editor
{
    internal sealed class ConsoleLayout
    {
        public const int ViewWidth = 1166, ViewHeight = 806;
        private static readonly Color Paper = ColorOf("E8F2FC"), Muted = ColorOf("9AB0C7"), Dim = ColorOf("6F88A2"),
            Cyan = ColorOf("70DEFF"), Line = ColorOf("263E55"), Card = ColorOf("101B2A"), Inset = ColorOf("0C1522");
        private readonly TMP_FontAsset font;
        private readonly VehicleCatalog catalog;

        public ConsoleLayout(TMP_FontAsset font, VehicleCatalog catalog)
        {
            this.font = font;
            this.catalog = catalog;
        }

        public void Build(VehicleRig rig, Camera camera, RenderTexture texture)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
            var canvas = new GameObject("AutoTwin Console", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var background = Box(canvas.transform, "Screen background", 0, 0, 0, 0, ColorOf("090F19"), true).rectTransform;
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            RectTransform root = Rect(canvas.transform, "Reference layout", 0, 0, 1920, 1080);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = Vector2.zero;

            Header(root);
            var list = Sidebar(root);
            var stage = Panel(root, "Vehicle viewport", 354, 104, 1168, 808);
            var raw = Rect(stage, "Live camera output", 1, 1, ViewWidth, ViewHeight).gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            var viewport = stage.gameObject.AddComponent<VehicleViewport>();
            viewport.Configure(camera, raw, eventSystem);
            var console = root.gameObject.AddComponent<ConsoleView>();
            var (viewLabel, power, powerLabel, wheels, wheelsLabel) = Stage(stage, console);
            var (detail, rows) = Inspector(root, viewLabel);
            var (free, pick, doors, doorLabel, hood, hoodLabel) = Toolbar(root, console);
            console.Configure(rig, viewport, detail, rows, list, new[] { free, pick, power, wheels, doors, hood },
                new[] { powerLabel, wheelsLabel, doorLabel, hoodLabel });
        }

        private void Header(Transform root)
        {
            Box(root, "Header background", 0, 0, 1920, 82, ColorOf("0D1725"), true);
            Box(root, "Header rule", 0, 81, 1920, 1, Line);
            Box(root, "Brand accent", 28, 22, 4, 38, Cyan);
            Text(root, "AutoTwin", 45, 15, 194, 52, 36, Paper, true);
            Box(root, "Header separator", 239, 25, 1, 32, Line);
            Text(root, "车辆数字孪生工作台", 260, 18, 330, 44, 24, Muted);
            Text(root, "EV PLATFORM  /  PC WORKSTATION", 712, 24, 488, 34, 16, Dim, align: TextAlignmentOptions.Center);
            Box(root, "Ready indicator", 1530, 37, 8, 8, ColorOf("61E3BA"));
            Text(root, "展示系统就绪", 1550, 22, 142, 36, 19, Paper);
            Box(root, "Build tag background", 1721, 22, 170, 38, ColorOf("153043"));
            Text(root, "PHASE 01  /  DEMO", 1721, 22, 170, 38, 15, Cyan, align: TextAlignmentOptions.Center);
        }

        private ConsoleView.PartButton[] Sidebar(Transform root)
        {
            var status = Panel(root, "Vehicle status", 28, 104, 310, 289);
            Section(status, "车辆状态", "VEHICLE STATUS", 310);
            Text(status, catalog.vehicleName, 17, 67, 270, 41, 27, Paper, true);
            Text(status, catalog.powertrain + "  /  模拟数据", 18, 107, 269, 27, 15, Muted);
            Text(status, "动力电池 SOC", 18, 148, 152, 30, 17, Muted);
            Text(status, $"{catalog.stateOfCharge}%", 171, 139, 119, 47, 32, Cyan, true, TextAlignmentOptions.Right);
            Box(status, "SOC track", 18, 187, 274, 7, Line);
            Box(status, "SOC fill", 18, 187, 274 * catalog.stateOfCharge / 100f, 7, Cyan);
            Box(status, "Status divider", 18, 211, 274, 1, Line);
            Text(status, "预计续航", 18, 222, 123, 25, 15, Muted);
            Text(status, $"{catalog.rangeKm} km", 18, 247, 124, 30, 24, Paper);
            Text(status, "电池端电压", 167, 222, 125, 25, 15, Muted);
            Text(status, $"{catalog.packVoltage} V", 167, 247, 125, 30, 24, Paper);

            var navigation = Panel(root, "Component navigation", 28, 408, 310, 504);
            Section(navigation, "部件清单", "COMPONENTS", 310);
            var list = new List<ConsoleView.PartButton>();
            PartGroup(navigation, list, "车身与覆盖件", 62, DemoCatalog.Body, DemoCatalog.Hood, DemoCatalog.Trunk);
            PartGroup(navigation, list, "动力总成", 181, DemoCatalog.Battery, DemoCatalog.Motor);
            PartGroup(navigation, list, "车门", 255, DemoCatalog.DoorIds);
            PartGroup(navigation, list, "车轮", 374, DemoCatalog.WheelIds);
            return list.ToArray();
        }

        private void PartGroup(Transform panel, List<ConsoleView.PartButton> list, string title, int y, params string[] ids)
        {
            Text(panel, title, 18, y, 274, 23, 15, Muted);
            for (int i = 0; i < ids.Length; i++)
            {
                var part = System.Array.Find(catalog.parts, profile => profile.id == ids[i]);
                var (button, _) = Control(panel, part.displayName, 17 + i % 2 * 142, y + 27 + i / 2 * 42, 133, 35, 17);
                list.Add(new ConsoleView.PartButton { id = part.id, button = button });
            }
        }

        private (TMP_Text, Button, TMP_Text, Button, TMP_Text) Stage(Transform stage, ConsoleView console)
        {
            var plaque = Box(stage, "Viewport title plate", 21, 18, 390, 95, ColorOf("0B1624ED"), true).transform;
            Box(plaque, "Viewport accent", 0, 0, 3, 95, Cyan);
            Text(plaque, "三维车辆视图", 16, 13, 355, 40, 29, Paper, true);
            var view = Text(plaque, "整车外观 / EXTERIOR", 17, 55, 355, 24, 16, Cyan);
            var (power, powerLabel) = Control(stage, "透视动力总成", 849, 29, 164, 40, 18, console.TogglePowertrain);
            var (wheels, wheelsLabel) = Control(stage, "旋转车轮", 1023, 29, 123, 40, 18, console.ToggleWheels);

            var help = Box(stage, "Pointer controls", 21, 746, 625, 43, ColorOf("0A1420ED"), true).transform;
            Text(help, "左键拖动  旋转     /     滚轮  缩放     /     中键拖动  平移", 14, 0, 594, 43, 16, Muted);
            Text(stage, "透视相机  /  车辆坐标", 848, 751, 298, 32, 16, Muted, align: TextAlignmentOptions.Right);
            foreach (int x in new[] { 10, 1143 })
            {
                Box(stage, "Top reticle", x, 9, 15, 2, Cyan);
                Box(stage, "Bottom reticle", x, 797, 15, 2, Cyan);
            }
            return (view, power, powerLabel, wheels, wheelsLabel);
        }

        private (TMP_Text[], ConsoleView.MetricRow[]) Inspector(Transform root, TMP_Text view)
        {
            var panel = Panel(root, "Part inspector", 1538, 104, 354, 808);
            Section(panel, "部件参数", "INSPECTOR", 354);
            var identity = Box(panel, "Identity card", 16, 73, 322, 188, Inset).transform;
            Box(identity, "Identity accent", 0, 0, 3, 188, ColorOf("317F9C"));
            var kind = Text(identity, "整车概览", 15, 14, 290, 28, 16, Cyan);
            var title = Text(identity, catalog.vehicleName, 15, 45, 290, 47, 31, Paper, true);
            var id = Text(identity, catalog.vehicleCode, 16, 97, 290, 29, 18, Muted);
            Box(identity, "Identifier divider", 16, 139, 289, 1, Line);
            Text(identity, "对象标识 / ASSET ID", 16, 146, 288, 29, 15, Dim);

            Text(panel, "部件说明", 18, 283, 316, 32, 22, Paper, true);
            var description = Text(panel, "点击三维视图或部件清单，查看部件说明与模拟参数。", 18, 324, 314, 91, 19, Muted, align: TextAlignmentOptions.TopLeft, wrap: true);
            Box(panel, "Description rule", 17, 424, 320, 1, Line);
            Text(panel, "模拟参数", 18, 444, 188, 33, 22, Paper, true);
            Text(panel, "DEMO DATA", 197, 447, 138, 27, 14, Cyan, align: TextAlignmentOptions.Right);
            var rows = new ConsoleView.MetricRow[4];
            for (int i = 0; i < rows.Length; i++)
            {
                var bar = Box(panel, "Metric row " + (i + 1), 17, 492 + 56 * i, 320, 47, Inset).transform;
                rows[i] = new ConsoleView.MetricRow
                {
                    label = Text(bar, catalog.overview[i].label, 12, 0, 126, 47, 17, Muted),
                    value = Text(bar, catalog.overview[i].value, 140, 0, 168, 47, 19, Paper, align: TextAlignmentOptions.Right)
                };
            }
            Box(panel, "Data footer rule", 17, 747, 320, 1, Line);
            Text(panel, "数据源  /  本地样车配置", 18, 760, 318, 30, 15, Dim);
            return (new[] { title, kind, id, description, view }, rows);
        }

        private (Button, Button, Button, TMP_Text, Button, TMP_Text) Toolbar(Transform root, ConsoleView console)
        {
            var toolbar = Panel(root, "Vehicle toolbar", 28, 931, 1864, 120);
            Text(toolbar, "车辆工具", 17, 20, 115, 37, 23, Paper, true);
            Text(toolbar, "CONTROLS", 18, 61, 113, 25, 14, Dim);
            Box(toolbar, "Toolbar divider", 137, 26, 1, 64, Line);
            Control(toolbar, "车辆复位", 154, 26, 198, 68, 20, console.ResetVehicle);
            var (free, _) = Control(toolbar, "自由观察", 367, 26, 198, 68, 20, console.FreeObservation);
            var (pick, _) = Control(toolbar, "部件选择", 580, 26, 198, 68, 20, console.EnablePicking);
            var (door, doorText) = Control(toolbar, "打开前车门", 793, 26, 198, 68, 20, console.ToggleFrontDoors);
            var (hood, hoodText) = Control(toolbar, "打开引擎盖", 1006, 26, 198, 68, 20, console.ToggleHood);
            Disabled(toolbar, "结构拆解", 1219);
            Disabled(toolbar, "运行仿真", 1432);
            Disabled(toolbar, "故障诊断", 1645);
            return (free, pick, door, doorText, hood, hoodText);
        }

        private void Disabled(Transform parent, string title, int x)
        {
            var (button, label) = Control(parent, title, x, 26, 198, 68, 19);
            button.interactable = false;
            label.rectTransform.sizeDelta = new Vector2(198, 33);
            label.rectTransform.anchoredPosition = new Vector2(0, -7);
            label.color = Dim;
            Text(button.transform, "未启用", 0, 38, 198, 19, 13, Dim, align: TextAlignmentOptions.Center);
        }

        private void Section(Transform panel, string title, string english, int width)
        {
            Box(panel, "Section accent", 16, 19, 3, 20, Cyan);
            Text(panel, title, 28, 10, 156, 39, 23, Paper, true);
            Text(panel, english, 169, 15, width - 186, 30, 13, Dim, align: TextAlignmentOptions.Right);
            Box(panel, "Section rule", 16, 54, width - 32, 1, Line);
        }

        private (Button, TMP_Text) Control(Transform parent, string title, float x, float y, float w, float h, int size, UnityAction action = null)
        {
            var frame = Box(parent, title, x, y, w, h, Line);
            var fill = Box(frame.transform, "Button face", 1, 1, w - 2, h - 2, Color.white, true);
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.colors = new ColorBlock
            {
                normalColor = ColorOf("131F2F"), highlightedColor = ColorOf("20506D"),
                selectedColor = ColorOf("18374E"), pressedColor = ColorOf("276381"),
                disabledColor = ColorOf("101923"), colorMultiplier = 1f, fadeDuration = .12f
            };
            // 序列化 UnityEvent，场景在脱离生成工具后也能直接运行。
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            var label = Text(frame.transform, title, 0, 0, w, h, size, Paper, align: TextAlignmentOptions.Center);
            return (button, label);
        }

        private static RectTransform Panel(Transform parent, string name, int x, int y, int w, int h)
        {
            var outline = Box(parent, name, x, y, w, h, Line, true);
            Box(outline.transform, "Panel fill", 1, 1, w - 2, h - 2, Card);
            return outline.rectTransform;
        }

        private TMP_Text Text(Transform parent, string content, float x, float y, float w, float h, int size, Color color, bool bold = false,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false)
        {
            var label = Rect(parent, content, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.enableWordWrapping = wrap;
            label.text = content;
            return label;
        }

        private static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
    }
}
