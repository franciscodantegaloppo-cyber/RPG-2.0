using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/UI/Skill Icon Catalog")]
public sealed class SkillIconCatalog : ScriptableObject
{
    [SerializeField] List<Sprite> icons = new List<Sprite>();

    public List<Sprite> Icons => icons;

    public Sprite ForNode(string nodeId)
    {
        if (icons == null || icons.Count == 0)
            return null;

        string id = (nodeId ?? string.Empty).ToLowerInvariant();
        int variation = NumericSuffix(id);
        if (id == "fire" || id.StartsWith("fire_"))
            return At(34 + variation % 11);
        if (id == "ice" || id.StartsWith("ice_"))
            return At(46 + variation % 14);
        if (id.StartsWith("lightning"))
            return At(112 - variation % 3);
        if (id.StartsWith("wind"))
            return At(49 + variation % 11);
        if (id.StartsWith("sword"))
            return At(new[] { 4, 65, 66, 74, 100 }[variation % 5]);
        if (id.StartsWith("magic"))
            return At(new[] { 1, 6, 14, 16, 25, 53, 64, 67 }[variation % 8]);
        if (id.StartsWith("gather"))
            return At(28 + variation % 6);
        if (id.StartsWith("dragon"))
            return At(new[] { 17, 42, 43, 87, 97, 98, 119, 120 }[variation % 8]);
        if (id.StartsWith("athletics") || id == "speed")
            return At(new[] { 62, 63, 70, 73, 75, 76, 83 }[variation % 7]);
        if (id.StartsWith("dark") || id == "resist")
            return At(new[] { 9, 10, 16, 45, 84, 89, 92, 116 }[variation % 8]);
        if (id.StartsWith("defense") || id == "all_resist" ||
            id.StartsWith("element"))
            return At(new[] { 5, 26, 35, 68, 101, 111, 114 }[variation % 7]);

        // Stable fallback: the same future skill receives the same icon in all builds.
        uint hash = 2166136261;
        for (int i = 0; i < id.Length; i++)
        {
            hash ^= id[i];
            hash *= 16777619;
        }
        return icons[(int)(hash % (uint)icons.Count)];
    }

    Sprite At(int oneBasedIndex)
        => icons[Mathf.Clamp(oneBasedIndex - 1, 0, icons.Count - 1)];

    static int NumericSuffix(string id)
    {
        int split = id.LastIndexOf('_');
        return split >= 0 && int.TryParse(id.Substring(split + 1), out int value)
            ? value
            : 0;
    }
}
