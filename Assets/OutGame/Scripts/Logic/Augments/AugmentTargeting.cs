using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Augments
{
    /// <summary>
    /// 증강이 특정 병과에 적용되는지 판정하는 공용 규칙(§5.6, §4-27): StatAugment는 병과 무관 전군
    /// 적용, ItemAugment는 targetArmyClass가 일치할 때만. ArmyStatCalculator(스탯 배율)와 스킬 강화
    /// 횟수 집계(DeploymentState) 등 여러 곳이 이 판정을 공유해야 한다 — 각자 판정하면 한쪽만 고치고
    /// 다른 쪽을 놓치는 불일치 버그가 난다(2026-07-26, 이 세션에서 반복 확인된 패턴).
    /// </summary>
    public static class AugmentTargeting
    {
        public static bool AppliesToClass(AugmentData augment, ArmyClass armyClass)
        {
            if (augment == null) throw new ArgumentNullException(nameof(augment));

            return augment.category == AugmentCategory.StatAugment
                || (augment.category == AugmentCategory.ItemAugment && augment.targetArmyClass == armyClass);
        }

        /// <summary>선택된 증강 중 이 병과에 적용되면서 effectType이 일치하는 개수를 센다 — 예: 장군 스킬
        /// 강화(GeneralSkillUpgrade) 증강을 몇 번 선택했는지 (§4-23, 효과 자체는 인게임 책임).</summary>
        public static int CountMatching(
            ArmyClass armyClass, AugmentEffectType effectType, IReadOnlyList<AugmentData> selectedAugments)
        {
            if (selectedAugments == null) throw new ArgumentNullException(nameof(selectedAugments));

            int count = 0;
            foreach (AugmentData augment in selectedAugments)
            {
                // AppliesToClass를 먼저 호출해야 null 가드가 실제로 먼저 걸린다 — 반대 순서면
                // augment.effectType 접근이 가드보다 먼저 실행돼 NullReferenceException이 난다.
                if (AppliesToClass(augment, armyClass) && augment.effectType == effectType)
                    count++;
            }
            return count;
        }
    }
}
