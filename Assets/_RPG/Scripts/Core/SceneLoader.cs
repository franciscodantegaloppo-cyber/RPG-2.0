using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Fade")]
    [SerializeField] CanvasGroup fadeCanvasGroup;
    [SerializeField] float fadeDuration = 0.5f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeCanvasGroup == null)
            CreateFadeCanvas();
    }

    void CreateFadeCanvas()
    {
        var go = new GameObject("FadeCanvas");
        go.transform.SetParent(transform);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(go.transform, false);
        var img = imgGO.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        fadeCanvasGroup = go.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }

    public void LoadScene(int sceneIndex)
    {
        StartCoroutine(LoadSceneRoutine(sceneIndex));
    }

    public void LoadScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
            StartCoroutine(LoadSceneRoutine(sceneName));
    }

    IEnumerator LoadSceneRoutine(int index)
    {
        GameManager.Instance?.SetState(GameState.Loading);
        yield return StartCoroutine(Fade(0f, 1f));

        var op = SceneManager.LoadSceneAsync(index);
        while (!op.isDone) yield return null;

        yield return StartCoroutine(Fade(1f, 0f));
        GameManager.Instance?.SetState(GameState.Exploration);
    }

    IEnumerator LoadSceneRoutine(string sceneName)
    {
        GameManager.Instance?.SetState(GameState.Loading);
        yield return StartCoroutine(Fade(0f, 1f));
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null) yield break;
        while (!operation.isDone) yield return null;
        yield return StartCoroutine(Fade(1f, 0f));
        GameManager.Instance?.SetState(GameState.Exploration);
    }

    IEnumerator Fade(float from, float to)
    {
        fadeCanvasGroup.blocksRaycasts = to > 0f;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = to >= 1f;
    }
}
