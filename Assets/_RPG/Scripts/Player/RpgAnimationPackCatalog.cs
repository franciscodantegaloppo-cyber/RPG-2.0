using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Animation Pack Catalog")]
public sealed class RpgAnimationPackCatalog : ScriptableObject
{
    public AnimationClip[] clips;
    public GameObject shieldPrefab;

    public AnimationClip Find(string exactName)
    {
        if (clips == null || string.IsNullOrEmpty(exactName)) return null;
        foreach (AnimationClip clip in clips)
            if (clip != null && clip.name == exactName)
                return clip;
        return null;
    }
}
