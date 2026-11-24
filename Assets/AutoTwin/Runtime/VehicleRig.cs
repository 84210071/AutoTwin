using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoTwin
{
    [DisallowMultipleComponent]
    public sealed class VehicleRig : MonoBehaviour
    {
        [SerializeField] private VehicleCatalog catalog;
        [SerializeField] private VehiclePart[] parts;
        [SerializeField] private Material twinShell, twinWheels;
        [SerializeField] private bool startInTwinMode;
        private readonly Dictionary<string, VehiclePart> bindings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PartProfile> profiles = new(StringComparer.Ordinal);
        private VehiclePart selected;

        public const int PickingLayer = 8;
        public VehicleCatalog Catalog => catalog;
        public PartProfile Selection => selected == null ? null : profiles[selected.Id];
        public bool PowertrainVisible { get; private set; }
        public bool BatteryAlert { get; private set; }
        public bool FrontDoorsOpen { get; private set; }
        public bool HoodOpen { get; private set; }
        public bool WheelsSpinning { get; private set; }
        public event Action Changed;

        private void Awake()
        {
            if (!catalog.ValidateBindings(parts, out string error))
            {
                throw new InvalidOperationException(error);
            }

            foreach (var profile in catalog.parts) profiles.Add(profile.id, profile);
            foreach (var part in parts) bindings.Add(part.Id, part);
            PowertrainVisible = startInTwinMode;
            ApplyView();
        }

        private void Update()
        {
            foreach (var part in parts) part.StepMotion(Time.unscaledDeltaTime);
        }

        public void Select(string id)
        {
            if (!bindings.TryGetValue(id, out var part)) return;

            // 从告警或清单定位内部件时自动打开透明车身。
            if (part.View == PartView.Powertrain && !PowertrainVisible)
            {
                PowertrainVisible = true;
                ApplyView();
            }

            selected?.SetHighlighted(false);
            selected = part;
            selected.SetHighlighted(true);
            Changed?.Invoke();
        }

        public void SelectAt(Ray ray)
        {
            // 同一车辆的碰撞体可能刚刚因动画或视图切换而移动。
            Physics.SyncTransforms();
            float nearest = float.PositiveInfinity;
            float nearestInternal = float.PositiveInfinity;
            VehiclePart candidate = null;
            VehiclePart internalCandidate = null;
            foreach (var hit in Physics.RaycastAll(ray, 40f, 1 << PickingLayer, QueryTriggerInteraction.Ignore))
            {
                var part = hit.collider.GetComponentInParent<VehiclePart>();
                if (part == null || !bindings.TryGetValue(part.Id, out var ownedPart) || part != ownedPart) continue;
                if (hit.distance < nearest)
                {
                    candidate = part;
                    nearest = hit.distance;
                }
                if (part.View == PartView.Powertrain && hit.distance < nearestInternal)
                {
                    internalCandidate = part;
                    nearestInternal = hit.distance;
                }
            }
            // 透视模式下可穿过外壳点击动力部件，车轮仍以最近的实体命中为准。
            if (PowertrainVisible && candidate?.View == PartView.Exterior && internalCandidate != null) candidate = internalCandidate;
            if (candidate == null) ClearSelection();
            else Select(candidate.Id);
        }

        public void ClearSelection()
        {
            selected?.SetHighlighted(false);
            selected = null;
            Changed?.Invoke();
        }

        public void TogglePowertrain()
        {
            PowertrainVisible = !PowertrainVisible;
            ApplyView();
            if (!PowertrainVisible && selected != null && selected.View == PartView.Powertrain) ClearSelection();
            else Changed?.Invoke();
        }

        public void SetBatteryAlert(bool active)
        {
            if (BatteryAlert == active) return;
            BatteryAlert = active;
            if (bindings.TryGetValue(TelemetrySimulation.BatteryPartId, out var battery)) battery.SetAlert(active);
            if (active && !PowertrainVisible) PowertrainVisible = true;
            ApplyView();
            Changed?.Invoke();
        }

        public bool TryPartAnchor(string id, out Vector3 anchor)
        {
            if (bindings.TryGetValue(id, out var part))
            {
                anchor = part.Anchor();
                return true;
            }
            anchor = default;
            return false;
        }

        public void ToggleFrontDoors() => SetPanels(!FrontDoorsOpen, HoodOpen);
        public void ToggleHood() => SetPanels(FrontDoorsOpen, !HoodOpen);

        private void SetPanels(bool doors, bool hood)
        {
            FrontDoorsOpen = doors;
            HoodOpen = hood;
            foreach (var part in parts)
            {
                if (part.ToolbarDoor) part.IsOpen = doors;
                if (profiles[part.Id].type == PartType.Hood) part.IsOpen = hood;
            }
            ApplyView();
            Changed?.Invoke();
        }

        public void ToggleWheels() => SetWheels(!WheelsSpinning);

        private void SetWheels(bool spinning)
        {
            WheelsSpinning = spinning;
            foreach (var part in parts)
                if (part.Motion == PartMotion.Wheel) part.IsSpinning = spinning;
            Changed?.Invoke();
        }

        public void ResetVehicle()
        {
            PowertrainVisible = startInTwinMode || BatteryAlert;
            ClearSelection();
            ApplyView();
            SetPanels(false, false);
            SetWheels(false);
        }

        private void ApplyView()
        {
            foreach (var part in parts)
            {
                bool frontMotor = profiles[part.Id].type == PartType.Motor;
                part.SetVisible(part.View != PartView.Powertrain || PowertrainVisible || (frontMotor && HoodOpen));
                if (part.View != PartView.Powertrain)
                    part.SetDisplayMaterial(part.View == PartView.Always ? twinWheels : twinShell, PowertrainVisible);
            }
        }

#if UNITY_EDITOR
        public void Configure(VehicleCatalog source, VehiclePart[] bindings)
        {
            catalog = source;
            parts = bindings;
        }

        public void ConfigureTwin(Material shell, Material wheels)
        {
            twinShell = shell;
            twinWheels = wheels;
            startInTwinMode = true;
        }
#endif
    }
}
