using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TempleEditionSetup
{
    const string ScenePath = "Assets/_RPG/Scenes/SpawnVillage.unity";
    const string RequestPath = "Assets/_RPG/Generated/TempleEditionSetup.generate";
    const string PrefabRoot = "Assets/(P&W)Temple_Edition/Prefabs";
    const string NatureRoot = "Assets/Proxy Games/Stylized Nature Kit Lite";
    const string FantasyNatureRoot = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Nature";
    const string TexturePath = "Assets/(P&W)Temple_Edition/Textures/Walls_1.png";
    const string NormalPath = "Assets/(P&W)Temple_Edition/Textures/Walls_1(Bumped).png";
    const string MaterialPath = "Assets/(P&W)Temple_Edition/FBX/Materials/Walls_1.mat";
    const string TempleName = "TempleEdition_AncientTemple_FarFromSpawn";

    static readonly HashSet<string> usedPrefabs = new HashSet<string>();
    static Material templeMaterial;

    struct TempleSlot
    {
        public readonly Vector3 position;
        public readonly float yaw;

        public TempleSlot(Vector3 position, float yaw)
        {
            this.position = position;
            this.yaw = yaw;
        }
    }

    [MenuItem("RPG/World/Build Temple Edition Temple")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TempleEditionSetup] Sali de Play Mode para construir el templo.");
            return;
        }

        EditorSceneUtility.OpenSceneSafely(ScenePath);
        GameObject existing = GameObject.Find(TempleName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        usedPrefabs.Clear();
        GameObject root = new GameObject(TempleName);
        root.transform.position = FindTempleOrigin();
        root.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
        templeMaterial = EnsureTempleMaterial();

        BuildReferenceImageTemple(root.transform);
        AddTempleLighting(root.transform);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TempleEditionSetup] Templo armado lejos del spawn con " + root.transform.childCount + " objetos principales/grupos y colliders.");
    }

    [InitializeOnLoadMethod]
    static void RunRequestedSetup()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        string absoluteRequestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RequestPath));
        if (!File.Exists(absoluteRequestPath))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }

        File.Delete(absoluteRequestPath);
        string metaPath = absoluteRequestPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Setup();
    }

    static void BuildReferenceImageTemple(Transform root)
    {
        Transform temple = Group(root, "Reference_Image_Monumental_Temple");

        BuildReferenceFoundations(temple);
        BuildReferenceGrandApproach(temple);
        BuildReferenceSideCourts(temple);
        BuildReferenceTwinPavilions(temple);
        BuildReferenceCentralSanctuary(temple);
        BuildReferenceInteriorPassages(temple);
        BuildReferenceOuterWallsAndDetails(temple);
        BuildReferenceValleyEnvironment(temple);
    }

    static void BuildReferenceFoundations(Transform parent)
    {
        for (float z = -72f; z <= 44f; z += 8f)
        {
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_A.prefab", parent, new Vector3(0f, 0f, z), 0f, 1f, "Central_Stone_Axis_" + z, 0f);
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(-8f, 0f, z), 0f, 1f, "Left_Central_Axis_" + z, 0f);
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(8f, 0f, z), 0f, 1f, "Right_Central_Axis_" + z, 0f);
        }

        for (float x = -56f; x <= 56f; x += 8f)
        {
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(x, 0f, -64f), 0f, 1f, "Lower_Front_Terrace_" + x, 0f);
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(x, 0f, -48f), 0f, 1f, "Middle_Front_Terrace_" + x, 0.55f);
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_A.prefab", parent, new Vector3(x, 0f, -24f), 0f, 1f, "Upper_Broad_Terrace_" + x, 1.25f);
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_A.prefab", parent, new Vector3(x, 0f, 8f), 0f, 1f, "Inner_Courtyard_Terrace_" + x, 1.75f);
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(x, 0f, 36f), 0f, 1f, "Rear_Temple_Terrace_" + x, 2.2f);
        }

        for (float z = -56f; z <= 32f; z += 8f)
        {
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_A.prefab", parent, new Vector3(-56f, 0f, z), 0f, 1f, "Left_Connected_Walkway_" + z, 1.1f);
            PlaceLevel("Basic/Platforms/8x8x.25_Floor_A.prefab", parent, new Vector3(56f, 0f, z), 0f, 1f, "Right_Connected_Walkway_" + z, 1.1f);
        }
    }

    static void BuildReferenceGrandApproach(Transform parent)
    {
        PlaceLevel("Unique/Structures/Temple_Stairs_Entrance_1A.prefab", parent, new Vector3(0f, 0f, -82f), 0f, 1.55f, "Massive_Front_Stairs_Lower", 0f);
        PlaceLevel("Basic/Platforms/Stairs_Platform_2A.prefab", parent, new Vector3(0f, 0f, -62f), 0f, 1.45f, "Massive_Front_Stairs_Upper", 0.25f);
        PlaceLevel("Basic/Platforms/Basic_Stairs_1A.prefab", parent, new Vector3(-18f, 0f, -58f), 0f, 1.05f, "Left_Front_Stairs", 0.35f);
        PlaceLevel("Basic/Platforms/Basic_Stairs_1B.prefab", parent, new Vector3(18f, 0f, -58f), 0f, 1.05f, "Right_Front_Stairs", 0.35f);
        PlaceLevel("Basic/Platforms/Stairs_Platform_1A.prefab", parent, new Vector3(0f, 0f, -34f), 0f, 1.2f, "Central_Inner_Stairs", 1f);

        for (float x = -48f; x <= 48f; x += 16f)
        {
            PlaceLevel("High_Detailed/Railings/HD_Railing_4A.prefab", parent, new Vector3(x, 0f, -54f), 0f, 1f, "Front_Railing_" + x, 1.05f);
            PlaceLevel("High_Detailed/Small_Objects/Bowl_1A.prefab", parent, new Vector3(x, 0f, -73f), 0f, 0.9f, "Front_Bowl_" + x, 0.35f);
        }

        for (float z = -72f; z <= -32f; z += 10f)
        {
            PlaceLevel("High_Detailed/Railings/HD_Railing_Post_2A.prefab", parent, new Vector3(-10f, 0f, z), 0f, 1f, "Left_Stair_Post_" + z, 0.6f);
            PlaceLevel("High_Detailed/Railings/HD_Railing_Post_2B.prefab", parent, new Vector3(10f, 0f, z), 0f, 1f, "Right_Stair_Post_" + z, 0.6f);
        }
    }

    static void BuildReferenceSideCourts(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            string prefix = side < 0 ? "Left" : "Right";
            float xCenter = side * 38f;

            for (float x = xCenter - 8f; x <= xCenter + 8f; x += 8f)
                for (float z = -32f; z <= -8f; z += 8f)
                    PlaceLevel("Basic/Platforms/6x6x.25_Floor_B.prefab", parent, new Vector3(x, 0f, z), 0f, 1f, prefix + "_Sunken_Court_Floor_" + x + "_" + z, 0.25f);

            PlaceLevel("High_Detailed/Archs/HD_Tunnel_Ramp_2B.prefab", parent, new Vector3(side * 26f, 0f, -24f), side < 0 ? 90f : -90f, 1f, prefix + "_Ramp_To_Court", 0.55f);
            PlaceLevel("High_Detailed/Rounded_Walls/HD_Rounded_Wall_4A.prefab", parent, new Vector3(side * 50f, 0f, -28f), side < 0 ? 90f : -90f, 1f, prefix + "_Curved_Outer_Court_Wall_A", 0.65f);
            PlaceLevel("High_Detailed/Rounded_Walls/HD_Rounded_Wall_4B.prefab", parent, new Vector3(side * 50f, 0f, -12f), side < 0 ? 90f : -90f, 1f, prefix + "_Curved_Outer_Court_Wall_B", 0.65f);
            PlaceLevel("Group_Walls/Barrier_Group_1A.prefab", parent, new Vector3(side * 33f, 0f, -38f), 0f, 1.1f, prefix + "_Lower_Court_Front_Barrier", 0.75f);
            PlaceLevel("Group_Walls/Barrier_Group_1B.prefab", parent, new Vector3(side * 33f, 0f, 0f), 180f, 1.1f, prefix + "_Lower_Court_Rear_Barrier", 0.85f);
        }
    }

    static void BuildReferenceTwinPavilions(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            string prefix = side < 0 ? "Left" : "Right";
            float xCenter = side * 42f;

            for (float x = xCenter - 8f; x <= xCenter + 8f; x += 8f)
                for (float z = 8f; z <= 32f; z += 8f)
                    PlaceLevel("High_Detailed/Platforms/HD_Platform_2A.prefab", parent, new Vector3(x, 0f, z), 0f, 0.96f, prefix + "_Pavilion_Floor_" + x + "_" + z, 2.15f);

            PlaceLevel("Unique/Structures/Entrance_1A.prefab", parent, new Vector3(xCenter, 0f, 0f), 0f, 1f, prefix + "_Pavilion_Front_Entrance", 2.1f);
            PlaceLevel("Unique/Structures/Entrance_1B.prefab", parent, new Vector3(xCenter, 0f, 38f), 180f, 1f, prefix + "_Pavilion_Rear_Entrance", 2.1f);

            for (float z = 8f; z <= 32f; z += 8f)
            {
                PlaceLevel("High_Detailed/Walls/HD_Wall_4A.prefab", parent, new Vector3(xCenter - side * 15f, 0f, z), side < 0 ? 90f : -90f, 1f, prefix + "_Pavilion_Outer_Wall_" + z, 2.1f);
                PlaceLevel("High_Detailed/Walls/HD_Wall_2A.prefab", parent, new Vector3(xCenter + side * 15f, 0f, z), side < 0 ? -90f : 90f, 1f, prefix + "_Pavilion_Inner_Wall_" + z, 2.1f);
            }

            for (float x = xCenter - 12f; x <= xCenter + 12f; x += 8f)
            {
                PlaceLevel("Basic/Walls/Basic_Pillar_1C.prefab", parent, new Vector3(x, 0f, 2f), 0f, 1.2f, prefix + "_Pavilion_Front_Pillar_" + x, 2.1f);
                PlaceLevel("Basic/Walls/Basic_Pillar_1C.prefab", parent, new Vector3(x, 0f, 34f), 0f, 1.2f, prefix + "_Pavilion_Rear_Pillar_" + x, 2.1f);
            }

            PlaceLevel("Basic/Beams/Beam_6A.prefab", parent, new Vector3(xCenter, 0f, 16f), 0f, 1.15f, prefix + "_Pavilion_Roof_Beam_A", 7.8f);
            PlaceLevel("Basic/Beams/Beam_7A.prefab", parent, new Vector3(xCenter, 0f, 26f), 0f, 1.15f, prefix + "_Pavilion_Roof_Beam_B", 7.8f);
            PlaceLevel("Unique/Unique_Stack_A3.prefab", parent, new Vector3(side * 65f, 0f, 16f), side < 0 ? 25f : -25f, 1f, prefix + "_Outer_Ruin_Stack_A", 1.4f);
            PlaceLevel("Unique/Unique_Stack_B3.prefab", parent, new Vector3(side * 62f, 0f, 35f), side < 0 ? -25f : 25f, 1f, prefix + "_Outer_Ruin_Stack_B", 1.4f);
        }
    }

    static void BuildReferenceCentralSanctuary(Transform parent)
    {
        for (float x = -16f; x <= 16f; x += 8f)
            for (float z = 4f; z <= 36f; z += 8f)
                PlaceLevel("High_Detailed/Platforms/HD_Platform_1A.prefab", parent, new Vector3(x, 0f, z), 0f, 1f, "Sanctuary_Raised_Floor_" + x + "_" + z, 2.3f);

        PlaceLevel("Basic/Platforms/Block_Platform_3A.prefab", parent, new Vector3(0f, 0f, 18f), 0f, 1.25f, "Central_Altar_Dais", 2.55f);
        PlaceLevel("Unique/Unique_Platform_1A.prefab", parent, new Vector3(0f, 0f, 23f), 0f, 1.05f, "Central_Ritual_Platform", 2.8f);
        PlaceLevel("Unique/Unique_Full_Railing_1.prefab", parent, new Vector3(0f, 0f, 18f), 0f, 1.08f, "Central_Sanctuary_Railing", 2.9f);
        PlaceLevel("High_Detailed/Small_Objects/Bowl_1A.prefab", parent, new Vector3(0f, 0f, 18f), 0f, 1.2f, "Central_Offering_Bowl", 3.1f);

        PlaceLevel("Unique/Structures/Entrance_3A.prefab", parent, new Vector3(0f, 0f, 2f), 0f, 1.15f, "Main_Sanctuary_Front_Portal", 2.25f);
        PlaceLevel("High_Detailed/Archs/HD_Arch_2A.prefab", parent, new Vector3(-10f, 0f, 8f), 0f, 1f, "Sanctuary_Left_Arch", 2.35f);
        PlaceLevel("High_Detailed/Archs/HD_Arch_2B.prefab", parent, new Vector3(10f, 0f, 8f), 0f, 1f, "Sanctuary_Right_Arch", 2.35f);

        for (float z = 4f; z <= 36f; z += 8f)
        {
            PlaceLevel("Basic/Walls/Basic_Pillar_1C.prefab", parent, new Vector3(-18f, 0f, z), 0f, 1.25f, "Left_Central_Column_" + z, 2.35f);
            PlaceLevel("Basic/Walls/Basic_Pillar_1C.prefab", parent, new Vector3(18f, 0f, z), 0f, 1.25f, "Right_Central_Column_" + z, 2.35f);
        }

        PlaceLevel("Unique/Structures/Entrance_3C.prefab", parent, new Vector3(0f, 0f, 52f), 180f, 1.2f, "Rear_High_Ceremonial_Gate", 2.35f);
        PlaceLevel("High_Detailed/Archs/HD_Arch_1A.prefab", parent, new Vector3(0f, 0f, 58f), 180f, 1.1f, "Rear_Top_Arch", 5.6f);
        PlaceLevel("Basic/Beams/Beam_4A.prefab", parent, new Vector3(0f, 0f, 44f), 0f, 1.25f, "Sanctuary_Roof_Beam_A", 7.2f);
        PlaceLevel("Basic/Beams/Beam_5A.prefab", parent, new Vector3(0f, 0f, 32f), 0f, 1.25f, "Sanctuary_Roof_Beam_B", 7.2f);
    }

    static void BuildReferenceOuterWallsAndDetails(Transform parent)
    {
        for (float x = -64f; x <= 64f; x += 8f)
        {
            PlaceLevel("High_Detailed/Railings/HD_Railing_3A.prefab", parent, new Vector3(x, 0f, -47f), 0f, 1f, "Lower_Terrace_Front_Railing_" + x, 1.4f);
            PlaceLevel("High_Detailed/Walls/HD_Wall_3A.prefab", parent, new Vector3(x, 0f, 46f), 180f, 1f, "Rear_Back_Wall_" + x, 2.25f);
        }

        for (float z = -40f; z <= 44f; z += 8f)
        {
            PlaceLevel("High_Detailed/Walls/HD_Wall_4B.prefab", parent, new Vector3(-68f, 0f, z), 90f, 1f, "Left_Outer_Backbone_Wall_" + z, 1.75f);
            PlaceLevel("High_Detailed/Walls/HD_Wall_4C.prefab", parent, new Vector3(68f, 0f, z), -90f, 1f, "Right_Outer_Backbone_Wall_" + z, 1.75f);
        }

        PlaceLevel("Unique/Single_Group_Tall_Corner_1A.prefab", parent, new Vector3(-68f, 0f, -48f), 45f, 1f, "Left_Front_Tall_Corner", 1.25f);
        PlaceLevel("Unique/Single_Group_Tall_Corner_1A.prefab", parent, new Vector3(68f, 0f, -48f), -45f, 1f, "Right_Front_Tall_Corner", 1.25f);
        PlaceLevel("Unique/Unique_Wall_Tall_1A.prefab", parent, new Vector3(-66f, 0f, 48f), 135f, 1f, "Left_Rear_Tall_Wall", 2.1f);
        PlaceLevel("Unique/Unique_Wall_Tall_1A.prefab", parent, new Vector3(66f, 0f, 48f), -135f, 1f, "Right_Rear_Tall_Wall", 2.1f);

        for (float z = -30f; z <= 34f; z += 16f)
        {
            PlaceLevel("High_Detailed/Small_Objects/Torch_Post_1A.prefab", parent, new Vector3(-24f, 0f, z), 0f, 1f, "Left_Central_Torch_" + z, 2.1f);
            PlaceLevel("High_Detailed/Small_Objects/Torch_Post_1B.prefab", parent, new Vector3(24f, 0f, z), 0f, 1f, "Right_Central_Torch_" + z, 2.1f);
        }
    }

    static void BuildReferenceInteriorPassages(Transform parent)
    {
        Transform passages = Group(parent, "Vaulted_Interior_Passages_And_Tunnels");

        for (float z = -16f; z <= 28f; z += 12f)
        {
            PlaceLevel("High_Detailed/Archs/HD_Tunnel_1A.prefab", passages, new Vector3(-14f, 0f, z), 0f, 1f, "Left_Vaulted_Passage_" + z, 1.65f);
            PlaceLevel("High_Detailed/Archs/HD_Tunnel_1B.prefab", passages, new Vector3(14f, 0f, z), 0f, 1f, "Right_Vaulted_Passage_" + z, 1.65f);
        }

        for (float z = -10f; z <= 34f; z += 11f)
        {
            PlaceLevel("High_Detailed/Archs/HD_Tunnel_Arch_1A.prefab", passages, new Vector3(0f, 0f, z), 0f, 1f, "Central_Processional_Arch_" + z, 2f);
            PlaceLevel("Basic/Platforms/6x6x.25_Floor_A.prefab", passages, new Vector3(0f, 0f, z + 4f), 0f, 1f, "Vaulted_Path_Floor_" + z, 1.85f);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            string prefix = side < 0 ? "Left" : "Right";
            PlaceLevel("High_Detailed/Archs/HD_Tunnel_Ramp_3B.prefab", passages, new Vector3(side * 34f, 0f, -4f), side < 0 ? 90f : -90f, 1f, prefix + "_Covered_Side_Ramp", 1.25f);
            PlaceLevel("High_Detailed/Archs/HD_Tunnel_1C.prefab", passages, new Vector3(side * 42f, 0f, 12f), side < 0 ? 90f : -90f, 1f, prefix + "_Side_Covered_Tunnel_A", 1.55f);
            PlaceLevel("High_Detailed/Archs/HD_Tunnel_1D.prefab", passages, new Vector3(side * 42f, 0f, 28f), side < 0 ? 90f : -90f, 1f, prefix + "_Side_Covered_Tunnel_B", 1.55f);
        }
    }

    static void BuildReferenceValleyEnvironment(Transform parent)
    {
        Transform environment = Group(parent, "Temple_Valley_Trees_Cliffs_And_Ruins");

        PlaceSceneAsset(NatureRoot + "/Meshes/Foliage/Trees/Spruce 1.fbx", environment, new Vector3(-30f, 0f, -18f), 18f, 4.4f, "Huge_Left_Front_Tree", 0f, false);
        PlaceSceneAsset(NatureRoot + "/Meshes/Foliage/Trees/Spruce 2.fbx", environment, new Vector3(30f, 0f, -18f), -18f, 4.2f, "Huge_Right_Front_Tree", 0f, false);
        PlaceSceneAsset(NatureRoot + "/Meshes/Foliage/Trees/Spruce 1.fbx", environment, new Vector3(-58f, 0f, 24f), 70f, 3.2f, "Left_Rear_Canopy_Tree", 0f, false);
        PlaceSceneAsset(NatureRoot + "/Meshes/Foliage/Trees/Spruce 2.fbx", environment, new Vector3(58f, 0f, 24f), -70f, 3.2f, "Right_Rear_Canopy_Tree", 0f, false);
        PlaceSceneAsset(NatureRoot + "/Meshes/Foliage/Trees/Spruce 1.fbx", environment, new Vector3(0f, 0f, 66f), 0f, 2.6f, "Sacred_Tree_Behind_Gate", 0f, false);

        for (int side = -1; side <= 1; side += 2)
        {
            string prefix = side < 0 ? "Left" : "Right";
            float x = side * 92f;
            PlaceSceneAsset(NatureRoot + "/Meshes/Rocks/Rock Cliffs/Rock Cliff 1.fbx", environment, new Vector3(x, 0f, -24f), side < 0 ? 78f : -78f, 5.5f, prefix + "_Near_Cliff_Wall", -2f, true);
            PlaceSceneAsset(NatureRoot + "/Meshes/Rocks/Rock Cliffs/Rock Cliff 3.fbx", environment, new Vector3(x, 0f, 24f), side < 0 ? 92f : -92f, 5.2f, prefix + "_Rear_Cliff_Wall", -2f, true);
            PlaceSceneAsset(NatureRoot + "/Meshes/Rocks/Mountain/Mountain.fbx", environment, new Vector3(side * 116f, 0f, 38f), side < 0 ? 55f : -55f, 4.8f, prefix + "_Distant_Mountain_Ridge", -4f, true);

            for (float z = -46f; z <= 42f; z += 22f)
            {
                PlaceSceneAsset(NatureRoot + "/Meshes/Foliage/Bush/Bush.fbx", environment, new Vector3(side * 76f, 0f, z), side * 25f, 1.8f, prefix + "_Wall_Bush_" + z, 0f, false);
                PlaceSceneAsset(FantasyNatureRoot + "/Rocks/Rock + Moss/RockMoss.002.prefab", environment, new Vector3(side * 72f, 0f, z + 8f), side * -15f, 1.4f, prefix + "_Mossy_Rock_" + z, 0f, true);
            }
        }

        for (float x = -64f; x <= 64f; x += 16f)
        {
            PlaceSceneAsset(FantasyNatureRoot + "/Grass/Grass.001.prefab", environment, new Vector3(x, 0f, -86f), 0f, 1.3f, "Front_Cobble_Edge_Grass_" + x, 0f, false);
            PlaceSceneAsset(FantasyNatureRoot + "/Plants/Plant.003.prefab", environment, new Vector3(x + 6f, 0f, 52f), 0f, 1.1f, "Rear_Ruin_Plant_" + x, 0f, false);
        }
    }

    static void PlaceLevel(string relativePath, Transform parent, Vector3 localPosition, float yaw, float scale, string name, float yOffset)
    {
        PlaceAssetPath(PrefabRoot + "/" + relativePath, parent, localPosition, yaw, scale, name, yOffset);
    }

    static GameObject PlaceSceneAsset(string assetPath, Transform parent, Vector3 localPosition, float yaw, float scale, string name, float yOffset, bool addColliders)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogWarning("[TempleEditionSetup] No se encontro asset de entorno: " + assetPath);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.localScale = Vector3.one * scale;
        MoveBoundsCenterToLocal(instance, parent, localPosition);
        SnapBottomToGround(instance);
        if (Mathf.Abs(yOffset) > 0.001f)
            instance.transform.position += Vector3.up * yOffset;
        if (addColliders)
            EnsureColliders(instance);
        MarkStatic(instance);
        return instance;
    }

    static void BuildMainSanctuary(Transform root)
    {
        Transform sanctuary = Group(root, "Main_Sanctuary");

        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
                Place("Basic/Platforms/8x8x.25_Floor_A.prefab", sanctuary, new Vector3(x * 8f, 0f, z * 8f), 0f, 1f, "StoneFloor_" + x + "_" + z);

        Place("Basic/Platforms/Block_Platform_3A.prefab", sanctuary, new Vector3(0f, 0f, 0f), 0f, 1.15f, "Central_Raised_Dais");
        Place("Basic/Platforms/Stairs_Platform_2A.prefab", sanctuary, new Vector3(0f, 0f, -16f), 0f, 1.2f, "Grand_Front_Stairs");
        Place("Basic/Platforms/Stairs_Platform_1B.prefab", sanctuary, new Vector3(-10f, 0f, -12f), 0f, 1f, "Left_Approach_Stairs");
        Place("Basic/Platforms/Stairs_Platform_1A.prefab", sanctuary, new Vector3(10f, 0f, -12f), 0f, 1f, "Right_Approach_Stairs");

        Place("Unique/Structures/Entrance_3A.prefab", sanctuary, new Vector3(0f, 0f, -18f), 0f, 1f, "Monumental_Entrance");
        Place("Unique/Structures/Entrance_2A.prefab", sanctuary, new Vector3(0f, 0f, 18f), 180f, 1f, "Rear_Ritual_Entrance");
        Place("Unique/Structures/Temple_Stairs_Entrance_1A.prefab", sanctuary, new Vector3(0f, 0f, -25f), 0f, 1f, "Temple_Stairs_Entrance");
        Place("High_Detailed/Archs/HD_Arch_2A.prefab", sanctuary, new Vector3(-8f, 0f, -16f), 0f, 1f, "Left_Front_Arch");
        Place("High_Detailed/Archs/HD_Arch_2B.prefab", sanctuary, new Vector3(8f, 0f, -16f), 0f, 1f, "Right_Front_Arch");
        Place("High_Detailed/Archs/HD_Tunnel_Arch_1A.prefab", sanctuary, new Vector3(0f, 0f, 10f), 180f, 1f, "Inner_Tunnel_Arch");

        for (int x = -16; x <= 16; x += 8)
        {
            Place("High_Detailed/Walls/HD_Wall_4A.prefab", sanctuary, new Vector3(x, 0f, 20f), 180f, 1f, "Back_Wall_" + x);
            Place("High_Detailed/Walls/HD_Wall_3A.prefab", sanctuary, new Vector3(x, 0f, -20f), 0f, 1f, "Front_Fragment_Wall_" + x);
        }

        for (int z = -12; z <= 12; z += 8)
        {
            Place("High_Detailed/Walls/HD_Wall_4B.prefab", sanctuary, new Vector3(-20f, 0f, z), 90f, 1f, "Left_Wall_" + z);
            Place("High_Detailed/Walls/HD_Wall_4C.prefab", sanctuary, new Vector3(20f, 0f, z), -90f, 1f, "Right_Wall_" + z);
        }

        Vector3[] pillarPositions =
        {
            new Vector3(-14f, 0f, -12f), new Vector3(14f, 0f, -12f),
            new Vector3(-14f, 0f, 0f), new Vector3(14f, 0f, 0f),
            new Vector3(-14f, 0f, 12f), new Vector3(14f, 0f, 12f)
        };
        for (int i = 0; i < pillarPositions.Length; i++)
            Place("Basic/Walls/Basic_Pillar_1C.prefab", sanctuary, pillarPositions[i], 0f, 1.2f, "Column_Row_" + i);

        Place("Unique/Unique_Platform_1A.prefab", sanctuary, new Vector3(0f, 0f, 5f), 0f, 1f, "Ritual_Platform");
        Place("Unique/Unique_Full_Railing_1.prefab", sanctuary, new Vector3(0f, 0f, 4f), 0f, 1f, "Central_Railing");
        Place("High_Detailed/Small_Objects/Bowl_1A.prefab", sanctuary, new Vector3(0f, 0f, 2f), 0f, 1.2f, "Offering_Bowl");
        Place("High_Detailed/Small_Objects/Torch_Post_1A.prefab", sanctuary, new Vector3(-6f, 0f, -8f), 0f, 1f, "Torch_Left_Front");
        Place("High_Detailed/Small_Objects/Torch_Post_1B.prefab", sanctuary, new Vector3(6f, 0f, -8f), 0f, 1f, "Torch_Right_Front");
        Place("High_Detailed/Small_Objects/Torch_Post_1C.prefab", sanctuary, new Vector3(-6f, 0f, 10f), 180f, 1f, "Torch_Left_Rear");
        Place("High_Detailed/Small_Objects/Post_1A.prefab", sanctuary, new Vector3(6f, 0f, 10f), 180f, 1f, "Carved_Post_Rear");
    }

    static void BuildOuterRuins(Transform root)
    {
        Transform ruins = Group(root, "Outer_Ruins_And_Processional_Path");

        for (int i = -2; i <= 2; i++)
        {
            Place("Basic/Platforms/6x6x.25_Floor_A.prefab", ruins, new Vector3(i * 6f, 0f, -42f), 0f, 1f, "Processional_Floor_A_" + i);
            Place("Basic/Platforms/6x6x.25_Floor_B.prefab", ruins, new Vector3(i * 6f, 0f, 34f), 0f, 1f, "Rear_Courtyard_Floor_" + i);
        }

        Place("High_Detailed/Archs/HD_Tunnel_1A.prefab", ruins, new Vector3(0f, 0f, -50f), 0f, 1f, "Outer_Tunnel_Gate");
        Place("High_Detailed/Archs/HD_Tunnel_Ramp_1B.prefab", ruins, new Vector3(0f, 0f, -58f), 0f, 1f, "Outer_Ramp");
        Place("Unique/Unique_Tunnel_1A.prefab", ruins, new Vector3(0f, 0f, 42f), 180f, 1f, "Collapsed_Rear_Tunnel");

        for (int i = 0; i < 6; i++)
        {
            float x = -28f + i * 11.2f;
            Place("High_Detailed/Railings/HD_Railing_3A.prefab", ruins, new Vector3(x, 0f, -31f), 0f, 1f, "Front_Processional_Railing_" + i);
            Place("High_Detailed/Barriers/HD_Railing_3B.prefab", ruins, new Vector3(x, 0f, 27f), 180f, 1f, "Rear_Broken_Barrier_" + i);
        }

        Place("Unique/Single_Group_Tall_Corner_1A.prefab", ruins, new Vector3(-30f, 0f, 25f), 45f, 1f, "Left_Tall_Ruin_Corner");
        Place("Unique/Unique_Wall_Tall_1A.prefab", ruins, new Vector3(30f, 0f, 25f), -45f, 1f, "Right_Tall_Ruin_Wall");
        Place("Unique/Unique_Stack_A.prefab", ruins, new Vector3(-26f, 0f, -26f), 20f, 1f, "Left_Fallen_Stone_Stack");
        Place("Unique/Unique_Stack_B.prefab", ruins, new Vector3(26f, 0f, -26f), -20f, 1f, "Right_Fallen_Stone_Stack");
        Place("Unique/Unique_Stack_C.prefab", ruins, new Vector3(-34f, 0f, 0f), 90f, 1f, "Side_Rubble_Stack");
    }

    static void BuildConnectedTempleComplex(Transform root)
    {
        Transform complex = Group(root, "Connected_Temple_Complex_All_Remaining_Pack_Objects");
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
        List<string> remaining = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!usedPrefabs.Contains(path))
                remaining.Add(path);
        }
        remaining.Sort();

        BuildConnectedFoundation(complex);

        List<string> platforms = TakeWhere(remaining, IsPlatform);
        List<string> walls = TakeWhere(remaining, IsWall);
        List<string> arches = TakeWhere(remaining, IsArchOrEntrance);
        List<string> railings = TakeWhere(remaining, IsRailingOrBarrier);
        List<string> beams = TakeWhere(remaining, IsBeam);
        List<string> decor = TakeWhere(remaining, IsDecor);

        List<TempleSlot> floorSlots = BuildGridSlots(-48f, 48f, -40f, 64f, 8f, 0f);
        List<TempleSlot> wallSlots = BuildPerimeterSlots(64f, 74f, 8f);
        List<TempleSlot> railSlots = BuildPerimeterSlots(48f, 58f, 7f);
        List<TempleSlot> archSlots = new List<TempleSlot>
        {
            new TempleSlot(new Vector3(0f, 0f, -74f), 0f),
            new TempleSlot(new Vector3(-24f, 0f, -74f), 0f),
            new TempleSlot(new Vector3(24f, 0f, -74f), 0f),
            new TempleSlot(new Vector3(0f, 0f, 74f), 180f),
            new TempleSlot(new Vector3(-64f, 0f, 0f), 90f),
            new TempleSlot(new Vector3(64f, 0f, 0f), -90f),
            new TempleSlot(new Vector3(-48f, 0f, 42f), 90f),
            new TempleSlot(new Vector3(48f, 0f, 42f), -90f)
        };
        List<TempleSlot> beamSlots = BuildGridSlots(-48f, 48f, -48f, 48f, 16f, 0f);
        List<TempleSlot> decorSlots = BuildGridSlots(-56f, 56f, -62f, 68f, 7f, 0f);

        int floorIndex = 0;
        int wallIndex = 0;
        int railIndex = 0;
        int archIndex = 0;
        int beamIndex = 0;
        int decorIndex = 0;

        PlaceCategorized(platforms, complex, floorSlots, ref floorIndex, 0.9f, 0f);
        PlaceCategorized(walls, complex, wallSlots, ref wallIndex, 0.92f, 0f);
        PlaceCategorized(arches, complex, archSlots, ref archIndex, 0.95f, 0f);
        PlaceCategorized(railings, complex, railSlots, ref railIndex, 0.86f, 0f);
        PlaceCategorized(beams, complex, beamSlots, ref beamIndex, 0.86f, 4.8f);
        PlaceCategorized(decor, complex, decorSlots, ref decorIndex, 0.78f, 0f);
        PlaceCategorized(remaining, complex, decorSlots, ref decorIndex, 0.82f, 0f);
    }

    static void BuildConnectedFoundation(Transform parent)
    {
        for (float x = -56f; x <= 56f; x += 8f)
        {
            Place("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(x, 0f, -56f), 0f, 1f, "Connected_Front_Causeway_" + x);
            Place("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(x, 0f, 56f), 0f, 1f, "Connected_Rear_Cloister_" + x);
        }

        for (float z = -48f; z <= 48f; z += 8f)
        {
            Place("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(-56f, 0f, z), 0f, 1f, "Connected_Left_Cloister_" + z);
            Place("Basic/Platforms/8x8x.25_Floor_B.prefab", parent, new Vector3(56f, 0f, z), 0f, 1f, "Connected_Right_Cloister_" + z);
        }

        for (float z = -56f; z <= 56f; z += 8f)
        {
            Place("Basic/Platforms/6x6x.25_Floor_A.prefab", parent, new Vector3(0f, 0f, z), 0f, 1f, "Connected_Central_Axis_" + z);
            Place("Basic/Platforms/6x6x.25_Floor_B.prefab", parent, new Vector3(-24f, 0f, z), 0f, 1f, "Connected_Left_Aisle_" + z);
            Place("Basic/Platforms/6x6x.25_Floor_B.prefab", parent, new Vector3(24f, 0f, z), 0f, 1f, "Connected_Right_Aisle_" + z);
        }

        for (float x = -48f; x <= 48f; x += 8f)
        {
            Place("Basic/Platforms/6x6x.25_Floor_A.prefab", parent, new Vector3(x, 0f, -32f), 0f, 1f, "Connected_Front_Transept_" + x);
            Place("Basic/Platforms/6x6x.25_Floor_A.prefab", parent, new Vector3(x, 0f, 32f), 0f, 1f, "Connected_Rear_Transept_" + x);
        }
    }

    static void AddTempleLighting(Transform root)
    {
        Vector3[] lights =
        {
            new Vector3(-6f, 3f, -8f), new Vector3(6f, 3f, -8f),
            new Vector3(-6f, 3f, 10f), new Vector3(6f, 3f, 10f),
            new Vector3(0f, 4f, 2f), new Vector3(0f, 3f, -42f)
        };

        for (int i = 0; i < lights.Length; i++)
        {
            GameObject go = new GameObject("Warm_Temple_Light_" + i);
            go.transform.SetParent(root, false);
            go.transform.localPosition = lights[i];
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = i == 4 ? 14f : 9f;
            light.intensity = i == 4 ? 1.45f : 1.05f;
            light.color = new Color(1f, 0.63f, 0.32f);
            light.shadows = LightShadows.Soft;
        }
    }

    static void Place(string relativePath, Transform parent, Vector3 localPosition, float yaw, float scale, string name)
    {
        PlaceAssetPath(PrefabRoot + "/" + relativePath, parent, localPosition, yaw, scale, name, 0f);
    }

    static GameObject PlaceAssetPath(string assetPath, Transform parent, Vector3 localPosition, float yaw, float scale, string name, float yOffset)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning("[TempleEditionSetup] No se encontro prefab: " + assetPath);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.localScale = Vector3.one * scale;
        MoveBoundsCenterToLocal(instance, parent, localPosition);
        SnapBottomToGround(instance);
        if (Mathf.Abs(yOffset) > 0.001f)
            instance.transform.position += Vector3.up * yOffset;
        ApplyTempleMaterial(instance);
        EnsureColliders(instance);
        MarkStatic(instance);
        usedPrefabs.Add(assetPath);
        return instance;
    }

    static Material EnsureTempleMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                        Shader.Find("Universal Render Pipeline/Simple Lit") ??
                        Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else if (shader != null && mat.shader != shader)
        {
            mat.shader = shader;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_WorkflowMode")) mat.SetFloat("_WorkflowMode", 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
        mat.EnableKeyword("_NORMALMAP");
        mat.renderQueue = -1;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    static void ApplyTempleMaterial(GameObject root)
    {
        if (templeMaterial == null)
            templeMaterial = EnsureTempleMaterial();

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = templeMaterial;
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
                materials[i] = templeMaterial;

            renderer.sharedMaterials = materials;
        }
    }

    static List<string> TakeWhere(List<string> source, System.Predicate<string> predicate)
    {
        List<string> result = new List<string>();
        for (int i = source.Count - 1; i >= 0; i--)
        {
            if (!predicate(source[i]))
                continue;

            result.Add(source[i]);
            source.RemoveAt(i);
        }
        result.Sort();
        return result;
    }

    static bool IsPlatform(string path) => path.Contains("/Platforms/");
    static bool IsWall(string path) => path.Contains("/Walls/") || path.Contains("Rounded_Walls") || path.Contains("Group_Walls") || Path.GetFileName(path).Contains("Wall");
    static bool IsArchOrEntrance(string path) => path.Contains("/Archs/") || path.Contains("Entrance") || path.Contains("Tunnel") || Path.GetFileName(path).Contains("Arch");
    static bool IsRailingOrBarrier(string path) => path.Contains("Railing") || path.Contains("Barrier");
    static bool IsBeam(string path) => path.Contains("/Beams/") || Path.GetFileName(path).Contains("Beam");
    static bool IsDecor(string path) => path.Contains("Small_Objects") || path.Contains("Stack") || path.Contains("Post") || path.Contains("Bowl") || path.Contains("Unique_");

    static void PlaceCategorized(List<string> prefabs, Transform parent, List<TempleSlot> slots, ref int slotIndex, float scale, float yOffset)
    {
        if (slots.Count == 0)
            return;

        foreach (string path in prefabs)
        {
            TempleSlot slot = slots[slotIndex % slots.Count];
            slotIndex++;
            PlaceAssetPath(path, parent, slot.position, slot.yaw, scale, "PackPiece_" + Path.GetFileNameWithoutExtension(path), yOffset);
        }
    }

    static List<TempleSlot> BuildGridSlots(float minX, float maxX, float minZ, float maxZ, float spacing, float yaw)
    {
        List<TempleSlot> slots = new List<TempleSlot>();
        for (float z = minZ; z <= maxZ; z += spacing)
            for (float x = minX; x <= maxX; x += spacing)
                slots.Add(new TempleSlot(new Vector3(x, 0f, z), yaw));
        return slots;
    }

    static List<TempleSlot> BuildPerimeterSlots(float halfX, float halfZ, float spacing)
    {
        List<TempleSlot> slots = new List<TempleSlot>();
        for (float x = -halfX; x <= halfX; x += spacing)
            slots.Add(new TempleSlot(new Vector3(x, 0f, -halfZ), 0f));
        for (float z = -halfZ + spacing; z <= halfZ - spacing; z += spacing)
            slots.Add(new TempleSlot(new Vector3(halfX, 0f, z), -90f));
        for (float x = halfX; x >= -halfX; x -= spacing)
            slots.Add(new TempleSlot(new Vector3(x, 0f, halfZ), 180f));
        for (float z = halfZ - spacing; z >= -halfZ + spacing; z -= spacing)
            slots.Add(new TempleSlot(new Vector3(-halfX, 0f, z), 90f));
        return slots;
    }

    static void MoveBoundsCenterToLocal(GameObject instance, Transform root, Vector3 localPosition)
    {
        Vector3 desiredCenter = root.TransformPoint(localPosition);
        Bounds bounds = GetBounds(instance);
        instance.transform.position += desiredCenter - bounds.center;
    }

    static void SnapBottomToGround(GameObject instance)
    {
        Bounds bounds = GetBounds(instance);
        Vector3 sample = bounds.center;
        float groundY = SampleGroundY(sample);
        instance.transform.position += Vector3.up * (groundY - bounds.min.y);
    }

    static void EnsureColliders(GameObject root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            if (filter.GetComponent<Collider>() != null)
                continue;

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }
    }

    static void MarkStatic(GameObject root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
    }

    static Bounds GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static float SampleGroundY(Vector3 worldPosition)
    {
        if (GroundUtility.TryProjectToGround(worldPosition + Vector3.up * 1.5f, null, out Vector3 grounded, 12f, 24f))
            return grounded.y;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;

        if (Physics.Raycast(worldPosition + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return worldPosition.y;
    }

    static Transform Group(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Vector3 FindTempleOrigin()
    {
        Vector3 candidate = new Vector3(360f, 0f, 320f);
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            candidate = origin + new Vector3(size.x * 0.72f, 0f, size.z * 0.66f);
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && Vector3.Distance(new Vector3(candidate.x, player.transform.position.y, candidate.z), player.transform.position) < 220f)
            candidate = player.transform.position + new Vector3(320f, 0f, 260f);

        candidate.y = SampleGroundY(candidate);
        return candidate;
    }
}
