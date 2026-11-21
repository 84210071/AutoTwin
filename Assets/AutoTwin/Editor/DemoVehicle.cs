using System.Collections.Generic;
using UnityEngine;
using static AutoTwin.Editor.SceneAssets;

namespace AutoTwin.Editor
{
    internal sealed class DemoVehicle
    {
        private readonly SceneAssets assets;
        private readonly Material paint, shadowPaint, glass, trim, tire, alloy, chrome, led, red, amber;
        private const float FrontAxle = 1.47f, RearAxle = -1.47f, WheelRadius = 0.36f;

        public DemoVehicle(SceneAssets assets)
        {
            this.assets = assets;
            paint = assets.Lit("Glacier Blue", "427C9E", .38f, .66f, "06101B");
            shadowPaint = assets.Lit("Deep Blue", "223E53", .25f, .52f, "04090E");
            glass = assets.Lit("Smoked Glass", "1C344B", .47f, .88f, "0A192B");
            trim = assets.Lit("Graphite", "152330", .14f, .42f);
            tire = assets.Lit("Tire Rubber", "151D26", .03f, .35f);
            alloy = assets.Lit("Machined Alloy", "8AABC0", .57f, .56f, "141F2C");
            chrome = assets.Lit("Blue Trim", "668DAB", .52f, .65f, "122237");
            led = assets.Lit("Ice LED", "9DEAFF", .18f, .4f, "38B6EB");
            red = assets.Lit("Tail LED", "FA517E", .15f, .4f, "CB1546");
            amber = assets.Lit("HV Orange", "FFAD52", .26f, .5f, "592706");
        }

        public GameObject Build(VehicleCatalog catalog)
        {
            var root = new GameObject("VehicleRoot");
            var parts = new List<VehiclePart>();

            Add(parts, Body(root.transform), DemoCatalog.Body);
            Add(parts, Hood(root.transform), DemoCatalog.Hood, PartView.Exterior, PartMotion.Hinge, Vector3.right, -52f);
            Add(parts, Trunk(root.transform), DemoCatalog.Trunk);
            for (int i = 0; i < 4; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                bool front = i < 2;
                string suffix = (front ? "F" : "R") + (side < 0 ? "L" : "R");
                Add(parts, Door(root.transform, suffix, side, front), DemoCatalog.DoorIds[i], PartView.Exterior,
                    PartMotion.Hinge, Vector3.up, -side * 62f, front);
                Add(parts, Wheel(root.transform, suffix, side, front), DemoCatalog.WheelIds[i], PartView.Always, PartMotion.Wheel, Vector3.right);
            }

            Add(parts, Battery(root.transform), DemoCatalog.Battery, PartView.Powertrain);
            Add(parts, Motor(root.transform), DemoCatalog.Motor, PartView.Powertrain);
            if (!catalog.ValidateBindings(parts, out string error)) throw new System.InvalidOperationException(error);
            root.AddComponent<VehicleRig>().Configure(catalog, parts.ToArray());
            return root;
        }

        private static void Add(List<VehiclePart> parts, Transform node, string id, PartView view = PartView.Exterior,
            PartMotion motion = PartMotion.None, Vector3 axis = default, float degrees = 0f, bool frontDoor = false)
        {
            var part = node.gameObject.AddComponent<VehiclePart>();
            part.Configure(id, view, motion, axis, degrees, frontDoor);
            parts.Add(part);
        }

        private Transform Body(Transform root)
        {
            var body = Node(root, "Body");
            var sides = new SurfaceMesh();
            var arches = new SurfaceMesh();
            var ends = new SurfaceMesh();
            var shoulders = new SurfaceMesh();

            // 两侧门洞留空，门打开后可以看到地板、座椅及 B 柱。
            float[] segments = { -2.30f, -2.12f, -1.90f, -1.86f, -1.80f, -1.70f, -1.60f, -1.47f, -1.34f, -1.24f, -1.14f,
                -1.08f, -.97f, .88f, 1.05f, 1.14f, 1.24f, 1.34f, 1.47f, 1.60f, 1.70f, 1.80f, 1.86f, 1.90f, 2.12f, 2.30f };
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 outward = Vector3.right * side;
                for (int i = 1; i < segments.Length; i++)
                {
                    float a = segments[i - 1], b = segments[i];
                    bool opening = a >= -.97f && b <= .88f;
                    float topA = opening ? .445f : Shoulder(a), topB = opening ? .445f : Shoulder(b);
                    Vector3 lowA = SidePoint(a, ArchBottom(a), side), lowB = SidePoint(b, ArchBottom(b), side);
                    sides.Quad(lowA, lowB, SidePoint(b, topB, side), SidePoint(a, topA, side), outward);
                    if (lowA.y > .31f && lowB.y > .31f)
                    {
                        var offset = outward * .013f;
                        arches.Quad(lowA + offset, lowB + offset, lowB + offset + Vector3.up * .037f, lowA + offset + Vector3.up * .037f, outward);
                    }
                }

                // 固定翼子板顶部补到机盖边缘，闭合时保留缝隙，打开时仍露出前机舱。
                float[] frontShoulder = { .902f, 1.15f, 1.47f, 1.8f, 2.12f, 2.29f };
                for (int i = 1; i < frontShoulder.Length; i++)
                {
                    float a = frontShoulder[i - 1], b = frontShoulder[i];
                    Vector3 outerA = SidePoint(a, Shoulder(a), side), outerB = SidePoint(b, Shoulder(b), side);
                    shoulders.Quad(outerA, outerB, FrontHoodSeam(b, outerB, side), FrontHoodSeam(a, outerA, side), Vector3.up);
                }

                Box(body, "Rocker " + side, new Vector3(side * .857f, .34f, -.01f), new Vector3(.12f, .075f, 1.91f), trim);
                Box(body, "B pillar " + side, new Vector3(side * .74f, 1.11f, -.12f), new Vector3(.085f, .63f, .063f), trim).localRotation = Quaternion.Euler(0, 0, side * 14f);
                Box(body, "Front Seat " + side, new Vector3(side * .39f, .62f, -.05f), new Vector3(.43f, .16f, .52f), trim);
                Box(body, "Front Backrest " + side, new Vector3(side * .39f, .9f, -.27f), new Vector3(.43f, .61f, .14f), trim).localRotation = Quaternion.Euler(-15f, 0, 0);
            }

            ends.Quad(new(-.77f, .757f, 2.30f), new(.77f, .757f, 2.30f), new(.77f, .782f, 2.282f), new(-.77f, .782f, 2.282f), new(0, 1, 1));

            foreach (int end in new[] { -1, 1 })
            {
                var normal = Vector3.forward * end;
                float z = 2.30f * end;
                ends.Quad(new(-.72f, .35f, z - .08f * end), new(.72f, .35f, z - .08f * end),
                    new(.81f, .57f, z), new(-.81f, .57f, z), normal);
                ends.Quad(new(-.81f, .57f, z), new(.81f, .57f, z), new(.77f, Shoulder(z), z), new(-.77f, Shoulder(z), z), normal);
                Box(body, "Fascia trim " + end, new Vector3(0, .495f, z + .007f * end), new Vector3(1.39f, .118f, .042f), trim);
                Box(body, "Center signature " + end, new Vector3(0, end > 0 ? .691f : .792f, z + .01f * end), new Vector3(1.48f, .014f, .02f), end > 0 ? led : red);
                foreach (int side in new[] { -1, 1 })
                {
                    var lamp = Box(body, $"{end} lamp {side}", new Vector3(side * .66f, end > 0 ? .731f : .814f, z - .024f * end),
                        new Vector3(.34f, .047f, .053f), end > 0 ? led : red);
                    lamp.localRotation = Quaternion.Euler(0, end * side * 15f, 0);
                }
            }

            Surface(body, "Sculpted fenders", sides.Save("Sculpted Fenders"), paint, true);
            Surface(body, "Wheel arch trim", arches.Save("Wheel Arch Trim"), shadowPaint, false);
            Surface(body, "Front and rear bumpers", ends.Save("Bumpers"), paint, true);
            Surface(body, "Fender shoulders", shoulders.Save("Fender Shoulders"), paint, true);
            Box(body, "Cabin floor", new Vector3(0, .39f, -.1f), new Vector3(1.57f, .12f, 2.05f), trim, true);

            var canopy = new SurfaceMesh();
            canopy.Quad(new(-.72f, 1.39f, -.86f), new(.72f, 1.39f, -.86f), new(.58f, 1.48f, .53f), new(-.58f, 1.48f, .53f), Vector3.up);
            Surface(body, "Roof", canopy.Save("Roof"), paint, true);
            var windshield = new SurfaceMesh();
            windshield.Quad(new(-.82f, .999f, .93f), new(.82f, .999f, .93f), new(.58f, 1.48f, .53f), new(-.58f, 1.48f, .53f), new(0, 1, 1));
            Surface(body, "Windshield", windshield.Save("Windshield"), glass, true);
            foreach (int side in new[] { -1, 1 })
            {
                Rail(body, "A pillar " + side, new(side * .807f, .999f, .925f), new(side * .574f, 1.476f, .53f), .044f, chrome);
                Rail(body, "Roof rail " + side, new(side * .58f, 1.48f, .53f), new(side * .72f, 1.39f, -.86f), .044f, chrome);
            }

            return body;
        }

        private Transform Hood(Transform root)
        {
            Vector3 pivot = new(0, 1.00f, .897f);
            var node = Node(root, "Hood", pivot);
            var hood = new SurfaceMesh();
            hood.Quad(new(-.835f, .995f, .902f), new(-.779f, .759f, 2.29f), new(-.531f, .794f, 2.27f), new(-.548f, 1.02f, .906f), Vector3.up);
            hood.Quad(new(-.548f, 1.02f, .906f), new(-.531f, .794f, 2.27f), new(.531f, .794f, 2.27f), new(.548f, 1.02f, .906f), Vector3.up);
            hood.Quad(new(.548f, 1.02f, .906f), new(.531f, .794f, 2.27f), new(.779f, .759f, 2.29f), new(.835f, .995f, .902f), Vector3.up);
            Surface(node, "Hood skin", hood.Save("Hood Skin", pivot), paint, true);
            return node;
        }

        private Transform Trunk(Transform root)
        {
            Vector3 pivot = new(0, 1.39f, -.86f);
            var node = Node(root, "Trunk", pivot);
            var rearGlass = new SurfaceMesh();
            rearGlass.Quad(new(-.712f, 1.383f, -.877f), new(.712f, 1.383f, -.877f), new(.824f, 1.015f, -1.69f), new(-.824f, 1.015f, -1.69f), new(0, 1, -1));
            Surface(node, "Rear glass", rearGlass.Save("Rear Glass", pivot), glass, true);
            var deck = new SurfaceMesh();
            deck.Quad(new(-.834f, 1.01f, -1.697f), new(.834f, 1.01f, -1.697f), new(.779f, .848f, -2.29f), new(-.779f, .848f, -2.29f), Vector3.up);
            Surface(node, "Deck lid", deck.Save("Deck Lid", pivot), paint, true);
            return node;
        }

        private Transform Door(Transform root, string suffix, int side, bool front)
        {
            float a = front ? .868f : -.123f, b = front ? -.11f : -.967f;
            Vector3 pivot = new(side * .895f, .88f, a);
            var node = Node(root, "Door_" + suffix, pivot);
            var outward = Vector3.right * side;
            var skin = new SurfaceMesh();
            Vector3 upperA = SidePoint(a, Shoulder(a) + .005f, side), upperB = SidePoint(b, Shoulder(b) + .005f, side);
            Vector3 midA = new(side * .97f, .633f, a), midB = new(side * .97f, .633f, b);
            skin.Quad(SidePoint(a, .445f, side), SidePoint(b, .445f, side), midB, midA, outward);
            skin.Quad(midA, midB, upperB, upperA, outward);
            Surface(node, "Door skin", skin.Save("Door Skin " + suffix, pivot), paint, true);
            var window = new SurfaceMesh();
            float roofA = front ? .51f : -.132f, roofB = front ? -.109f : -.856f;
            Vector3 glassA = new(side * RoofWidth(roofA), RoofHeight(roofA) - .023f, roofA);
            Vector3 glassB = new(side * RoofWidth(roofB), RoofHeight(roofB) - .023f, roofB);
            window.Quad(upperA, upperB, glassB, glassA, outward);
            Surface(node, "Side window", window.Save("Side Window " + suffix, pivot), glass, true);
            Rail(node, "Window sill", upperA - pivot, upperB - pivot, .023f, chrome);
            Rail(node, "Window frame", upperB - pivot, glassB - pivot, .028f, trim);
            Box(node, "Flush handle", new Vector3(side * .941f, upperB.y - .077f, b + .15f) - pivot, new(.034f, .025f, .164f), chrome);
            if (front)
            {
                Box(node, "Mirror stem", new Vector3(side * .95f, 1.029f, a - .13f) - pivot, new(.18f, .047f, .058f), trim);
                Primitive(node, "Mirror", PrimitiveType.Sphere, new Vector3(side * 1.042f, 1.068f, a - .137f) - pivot, new(.212f, .095f, .159f), paint);
            }

            return node;
        }

        private Transform Wheel(Transform root, string suffix, int side, bool front)
        {
            var node = Node(root, "Wheel_" + suffix, new(side * .841f, WheelRadius, front ? FrontAxle : RearAxle));
            // 圆柱的原生纵轴为 Y。安装后轮轴与父对象的局部 X 轴重合。
            Cylinder(node, "Tire", Vector3.zero, new(.72f, .113f, .72f), tire, true);
            Cylinder(node, "Rim barrel", new(side * .086f, 0, 0), new(.551f, .027f, .551f), chrome);
            Cylinder(node, "Brake disc", new(side * .116f, 0, 0), new(.471f, .008f, .471f), trim);
            for (int i = 0; i < 8; i++)
            {
                var spoke = Node(node, "Spoke " + i);
                spoke.localRotation = Quaternion.Euler(i * 45f, 0, 0);
                Box(spoke, "Machined face", new(side * .132f, .155f, .021f), new(.025f, .20f, .046f), alloy).localRotation = Quaternion.Euler(17, 0, 0);
            }
            Cylinder(node, "Hub", new(side * .14f, 0, 0), new(.13f, .016f, .13f), alloy);
            Cylinder(node, "Hub badge", new(side * .158f, 0, 0), new(.058f, .004f, .058f), led);
            return node;
        }

        private Transform Battery(Transform root)
        {
            var node = Node(root, "Battery", new(0, .31f, -.08f));
            Box(node, "Pack enclosure", Vector3.zero, new(1.52f, .23f, 2.53f), chrome, true);
            Box(node, "Pack gasket", new(0, .119f, 0), new(1.44f, .022f, 2.46f), trim, true);
            for (int row = 0; row < 6; row++)
                for (int column = 0; column < 3; column++)
                {
                    var point = new Vector3((column - 1) * .462f, .151f, (row - 2.5f) * .388f);
                    Box(node, $"Module {row}-{column}", point, new(.427f, .047f, .356f), alloy, true);
                    Box(node, $"Module rail {row}-{column}", point + new Vector3(0, .026f, -.104f), new(.336f, .008f, .024f), led);
                }
            Box(node, "HV connection", new(0, .116f, 1.288f), new(.26f, .12f, .14f), amber, true);
            return node;
        }

        private Transform Motor(Transform root)
        {
            var node = Node(root, "Motor_F", new(0, .496f, FrontAxle));
            Cylinder(node, "Stator", Vector3.zero, new(.50f, .35f, .50f), alloy, true);
            Cylinder(node, "Reduction gear", new(.35f, -.07f, 0), new(.41f, .102f, .41f), shadowPaint, true);
            Box(node, "Inverter", new(-.065f, .253f, -.025f), new(.52f, .152f, .393f), chrome, true);
            for (int i = 0; i < 7; i++)
                Box(node, "Cooling fin " + i, new((i - 3) * .064f - .065f, .338f, -.025f), new(.016f, .025f, .351f), alloy);
            Rail(node, "High voltage lead", new(-.18f, .26f, -.23f), new(-.13f, -.027f, -.41f), .065f, amber);
            return node;
        }

        private static float ArchBottom(float z)
        {
            float axleDistance = Mathf.Min(Mathf.Abs(z - FrontAxle), Mathf.Abs(z - RearAxle));
            return axleDistance < .404f ? .355f + Mathf.Sqrt(.404f * .404f - axleDistance * axleDistance) : .301f;
        }

        private static Vector3 SidePoint(float z, float y, int side)
        {
            float taper = .16f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.67f, 2.32f, Mathf.Abs(z)));
            return new(side * (.966f - taper - .057f * Mathf.Abs(y - .63f) / .4f), y, z);
        }

        private static Vector3 FrontHoodSeam(float z, Vector3 outer, int side)
        {
            float blend = Mathf.InverseLerp(.902f, 2.29f, z);
            float x = Mathf.Min(Mathf.Abs(outer.x) - .002f, Mathf.Lerp(.835f, .779f, blend) + .012f);
            return new(side * x, Mathf.Lerp(.995f, .759f, blend) - .006f, z);
        }

        private static float Shoulder(float z) => z > .88f ? Mathf.Lerp(.997f, .757f, Mathf.InverseLerp(.88f, 2.3f, z))
            : Mathf.Lerp(1.015f, .848f, Mathf.InverseLerp(1.66f, 2.3f, -z));
        private static float RoofWidth(float z) => Mathf.Lerp(.72f, .58f, Mathf.InverseLerp(-.86f, .53f, z));
        private static float RoofHeight(float z) => Mathf.Lerp(1.39f, 1.48f, Mathf.InverseLerp(-.86f, .53f, z));

        private static Transform Box(Transform parent, string name, Vector3 center, Vector3 scale, Material material, bool pick = false) =>
            Primitive(parent, name, PrimitiveType.Cube, center, scale, material, pick);

        private static void Cylinder(Transform parent, string name, Vector3 center, Vector3 scale, Material material, bool pick = false) =>
            Primitive(parent, name, PrimitiveType.Cylinder, center, scale, material, pick).localRotation = Quaternion.Euler(0, 0, 90);

        private static void Rail(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var rail = Box(parent, name, (a + b) / 2, new(thickness, thickness, Vector3.Distance(a, b)), material);
            rail.localRotation = Quaternion.LookRotation(b - a);
        }
    }
}
