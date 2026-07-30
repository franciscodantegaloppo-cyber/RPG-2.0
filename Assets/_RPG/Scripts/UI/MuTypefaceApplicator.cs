using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the ornate MU-like typeface on every runtime-built screen, including UI created after
// a scene transition. Cinzel is the project's local font asset and remains readable at small
// sizes while preserving the requested medieval MU appearance.
public sealed class MuTypefaceApplicator : MonoBehaviour
{
    static MuTypefaceApplicator instance;
    TMP_FontAsset muFont;
    float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("MU_Typeface");
        instance = root.AddComponent<MuTypefaceApplicator>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        Font source = Resources.Load<Font>("Fonts/Cinzel");
        if (source != null)
        {
            muFont = TMP_FontAsset.CreateFontAsset(source);
            muFont.name = "MU_Cinzel_Runtime";
            muFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
            if (fallback != null && fallback != muFont &&
                !muFont.fallbackFontAssetTable.Contains(fallback))
                muFont.fallbackFontAssetTable.Add(fallback);
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .5f;
        Apply();
    }

    void Apply()
    {
        if (muFont == null) return;
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (text == null || text.font == muFont) continue;
            text.font = muFont;
        }
    }
}
