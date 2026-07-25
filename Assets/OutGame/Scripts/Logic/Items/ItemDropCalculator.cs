using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Items
{
    /// <summary>
    /// 전투 승리 시 격파한 적 병과 구성에 따른 아이템 드롭 판정 (§4-28). 병과 없는(None) 적이나
    /// 매핑된 아이템이 없는 병과는 판정 자체를 하지 않는다 — 병과별 독립 베르누이 시행.
    /// </summary>
    public static class ItemDropCalculator
    {
        public static List<string> RollDrops(
            IReadOnlyList<ArmyClass> defeatedComposition,
            ItemDropConfig config,
            IReadOnlyDictionary<ArmyClass, string> itemIdByClass,
            Random rng)
        {
            if (defeatedComposition == null) throw new ArgumentNullException(nameof(defeatedComposition));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (itemIdByClass == null) throw new ArgumentNullException(nameof(itemIdByClass));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var drops = new List<string>();
            foreach (ArmyClass armyClass in defeatedComposition)
            {
                if (armyClass == ArmyClass.None) continue;
                if (!itemIdByClass.TryGetValue(armyClass, out string itemId)) continue;

                if (rng.NextDouble() < config.ChanceFor(armyClass))
                    drops.Add(itemId);
            }

            return drops;
        }
    }
}
