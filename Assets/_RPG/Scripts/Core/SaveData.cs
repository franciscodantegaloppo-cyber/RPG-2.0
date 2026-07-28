using System;
using System.Collections.Generic;

[Serializable]
public class PlayerSaveData
{
    public float health;
    public float stamina;
    public SerializableVector3 position;
    public int currentScene;
    public List<ItemSaveEntry> inventory = new List<ItemSaveEntry>();
    public List<string> equippedItemIDs = new List<string>();
}

[Serializable]
public class ItemSaveEntry
{
    public string itemID;
    public int quantity;
}

[Serializable]
public class SerializableVector3
{
    public float x, y, z;

    public SerializableVector3(UnityEngine.Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(x, y, z);
}
