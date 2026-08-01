using System.Collections;
using UnityEngine;

public class WeaponSocket : MonoBehaviour
{
    [Header("Sockets")]
    [SerializeField] HumanBodyBones handBone = HumanBodyBones.RightHand;
    [SerializeField] Transform backSocket;

    [Header("Offset")]
    [SerializeField] Vector3 handPositionOffset = new Vector3(0.11f, 0.06f, 0.09f);
    [SerializeField] Vector3 handRotationOffset = new Vector3(289.52f, 32.80f, 107.87f);
    [SerializeField] float maxEquippedWeaponSize = 1.45f;
    [SerializeField] float gripLerpSpeed = 12f;
    [SerializeField, Range(0f, 1f)] float rotationStabilizeAmount = 0.92f;

    [Header("Sprint")]
    [SerializeField] float sprintBackTiltDegrees = 22f;

    [Header("Off-hand grip")]
    [SerializeField] float offHandGripDistance = 0.13f;
    [SerializeField] float offHandBlendSpeed = 6f;

    Animator anim;
    GanzPlayerVisual ganzVisual;
    PlayerController playerController;
    CombatSystem combatSystem;
    Transform handTransform;
    GameObject equippedWeapon;
    Transform weaponModel;
    WeaponController currentWeaponCtrl;
    WeaponData currentWeaponData;
    TwoHandGripIK gripIK;
    MeasuredSwordSlashTrail measuredSlashTrail;
    Transform offHandGripPoint;
    bool animationDrivenGrip;
    bool carriedOnBack;
    bool runtimeBackSocket;
    bool weaponTransitioning;
    Coroutine carryTransition;
    Color currentSlashColor = new Color(.82f, .88f, 1f);

    // The weapon prefab's mesh root can carry its own baked-in rotation (it did for
    // StarterSword - not identity), so the blade's actual long axis in the grip's local space is
    // NOT simply Vector3.forward. Cached once per equip from the real mesh bounds so the rotation
    // math below targets where the blade geometry actually points, not an assumed axis.
    Vector3 bladeAxisInGripSpace = Vector3.forward;

    void Start()
    {
        ganzVisual = GetComponent<GanzPlayerVisual>();
        playerController = GetComponent<PlayerController>();
        combatSystem = GetComponent<CombatSystem>();
        anim = GetBestAnimator();
        if (anim != null)
        {
            handTransform = anim.GetBoneTransform(handBone);
            CacheGripIK();
        }
    }

    public void BeginMeasuredSlash(float attackDuration, float attackSpeedMultiplier)
    {
        if (!HasBladeGeometry || equippedWeapon == null)
            return;

        if (measuredSlashTrail == null)
            measuredSlashTrail = GetComponent<MeasuredSwordSlashTrail>();
        if (measuredSlashTrail == null)
            measuredSlashTrail = gameObject.AddComponent<MeasuredSwordSlashTrail>();

        float bladeLength =
            Vector3.Distance(CurrentBladeHandleWorldPosition, CurrentBladeTipWorldPosition);
        measuredSlashTrail.Begin(CurrentBladeTipWorldPosition,
            CurrentBladeHandleWorldPosition, bladeLength, attackDuration,
            attackSpeedMultiplier, currentSlashColor);
    }

    void CacheGripIK()
    {
        if (anim == null)
            return;
        gripIK = anim.GetComponent<TwoHandGripIK>();
        if (gripIK == null)
            gripIK = anim.gameObject.AddComponent<TwoHandGripIK>();
    }

    public void AttachWeapon(GameObject weaponPrefab)
    {
        AttachWeapon(weaponPrefab, null);
    }

    public void AttachWeapon(GameObject weaponPrefab, WeaponData weaponData)
    {
        DetachWeapon();
        if (weaponPrefab == null) return;

        CacheHand();
        Transform attachPoint = handTransform != null ? handTransform : transform;
        equippedWeapon = new GameObject("EquippedWeaponGrip");
        equippedWeapon.transform.SetParent(attachPoint, false);

        GameObject model = Instantiate(weaponPrefab, equippedWeapon.transform);
        model.name = weaponPrefab.name;
        weaponModel = model.transform;
        currentWeaponData = weaponData;
        NormalizeWeaponSize(model);
        AlignModelGripToHand(model.transform);
        CacheBladeAxis(model.transform);
        CacheFallbackBladeEndpoints(model.transform);
        ResetBladeHistory();

        GameObject offHandGo = new GameObject("OffHandGripPoint");
        offHandGripPoint = offHandGo.transform;
        offHandGripPoint.SetParent(equippedWeapon.transform, false);

        ApplyFixedGrip(equippedWeapon.transform, true);

        currentWeaponCtrl = equippedWeapon.GetComponent<WeaponController>();
        if (currentWeaponCtrl == null)
            currentWeaponCtrl = equippedWeapon.AddComponent<WeaponController>();
        currentWeaponCtrl.Initialize(weaponData);
        currentSlashColor = ResolveWeaponSlashColor();

        foreach (Collider collider in weaponModel.GetComponentsInChildren<Collider>(true))
            Destroy(collider);
    }

    public bool HasEquippedWeapon => equippedWeapon != null;
    public Transform EquippedWeaponGrip => equippedWeapon != null
        ? equippedWeapon.transform
        : null;
    public WeaponData CurrentWeaponData => currentWeaponData;
    public Vector3 ActiveHandPositionOffset => CurrentHandPositionOffset;
    public Vector3 ActiveHandRotationOffset => CurrentHandRotationOffset;

    /// <summary>
    /// Applies offsets immediately so the editor calibration tool can position a sword
    /// against the animated hand without guessing numeric values.
    /// </summary>
    public void PreviewEquippedWeaponOffsets(Vector3 position, Vector3 rotation)
    {
        if (currentWeaponData != null)
        {
            currentWeaponData.useCustomEquippedVisual = true;
            currentWeaponData.customHandPositionOffset = position;
            currentWeaponData.customHandRotationOffset = rotation;
        }
        else
        {
            handPositionOffset = position;
            handRotationOffset = rotation;
        }

        if (equippedWeapon != null)
            ApplyFixedGrip(equippedWeapon.transform, true);
    }

    public void ApplyItemInstanceVisual(ItemInstance instance)
    {
        if (weaponModel == null || instance == null)
            return;

        if (instance.darkEnergyEnchanted &&
            weaponModel.GetComponent<DarkEnergyWeaponEffect>() == null)
            weaponModel.gameObject.AddComponent<DarkEnergyWeaponEffect>();

        Color glow = instance.ExcellenceGlowColor;
        float strength = instance.ExcellenceGlowStrength;
        Color intrinsicGlow = currentWeaponData != null && currentWeaponData.hasIntrinsicGlow
            ? currentWeaponData.intrinsicGlowColor
            : Color.clear;
        float intrinsicStrength = currentWeaponData != null && currentWeaponData.hasIntrinsicGlow
            ? currentWeaponData.intrinsicGlowStrength
            : 0f;
        // Excellence levels used to tint the complete material almost white. Apart from hiding
        // the weapon texture, +5/+7 weapons became a featureless glowing silhouette. Preserve
        // the authored albedo and let excellence contribute a restrained emission instead.
        Color finalGlow = strength > 0f ? Color.Lerp(intrinsicGlow, glow, 0.28f) : intrinsicGlow;
        float finalStrength = Mathf.Min(Mathf.Max(strength, intrinsicStrength), 1.25f);
        Color materialColor = ResolveWeaponSlashColor();
        if (instance.darkEnergyEnchanted)
            currentSlashColor = new Color(.42f, .12f, .72f);
        else if (finalStrength > .01f)
            currentSlashColor = Color.Lerp(materialColor, finalGlow, .68f);
        else
            currentSlashColor = materialColor;
        currentSlashColor = MakeSlashColorReadable(currentSlashColor);

        foreach (Renderer renderer in weaponModel.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (finalStrength <= 0f)
                    continue;

                Color emission = finalGlow * finalStrength * 0.34f;
                if (materials[i].HasProperty("_EmissionColor"))
                {
                    materials[i].EnableKeyword("_EMISSION");
                    materials[i].SetColor("_EmissionColor", emission);
                }
            }
            renderer.materials = materials;
        }

        if (finalStrength > 0f && weaponModel.GetComponentInChildren<Light>(true) == null)
        {
            GameObject lightGo = new GameObject("ExcellenceGlowLight");
            lightGo.transform.SetParent(weaponModel, false);
            lightGo.transform.localPosition = Vector3.zero;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = finalGlow;
            light.range = 1.25f + finalStrength * 0.18f;
            light.intensity = finalStrength * 0.16f;
        }
    }

    Color ResolveWeaponSlashColor()
    {
        if (weaponModel == null)
            return new Color(.82f, .88f, 1f);

        Color weighted = Color.black;
        float totalWeight = 0f;
        foreach (Renderer renderer in weaponModel.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                Color candidate = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.HasProperty("_Color")
                        ? material.GetColor("_Color")
                        : Color.white;
                Texture texture = material.HasProperty("_BaseMap")
                    ? material.GetTexture("_BaseMap")
                    : material.mainTexture;
                if (texture != null && TrySampleDominantTextureColor(texture, out Color sampled))
                    candidate *= sampled;

                Color.RGBToHSV(candidate, out float hue, out float saturation,
                    out float value);
                float weight = Mathf.Lerp(.35f, 1.4f, saturation) *
                               Mathf.Lerp(.2f, 1f, value);
                weighted += candidate.linear * weight;
                totalWeight += weight;
            }
        }

        if (totalWeight <= .001f)
            return new Color(.82f, .88f, 1f);
        Color result = (weighted / totalWeight).gamma;
        result.a = 1f;
        return MakeSlashColorReadable(result);
    }

    static bool TrySampleDominantTextureColor(Texture source, out Color result)
    {
        result = Color.white;
        RenderTexture temporary = RenderTexture.GetTemporary(12, 12, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            pixels = new Texture2D(12, 12, TextureFormat.RGBA32, false, true);
            pixels.ReadPixels(new Rect(0f, 0f, 12f, 12f), 0, 0, false);
            pixels.Apply(false, false);

            Color sum = Color.black;
            float total = 0f;
            foreach (Color pixel in pixels.GetPixels())
            {
                Color.RGBToHSV(pixel, out float hue, out float saturation,
                    out float value);
                if (pixel.a < .08f || value < .045f)
                    continue;
                float weight = pixel.a * Mathf.Lerp(.25f, 1.6f, saturation);
                sum += pixel.linear * weight;
                total += weight;
            }
            if (total <= .001f)
                return false;
            result = (sum / total).gamma;
            result.a = 1f;
            return true;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (pixels != null)
                Destroy(pixels);
        }
    }

    static Color MakeSlashColorReadable(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        saturation = Mathf.Clamp(saturation * 1.18f, .18f, .92f);
        value = Mathf.Clamp(value * 1.22f, .64f, 1f);
        Color readable = Color.HSVToRGB(hue, saturation, value);
        readable.a = 1f;
        return readable;
    }

    void LateUpdate()
    {
        if (equippedWeapon == null || !equippedWeapon.activeInHierarchy)
            return;

        if (weaponTransitioning)
            return;

        if (carriedOnBack)
        {
            UpdateRuntimeBackSocketPose();
            return;
        }

        UpdateBladeTipVelocity();
        measuredSlashTrail?.Sample(CurrentBladeTipWorldPosition,
            CurrentBladeHandleWorldPosition);

        bool attacking = combatSystem != null && combatSystem.IsAttacking;
        if (attacking || animationDrivenGrip)
            return;

        ApplyFixedGrip(equippedWeapon.transform, false);
    }

    // Tracked every frame (including mid-swing, when ApplyFixedGrip is skipped and the weapon
    // is instead following the animated hand bone) so slash VFX can be anchored to and oriented
    // by the blade's real swing instead of guessing from the combo index.
    Vector3 previousTipWorldPosition;
    bool hasPreviousTipPosition;
    Vector3 currentTipVelocity;
    const int BladeHistoryCapacity = 18;
    readonly Vector3[] bladeTipHistory = new Vector3[BladeHistoryCapacity];
    readonly Vector3[] bladeHandleHistory = new Vector3[BladeHistoryCapacity];
    readonly float[] bladeTimeHistory = new float[BladeHistoryCapacity];
    int bladeHistoryWriteIndex;
    int bladeHistoryCount;

    void UpdateBladeTipVelocity()
    {
        Vector3 tip = CurrentBladeTipWorldPosition;
        Vector3 handle = CurrentBladeHandleWorldPosition;
        if (hasPreviousTipPosition && Time.deltaTime > 0.0001f)
            currentTipVelocity = (tip - previousTipWorldPosition) / Time.deltaTime;
        previousTipWorldPosition = tip;
        hasPreviousTipPosition = true;

        bladeTipHistory[bladeHistoryWriteIndex] = tip;
        bladeHandleHistory[bladeHistoryWriteIndex] = handle;
        bladeTimeHistory[bladeHistoryWriteIndex] = Time.time;
        bladeHistoryWriteIndex = (bladeHistoryWriteIndex + 1) % BladeHistoryCapacity;
        bladeHistoryCount = Mathf.Min(bladeHistoryCount + 1, BladeHistoryCapacity);
    }

    // World position of the blade's actual tip vertex this frame, and its instantaneous swing
    // velocity - used by StylizedSwordSlashVfx to anchor/orient the slash to how the sword is
    // really swinging rather than a fixed dead-ahead point and an index-parity tilt guess.
    public Vector3 CurrentBladeTipWorldPosition
    {
        get
        {
            if (bladeMeshTransform != null && hasVertexExtremes)
                return bladeMeshTransform.TransformPoint(tipEndLocal);
            if (equippedWeapon != null && hasFallbackBladeBounds)
                return equippedWeapon.transform.TransformPoint(fallbackTipGripLocal);
            return equippedWeapon != null
                ? equippedWeapon.transform.position
                : transform.position;
        }
    }

    public Vector3 CurrentBladeHandleWorldPosition
    {
        get
        {
            if (bladeMeshTransform != null && hasVertexExtremes)
                return bladeMeshTransform.TransformPoint(handleEndLocal);
            if (equippedWeapon != null && hasFallbackBladeBounds)
                return equippedWeapon.transform.TransformPoint(fallbackHandleGripLocal);
            return equippedWeapon != null
                ? equippedWeapon.transform.position
                : transform.position;
        }
    }

    public Vector3 CurrentBladeSwingVelocity => currentTipVelocity;

    /// Reconstructs the actual recent cutting arc from several rendered animation frames.
    /// Comparing handle-to-tip vectors removes player/root movement, so running forward cannot
    /// make the slash point in the travel direction. This method also reads the current bones
    /// directly: animation events execute before WeaponSocket.LateUpdate, therefore the current
    /// frame is intentionally compared with samples saved by previous LateUpdates.
    public bool TryGetRecentBladeSwing(out Vector3 arcCenter, out Vector3 bladeDirection,
        out Vector3 swingDirection, out float bladeLength, out float sweepDistance)
    {
        arcCenter = Vector3.zero;
        bladeDirection = Vector3.up;
        swingDirection = transform.right;
        bladeLength = 0f;
        sweepDistance = 0f;
        if (!HasBladeGeometry || bladeHistoryCount < 2)
            return false;

        Vector3 currentHandle = CurrentBladeHandleWorldPosition;
        Vector3 currentTip = CurrentBladeTipWorldPosition;
        Vector3 currentRadial = currentTip - currentHandle;
        bladeLength = currentRadial.magnitude;
        if (bladeLength < .08f)
            return false;

        Vector3 currentDirection = currentRadial / bladeLength;
        float now = Time.time;
        int bestIndex = -1;
        float bestScore = 0f;
        Vector3 bestRelativeSweep = Vector3.zero;

        for (int offset = 1; offset <= bladeHistoryCount; offset++)
        {
            int index = (bladeHistoryWriteIndex - offset + BladeHistoryCapacity) %
                        BladeHistoryCapacity;
            float age = now - bladeTimeHistory[index];
            if (age < .012f || age > .18f)
                continue;

            Vector3 oldRadial = bladeTipHistory[index] - bladeHandleHistory[index];
            if (oldRadial.sqrMagnitude < .0064f)
                continue;

            // Rotation of the blade relative to its own handle. Translating the player, hand or
            // root contributes equally to tip and handle and is cancelled by this subtraction.
            Vector3 relativeSweep = currentRadial - oldRadial;
            relativeSweep = Vector3.ProjectOnPlane(relativeSweep, currentDirection);
            float distance = relativeSweep.magnitude;
            float score = distance * (1f + Mathf.Clamp01(age / .09f) * .35f);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestIndex = index;
            bestRelativeSweep = relativeSweep;
        }

        if (bestIndex < 0 || bestRelativeSweep.magnitude < .045f)
            return false;

        Vector3 previousRadial = bladeTipHistory[bestIndex] -
                                 bladeHandleHistory[bestIndex];
        Vector3 previousDirection = previousRadial.normalized;
        Vector3 middleDirection = currentDirection + previousDirection;
        bladeDirection = middleDirection.sqrMagnitude > .0001f
            ? middleDirection.normalized
            : currentDirection;
        swingDirection = bestRelativeSweep.normalized;
        sweepDistance = bestRelativeSweep.magnitude;

        // NamuFX's slash is a radial arc whose root is its centre of rotation. The former code
        // placed that root 62% along the blade, pushing the complete effect beyond the sword.
        // The midpoint between the two animated handle samples is the real centre of the swing.
        Vector3 previousHandle = bladeHandleHistory[bestIndex];
        arcCenter = Vector3.Lerp(previousHandle, currentHandle, .5f) +
                    bladeDirection * bladeLength * .06f;
        return true;
    }

    /// True only when the mesh was readable and its extremes were cached, i.e. when the blade
    /// tip/handle positions above are real geometry rather than the equipped-root fallback.
    public bool HasBladeGeometry =>
        (bladeMeshTransform != null && hasVertexExtremes) || hasFallbackBladeBounds;

    void ApplyFixedGrip(Transform weapon, bool instant)
    {
        weapon.localPosition = CurrentHandPositionOffset;

        bool sprinting = playerController != null && playerController.IsSprinting;

        // The pack's Unarmed-Walk/Run clips swing the hand bone through a wide arc (they were
        // authored for an empty hand, not a rigidly-gripped two-handed sword), so a purely
        // hand-relative offset makes the blade swing wildly during locomotion. Blend the natural
        // hand-relative rotation toward a world-space "blade points up, aligned to facing" target
        // to keep the sword visually stable while still tracking the hand's grip point/position.
        Transform parent = weapon.parent;
        Quaternion naturalWorldRotation = parent.rotation * Quaternion.Euler(CurrentHandRotationOffset);
        // Stable target must send the ACTUAL blade axis to the target "up" direction, not the
        // grip's own forward - those aren't the same direction once the prefab's baked-in mesh
        // rotation is accounted for. While sprinting, tilt that target backward (around the
        // player's right axis) so the blade trails behind instead of staying bolt upright.
        Vector3 stableUpTarget = sprinting
            ? Quaternion.AngleAxis(-sprintBackTiltDegrees, transform.right) * Vector3.up
            : Vector3.up;
        Quaternion stableWorldRotation = Quaternion.FromToRotation(bladeAxisInGripSpace, stableUpTarget);
        Quaternion targetWorldRotation = Quaternion.Slerp(naturalWorldRotation, stableWorldRotation, rotationStabilizeAmount);
        Quaternion targetRotation = Quaternion.Inverse(parent.rotation) * targetWorldRotation;

        weapon.localRotation = instant
            ? targetRotation
            : Quaternion.Slerp(weapon.localRotation, targetRotation, Time.deltaTime * gripLerpSpeed);

        UpdateOffHandGrip(weapon, instant);
    }

    void UpdateOffHandGrip(Transform weapon, bool instant)
    {
        if (offHandGripPoint != null)
        {
            offHandGripPoint.localPosition = -bladeAxisInGripSpace * offHandGripDistance;
            offHandGripPoint.localRotation = Quaternion.identity;
        }

        if (gripIK == null)
            return;

        gripIK.target = offHandGripPoint;
        float targetWeight = offHandGripPoint != null ? 1f : 0f;
        gripIK.weight = instant
            ? targetWeight
            : Mathf.MoveTowards(gripIK.weight, targetWeight, Time.deltaTime * offHandBlendSpeed);
    }

    // Cached by CacheExtremes() so AlignModelGripToHand (anchor position) and CacheBladeAxis
    // (anchor rotation) agree on which end of the mesh is "the handle" - computing them
    // independently from two different heuristics (as before) let them disagree on a diagonally-
    // authored mesh, which is what actually produced the King Goblin sword's broken grip: the
    // rotation fix alone wasn't enough because the POSITION anchor was still using the old
    // axis-aligned-bounds heuristic and was landing mid-blade instead of at the handle.
    bool hasVertexExtremes;
    Vector3 handleEndLocal;
    Vector3 tipEndLocal;
    bool hasFallbackBladeBounds;
    Vector3 fallbackHandleGripLocal;
    Vector3 fallbackTipGripLocal;
    // The transform handleEndLocal/tipEndLocal are actually expressed relative to - the
    // MeshFilter's own transform, which is NOT always the model root (weapon prefabs commonly
    // nest their mesh under a child object). Using the wrong transform to convert those local
    // vertex positions to world space silently produces nonsense coordinates.
    Transform bladeMeshTransform;

    void AlignModelGripToHand(Transform model)
    {
        MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
        bool wantsVertexAnchor = currentWeaponData != null && currentWeaponData.useVertexGripAnchor;
        CacheExtremes(mf);

        if (!wantsVertexAnchor || !hasVertexExtremes)
        {
            AlignModelGripToHandByBounds(model);
            return;
        }

        Vector3 handleWorld = mf.transform.TransformPoint(handleEndLocal);
        Vector3 tipWorld = mf.transform.TransformPoint(tipEndLocal);
        Vector3 gripWorld = Vector3.Lerp(handleWorld, tipWorld, CurrentGripAnchorPercent);
        model.localPosition -= model.parent.InverseTransformPoint(gripWorld);
    }

    // Fallback for meshes that can't be read (Read/Write disabled in import settings) - same
    // axis-aligned-bounds heuristic used before the vertex-based fix. Only reliable when the
    // mesh happens to be authored aligned to its own local axes.
    void AlignModelGripToHandByBounds(Transform model)
    {
        Bounds localBounds;
        if (!TryGetLocalBounds(model, out localBounds))
            return;

        Vector3 size = localBounds.size;
        int axis = 1;
        float length = size.y;
        if (size.x > length) { axis = 0; length = size.x; }
        if (size.z > length) { axis = 2; length = size.z; }
        if (length <= 0.001f)
            return;

        Vector3 gripPoint = localBounds.center;
        float min = axis == 0 ? localBounds.min.x : axis == 1 ? localBounds.min.y : localBounds.min.z;
        float handleOffset = length * CurrentGripAnchorPercent;
        if (axis == 0) gripPoint.x = min + handleOffset;
        else if (axis == 1) gripPoint.y = min + handleOffset;
        else gripPoint.z = min + handleOffset;

        model.localPosition -= gripPoint;
    }

    void CacheBladeAxis(Transform model)
    {
        bladeAxisInGripSpace = Vector3.forward;
        MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
        CacheExtremes(mf);
        if (!hasVertexExtremes)
        {
            // Mesh.bounds is available even with Read/Write disabled. CacheFallbackBladeEndpoints
            // runs immediately after this method and derives the real long axis from those bounds,
            // so this is a supported import configuration rather than an error condition.
            return;
        }

        Vector3 axisLocal = (tipEndLocal - handleEndLocal).normalized;

        // axisLocal is in the mesh's own local space; rotate it by everything between the mesh
        // and the grip (the model root's local rotation, plus the mesh renderer's own local
        // rotation if the MeshFilter isn't on the model root itself) to express it in grip space.
        Transform cursor = mf.transform;
        Quaternion accumulated = Quaternion.identity;
        while (cursor != null && cursor != model.parent)
        {
            accumulated = cursor.localRotation * accumulated;
            cursor = cursor.parent;
        }
        bladeAxisInGripSpace = (accumulated * axisLocal).normalized;
    }

    // Mesh.bounds remains available even when Read/Write is disabled. It is less exact than the
    // vertex extremes above, but still gives every imported sword a real handle/tip pair instead
    // of sending its slash to a generic point in front of the character.
    void CacheFallbackBladeEndpoints(Transform model)
    {
        hasFallbackBladeBounds = false;
        if (equippedWeapon == null || model == null)
            return;

        MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        Bounds bounds = mf.sharedMesh.bounds;
        Vector3 size = bounds.size;
        int axis = 0;
        if (size.y > size.x && size.y >= size.z) axis = 1;
        else if (size.z > size.x && size.z > size.y) axis = 2;
        float length = axis == 0 ? size.x : axis == 1 ? size.y : size.z;
        if (length < .08f)
            return;

        Vector3 endpointA = bounds.center;
        Vector3 endpointB = bounds.center;
        if (axis == 0) { endpointA.x = bounds.min.x; endpointB.x = bounds.max.x; }
        else if (axis == 1) { endpointA.y = bounds.min.y; endpointB.y = bounds.max.y; }
        else { endpointA.z = bounds.min.z; endpointB.z = bounds.max.z; }

        Vector3 gripA = equippedWeapon.transform.InverseTransformPoint(
            mf.transform.TransformPoint(endpointA));
        Vector3 gripB = equippedWeapon.transform.InverseTransformPoint(
            mf.transform.TransformPoint(endpointB));
        bool aIsHandle = gripA.sqrMagnitude <= gripB.sqrMagnitude;
        fallbackHandleGripLocal = aIsHandle ? gripA : gripB;
        fallbackTipGripLocal = aIsHandle ? gripB : gripA;
        hasFallbackBladeBounds =
            (fallbackTipGripLocal - fallbackHandleGripLocal).sqrMagnitude > .0064f;

        if (!hasVertexExtremes && hasFallbackBladeBounds)
            bladeAxisInGripSpace =
                (fallbackTipGripLocal - fallbackHandleGripLocal).normalized;
    }

    // Approximates the mesh's long axis as the two most-distant vertices (a standard cheap
    // stand-in for full PCA) - unlike an axis-aligned bounding box, this finds the true elongated
    // direction/extent of the geometry even when it runs diagonally through the mesh's local
    // X/Y/Z axes instead of along one of them (true for the King Goblin sword, not for
    // StarterSword). handleEndLocal/tipEndLocal are cached once per equip and shared by both the
    // rotation (CacheBladeAxis) and position (AlignModelGripToHand) math so they can't disagree
    // on which end is the handle.
    void CacheExtremes(MeshFilter mf)
    {
        hasVertexExtremes = false;
        bladeMeshTransform = mf != null ? mf.transform : null;
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable)
            return;

        Vector3[] vertices = mf.sharedMesh.vertices;
        if (vertices == null || vertices.Length < 2)
            return;

        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < vertices.Length; i++)
            centroid += vertices[i];
        centroid /= vertices.Length;

        Vector3 farthestFromCentroid = vertices[0];
        float bestDist = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d = (vertices[i] - centroid).sqrMagnitude;
            if (d > bestDist) { bestDist = d; farthestFromCentroid = vertices[i]; }
        }

        Vector3 farthestFromThat = vertices[0];
        bestDist = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d = (vertices[i] - farthestFromCentroid).sqrMagnitude;
            if (d > bestDist) { bestDist = d; farthestFromThat = vertices[i]; }
        }

        if ((farthestFromThat - farthestFromCentroid).sqrMagnitude <= 0.0000001f)
            return;

        bool invert = currentWeaponData != null && currentWeaponData.invertGripAxis;
        handleEndLocal = invert ? farthestFromThat : farthestFromCentroid;
        tipEndLocal = invert ? farthestFromCentroid : farthestFromThat;
        hasVertexExtremes = true;
    }

    bool TryGetLocalBounds(Transform model, out Bounds bounds)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (renderers.Length == 0)
            return false;

        bool initializedBounds = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 local = model.parent.InverseTransformPoint(corner);
                if (!initializedBounds)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    initializedBounds = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return initializedBounds;
    }

    void CacheHand()
    {
        Animator bestAnimator = GetBestAnimator();
        if (bestAnimator != anim)
        {
            anim = bestAnimator;
            handTransform = null;
            CacheGripIK();
        }

        if (handTransform == null && anim != null)
            handTransform = anim.GetBoneTransform(handBone);
    }

    Animator GetBestAnimator()
    {
        if (ganzVisual == null)
            ganzVisual = GetComponent<GanzPlayerVisual>();
        if (ganzVisual != null && ganzVisual.VisualAnimator != null)
            return ganzVisual.VisualAnimator;
        return GetComponentInChildren<Animator>();
    }

    void NormalizeWeaponSize(GameObject weapon)
    {
        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float maxAllowedSize = CurrentMaxEquippedWeaponSize;
        if (maxSize > maxAllowedSize && maxSize > 0.01f)
            weapon.transform.localScale *= maxAllowedSize / maxSize;
    }

    public void DetachWeapon()
    {
        if (equippedWeapon != null)
        {
            Destroy(equippedWeapon);
        }

        equippedWeapon = null;
        weaponModel = null;
        currentWeaponCtrl = null;
        currentWeaponData = null;
        hasFallbackBladeBounds = false;
        offHandGripPoint = null;
        animationDrivenGrip = false;
        carriedOnBack = false;
        ResetBladeHistory();
        if (gripIK != null)
        {
            gripIK.target = null;
            gripIK.weight = 0f;
        }
    }

    public void HideWeapon()
    {
        if (equippedWeapon == null)
            return;

        CancelCarryTransition();
        EnsureBackSocket();
        if (backSocket == null)
        {
            equippedWeapon.SetActive(false);
            return;
        }

        carriedOnBack = true;
        UpdateRuntimeBackSocketPose();
        equippedWeapon.transform.SetParent(backSocket, false);
        equippedWeapon.transform.localPosition = Vector3.zero;
        equippedWeapon.transform.localRotation = Quaternion.identity;
        equippedWeapon.SetActive(true);

        if (gripIK != null)
        {
            gripIK.target = null;
            gripIK.weight = 0f;
        }
    }

    public void ShowWeapon()
    {
        if (equippedWeapon == null) return;

        CancelCarryTransition();
        CacheHand();
        Transform attachPoint = handTransform != null ? handTransform : transform;
        carriedOnBack = false;
        equippedWeapon.transform.SetParent(attachPoint, false);
        ApplyFixedGrip(equippedWeapon.transform, true);
        equippedWeapon.SetActive(true);
    }

    public void SetAnimationDrivenGrip(bool enabled)
    {
        animationDrivenGrip = enabled;
        if (!enabled || equippedWeapon == null)
            return;

        // NPC attacks do not own a CombatSystem, so they must explicitly opt out of the
        // world-up stabilizer. Keep only the grip offsets and let the animated hand bone
        // provide the complete swing rotation, exactly as it does during a player attack.
        equippedWeapon.transform.localPosition = CurrentHandPositionOffset;
        equippedWeapon.transform.localRotation = Quaternion.Euler(CurrentHandRotationOffset);
        UpdateOffHandGrip(equippedWeapon.transform, true);
    }

    public WeaponController GetCurrentWeapon() => currentWeaponCtrl;
    public bool HasWeaponEquipped() => equippedWeapon != null;

    public void ToggleWeaponVisibility()
    {
        // Imported draw/sheath clips can contain WeaponSwitch events. During the continuous
        // transfer below those events must not snap the sword to the opposite socket.
        if (equippedWeapon == null || weaponTransitioning) return;
        if (carriedOnBack || !equippedWeapon.activeSelf) ShowWeapon();
        else HideWeapon();
    }

    public bool IsCarriedOnBack => carriedOnBack;
    public bool IsCarryTransitioning => weaponTransitioning;

    public void AnimateWeaponToHand(float duration = .42f)
    {
        StartCarryTransition(true, duration);
    }

    public void AnimateWeaponToBack(float duration = .46f)
    {
        StartCarryTransition(false, duration);
    }

    public void CancelCarryTransition()
    {
        if (carryTransition != null)
            StopCoroutine(carryTransition);
        carryTransition = null;
        weaponTransitioning = false;
    }

    void StartCarryTransition(bool toHand, float duration)
    {
        if (equippedWeapon == null)
            return;
        CancelCarryTransition();
        carryTransition = StartCoroutine(
            CarryTransitionRoutine(toHand, Mathf.Max(.08f, duration)));
    }

    IEnumerator CarryTransitionRoutine(bool toHand, float duration)
    {
        EnsureBackSocket();
        CacheHand();
        if (backSocket == null || handTransform == null)
        {
            if (toHand) ShowWeapon();
            else HideWeapon();
            yield break;
        }

        weaponTransitioning = true;
        animationDrivenGrip = false;
        equippedWeapon.SetActive(true);

        if (gripIK != null)
        {
            gripIK.target = null;
            gripIK.weight = 0f;
        }

        float elapsed = 0f;
        bool changedParent = false;
        Vector3 transitionLocalPosition = Vector3.zero;
        Quaternion transitionLocalRotation = Quaternion.identity;
        float handContact = toHand ? .58f : .78f;

        if (toHand)
        {
            carriedOnBack = true;
            UpdateRuntimeBackSocketPose();
            equippedWeapon.transform.SetParent(backSocket, false);
            equippedWeapon.transform.localPosition = Vector3.zero;
            equippedWeapon.transform.localRotation = Quaternion.identity;
        }
        else
        {
            // During most of the sheath animation the sword remains rigidly attached to the
            // animated hand. The hand therefore carries it toward the back instead of the sword
            // travelling independently as if pulled by a magnet.
            carriedOnBack = false;
            equippedWeapon.transform.SetParent(handTransform, false);
            ApplyFixedGrip(equippedWeapon.transform, true);
        }

        while (elapsed < duration && equippedWeapon != null)
        {
            elapsed += Time.deltaTime;
            float linear = Mathf.Clamp01(elapsed / duration);
            UpdateRuntimeBackSocketPose();
            if (toHand)
            {
                // Leave the sword fixed on the back while the empty hand reaches for the grip.
                // Once contact is made, parent it to that hand and let the remaining animation
                // pull both hand and blade forward together.
                if (linear < handContact)
                {
                    equippedWeapon.transform.localPosition = Vector3.zero;
                    equippedWeapon.transform.localRotation = Quaternion.identity;
                    yield return null;
                    continue;
                }
                if (!changedParent)
                {
                    equippedWeapon.transform.SetParent(handTransform, true);
                    transitionLocalPosition =
                        equippedWeapon.transform.localPosition;
                    transitionLocalRotation =
                        equippedWeapon.transform.localRotation;
                    changedParent = true;
                }
                float phase = Mathf.InverseLerp(handContact, 1f, linear);
                float eased = phase * phase * (3f - 2f * phase);
                equippedWeapon.transform.localPosition = Vector3.Lerp(
                    transitionLocalPosition, CurrentHandPositionOffset, eased);
                equippedWeapon.transform.localRotation = Quaternion.Slerp(
                    transitionLocalRotation,
                    Quaternion.Euler(CurrentHandRotationOffset), eased);
            }
            else
            {
                // The hand owns the sword until it reaches the shoulder. Only the last short
                // portion settles the grip precisely into the back socket.
                if (linear < handContact)
                {
                    yield return null;
                    continue;
                }
                if (!changedParent)
                {
                    equippedWeapon.transform.SetParent(backSocket, true);
                    transitionLocalPosition =
                        equippedWeapon.transform.localPosition;
                    transitionLocalRotation =
                        equippedWeapon.transform.localRotation;
                    changedParent = true;
                }
                float phase = Mathf.InverseLerp(handContact, 1f, linear);
                float eased = phase * phase * (3f - 2f * phase);
                equippedWeapon.transform.localPosition = Vector3.Lerp(
                    transitionLocalPosition, Vector3.zero, eased);
                equippedWeapon.transform.localRotation = Quaternion.Slerp(
                    transitionLocalRotation, Quaternion.identity, eased);
            }
            yield return null;
        }

        if (equippedWeapon != null)
        {
            if (toHand)
            {
                carriedOnBack = false;
                equippedWeapon.transform.SetParent(handTransform, false);
                ApplyFixedGrip(equippedWeapon.transform, true);
            }
            else
            {
                carriedOnBack = true;
                UpdateRuntimeBackSocketPose();
                equippedWeapon.transform.SetParent(backSocket, false);
                equippedWeapon.transform.localPosition = Vector3.zero;
                equippedWeapon.transform.localRotation = Quaternion.identity;
            }
        }
        weaponTransitioning = false;
        carryTransition = null;
    }

    void EnsureBackSocket()
    {
        if (backSocket != null)
            return;

        GameObject socketObject = new GameObject("WeaponBackSocket_Runtime");
        backSocket = socketObject.transform;
        backSocket.SetParent(transform, false);
        runtimeBackSocket = true;
        UpdateRuntimeBackSocketPose();
    }

    void UpdateRuntimeBackSocketPose()
    {
        if (!runtimeBackSocket || backSocket == null)
            return;

        CacheHand();
        Transform chest = anim != null
            ? anim.GetBoneTransform(HumanBodyBones.Chest) ??
              anim.GetBoneTransform(HumanBodyBones.UpperChest)
            : null;
        Vector3 chestPosition = chest != null
            ? chest.position
            : transform.position + transform.up * 1.15f;

        // The grip origin is close to the handle. Put it on the lower-left
        // portion of the back and point the real cached blade axis toward the
        // opposite shoulder. This works for differently authored sword meshes.
        backSocket.position = chestPosition - transform.forward * .2f -
                              transform.up * .36f - transform.right * .16f;
        Vector3 desiredBladeDirection =
            (transform.up + transform.right * .34f).normalized;
        Vector3 localBladeAxis = bladeAxisInGripSpace.sqrMagnitude > .001f
            ? bladeAxisInGripSpace.normalized
            : Vector3.forward;
        backSocket.rotation = Quaternion.FromToRotation(
            localBladeAxis, desiredBladeDirection);
    }

    Vector3 CurrentHandPositionOffset =>
        currentWeaponData != null && currentWeaponData.useCustomEquippedVisual
            ? currentWeaponData.customHandPositionOffset
            : handPositionOffset;

    Vector3 CurrentHandRotationOffset =>
        currentWeaponData != null && currentWeaponData.useCustomEquippedVisual
            ? currentWeaponData.customHandRotationOffset
            : handRotationOffset;

    float CurrentGripAnchorPercent =>
        currentWeaponData != null && currentWeaponData.useCustomEquippedVisual
            ? currentWeaponData.customGripAnchorPercent
            : 0.16f;

    float CurrentMaxEquippedWeaponSize =>
        currentWeaponData != null && currentWeaponData.useCustomEquippedVisual && currentWeaponData.customMaxEquippedWeaponSize > 0.01f
            ? currentWeaponData.customMaxEquippedWeaponSize
            : maxEquippedWeaponSize;

    void ResetBladeHistory()
    {
        hasPreviousTipPosition = false;
        currentTipVelocity = Vector3.zero;
        bladeHistoryWriteIndex = 0;
        bladeHistoryCount = 0;
    }
}
