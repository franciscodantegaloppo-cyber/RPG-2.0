using UnityEditor;

public static class StoneWoodBridgeSetupRefreshTrigger
{
    [InitializeOnLoadMethod]
    static void Touch()
    {
        _ = typeof(StoneWoodBridgeSetup).Name;
        _ = 1;
        _ = 2;
        _ = 3;
    }
}
