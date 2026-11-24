using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoTwin
{
    public sealed class ConsoleView : MonoBehaviour
    {
        [Serializable] public struct MetricRow { public TMP_Text label, value; }
        [Serializable] public struct PartButton { public string id; public Button button; }

        [SerializeField] private VehicleRig vehicle;
        [SerializeField] private VehicleViewport viewport;
        [SerializeField] private TMP_Text nameText, typeText, idText, description, viewLabel;
        [SerializeField] private MetricRow[] metricRows;
        [SerializeField] private PartButton[] partButtons;
        [SerializeField] private Button free, pick, power, wheels, doors, hood;
        [SerializeField] private TMP_Text powerText, wheelsText, doorText, hoodText;

        private void Start()
        {
            // 清单对应稳定 ID，替换模型后仍使用同一份配置和按钮。
            foreach (var entry in partButtons)
            {
                string id = entry.id;
                entry.button.onClick.AddListener(() => vehicle.Select(id));
            }

            vehicle.Changed += Refresh;
            viewport.Clicked += vehicle.SelectAt;
            Refresh();
        }

        private void OnDestroy()
        {
            if (vehicle != null) vehicle.Changed -= Refresh;
            if (vehicle != null && viewport != null) viewport.Clicked -= vehicle.SelectAt;
        }

        public void ResetCamera() => viewport.ResetView();

        public void ResetVehicle()
        {
            viewport.ResetView();
            viewport.SelectionEnabled = true;
            vehicle.ResetVehicle();
        }

        public void FreeObservation()
        {
            viewport.SelectionEnabled = false;
            vehicle.ClearSelection();
        }

        public void EnablePicking()
        {
            viewport.SelectionEnabled = true;
            Refresh();
        }

        public void TogglePowertrain() => vehicle.TogglePowertrain();
        public void ToggleWheels() => vehicle.ToggleWheels();
        public void ToggleFrontDoors() => vehicle.ToggleFrontDoors();
        public void ToggleHood() => vehicle.ToggleHood();

        private void Refresh()
        {
            var selection = vehicle.Selection;
            nameText.text = selection?.displayName ?? vehicle.Catalog.vehicleName;
            typeText.text = selection?.typeLabel ?? "整车概览";
            idText.text = selection?.id ?? vehicle.Catalog.vehicleCode;
            description.text = selection?.description ?? "点击三维视图或部件清单，查看部件说明与模拟参数。";
            var metrics = selection?.metrics ?? vehicle.Catalog.overview;
            for (int i = 0; i < metricRows.Length; i++)
            {
                var row = metricRows[i];
                row.label.transform.parent.gameObject.SetActive(i < metrics.Length);
                if (i >= metrics.Length) continue;
                row.label.text = metrics[i].label;
                row.value.text = metrics[i].value;
            }

            foreach (var entry in partButtons) SetActive(entry.button, entry.id == selection?.id);
            SetActive(free, !viewport.SelectionEnabled);
            SetActive(pick, viewport.SelectionEnabled);
            SetActive(power, vehicle.PowertrainVisible);
            SetActive(wheels, vehicle.WheelsSpinning);
            SetActive(doors, vehicle.FrontDoorsOpen);
            SetActive(hood, vehicle.HoodOpen);
            viewLabel.text = vehicle.PowertrainVisible ? "EV-01   /   数字孪生" : "EV-01   /   普通展示";
            powerText.text = vehicle.PowertrainVisible ? "普通展示" : "数字孪生";
            wheelsText.text = vehicle.WheelsSpinning ? "停止车轮" : "旋转车轮";
            doorText.text = vehicle.FrontDoorsOpen ? "关闭前车门" : "打开前车门";
            hoodText.text = vehicle.HoodOpen ? "关闭引擎盖" : "打开引擎盖";
        }

        private static void SetActive(Button button, bool active)
        {
            var states = button.colors;
            states.normalColor = active ? new Color32(85, 115, 157, 51) : Color.clear;
            button.colors = states;
        }

#if UNITY_EDITOR
        public void Configure(VehicleRig rig, VehicleViewport controls, TMP_Text[] detail, MetricRow[] rows,
            PartButton[] list, Button[] buttons, TMP_Text[] actionLabels)
        {
            vehicle = rig;
            viewport = controls;
            (nameText, typeText, idText, description, viewLabel) = (detail[0], detail[1], detail[2], detail[3], detail[4]);
            (free, pick, power, wheels, doors, hood) = (buttons[0], buttons[1], buttons[2], buttons[3], buttons[4], buttons[5]);
            (powerText, wheelsText, doorText, hoodText) = (actionLabels[0], actionLabels[1], actionLabels[2], actionLabels[3]);
            metricRows = rows;
            partButtons = list;
        }
#endif
    }
}
