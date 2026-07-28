using UnityEngine;

// Plays the imported NamuFX Simple Stylized Slash effect at the exact animation
// event that also resolves the sword hit. It is visual-only and cannot create
// extra damage, collisions or physics contacts.
public static class StylizedSwordSlashVfx
{
    const string HorizontalResource = "VFX/SwordSlashes/Slash_A";
    const string VerticalResource = "VFX/SwordSlashes/Slash_B_Vertical";
    const string VisualProfileResource = "VFX/SwordSlashes/SwordSlashVisualProfile";

#if UNITY_EDITOR
    const string HorizontalEditorPath =
        "Assets/NamuFX/Simple Stylized Slash vol2/Prefabs/Slash_A.prefab";
    const string VerticalEditorPath =
        "Assets/NamuFX/Simple Stylized Slash vol2/Prefabs/Slash_B_vertical Variant.prefab";
#endif

    static bool TryBuildSwingOrientation(WeaponSocket socket, out Vector3 position,
        out Quaternion rotation, out bool vertical, out float measuredBladeLength,
        out float measuredSweep)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        vertical = false;
        measuredBladeLength = 0f;
        measuredSweep = 0f;

        if (socket == null ||
            !socket.TryGetRecentBladeSwing(out position, out Vector3 bladeDir,
                out Vector3 swingDir, out measuredBladeLength, out measuredSweep))
            return false;

        // Cross order matters and is not arbitrary. SlashMesh is authored 2.00 x 1.20 x 0.06,
        // so its +Z is the arc's plane normal and its +X is the direction the arc sweeps.
        // LookRotation(f, up) sends +Z to f and +X to cross(up, f); with up = bladeDir that is
        // cross(bladeDir, planeNormal). Taking planeNormal = cross(bladeDir, swingDir) makes
        // that resolve to -swingDir (vector triple product, since swingDir ⟂ bladeDir), i.e. the
        // arc sweeps against the swing - the mirrored slash. Crossing the other way flips it so
        // +X lands on +swingDir and the arc follows the blade.
        Vector3 planeNormal = Vector3.Cross(swingDir, bladeDir);
        if (planeNormal.sqrMagnitude < .0001f)
            return false;
        planeNormal.Normalize();

        rotation = Quaternion.LookRotation(planeNormal, bladeDir);

        // Always the horizontal prefab on this path. The basis above already aims the arc in 3D
        // for any swing direction, vertical included, so no pre-rotated variant is needed - and
        // Slash_B_vertical must NOT be used here: its mesh nodes carry a baked (0, 90, 291.59)
        // local rotation, so the root's axes do not map to the arc the way this basis assumes.
        // The vertical variant stays reserved for the geometry-less fallback below.
        vertical = false;
        return true;
    }

    public static GameObject Play(Transform player, WeaponSocket weaponSocket, int attackAction,
        float rangeMultiplier, float animationSpeed, bool autoDestroy = true)
    {
        if (player == null) return null;

        // Derive the slash from the blade's real swept plane. bladeDir is the blade's long axis
        // and swingDir is where the tip is actually travelling, so their cross product is the
        // normal of the plane the sword is cutting through. Orienting the effect against that
        // basis makes it track the animation instead of a fixed dead-ahead guess - including the
        // left/right handedness, which flips automatically with the sign of the velocity.
        bool vertical;
        float bladeLength;
        float measuredSweep;
        if (!TryBuildSwingOrientation(weaponSocket, out Vector3 position,
                out Quaternion rotation, out vertical, out bladeLength,
                out measuredSweep))
        {
            vertical = false;

            // Some imported attack clips contain too little hand motion around their hit event
            // to reconstruct a stable velocity. That must not send the effect back to a guessed
            // point in front of the player: the socket still knows the real handle and tip.
            // Anchor the radial NamuFX root at the grip and align its local blade axis with the
            // actual rendered sword. This is deterministic for every weapon pivot/orientation.
            if (weaponSocket != null && weaponSocket.HasBladeGeometry)
            {
                Vector3 handle = weaponSocket.CurrentBladeHandleWorldPosition;
                Vector3 tip = weaponSocket.CurrentBladeTipWorldPosition;
                Vector3 blade = tip - handle;
                bladeLength = blade.magnitude;
                Vector3 bladeDir = bladeLength > .001f ? blade / bladeLength : Vector3.up;
                Vector3 planeNormal = Vector3.ProjectOnPlane(player.forward, bladeDir);
                if (planeNormal.sqrMagnitude < .0001f)
                    planeNormal = Vector3.ProjectOnPlane(player.right, bladeDir);
                planeNormal.Normalize();

                position = handle + bladeDir * bladeLength * .06f;
                rotation = Quaternion.LookRotation(planeNormal, bladeDir);
                measuredSweep = 0f;
            }
            else
            {
                // Last-resort path for a weapon prefab with no MeshFilter at all.
                float distance = Mathf.Clamp(.72f * rangeMultiplier, .58f, 1.15f);
                position = player.position + Vector3.up * 1.02f +
                           player.forward * distance;
                rotation = Quaternion.LookRotation(player.forward, Vector3.up);
                bladeLength = 1f;
                measuredSweep = 0f;
            }
        }

        SwordSlashVisualProfile profile =
            Resources.Load<SwordSlashVisualProfile>(VisualProfileResource);
        if (profile != null)
        {
            position += rotation * profile.localPositionOffset;
            rotation *= Quaternion.Euler(profile.rotationCorrection);
        }

        GameObject prefab = Resources.Load<GameObject>(
            vertical ? VerticalResource : HorizontalResource);
#if UNITY_EDITOR
        // Keeps the effect testable while Unity is waiting to consume the editor
        // setup marker. Player builds use only the Resources copies.
        if (prefab == null)
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                vertical ? VerticalEditorPath : HorizontalEditorPath);
#endif
        if (prefab == null)
        {
            Debug.LogWarning("[StylizedSwordSlashVfx] No se encontró el prefab del Slash Pack.");
            return null;
        }

        GameObject slash = Object.Instantiate(prefab, position, rotation);
        slash.name = "PlayerSwordSlash_" + attackAction;
        // Slash_A's mesh spans roughly two world units around its root. Scale it from the real
        // handle-to-tip length so short swords no longer create a huge detached arc and long
        // swords still reach their visible tip. A small sweep contribution keeps broad attacks
        // slightly fuller without letting a noisy sample explode the effect.
        float trajectoryScale = bladeLength * .68f + Mathf.Min(measuredSweep, bladeLength) * .12f;
        float profileScale = profile != null ? Mathf.Max(.1f, profile.scaleMultiplier) : 1f;
        float visualScale = Mathf.Clamp(trajectoryScale * rangeMultiplier, .58f, 1.28f) *
                            profileScale;
        slash.transform.localScale = Vector3.one * visualScale;

        foreach (Collider collider in slash.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in slash.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        float speed = Mathf.Clamp(animationSpeed, .55f, 2.6f);
        foreach (ParticleSystem particles in
                 slash.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.simulationSpeed *= speed;
            particles.Play(true);
        }

        if (autoDestroy)
            Object.Destroy(slash, 2.8f / speed + .35f);
        return slash;
    }
}
