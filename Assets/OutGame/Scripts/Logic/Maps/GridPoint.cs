using System;

namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 맵 격자 좌표 (x = 층 내 위치, y = 층 인덱스). Unity 타입 미의존 직렬화용.
    /// </summary>
    [Serializable]
    public struct GridPoint : IEquatable<GridPoint>
    {
        public int x;
        public int y;

        public GridPoint(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(GridPoint other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (x * 397) ^ y;
        public override string ToString() => $"({x},{y})";
    }
}
