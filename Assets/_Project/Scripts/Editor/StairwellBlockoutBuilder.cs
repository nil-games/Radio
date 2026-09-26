using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Radio.EditorTools
{
    /// <summary>
    /// Creates a coarse 1990s-2000s apartment stairwell outside the existing entrance.
    /// The result is intentionally independent from the apartment prefab so it remains
    /// easy to iterate on or remove without touching the interior blockout.
    /// </summary>
    public static class StairwellBlockoutBuilder
    {
        public const string RootName = "Stairwell_Blockout";
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";
        private const string MaterialFolder = "Assets/_Project/Materials/ApartmentBlockout/Stairwell";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("Radio/Apartment/Create stairwell blockout")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open Apartment in Edit Mode before building the stairwell.");
            if (GameObject.Find(RootName) != null)
                throw new InvalidOperationException("Stairwell blockout already exists; edit or delete that root explicitly.");

            Transform apartment = GameObject.Find(ApartmentBlockoutBuilder.RootName)?.transform;
            if (apartment == null)
                throw new InvalidOperationException("Apartment blockout root is missing.");

            Transform entrance = apartment.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Entrance_900x2100");
            if (entrance == null)
                throw new InvalidOperationException("Apartment entrance marker is missing.");

            CreateMaterials();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create stairwell blockout");

            var rootObject = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create stairwell blockout");

            try
            {
                BuildGeometry(rootObject.transform, entrance.position);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = rootObject;

                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();

                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("Stairwell blockout created: landing, neighbouring door, up/down flights and railings.");
            }
            catch
            {
                Undo.CollapseUndoOperations(undoGroup);
                throw;
            }
        }

        private static void BuildGeometry(Transform root, Vector3 entrance)
        {
            const float wallThickness = .2f;
            const float wallBottom = -1.4f;
            // One and a half storeys of clear height keeps the upper half-flight usable
            // while preventing the exterior sky from showing through the entrance.
            const float wallTop = 4.2f;
            const float wallHeight = wallTop - wallBottom;
            const float wallY = (wallTop + wallBottom) * .5f;

            // The apartment entrance faces +X. These bounds follow the user's plan:
            // a landing immediately outside the door and two flights extending south.
            float west = entrance.x + .1f;
            float east = west + 3.4f;
            float north = entrance.z + 1.35f;
            float stairStart = entrance.z - 1.27f;
            float flightEnd = stairStart - 2.7f;
            float south = entrance.z - 4.65f;

            Transform shell = Group(root, "01_Shell");
            Transform floors = Group(root, "02_Landings");
            Transform stairs = Group(root, "03_Stairs");
            Transform details = Group(root, "04_Details");
            Transform lighting = Group(root, "05_Lighting");

            // Main floor landing. The two stair voids start immediately after it.
            Box(floors, "CurrentFloor_Landing", Mid(west, east), -.08f, Mid(north, stairStart),
                east - west, .16f, north - stairStart, "LandingFloor");
            Box(floors, "Landing_Threshold", west + .18f, -.035f, entrance.z,
                .36f, .07f, .92f, "Concrete");

            // Outer shell. Existing apartment walls close the west side around its door;
            // only the return below the apartment corner is added here.
            Wall(shell, "NorthWall", Mid(west, east), wallY, north + wallThickness * .5f,
                east - west + wallThickness * 2f, wallHeight, wallThickness);
            Wall(shell, "SouthWall", Mid(west, east), wallY, south - wallThickness * .5f,
                east - west + wallThickness * 2f, wallHeight, wallThickness);
            Wall(shell, "WestReturn", west - wallThickness * .5f, wallY, Mid(stairStart, south),
                wallThickness, wallHeight, stairStart - south);

            // A second apartment door sits opposite the player's door. The east wall is
            // split so the door reads as a real opening rather than a slab on a wall.
            const float neighbourDoorWidth = .9f;
            const float neighbourDoorHeight = 2.1f;
            float doorNorth = entrance.z + neighbourDoorWidth * .5f;
            float doorSouth = entrance.z - neighbourDoorWidth * .5f;
            Wall(shell, "EastWall_North", east + wallThickness * .5f, wallY, Mid(north, doorNorth),
                wallThickness, wallHeight, north - doorNorth);
            Wall(shell, "EastWall_South", east + wallThickness * .5f, wallY, Mid(doorSouth, south),
                wallThickness, wallHeight, doorSouth - south);
            Wall(shell, "EastWall_DoorLintel", east + wallThickness * .5f,
                neighbourDoorHeight + (wallTop - neighbourDoorHeight) * .5f, entrance.z,
                wallThickness, wallTop - neighbourDoorHeight, neighbourDoorWidth);
            Wall(shell, "EastWall_BelowDoor", east + wallThickness * .5f,
                wallBottom * .5f, entrance.z, wallThickness, -wallBottom, neighbourDoorWidth);

            Box(details, "NeighbourApartmentDoor", east - .035f, neighbourDoorHeight * .5f, entrance.z,
                .07f, neighbourDoorHeight, neighbourDoorWidth - .06f, "Door");
            Box(details, "NeighbourDoorFrame_Top", east - .075f, neighbourDoorHeight + .035f, entrance.z,
                .09f, .07f, neighbourDoorWidth + .12f, "DoorTrim", false);
            Box(details, "NeighbourDoorFrame_N", east - .075f, neighbourDoorHeight * .5f,
                doorNorth + .035f, .09f, neighbourDoorHeight, .07f, "DoorTrim", false);
            Box(details, "NeighbourDoorFrame_S", east - .075f, neighbourDoorHeight * .5f,
                doorSouth - .035f, .09f, neighbourDoorHeight, .07f, "DoorTrim", false);
            Round(details, "NeighbourDoorHandle", east - .13f, 1.02f, entrance.z - .28f,
                .055f, .08f, "Metal", false, Quaternion.Euler(0, 0, 90));

            // Soviet/Russian stairwell-style washable green lower wall band.
            const float panelHeight = 1.12f;
            Box(details, "NorthWall_LowerPaint", Mid(west, east), panelHeight * .5f, north - .011f,
                east - west, panelHeight, .022f, "LowerWall", false);
            Box(details, "SouthWall_LowerPaint", Mid(west, east), panelHeight * .5f, south + .011f,
                east - west, panelHeight, .022f, "LowerWall", false);
            Box(details, "WestReturn_LowerPaint", west + .011f, panelHeight * .5f, Mid(stairStart, south),
                .022f, panelHeight, stairStart - south, "LowerWall", false);
            AddEastWallPaint(details, east - .011f, panelHeight, north, doorNorth, doorSouth, south);

            const int stepCount = 9;
            const float halfStorey = 1.4f;
            const float flightWidth = 1.2f;
            float tread = (stairStart - flightEnd) / stepCount;
            float rise = halfStorey / stepCount;
            float leftCentre = west + .2f + flightWidth * .5f;
            float rightCentre = leftCentre + flightWidth + .2f;
            float lowerBase = -halfStorey - .08f;

            for (int index = 0; index < stepCount; index++)
            {
                float z = stairStart - (index + .5f) * tread;

                float upTop = (index + 1) * rise;
                Box(stairs, $"UpStep_{index + 1:00}", leftCentre, (upTop - .08f) * .5f, z,
                    flightWidth, upTop + .08f, tread + .012f, "Concrete");

                float downTop = -(index + 1) * rise;
                float downHeight = downTop - lowerBase;
                Box(stairs, $"DownStep_{index + 1:00}", rightCentre, lowerBase + downHeight * .5f, z,
                    flightWidth, downHeight, tread + .012f, "Concrete");
            }

            Box(floors, "UpperHalfLanding", leftCentre, halfStorey - .08f, Mid(flightEnd, south),
                flightWidth, .16f, flightEnd - south, "LandingFloor");
            Box(floors, "LowerHalfLanding", rightCentre, -halfStorey - .08f, Mid(flightEnd, south),
                flightWidth, .16f, flightEnd - south, "LandingFloor");

            // Central railings make the direction and separation of both flights clear.
            float upRailX = leftCentre + flightWidth * .5f + .055f;
            float downRailX = rightCentre - flightWidth * .5f - .055f;
            SlopedRail(details, "UpFlight_TopRail",
                new Vector3(upRailX, .9f, stairStart - .05f),
                new Vector3(upRailX, halfStorey + .9f, flightEnd + .05f), .055f);
            SlopedRail(details, "DownFlight_TopRail",
                new Vector3(downRailX, .9f, stairStart - .05f),
                new Vector3(downRailX, -halfStorey + .9f, flightEnd + .05f), .055f);

            for (int index = 0; index <= stepCount; index += 3)
            {
                float ratio = index / (float)stepCount;
                float z = Mathf.Lerp(stairStart - .05f, flightEnd + .05f, ratio);
                float upFloor = Mathf.Lerp(0, halfStorey, ratio);
                float downFloor = Mathf.Lerp(0, -halfStorey, ratio);
                Box(details, $"UpRailPost_{index:00}", upRailX, upFloor + .46f, z,
                    .045f, .92f, .045f, "Metal", false);
                Box(details, $"DownRailPost_{index:00}", downRailX, downFloor + .46f, z,
                    .045f, .92f, .045f, "Metal", false);
            }

            // Coarse period details: electrical panel, exposed conduit and wall lamp.
            Box(details, "ElectricalPanel", east - .095f, 1.45f, entrance.z - 1.05f,
                .13f, .72f, .52f, "Panel", false);
            Box(details, "ElectricalPanel_Door", east - .17f, 1.45f, entrance.z - 1.05f,
                .025f, .65f, .45f, "Metal", false);
            Box(details, "ExposedConduit", east - .14f, 2.15f, entrance.z - 1.05f,
                .035f, .75f, .035f, "Metal", false);

            Box(lighting, "WallLamp_Backplate", east - .13f, 2.38f, entrance.z + .72f,
                .05f, .28f, .48f, "Metal", false);
            Box(lighting, "WallLamp_Diffuser", east - .18f, 2.38f, entrance.z + .72f,
                .08f, .18f, .38f, "Light", false);
            var lightObject = new GameObject("ColdStairwellLight");
            Undo.RegisterCreatedObjectUndo(lightObject, "Create stairwell light");
            lightObject.transform.SetParent(lighting, false);
            lightObject.transform.position = new Vector3(east - .45f, 2.35f, entrance.z + .72f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(.78f, .86f, 1f);
            light.intensity = 1.15f;
            light.range = 5.2f;
            light.shadows = LightShadows.None;

            // Toggle this group off temporarily when an unobstructed top view is needed.
            Transform ceiling = Group(root, "06_Ceiling_TOGGLE_for_top_view");
            Box(ceiling, "StairwellCeiling", Mid(west, east), 4.28f, Mid(north, south),
                east - west + .4f, .16f, north - south + .4f, "Wall");
        }

        private static void AddEastWallPaint(Transform parent, float x, float height,
            float north, float doorNorth, float doorSouth, float south)
        {
            Box(parent, "EastWall_North_LowerPaint", x, height * .5f, Mid(north, doorNorth),
                .022f, height, north - doorNorth, "LowerWall", false);
            Box(parent, "EastWall_South_LowerPaint", x, height * .5f, Mid(doorSouth, south),
                .022f, height, doorSouth - south, "LowerWall", false);
        }

        private static void Wall(Transform parent, string name, float x, float y, float z,
            float width, float height, float depth)
        {
            Box(parent, name, x, y, z, width, height, depth, "Wall");
        }

        private static void SlopedRail(Transform parent, string name, Vector3 start, Vector3 end, float thickness)
        {
            Vector3 direction = end - start;
            GameObject rail = Box(parent, name, 0, 0, 0, thickness, thickness, direction.magnitude, "Metal", false);
            rail.transform.position = (start + end) * .5f;
            rail.transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction.normalized);
        }

        private static Transform Group(Transform parent, string name)
        {
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static GameObject Box(Transform parent, string name, float x, float y, float z,
            float width, float height, float depth, string material, bool collision = true)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(x, y, z);
            gameObject.transform.localScale = new Vector3(width, height, depth);
            gameObject.GetComponent<Renderer>().sharedMaterial = Materials[material];
            if (!collision)
                UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            return gameObject;
        }

        private static GameObject Round(Transform parent, string name, float x, float y, float z,
            float diameter, float height, string material, bool collision, Quaternion rotation)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(x, y, z);
            gameObject.transform.rotation = rotation;
            gameObject.transform.localScale = new Vector3(diameter, height * .5f, diameter);
            gameObject.GetComponent<Renderer>().sharedMaterial = Materials[material];
            if (!collision)
                UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            return gameObject;
        }

        private static float Mid(float first, float second)
        {
            return (first + second) * .5f;
        }

        private static void CreateMaterials()
        {
            EnsureFolder(MaterialFolder);
            Materials.Clear();
            AddMaterial("Wall", "B8B2A5", .08f);
            AddMaterial("LowerWall", "647468", .16f);
            AddMaterial("Concrete", "777570", .06f);
            AddMaterial("LandingFloor", "858276", .1f);
            AddMaterial("Metal", "414B4D", .22f);
            AddMaterial("Panel", "696D69", .12f);
            AddMaterial("Door", "673C2E", .14f);
            AddMaterial("DoorTrim", "3B2924", .18f);
            AddMaterial("Light", "D9E7E8", .3f, true);
        }

        private static void AddMaterial(string name, string hex, float smoothness, bool emission = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    throw new InvalidOperationException("URP Lit shader is missing.");

                material = new Material(shader) { name = name };
                ColorUtility.TryParseHtmlString("#" + hex, out Color color);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                if (emission)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * .45f);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            Materials.Add(name, material);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
