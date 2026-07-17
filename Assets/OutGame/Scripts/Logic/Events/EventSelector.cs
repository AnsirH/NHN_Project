using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Events
{
    /// <summary>
    /// 이벤트 풀에서 추첨 (§5.4): 동일 런 내 중복 방지, 전부 소진 시 전체 풀에서 재추첨(반복 허용).
    /// </summary>
    public static class EventSelector
    {
        public static EventData SelectRandom(
            IReadOnlyList<EventData> pool, IReadOnlyCollection<string> visitedIds, Random rng)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (visitedIds == null) throw new ArgumentNullException(nameof(visitedIds));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (pool.Count == 0) throw new ArgumentException("이벤트 풀이 비어 있습니다.", nameof(pool));

            List<EventData> unvisited = pool.Where(e => !visitedIds.Contains(e.id)).ToList();
            IReadOnlyList<EventData> candidates = unvisited.Count > 0 ? unvisited : pool;

            return candidates[rng.Next(candidates.Count)];
        }
    }
}
