using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Items
{
    /// <summary>병과별 전투 승리 아이템 드롭 확률 (§4-28 초안 — 밸런스 튜닝 전).</summary>
    [Serializable]
    public class ItemDropConfig
    {
        public float archerDropChance = 0.25f;
        public float warriorDropChance = 0.25f;
        public float hunterDropChance = 0.25f; // 2026-07-26: 4병과 확장(§4-28)
        public float assassinDropChance = 0.25f;

        /// <summary>필드 단위 얕은 복사 — 호출자가 반환값을 변형해도 원본(에셋 등)에 영향이 없도록 한다.</summary>
        public ItemDropConfig Clone() => new ItemDropConfig
        {
            archerDropChance = archerDropChance,
            warriorDropChance = warriorDropChance,
            hunterDropChance = hunterDropChance,
            assassinDropChance = assassinDropChance,
        };

        public void Validate()
        {
            if (archerDropChance < 0f || archerDropChance > 1f)
                throw new ArgumentException($"archerDropChance는 0~1 사이여야 합니다. 현재: {archerDropChance}");
            if (warriorDropChance < 0f || warriorDropChance > 1f)
                throw new ArgumentException($"warriorDropChance는 0~1 사이여야 합니다. 현재: {warriorDropChance}");
            if (hunterDropChance < 0f || hunterDropChance > 1f)
                throw new ArgumentException($"hunterDropChance는 0~1 사이여야 합니다. 현재: {hunterDropChance}");
            if (assassinDropChance < 0f || assassinDropChance > 1f)
                throw new ArgumentException($"assassinDropChance는 0~1 사이여야 합니다. 현재: {assassinDropChance}");
        }

        public float ChanceFor(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.Archer: return archerDropChance;
                case ArmyClass.Warrior: return warriorDropChance;
                case ArmyClass.Hunter: return hunterDropChance;
                case ArmyClass.Assassin: return assassinDropChance;
                default:
                    throw new ArgumentException($"{armyClass} 병과의 드롭 확률이 아직 정의되지 않았습니다.");
            }
        }
    }
}
