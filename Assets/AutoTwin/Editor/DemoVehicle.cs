using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static AutoTwin.Editor.SceneAssets;

namespace AutoTwin.Editor
{
    internal sealed class DemoVehicle
    {
        public const string ModelPath = Root + "/Art/Vehicle/EV01 Concept.fbx";

        private readonly Dictionary<string, Material> finishes;

        public DemoVehicle(SceneAssets assets)
        {
            finishes = new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["Paint"] = assets.Lit("Concept · Fjord Metallic", "516471", .50f, .63f),
                ["Glass"] = assets.Lit("Concept · Polar Glass", "15212C", .00f, .65f),
                ["Seal"] = assets.Lit("Concept · Window Seal", "14181C", .00f, .27f),
                ["Graphite"] = assets.Lit("Concept · Graphite", "26323B", .20f, .42f),
                ["Satin"] = assets.Lit("Concept · Satin Alloy", "87959C", .71f, .50f),
                ["Alloy"] = assets.Lit("Concept · Machined Alloy", "9DA9AE", .80f, .61f),
                ["Tire"] = assets.Lit("Concept · Tire", "171B20", .00f, .29f),
                ["Tread"] = assets.Lit("Concept · Tread", "12161A", .00f, .17f),
                ["Ice"] = assets.Lit("Concept · Ice Signature", "B1D7DF", .04f, .54f, "5496AB"),
                ["Tail"] = assets.Lit("Concept · Rear Signature", "BC3A53", .04f, .50f, "A82E53"),
                ["HV"] = assets.Lit("Concept · HV Orange", "FEA85B", .18f, .42f, "582B16"),
                ["Battery"] = assets.Lit("Concept · Battery Modules", "A2CCD6", .45f, .54f, "243D4B"),
                ["Coolant"] = assets.Lit("Concept · Coolant", "52B9C3", .32f, .51f, "174C57"),
                ["Copper"] = assets.Lit("Concept · Copper", "AE724F", .63f, .44f, "2E1A17"),
                ["Lens"] = assets.Lit("Concept · Lamp Lens", "26394C", .08f, .76f),
                ["Cabin"] = assets.Lit("Concept · Cabin Trim", "313A42", .03f, .34f),
                ["Liner"] = assets.Lit("Concept · Wheelhouse Liner", "171F25", .00f, .24f),
                ["Display"] = assets.Lit("Concept · Cabin Display", "203B51", .03f, .63f, "132536")
            };
        }

        public GameObject Build(VehicleCatalog catalog)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException($"缺少 Blender 样车资源：{ModelPath}");
            var root = new GameObject("VehicleRoot");
            var imported = UnityEngine.Object.Instantiate(source);
            var nodes = imported.GetComponentsInChildren<Transform>(true).ToDictionary(node => node.name, StringComparer.Ordinal);
            var parts = new List<VehiclePart>();
            try
            {
                Attach(root.transform, nodes, parts, "Body", DemoCatalog.Body);
                Attach(root.transform, nodes, parts, "Hood", DemoCatalog.Hood, PartView.Exterior, PartMotion.Hinge,
                    Vector3.right, -DemoCatalog.HoodOpenDegrees);
                Attach(root.transform, nodes, parts, "Trunk", DemoCatalog.Trunk);
                for (int index = 0; index < 4; index++)
                {
                    int side = index % 2 == 0 ? -1 : 1;
                    bool front = index < 2;
                    string suffix = (front ? "F" : "R") + (side < 0 ? "L" : "R");
                    Attach(root.transform, nodes, parts, "Door_" + suffix, DemoCatalog.DoorIds[index], PartView.Exterior,
                        PartMotion.Hinge, Vector3.up, -side * (front ? DemoCatalog.FrontDoorOpenDegrees : DemoCatalog.RearDoorOpenDegrees), front);
                    Attach(root.transform, nodes, parts, "Wheel_" + suffix, DemoCatalog.WheelIds[index], PartView.Always,
                        PartMotion.Wheel, Vector3.right);
                }

                Attach(root.transform, nodes, parts, "Battery", DemoCatalog.Battery, PartView.Powertrain);
                Attach(root.transform, nodes, parts, "Motor_F", DemoCatalog.Motor, PartView.Powertrain);
                if (!catalog.ValidateBindings(parts, out string error)) throw new InvalidOperationException(error);
                root.AddComponent<VehicleRig>().Configure(catalog, parts.ToArray());
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(imported);
            }
        }

        private void Attach(Transform root, Dictionary<string, Transform> nodes, List<VehiclePart> parts, string nodeName, string id,
            PartView view = PartView.Exterior, PartMotion motion = PartMotion.None, Vector3 axis = default,
            float degrees = 0f, bool frontDoor = false)
        {
            if (!nodes.TryGetValue(nodeName, out var source)) throw new InvalidOperationException($"FBX 缺少节点：{nodeName}");

            // 动作节点始终使用 Unity 的轴向。网格保留 FBX 导入变换，但不会改变铰链的局部旋转轴。
            var partRoot = Node(root, nodeName, root.InverseTransformPoint(source.position));
            var meshes = source.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length == 0) throw new InvalidOperationException($"FBX 节点没有网格：{nodeName}");

            foreach (var mesh in meshes)
            {
                string family = mesh.name.Split(new[] { "__" }, 2, StringSplitOptions.None)[0];
                if (!finishes.TryGetValue(family, out var material)) throw new InvalidOperationException($"未定义模型材质族：{family}");
                mesh.transform.SetParent(partRoot, worldPositionStays: true);
                var renderer = mesh.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                if (!IsPickingSurface(view, family)) continue;
                mesh.gameObject.layer = VehicleRig.PickingLayer;
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            }

            var part = partRoot.gameObject.AddComponent<VehiclePart>();
            part.Configure(id, view, motion, axis, degrees, frontDoor);
            parts.Add(part);
        }

        private static bool IsPickingSurface(PartView view, string family)
        {
            switch (view)
            {
                case PartView.Always:
                    return family == "Tire";
                case PartView.Powertrain:
                    return family == "Graphite" || family == "Battery" || family == "Satin";
                default:
                    return family == "Paint" || family == "Glass";
            }
        }
    }
}
