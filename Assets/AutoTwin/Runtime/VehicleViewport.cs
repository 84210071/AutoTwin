using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoTwin
{
    [DisallowMultipleComponent]
    public sealed class VehicleViewport : MonoBehaviour
    {
        [SerializeField] private Camera vehicleCamera;
        [SerializeField] private RawImage viewport;
        [SerializeField] private EventSystem eventSystem;

        private static readonly Vector3 DefaultTarget = new(0f, 0.65f, 0f);
        private const float DefaultYaw = 140f;
        private const float DefaultPitch = 22f;
        private const float DefaultDistance = 6.8f;
        private const float ClickThreshold = 5f;

        private Vector3 target, smoothTarget;
        private float yaw, pitch, distance, smoothYaw, smoothPitch, smoothDistance;
        private Vector2 lastPointer;
        private float leftTravel;
        private bool leftHeld, middleHeld;
        private PointerEventData pointer;
        private readonly List<RaycastResult> hits = new();

        public bool SelectionEnabled { get; set; } = true;
        public event System.Action<Ray> Clicked;

        private void Awake()
        {
            pointer = new PointerEventData(eventSystem);
            // 先为 RawImage 分配相机输出，确保首次绘制就有可采样的目标。
            var output = vehicleCamera.targetTexture;
            if (!output.IsCreated()) output.Create();
            ResetView();
            StepCamera(1f);
        }

        private void LateUpdate()
        {
            if (!Application.isFocused)
            {
                leftHeld = middleHeld = false;
            }
            else
            {
                ReadPointer();
            }

            StepCamera(1f - Mathf.Exp(-13f * Time.unscaledDeltaTime));
        }

        private void ReadPointer()
        {
            Vector2 position = Input.mousePosition;
            Vector2 delta = position - lastPointer;
            lastPointer = position;
            bool inside = IsViewportAt(position);

            if (Input.GetMouseButtonDown(0))
            {
                leftHeld = inside;
                leftTravel = 0f;
                delta = Vector2.zero;
            }

            if (Input.GetMouseButtonDown(2))
            {
                middleHeld = inside;
                delta = Vector2.zero;
            }

            // 在视口开始的手势离开视口后会取消，UI 上按下并移入视口也不会触发手势。
            if (!inside)
            {
                leftHeld = middleHeld = false;
                return;
            }

            if (leftHeld)
            {
                leftTravel += delta.magnitude;
                if (leftTravel > ClickThreshold) Orbit(delta);
                if (Input.GetMouseButtonUp(0))
                {
                    leftHeld = false;
                    if (SelectionEnabled && leftTravel <= ClickThreshold && TryRay(position, out Ray ray))
                        Clicked?.Invoke(ray);
                }
            }

            if (middleHeld && Input.GetMouseButton(2)) Pan(delta, viewport.rectTransform.rect.height * viewport.canvas.scaleFactor);
            else middleHeld = false;

            Zoom(Input.mouseScrollDelta.y);
        }

        public bool IsViewportAt(Vector2 position)
        {
            pointer.position = position;
            hits.Clear();
            eventSystem.RaycastAll(pointer, hits);
            return hits.Count > 0 && hits[0].gameObject == viewport.gameObject;
        }

        public bool TryRay(Vector2 screenPosition, out Ray ray)
        {
            var rect = viewport.rectTransform.rect;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport.rectTransform, screenPosition, null, out var point);
            Vector2 uv = Rect.PointToNormalized(rect, point);
            ray = vehicleCamera.ViewportPointToRay(new Vector3(uv.x, uv.y));
            return rect.Contains(point);
        }

        public void Orbit(Vector2 delta)
        {
            yaw = Mathf.Repeat(yaw + delta.x * 0.24f, 360f);
            pitch = Mathf.Clamp(pitch + delta.y * 0.24f, 7f, 75f);
        }

        public void Zoom(float amount) => distance = Mathf.Clamp(distance * Mathf.Exp(-0.13f * amount), 3.3f, 11f);

        public void Pan(Vector2 delta, float viewportHeight)
        {
            float scale = 2f * distance * Mathf.Tan(vehicleCamera.fieldOfView * Mathf.Deg2Rad / 2f) / Mathf.Max(1f, viewportHeight);
            var cameraAxes = vehicleCamera.transform;
            var offset = (cameraAxes.right * delta.x + cameraAxes.up * delta.y) * scale;
            target = DefaultTarget + Vector3.ClampMagnitude(target - DefaultTarget - offset, 2.3f);
        }

        public void ResetView()
        {
            yaw = DefaultYaw;
            pitch = DefaultPitch;
            distance = DefaultDistance;
            target = DefaultTarget;
        }

        public void StepCamera(float blend)
        {
            smoothYaw = Mathf.LerpAngle(smoothYaw, yaw, blend);
            smoothPitch = Mathf.Lerp(smoothPitch, pitch, blend);
            smoothDistance = Mathf.Lerp(smoothDistance, distance, blend);
            smoothTarget = Vector3.Lerp(smoothTarget, target, blend);
            var rotation = Quaternion.Euler(smoothPitch, smoothYaw, 0f);
            vehicleCamera.transform.SetPositionAndRotation(smoothTarget - rotation * Vector3.forward * smoothDistance, rotation);
        }

#if UNITY_EDITOR
        public void Configure(Camera camera, RawImage image, EventSystem events)
        {
            vehicleCamera = camera;
            viewport = image;
            eventSystem = events;
            ResetView();
            StepCamera(1f);
        }
#endif
    }
}
