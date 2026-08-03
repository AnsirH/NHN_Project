using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Events
{
    /// <summary>보상 1건 — type에 따라 armyClass/itemId/goldAmount 중 해당 필드만 유효.</summary>
    [Serializable]
    public class RewardGrant
    {
        public RewardType type;
        public ArmyClass armyClass;
        public string itemId;
        public int goldAmount;
    }
}
