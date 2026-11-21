using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoTwin
{
    public enum PartType { Body, Wheel, Door, Hood, Trunk, Battery, Motor }

    [Serializable]
    public struct PartMetric
    {
        public string label;
        public string value;

        public PartMetric(string label, string value)
        {
            this.label = label;
            this.value = value;
        }
    }

    [Serializable]
    public sealed class PartProfile
    {
        public string id;
        public string displayName;
        public PartType type;
        public string typeLabel;
        [TextArea] public string description;
        public PartMetric[] metrics;
    }

    [CreateAssetMenu(menuName = "AutoTwin/车辆配置")]
    public sealed class VehicleCatalog : ScriptableObject
    {
        public string vehicleName;
        public string vehicleCode;
        public string powertrain;
        [Range(0, 100)] public int stateOfCharge;
        public int rangeKm;
        public int packVoltage;
        public PartMetric[] overview;
        public PartProfile[] parts;

        public bool ValidateBindings(IEnumerable<VehiclePart> bindings, out string error)
        {
            var missing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in parts)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.id) || !missing.Add(profile.id))
                {
                    error = "配置存在空缺或重复的部件 ID。";
                    return false;
                }
            }

            foreach (var part in bindings)
            {
                if (!missing.Remove(part.Id))
                {
                    error = $"部件 ID 未配置或重复绑定：{part.Id}";
                    return false;
                }
            }

            error = missing.Count == 0 ? null : $"缺少部件绑定：{string.Join(", ", missing)}";
            return error == null;
        }
    }
}
