using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "UnlockItemConfig", menuName = "FrogGame/UnlockItemConfig", order = 0)]
public class UnlockItemConfig : ScriptableObject {
    public List<UnlockItem> unlockItems;
}

[System.Serializable]
public class UnlockItem {
    public string itemName;
    public int unlockLevel;
    public Sprite itemIcon;
}