using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws the path measured at the real sword tip. The centre line is rebuilt as a
// mathematically smoothed curve instead of letting TrailRenderer join samples with
// visible straight segments. Timed samples are removed from the tail, so the cut is
// progressively consumed rather than remaining painted in the air.
public class MeasuredSwordSlashTrail : MonoBehaviour
{
    sealed class SlashStroke
    {
        public GameObject gameObject;
        public LineRenderer line;
        public LineRenderer coreLine;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Mesh mesh;
        public ParticleSystem particles;
        public readonly List<TimedPoint> points = new List<TimedPoint>(32);
        public Color color;
        public float lifetime;
        public float minDistance;
        public bool accepting = true;
    }

    struct TimedPoint
    {
        public Vector3 position;
        public Vector3 innerPosition;
        public float createdAt;

        public TimedPoint(Vector3 position, Vector3 innerPosition, float createdAt)
        {
            this.position = position;
            this.innerPosition = innerPosition;
            this.createdAt = createdAt;
        }
    }

    readonly List<SlashStroke> strokes = new List<SlashStroke>(3);
    SlashStroke activeStroke;
    Coroutine stopRoutine;
    Vector3 latestTipWorldPosition;
    Vector3 latestHandleWorldPosition;
    bool pendingActivation;
    float pendingEarliestTime;
    float pendingDeadline;
    float pendingVisibleWindow;
    float pendingBladeLength;
    float pendingAttackSpeed;
    float pendingRelativeTravel;
    Vector3 previousBladeRadial;
    Color pendingColor;
    static Material sharedTrailMaterial;
    static Texture2D sharedTrailTexture;

    public void Begin(Vector3 tipWorldPosition, Vector3 handleWorldPosition,
        float bladeLength, float attackDuration, float attackSpeedMultiplier,
        Color weaponColor)
    {
        latestTipWorldPosition = tipWorldPosition;
        latestHandleWorldPosition = handleWorldPosition;
        if (activeStroke != null)
            activeStroke.accepting = false;
        if (stopRoutine != null)
        {
            StopCoroutine(stopRoutine);
            stopRoutine = null;
        }
        // Measure blade rotation relative to its handle. Player translation from walking or
        // sprinting moves both points equally and therefore cannot hide or falsely trigger a cut.
        pendingActivation = true;
        pendingEarliestTime = Time.time + Mathf.Max(.025f, attackDuration * .12f);
        pendingDeadline = Time.time + Mathf.Max(.08f, attackDuration * .58f);
        pendingVisibleWindow = Mathf.Clamp(attackDuration * .2f, .065f, .145f);
        pendingBladeLength = bladeLength;
        pendingAttackSpeed = attackSpeedMultiplier;
        pendingColor = weaponColor;
        pendingRelativeTravel = 0f;
        previousBladeRadial = tipWorldPosition - handleWorldPosition;
    }

    void ActivateStroke()
    {
        pendingActivation = false;

        GameObject slashObject = new GameObject("MeasuredSwordTipSlash_SmoothCurve");
        LineRenderer line = slashObject.AddComponent<LineRenderer>();
        MeshFilter meshFilter = slashObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = slashObject.AddComponent<MeshRenderer>();
        Mesh sweepMesh = new Mesh { name = "SwordBladeSweptArc_Runtime" };
        sweepMesh.MarkDynamic();
        meshFilter.sharedMesh = sweepMesh;
        meshRenderer.sharedMaterial = GetTrailMaterial();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        GameObject coreObject = new GameObject("WindCutCore");
        coreObject.transform.SetParent(slashObject.transform, false);
        LineRenderer core = coreObject.AddComponent<LineRenderer>();
        ParticleSystem particles = slashObject.AddComponent<ParticleSystem>();
        SlashStroke stroke = new SlashStroke
        {
            gameObject = slashObject,
            line = line,
            coreLine = core,
            meshFilter = meshFilter,
            meshRenderer = meshRenderer,
            mesh = sweepMesh,
            particles = particles,
            color = pendingColor,
            lifetime = Mathf.Clamp(pendingVisibleWindow * .26f, .019f, .04f),
            minDistance = Mathf.Clamp(pendingBladeLength * .0045f, .0025f, .009f)
        };
        Configure(line, pendingBladeLength, pendingAttackSpeed, pendingColor);
        ConfigureCore(core, line, pendingColor);
        ConfigureParticles(particles, pendingBladeLength, pendingColor);
        stroke.points.Add(new TimedPoint(latestTipWorldPosition,
            Vector3.Lerp(latestHandleWorldPosition, latestTipWorldPosition, .24f),
            Time.time));
        line.positionCount = 1;
        line.SetPosition(0, latestTipWorldPosition);
        core.positionCount = 1;
        core.SetPosition(0, latestTipWorldPosition);
        strokes.Add(stroke);
        activeStroke = stroke;
        stopRoutine = StartCoroutine(StopAfter(stroke, pendingVisibleWindow));
    }

    public void Sample(Vector3 tipWorldPosition, Vector3 handleWorldPosition)
    {
        latestTipWorldPosition = tipWorldPosition;
        latestHandleWorldPosition = handleWorldPosition;
        Vector3 radial = tipWorldPosition - handleWorldPosition;
        if (pendingActivation)
        {
            Vector3 radialDelta = radial - previousBladeRadial;
            pendingRelativeTravel += radialDelta.magnitude;
            float instantaneousBladeSpeed =
                radialDelta.magnitude / Mathf.Max(Time.deltaTime, .001f);
            previousBladeRadial = radial;
            bool crossedFastestPart = instantaneousBladeSpeed >=
                                      Mathf.Max(.8f, pendingBladeLength * 3.2f);
            if (Time.time >= pendingEarliestTime &&
                (crossedFastestPart || Time.time >= pendingDeadline))
                ActivateStroke();
        }
        if (activeStroke == null || !activeStroke.accepting)
            return;

        List<TimedPoint> points = activeStroke.points;
        if (points.Count > 0 &&
            (points[points.Count - 1].position - tipWorldPosition).sqrMagnitude <
            activeStroke.minDistance * activeStroke.minDistance)
            return;

        Vector3 direction = points.Count > 0
            ? (tipWorldPosition - points[points.Count - 1].position).normalized
            : Vector3.up;
        points.Add(new TimedPoint(tipWorldPosition,
            Vector3.Lerp(handleWorldPosition, tipWorldPosition, .24f), Time.time));
        EmitCutParticles(activeStroke, tipWorldPosition, direction);
    }

    void LateUpdate()
    {
        float now = Time.time;
        for (int i = strokes.Count - 1; i >= 0; i--)
        {
            SlashStroke stroke = strokes[i];
            while (stroke.points.Count > 0 &&
                   now - stroke.points[0].createdAt > stroke.lifetime)
                stroke.points.RemoveAt(0);

            if (stroke.points.Count == 0)
            {
                stroke.line.positionCount = 0;
                if (stroke.coreLine != null)
                    stroke.coreLine.positionCount = 0;
                if (stroke.mesh != null)
                    stroke.mesh.Clear();
                bool particlesAlive = stroke.particles != null &&
                    stroke.particles.IsAlive(true);
                if (!stroke.accepting && !particlesAlive)
                {
                    if (stroke == activeStroke)
                        activeStroke = null;
                    if (stroke.gameObject != null)
                        Destroy(stroke.gameObject);
                    strokes.RemoveAt(i);
                }
                continue;
            }

            RenderSmoothed(stroke);
            RenderBladeSweep(stroke);
        }
    }

    static void RenderBladeSweep(SlashStroke stroke)
    {
        if (stroke.mesh == null) return;
        int count = stroke.points.Count;
        if (count < 2)
        {
            stroke.mesh.Clear();
            return;
        }

        var outer = new List<Vector3>(count);
        var inner = new List<Vector3>(count);
        foreach (TimedPoint point in stroke.points)
        {
            outer.Add(point.position);
            inner.Add(point.innerPosition);
        }
        for (int pass = 0; pass < 2 && outer.Count < 120; pass++)
        {
            outer = RoundCurve(outer);
            inner = RoundCurve(inner);
        }

        int samples = outer.Count;
        Vector3[] vertices = new Vector3[samples * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[(samples - 1) * 6];
        for (int i = 0; i < samples; i++)
        {
            float progress = i / (samples - 1f);
            Vector3 centre = Vector3.Lerp(inner[i], outer[i], .5f);
            // The old tail collapses toward its centre while the current sword span stays
            // broad, producing a thick leading cut and a genuinely tapered wake.
            float tailTaper = Mathf.SmoothStep(.035f, 1f, progress);
            vertices[i * 2] = Vector3.Lerp(centre, inner[i], tailTaper);
            vertices[i * 2 + 1] = Vector3.Lerp(centre, outer[i], tailTaper);
            uv[i * 2] = new Vector2(progress, 0f);
            uv[i * 2 + 1] = new Vector2(progress, 1f);
            float alpha = Mathf.SmoothStep(0f, .82f, progress) *
                          Mathf.Lerp(.42f, .78f, progress);
            Color color = Color.Lerp(stroke.color, Color.white,
                .18f + progress * .22f);
            color.a = alpha;
            colors[i * 2] = colors[i * 2 + 1] = color;
            if (i >= samples - 1) continue;
            int tri = i * 6;
            int vertex = i * 2;
            triangles[tri] = vertex;
            triangles[tri + 1] = vertex + 2;
            triangles[tri + 2] = vertex + 1;
            triangles[tri + 3] = vertex + 1;
            triangles[tri + 4] = vertex + 2;
            triangles[tri + 5] = vertex + 3;
        }
        stroke.mesh.Clear();
        stroke.mesh.vertices = vertices;
        stroke.mesh.uv = uv;
        stroke.mesh.colors = colors;
        stroke.mesh.triangles = triangles;
        stroke.mesh.RecalculateBounds();
    }

    static List<Vector3> RoundCurve(List<Vector3> source)
    {
        List<Vector3> rounded = new List<Vector3>(source.Count * 2);
        rounded.Add(source[0]);
        for (int i = 0; i < source.Count - 1; i++)
        {
            rounded.Add(Vector3.Lerp(source[i], source[i + 1], .25f));
            rounded.Add(Vector3.Lerp(source[i], source[i + 1], .75f));
        }
        rounded.Add(source[source.Count - 1]);
        return rounded;
    }

    IEnumerator StopAfter(SlashStroke stroke, float attackDuration)
    {
        yield return new WaitForSeconds(attackDuration);
        stroke.accepting = false;
        if (stroke == activeStroke)
            activeStroke = null;
        stopRoutine = null;
    }

    static void RenderSmoothed(SlashStroke stroke)
    {
        int rawCount = stroke.points.Count;
        if (rawCount == 1)
        {
            stroke.line.positionCount = 1;
            stroke.line.SetPosition(0, stroke.points[0].position);
            stroke.coreLine.positionCount = 1;
            stroke.coreLine.SetPosition(0, stroke.points[0].position);
            return;
        }

        List<Vector3> curve = new List<Vector3>(rawCount);
        for (int i = 0; i < rawCount; i++)
            curve.Add(stroke.points[i].position);

        // Three Chaikin passes round the actual centre line. This is fundamentally
        // different from TrailRenderer corner vertices, which only round the ribbon's
        // outer mesh while its path remains a polygon.
        for (int pass = 0; pass < 3 && curve.Count < 160; pass++)
        {
            List<Vector3> rounded = new List<Vector3>(curve.Count * 2);
            rounded.Add(curve[0]);
            for (int i = 0; i < curve.Count - 1; i++)
            {
                Vector3 a = curve[i];
                Vector3 b = curve[i + 1];
                rounded.Add(Vector3.LerpUnclamped(a, b, .25f));
                rounded.Add(Vector3.LerpUnclamped(a, b, .75f));
            }
            rounded.Add(curve[curve.Count - 1]);
            curve = rounded;
        }

        stroke.line.positionCount = curve.Count;
        stroke.line.SetPositions(curve.ToArray());
        stroke.coreLine.positionCount = curve.Count;
        stroke.coreLine.SetPositions(curve.ToArray());
    }

    static void Configure(LineRenderer line, float bladeLength,
        float attackSpeedMultiplier, Color weaponColor)
    {
        line.sharedMaterial = GetTrailMaterial();
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.useWorldSpace = true;
        line.loop = false;
        line.numCornerVertices = 12;
        line.numCapVertices = 12;
        line.generateLightingData = false;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        float speedWidth = Mathf.Sqrt(Mathf.Clamp(attackSpeedMultiplier, .55f, 3f));
        line.widthMultiplier = Mathf.Clamp(bladeLength * .082f * speedWidth,
            .055f, .18f);
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 5f),
            new Keyframe(.2f, .72f, 1.7f, 1.7f),
            new Keyframe(.58f, 1f, 0f, 0f),
            new Keyframe(1f, .08f, -3.5f, 0f));

        Gradient color = new Gradient();
        Color hotCore = Color.Lerp(weaponColor, Color.white, .62f);
        Color edge = Color.Lerp(weaponColor, Color.black, .16f);
        color.SetKeys(
            new[]
            {
                new GradientColorKey(edge, 0f),
                new GradientColorKey(hotCore, .58f),
                new GradientColorKey(weaponColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.68f, .16f),
                new GradientAlphaKey(.88f, .55f),
                new GradientAlphaKey(0f, 1f)
            });
        line.colorGradient = color;
    }

    static void ConfigureCore(LineRenderer core, LineRenderer outer, Color weaponColor)
    {
        core.sharedMaterial = GetTrailMaterial();
        core.alignment = LineAlignment.View;
        core.textureMode = LineTextureMode.Stretch;
        core.useWorldSpace = true;
        core.loop = false;
        core.numCornerVertices = 12;
        core.numCapVertices = 12;
        core.shadowCastingMode = ShadowCastingMode.Off;
        core.receiveShadows = false;
        core.widthMultiplier = outer.widthMultiplier * .24f;
        core.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(.28f, .8f),
            new Keyframe(.62f, 1f),
            new Keyframe(1f, 0f));
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(weaponColor, Color.white, .72f), 0f),
                new GradientColorKey(Color.white, .55f),
                new GradientColorKey(Color.Lerp(weaponColor, Color.white, .4f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.9f, .3f),
                new GradientAlphaKey(.72f, .7f),
                new GradientAlphaKey(0f, 1f)
            });
        core.colorGradient = gradient;
    }

    static void ConfigureParticles(ParticleSystem particles, float bladeLength,
        Color weaponColor)
    {
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.025f, .06f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.035f, .14f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Clamp(bladeLength * .006f, .006f, .014f),
            Mathf.Clamp(bladeLength * .018f, .014f, .034f));
        main.startColor = new ParticleSystem.MinMaxGradient(
            WithAlpha(Color.Lerp(weaponColor, Color.black, .12f), .16f),
            WithAlpha(Color.Lerp(weaponColor, Color.white, .55f), .62f));
        main.maxParticles = 80;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
                new[]
                {
                new GradientColorKey(Color.Lerp(weaponColor, Color.white, .5f), 0f),
                new GradientColorKey(weaponColor, 1f)
                },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.58f, .18f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = fade;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.8f;
        renderer.velocityScale = .12f;
        renderer.sharedMaterial = GetTrailMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    static void EmitCutParticles(SlashStroke stroke, Vector3 position, Vector3 direction)
    {
        if (stroke.particles == null)
            return;

        int amount = Random.value < .55f ? 2 : 1;
        for (int i = 0; i < amount; i++)
        {
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = position + Random.insideUnitSphere * .018f,
                velocity = direction * Random.Range(.025f, .09f) +
                           Random.insideUnitSphere * .055f,
                startLifetime = Random.Range(.025f, .06f),
                startSize = Random.Range(.009f, .028f),
                startColor = Color.Lerp(
                    WithAlpha(Color.Lerp(stroke.color, Color.black, .12f), .28f),
                    WithAlpha(Color.Lerp(stroke.color, Color.white, .55f), .72f),
                    Random.value)
            };
            stroke.particles.Emit(emit, 1);
        }
    }

    static Material GetTrailMaterial()
    {
        if (sharedTrailMaterial != null)
            return sharedTrailMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        sharedTrailMaterial = new Material(shader)
        {
            name = "MeasuredSwordSlash_Runtime",
            mainTexture = GetTrailTexture(),
            renderQueue = (int)RenderQueue.Transparent,
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedTrailMaterial;
    }

    static Texture2D GetTrailTexture()
    {
        if (sharedTrailTexture != null)
            return sharedTrailTexture;

        const int width = 64;
        const int height = 16;
        sharedTrailTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "MeasuredSwordSlash_SoftBlade",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < height; y++)
        {
            float across = y / (height - 1f);
            float softEdge = Mathf.Pow(Mathf.Sin(across * Mathf.PI), 1.9f);
            for (int x = 0; x < width; x++)
            {
                float along = x / (width - 1f);
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.Min(along * 5f,
                    (1f - along) * 8f));
                sharedTrailTexture.SetPixel(x, y,
                    new Color(1f, 1f, 1f, softEdge * taper));
            }
        }
        sharedTrailTexture.Apply(false, true);
        return sharedTrailTexture;
    }

    void OnDisable()
    {
        if (stopRoutine != null)
            StopCoroutine(stopRoutine);
        pendingActivation = false;
        foreach (SlashStroke stroke in strokes)
            if (stroke.gameObject != null)
                Destroy(stroke.gameObject);
        strokes.Clear();
        activeStroke = null;
    }
}
