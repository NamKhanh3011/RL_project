using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// One-click procedural scene builder that generates the EXACT 3D room, furniture,
/// vacuum robot with ray sensors, dirt spawner, materials, and HUD from the reference image.
/// Menu: ML-Agents > Build Roomba Simulation Scene (One Click)
/// </summary>
public class RoombaSceneBuilder : EditorWindow
{
    private static Material matFloor, matRug, matMat, matWall, matWindow, matCurtain;
    private static Material matDoor, matWhiteFurniture, matSofa, matPillow, matCoffeeTable;
    private static Material matPlantPot1, matPlantPot2, matPlantFoliage1, matPlantFoliage2;
    private static Material matRockingHorse, matRobotBody, matRobotPlate, matRobotRed, matDirt;

    [MenuItem("ML-Agents/Build Roomba Simulation Scene (One Click)")]
    public static void BuildScene()
    {
        // 1. Create or open scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Setup Tags
        EnsureTag("Obstacle");
        EnsureTag("Wall");
        EnsureTag("Dirt");
        EnsureTag("Player");

        // 3. Create Materials (Using the project's active pipeline shader)
        InitMaterials();

        // 4. Create Lighting & Camera
        SetupCameraAndLighting();

        // 5. Build Environment Hierarchy
        GameObject envRoot = new GameObject("RoomEnvironment");
        RoomArea roomArea = envRoot.AddComponent<RoomArea>();
        SimulationHUD hud = envRoot.AddComponent<SimulationHUD>();

        // 6. Build Room Architecture (Floor, Rug, Cutaway Walls, Bay Window, Doors)
        Transform floorT = BuildRoomArchitecture(envRoot.transform);
        roomArea.floorTransform = floorT;

        // 7. Build Furniture (Tables, Chairs, Sofa, Lamp, Shelf, Plants, Rocking Horse)
        BuildFurniture(envRoot.transform);

        // 8. Create Dirt Prefab Template
        GameObject dirtTemplate = CreateDirtTemplate(envRoot.transform);
        roomArea.dirtPrefab = dirtTemplate;

        // 9. Create Roomba Robot Agent
        GameObject robotObj = BuildRoombaAgent(envRoot.transform, roomArea);
        roomArea.agent = robotObj.GetComponent<VacuumAgent>();
        roomArea.hud = hud;

        // 10. Save Scene Asset
        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        string scenePath = "Assets/Scenes/RoombaCleanerScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // Register in EditorBuildSettings
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Exists(s => s.path == scenePath))
        {
            buildScenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        Debug.Log("<color=green><b>[SUCCESS]</b> Roomba Simulation Scene built and saved to " + scenePath + "!</color>");
    }

    private static void InitMaterials()
    {
        // Dynamically get the default shader used by primitives in this project
        // This is 100% guaranteed to be compatible with whatever pipeline is active (Built-in or URP)
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Shader activeShader = tempCube.GetComponent<Renderer>().sharedMaterial.shader;
        DestroyImmediate(tempCube);

        if (activeShader == null || activeShader.name.Contains("Error"))
        {
            activeShader = Shader.Find("Standard");
        }
        if (activeShader == null)
        {
            activeShader = Shader.Find("Universal Render Pipeline/Lit");
        }

        matFloor = MakeMat("Mat_Floor", activeShader, new Color(0.55f, 0.54f, 0.52f)); // Wood grey plank
        matRug = MakeMat("Mat_Rug", activeShader, new Color(0.83f, 0.87f, 0.91f));   // Light blue dotted rug
        matMat = MakeMat("Mat_DoorMat", activeShader, new Color(0.50f, 0.76f, 0.71f)); // Turquoise door mat
        matWall = MakeMat("Mat_Wall", activeShader, new Color(0.48f, 0.70f, 0.60f));  // Mint green pastel wall
        matWindow = MakeMat("Mat_Window", activeShader, new Color(0.75f, 0.88f, 0.95f, 0.6f), true);
        matCurtain = MakeMat("Mat_Curtain", activeShader, new Color(0.38f, 0.58f, 0.48f)); // Olive green curtains
        matDoor = MakeMat("Mat_Door", activeShader, new Color(0.65f, 0.66f, 0.78f)); // Soft purple/lavender door
        matWhiteFurniture = MakeMat("Mat_White", activeShader, new Color(0.92f, 0.94f, 0.95f));
        matSofa = MakeMat("Mat_Sofa", activeShader, new Color(0.95f, 0.95f, 0.93f)); // Cream couch
        matPillow = MakeMat("Mat_Pillow", activeShader, new Color(0.52f, 0.48f, 0.66f)); // Purple pillows
        matCoffeeTable = MakeMat("Mat_CoffeeTable", activeShader, new Color(0.72f, 0.69f, 0.62f));
        matPlantPot1 = MakeMat("Mat_Pot1", activeShader, new Color(0.68f, 0.55f, 0.62f));
        matPlantPot2 = MakeMat("Mat_Pot2", activeShader, new Color(0.85f, 0.65f, 0.70f));
        matPlantFoliage1 = MakeMat("Mat_Foliage1", activeShader, new Color(0.35f, 0.28f, 0.50f)); // Spiky purple plant
        matPlantFoliage2 = MakeMat("Mat_Foliage2", activeShader, new Color(0.25f, 0.55f, 0.85f)); // Blue/teal leafy plant
        matRockingHorse = MakeMat("Mat_RockingHorse", activeShader, new Color(0.80f, 0.82f, 0.84f));
        matRobotBody = MakeMat("Mat_RobotBody", activeShader, new Color(0.18f, 0.20f, 0.22f)); // Dark graphite
        matRobotPlate = MakeMat("Mat_RobotPlate", activeShader, new Color(0.28f, 0.32f, 0.34f));
        matRobotRed = MakeMat("Mat_RobotRed", activeShader, new Color(0.90f, 0.22f, 0.25f)); // Red button
        matDirt = MakeMat("Mat_Dirt", activeShader, new Color(1.0f, 0.82f, 0.20f)); // Golden star dirt
    }

    private static Material MakeMat(string name, Shader shader, Color color, bool transparent = false)
    {
        Material m = new Material(shader);
        m.name = name;
        m.color = color;
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);

        if (transparent)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1);
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3); // Standard shader transparent mode
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
        }

        if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");
        string path = $"Assets/Materials/{name}.mat";
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static void SetupCameraAndLighting()
    {
        // Camera
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0f, 8.8f, -7.2f);
        camObj.transform.rotation = Quaternion.Euler(46f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.42f, 0.66f, 0.86f); // Sky blue background
        cam.fieldOfView = 50f;
        // AudioListener omitted to prevent disabled package warnings

        // Directional Sun
        GameObject dirLight = new GameObject("Directional Light");
        Light l = dirLight.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.15f;
        l.color = new Color(1.0f, 0.98f, 0.93f);
        dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static Transform BuildRoomArchitecture(Transform parent)
    {
        GameObject roomRoot = new GameObject("Architecture");
        roomRoot.transform.parent = parent;

        // Floor (13m x 8m)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.parent = roomRoot.transform;
        floor.transform.localPosition = new Vector3(0f, -0.1f, 0.3f);
        floor.transform.localScale = new Vector3(13.2f, 0.2f, 8.4f);
        floor.GetComponent<Renderer>().sharedMaterial = matFloor;

        // Center Rug
        GameObject rug = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rug.name = "CenterRug";
        rug.transform.parent = roomRoot.transform;
        rug.transform.localPosition = new Vector3(-0.5f, 0.005f, -0.2f);
        rug.transform.localScale = new Vector3(4.5f, 0.01f, 3.6f);
        DestroyImmediate(rug.GetComponent<Collider>());
        rug.GetComponent<Renderer>().sharedMaterial = matRug;

        // Door Mat (Left)
        GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mat.name = "DoorMat";
        mat.transform.parent = roomRoot.transform;
        mat.transform.localPosition = new Vector3(-4.6f, 0.006f, -1.2f);
        mat.transform.localScale = new Vector3(1.4f, 0.01f, 0.9f);
        DestroyImmediate(mat.GetComponent<Collider>());
        mat.GetComponent<Renderer>().sharedMaterial = matMat;

        // Walls (Cutaway style: No front wall, mint green pastel walls)
        CreateWall("Wall_Left", roomRoot.transform, new Vector3(-6.6f, 1.6f, 0.3f), new Vector3(0.3f, 3.2f, 8.4f));
        CreateWall("Wall_Right", roomRoot.transform, new Vector3(6.6f, 1.6f, 0.3f), new Vector3(0.3f, 3.2f, 8.4f));

        // Back Wall Segments (flanking bay window)
        CreateWall("Wall_Back_Left", roomRoot.transform, new Vector3(-4.7f, 1.6f, 4.45f), new Vector3(4.1f, 3.2f, 0.3f));
        CreateWall("Wall_Back_Right", roomRoot.transform, new Vector3(4.7f, 1.6f, 4.45f), new Vector3(4.1f, 3.2f, 0.3f));

        // Bay Window Outdent
        CreateWall("Bay_Angled_Left", roomRoot.transform, new Vector3(-2.2f, 1.6f, 4.95f), new Vector3(0.2f, 3.2f, 1.5f), 45f);
        CreateWall("Bay_Angled_Right", roomRoot.transform, new Vector3(2.2f, 1.6f, 4.95f), new Vector3(0.2f, 3.2f, 1.5f), -45f);
        CreateWall("Bay_Sill", roomRoot.transform, new Vector3(0f, 0.45f, 5.5f), new Vector3(3.2f, 0.9f, 0.2f));

        // Window Glass
        GameObject window = GameObject.CreatePrimitive(PrimitiveType.Cube);
        window.name = "WindowGlass";
        window.transform.parent = roomRoot.transform;
        window.transform.localPosition = new Vector3(0f, 2.05f, 5.5f);
        window.transform.localScale = new Vector3(3.0f, 2.1f, 0.05f);
        window.tag = "Wall";
        window.GetComponent<Renderer>().sharedMaterial = matWindow;

        // Curtains
        CreateCurtain("Curtain_Left", roomRoot.transform, new Vector3(-1.75f, 1.8f, 4.5f));
        CreateCurtain("Curtain_Right", roomRoot.transform, new Vector3(1.75f, 1.8f, 4.5f));

        // Doors (Left & Right)
        CreateDoor("Door_Left", roomRoot.transform, new Vector3(-6.45f, 1.4f, -1.2f), new Vector3(0.06f, 2.7f, 1.35f));
        CreateDoor("Door_Right", roomRoot.transform, new Vector3(6.45f, 1.4f, 2.1f), new Vector3(0.06f, 2.7f, 1.35f));

        // Front Cutaway Wall (Visible low border matching the reference photo)
        CreateWall("Wall_Front_Cutaway", roomRoot.transform, new Vector3(0f, 0.12f, -3.9f), new Vector3(13.2f, 0.25f, 0.3f));

        // Invisible Barrier across the front so the robot physically cannot escape the room
        GameObject frontBarrier = new GameObject("Front_Invisible_Barrier");
        frontBarrier.tag = "Wall";
        frontBarrier.transform.parent = roomRoot.transform;
        frontBarrier.transform.localPosition = new Vector3(0f, 1.6f, -3.9f);
        BoxCollider bc = frontBarrier.AddComponent<BoxCollider>();
        bc.size = new Vector3(14.0f, 3.2f, 0.4f);

        return floor.transform;
    }

    private static void CreateWall(string name, Transform parent, Vector3 pos, Vector3 scale, float yRot = 0f)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name;
        w.tag = "Wall";
        w.transform.parent = parent;
        w.transform.localPosition = pos;
        w.transform.localScale = scale;
        if (yRot != 0f) w.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        w.GetComponent<Renderer>().sharedMaterial = matWall;
    }

    private static void CreateCurtain(string name, Transform parent, Vector3 pos)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = name;
        c.tag = "Obstacle";
        c.transform.parent = parent;
        c.transform.localPosition = pos;
        c.transform.localScale = new Vector3(0.45f, 2.8f, 0.2f);
        c.GetComponent<Renderer>().sharedMaterial = matCurtain;
    }

    private static void CreateDoor(string name, Transform parent, Vector3 pos, Vector3 scale)
    {
        GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cube);
        d.name = name;
        d.tag = "Wall";
        d.transform.parent = parent;
        d.transform.localPosition = pos;
        d.transform.localScale = scale;
        d.GetComponent<Renderer>().sharedMaterial = matDoor;
    }

    private static void BuildFurniture(Transform parent)
    {
        GameObject furn = new GameObject("Furniture");
        furn.transform.parent = parent;

        // 1. Dining Table & 4 Chairs
        BuildDiningSet(furn.transform, new Vector3(0.8f, 0f, 3.4f));

        // 2. Floor Lamp
        BuildFloorLamp(furn.transform, new Vector3(-2.2f, 0f, 3.9f));

        // 3. Bookshelf
        BuildShelf(furn.transform, new Vector3(-3.4f, 0f, 4.0f));

        // 4. Plants (Widened walkway to eliminate dead-end corner pockets)
        BuildPlant(furn.transform, new Vector3(-5.2f, 0f, 0.8f), matPlantPot1, matPlantFoliage1, true);
        BuildPlant(furn.transform, new Vector3(4.5f, 0f, 3.8f), matPlantPot2, matPlantFoliage2, false);

        // 5. Sofa & Pillows
        BuildSofa(furn.transform, new Vector3(4.5f, 0f, -0.6f));

        // 7. Coffee Table
        BuildCoffeeTable(furn.transform, new Vector3(2.5f, 0f, -0.6f));
    }

    private static void BuildDiningSet(Transform parent, Vector3 pos)
    {
        GameObject root = new GameObject("DiningTableSet");
        root.tag = "Obstacle";
        root.transform.parent = parent;
        root.transform.localPosition = pos;

        // Table Top (2.0 x 0.08 x 1.0)
        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "TableTop";
        top.tag = "Obstacle";
        top.transform.parent = root.transform;
        top.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        top.transform.localScale = new Vector3(2.0f, 0.08f, 1.0f);
        top.GetComponent<Renderer>().sharedMaterial = matWhiteFurniture;

        // Table Legs
        float lx = 0.9f, lz = 0.42f;
        MakeLeg(root.transform, new Vector3(lx, 0.42f, lz), 0.85f);
        MakeLeg(root.transform, new Vector3(-lx, 0.42f, lz), 0.85f);
        MakeLeg(root.transform, new Vector3(lx, 0.42f, -lz), 0.85f);
        MakeLeg(root.transform, new Vector3(-lx, 0.42f, -lz), 0.85f);

        // 4 Chairs
        BuildChair(root.transform, new Vector3(-1.35f, 0f, 0f), 90f);  // Left
        BuildChair(root.transform, new Vector3(1.35f, 0f, 0f), -90f);  // Right
        BuildChair(root.transform, new Vector3(0f, 0f, -0.9f), 0f);    // Front
        BuildChair(root.transform, new Vector3(0.5f, 0f, 0.9f), 180f); // Back
    }

    private static void MakeLeg(Transform parent, Vector3 pos, float height, bool hasCollider = true)
    {
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.name = "Leg";
        leg.tag = "Obstacle";
        leg.transform.parent = parent;
        leg.transform.localPosition = pos;
        leg.transform.localScale = new Vector3(0.06f, height * 0.5f, 0.06f);
        leg.GetComponent<Renderer>().sharedMaterial = matWhiteFurniture;
        if (!hasCollider) DestroyImmediate(leg.GetComponent<Collider>());
    }

    private static void BuildChair(Transform parent, Vector3 localPos, float yRot)
    {
        GameObject chair = new GameObject("Chair");
        chair.tag = "Obstacle";
        chair.transform.parent = parent;
        chair.transform.localPosition = localPos;
        chair.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);

        // Seat
        GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.name = "Seat";
        seat.tag = "Obstacle";
        seat.transform.parent = chair.transform;
        seat.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        seat.transform.localScale = new Vector3(0.48f, 0.05f, 0.48f);
        seat.GetComponent<Renderer>().sharedMaterial = matWhiteFurniture;

        // Backrest
        GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "Backrest";
        back.tag = "Obstacle";
        back.transform.parent = chair.transform;
        back.transform.localPosition = new Vector3(0f, 0.88f, -0.22f);
        back.transform.localScale = new Vector3(0.44f, 0.75f, 0.05f);
        back.GetComponent<Renderer>().sharedMaterial = matWhiteFurniture;

        // 4 Legs (No collision on chair legs so vacuum robot glides freely underneath)
        MakeLeg(chair.transform, new Vector3(0.2f, 0.24f, 0.2f), 0.48f, false);
        MakeLeg(chair.transform, new Vector3(-0.2f, 0.24f, 0.2f), 0.48f, false);
        MakeLeg(chair.transform, new Vector3(0.2f, 0.24f, -0.2f), 0.48f, false);
        MakeLeg(chair.transform, new Vector3(-0.2f, 0.24f, -0.2f), 0.48f, false);
    }

    private static void BuildFloorLamp(Transform parent, Vector3 pos)
    {
        GameObject lamp = new GameObject("FloorLamp");
        lamp.tag = "Obstacle";
        lamp.transform.parent = parent;
        lamp.transform.localPosition = pos;

        // Base & Pole
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.tag = "Obstacle";
        pole.transform.parent = lamp.transform;
        pole.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        pole.transform.localScale = new Vector3(0.08f, 1.1f, 0.08f);
        pole.GetComponent<Renderer>().sharedMaterial = matWhiteFurniture;

        // Shade
        GameObject shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shade.tag = "Obstacle";
        shade.transform.parent = lamp.transform;
        shade.transform.localPosition = new Vector3(0f, 2.15f, 0f);
        shade.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
        shade.GetComponent<Renderer>().sharedMaterial = matWhiteFurniture;

        // Lamp Light
        GameObject lObj = new GameObject("LampLight");
        lObj.transform.parent = lamp.transform;
        lObj.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        Light pl = lObj.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.range = 4.5f;
        pl.intensity = 1.6f;
        pl.color = new Color(1f, 0.96f, 0.85f);
    }

    private static void BuildShelf(Transform parent, Vector3 pos)
    {
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Bookshelf";
        shelf.tag = "Obstacle";
        shelf.transform.parent = parent;
        shelf.transform.localPosition = pos + Vector3.up * 0.9f;
        shelf.transform.localScale = new Vector3(0.75f, 1.8f, 0.4f);
        shelf.GetComponent<Renderer>().sharedMaterial = matWhiteFurniture;
    }

    private static void BuildPlant(Transform parent, Vector3 pos, Material matPot, Material matFoliage, bool spiky)
    {
        GameObject plant = new GameObject("PottedPlant");
        plant.tag = "Obstacle";
        plant.transform.parent = parent;
        plant.transform.localPosition = pos;

        // Pot
        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.tag = "Obstacle";
        pot.transform.parent = plant.transform;
        pot.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        pot.transform.localScale = new Vector3(0.45f, 0.28f, 0.45f);
        pot.GetComponent<Renderer>().sharedMaterial = matPot;

        // Foliage (Visual only - remove colliders so leaves never trap the robot)
        if (spiky)
        {
            GameObject sp1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sp1.tag = "Obstacle";
            sp1.transform.parent = plant.transform;
            sp1.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            sp1.transform.localScale = new Vector3(0.25f, 0.5f, 0.25f);
            sp1.GetComponent<Renderer>().sharedMaterial = matFoliage;
            DestroyImmediate(sp1.GetComponent<Collider>());
        }
        else
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.tag = "Obstacle";
            ball.transform.parent = plant.transform;
            ball.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            ball.transform.localScale = new Vector3(0.5f, 0.65f, 0.5f);
            ball.GetComponent<Renderer>().sharedMaterial = matFoliage;
            DestroyImmediate(ball.GetComponent<Collider>());
        }
    }

    private static void BuildSofa(Transform parent, Vector3 pos)
    {
        GameObject sofa = new GameObject("Sofa");
        sofa.tag = "Obstacle";
        sofa.transform.parent = parent;
        sofa.transform.localPosition = pos;

        // Base & Cushion (3-seater: 2.8m long)
        GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.name = "SeatCushion";
        seat.tag = "Obstacle";
        seat.transform.parent = sofa.transform;
        seat.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        seat.transform.localScale = new Vector3(1.1f, 0.45f, 2.7f);
        seat.GetComponent<Renderer>().sharedMaterial = matSofa;

        // Backrest
        GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "Backrest";
        back.tag = "Obstacle";
        back.transform.parent = sofa.transform;
        back.transform.localPosition = new Vector3(0.48f, 0.85f, 0f);
        back.transform.localScale = new Vector3(0.28f, 0.85f, 2.7f);
        back.GetComponent<Renderer>().sharedMaterial = matSofa;

        // Armrests
        GameObject armL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armL.name = "Armrest_Left";
        armL.tag = "Obstacle";
        armL.transform.parent = sofa.transform;
        armL.transform.localPosition = new Vector3(0.05f, 0.65f, -1.35f);
        armL.transform.localScale = new Vector3(1.15f, 0.45f, 0.28f);
        armL.GetComponent<Renderer>().sharedMaterial = matSofa;

        GameObject armR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armR.name = "Armrest_Right";
        armR.tag = "Obstacle";
        armR.transform.parent = sofa.transform;
        armR.transform.localPosition = new Vector3(0.05f, 0.65f, 1.35f);
        armR.transform.localScale = new Vector3(1.15f, 0.45f, 0.28f);
        armR.GetComponent<Renderer>().sharedMaterial = matSofa;

        // Pillows
        BuildPillow(sofa.transform, new Vector3(0.18f, 0.68f, -1.05f));
        BuildPillow(sofa.transform, new Vector3(0.18f, 0.68f, 1.05f));
    }

    private static void BuildPillow(Transform parent, Vector3 localPos)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = "Pillow";
        p.tag = "Obstacle";
        p.transform.parent = parent;
        p.transform.localPosition = localPos;
        p.transform.localScale = new Vector3(0.28f, 0.3f, 0.3f);
        p.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
        p.GetComponent<Renderer>().sharedMaterial = matPillow;
    }

    private static void BuildCoffeeTable(Transform parent, Vector3 pos)
    {
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "CoffeeTable";
        table.tag = "Obstacle";
        table.transform.parent = parent;
        table.transform.localPosition = pos + Vector3.up * 0.25f;
        table.transform.localScale = new Vector3(0.85f, 0.35f, 1.8f);
        table.GetComponent<Renderer>().sharedMaterial = matCoffeeTable;
    }

    private static GameObject CreateDirtTemplate(Transform parent)
    {
        GameObject template = new GameObject("Dirt_Template");
        template.tag = "Dirt";
        template.transform.parent = parent;
        template.transform.localPosition = new Vector3(0f, -50f, 0f); // Hidden off-screen

        // Star / Diamond visual (Octahedron or cross cube)
        GameObject d1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        d1.name = "Mesh1";
        d1.transform.parent = template.transform;
        d1.transform.localPosition = Vector3.zero;
        d1.transform.localScale = new Vector3(0.12f, 0.22f, 0.05f);
        d1.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        d1.GetComponent<Renderer>().sharedMaterial = matDirt;
        DestroyImmediate(d1.GetComponent<Collider>());

        GameObject d2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        d2.name = "Mesh2";
        d2.transform.parent = template.transform;
        d2.transform.localPosition = Vector3.zero;
        d2.transform.localScale = new Vector3(0.05f, 0.22f, 0.12f);
        d2.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        d2.GetComponent<Renderer>().sharedMaterial = matDirt;
        DestroyImmediate(d2.GetComponent<Collider>());

        // Trigger Collider
        SphereCollider sc = template.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.22f;

        template.AddComponent<DirtItem>();
        return template;
    }

    private static GameObject BuildRoombaAgent(Transform parent, RoomArea area)
    {
        GameObject robot = new GameObject("RoombaAgent");
        robot.tag = "Player";
        robot.transform.parent = parent;
        robot.transform.localPosition = new Vector3(-0.5f, 0.08f, -0.2f); // At center rug

        // Outer circular chassis
        GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chassis.name = "Chassis";
        chassis.transform.parent = robot.transform;
        chassis.transform.localPosition = Vector3.zero;
        chassis.transform.localScale = new Vector3(0.82f, 0.14f, 0.82f);
        chassis.GetComponent<Renderer>().sharedMaterial = matRobotBody;
        DestroyImmediate(chassis.GetComponent<Collider>());

        // Top inner accent plate
        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plate.name = "Plate";
        plate.transform.parent = robot.transform;
        plate.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        plate.transform.localScale = new Vector3(0.68f, 0.14f, 0.68f);
        plate.GetComponent<Renderer>().sharedMaterial = matRobotPlate;
        DestroyImmediate(plate.GetComponent<Collider>());

        // Red Power Indicator
        GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btn.name = "PowerButton";
        btn.transform.parent = robot.transform;
        btn.transform.localPosition = new Vector3(0.18f, 0.11f, 0f);
        btn.transform.localScale = new Vector3(0.09f, 0.05f, 0.09f);
        btn.GetComponent<Renderer>().sharedMaterial = matRobotRed;
        DestroyImmediate(btn.GetComponent<Collider>());

        // Main Physical Collider
        CapsuleCollider cc = robot.AddComponent<CapsuleCollider>();
        cc.radius = 0.42f;
        cc.height = 0.28f;
        cc.direction = 1; // Y-Axis

        // Rigidbody
        Rigidbody rb = robot.AddComponent<Rigidbody>();
        rb.mass = 3.5f;
        rb.linearDamping = 1.0f;
        rb.angularDamping = 5.0f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // ML-Agents Agent
        VacuumAgent agent = robot.AddComponent<VacuumAgent>();
        agent.roomArea = area;
        agent.MaxStep = 12000;

        // Clean up any extra BehaviorParameters that Agent may have auto-added
        BehaviorParameters[] bps = robot.GetComponents<BehaviorParameters>();
        BehaviorParameters bp = bps.Length > 0 ? bps[0] : robot.AddComponent<BehaviorParameters>();
        for (int i = 1; i < bps.Length; i++)
        {
            DestroyImmediate(bps[i]);
        }

        bp.BehaviorName = "RoombaCleaner";
        bp.BrainParameters.VectorObservationSize = 6;
        bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
        bp.BehaviorType = BehaviorType.Default; // Connects to Python training or runs Heuristic fallback

        // Decision Requester
        DecisionRequester dr = robot.AddComponent<DecisionRequester>();
        dr.DecisionPeriod = 5;
        dr.TakeActionsBetweenDecisions = true;

        // ML-Agents RayPerceptionSensor3D
        RayPerceptionSensorComponent3D raySensor = robot.AddComponent<RayPerceptionSensorComponent3D>();
        raySensor.SensorName = "RayPerceptionSensor";
        raySensor.DetectableTags = new List<string> { "Dirt", "Obstacle", "Wall" };
        raySensor.RaysPerDirection = 6;
        raySensor.MaxRayDegrees = 180f;
        raySensor.RayLength = 4.5f;
        raySensor.SphereCastRadius = 0.06f;

        // Runtime Sensor Visualizer (draws green rays & endpoint rings matching reference photo!)
        RoombaSensorsVisualizer viz = robot.AddComponent<RoombaSensorsVisualizer>();
        viz.rayCount = 14;
        viz.maxRayDistance = 4.2f;
        viz.detectionLayers = LayerMask.GetMask("Default");

        return robot;
    }

    private static void EnsureTag(string tag)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        bool exists = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue.Equals(tag))
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }
}
