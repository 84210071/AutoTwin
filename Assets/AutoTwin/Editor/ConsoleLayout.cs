using System.Collections.Generic;
using TMPro;
using UnityEditor;
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
        public const int ViewWidth = 1226, ViewHeight = 742;
        private const int RailWidth = 321, LeftRail = 18, RightRail = 1581, StageX = 347, RailY = 51;
        private static readonly Color Paper = ColorOf("E1E6F0"), Fog = ColorOf("A9B3C6"), Dim = ColorOf("8490A7"),
            Blue = ColorOf("739FFF"), Cyan = ColorOf("7CC6EE"), Violet = ColorOf("9A9CF3"),
            Mint = ColorOf("70DCC2"), Amber = ColorOf("EDB475"), Rose = ColorOf("FF8375"),
            Border = ColorOf("7997C238"), Rule = ColorOf("768BBE2A"), RailTop = ColorOf("202938BE"),
            RailBottom = ColorOf("161D2ACD");
        private readonly TMP_FontAsset font;
        private readonly VehicleCatalog catalog;

        public ConsoleLayout(TMP_FontAsset font, VehicleCatalog catalog)
        {
            this.font = font;
            this.catalog = catalog;
        }

        public RawImage Build(VehicleRig rig, Camera camera, RenderTexture texture)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
            var canvas = new GameObject("AutoTwin Console", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var background = Glass(canvas.transform, "Dashboard background", 0, 0, 0, 0,
                ColorOf("151923"), ColorOf("101521"), Color.clear, 0, true);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            RectTransform root = Rect(canvas.transform, "Reference layout", 0, 0, 1920, 1080);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = Vector2.zero;

            var source = root.gameObject.AddComponent<TelemetrySimulation>();
            var dashboard = root.gameObject.AddComponent<DashboardView>();
            var console = root.gameObject.AddComponent<ConsoleView>();
            var values = new DashboardView.Readouts();
            var (start, pause, toolsToggle) = Header(root, source, dashboard, ref values);
            Sidebar(root, source, ref values);

            var stage = Rect(root, "Vehicle viewport", StageX, 44, ViewWidth, ViewHeight);
            var image = Rect(stage, "Live camera output", 0, 0, ViewWidth, ViewHeight).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            var viewport = stage.gameObject.AddComponent<VehicleViewport>();
            viewport.Configure(camera, image, events);
            var view = ViewportChrome(stage, ref values);
            var monitors = Rect(root, "System monitors", RightRail, RailY, RailWidth, 997);
            var (alerts, empty, alertCount, inject, recover) = Monitors(monitors, source, ref values);
            var (tools, mode, modeLabel, actions, actionLabels) = VehicleTools(root, console, dashboard, source);
            var (details, metrics, parts, inspector) = Inspector(root, view, dashboard);
            var logRows = Trends(root, source, ref values);
            Footer(root);

            console.Configure(rig, viewport, details, metrics, parts,
                new[] { actions[0], actions[1], mode, actions[2], actions[3], actions[4] },
                new[] { modeLabel, actionLabels[0], actionLabels[1], actionLabels[2] });
            dashboard.Configure(source, rig, values, alerts, logRows, empty, alertCount,
                start, pause, inject, recover, toolsToggle, monitors.gameObject, inspector.gameObject, tools.gameObject);

            image.gameObject.AddComponent<VehicleCallouts>().Configure(rig, viewport, source, font);
            Canvas.ForceUpdateCanvases();
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                // 场景生成期间 Canvas 可能先用 TMP 默认字体重建，保存前统一写入工作台字体。
                using var properties = new SerializedObject(label);
                properties.FindProperty("m_fontAsset").objectReferenceValue = font;
                properties.FindProperty("m_sharedMaterial").objectReferenceValue = font.material;
                properties.ApplyModifiedPropertiesWithoutUndo();
            }
            return image;
        }

        private (Button, Button, Button) Header(Transform root, TelemetrySimulation source, DashboardView dashboard,
            ref DashboardView.Readouts values)
        {
            // 极窄顶栏保留全局状态，中央车区没有横跨视野的工具带。
            Glass(root, "Header shading", 0, 0, 1920, 43, ColorOf("22283778"), ColorOf("11182300"), Color.clear, 0);
            Line(root, "Left header hairline", 19, 39, 646, 1, ColorOf("66819D31"));
            Line(root, "Right header hairline", 1255, 39, 646, 1, ColorOf("66819D31"));
            var crest = Glass(root, "Sculpted title crest", 664, -2, 592, 44,
                ColorOf("2A3549E5"), ColorOf("161E2BD9"), ColorOf("86B8F055"), 41, false, DashboardGlass.Shape.Crest);
            Line(crest, "Crest highlight left", 63, 36, 72, 1, ColorOf("9CDFFF84"));
            Line(crest, "Crest highlight right", 457, 36, 72, 1, ColorOf("9CDFFF84"));
            Label(crest, "AutoTwin", 125, 4, 342, 32, 26, Paper, false, TextAlignmentOptions.Center);

            values.statusLight = Line(root, "System status indicator", 21, 17, 5, 5, Mint);
            values.status = Label(root, "仿真已暂停", 36, 8, 105, 23, 15, Mint);
            values.clock = Label(root, "00:00", 147, 8, 81, 23, 17, Paper);
            Line(root, "Clock separator", 231, 12, 1, 16, Rule);
            var start = Control(root, "开始模拟", 241, 5, 89, 30, source.StartSimulation, Cyan, true).button;
            var pause = Control(root, "暂停模拟", 241, 5, 89, 30, source.PauseSimulation, Fog, true).button;
            pause.gameObject.SetActive(false);

            Label(root, "本地演示", 1720, 8, 74, 23, 14, Dim, false, TextAlignmentOptions.Right);
            var tools = Control(root, "工具", 1822, 5, 79, 30, dashboard.ToggleVehicleTools, Paper, true);
            tools.label.rectTransform.anchoredPosition = new Vector2(17, 0);
            tools.label.rectTransform.sizeDelta = new Vector2(61, 30);
            Line(tools.button.transform, "Drawer icon top", 8, 10, 13, 1, Cyan);
            Line(tools.button.transform, "Drawer icon middle", 8, 15, 10, 1, Cyan);
            Line(tools.button.transform, "Drawer icon bottom", 8, 20, 13, 1, Cyan);
            return (start, pause, tools.button);
        }

        private void Sidebar(Transform root, TelemetrySimulation source, ref DashboardView.Readouts values)
        {
            var charge = Panel(root, "Battery state of charge", LeftRail, RailY, RailWidth, 324, "动力电池 SOC", "");
            Ring(charge, "SOC outer scale", 84, 44, 153, 153, ColorOf("6989B430"), ColorOf("779EE350"), 1).SetValue(1);
            values.socRing = Ring(charge, "SOC live ring", 92, 52, 137, 137, ColorOf("2A334A"), Blue, 7);
            values.socRing.SetValue(.72f);
            values.soc = Label(charge, "72.0%", 93, 80, 135, 49, 35, Paper, false, TextAlignmentOptions.Center);
            Label(charge, "荷电状态", 93, 126, 135, 26, 14, Dim, false, TextAlignmentOptions.Center);
            Label(charge, "剩余续航", 19, 207, 123, 24, 16, Fog);
            values.estimate = Label(charge, "样车估算", 174, 207, 129, 24, 15, Cyan, false, TextAlignmentOptions.Right);
            values.range = Label(charge, "328 km", 19, 240, 283, 37, 30, Paper);
            Line(charge, "Estimated range track", 20, 289, 281, 4, ColorOf("7185C333"));
            values.rangeFill = Line(charge, "Estimated range value", 20, 289, 205, 4, Blue).rectTransform;

            var history = Panel(root, "Battery SOC history", LeftRail, 385, RailWidth, 278, "电池 SOC 历史", "");
            var hover = Label(history, "", 100, 236, 204, 24, 14, Paper, false, TextAlignmentOptions.Right);
            hover.gameObject.name = "SOC cursor readout";
            Chart(history, "Interactive SOC plot", 45, 52, 258, 153, source, TelemetryMetric.Soc, 65, 79, Violet, hover);
            foreach (var tick in new[] { ("79", 45), ("72", 121), ("65", 196) })
                Label(history, tick.Item1, 13, tick.Item2, 25, 17, 13, Dim, false, TextAlignmentOptions.Right);
            values.historyTimes = new[]
            {
                Label(history, "-01:00", 45, 209, 76, 23, 13, Dim),
                Label(history, "-00:30", 137, 209, 76, 23, 13, Dim, false, TextAlignmentOptions.Center),
                Label(history, "00:00", 225, 209, 78, 23, 13, Dim, false, TextAlignmentOptions.Right)
            };
            Label(history, "模拟时间", 18, 236, 91, 24, 13, Dim);

            var battery = Panel(root, "Battery data", LeftRail, 673, RailWidth, 375, "动力电池信息", "");
            Row(battery, "额定容量", $"{TelemetrySimulation.BatteryCapacityKwh:F0} kWh", 53, RailWidth);
            values.voltage = Row(battery, "端电压", "394.0 V", 103, RailWidth);
            values.current = Row(battery, "电池电流", "0.0 A", 153, RailWidth);
            values.batteryTemperature = Row(battery, "电池温度", "28.8 °C", 203, RailWidth);
            values.chargeMode = Row(battery, "充放电状态", "待机", 253, RailWidth, Cyan);
            Label(battery, "高压电池包", 19, 320, 156, 25, 14, Dim);
            Label(battery, "EV-BAT-HV", 175, 320, 129, 25, 13, Dim, false, TextAlignmentOptions.Right);
        }

        private TMP_Text ViewportChrome(Transform stage, ref DashboardView.Readouts values)
        {
            // 周边使用雾化羽化，车辆轮廓、拾取和地面网格保持同一个开放舞台。
            Glass(stage, "Viewport left blend", 0, 0, 27, ViewHeight, ColorOf("151923B0"), ColorOf("15192300"),
                Color.clear, 0, false, DashboardGlass.Shape.Horizontal);
            Glass(stage, "Viewport right blend", ViewWidth - 27, 0, 27, ViewHeight, ColorOf("15192300"), ColorOf("151923B0"),
                Color.clear, 0, false, DashboardGlass.Shape.Horizontal);
            Glass(stage, "Viewport lower blend", 0, ViewHeight - 43, ViewWidth, 43, ColorOf("11172400"),
                ColorOf("111724CC"), Color.clear, 0);
            var view = Label(stage, "EV-01   /   数字孪生", 22, 13, 305, 23, 14, Fog);
            Label(stage, "左键旋转  /  滚轮缩放  /  中键平移", 17, ViewHeight - 34, 408, 23, 13, Dim);
            Label(stage, "车速", ViewWidth - 180, ViewHeight - 34, 50, 23, 14, Dim, false, TextAlignmentOptions.Right);
            values.speed = Label(stage, "0 km/h", ViewWidth - 123, ViewHeight - 37, 104, 27, 20, Paper, false, TextAlignmentOptions.Right);
            return view;
        }

        private (RectTransform, Button, TMP_Text, Button[], TMP_Text[]) VehicleTools(Transform root, ConsoleView console,
            DashboardView dashboard, TelemetrySimulation source)
        {
            var tools = Panel(root, "Vehicle tools flyout", RightRail, RailY, RailWidth, 377, "车辆与仿真工具", "", true);
            // 抽屉覆盖监控卡片时单独排序，点击由最上层画布接收。
            var overlay = tools.gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 10;
            tools.gameObject.AddComponent<GraphicRaycaster>();
            var (mode, modeLabel) = Control(tools, "普通展示", 17, 56, 143, 33, console.TogglePowertrain, Cyan);
            Control(tools, "复位视角", 161, 56, 143, 33, console.ResetCamera);
            var (free, _) = Control(tools, "自由观察", 17, 95, 143, 33, console.FreeObservation);
            var (pick, _) = Control(tools, "部件选择", 161, 95, 143, 33, console.EnablePicking);
            Line(tools, "Motion controls divider", 16, 146, 289, 1, Rule);
            Label(tools, "车身操作", 17, 156, 285, 24, 14, Dim);
            var (doors, doorsLabel) = Control(tools, "打开前车门", 17, 183, 143, 33, console.ToggleFrontDoors);
            var (hood, hoodLabel) = Control(tools, "打开引擎盖", 161, 183, 143, 33, console.ToggleHood);
            var (wheels, wheelsLabel) = Control(tools, "旋转车轮", 17, 222, 143, 33, console.ToggleWheels);
            Control(tools, "车辆复位", 161, 222, 143, 33, console.ResetVehicle);
            Line(tools, "Inspection controls divider", 16, 274, 289, 1, Rule);
            Label(tools, "监控与诊断", 17, 284, 285, 24, 14, Dim);
            Control(tools, "部件详情", 17, 312, 143, 35, dashboard.ToggleInspector, Cyan);
            Control(tools, "重置模拟", 161, 312, 143, 35, source.ResetSimulation);
            tools.gameObject.SetActive(false);
            return (tools, mode, modeLabel, new[] { free, pick, wheels, doors, hood }, new[] { wheelsLabel, doorsLabel, hoodLabel });
        }

        private (DashboardView.AlertRow[], TMP_Text, TMP_Text, Button, Button) Monitors(Transform root,
            TelemetrySimulation source, ref DashboardView.Readouts values)
        {
            var alert = Panel(root, "Fault warning", 0, 0, RailWidth, 324, "故障预警", "");
            var active = Label(alert, "0 条活动告警", 164, 10, 139, 24, 14, Mint, false, TextAlignmentOptions.Right);
            var empty = Label(alert, "当前无告警记录", 19, 56, 282, 33, 17, Fog);
            Line(alert, "Demo actions divider", 16, 268, 289, 1, Rule);
            var inject = Control(alert, "模拟电池过热", 15, 275, 142, 36, source.InjectBatteryHeat, Cyan, true).button;
            var recover = Control(alert, "维修恢复", 162, 275, 142, 36, source.RecoverBatteryHeat, Mint, true).button;
            var alerts = new DashboardView.AlertRow[3];
            for (int i = 0; i < alerts.Length; i++)
            {
                var row = Rect(alert, "Alert row " + (i + 1), 11, 46 + i * 73, 300, 69);
                var hit = Line(row, "Alert click surface", 0, 0, 300, 69, Color.white, true);
                var button = row.gameObject.AddComponent<Button>();
                SetButtonTheme(button, hit, false);
                var indicator = Line(row, "Alert severity indicator", 6, 10, 2, 45, Amber);
                alerts[i] = new DashboardView.AlertRow
                {
                    button = button, indicator = indicator,
                    title = Label(row, "00:00", 16, 5, 100, 22, 14, Dim),
                    severity = Label(row, "警告", 208, 5, 83, 22, 14, Amber, false, TextAlignmentOptions.Right),
                    message = Label(row, "", 16, 25, 275, 26, 16, Paper),
                    part = Label(row, "", 16, 50, 275, 16, 13, Fog)
                };
                Line(row, "Alert row divider", 6, 65, 286, 1, Rule);
                row.gameObject.SetActive(false);
            }

            var health = Panel(root, "Battery state of health", 0, 334, RailWidth, 278, "电池健康状态", "SOH");
            Ring(health, "SOH outer scale", 16, 55, 153, 153, ColorOf("547DAD40"), ColorOf("7C96BF5E"), 1, 259, 219).SetValue(1);
            values.sohRing = Ring(health, "SOH live gauge", 22, 61, 141, 141, ColorOf("28394D"), Mint, 7, 258, 218);
            values.sohRing.SetValue(.95f);
            values.soh = Label(health, "95.0%", 22, 98, 141, 40, 29, Paper, false, TextAlignmentOptions.Center);
            values.health = Label(health, "健康", 22, 139, 141, 24, 15, Mint, false, TextAlignmentOptions.Center);
            Label(health, "容量保持率", 177, 75, 125, 29, 17, Paper);
            Label(health, $"{TelemetrySimulation.BatterySohThreshold:F0}% 及以上正常", 178, 106, 125, 25, 14, Mint);
            Label(health, $"< {TelemetrySimulation.BatterySohThreshold:F0}%  需检修", 178, 135, 125, 25, 14, Fog);
            Label(health, $"温度阈值 {TelemetrySimulation.BatteryOverheatThreshold:F0} °C", 178, 165, 125, 25, 13, Dim);
            Line(health, "Health footer rule", 16, 211, 289, 1, Rule);
            Label(health, "热状态", 16, 228, 109, 28, 17, Fog);
            values.thermal = Label(health, "温度正常", 130, 228, 173, 28, 17, Mint, false, TextAlignmentOptions.Right);

            var drive = Panel(root, "Electric drive status", 0, 622, RailWidth, 375, "电驱动系统状态", "");
            values.driveRing = Ring(drive, "Drive power gauge", 13, 49, 152, 152, ColorOf("27334D"), Violet, 6, 260, 220);
            values.power = Label(drive, "0.0", 13, 95, 152, 49, 31, Paper, false, TextAlignmentOptions.Center);
            Label(drive, "kW", 13, 139, 152, 24, 15, Violet, false, TextAlignmentOptions.Center);
            Label(drive, "当前功率", 177, 85, 127, 32, 17, Paper);
            Label(drive, "电机温度阈值", 177, 124, 127, 24, 14, Dim);
            Label(drive, $"{TelemetrySimulation.MotorOverheatThreshold:F0} °C", 177, 150, 127, 25, 15, Fog);
            Line(drive, "Drive data separator", 16, 213, 289, 1, Rule);
            values.motorTemperature = Row(drive, "电机温度", "43.7 °C", 223, RailWidth);
            values.rpm = Row(drive, "电机转速", "0 rpm", 268, RailWidth);
            values.driveCondition = Row(drive, "故障等级", "无电驱告警", 313, RailWidth, Mint);
            return (alerts, empty, active, inject, recover);
        }

        private (TMP_Text[], ConsoleView.MetricRow[], ConsoleView.PartButton[], RectTransform) Inspector(
            Transform root, TMP_Text view, DashboardView dashboard)
        {
            var panel = Panel(root, "Part inspector", RightRail, RailY, RailWidth, 997, "部件参数", "");
            Control(panel, "返回监控", 191, 5, 115, 34, dashboard.CloseInspector, Cyan, true);
            var kind = Label(panel, "整车概览", 16, 57, 307, 25, 16, Cyan);
            var name = Label(panel, catalog.vehicleName, 16, 83, 309, 43, 29, Paper, true);
            var id = Label(panel, catalog.vehicleCode, 16, 130, 308, 26, 16, Fog);
            var description = Label(panel, "点击三维视图或部件清单查看参数。", 16, 169, 310, 103, 17, Fog,
                false, TextAlignmentOptions.TopLeft, true);
            Line(panel, "Inspector parameters divider", 16, 279, 312, 1, Rule);
            Label(panel, "部件参数", 16, 290, 307, 30, 19, Paper, true);
            var rows = new ConsoleView.MetricRow[4];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = Rect(panel, "Part parameter " + (i + 1), 15, 336 + i * 40, 314, 38);
                Line(row, "Metric rule", 0, 36, 314, 1, Rule);
                rows[i] = new ConsoleView.MetricRow
                {
                    label = Label(row, "", 0, 1, 131, 32, 16, Fog),
                    value = Label(row, "", 133, 1, 180, 32, 16, Paper, false, TextAlignmentOptions.Right)
                };
            }
            Line(panel, "Part list divider", 16, 506, 312, 1, Rule);
            Label(panel, "部件清单", 16, 519, 307, 30, 19, Paper, true);
            var parts = new List<ConsoleView.PartButton>();
            PartGroup(panel, parts, "结构", 564, DemoCatalog.Body, DemoCatalog.Hood, DemoCatalog.Trunk);
            PartGroup(panel, parts, "动力", 629, DemoCatalog.Battery, DemoCatalog.Motor);
            PartGroup(panel, parts, "车门", 694, DemoCatalog.DoorIds);
            PartGroup(panel, parts, "车轮", 797, DemoCatalog.WheelIds);
            panel.gameObject.SetActive(false);
            return (new[] { name, kind, id, description, view }, rows, parts.ToArray(), panel);
        }

        private void PartGroup(Transform panel, List<ConsoleView.PartButton> result, string title, int y, params string[] ids)
        {
            Label(panel, title, 16, y, 78, 26, 14, Dim);
            int columns = ids.Length == 3 ? 3 : 2;
            float width = columns == 3 ? 91 : 140;
            for (int i = 0; i < ids.Length; i++)
            {
                var profile = System.Array.Find(catalog.parts, item => item.id == ids[i]);
                var (button, _) = Control(panel, profile.displayName, 16 + i % columns * (width + 8),
                    y + 29 + i / columns * 34, width, 30);
                result.Add(new ConsoleView.PartButton { id = profile.id, button = button });
            }
        }

        private DashboardView.LogRow[] Trends(Transform root, TelemetrySimulation source, ref DashboardView.Readouts values)
        {
            var panel = Panel(root, "Telemetry trends and log", StageX, 790, ViewWidth, 258, "车辆历史与运行监控", "");
            MiniTrend(panel, "SOC 趋势", "%", 17, source, TelemetryMetric.Soc, 65, 79, Blue);
            MiniTrend(panel, "电池温度", "°C", 213, source, TelemetryMetric.BatteryTemperature, 24, 68, Violet);
            MiniTrend(panel, "电机温度", "°C", 409, source, TelemetryMetric.MotorTemperature, 30, 82, Cyan);
            foreach (int x in new[] { 203, 399, 595, 806 }) Line(panel, "Trend column divider", x, 57, 1, 173, Rule);

            var energy = Rect(panel, "Battery energy", 610, 57, 180, 173);
            Label(energy, "充放电累计", 0, 0, 176, 24, 16, Paper);
            Label(energy, "回充", 0, 39, 42, 28, 14, Mint);
            values.charged = Label(energy, "0.000 kWh", 34, 38, 142, 31, 20, Paper, false, TextAlignmentOptions.Right);
            Line(energy, "Charge baseline", 0, 76, 175, 2, ColorOf("76D6C04C"));
            Label(energy, "放电", 0, 106, 42, 28, 14, Blue);
            values.discharged = Label(energy, "0.000 kWh", 34, 105, 142, 31, 20, Paper, false, TextAlignmentOptions.Right);
            Line(energy, "Discharge baseline", 0, 144, 175, 2, ColorOf("789BE44B"));

            var log = Rect(panel, "System events", 824, 57, 385, 176);
            Label(log, "系统运行日志", 0, 0, 370, 24, 16, Paper);
            Line(log, "Log header divider", 0, 34, 385, 1, Rule);
            var rows = new DashboardView.LogRow[4];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new DashboardView.LogRow
                {
                    time = Label(log, "", 0, 44 + i * 27, 67, 24, 13, Blue),
                    message = Label(log, "", 77, 44 + i * 27, 307, 24, 14, Fog)
                };
            }
            return rows;
        }

        private void MiniTrend(Transform panel, string title, string unit, int x, TelemetrySimulation source,
            TelemetryMetric metric, float min, float max, Color color)
        {
            var section = Rect(panel, title, x, 57, 170, 175);
            Label(section, title, 0, 0, 126, 24, 16, Paper);
            Label(section, unit, 128, 0, 41, 23, 13, color, false, TextAlignmentOptions.Right);
            Chart(section, title + " plot", 0, 35, 170, 114, source, metric, min, max, color);
            Label(section, $"{min:F0}", 0, 150, 58, 20, 13, Dim);
            Label(section, $"{max:F0}", 112, 150, 58, 20, 13, Dim, false, TextAlignmentOptions.Right);
        }

        private void Footer(Transform root)
        {
            Label(root, "遥测与故障记录来自本地演示模型", 632, 1052, 657, 22, 12, Dim, false, TextAlignmentOptions.Center);
        }

        private TMP_Text Row(Transform parent, string name, string value, int y, int width, Color? accent = null)
        {
            Label(parent, name, 16, y, 151, 30, 16, Fog);
            Line(parent, name + " rule", 16, y + 34, width - 32, 1, Rule);
            return Label(parent, value, 161, y, width - 178, 30, 18, accent ?? Paper, false, TextAlignmentOptions.Right);
        }

        private static TelemetryChart Chart(Transform parent, string name, float x, float y, float w, float h, TelemetrySimulation source,
            TelemetryMetric metric, float min, float max, Color accent, TMP_Text tooltip = null)
        {
            var chart = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TelemetryChart>();
            chart.Configure(source, metric, min, max, accent, tooltip);
            chart.raycastTarget = tooltip != null;
            return chart;
        }

        private static TelemetryRing Ring(Transform parent, string name, float x, float y, float w, float h,
            Color track, Color accent, float thickness, float sweep = 360f, float start = 90f)
        {
            var ring = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TelemetryRing>();
            ring.raycastTarget = false;
            ring.Configure(track, accent, thickness, sweep, start);
            return ring;
        }

        private (Button button, TMP_Text label) Control(Transform parent, string title, float x, float y, float w, float h,
            UnityAction action = null, Color? accent = null, bool bare = false)
        {
            var frame = Rect(parent, title, x, y, w, h);
            var target = Line(frame, "Click surface", 0, 1, w, h - 2, Color.white, true);
            var button = frame.gameObject.AddComponent<Button>();
            SetButtonTheme(button, target, accent.HasValue && !bare);
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            var label = Label(frame, title, 4, 0, w - 8, h, 15, accent ?? Paper, false, TextAlignmentOptions.Center);
            return (button, label);
        }

        private static void SetButtonTheme(Button button, Graphic target, bool accent)
        {
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.colors = new ColorBlock
            {
                normalColor = accent ? ColorOf("4C87B121") : Color.clear,
                highlightedColor = ColorOf("739AC442"), selectedColor = ColorOf("739AC44B"),
                pressedColor = ColorOf("75B7DE69"), disabledColor = Color.clear,
                colorMultiplier = 1f, fadeDuration = .12f
            };
        }

        private RectTransform Panel(Transform parent, string name, float x, float y, int w, int h, string title, string english,
            bool overlay = false)
        {
            var panel = Glass(parent, name, x, y, w, h, overlay ? ColorOf("283345FC") : RailTop,
                overlay ? ColorOf("192337FC") : RailBottom, Border, 14, true);
            Line(panel, "Panel leading highlight", 1, 7, 1, 57, ColorOf("A1BEED47"));
            Label(panel, title, 17, 9, w - 33, 26, 18, Paper);
            if (!string.IsNullOrEmpty(english))
                Label(panel, english, w - 171, 10, 155, 24, 13, Dim, false, TextAlignmentOptions.Right);
            Line(panel, "Panel header line", 16, 40, w - 32, 1, Rule);
            return panel;
        }

        private static RectTransform Glass(Transform parent, string name, float x, float y, float w, float h, Color top,
            Color bottom, Color edge, float corner, bool raycast = false, DashboardGlass.Shape shape = DashboardGlass.Shape.Panel)
        {
            var glass = Rect(parent, name, x, y, w, h).gameObject.AddComponent<DashboardGlass>();
            glass.Configure(top, bottom, edge, corner, shape);
            glass.raycastTarget = raycast;
            return glass.rectTransform;
        }

        private TMP_Text Label(Transform parent, string content, float x, float y, float w, float h, int size, Color color,
            bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Left, bool wrap = false)
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
            // 中英混排字体的字面高度大于个别紧凑文字槽，按实际字形绘制以免整行被截断。
            label.overflowMode = TextOverflowModes.Overflow;
            label.text = content;
            return label;
        }

        private static Image Line(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
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
