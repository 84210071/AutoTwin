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
        private readonly Dictionary<string, VehiclePart> bindings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PartProfile> profiles = new(StringComparer.Ordinal);
        private VehiclePart selected;

        public const int PickingLayer = 8;
        public VehicleCatalog Catalog => catalog;
        public PartProfile Selection => selected == null ? null : profiles[selected.Id];
        public bool PowertrainVisible { get; private set; }
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
            ApplyView();
        }

        private void Update()
        {
            foreach (var part in parts) part.StepMotion(Time.unscaledDeltaTime);
        }

        public void Select(string id)
        {
            if (!bindings.TryGetValue(id, out var part)) return;

            // 清单可以直接打开相应视图，让内部件和外覆盖件始终可定位。
            if (part.View != PartView.Always)
            {
                PowertrainVisible = part.View == PartView.Powertrain;
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
            if (Physics.Raycast(ray, out var hit, 40f, 1 << PickingLayer, QueryTriggerInteraction.Ignore))
            {
                var part = hit.collider.GetComponentInParent<VehiclePart>();
                if (part != null && bindings.TryGetValue(part.Id, out var ownedPart) && part == ownedPart)
                {
                    Select(part.Id);
                    return;
                }
            }

            ClearSelection();
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
            if (selected != null && selected.View != PartView.Always) ClearSelection();
            else Changed?.Invoke();
        }

        public void ToggleFrontDoors() => SetPanels(!FrontDoorsOpen, HoodOpen);
        public void ToggleHood() => SetPanels(FrontDoorsOpen, !HoodOpen);

        private void SetPanels(bool doors, bool hood)
        {
            if (PowertrainVisible)
            {
                PowertrainVisible = false;
                ApplyView();
                if (selected != null && selected.View == PartView.Powertrain) ClearSelection();
            }

            FrontDoorsOpen = doors;
            HoodOpen = hood;
            foreach (var part in parts)
            {
                if (part.ToolbarDoor) part.IsOpen = doors;
                if (profiles[part.Id].type == PartType.Hood) part.IsOpen = hood;
            }
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
            PowertrainVisible = false;
            ClearSelection();
            ApplyView();
            SetPanels(false, false);
            SetWheels(false);
        }

        private void ApplyView()
        {
            foreach (var part in parts)
                part.SetVisible(part.View == PartView.Always || (part.View == PartView.Powertrain) == PowertrainVisible);
        }

#if UNITY_EDITOR
        public void Configure(VehicleCatalog source, VehiclePart[] bindings)
        {
            catalog = source;
            parts = bindings;
        }
#endif
    }
}
