using System;
using System.Collections.Generic;

namespace GameCraftLab.UnifiedInventorySystem
{
    // Plain serializable POCOs — JsonUtility friendly. Keep them dumb; logic lives in InventorySerializer.

    [Serializable]
    public class ItemSaveData
    {
        public string itemId;
        public string instanceId;
        public int stackCount = 1;
        public int x;
        public int y;
        public int rotation;
    }

    [Serializable]
    public class ContainerSaveData
    {
        public string containerId;
        public List<ItemSaveData> items = new List<ItemSaveData>();
    }

    [Serializable]
    public class InventorySaveData
    {
        public int version = InventorySerializer.CurrentVersion;
        public List<ContainerSaveData> containers = new List<ContainerSaveData>();
    }
}
