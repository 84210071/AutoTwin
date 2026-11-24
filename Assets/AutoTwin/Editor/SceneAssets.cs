using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AutoTwin.Editor
{
    internal sealed class SceneAssets
    {
        public const string Root = "Assets/AutoTwin";
        public const string Generated = Root + "/Generated";
        private readonly Dictionary<string, Material> materials = new();

        public static Color ColorOf(string hex)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex, out var color)) throw new ArgumentException(hex);
            return color;
        }

        public Material Lit(string name, string color, float metallic = 0.12f, float smoothness = 0.42f, string emission = "000000")
        {
            if (materials.TryGetValue(name, out var existing)) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", ColorOf(color));
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", ColorOf(emission));
            return materials[name] = Store(material, $"{Generated}/Materials/{name}.mat");
        }

        public static T Store<T>(T asset, string path) where T : UnityEngine.Object
        {
            var current = AssetDatabase.LoadAssetAtPath<T>(path);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (current == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            // 原地更新生成资产，保留已有场景和 Prefab 对资源 GUID 的引用。
            EditorUtility.CopySerialized(asset, current);
            UnityEngine.Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(current);
            return current;
        }

        public static Transform Node(Transform parent, string name, Vector3 localPosition = default)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = localPosition;
            return node;
        }

        public static Transform Primitive(Transform parent, string name, PrimitiveType shape, Vector3 center, Vector3 scale,
            Material material, bool pickable = false)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = center;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!pickable)
            {
                UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            }
            else
            {
                item.layer = VehicleRig.PickingLayer;
                // 轮胎的薄圆柱轮廓用于拾取，默认圆柱胶囊体在非均匀缩放后会超出轮胎。
                if (shape == PrimitiveType.Cylinder)
                {
                    UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
                    item.AddComponent<MeshCollider>().sharedMesh = item.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            return item.transform;
        }

    }
}
