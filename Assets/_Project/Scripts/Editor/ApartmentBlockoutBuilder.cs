using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Radio.EditorTools
{
    /// <summary>One-time, editable primitive blockout from the supplied apartment dimension plan.</summary>
    public static class ApartmentBlockoutBuilder
    {
        public const string RootName = "Apartment_Blockout";
        public const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";
        public const string PrefabPath = "Assets/_Project/Prefabs/Apartment/Apartment_Blockout.prefab";
        public const float Width = 10.35f;
        public const float Depth = 7.45f;
        public const float Height = 2.7f;
        private const string MaterialFolder = "Assets/_Project/Materials/ApartmentBlockout";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        // Plan coordinates: x from exterior west edge, d from exterior north edge, in metres.
        // World origin is the apartment centre; north is +Z and finished floor is Y=0.
        private static Vector3 Plan(float x, float y, float d)
        {
            return new Vector3(x - Width / 2f, y, Depth / 2f - d);
        }

        [MenuItem("Radio/Apartment/Create primitive blockout")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open Apartment in Edit Mode before building.");
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == RootName)
                    throw new InvalidOperationException("Apartment blockout already exists. Edit its primitives directly; no automatic replacement.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("The apartment prefab already exists; refusing to overwrite it.");

            CreateMaterials();
            EnsureFolder("Assets/_Project/Prefabs/Apartment");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create apartment primitive blockout");
            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(root, "Create apartment");
            try
            {
                BuildArchitecture(root.transform);
                BuildStudio(Group(root.transform, "02_RadioStudio_3500mm"));
                BuildBedroom(Group(root.transform, "03_Bedroom_3300x5400mm"));
                BuildLivingRoom(Group(root.transform, "04_LivingRoom_2850mm"));
                BuildBathroom(Group(root.transform, "05_Bathroom"));
                BuildStorage(Group(root.transform, "06_StorageAndHall"));
                var ceiling = Group(root.transform, "07_Ceiling_ENABLE_for_interior");
                Box(ceiling, "Ceiling_2700mm", Width / 2f, 2.78f, Depth / 2f, Width, .16f, Depth, "Plaster");
                ceiling.gameObject.SetActive(false);
                BuildLighting(Group(root.transform, "08_InteriorLighting"));
                var viewpoints = Group(root.transform, "09_Viewpoints");
                Viewpoint(viewpoints, "RadioDesk_EyeLevel", 1.65f, 1.65f, 2.65f, .6f, 1.15f, 1.9f);
                Viewpoint(viewpoints, "Entrance_EyeLevel", 9.85f, 1.65f, 6.5f, 4.8f, 1.5f, 6.5f);
                var north = Group(viewpoints, "North_+Z");
                north.localPosition = Plan(Width / 2f, 0, 0);
                ConfigureOverview(scene);
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save Apartment scene.");
                Selection.activeGameObject = root;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.LookAt(new Vector3(0, .3f, 0), Quaternion.Euler(65, 0, 0), 9, true, true);
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("Apartment blockout built: 10.35 x 7.45 m, wall height 2.70 m. Ceiling disabled for overview.");
            }
            catch
            {
                // Leave created objects inspectable and undoable if a later asset operation fails.
                Undo.CollapseUndoOperations(undoGroup);
                throw;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void CreateMaterials()
        {
            EnsureFolder(MaterialFolder);
            Materials.Clear();
            Material("Plaster", "B8AD96");
            Material("Wood", "745238");
            Material("Floor", "98754F");
            Material("FloorLight", "A8845B");
            Material("FloorDark", "866343");
            Material("Trim", "E0D7BD");
            Material("Dark", "252D2E");
            Material("Metal", "778080");
            Material("Fabric", "53695F");
            Material("Blanket", "895D43");
            Material("Rug", "763D3C");
            Material("RugBorder", "C6A577");
            Material("Tile", "9BAAA4");
            Material("Porcelain", "DDDACC");
            Material("Glass", "ABC6CC");
            Material("Screen", "72AA98", true);
            Material("OnAir", "ED7754", true);
            Material("Paper", "DBCAA3");
        }

        private static void Material(string name, string hex, bool emission = false)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
                material = new Material(shader) { name = name };
                ColorUtility.TryParseHtmlString("#" + hex, out Color color);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", name == "Porcelain" || name == "Glass" ? .4f : .15f);
                if (emission)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * .45f);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            Materials.Add(name, material);
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform Furniture(Transform parent, string name, float x, float d, float yaw = 0)
        {
            var group = Group(parent, name);
            group.localPosition = Plan(x, 0, d);
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            return group;
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 size, string material, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Materials[material];
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Part(Transform parent, string name, float x, float y, float z,
            float w, float h, float d, string material, bool collision = true)
        {
            return Primitive(parent, name, PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(w, h, d), material, collision);
        }

        private static GameObject Box(Transform parent, string name, float x, float y, float d,
            float width, float height, float depth, string material, bool collision = true)
        {
            return Primitive(parent, name, PrimitiveType.Cube, Plan(x, y, d), new Vector3(width, height, depth), material, collision);
        }

        private static GameObject Round(Transform parent, string name, float x, float y, float z,
            float diameter, float height, string material, bool collision = true)
        {
            return Primitive(parent, name, PrimitiveType.Cylinder, new Vector3(x, y, z),
                new Vector3(diameter, height / 2f, diameter), material, collision);
        }

        private static void WallX(Transform p, string name, float a, float b, float d, float t)
        {
            Box(p, name, (a + b) / 2, Height / 2, d, b - a, Height, t, "Plaster");
            Box(p, name + "_SkirtingN", (a + b) / 2, .065f, d - t / 2 - .014f, b - a, .13f, .028f, "Wood", false);
            Box(p, name + "_SkirtingS", (a + b) / 2, .065f, d + t / 2 + .014f, b - a, .13f, .028f, "Wood", false);
        }

        private static void WallZ(Transform p, string name, float a, float b, float x, float t)
        {
            Box(p, name, x, Height / 2, (a + b) / 2, t, Height, b - a, "Plaster");
            Box(p, name + "_SkirtingW", x - t / 2 - .014f, .065f, (a + b) / 2, .028f, .13f, b - a, "Wood", false);
            Box(p, name + "_SkirtingE", x + t / 2 + .014f, .065f, (a + b) / 2, .028f, .13f, b - a, "Wood", false);
        }

        private static void Door(Transform p, string name, float x, float d, float width, bool alongZ, float thickness)
        {
            var frame = Furniture(p, name, x, d, alongZ ? 90 : 0);
            Part(frame, "Lintel", 0, 2.4f, 0, width, .6f, thickness, "Plaster");
            // Trim stays outside the structural opening; the clear width remains the drawn width.
            Part(frame, "Jamb_L", -width / 2 - .025f, 1.05f, 0, .05f, 2.1f, thickness + .05f, "Wood", false);
            Part(frame, "Jamb_R", width / 2 + .025f, 1.05f, 0, .05f, 2.1f, thickness + .05f, "Wood", false);
            Part(frame, "Frame_Top", 0, 2.125f, 0, width + .1f, .05f, thickness + .05f, "Wood", false);
        }

        private static void BuildArchitecture(Transform root)
        {
            var architecture = Group(root, "01_Architecture");
            var floor = Group(architecture, "Floors");
            Box(floor, "Foundation_10350x7450", Width / 2, -.14f, Depth / 2, Width, .24f, Depth, "Dark");
            Box(floor, "FinishedFloor_Y0", Width / 2, -.01f, Depth / 2, Width - .4f, .02f, Depth - .4f, "Floor");
            // Broad board strips are deliberately coarse blockout geometry, with no image textures.
            for (int row = 0; row < 29; row++)
            {
                float start = .2f + row * (7.05f / 29);
                for (int column = 0; column < 6; column++)
                {
                    float left = .2f + column * (9.95f / 6);
                    Box(floor, "Board_" + row + "_" + column, left + 9.95f / 12, -.003f,
                        start + 7.05f / 58, 9.95f / 6 - .008f, .006f, 7.05f / 29 - .005f,
                        (row + column) % 3 == 0 ? "FloorDark" : "FloorLight", false);
                }
            }
            var exterior = Group(architecture, "Exterior_200mm");
            WallZ(exterior, "West", .2f, 7.25f, .1f, .2f);
            WallX(exterior, "South", 0, Width, 7.35f, .2f);
            WallZ(exterior, "East_N", .2f, 6.05f, 10.25f, .2f);
            WallZ(exterior, "East_S", 6.95f, 7.25f, 10.25f, .2f);
            Door(exterior, "Entrance_900x2100", 10.25f, 6.5f, .9f, true, .2f);
            float[] edges = { 0, 1.1f, 2.8f, 4.65f, 6.35f, 7.9f, 9.6f, Width };
            for (int i = 0; i < edges.Length; i += 2)
                WallX(exterior, "North_Pier_" + i, edges[i], edges[i + 1], .1f, .2f);
            Window(exterior, "StudioWindow_1700", 1.95f);
            Window(exterior, "BedroomWindow_1700", 5.5f);
            Window(exterior, "LivingWindow_1700", 8.75f);

            var partitions = Group(architecture, "Partitions_150mm");
            WallZ(partitions, "Studio_Bedroom", .2f, 6.05f, 3.775f, .15f);
            WallZ(partitions, "Studio_SouthStub", 6.95f, 7.25f, 3.775f, .15f);
            Door(partitions, "Studio_900x2100", 3.775f, 6.5f, .9f, true, .15f);
            WallZ(partitions, "Bedroom_Living", .2f, 5.75f, 7.225f, .15f);
            WallX(partitions, "Bedroom_SouthL", 3.85f, 5.05f, 5.675f, .15f);
            WallX(partitions, "Bedroom_SouthR", 5.95f, 7.15f, 5.675f, .15f);
            Door(partitions, "Bedroom_900x2100", 5.5f, 5.675f, .9f, false, .15f);
            WallX(partitions, "Storage_SouthWest_N", .2f, 2.3f, 5.7f, .1f);
            WallZ(partitions, "Storage_SouthWest_E1", 5.75f, 6.2f, 2.25f, .1f);
            WallZ(partitions, "Storage_SouthWest_E2", 7, 7.25f, 2.25f, .1f);
            Door(partitions, "Storage_800x2100", 2.25f, 6.6f, .8f, true, .1f);
            WallX(partitions, "Bathroom_N", 8.3f, 10.15f, 2.775f, .15f);
            WallZ(partitions, "Bathroom_W1", 2.85f, 3.55f, 8.375f, .15f);
            WallZ(partitions, "Bathroom_W2", 4.35f, 4.6f, 8.375f, .15f);
            Door(partitions, "Bathroom_800x2100", 8.375f, 3.95f, .8f, true, .15f);
            WallX(partitions, "Bathroom_S", 8.45f, 10.15f, 4.525f, .15f);
            WallX(partitions, "ServiceNiche_S", 8.3f, 10.15f, 5.675f, .15f);
            WallZ(partitions, "ServiceNiche_W1", 4.6f, 4.7f, 8.375f, .15f);
            WallZ(partitions, "ServiceNiche_W2", 5.5f, 5.6f, 8.375f, .15f);
            Door(partitions, "ServiceNiche_800x2100", 8.375f, 5.1f, .8f, true, .15f);
            Box(partitions, "NorthEast_ServiceChase", 7.6f, 1.35f, .5f, .6f, 2.7f, .6f, "Plaster");
        }

        private static void Window(Transform parent, string name, float x)
        {
            var g = Furniture(parent, name, x, .1f);
            Part(g, "BelowSill", 0, .425f, 0, 1.7f, .85f, .2f, "Plaster");
            Part(g, "AboveWindow", 0, 2.5f, 0, 1.7f, .4f, .2f, "Plaster");
            Part(g, "Sill", 0, .85f, -.06f, 1.82f, .055f, .34f, "Trim");
            Part(g, "Glass_Blockout", 0, 1.575f, 0, 1.61f, 1.36f, .025f, "Glass", false);
            foreach (float offset in new[] { -.825f, 0, .825f })
                Part(g, "Frame_V", offset, 1.575f, -.025f, .05f, 1.45f, .085f, "Trim", false);
            foreach (float y in new[] { .875f, 2.275f })
                Part(g, "Frame_H", 0, y, -.025f, 1.7f, .05f, .085f, "Trim", false);
            var radiator = Furniture(parent, name + "_Radiator", x, .39f);
            for (int i = 0; i < 10; i++)
                Part(radiator, "Section_" + i, -.54f + .12f * i, .4f, 0, .09f, .52f, .16f, "Trim");
            Part(radiator, "FeedPipe", 0, .18f, 0, 1.32f, .045f, .045f, "Metal", false);
        }

        private static void Rug(Transform parent, string name, float x, float d, float w, float depth)
        {
            var g = Furniture(parent, name, x, d);
            Part(g, "Backing", 0, .008f, 0, w, .012f, depth, "Rug", false);
            Part(g, "Border", 0, .016f, 0, w - .08f, .004f, depth - .08f, "RugBorder", false);
            Part(g, "Centre", 0, .02f, 0, w - .2f, .004f, depth - .2f, "Rug", false);
        }

        private static void Table(Transform p, string name, float x, float d, float w, float depth, float h)
        {
            var g = Furniture(p, name, x, d);
            Part(g, "Top", 0, h - .035f, 0, w, .07f, depth, "Wood");
            foreach (float dx in new[] { -w / 2 + .08f, w / 2 - .08f })
                foreach (float dz in new[] { -depth / 2 + .08f, depth / 2 - .08f })
                    Part(g, "Leg", dx, (h - .07f) / 2, dz, .065f, h - .07f, .065f, "Dark");
        }

        private static void Cabinet(Transform p, string name, float x, float d, float w, float depth, float h, float yaw = 0)
        {
            var g = Furniture(p, name, x, d, yaw);
            Part(g, "Carcass", 0, h / 2, 0, w, h, depth, "Wood");
            for (int i = 0; i < 2; i++)
            {
                Part(g, "Door_" + i, (i == 0 ? -1 : 1) * w / 4, h / 2, -depth / 2 - .012f,
                    w / 2 - .015f, h - .06f, .025f, "FloorDark", false);
                Part(g, "Handle_" + i, (i == 0 ? -.035f : .035f), h * .55f, -depth / 2 - .042f,
                    .018f, .11f, .035f, "Metal", false);
            }
        }

        private static void Chair(Transform p, string name, float x, float d, float yaw)
        {
            var g = Furniture(p, name, x, d, yaw);
            Part(g, "Seat", 0, .47f, 0, .5f, .12f, .48f, "Fabric");
            Part(g, "Back", 0, .78f, .21f, .5f, .54f, .1f, "Fabric");
            Round(g, "Pedestal", 0, .22f, 0, .07f, .4f, "Metal");
            Part(g, "Base", 0, .04f, 0, .48f, .06f, .42f, "Dark");
            Part(g, "Arm_L", -.28f, .65f, 0, .045f, .05f, .38f, "Dark");
            Part(g, "Arm_R", .28f, .65f, 0, .045f, .05f, .38f, "Dark");
        }

        private static void BuildStudio(Transform p)
        {
            Rug(p, "StudioRug", 2.05f, 3.02f, 2.15f, 4.5f);
            Table(p, "BroadcastDesk_700x2700", .55f, 1.55f, .7f, 2.7f, .76f);
            Table(p, "Desk_NorthReturn", .95f, .49f, 1.2f, .58f, .76f);
            Chair(p, "PresenterChair", 1.35f, 1.7f, 90);
            Cabinet(p, "RecordsCabinet", .57f, 3.67f, .64f, .65f, .88f, -90);
            Cabinet(p, "EquipmentRack", .5f, 4.42f, .55f, .58f, 1.15f, -90);

            var mixer = Furniture(p, "MixingConsole", .58f, 1.75f, -90);
            Part(mixer, "Chassis", 0, .825f, 0, .73f, .13f, .48f, "Metal");
            for (int channel = 0; channel < 8; channel++)
            {
                float x = -.3f + channel * .085f;
                Part(mixer, "FaderSlot_" + channel, x, .893f, -.09f, .012f, .005f, .16f, "Dark", false);
                Part(mixer, "Fader_" + channel, x, .903f, -.13f + channel % 3 * .03f, .04f, .02f, .025f, "Trim", false);
                for (int row = 0; row < 3; row++)
                    Round(mixer, "Knob_" + channel + "_" + row, x, .905f, .035f + row * .058f, .027f, .025f, "Dark", false);
            }
            var computer = Furniture(p, "CRT_Computer", .6f, .85f, -90);
            Part(computer, "MonitorCase", 0, 1.015f, 0, .43f, .37f, .38f, "Trim");
            Part(computer, "Screen", 0, 1.035f, -.198f, .34f, .25f, .016f, "Screen", false);
            Part(computer, "Stand", 0, .81f, 0, .23f, .1f, .21f, "Trim");
            Part(computer, "Keyboard", 0, .79f, -.31f, .43f, .045f, .15f, "Trim");
            var deck = Furniture(p, "Turntable", .55f, 2.57f);
            Part(deck, "Base", 0, .805f, 0, .49f, .09f, .42f, "Dark");
            Round(deck, "Record", -.035f, .856f, 0, .31f, .012f, "Dark", false);
            Round(deck, "Label", -.035f, .865f, 0, .095f, .005f, "OnAir", false);
            Part(deck, "Tonearm", .18f, .875f, .045f, .014f, .018f, .24f, "Metal", false);
            var mic = Furniture(p, "Microphone", .91f, 1.35f);
            Round(mic, "Base", 0, .78f, 0, .17f, .035f, "Dark");
            Round(mic, "Stem", 0, .99f, 0, .018f, .39f, "Metal", false);
            Part(mic, "Head", .08f, 1.17f, 0, .19f, .075f, .075f, "Dark", false);
            for (int i = 0; i < 2; i++)
            {
                var tape = Furniture(p, "CassetteDeck_" + (i == 0 ? "A" : "B"), .58f, 3.56f + .28f * i, -90);
                Part(tape, "Case", 0, .955f, 0, .38f, .14f, .22f, "Dark");
                Part(tape, "CassetteWindow", -.04f, .96f, -.115f, .17f, .075f, .015f, "Metal", false);
                Part(tape, "Display", .12f, .97f, -.116f, .085f, .04f, .016f, "Screen", false);
                var speaker = Furniture(p, "MonitorSpeaker_" + i, .54f, .34f + i * 2.62f, -90);
                Part(speaker, "Case", 0, 1.025f, 0, .24f, .4f, .23f, "Dark");
                var driver = Round(speaker, "Driver", 0, 1.0f, -.12f, .15f, .025f, "Metal", false);
                driver.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            var radio = Furniture(p, "Receiver_Transmitter", .53f, 4.4f, -90);
            Part(radio, "Receiver", 0, 1.25f, 0, .45f, .2f, .37f, "Dark");
            Part(radio, "FrequencyDisplay", -.06f, 1.27f, -.19f, .22f, .075f, .015f, "Screen", false);
            Part(radio, "TuningKnob", .155f, 1.25f, -.21f, .075f, .075f, .04f, "Metal", false);
            var phone = Furniture(p, "DeskTelephone", 1.25f, .48f);
            Part(phone, "Body", 0, .805f, 0, .23f, .09f, .21f, "Dark");
            Part(phone, "Handset", 0, .875f, .045f, .28f, .06f, .07f, "Dark");
            Box(p, "OnAirSign", .227f, 1.9f, 1.03f, .05f, .22f, .6f, "OnAir", false);
            var armchair = Furniture(p, "GuestArmchair", .9f, 4.97f, -25);
            Part(armchair, "Seat", 0, .36f, 0, .64f, .25f, .62f, "Fabric");
            Part(armchair, "Back", 0, .66f, .28f, .72f, .6f, .16f, "Fabric");
            Part(armchair, "Arm_L", -.36f, .5f, 0, .12f, .4f, .72f, "Fabric");
            Part(armchair, "Arm_R", .36f, .5f, 0, .12f, .4f, .72f, "Fabric");
            var coffee = Furniture(p, "RoundCoffeeTable", 2.05f, 3.65f);
            Round(coffee, "Top", 0, .48f, 0, .58f, .055f, "Wood");
            Round(coffee, "Leg", 0, .24f, 0, .075f, .45f, "Dark");
            Part(coffee, "Notebook", -.07f, .519f, .025f, .13f, .025f, .19f, "Paper", false);
            Round(coffee, "Mug", .14f, .55f, -.06f, .075f, .1f, "Porcelain", false);
        }

        private static void BuildBedroom(Transform p)
        {
            Rug(p, "BedroomRug", 5.65f, 3.05f, 2.25f, 3.75f);
            var bed = Furniture(p, "Bed_1400x2200", 6.33f, 3.05f);
            Part(bed, "Frame", 0, .22f, 0, 1.4f, .34f, 2.2f, "Wood");
            Part(bed, "Mattress", 0, .47f, 0, 1.35f, .2f, 2.08f, "Porcelain");
            Part(bed, "Blanket", 0, .583f, -.27f, 1.36f, .035f, 1.5f, "Blanket", false);
            Part(bed, "Pillow_L", -.34f, .62f, .72f, .54f, .13f, .38f, "Trim", false);
            Part(bed, "Pillow_R", .34f, .62f, .72f, .54f, .13f, .38f, "Trim", false);
            Part(bed, "Headboard", 0, .57f, 1.08f, 1.45f, .94f, .09f, "Wood");
            Cabinet(p, "Sideboard", 4.13f, 2.8f, 2.55f, .5f, .69f, -90);
            // Long console runs along the west wall; television faces the bed.
            var tv = Furniture(p, "Television", 4.16f, 2.8f, -90);
            Part(tv, "Case", 0, .96f, 0, .65f, .51f, .34f, "Dark");
            Part(tv, "Screen", -.04f, .99f, -.176f, .48f, .35f, .015f, "Metal", false);
            Cabinet(p, "BedsideCabinet", 6.78f, 1.38f, .52f, .52f, .62f);
            Cabinet(p, "BedroomWardrobe", 6.78f, 4.85f, .95f, .6f, 2.15f, 90);
            var lamp = Furniture(p, "BedsideLamp", 6.78f, 1.38f);
            Round(lamp, "Stem", 0, .83f, 0, .045f, .42f, "Metal", false);
            Round(lamp, "Shade", 0, 1.02f, 0, .28f, .24f, "Paper", false);
        }

        private static void BuildLivingRoom(Transform p)
        {
            Rug(p, "LivingRug", 8.87f, 1.67f, 1.5f, 1.55f);
            Cabinet(p, "Wardrobe", 7.64f, 1.38f, .9f, .55f, 2.15f, -90);
            Cabinet(p, "ChestOfDrawers", 9.76f, .76f, .8f, .6f, .97f, 90);
            Chair(p, "SpareChair", 9.69f, 2.04f, -32);
            Box(p, "StorageBox", 9.77f, .21f, 2.48f, .45f, .42f, .36f, "Paper");
        }

        private static void BuildBathroom(Transform p)
        {
            Box(p, "TileFloor", 9.3f, .004f, 3.65f, 1.7f, .008f, 1.6f, "Tile", false);
            var bath = Furniture(p, "Bath_1600x700", 9.3f, 3.24f);
            Part(bath, "Bottom", 0, .18f, 0, 1.6f, .2f, .7f, "Porcelain");
            Part(bath, "Rim_N", 0, .44f, .31f, 1.6f, .36f, .08f, "Porcelain");
            Part(bath, "Rim_S", 0, .44f, -.31f, 1.6f, .36f, .08f, "Porcelain");
            Part(bath, "Rim_W", -.755f, .44f, 0, .09f, .36f, .54f, "Porcelain");
            Part(bath, "Rim_E", .755f, .44f, 0, .09f, .36f, .54f, "Porcelain");
            Part(bath, "Tap", .59f, .67f, .22f, .14f, .14f, .08f, "Metal", false);
            var sink = Furniture(p, "WashBasin", 8.79f, 4.27f);
            Round(sink, "Pedestal", 0, .36f, 0, .16f, .68f, "Porcelain");
            Part(sink, "Basin", 0, .79f, 0, .48f, .16f, .3f, "Porcelain");
            Part(sink, "Inset", 0, .875f, 0, .32f, .012f, .2f, "Tile", false);
            var toilet = Furniture(p, "Toilet", 9.83f, 4.07f, -90);
            Round(toilet, "Pedestal", 0, .2f, 0, .28f, .4f, "Porcelain");
            Round(toilet, "Bowl", 0, .43f, -.08f, .39f, .12f, "Porcelain");
            Part(toilet, "Cistern", 0, .66f, .2f, .39f, .42f, .2f, "Porcelain");
        }

        private static void BuildStorage(Transform p)
        {
            Cabinet(p, "PantryFridge", .67f, 6.79f, .66f, .64f, 1.6f, 180);
            Cabinet(p, "PantryShelves", 1.71f, 5.99f, .63f, .42f, 1.45f);
            Cabinet(p, "ServiceCupboard", 9.82f, 5.1f, .75f, .55f, 1.9f, 90);
            Rug(p, "HallRunner", 6.7f, 6.5f, 3.9f, .87f);
            Rug(p, "StudioEntryMat", 3.04f, 6.69f, .72f, .82f);
            Box(p, "EntryMat", 9.52f, .012f, 6.5f, .7f, .024f, .83f, "Fabric", false);
            Cabinet(p, "HallShoeCabinet", 4.41f, 7.04f, .8f, .36f, .68f, 180);
        }

        private static void BuildLighting(Transform p)
        {
            float[,] centres = { { 2.15f, 2.9f }, { 5.45f, 3.05f }, { 8.9f, 1.5f }, { 9.25f, 3.8f }, { 6.5f, 6.5f } };
            for (int i = 0; i < centres.GetLength(0); i++)
            {
                var g = Furniture(p, "WarmRoomLight_" + i, centres[i, 0], centres[i, 1]);
                g.localPosition += Vector3.up * 2.4f;
                var light = g.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1, .83f, .65f);
                light.intensity = .85f;
                light.range = i == 3 ? 2.6f : 4.8f;
                light.shadows = LightShadows.None;
            }
        }

        private static void Viewpoint(Transform p, string name, float x, float y, float d, float tx, float ty, float td)
        {
            var g = Group(p, name);
            g.localPosition = Plan(x, y, d);
            g.localRotation = Quaternion.LookRotation(Plan(tx, ty, td) - g.localPosition);
            var camera = g.gameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 65;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 50;
        }

        [MenuItem("Radio/Apartment/Validate blockout")]
        public static void ValidateMenu()
        {
            Debug.Log(Validate());
        }

        public static string Validate()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open Apartment to validate the blockout.");
            var root = GameObject.Find(RootName);
            if (root == null) throw new InvalidOperationException("Blockout root missing.");
            var errors = new List<string>();
            if (root.transform.position != Vector3.zero || root.transform.localScale != Vector3.one
                || Quaternion.Angle(root.transform.rotation, Quaternion.identity) > .001f)
                errors.Add("Root must have identity transform at world origin.");
            var foundation = root.transform.Find("01_Architecture/Floors/Foundation_10350x7450");
            var size = foundation.GetComponent<Renderer>().bounds.size;
            if (Mathf.Abs(size.x - Width) > .001f || Mathf.Abs(size.z - Depth) > .001f)
                errors.Add("Footprint differs from 10.35 x 7.45 m.");
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    errors.Add("Missing script: " + transform.name);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterial == null || !AssetDatabase.Contains(renderer.sharedMaterial)
                    || renderer.sharedMaterial.shader == null || !renderer.sharedMaterial.shader.isSupported)
                    errors.Add("Invalid material: " + renderer.name);

            // A 0.5 m diameter, 1.8 m tall capsule samples connected routes every <= 10 cm.
            // This checks the actual colliders, not just the intended floorplan coordinates.
            float[][] paths = {
                new[] {10.55f, 6.5f, 3.05f, 6.5f},
                new[] {3.05f, 6.5f, 3.05f, 1.4f},
                new[] {3.05f, 6.6f, 1.65f, 6.6f},
                new[] {5.5f, 6.5f, 5.5f, 5.05f},
                new[] {5.5f, 5.05f, 5.1f, 5.05f},
                new[] {5.1f, 5.05f, 5.1f, 1.15f},
                new[] {7.8f, 6.5f, 7.8f, 2.15f},
                new[] {7.8f, 3.85f, 9.15f, 3.85f},
                new[] {7.8f, 5.1f, 9.25f, 5.1f},
                new[] {7.8f, 2.15f, 8.75f, 2.15f}
            };
            Physics.SyncTransforms();
            int samples = 0;
            foreach (var path in paths)
            {
                var start = Plan(path[0], 0, path[1]);
                var end = Plan(path[2], 0, path[3]);
                int steps = Mathf.CeilToInt(Vector3.Distance(start, end) / .1f);
                for (int i = 0; i <= steps; i++)
                {
                    var position = Vector3.Lerp(start, end, (float)i / steps);
                    foreach (var hit in Physics.OverlapCapsule(position + Vector3.up * .3f,
                        position + Vector3.up * 1.6f, .25f, ~0, QueryTriggerInteraction.Ignore))
                        errors.Add("Blocked route at " + position.ToString("F2") + ": " + hit.name);
                    // The first 0.2 m of the entrance route is outside the apartment floor.
                    if (position.x <= Width / 2 - .01f && !Physics.Raycast(position + Vector3.up * .2f,
                        Vector3.down, .4f, ~0, QueryTriggerInteraction.Ignore))
                        errors.Add("Missing floor at " + position.ToString("F2"));
                    samples++;
                }
            }
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            return "PASS: identity root, 10.35 x 7.45 m footprint, persistent materials, no missing scripts; "
                + samples + " capsule/floor samples across 10 connected routes passed (0.50 m diameter, 1.80 m height).";
        }

        private static void ConfigureOverview(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var camera = go.GetComponent<Camera>();
                if (camera != null && go.CompareTag("MainCamera"))
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform }, "Frame apartment overview");
                    camera.transform.position = new Vector3(0, 16, 0);
                    camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                    camera.orthographic = true;
                    camera.orthographicSize = 4.65f;
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 50;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.115f, .135f, .15f);
                }
            }
        }
    }
}
