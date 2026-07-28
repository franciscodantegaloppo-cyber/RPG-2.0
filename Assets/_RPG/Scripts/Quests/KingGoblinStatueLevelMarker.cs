using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KingGoblinStatueLevelMarker : MonoBehaviour
{
    GameObject marker;

    void Update()
    {
        QuestManager quests = QuestManager.Instance;
        bool visible = quests != null &&
                       quests.MerchantIntroductionState >= PrimaryQuestState.ReturnToTonioAfterTrial;
        if (marker == null) Build();
        if (marker.activeSelf != visible) marker.SetActive(visible);
    }

    void LateUpdate()
    {
        if (marker == null || !marker.activeSelf || Camera.main == null) return;
        marker.transform.rotation = Camera.main.transform.rotation;
    }

    void Build()
    {
        marker = new GameObject("KingGoblinStatue_Level_1", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup));
        marker.transform.SetParent(transform, false);
        marker.transform.position = FindTop() + Vector3.up * .65f;
        marker.transform.localScale = Vector3.one * .0065f;
        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 90f);
        Canvas canvas = marker.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 780;
        marker.GetComponent<CanvasGroup>().blocksRaycasts = false;

        GameObject glow = new GameObject("LevelGlow", typeof(RectTransform), typeof(TextMeshProUGUI));
        glow.transform.SetParent(marker.transform, false);
        TextMeshProUGUI text = glow.GetComponent<TextMeshProUGUI>();
        text.text = "1";
        text.fontSize = 76f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, .78f, .05f);
        text.outlineColor = new Color(.12f, .04f, 0f, 1f);
        text.outlineWidth = .28f;
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        marker.SetActive(false);
    }

    Vector3 FindTop()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        float top = transform.position.y + 3f;
        foreach (Renderer renderer in renderers) top = Mathf.Max(top, renderer.bounds.max.y);
        return new Vector3(transform.position.x, top, transform.position.z);
    }
}
