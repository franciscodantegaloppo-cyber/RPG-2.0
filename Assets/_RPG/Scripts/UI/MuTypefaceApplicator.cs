using System.Collections.Generic;
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
        MuTypefaceApplicator applicator =
            root.AddComponent<MuTypefaceApplicator>();
        // Awake runs inside AddComponent, so normally it has already claimed
        // the singleton. Keep this assignment as a safe fallback.
        if (instance == null)
            instance = applicator;
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        Font source = Resources.Load<Font>("Fonts/Cinzel");
        if (source != null)
        {
            muFont = TMP_FontAsset.CreateFontAsset(source);
            if (muFont != null)
            {
                muFont.name = "MU_Cinzel_Runtime";
                muFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;

                TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
                List<TMP_FontAsset> fallbackTable =
                    muFont.fallbackFontAssetTable;
                if (fallbackTable == null)
                {
                    fallbackTable = new List<TMP_FontAsset>();
                    muFont.fallbackFontAssetTable = fallbackTable;
                }

                if (fallback != null && fallback != muFont &&
                    !fallbackTable.Contains(fallback))
                    fallbackTable.Add(fallback);
            }
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }
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
