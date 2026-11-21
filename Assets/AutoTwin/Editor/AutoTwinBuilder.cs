using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using static AutoTwin.Editor.SceneAssets;

namespace AutoTwin.Editor
{
    public static class AutoTwinBuilder
    {
        public const string ScenePath = "Assets/Scenes/AutoTwinMain.unity";
        private const string PrefabPath = Generated + "/EV01 Demo Vehicle.prefab";
        private const string FontPath = Root + "/Art/Fonts/AutoTwin Sans.ttf";
        private const string FontAssetPath = Generated + "/AutoTwin Sans SDF.asset";

        [MenuItem("AutoTwin/重建第一阶段演示场景")]
        private static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("重建演示场景",
                "此操作会重新生成 AutoTwinMain 与临时车辆资源。若已接入正式模型，请先另存场景。", "重建", "取消")) return;
            Build();
        }

        public static void Build()
        {
            foreach (string folder in new[] { Generated, Generated + "/Materials", Generated + "/Meshes" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            EnsurePickingLayer();
            var assets = new SceneAssets();
            string catalogPath = Generated + "/EV01 Vehicle Data.asset";
            // 参数资产允许独立编辑；重建演示仍读取本地车型配置。
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(catalogPath);
            if (catalog == null) catalog = Store(DemoCatalog.Create(), catalogPath);
            var font = GetFont();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var demo = new DemoVehicle(assets).Build(catalog);
            var prefab = PrefabUtility.SaveAsPrefabAsset(demo, PrefabPath);
            UnityEngine.Object.DestroyImmediate(demo);
            var rig = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<VehicleRig>();

            var texture = Store(new RenderTexture(ConsoleLayout.ViewWidth, ConsoleLayout.ViewHeight, 24, RenderTextureFormat.ARGB32)
                { antiAliasing = 4, filterMode = FilterMode.Bilinear }, Generated + "/Vehicle View.renderTexture");
            var camera = BuildStage(texture);
            new ConsoleLayout(font, catalog).Build(rig, camera, texture);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException($"场景保存失败：{ScenePath}");

            var previous = EditorBuildSettings.scenes.Where(item => item.path != ScenePath).ToArray();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(previous).ToArray();
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            Debug.Log("AutoTwin 演示场景和临时车辆已生成。");
        }

        [MenuItem("AutoTwin/刷新所选车辆绑定")]
        private static void RefreshSelectedVehicle()
        {
            var rig = Selection.activeGameObject?.GetComponent<VehicleRig>();
            if (rig == null) throw new InvalidOperationException("请选择含 VehicleRig 组件的车辆根对象。");
            var parts = rig.GetComponentsInChildren<VehiclePart>(true);
            if (!rig.Catalog.ValidateBindings(parts, out string error)) throw new InvalidOperationException(error);

            foreach (var part in parts)
            {
                Undo.RecordObject(part, "Refresh AutoTwin geometry");
                part.RefreshGeometry();
                foreach (var collider in part.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.GetComponentInParent<VehiclePart>(true) != part) continue;
                    Undo.RecordObject(collider.gameObject, "Set AutoTwin picking layer");
                    collider.gameObject.layer = VehicleRig.PickingLayer;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider.gameObject);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                EditorUtility.SetDirty(part);
            }

            Undo.RecordObject(rig, "Refresh AutoTwin vehicle");
            rig.Configure(rig.Catalog, parts);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            EditorUtility.SetDirty(rig);
            Debug.Log($"已刷新 {parts.Length} 个车辆部件的绑定。", rig);
        }

        private static TMP_FontAsset GetFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (source == null) throw new InvalidOperationException("缺少 AutoTwin 中文字体文件。");
            // 录入当前控制台字形，避免依赖 Windows 系统字体；后续可更换同一 TMP 资产的源字体。
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 40, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
                font.name = "AutoTwin Sans SDF";
                AssetDatabase.CreateAsset(font, FontAssetPath);
                font.material.name = "AutoTwin Sans SDF Material";
                font.atlasTexture.name = "AutoTwin Sans SDF Atlas";
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            }

            // 预烘焙 UI、配置及运行时状态文本用到的字符，模板注释里的额外字符可留在源字体中。
            var characters = Enumerable.Range(32, 95).Select(value => (char)value).Concat("°·×，。；：、（）未启用")
                .Concat(new[] { "ConsoleLayout.cs", "DemoCatalog.cs", "../Runtime/ConsoleView.cs" }
                    .SelectMany(path => File.ReadAllText(Path.Combine(Root, "Editor", path)))
                    .Where(character => character >= 0x3400 && character <= 0x9FFF));
            string text = new string(characters.Distinct().OrderBy(character => character).ToArray());
            font.ReadFontAssetDefinition();
            string pending = new string(text.Where(character => !font.HasCharacter(character)).ToArray());
            if (pending.Length > 0 && !font.TryAddCharacters(pending, out string missing))
                throw new InvalidOperationException($"字体缺少字形：{missing}");
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.atlasTexture);
            return font;
        }

        private static void EnsurePickingLayer()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tags.FindProperty("layers").GetArrayElementAtIndex(VehicleRig.PickingLayer);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != "AutoTwinPart")
                throw new InvalidOperationException("图层 8 已占用，请在 VehicleRig 中指定一个空闲图层。");
            layer.stringValue = "AutoTwinPart";
            tags.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Camera BuildStage(RenderTexture texture)
        {
            var stage = new GameObject("Presentation Stage").transform;
            var display = Node(stage, "Display Backdrop").gameObject.AddComponent<Camera>();
            display.clearFlags = CameraClearFlags.SolidColor;
            display.backgroundColor = ColorOf("090F19");
            display.cullingMask = 0;
            display.depth = -10;

            var camera = Node(stage, "Vehicle Camera").gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorOf("0A131F");
            camera.nearClipPlane = .07f;
            camera.farClipPlane = 75f;
            camera.fieldOfView = 36f;
            camera.depth = -20;
            camera.targetTexture = texture;
            camera.aspect = (float)ConsoleLayout.ViewWidth / ConsoleLayout.ViewHeight;

            var floor = Store(new Material(Shader.Find("AutoTwin/Diagnostic Floor")), Generated + "/Materials/Diagnostic Floor.mat");
            Primitive(stage, "Diagnostic floor", PrimitiveType.Plane, new Vector3(0, -.018f, 0), new Vector3(12, 1, 12), floor);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ColorOf("9BB6D0");
            RenderSettings.reflectionIntensity = .72f;

            Light key = Directional(stage, "Key · cool white", new Vector3(47, 140, 0), "D9EEFF", 1.55f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .86f;
            RenderSettings.sun = key;
            Directional(stage, "Rim · electric blue", new Vector3(26, -42, 0), "73BCFF", 1.7f);
            Directional(stage, "Fill · pale cyan", new Vector3(19, 45, 0), "C0E8FF", .65f);
            return camera;
        }

        private static Light Directional(Transform root, string label, Vector3 euler, string color, float intensity)
        {
            var light = Node(root, label).gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.localRotation = Quaternion.Euler(euler);
            light.color = ColorOf(color);
            light.intensity = intensity;
            return light;
        }
    }
}
