using System;
using UnityEditor;
using UnityEngine;

namespace AutoTwin.Editor
{
    internal static class DemoCatalog
    {
        public const string Body = "VEH-BODY", Hood = "BODY-HOOD-F", Trunk = "BODY-TRUNK-R";
        public const string Battery = "EV-BAT-HV", Motor = "EV-MOTOR-F";
        public const int HoodOpenDegrees = 34;
        public const int FrontDoorOpenDegrees = 40;
        public const int RearDoorOpenDegrees = 62;
        private const string VehicleCode = "AUTOTWIN-EV01";
        private const string BodyDimensions = "4,786 × 2,018 mm";
        private const string Wheelbase = "2,890 mm";
        private const string WheelDiameter = "746 mm";
        private const string FrontDoorDescription = "铰链位于门板前缘，可在车身操作菜单中联动前车门。";
        private const string PreviousFrontDoorDescription = "铰链位于门板前缘，可通过下方工具栏联动前车门。";
        public static readonly string[] DoorIds = { "DOOR-FL", "DOOR-FR", "DOOR-RL", "DOOR-RR" };
        public static readonly string[] WheelIds = { "WHEEL-FL", "WHEEL-FR", "WHEEL-RL", "WHEEL-RR" };

        public static VehicleCatalog Create()
        {
            // 这些展示数据生成到独立配置资产，运行时面板可直接读取。
            var catalog = ScriptableObject.CreateInstance<VehicleCatalog>();
            catalog.vehicleName = "EV-01 纯电样车";
            catalog.vehicleCode = VehicleCode;
            catalog.powertrain = "纯电驱动";
            catalog.stateOfCharge = 72;
            catalog.rangeKm = 328;
            catalog.packVoltage = 394;
            catalog.overview = new[] { new PartMetric("动力架构", "前置单电机"), new PartMetric("动力电池", "78.0 kWh"), new PartMetric("电压平台", "400 V"), new PartMetric("整备质量", "1,920 kg") };

            catalog.parts = new[]
            {
                Part(Body, "车身总成", PartType.Body, "车身结构", "样车外覆盖件及乘员舱结构，为各车门和前后盖提供安装基准。", "车长 × 车宽", BodyDimensions, "轴距", Wheelbase, "整备质量", "1,920 kg"),
                Part(Hood, "前引擎盖", PartType.Hood, "活动覆盖件", "提供前机舱检修入口，围绕靠近前风挡的横向铰链开启。", "开启角度", $"{HoodOpenDegrees}°", "铰链位置", "后缘横轴", "面板材质", "铝合金"),
                Part(Trunk, "后备箱盖", PartType.Trunk, "活动覆盖件", "后部行李舱检修与装载入口，本阶段支持查看参数。", "行李舱容积", "465 L", "安装位置", "车尾", "面板材质", "铝合金"),
                Part(Battery, "动力电池", PartType.Battery, "高压储能", "地板下方的动力电池包，为电驱系统供电。当前数值为展示用工况。", "额定容量", "78.0 kWh", "荷电状态", $"{catalog.stateOfCharge}%", "端电压", $"{catalog.packVoltage} V", "电芯温度", "29.4 °C"),
                Part(Motor, "前驱动电机", PartType.Motor, "前轴电驱", "前舱电驱总成包含电机、减速器及高压连接端。", "峰值功率", "180 kW", "峰值扭矩", "330 N·m", "母线电压", "400 V", "绕组温度", "34.0 °C"),
                Door(0, "左前车门", "左前"), Door(1, "右前车门", "右前"), Door(2, "左后车门", "左后"), Door(3, "右后车门", "右后"),
                Wheel(0, "左前车轮"), Wheel(1, "右前车轮"), Wheel(2, "左后车轮"), Wheel(3, "右后车轮")
            };
            return catalog;
        }

        public static void RefreshDemoModelMetrics(VehicleCatalog catalog)
        {
            if (catalog.vehicleCode != VehicleCode) return;

            // 只迁移内置样车尚未编辑的旧参数，保留用户对说明和车型数据的修改。
            bool changed = false;
            foreach (var profile in catalog.parts)
            {
                if (profile == null) continue;
                if (profile.id == Body)
                {
                    changed |= ReplaceMetric(profile, "车长 × 车宽", "4,640 × 1,940 mm", BodyDimensions);
                    changed |= ReplaceMetric(profile, "轴距", "2,940 mm", Wheelbase);
                }
                if (profile.type == PartType.Wheel) changed |= ReplaceMetric(profile, "轮胎外径", "720 mm", WheelDiameter);
                if (profile.id == Hood) changed |= ReplaceMetric(profile, "开启角度", "52°", $"{HoodOpenDegrees}°");
                if (profile.id != DoorIds[0] && profile.id != DoorIds[1]) continue;
                changed |= ReplaceMetric(profile, "开启规格", "62°", $"{FrontDoorOpenDegrees}°");
                if (profile.description != PreviousFrontDoorDescription) continue;
                profile.description = FrontDoorDescription;
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(catalog);
        }

        private static bool ReplaceMetric(PartProfile profile, string label, string previousValue, string currentValue)
        {
            if (profile.metrics == null) return false;
            int index = Array.FindIndex(profile.metrics, metric => metric.label == label && metric.value == previousValue);
            if (index < 0) return false;
            profile.metrics[index].value = currentValue;
            return true;
        }

        private static PartProfile Door(int index, string name, string location) =>
            Part(DoorIds[index], name, PartType.Door, "活动覆盖件", index < 2 ? FrontDoorDescription : "铰链位于门板前缘，本阶段支持查看参数。",
                "安装位置", location, "铰链位置", "门板前缘", "开启规格", $"{(index < 2 ? FrontDoorOpenDegrees : RearDoorOpenDegrees)}°");

        private static PartProfile Wheel(int index, string name) =>
            Part(WheelIds[index], name, PartType.Wheel, "车轮总成", "轮胎与轮辋围绕轮毂中心转动，可在视图内开启四轮旋转演示。",
                "轮胎规格", "235 / 55 R19", "标准胎压", "250 kPa", "轮胎外径", WheelDiameter);

        private static PartProfile Part(string id, string title, PartType type, string typeLabel, string description, params string[] data)
        {
            var metrics = new PartMetric[data.Length / 2];
            for (int i = 0; i < metrics.Length; i++) metrics[i] = new PartMetric(data[i * 2], data[i * 2 + 1]);
            return new PartProfile { id = id, displayName = title, type = type, typeLabel = typeLabel, description = description, metrics = metrics };
        }
    }
}
