using System.IO;
using UnityEditor;

public class GeneratedRequestProcessor : AssetPostprocessor
{
    const string SkeletonRequest = "Assets/_RPG/Generated/SkeletonEnemySetup.generate";
    const string HouseColliderRequest = "Assets/_RPG/Generated/HouseColliderSetup.generate";
    const string NpcSnapRequest = "Assets/_RPG/Generated/SnapNPCsToTerrain.generate";
    const string SpawnGrassRequest = "Assets/_RPG/Generated/SpawnGrass.generate";
    const string AnimalSetupRequest = "Assets/_RPG/Generated/AnimalSetup.generate";
    const string NpcDiagnosticsRequest = "Assets/_RPG/Generated/NpcDiagnostics.generate";
    const string MaterialScanRequest = "Assets/_RPG/Generated/MaterialScan.generate";
    const string FullMaterialScanRequest = "Assets/_RPG/Generated/FullMaterialScan.generate";
    const string HouseDecorationRequest = "Assets/_RPG/Generated/HouseDecoration.generate";
    const string CastleSetupRequest = "Assets/_RPG/Generated/CastleSetup.generate";
    const string ChestSetupRequest = "Assets/_RPG/Generated/ChestSetup.generate";
    const string KingGoblinSwordCraftingRequest = "Assets/_RPG/Generated/KingGoblinSwordCrafting.generate";
    const string TreeAnomalySpawnerRequest = "Assets/_RPG/Generated/TreeAnomalySpawnerSetup.generate";
    const string GoldCoinSetupRequest = "Assets/_RPG/Generated/GoldCoinSetup.generate";
    const string CrabBossSetupRequest = "Assets/_RPG/Generated/CrabDemonBossSetup.generate";
    const string TreeAnomalySetupRequest = "Assets/_RPG/Generated/TreeAnomalySetup.generate";
    const string WaterWorksRiverSetupRequest = "Assets/_RPG/Generated/WaterWorksRiverSetup.generate";
    const string SuperiorTreeAnomalySetupRequest = "Assets/_RPG/Generated/SuperiorTreeAnomalySetup.generate";
    const string SuperiorTreeAnomalySpawnerRequest = "Assets/_RPG/Generated/SuperiorTreeAnomalySpawnerSetup.generate";
    const string BridgeExtraColliderCleanupRequest = "Assets/_RPG/Generated/BridgeExtraColliderCleanup.generate";

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string asset in importedAssets)
        {
            if (asset == SkeletonRequest || asset == HouseColliderRequest || asset == NpcSnapRequest || asset == SpawnGrassRequest || asset == AnimalSetupRequest || asset == NpcDiagnosticsRequest || asset == ChestSetupRequest || asset == MaterialScanRequest || asset == FullMaterialScanRequest || asset == HouseDecorationRequest || asset == CastleSetupRequest || asset == KingGoblinSwordCraftingRequest || asset == TreeAnomalySpawnerRequest || asset == GoldCoinSetupRequest || asset == CrabBossSetupRequest || asset == TreeAnomalySetupRequest || asset == WaterWorksRiverSetupRequest || asset == SuperiorTreeAnomalySetupRequest || asset == SuperiorTreeAnomalySpawnerRequest || asset == BridgeExtraColliderCleanupRequest)
            {
                EditorApplication.delayCall += ProcessRequests;
                return;
            }
        }
    }

    [InitializeOnLoadMethod]
    static void RunExistingRequests()
    {
        EditorApplication.delayCall += ProcessRequests;
    }

    static void ProcessRequests()
    {
        if (!HasAnyRequest())
            return;

        // Read-only, safe to run even mid-Play-Mode - needed to inspect runtime-only objects
        // (e.g. materials built at runtime) that don't exist once you exit Play Mode.
        if (Consume(FullMaterialScanRequest))
            RpgDiagnostics.DumpAllBrokenMaterials();

        if (!HasAnyRequest())
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ProcessAfterPlayMode;
            EditorApplication.playModeStateChanged += ProcessAfterPlayMode;
            return;
        }

        if (Consume(SkeletonRequest))
            SkeletonEnemySetup.Setup();
        if (Consume(HouseColliderRequest))
            HouseColliderSetup.AddHouseColliders();
        if (Consume(NpcSnapRequest))
            SnapNPCsToTerrainSetup.Snap();
        if (Consume(SpawnGrassRequest))
            SpawnGrassGenerator.Generate();
        if (Consume(AnimalSetupRequest))
            AnimalSetup.Setup();
        if (Consume(NpcDiagnosticsRequest))
            RpgDiagnostics.DumpNpcInfo();
        if (Consume(ChestSetupRequest))
            ChestSetup.Setup();
        if (Consume(MaterialScanRequest))
            RpgDiagnostics.DumpBrokenMaterialsNearNpcs();
        if (Consume(HouseDecorationRequest))
            HouseDecorationSetup.Setup();
        if (Consume(CastleSetupRequest))
            CastleSetup.Setup();
        if (Consume(KingGoblinSwordCraftingRequest))
            KingGoblinSwordCraftingSetup.Setup();
        if (Consume(TreeAnomalySpawnerRequest))
            TreeAnomalySpawnerSetup.Setup();
        if (Consume(GoldCoinSetupRequest))
            GoldCoinSetup.Setup();
        if (Consume(CrabBossSetupRequest))
            CrabDemonBossSetup.Setup();
        if (Consume(TreeAnomalySetupRequest))
            TreeAnomalySetup.Setup();
        if (Consume(WaterWorksRiverSetupRequest))
            WaterWorksRiverSetup.Setup();
        if (Consume(SuperiorTreeAnomalySetupRequest))
            SuperiorTreeAnomalySetup.Setup();
        if (Consume(SuperiorTreeAnomalySpawnerRequest))
            SuperiorTreeAnomalySpawnerSetup.Setup();
        if (Consume(BridgeExtraColliderCleanupRequest))
            BridgeExtraColliderCleanup.RemoveExtraBridgeColliders();
    }

    static bool HasAnyRequest()
    {
        return File.Exists(Path.GetFullPath(SkeletonRequest)) ||
            File.Exists(Path.GetFullPath(HouseColliderRequest)) ||
            File.Exists(Path.GetFullPath(NpcSnapRequest)) ||
            File.Exists(Path.GetFullPath(SpawnGrassRequest)) ||
            File.Exists(Path.GetFullPath(AnimalSetupRequest)) ||
            File.Exists(Path.GetFullPath(NpcDiagnosticsRequest)) ||
            File.Exists(Path.GetFullPath(ChestSetupRequest)) ||
            File.Exists(Path.GetFullPath(MaterialScanRequest)) ||
            File.Exists(Path.GetFullPath(FullMaterialScanRequest)) ||
            File.Exists(Path.GetFullPath(HouseDecorationRequest)) ||
            File.Exists(Path.GetFullPath(CastleSetupRequest)) ||
            File.Exists(Path.GetFullPath(KingGoblinSwordCraftingRequest)) ||
            File.Exists(Path.GetFullPath(TreeAnomalySpawnerRequest)) ||
            File.Exists(Path.GetFullPath(GoldCoinSetupRequest)) ||
            File.Exists(Path.GetFullPath(CrabBossSetupRequest)) ||
            File.Exists(Path.GetFullPath(TreeAnomalySetupRequest)) ||
            File.Exists(Path.GetFullPath(WaterWorksRiverSetupRequest)) ||
            File.Exists(Path.GetFullPath(SuperiorTreeAnomalySetupRequest)) ||
            File.Exists(Path.GetFullPath(SuperiorTreeAnomalySpawnerRequest)) ||
            File.Exists(Path.GetFullPath(BridgeExtraColliderCleanupRequest));
    }

    static void ProcessAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= ProcessAfterPlayMode;
        EditorApplication.delayCall += ProcessRequests;
    }

    static bool Consume(string path)
    {
        string absolutePath = Path.GetFullPath(path);
        if (!File.Exists(absolutePath))
            return false;

        File.Delete(absolutePath);
        string metaPath = absolutePath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        return true;
    }
}
