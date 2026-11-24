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
        private Material[][] surfaceMaterials;
        private Material[][] twinMaterials;
        private bool twinSurface, selected, alerted;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int Signal = Shader.PropertyToID("_Signal");

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

        internal void SetDisplayMaterial(Material material, bool useTwin)
        {
            // 保留 FBX 的原始材质槽，模式切回外观时无需重新导入或实例化材质。
            if (material == null || twinSurface == useTwin) return;
            if (surfaceMaterials == null)
            {
                surfaceMaterials = new Material[renderers.Length][];
                twinMaterials = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++)
                {
                    surfaceMaterials[i] = renderers[i].sharedMaterials;
                    twinMaterials[i] = new Material[surfaceMaterials[i].Length];
                    System.Array.Fill(twinMaterials[i], material);
                }
            }

            var materials = useTwin ? twinMaterials : surfaceMaterials;
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = materials[i];
            twinSurface = useTwin;
            RefreshHighlight();
        }

        internal void SetHighlighted(bool value)
        {
            if (selected == value) return;
            selected = value;
            RefreshHighlight();
        }

        internal void SetAlert(bool value)
        {
            if (alerted == value) return;
            alerted = value;
            RefreshHighlight();
        }

        internal Vector3 Anchor()
        {
            if (renderers.Length == 0) return transform.position;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center + Vector3.up * bounds.extents.y * .55f;
        }

        private void RefreshHighlight()
        {
            highlight ??= new MaterialPropertyBlock();
            Color tint = alerted ? new Color(1f, .38f, .14f) : new Color(.26f, .84f, 1f);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    if (!selected && !alerted)
                    {
                        renderer.SetPropertyBlock(null, index);
                        continue;
                    }

                    var material = materials[index];
                    if (material == null || !material.HasProperty(BaseColor)) continue;
                    highlight.Clear();
                    highlight.SetColor(BaseColor, Color.Lerp(material.GetColor(BaseColor), tint, alerted ? .8f : .58f));
                    highlight.SetColor(Emission, tint * (alerted ? .56f : .24f));
                    highlight.SetFloat(Signal, alerted ? 1f : .65f);
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
