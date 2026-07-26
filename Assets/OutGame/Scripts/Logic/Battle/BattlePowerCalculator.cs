using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 전투력 계산 (§4-22): (Σ병사 수 × 병과 계수 + 장군 보정) × 배율. 배치 화면 양 진영 하단에 표시.
    /// 업그레이드/증강 배율은 ArmyInfoPopup과 같은 ArmyStatCalculator를 공유해 두 화면의 수치가
    /// 어긋나지 않게 한다(2026-07-26 — 이전엔 이 계산기가 upgradeLevel을 전혀 안 읽어 업그레이드를
    /// 해도 전투력이 안 오르는 버그가 있었다). 장군·유닛에 같은 배율을 쓰는 이유: 같은 군대·같은
    /// 병과이므로 업그레이드도 증강도 둘에 동일하게 적용된다(§4-26, §4-27 — 장군도 증강 혜택을
    /// 받도록 2026-07-26 사용자 확정, ArmyInfoPopup과 동일 원칙).
    /// </summary>
    public static class BattlePowerCalculator
    {
        public static float Calculate(
            IReadOnlyList<DeployedArmy> armies,
            BattlePowerConfig config,
            IReadOnlyDictionary<string, ArmyData> armyDefs,
            IReadOnlyList<AugmentData> selectedAugments)
        {
            if (armies == null) throw new ArgumentNullException(nameof(armies));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (armyDefs == null) throw new ArgumentNullException(nameof(armyDefs));
            if (selectedAugments == null) throw new ArgumentNullException(nameof(selectedAugments));

            float total = 0f;
            foreach (DeployedArmy army in armies)
            {
                if (!armyDefs.TryGetValue(army.armyDefId, out ArmyData def))
                    throw new ArgumentException($"정의되지 않은 ArmyDefinition: {army.armyDefId}");

                // "전투력"은 스탯 하나가 아니라 종합 지표이므로 Health/Attack/Defense 세 배율의 평균을
                // 쓴다 — 공격력만 대표로 쓰면 체력/방어력 증강을 골라도 전투력 화면 수치가 그대로인
                // 것처럼 보이는 불일치가 생긴다(코드 리뷰 지적).
                float multiplier = (
                    ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, army.armyClass, AugmentStat.Health, selectedAugments)
                    + ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, army.armyClass, AugmentStat.Attack, selectedAugments)
                    + ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, army.armyClass, AugmentStat.Defense, selectedAugments)
                ) / 3f;

                total += (army.soldierCount * config.WeightOf(army.armyClass) + def.generalPower) * multiplier;
            }

            return total;
        }
    }
}
