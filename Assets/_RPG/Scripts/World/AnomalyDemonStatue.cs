using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnomalyDemonStatue : MonoBehaviour, IInteractable
{
    const string Inscription = "Guardián de anomalías, señor de las tinieblas, soberano del abismo eterno.";
    [SerializeField] float cooldown = 8f;
    [SerializeField] float windPushSpeed = 5.5f;
    float nextUseTime;
    bool knockedDown;

    public string GetInteractionText() => "[E] Invocar ráfagas oscuras";
    public bool CanInteract(PlayerInteraction player) => Time.time >= nextUseTime;

    public void Interact(PlayerInteraction player)
    {
        if (knockedDown || Time.time < nextUseTime) return;
        nextUseTime = Time.time + cooldown;
        StartCoroutine(ReleaseDarkBursts(player));
    }

    public void KnockDownAndBurn(Vector3 sourcePosition)
    {
        if (knockedDown) return;
        knockedDown = true;
        StopAllCoroutines();
        StartCoroutine(BurnAndFall(sourcePosition));
    }

    IEnumerator BurnAndFall(Vector3 sourcePosition)
    {
        Bounds bounds = GetBounds();
        GameObject firePrefab = Resources.Load<GameObject>("VFX/FreeFireProjectileVFX");
        if (firePrefab != null)
        {
            GameObject fire = Instantiate(firePrefab, bounds.center, Quaternion.identity, transform);
            fire.name = "StatueBurningFire";
            fire.transform.localScale = Vector3.one * 2.6f;
            Destroy(fire, 10f);
        }
        yield return new WaitForSeconds(.35f);
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;

        Vector3 away = transform.position - sourcePosition;
        away.y = 0f;
        if (away.sqrMagnitude < .01f) away = transform.right;
        Vector3 axis = Vector3.Cross(Vector3.up, away.normalized);
        Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        float elapsed = 0f, previousAngle = 0f;
        while (elapsed < 1.2f)
        {
            elapsed += Time.deltaTime;
            float angle = 84f * Mathf.Pow(Mathf.Clamp01(elapsed / 1.2f), 2f);
            transform.RotateAround(pivot, axis, angle - previousAngle);
            previousAngle = angle;
            yield return null;
        }
        yield return new WaitForSeconds(10f);
        Destroy(gameObject);
    }

    Bounds GetBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 2f);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    IEnumerator ReleaseDarkBursts(PlayerInteraction player)
    {
        ParticleSystem bursts = CreateDarkBursts();
        float elapsed = 0f;
        CharacterController playerController = player != null ? player.GetComponent<CharacterController>() : null;
        while (elapsed < 5f)
        {
            elapsed += Time.deltaTime;
            if (player != null)
            {
                Vector3 direction = player.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .01f)
                {
                    Vector3 push = direction.normalized * windPushSpeed * Time.deltaTime;
                    if (playerController != null && playerController.enabled) playerController.Move(push);
                    else player.transform.position += push;
                }
            }
            yield return null;
        }
        var emission = bursts.emission;
        emission.enabled = false;
        Destroy(bursts.gameObject, 2f);
        yield return ShowInscription();
    }

    ParticleSystem CreateDarkBursts()
    {
        GameObject go = new GameObject("StatueDarkBursts");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 1.25f;
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5.5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.62f);
        main.startColor = new Color(0.2f, 0.01f, 0.34f, 0.65f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission;
        emission.rateOverTime = 180f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(0.09f, 0.005f, 0.17f), 0f), new GradientColorKey(new Color(0.5f, 0.08f, 0.85f), 0.55f), new GradientColorKey(new Color(0.02f, 0f, 0.06f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.72f, 0.12f), new GradientAlphaKey(0.25f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = WindVisualEffect.CreateWindStreakMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        particles.Play();
        return particles;
    }

    IEnumerator ShowInscription()
    {
        GameObject panel = new GameObject("StatueInscription", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        panel.transform.SetParent(transform, false);
        // At ground level, just in front of the statue's feet, so nearby players can read it.
        panel.transform.localPosition = Vector3.up * 0.18f;
        panel.transform.localScale = Vector3.one * 0.01f;
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500f, 76f);
        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        Image frame = panel.AddComponent<Image>();
        Sprite frameSprite = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frameSprite != null) { frame.sprite = frameSprite; frame.type = Image.Type.Sliced; }
        frame.color = new Color(0.055f, 0.012f, 0.10f, 0f);

        GameObject labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(panel.transform, false);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 8f); labelRect.offsetMax = new Vector2(-18f, -8f);
        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = Inscription;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9; label.fontSizeMax = 16;
        label.color = new Color(0.86f, 0.67f, 1f, 0f);
        label.outlineColor = new Color(0.04f, 0f, 0.08f, 1f); label.outlineWidth = .16f;

        Camera camera = Camera.main;
        float elapsed = 0f;
        while (elapsed < 6f)
        {
            elapsed += Time.deltaTime;
            if (camera == null) camera = Camera.main;
            if (camera != null) rect.rotation = Quaternion.LookRotation(rect.position - camera.transform.position, Vector3.up);
            float alpha = Mathf.Clamp01(Mathf.Min(elapsed / .35f, (6f - elapsed) / .8f));
            Color frameColor = frame.color; frameColor.a = alpha * .92f; frame.color = frameColor;
            Color labelColor = label.color; labelColor.a = alpha; label.color = labelColor;
            yield return null;
        }
        Destroy(panel);
    }
}
