using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public class SkillTreeProgress : MonoBehaviour
{
    [Serializable] public class NodeLevel { public string id; public int level; }
    [SerializeField] List<NodeLevel> nodes = new List<NodeLevel>();
    PlayerStats stats;
    public event Action OnChanged;

    void Awake() => stats = GetComponent<PlayerStats>();
    public int GetLevel(string id) => nodes.Find(n => n.id == id)?.level ?? 0;
    public void GrantLevel(string id, int minimumLevel)
    {
        if (string.IsNullOrEmpty(id) || minimumLevel <= 0) return;
        NodeLevel node = nodes.Find(n => n.id == id);
        if (node == null)
        {
            node = new NodeLevel { id = id, level = minimumLevel };
            nodes.Add(node);
        }
        else
            node.level = Mathf.Max(node.level, minimumLevel);
        stats?.NotifySkillTreeChanged();
        OnChanged?.Invoke();
    }
    // All branches are visible and directly selectable. The image itself still communicates
    // the intended paths, but the player is free to build in any order.
    public bool CanAccess(string parentId) => true;
    public bool TryUpgrade(string id, string parentId)
    {
        if (!CanAccess(parentId) || stats == null || !stats.TrySpendSkillPoint()) return false;
        NodeLevel node = nodes.Find(n => n.id == id);
        if (node == null) { node = new NodeLevel { id = id }; nodes.Add(node); }
        if (node.level >= 10) { stats.AddSkillPoints(1); return false; }
        node.level++;
        stats.NotifySkillTreeChanged();
        OnChanged?.Invoke();
        return true;
    }
    public static float GetMagicBonus(Transform player)
    {
        SkillTreeProgress tree = player != null ? player.GetComponent<SkillTreeProgress>() : null;
        return tree == null ? 0f : (tree.Sum("magic") + tree.GetLevel("fire") + tree.GetLevel("ice")) * .035f;
    }

    public static float GetPhysicalAttackBonus(Transform player)
    {
        SkillTreeProgress tree = player != null ? player.GetComponent<SkillTreeProgress>() : null;
        return tree == null ? 0f : (tree.Sum("sword") + tree.Sum("dragon")) * .02f;
    }

    public static float GetDefenseBonus(Transform player)
    {
        SkillTreeProgress tree = player != null ? player.GetComponent<SkillTreeProgress>() : null;
        return tree == null ? 0f : tree.GetLevel("core") + tree.Sum("resist") + tree.Sum("defense") + tree.Sum("all_resist") +
            tree.Sum("fire_res") + tree.Sum("lightning") + tree.Sum("ice_res") + tree.Sum("wind");
    }

    public static float GetMoveSpeedBonus(Transform player)
    {
        SkillTreeProgress tree = player != null ? player.GetComponent<SkillTreeProgress>() : null;
        return tree == null ? 0f : (tree.Sum("athletics") + tree.Sum("speed")) * .01f;
    }

    public static float GetGoldBonus(Transform player)
    {
        SkillTreeProgress tree = player != null ? player.GetComponent<SkillTreeProgress>() : null;
        return tree == null ? 0f : tree.Sum("gather") * .01f;
    }

    public static float GetStaminaBonus(Transform player)
    {
        SkillTreeProgress tree = player != null ? player.GetComponent<SkillTreeProgress>() : null;
        return tree == null ? 0f : tree.Sum("vitality") * .01f;
    }

    public bool IsSpellUnlocked(int spellIndex)
    {
        return spellIndex switch
        {
            0 => GetLevel("fire") > 0,
            1 => GetLevel("ice") > 0,
            2 => GetLevel("magic_1") > 0,
            3 => GetLevel("magic") >= 5,
            4 => GetLevel("magic") >= 10,
            _ => false
        };
    }

    int Sum(string prefix)
    {
        int total = 0;
        foreach (NodeLevel node in nodes)
            if (node != null && (node.id == prefix || node.id.StartsWith(prefix + "_"))) total += node.level;
        return total;
    }
}
