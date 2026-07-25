using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Augments
{
    /// <summary>증강 방(§4-27)에서 pool 중 3개를 무작위로 노출한다 — 같은 화면 안에서는 중복 없이.</summary>
    public static class AugmentPoolService
    {
        public static List<AugmentData> PickRandomThree(IReadOnlyList<AugmentData> pool, Random rng)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (pool.Count == 0) throw new ArgumentException("증강 풀이 비어 있습니다.", nameof(pool));

            int count = Math.Min(3, pool.Count);
            List<AugmentData> shuffled = pool.ToList();
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            return shuffled.Take(count).ToList();
        }
    }
}
