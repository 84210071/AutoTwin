using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using static AutoTwin.Editor.SceneAssets;

namespace AutoTwin.Editor
{
    public static class AutoTwinBuilder
    {
        public const string ScenePath = "Assets/Scenes/AutoTwinMain.unity";
        private const string PrefabPath = Generated + "/EV01 Demo Vehicle.prefab";
        private const string FontPath = Root + "/Art/Fonts/AutoTwin Sans.ttf";
        private const string FontAssetPath = Generated + "/AutoTwin Dashboard SDF.asset";

        [MenuItem("AutoTwin/重建数字孪生演示场景")]
        private static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("重建演示场景",
                "此操作会重新生成 AutoTwinMain 与概念车资源。若已接入正式模型，请先另存场景。", "重建", "取消")) return;
            Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(Generated + "/Materials");
            AssetDatabase.Refresh();
            EnsurePickingLayer();
            var assets = new SceneAssets();
            string catalogPath = Generated + "/EV01 Vehicle Data.asset";
            // 参数资产允许独立编辑；重建演示仍读取本地车型配置。
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(catalogPath);
            if (catalog == null) catalog = Store(DemoCatalog.Create(), catalogPath);
            DemoCatalog.RefreshDemoModelMetrics(catalog);
            var font = GetFont();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var demo = new DemoVehicle(assets).Build(catalog);
            demo.GetComponent<VehicleRig>().ConfigureTwin(
                Store(XRay("719CE3", .335f), Generated + "/Materials/EV01 X-Ray Shell.mat"),
                Store(XRay("6DA2E0", .205f), Generated + "/Materials/EV01 X-Ray Wheels.mat"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(demo, PrefabPath);
            UnityEngine.Object.DestroyImmediate(demo);
            var rig = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<VehicleRig>();

            var texture = Store(new RenderTexture(ConsoleLayout.ViewWidth, ConsoleLayout.ViewHeight, 24, RenderTextureFormat.DefaultHDR)
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
            Debug.Log("AutoTwin 数字孪生场景和概念车已生成。");
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
            // 预烘焙 UI、配置及运行时状态文本用到的字符，代码注释不占用图集。
            var characters = Enumerable.Range(32, 95).Select(value => (char)value).Concat("°·×−±—–→‹›≥，。；：、（）%")
                .Concat(Directory.EnumerateFiles(Root, "*.cs", SearchOption.AllDirectories).SelectMany(File.ReadLines)
                    .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)).SelectMany(line => line)
                    .Where(character => character is >= '\u3400' and <= '\u9fff'));
            string text = new string(characters.Distinct().OrderBy(character => character).ToArray());
            if (font != null)
            {
                font.ReadFontAssetDefinition();
                if (text.All(character => font.HasCharacter(character))) return font;
            }

            // 源字体子集增加字形时，重新烘焙并继续使用场景已绑定的字体、材质和图集资源。
            var rebuilt = TMP_FontAsset.CreateFontAsset(source, 46, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            var newMaterial = rebuilt.material;
            var newAtlas = rebuilt.atlasTexture;
            if (!rebuilt.TryAddCharacters(text, out string missing))
            {
                UnityEngine.Object.DestroyImmediate(newMaterial);
                UnityEngine.Object.DestroyImmediate(newAtlas);
                UnityEngine.Object.DestroyImmediate(rebuilt);
                throw new InvalidOperationException($"字体缺少字形：{missing}");
            }

            rebuilt.name = "AutoTwin Dashboard SDF";
            newMaterial.name = "AutoTwin Dashboard Material";
            newAtlas.name = "AutoTwin Dashboard Atlas";
            if (font == null)
            {
                font = rebuilt;
                AssetDatabase.CreateAsset(font, FontAssetPath);
                AssetDatabase.AddObjectToAsset(newMaterial, font);
                AssetDatabase.AddObjectToAsset(newAtlas, font);
            }
            else
            {
                var atlas = font.atlasTexture;
                var material = font.material;
                EditorUtility.CopySerialized(newAtlas, atlas);
                EditorUtility.CopySerialized(newMaterial, material);
                material.mainTexture = atlas;
                rebuilt.atlasTextures = new[] { atlas };
                rebuilt.material = material;
                EditorUtility.CopySerialized(rebuilt, font);
                UnityEngine.Object.DestroyImmediate(newAtlas);
                UnityEngine.Object.DestroyImmediate(newMaterial);
                UnityEngine.Object.DestroyImmediate(rebuilt);
            }

            font.ReadFontAssetDefinition();
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.atlasTexture);
            EditorUtility.SetDirty(font.material);
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
            display.backgroundColor = ColorOf("121723");
            display.cullingMask = 0;
            display.depth = -10;

            var camera = Node(stage, "Vehicle Camera").gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorOf("121723");
            camera.nearClipPlane = .07f;
            camera.farClipPlane = 75f;
            camera.fieldOfView = 34f;
            camera.depth = -20;
            camera.targetTexture = texture;
            camera.aspect = (float)ConsoleLayout.ViewWidth / ConsoleLayout.ViewHeight;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            AddOpticalVolume(stage);

            var floor = Store(new Material(Shader.Find("AutoTwin/Diagnostic Floor")), Generated + "/Materials/Diagnostic Floor.mat");
            Primitive(stage, "Diagnostic floor", PrimitiveType.Plane, new Vector3(0, -.018f, 0), new Vector3(12, 1, 12), floor);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ColorOf("939BA8");
            RenderSettings.reflectionIntensity = .63f;

            Light key = Directional(stage, "Key · soft white", new Vector3(51, 141, 0), "E6EDFA", 1.42f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .86f;
            RenderSettings.sun = key;
            Directional(stage, "Rim · cool white", new Vector3(31, -40, 0), "A3B9DC", .87f);
            Directional(stage, "Fill · pale cyan", new Vector3(23, 37, 0), "D1E5FF", .64f);
            return camera;
        }

        private static void AddOpticalVolume(Transform stage)
        {
            string path = Generated + "/Vehicle Optical Volume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(false);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }

            // 若干高亮的动力部件提供淡淡的光晕，UI 由另一台相机和覆盖画布绘制。
            bloom.threshold.Override(1.20f);
            bloom.intensity.Override(.16f);
            bloom.scatter.Override(.48f);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);
            var volume = Node(stage, "Vehicle optical effects").gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static Material XRay(string color, float density)
        {
            var material = new Material(Shader.Find("AutoTwin/Vehicle X-Ray"));
            material.SetColor("_BaseColor", ColorOf(color));
            material.SetFloat("_Opacity", density);
            return material;
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
