using System;
using System.Collections.Generic;

namespace OutGame.Logic.Narrative
{
    /// <summary>
    /// "의지의 파편" 판정 (Docs/OutGame/로딩 화면 - 의지의 파편 설계.md) — 전투 종료 → 맵 화면
    /// 복귀 시점에만 낮은 확률로 플레이버 텍스트 하나를 보여준다. EventSelector와 달리 같은 런 안
    /// 중복 방지 로직이 없다 — 확률 자체가 낮아(기본 15%) 굳이 방문 기록을 관리할 필요가 없다는
    /// 설계 판단([[story-worldbuilding]]의 "의지의 파편은 아주 드묾" 설정과 합의된 수치).
    /// </summary>
    public static class LoreFragmentSelector
    {
        public const float DefaultTriggerChance = 0.15f;

        /// <summary>확률 판정 → 걸리면 풀에서 무작위 1개를 selected에 담아 true, 아니면 false.
        /// 풀이 비어 있으면 확률과 무관하게 항상 false(빈 풀에서 뽑을 수 없으므로).</summary>
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
