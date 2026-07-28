using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A one-shot, invisible story trigger placed just ahead of the SpawnVillage spawn point.
// It creates world-space text so the lines read as the player's own thoughts, above their name.
public class SpawnMysteryThoughtTrigger : MonoBehaviour
{
    bool used;

    void OnTriggerEnter(Collider other)
    {
        if (used) return;
        Transform player = other.GetComponentInParent<PlayerStats>()?.transform;
        if (player == null && other.CompareTag("Player")) player = other.transform;
        if (player == null) return;

        used = true;
        PlayerMysteryThoughts thoughts = player.GetComponent<PlayerMysteryThoughts>();
        if (thoughts == null) thoughts = player.gameObject.AddComponent<PlayerMysteryThoughts>();
        thoughts.PlaySequence();
        Destroy(gameObject);
    }
}

public class PlayerMysteryThoughts : MonoBehaviour
{
    TextMeshProUGUI text;
    Image background;
    RectTransform thoughtRect;
    Camera mainCamera;
    Coroutine sequence;

    public void PlaySequence()
    {
        if (sequence != null) StopCoroutine(sequence);
        EnsureText();
        sequence = StartCoroutine(Sequence());
    }

    void EnsureText()
    {
        if (text != null) return;
        GameObject thought = new GameObject("PlayerMysteryThought", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        thought.transform.SetParent(transform, false);
        thought.transform.localPosition = new Vector3(0f, 3.35f, 0f);
        thought.transform.localScale = Vector3.one * 0.008f;
        thoughtRect = thought.GetComponent<RectTransform>();
        thoughtRect.sizeDelta = new Vector2(440f, 62f);
        Canvas canvas = thought.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

        background = thought.AddComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (frame != null)
        {
            background.sprite = frame;
            background.type = Image.Type.Sliced;
        }
        background.color = new Color(0.09f, 0.025f, 0.15f, 0f);

        GameObject textGo = new GameObject("ThoughtText", typeof(RectTransform));
        textGo.transform.SetParent(thought.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 7f);
        textRect.offsetMax = new Vector2(-18f, -7f);
        text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 15f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 15f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = new Color(0.86f, 0.74f, 1f, 0f);
        text.outlineColor = new Color(0.08f, 0.005f, 0.15f, 0.95f);
        text.outlineWidth = 0.18f;
    }

    IEnumerator Sequence()
    {
        yield return ShowLine("Este lugar es muy misterioso");
        yield return ShowLine("Siento mucha energia oscura");
        yield return ShowLine("Que me está queriendo decir Tonio?");
        Destroy(thoughtRect.gameObject, 1.2f);
        text = null;
        sequence = null;
    }

    IEnumerator ShowLine(string line)
    {
        text.text = line;
        yield return Fade(0f, 1f, 0.35f);
        yield return new WaitForSeconds(2.1f);
        yield return Fade(1f, 0f, 1.05f);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Color color = text.color;
            color.a = Mathf.Lerp(from, to, elapsed / duration);
            text.color = color;
            Color panelColor = background.color;
            panelColor.a = Mathf.Lerp(from * 0.82f, to * 0.82f, elapsed / duration);
            background.color = panelColor;
            yield return null;
        }
        Color finalColor = text.color;
        finalColor.a = to;
        text.color = finalColor;
        Color finalPanelColor = background.color;
        finalPanelColor.a = to * 0.82f;
        background.color = finalPanelColor;
    }

    void LateUpdate()
    {
        if (text == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
            thoughtRect.rotation = Quaternion.LookRotation(thoughtRect.position - mainCamera.transform.position, Vector3.up);
    }
}

public static class SpawnMysteryThoughtBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddTriggerToSpawnVillage()
    {
        if (SceneManager.GetActiveScene().name != "SpawnVillage" || GameObject.Find("SpawnMysteryThoughtTrigger") != null)
            return;

        GameObject spawn = GameObject.FindWithTag("SpawnPoint");
        if (spawn == null) spawn = GameObject.FindWithTag("Player");
        if (spawn == null) return;

        GameObject trigger = new GameObject("SpawnMysteryThoughtTrigger");
        Vector3 forward = spawn.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        trigger.transform.position = spawn.transform.position + forward.normalized * 3.5f;
        BoxCollider box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.4f, 0f);
        box.size = new Vector3(6f, 3f, 4f);
        trigger.AddComponent<SpawnMysteryThoughtTrigger>();
    }
}
