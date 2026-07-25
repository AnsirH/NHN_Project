using System;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Augments
{
    /// <summary>
    /// 증강 데이터 (순수 POCO — AugmentDefinition SO가 이 데이터를 채워 넘긴다). 증강 방(§4-27)에서
    /// 3개 중 1개를 선택하면 런 전체에 영구 적용된다 — 아이템 증강은 대상 병과 군대에만,
    /// 스탯 증강은 보유한 모든 군대에 적용(§5.6).
    /// </summary>
    [Serializable]
    public class AugmentData
    {
        public string id;
        public string displayName;
        public string description;
        public AugmentCategory category;
        public ArmyClass targetArmyClass = ArmyClass.None; // category == ItemAugment일 때만 사용
        public AugmentEffectType effectType;
        public AugmentStat targetStat;      // effectType == StatBoost일 때만 사용
        public float statBoostPercent;      // effectType == StatBoost일 때만 사용 (0.15 = +15%)

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("AugmentData.id가 비어 있습니다.");
            if (category == AugmentCategory.ItemAugment && targetArmyClass == ArmyClass.None)
                throw new ArgumentException($"'{id}': 아이템 증강은 targetArmyClass가 지정돼야 합니다.");
            if (effectType == AugmentEffectType.StatBoost && statBoostPercent <= 0f)
                throw new ArgumentException($"'{id}': 스탯 증강은 statBoostPercent가 0보다 커야 합니다.");
        }
    }
}
