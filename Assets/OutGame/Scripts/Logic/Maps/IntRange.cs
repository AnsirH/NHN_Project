using System;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// [min, max] 폐구간 정수 범위. 생성 시 rng로 하나를 뽑는다.
    /// </summary>
    [Serializable]
    public struct IntRange
    {
        public int min;
        public int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public int GetValue(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            return rng.Next(min, max + 1);
        }
    }
}
