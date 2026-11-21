using UnityEngine;

namespace AutoTwin
{
    public enum PartMotion { None, Hinge, Wheel }
    public enum PartView { Always, Exterior, Powertrain }

    [DisallowMultipleComponent]
    public sealed class VehiclePart : MonoBehaviour
    {
        [SerializeField] private string partId;
        [SerializeField] private PartView view;
        [SerializeField] private PartMotion motion;
        [SerializeField] private Vector3 localAxis = Vector3.up;
        [SerializeField] private float openingDegrees;
        [SerializeField] private bool toolbarDoor;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Collider[] colliders;

        private Quaternion closedRotation;
        private float angle;
        private MaterialPropertyBlock highlight;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        public string Id => partId;
        public PartView View => view;
        public PartMotion Motion => motion;
        public bool ToolbarDoor => toolbarDoor;
        public bool IsOpen { get; set; }
        public bool IsSpinning { get; set; }

        private void Awake() => closedRotation = transform.localRotation;

        public void StepMotion(float deltaTime)
        {
            // 局部坐标由 Prefab 铰链或轮轴定义，闭合姿态取自导入绑定。
            if (motion == PartMotion.None)
                return;

            if (motion == PartMotion.Wheel && IsSpinning)
                angle = Mathf.Repeat(angle + 260f * deltaTime, 360f);
            else
                angle = Mathf.LerpAngle(angle, IsOpen ? openingDegrees : 0f, 1f - Mathf.Exp(-8f * deltaTime));

            transform.localRotation = closedRotation * Quaternion.AngleAxis(angle, localAxis);
        }

        internal void SetVisible(bool visible)
        {
            foreach (var renderer in renderers) renderer.enabled = visible;
            foreach (var collider in colliders) collider.enabled = visible;
        }

        internal void SetHighlighted(bool selected)
        {
            highlight ??= new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    if (!selected)
                    {
                        renderer.SetPropertyBlock(null, index);
                        continue;
                    }

                    var material = materials[index];
                    if (material == null || !material.HasProperty(BaseColor)) continue;
                    highlight.Clear();
                    highlight.SetColor(BaseColor, Color.Lerp(material.GetColor(BaseColor), new Color(0.18f, 0.85f, 1f), 0.65f));
                    highlight.SetColor(Emission, new Color(0.03f, 0.32f, 0.48f));
                    renderer.SetPropertyBlock(highlight, index);
                }
            }
        }

#if UNITY_EDITOR
        public void Configure(string id, PartView visibility, PartMotion action, Vector3 axis, float degrees, bool frontDoor)
        {
            partId = id;
            view = visibility;
            motion = action;
            localAxis = axis;
            openingDegrees = degrees;
            toolbarDoor = frontDoor;
            RefreshGeometry();
        }

        public void RefreshGeometry()
        {
            // 只缓存当前绑定范围内的网格与碰撞体，避免父级部件吞掉子级部件。
            renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), item => item.GetComponentInParent<VehiclePart>(true) == this);
            colliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), item => item.GetComponentInParent<VehiclePart>(true) == this);
        }
#endif
    }
}
