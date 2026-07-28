using System;
using System.Numerics;

namespace NHN.Simulation.Spatial
{
    /// <summary>
    /// 근접 질의의 후보 수락 필터. struct로 구현해 제네릭 제약으로 호출하면
    /// 박싱/클로저 없이 팀·생존·우선순위 조건을 표현할 수 있다.
    /// </summary>
    public interface IUnitFilter
    {
        bool Accept(int unitIndex);
    }

    /// <summary>
    /// 아레나를 덮는 균일 그리드 공간 분할. 틱마다 Rebuild 후 근접 질의에 사용한다.
    /// 생성 시 할당한 고정 배열만 사용한다 — 틱 루프 중 힙 할당 없음.
    /// </summary>
    public sealed class SpatialHashGrid
    {
        private const int EmptyCell = -1;

        private readonly float _cellSize;
        private readonly float _originX;
        private readonly float _originY;
        private readonly int _cols;
        private readonly int _rows;
        private readonly int[] _cellHead;
        private readonly int[] _next;

        private Vector2[] _positions;
        private int _count;

        public SpatialHashGrid(float arenaHalfWidth, float arenaHalfHeight, float cellSize, int maxUnits)
        {
            _cellSize = cellSize;
            _originX = -arenaHalfWidth;
            _originY = -arenaHalfHeight;
            _cols = (int)MathF.Ceiling(arenaHalfWidth * 2f / cellSize) + 1;
            _rows = (int)MathF.Ceiling(arenaHalfHeight * 2f / cellSize) + 1;
            _cellHead = new int[_cols * _rows];
            _next = new int[maxUnits];
        }

        public void Rebuild(Vector2[] positions, int count)
        {
            _positions = positions;
            _count = count;
            Array.Fill(_cellHead, EmptyCell);
            for (int i = 0; i < count; i++)
            {
                int cell = CellIndex(positions[i]);
                _next[i] = _cellHead[cell];
                _cellHead[cell] = i;
            }
        }

        /// <summary>반경 내 유닛 인덱스를 buffer에 담고 개수를 반환한다 (질의 위치의 유닛 자신 포함).</summary>
        public int QueryCircle(Vector2 center, float radius, int[] buffer)
        {
            int minCol = ClampCol((int)MathF.Floor((center.X - radius - _originX) / _cellSize));
            int maxCol = ClampCol((int)MathF.Floor((center.X + radius - _originX) / _cellSize));
            int minRow = ClampRow((int)MathF.Floor((center.Y - radius - _originY) / _cellSize));
            int maxRow = ClampRow((int)MathF.Floor((center.Y + radius - _originY) / _cellSize));

            float radiusSq = radius * radius;
            int found = 0;
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    for (int u = _cellHead[row * _cols + col]; u != EmptyCell; u = _next[u])
                    {
                        if (Vector2.DistanceSquared(center, _positions[u]) <= radiusSq)
                        {
                            buffer[found++] = u;
                        }
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// 필터를 통과하는 최근접 유닛 인덱스를 반환한다 (없으면 -1).
        /// 중심 셀에서 링을 넓혀가며 탐색하고, 더 가까운 후보가 나올 수 없는 링에서 중단한다.
        /// </summary>
        public int FindNearest<TFilter>(Vector2 from, TFilter filter) where TFilter : struct, IUnitFilter
        {
            int centerCol = ClampCol((int)MathF.Floor((from.X - _originX) / _cellSize));
            int centerRow = ClampRow((int)MathF.Floor((from.Y - _originY) / _cellSize));
            int maxRing = Math.Max(_cols, _rows);

            int best = -1;
            float bestDistSq = float.MaxValue;

            for (int ring = 0; ring <= maxRing; ring++)
            {
                if (best != EmptyCell && ring >= 2)
                {
                    // 링 경계까지의 최소 거리가 이미 찾은 후보보다 멀면 종료.
                    float ringMinDist = (ring - 1) * _cellSize;
                    if (ringMinDist * ringMinDist > bestDistSq)
                    {
                        break;
                    }
                }

                int minCol = centerCol - ring;
                int maxCol = centerCol + ring;
                int minRow = centerRow - ring;
                int maxRow = centerRow + ring;

                for (int row = Math.Max(minRow, 0); row <= Math.Min(maxRow, _rows - 1); row++)
                {
                    // 링 둘레만 순회: 상/하단 행은 전체, 중간 행은 좌우 끝 열만.
                    if (row == minRow || row == maxRow)
                    {
                        for (int col = Math.Max(minCol, 0); col <= Math.Min(maxCol, _cols - 1); col++)
                        {
                            ScanCell(row, col, from, filter, ref best, ref bestDistSq);
                        }
                    }
                    else
                    {
                        if (minCol >= 0)
                        {
                            ScanCell(row, minCol, from, filter, ref best, ref bestDistSq);
                        }
                        if (maxCol <= _cols - 1 && maxCol != minCol)
                        {
                            ScanCell(row, maxCol, from, filter, ref best, ref bestDistSq);
                        }
                    }
                }
            }
            return best;
        }

        private void ScanCell<TFilter>(
            int row, int col, Vector2 from, TFilter filter,
            ref int best, ref float bestDistSq) where TFilter : struct, IUnitFilter
        {
            for (int u = _cellHead[row * _cols + col]; u != EmptyCell; u = _next[u])
            {
                if (!filter.Accept(u))
                {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(from, _positions[u]);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = u;
                }
            }
        }

        private int CellIndex(Vector2 position)
        {
            int col = ClampCol((int)MathF.Floor((position.X - _originX) / _cellSize));
            int row = ClampRow((int)MathF.Floor((position.Y - _originY) / _cellSize));
            return row * _cols + col;
        }

        private int ClampCol(int col) => Math.Clamp(col, 0, _cols - 1);

        private int ClampRow(int row) => Math.Clamp(row, 0, _rows - 1);
    }
}
