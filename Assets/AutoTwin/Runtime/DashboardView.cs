using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoTwin
{
    /// <summary>将仿真快照绑定到仪表、遥测读数、日志和车辆部件入口。</summary>
    public sealed class DashboardView : MonoBehaviour
    {
        [Serializable]
        public struct Readouts
        {
            public TMP_Text status, clock, speed, soc, range, estimate, voltage, current, batteryTemperature,
                chargeMode, soh, health, thermal, motorTemperature, rpm, power, driveCondition, charged, discharged;
            public Graphic statusLight;
            public TelemetryRing socRing, sohRing, driveRing;
            public RectTransform rangeFill;
            public TMP_Text[] historyTimes;
        }

        [Serializable]
        public struct AlertRow
        {
            public Button button;
            public Graphic indicator;
            public TMP_Text title, severity, message, part;
        }

        [Serializable] public struct LogRow { public TMP_Text time, message; }

        [SerializeField] private TelemetrySimulation source;
        [SerializeField] private VehicleRig vehicle;
        [SerializeField] private Readouts readouts;
        [SerializeField] private AlertRow[] alertRows;
        [SerializeField] private LogRow[] logs;
        [SerializeField] private TMP_Text noAlerts, alertCount;
        [SerializeField] private Button start, pause, inject, recover, toolsToggle;
        [SerializeField] private GameObject monitors, inspector, vehicleTools;
        private TMP_Text[] commandLabels;

        private static readonly Color Safe = new Color32(105, 222, 190, 255);
        private static readonly Color Caution = new Color32(237, 180, 117, 255);
        private static readonly Color Alarm = new Color32(255, 131, 117, 255);
        private static readonly Color Idle = new Color32(136, 164, 206, 255);

        private void Start()
        {
            commandLabels = new[] { start.GetComponentInChildren<TMP_Text>(true), pause.GetComponentInChildren<TMP_Text>(true),
                inject.GetComponentInChildren<TMP_Text>(), recover.GetComponentInChildren<TMP_Text>() };
            for (int i = 0; i < alertRows.Length; i++)
            {
                int index = i;
                alertRows[i].button.onClick.AddListener(() => SelectRelatedPart(index));
            }

            source.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (source != null) source.Changed -= Refresh;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (vehicleTools.activeSelf) CloseVehicleTools();
            else if (inspector.activeSelf) CloseInspector();
        }

        public void ToggleInspector()
        {
            bool show = !inspector.activeSelf;
            CloseVehicleTools();
            SetInspector(show);
            if (show) inspector.GetComponentInChildren<Button>().Select();
        }

        public void CloseInspector()
        {
            SetInspector(false);
            toolsToggle.Select();
        }

        public void ToggleVehicleTools()
        {
            if (vehicleTools.activeSelf)
            {
                CloseVehicleTools();
                return;
            }
            vehicleTools.SetActive(true);
            SetInspector(false);
            vehicleTools.GetComponentInChildren<Button>().Select();
        }

        private void CloseVehicleTools()
        {
            if (!vehicleTools.activeSelf) return;
            vehicleTools.SetActive(false);
            monitors.SetActive(!inspector.activeSelf);
            toolsToggle.Select();
        }

        private void SetInspector(bool visible)
        {
            inspector.SetActive(visible);
            monitors.SetActive(!visible && !vehicleTools.activeSelf);
        }

        private void SelectRelatedPart(int index)
        {
            if (index < source.Alerts.Count) vehicle.Select(source.Alerts[index].PartId);
        }

        private void Refresh()
        {
            TelemetryFrame frame = source.Current;
            Color signal = source.HeatFaultActive ? Alarm : source.HeatInjection ? Caution : source.IsRunning ? Safe : Idle;
            readouts.status.text = source.HeatFaultActive ? "温度告警" : source.HeatInjection ? "升温演示" : source.IsRunning ? "仿真运行中" : "仿真已暂停";
            readouts.status.color = signal;
            readouts.statusLight.color = signal;
            readouts.clock.text = TelemetrySimulation.FormatTime(frame.Time);
            readouts.speed.text = FormattableString.Invariant($"{frame.Speed:F0} km/h");
            readouts.soc.text = FormattableString.Invariant($"{frame.Soc:F1}%");
            readouts.socRing.SetValue(frame.Soc * .01f);
            readouts.range.text = FormattableString.Invariant($"{frame.RangeKm:F0} km");
            readouts.rangeFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 281f * Mathf.Clamp01(frame.RangeKm / 450f));
            readouts.estimate.text = source.IsRunning ? "实时估算" : "样车估算";
            readouts.voltage.text = FormattableString.Invariant($"{frame.Voltage:F1} V");
            readouts.current.text = FormattableString.Invariant($"{frame.Current:+0.0;-0.0;0.0} A");
            readouts.batteryTemperature.text = FormattableString.Invariant($"{frame.BatteryTemperature:F1} °C");
            readouts.batteryTemperature.color = source.HeatFaultActive ? Alarm : Idle;
            readouts.chargeMode.text = frame.Current > .5f ? "放电" : frame.Current < -.5f ? "回充" : "待机";
            readouts.soh.text = FormattableString.Invariant($"{frame.Soh:F1}%");
            bool healthy = frame.Soh >= TelemetrySimulation.BatterySohThreshold;
            readouts.sohRing.SetValue(frame.Soh * .01f, healthy ? Safe : Caution);
            readouts.health.text = healthy ? "健康" : "低于健康阈值";
            readouts.health.color = healthy ? Safe : Caution;
            readouts.thermal.text = source.HeatFaultActive ? "电池过热" : source.HeatInjection ? "温度上升中" : "温度正常";
            readouts.thermal.color = source.HeatFaultActive ? Alarm : source.HeatInjection ? Caution : Safe;
            readouts.motorTemperature.text = FormattableString.Invariant($"{frame.MotorTemperature:F1} °C");
            readouts.rpm.text = FormattableString.Invariant($"{frame.MotorRpm:F0} rpm");
            readouts.power.text = FormattableString.Invariant($"{frame.Power:F1}");
            readouts.driveRing.SetValue(Mathf.Abs(frame.Power) / 80f, frame.Power < 0f ? Safe : Idle);
            readouts.charged.text = FormattableString.Invariant($"{frame.ChargeEnergy:F3} kWh");
            readouts.discharged.text = FormattableString.Invariant($"{frame.DischargeEnergy:F3} kWh");
            if (source.History.Count > 0)
            {
                float oldest = source.History[0].Time;
                for (int i = 0; i < readouts.historyTimes.Length; i++)
                    readouts.historyTimes[i].text = TelemetrySimulation.FormatTime(Mathf.Lerp(oldest, frame.Time, i / 2f));
            }

            SetAvailable(start, commandLabels[0], !source.IsRunning);
            SetAvailable(pause, commandLabels[1], source.IsRunning);
            UpdateRunControl();
            SetAvailable(inject, commandLabels[2], !source.HeatInjection && !source.HeatFaultActive);
            SetAvailable(recover, commandLabels[3], source.HeatInjection);
            RefreshRecords();
        }

        private void UpdateRunControl()
        {
            Button shown = source.IsRunning ? pause : start;
            Button hidden = source.IsRunning ? start : pause;
            bool moveFocus = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == hidden.gameObject;
            shown.gameObject.SetActive(true);
            hidden.gameObject.SetActive(false);
            if (moveFocus) shown.Select();
        }

        private static void SetAvailable(Button button, TMP_Text label, bool available)
        {
            button.interactable = available;
            label.alpha = available ? 1f : .46f;
        }

        private void RefreshRecords()
        {
            noAlerts.gameObject.SetActive(source.Alerts.Count == 0);
            int active = 0;
            TelemetrySeverity? driveSeverity = null;
            foreach (var item in source.Alerts)
            {
                if (item.Resolved) continue;
                active++;
                if (item.PartId == "EV-MOTOR-F" && driveSeverity != TelemetrySeverity.Critical) driveSeverity = item.Severity;
            }
            if (!driveSeverity.HasValue && source.MotorWarning)
                driveSeverity = TelemetrySeverity.Warning;
            readouts.driveCondition.text = driveSeverity == null ? "无电驱告警" :
                driveSeverity == TelemetrySeverity.Critical ? "严重" : "警告";
            readouts.driveCondition.color = driveSeverity == null ? Safe :
                driveSeverity == TelemetrySeverity.Critical ? Alarm : Caution;
            alertCount.text = $"{active} 条活动告警";
            alertCount.color = active > 0 ? Alarm : Safe;
            for (int i = 0; i < alertRows.Length; i++)
            {
                var row = alertRows[i];
                bool available = i < source.Alerts.Count;
                row.button.gameObject.SetActive(available);
                if (!available) continue;
                var item = source.Alerts[i];
                bool critical = item.Severity == TelemetrySeverity.Critical;
                Color color = item.Resolved ? Safe : critical ? Alarm : Caution;
                row.indicator.color = row.severity.color = color;
                row.severity.text = item.Resolved ? "已恢复" : critical ? "严重" : "警告";
                row.title.text = TelemetrySimulation.FormatTime(item.Time);
                row.message.text = item.Message;
                var profile = Array.Find(vehicle.Catalog.parts, part => part.id == item.PartId);
                row.part.text = (profile?.displayName ?? item.PartId) + "  /  " + item.PartId;
            }

            for (int i = 0; i < logs.Length; i++)
            {
                bool available = i < source.Logs.Count;
                logs[i].time.text = available ? TelemetrySimulation.FormatTime(source.Logs[i].Time) : "";
                logs[i].message.text = available ? source.Logs[i].Message : "";
            }
        }

#if UNITY_EDITOR
        public void Configure(TelemetrySimulation telemetry, VehicleRig rig, Readouts values, AlertRow[] alerts, LogRow[] logRows,
            TMP_Text emptyState, TMP_Text count, Button play, Button stop, Button fault, Button clearFault, Button toggleTools,
            GameObject panels, GameObject detail, GameObject tools)
        {
            source = telemetry;
            vehicle = rig;
            readouts = values;
            alertRows = alerts;
            logs = logRows;
            noAlerts = emptyState;
            alertCount = count;
            (start, pause, inject, recover) = (play, stop, fault, clearFault);
            toolsToggle = toggleTools;
            (monitors, inspector, vehicleTools) = (panels, detail, tools);
        }
#endif
    }
}
