using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoTwin
{
    public enum TelemetryMetric { Soc, BatteryTemperature, MotorTemperature, Power, Current, Voltage, MotorRpm, Speed }
    public enum TelemetrySeverity { Warning, Critical }

    public struct TelemetryFrame
    {
        public float Time, Soc, Soh, BatteryTemperature, MotorTemperature, MotorRpm, Speed;
        public float Power, Voltage, Current, RangeKm, ChargeEnergy, DischargeEnergy;

        public float Read(TelemetryMetric metric) => metric switch
        {
            TelemetryMetric.Soc => Soc,
            TelemetryMetric.BatteryTemperature => BatteryTemperature,
            TelemetryMetric.MotorTemperature => MotorTemperature,
            TelemetryMetric.Power => Power,
            TelemetryMetric.Current => Current,
            TelemetryMetric.Voltage => Voltage,
            TelemetryMetric.MotorRpm => MotorRpm,
            TelemetryMetric.Speed => Speed,
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };
    }

    public sealed class TelemetryAlert
    {
        public float Time { get; }
        public TelemetrySeverity Severity { get; }
        public string Message { get; }
        public string PartId { get; }
        public bool Resolved { get; internal set; }

        internal TelemetryAlert(float time, TelemetrySeverity severity, string message, string partId)
        {
            Time = time;
            Severity = severity;
            Message = message;
            PartId = partId;
        }
    }

    public readonly struct TelemetryLog
    {
        public float Time { get; }
        public string Message { get; }

        internal TelemetryLog(float time, string message)
        {
            Time = time;
            Message = message;
        }
    }

    [DisallowMultipleComponent]
    public sealed class TelemetrySimulation : MonoBehaviour
    {
        public const float BatteryCapacityKwh = 78f;
        public const float BatterySohThreshold = 80f;
        public const float BatteryOverheatThreshold = 52f;
        public const float BatteryRecoveryThreshold = 44f;
        public const float MotorOverheatThreshold = 105f;
        public const string BatteryPartId = "EV-BAT-HV";

        private const float ClockRate = 12f;
        private const float SampleInterval = 4f;
        private const int SampleCapacity = 97;
        private const int EntryCapacity = 24;
        private const float InitialSoc = 72f;
        private const float InitialSoh = 94.7f;
        private readonly List<TelemetryFrame> history = new(SampleCapacity);
        private readonly List<TelemetryAlert> alerts = new(EntryCapacity);
        private readonly List<TelemetryLog> logs = new(EntryCapacity);
        private TelemetryAlert currentHeatAlert;
        private float accumulation;

        public TelemetryFrame Current { get; private set; }
        public IReadOnlyList<TelemetryFrame> History => history;
        public IReadOnlyList<TelemetryAlert> Alerts => alerts;
        public IReadOnlyList<TelemetryLog> Logs => logs;
        public bool IsRunning { get; private set; }
        public bool HeatFaultActive => currentHeatAlert != null;
        public bool HeatInjection { get; private set; }
        public bool MotorWarning => Current.MotorTemperature >= MotorOverheatThreshold;
        public event Action Changed;

        private void Awake() => ResetSimulation();

        private void Update() => Advance(UnityEngine.Time.unscaledDeltaTime);

        public void Advance(float unscaledSeconds)
        {
            if (!IsRunning || unscaledSeconds <= 0f || float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            accumulation += unscaledSeconds * ClockRate;
            if (accumulation < SampleInterval) return;

            // 固定步长推进演示时钟，传感、图表和告警共享同一份采样。
            while (accumulation >= SampleInterval)
            {
                accumulation -= SampleInterval;
                Current = Integrate(Current, SampleInterval, HeatInjection);
                if (history.Count == SampleCapacity) history.RemoveAt(0);
                history.Add(Current);
                CheckHeat();
            }

            Changed?.Invoke();
        }

        public void StartSimulation()
        {
            if (IsRunning) return;
            SetRunning();
            Changed?.Invoke();
        }

        public void PauseSimulation()
        {
            if (!IsRunning) return;
            IsRunning = false;
            AddLog("演示模拟暂停");
            Changed?.Invoke();
        }

        public void ResetSimulation()
        {
            IsRunning = false;
            HeatInjection = false;
            currentHeatAlert = null;
            accumulation = 0f;
            alerts.Clear();
            logs.Clear();
            history.Clear();

            // 负时间为同一模型生成的预采样，零时刻之后才计入本次运行和能量统计。
            var sample = new TelemetryFrame
            {
                Time = -(SampleCapacity - 1) * SampleInterval,
                Speed = DriveSpeed(-(SampleCapacity - 1) * SampleInterval),
                Soc = InitialSoc,
                Soh = InitialSoh,
                BatteryTemperature = 32f,
                MotorTemperature = 49f
            };
            history.Add(sample);
            for (int i = 1; i < SampleCapacity; i++)
            {
                sample = Integrate(sample, SampleInterval, false);
                history.Add(sample);
            }

            float adjustment = InitialSoc - sample.Soc;
            for (int i = 0; i < history.Count; i++)
            {
                var entry = history[i];
                entry.Soc = Mathf.Clamp(entry.Soc + adjustment, 0f, 100f);
                entry.Soh = InitialSoh;
                entry.ChargeEnergy = entry.DischargeEnergy = 0f;
                UpdateEstimates(ref entry);
                history[i] = entry;
            }

            Current = history[history.Count - 1];
            AddLog("预采样已装载，等待开始");
            Changed?.Invoke();
        }

        public void InjectBatteryHeat()
        {
            if (HeatInjection) return;
            SetRunning();
            HeatInjection = true;
            AddLog("已注入电池热异常");
            Changed?.Invoke();
        }

        public void RecoverBatteryHeat()
        {
            if (!HeatInjection) return;
            SetRunning();
            HeatInjection = false;
            AddLog("热异常源已移除，电池冷却中");
            Changed?.Invoke();
        }

        private static TelemetryFrame Integrate(TelemetryFrame previous, float step, bool overheated)
        {
            var sample = previous;
            sample.Time += step;
            sample.Speed = previous.Soc > .05f ? DriveSpeed(sample.Time) : 0f;
            float metresPerSecond = sample.Speed / 3.6f;
            float acceleration = (sample.Speed - previous.Speed) / (3.6f * step);
            sample.MotorRpm = metresPerSecond * 8.9f * 60f / (2f * Mathf.PI * .36f);

            // 轮端功率包含滚阻、风阻及加减速，负功率按较低效率回充。
            float force = 1920f * acceleration + 240f + .43f * metresPerSecond * metresPerSecond;
            float wheelPower = force * metresPerSecond / 1000f;
            sample.Power = wheelPower > 0f ? wheelPower / .88f + 1.3f : wheelPower * .66f + 1.3f;
            if (previous.Soc <= .05f) sample.Power = 0f;

            // 依据可用容量约束单次能量变化，使展示功率与充放电累计在满电边界上一致。
            float capacity = UsableCapacity(previous.Soh);
            float stored = previous.Soc * .01f * capacity;
            float energy = Mathf.Clamp(sample.Power * step / 3600f, stored - capacity, stored);
            sample.Power = energy * 3600f / step;

            float batteryNormal = 29.2f + Mathf.Abs(sample.Power) * .12f + 1.9f * Mathf.Sin(sample.Time * .042f);
            float batteryTarget = overheated ? 65f : batteryNormal;
            float motorTarget = 38f + Mathf.Abs(sample.Power) * .68f + sample.Speed * .13f;
            sample.BatteryTemperature = Approach(previous.BatteryTemperature, batteryTarget, overheated ? 14f : 32f, step);
            sample.MotorTemperature = Approach(previous.MotorTemperature, motorTarget, 23f, step);

            sample.Soc = Mathf.Clamp((stored - energy) / capacity * 100f, 0f, 100f);
            sample.ChargeEnergy += Mathf.Max(0f, -energy);
            sample.DischargeEnergy += Mathf.Max(0f, energy);
            // SOH 表示长期衰减。过热用独立阈值告警，温度回落后也不会恢复已损耗的 SOH。
            sample.Soh = Mathf.Max(0f, previous.Soh - step * (.000002f + Mathf.Max(0f, sample.BatteryTemperature - 48f) * .000002f));
            UpdateEstimates(ref sample);
            return sample;
        }

        private static void UpdateEstimates(ref TelemetryFrame sample)
        {
            float openVoltage = 334f + sample.Soc * .82f;
            sample.Voltage = openVoltage - .051f * (sample.Power * 1000f / openVoltage);
            sample.Current = sample.Power * 1000f / sample.Voltage;
            sample.RangeKm = sample.Soc * UsableCapacity(sample.Soh) / 16.2f;
        }

        private static float UsableCapacity(float soh) => BatteryCapacityKwh * Mathf.Max(.05f, soh * .01f);

        private static float DriveSpeed(float time) => Mathf.Clamp(46f + 20f * Mathf.Sin(.054f * time) + 9f * Mathf.Sin(.131f * time + .7f), 12f, 78f);

        private static float Approach(float value, float target, float delay, float step) => Mathf.Lerp(value, target, 1f - Mathf.Exp(-step / delay));

        private void CheckHeat()
        {
            if (currentHeatAlert == null && Current.BatteryTemperature >= BatteryOverheatThreshold)
            {
                currentHeatAlert = new TelemetryAlert(Current.Time, TelemetrySeverity.Warning, "动力电池温度高于阈值", BatteryPartId);
                AddNewest(alerts, currentHeatAlert);
                AddLog("电池过热告警已触发");
            }
            else if (currentHeatAlert != null && Current.BatteryTemperature <= BatteryRecoveryThreshold)
            {
                currentHeatAlert.Resolved = true;
                currentHeatAlert = null;
                AddLog("电池温度恢复，告警解除");
            }
        }

        private void SetRunning()
        {
            if (IsRunning) return;
            IsRunning = true;
            AddLog("演示模拟运行中");
        }

        private void AddLog(string message) => AddNewest(logs, new TelemetryLog(Current.Time, message));

        private static void AddNewest<T>(List<T> list, T entry)
        {
            if (list.Count == EntryCapacity) list.RemoveAt(EntryCapacity - 1);
            list.Insert(0, entry);
        }

        public static string FormatTime(float elapsed)
        {
            int seconds = Mathf.FloorToInt(Mathf.Abs(elapsed));
            int hours = seconds / 3600;
            string sign = elapsed < 0f ? "-" : "";
            return hours > 0 ? $"{sign}{hours:00}:{seconds / 60 % 60:00}:{seconds % 60:00}"
                : $"{sign}{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
