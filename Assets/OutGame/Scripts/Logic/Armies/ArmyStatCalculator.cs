using System;
using System.Collections.Generic;
using OutGame.Logic.Augments;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 군대 업그레이드(§4-26) + 증강(§4-27) 효과를 합산한 최종 스탯 배율. 스탯을 표시·계산하는
    /// 모든 곳이 이 헬퍼 하나를 공유해야 한다 — 각자 계산하면 한쪽만 고치고 다른 쪽을 놓치는
    /// 불일치 버그가 난다(2026-07-19, 배치 UI와 휴식 방이 각자 계산하다 어긋났던 사례와 동일 원칙).
    /// </summary>
    public static class ArmyStatCalculator
    {
        /// <summary>
        /// 업그레이드 배율(1 + 레벨×10%)에, 이 병과·스탯에 적용되는 증강의 statBoostPercent를 전부
        /// 가산한다. StatAugment는 병과 무관 전군 적용, ItemAugment는 targetArmyClass가 일치할 때만.
        /// GeneralSkillUpgrade 타입 증강은 수치가 없으므로(§4-23과 동일 원칙 — 인게임 책임) 대상에서 제외.
        /// </summary>
        public static float GetStatMultiplier(
            ArmyInstance army,
            ArmyClass armyClass,
            AugmentStat stat,
            IReadOnlyList<AugmentData> selectedAugments)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            if (selectedAugments == null) throw new ArgumentNullException(nameof(selectedAugments));

            float multiplier = ArmyUpgradeService.GetStatMultiplier(army.upgradeLevel);
            foreach (AugmentData augment in selectedAugments)
            {
                if (augment.effectType != AugmentEffectType.StatBoost) continue;
                if (augment.targetStat != stat) continue;

                bool applies = augment.category == AugmentCategory.StatAugment
                    || (augment.category == AugmentCategory.ItemAugment && augment.targetArmyClass == armyClass);
                if (applies) multiplier += augment.statBoostPercent;
            }

            return multiplier;
        }
    }
}
