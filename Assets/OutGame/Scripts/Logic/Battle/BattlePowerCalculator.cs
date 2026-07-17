using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 전투력 계산 (§4-22): Σ(병사 수 × 병과 계수) + 장군 보정. 배치 화면 양 진영 하단에 표시.
    /// </summary>
    public static class BattlePowerCalculator
    {
        public static float Calculate(
            IReadOnlyList<DeployedArmy> armies,
            BattlePowerConfig config,
            IReadOnlyDictionary<string, ArmyData> armyDefs)
        {
            if (armies == null) throw new ArgumentNullException(nameof(armies));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (armyDefs == null) throw new ArgumentNullException(nameof(armyDefs));

            float total = 0f;
            foreach (DeployedArmy army in armies)
            {
                if (!armyDefs.TryGetValue(army.armyDefId, out ArmyData def))
                    throw new ArgumentException($"정의되지 않은 ArmyDefinition: {army.armyDefId}");

                total += army.soldierCount * config.WeightOf(army.armyClass) + def.generalPower;
            }

            return total;
        }
    }
}
