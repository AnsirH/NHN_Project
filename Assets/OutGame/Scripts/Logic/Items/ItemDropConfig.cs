using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Items
{
    /// <summary>병과별 전투 승리 아이템 드롭 확률 (§4-28 초안 — 밸런스 튜닝 전).</summary>
    [Serializable]
    public class ItemDropConfig
    {
        public float archerDropChance = 0.25f;
        public float shieldmanDropChance = 0.25f;

        public void Validate()
        {
            if (archerDropChance < 0f || archerDropChance > 1f)
                throw new ArgumentException($"archerDropChance는 0~1 사이여야 합니다. 현재: {archerDropChance}");
            if (shieldmanDropChance < 0f || shieldmanDropChance > 1f)
                throw new ArgumentException($"shieldmanDropChance는 0~1 사이여야 합니다. 현재: {shieldmanDropChance}");
        }

        public float ChanceFor(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.Archer: return archerDropChance;
                case ArmyClass.Shieldman: return shieldmanDropChance;
                default:
                    throw new ArgumentException($"{armyClass} 병과의 드롭 확률이 아직 정의되지 않았습니다.");
            }
        }
    }
}
