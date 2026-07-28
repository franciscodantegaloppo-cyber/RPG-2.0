#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SkillTreeImageSetup
{
    static SkillTreeImageSetup()
    {
        EditorApplication.delayCall += CopyImage;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall += CopyImage;
    }
    static void CopyImage()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        const string folder="Assets/_RPG/Resources/UI";
        const string destination=folder+"/arbol de habilidades mu.jpg";
        if(!AssetDatabase.IsValidFolder("Assets/_RPG/Resources"))AssetDatabase.CreateFolder("Assets/_RPG","Resources");
        if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_RPG/Resources","UI");
        if(AssetDatabase.LoadAssetAtPath<Texture2D>(destination)==null)
            AssetDatabase.CopyAsset("Assets/_RPG/Scripts/Combat/arbol de habilidades mu.jpg",destination);
        const string animationFolder="Assets/_RPG/Resources/SpellAnimations";
        if(!AssetDatabase.IsValidFolder(animationFolder))AssetDatabase.CreateFolder("Assets/_RPG/Resources","SpellAnimations");
        CopyAnimation("HumanM@MagicAttackDirect1H01_R - Cast.fbx");
        CopyAnimation("HumanM@MagicAttackDirect1H01_L - Cast.fbx");
        CopyAnimation("HumanM@MagicAttackDirect2H01 - Cast.fbx");
        CopyAnimation("HumanM@MagicAttackOmni01 - Cast.fbx");
        CopyAnimation("HumanM@MagicAttackOmni01.fbx");
        const string iconFolder="Assets/_RPG/Resources/SpellIcons";
        if(!AssetDatabase.IsValidFolder(iconFolder))AssetDatabase.CreateFolder("Assets/_RPG/Resources","SpellIcons");
        CopySpellIcon("Human_Spell_Fireball_Texture.tga","Fireball.tga");
        CopySpellIcon("Human_Spell_IceSpike_Texture.png","Ice.png");
        CopySpellIcon("Human_Spell_Lightning_Texture.png","Lightning.png");
        CopySpellIcon("Human_Spell_Seal.png","Heal.png");
        CopySpellIcon("Human_Spell_Shockwave_Ground_Texture.png","Shockwave.png");
        const string spellFolder="Assets/_RPG/Resources/PlayerSpells";
        if(!AssetDatabase.IsValidFolder(spellFolder))AssetDatabase.CreateFolder("Assets/_RPG/Resources","PlayerSpells");
        CopySpellPrefab("Human_Spell_Fireball.prefab","Fireball.prefab");
        CopySpellPrefab("Human_Spell_Ice.prefab","Ice.prefab");
        CopySpellPrefab("Human_Spell_LightningStrike.prefab","Lightning.prefab");
        CopySpellPrefab("Human_Spell_Heal.prefab","Heal.prefab");
        CopySpellPrefab("Human_Spell_Shockwave_Explosion.prefab","Shockwave.prefab");
        const string handSpellFolder="Assets/_RPG/Resources/PlayerSpellHands";
        if(!AssetDatabase.IsValidFolder(handSpellFolder))AssetDatabase.CreateFolder("Assets/_RPG/Resources","PlayerSpellHands");
        CopyHandSpellPrefab("Human_SpellAura_Fire.prefab","Fire.prefab");
        CopyHandSpellPrefab("Human_SpellAura_Ice.prefab","Ice.prefab");
        CopyHandSpellPrefab("Human_SpellAura_Lightning.prefab","Lightning.prefab");
        CopyHandSpellPrefab("Human_SpellAura_Heal.prefab","Heal.prefab");
        CopyHandSpellPrefab("Human_SpellAura_Shockwave_Ground.prefab","Shockwave.prefab");
    }

    static void CopyAnimation(string name)
    {
        const string source="Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/";
        string path = name.Contains("Omni") ? source+"Omnidirectional/"+name : source+"Directional/"+name;
        string destination="Assets/_RPG/Resources/SpellAnimations/"+name;
        if(AssetDatabase.LoadMainAssetAtPath(destination)!=null)return;
        if(AssetDatabase.CopyAsset(path,destination))
            AssetDatabase.ImportAsset(destination,ImportAssetOptions.ForceUpdate);
        else
            Debug.LogError("No se pudo importar la animacion de hechizo: "+path);
    }

    static void CopySpellIcon(string sourceName,string destinationName)
    {
        const string sourceFolder="Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Spellcasting Animations/Textures/";
        string destination="Assets/_RPG/Resources/SpellIcons/"+destinationName;
        if(AssetDatabase.LoadMainAssetAtPath(destination)==null)AssetDatabase.CopyAsset(sourceFolder+sourceName,destination);
    }

    static void CopySpellPrefab(string sourceName,string destinationName)
    {
        const string sourceFolder="Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Spellcasting Animations/Prefabs/Spells/";
        string destination="Assets/_RPG/Resources/PlayerSpells/"+destinationName;
        if(AssetDatabase.LoadMainAssetAtPath(destination)==null)AssetDatabase.CopyAsset(sourceFolder+sourceName,destination);
    }

    static void CopyHandSpellPrefab(string sourceName,string destinationName)
    {
        const string sourceFolder="Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Spellcasting Animations/Prefabs/Spells/";
        string destination="Assets/_RPG/Resources/PlayerSpellHands/"+destinationName;
        if(AssetDatabase.LoadMainAssetAtPath(destination)==null)AssetDatabase.CopyAsset(sourceFolder+sourceName,destination);
    }
}
#endif
