using System;
using System.Collections.Generic;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 주입된 rng를 쓰는 리스트 셔플/추첨 확장 (silverua ShufflingExtension 이식 —
    /// 전역 rng를 제거하고 시드 기반 결정성을 확보).
    /// </summary>
    public static class ListRandomExtensions
    {
        public static void Shuffle<T>(this IList<T> list, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            for (int n = list.Count - 1; n > 0; n--)
            {
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public static T PickRandom<T>(this IReadOnlyList<T> list, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (list == null || list.Count == 0)
                throw new ArgumentException("빈 리스트에서 추첨할 수 없습니다.", nameof(list));

            return list[rng.Next(list.Count)];
        }
    }
}
