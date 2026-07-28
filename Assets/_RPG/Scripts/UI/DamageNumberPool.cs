using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Self-bootstraps like PauseManager/WindVisualEffect - builds its own pooled world-space text
// objects at runtime, no prefab or scene wiring needed.
public class DamageNumberPool : MonoBehaviour
{
    public static DamageNumberPool Instance { get; private set; }

    enum NumberKind { Physical, Critical, Magic, Dark, Healing, PlayerDamage }

    const int PoolSize = 32;
    const float NormalFontSize = 3.2f;
    const float CritFontSize = 5.5f;
    static readonly Color NormalColor = new Color(1f, 0.92f, 0.75f);
    static readonly Color CritColor = new Color(1f, .72f, .12f);
    static readonly Color MagicColor = new Color(.3f, .78f, 1f);
    static readonly Color DarkColor = new Color(.7f, .3f, 1f);
    static readonly Color HealingColor = new Color(.3f, 1f, .48f);
    static readonly Color PlayerDamageColor = new Color(1f, .22f, .18f);

    Queue<GameObject> pool = new Queue<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<DamageNumberPool>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("DamageNumberPool");
        go.AddComponent<DamageNumberPool>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject entry = BuildEntry();
            entry.SetActive(false);
            pool.Enqueue(entry);
        }
    }

    GameObject BuildEntry()
    {
        GameObject go = new GameObject("DamageNumber");
        go.transform.SetParent(transform, false);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = NormalFontSize;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return go;
    }

    public void ShowDamage(Vector3 worldPos, float amount, bool isCrit = false)
        => ShowNumber(worldPos, amount, isCrit ? NumberKind.Critical : NumberKind.Physical);

    public void ShowMagicDamage(Vector3 worldPos, float amount)
        => ShowNumber(worldPos, amount, NumberKind.Magic);

    public void ShowDarkDamage(Vector3 worldPos, float amount)
        => ShowNumber(worldPos, amount, NumberKind.Dark);

    public void ShowHealing(Vector3 worldPos, float amount)
        => ShowNumber(worldPos, amount, NumberKind.Healing);

    public void ShowPlayerDamage(Vector3 worldPos, float amount)
        => ShowNumber(worldPos, amount, NumberKind.PlayerDamage);

    void ShowNumber(Vector3 worldPos, float amount, NumberKind kind)
    {
        if (pool.Count == 0 || amount <= .01f) return;
        var go = pool.Dequeue();
        go.SetActive(true);

        // Slight random horizontal jitter so several hits in the same spot don't perfectly overlap.
        Vector3 jitter = new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
        go.transform.position = worldPos + Vector3.up * .65f + jitter;
        Camera cam = Camera.main;
        if (cam != null)
            go.transform.rotation = cam.transform.rotation;

        var tmp = go.GetComponent<TextMeshPro>();
        if (tmp != null)
        {
            string prefix = kind == NumberKind.Healing ? "+" :
                            kind == NumberKind.Critical ? "CRÍTICO " : "";
            tmp.text = prefix + Mathf.RoundToInt(amount);
            tmp.color = KindColor(kind);
            tmp.fontSize = kind == NumberKind.Critical ? CritFontSize :
                           kind == NumberKind.Healing ? 4.2f : NormalFontSize;
        }

        StartCoroutine(ReturnToPool(go, kind));
    }

    static Color KindColor(NumberKind kind) => kind switch
    {
        NumberKind.Critical => CritColor,
        NumberKind.Magic => MagicColor,
        NumberKind.Dark => DarkColor,
        NumberKind.Healing => HealingColor,
        NumberKind.PlayerDamage => PlayerDamageColor,
        _ => NormalColor
    };

    IEnumerator ReturnToPool(GameObject go, NumberKind kind)
    {
        float t = 0f;
        Vector3 start = go.transform.position;
        Vector3 drift = new Vector3(Random.Range(-.18f, .18f), .82f, 0f);
        Camera cam = Camera.main;
        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        Color baseColor = KindColor(kind);

        while (t < .9f)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / .9f);
            go.transform.position = start + drift * normalized;
            float pop = normalized < .18f
                ? Mathf.Lerp(.65f, 1.25f, normalized / .18f)
                : Mathf.Lerp(1.25f, .86f, (normalized - .18f) / .82f);
            go.transform.localScale = Vector3.one * pop;
            if (tmp != null)
            {
                Color faded = baseColor;
                faded.a = 1f - Mathf.SmoothStep(.55f, 1f, normalized);
                tmp.color = faded;
            }
            if (cam != null)
                go.transform.rotation = cam.transform.rotation;
            yield return null;
        }

        go.transform.localScale = Vector3.one;
        go.SetActive(false);
        pool.Enqueue(go);
    }
}
