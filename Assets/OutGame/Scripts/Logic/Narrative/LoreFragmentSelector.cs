using System;
using System.Collections.Generic;

namespace OutGame.Logic.Narrative
{
    /// <summary>
    /// 로딩 오버레이가 "의지의 파편"을 띄울지/무엇을 띄울지 결정한다 (§2-2 세계관: 아주 드묾).
    /// EventSelector와 달리 방문 중복 방지 로직은 없다 — 확률 자체가 낮아 반복 없이도 자연스럽다.
    /// </summary>
    public static class LoreFragmentSelector
    {
        /// <summary>사용자 확정(2026-08-04): 로딩마다 15% 확률로 노출.</summary>
        public const float DefaultTriggerChance = 0.15f;

        /// <summary>확률을 굴려 노출 여부를 정하고, 노출된다면 풀에서 무작위로 하나 고른다.</summary>
        public static bool TrySelect(
            IReadOnlyList<LoreFragmentData> pool, Random rng, float triggerChance, out LoreFragmentData selected)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            selected = null;
            if (pool.Count == 0) return false;
            if (rng.NextDouble() >= triggerChance) return false;

            selected = pool[rng.Next(pool.Count)];
            return true;
        }
    }
}
