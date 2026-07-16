using System;
using System.Numerics;
using NHN.Simulation.Spatial;

namespace NHN.Simulation.Stress
{
    /// <summary>
    /// 성능 상한 측정용 최소 시뮬: 유닛이 최근접 적을 향해 이동하고 접촉 시 넉백된다.
    /// 롤/전투 로직 없음. 고정 틱 + 시드 주입 결정론 — 같은 (unitCount, seed)는 항상 같은 결과.
    /// 틱 루프 중 힙 할당 없음.
    /// </summary>
    public sealed class StressSimulation
    {
        private const int NoTarget = -1;

        /// <summary>지정 팀의 유닛만 수락하는 최근접 탐색 필터 (자기 자신 제외).</summary>
        private readonly struct EnemyOfTeamFilter : IUnitFilter
        {
            private readonly byte[] _teams;
            private readonly byte _targetTeam;
            private readonly int _excludeIndex;

            public EnemyOfTeamFilter(byte[] teams, byte targetTeam, int excludeIndex)
            {
                _teams = teams;
                _targetTeam = targetTeam;
                _excludeIndex = excludeIndex;
            }

            public bool Accept(int unitIndex)
            {
                return unitIndex != _excludeIndex && _teams[unitIndex] == _targetTeam;
            }
        }

        private readonly StressSimConfig _config;
        private readonly SpatialHashGrid _grid;
        private readonly Vector2[] _positions;
        private readonly Vector2[] _prevPositions;
        private readonly Vector2[] _knockbackVelocities;
        private readonly byte[] _teams;
        private readonly int[] _targets;
        private readonly float[] _nextRetargetTimes;
        private readonly int[] _queryBuffer;

        private Random _random;
        private float _time;
        private int _unitCount;

        public StressSimulation(in StressSimConfig config)
        {
            _config = config;
            // 셀 크기는 접촉 질의 반경(지름)의 2배 — 질의가 보통 3x3 셀 안에서 끝난다.
            float cellSize = MathF.Max(config.UnitRadius * 4f, 0.25f);
            _grid = new SpatialHashGrid(config.ArenaHalfWidth, config.ArenaHalfHeight, cellSize, config.MaxUnits);
            _positions = new Vector2[config.MaxUnits];
            _prevPositions = new Vector2[config.MaxUnits];
            _knockbackVelocities = new Vector2[config.MaxUnits];
            _teams = new byte[config.MaxUnits];
            _targets = new int[config.MaxUnits];
            _nextRetargetTimes = new float[config.MaxUnits];
            _queryBuffer = new int[config.MaxUnits];
        }

        public int UnitCount => _unitCount;

        public float TickDeltaTime => _config.TickDeltaTime;

        public void Reset(int unitCount, int seed)
        {
            _unitCount = Math.Clamp(unitCount, 0, _config.MaxUnits);
            _random = new Random(seed);
            _time = 0f;

            int teamACount = _unitCount / 2;
            SpawnTeamBlock(0, teamACount, team: 0, blockCenterX: -_config.ArenaHalfWidth * 0.55f);
            SpawnTeamBlock(teamACount, _unitCount, team: 1, blockCenterX: _config.ArenaHalfWidth * 0.55f);

            Array.Copy(_positions, _prevPositions, _unitCount);

            // 최초 타겟은 즉시 배정하고, 이후 재탐색 시점은 유닛별로 균등 시차를 둔다.
            _grid.Rebuild(_positions, _unitCount);
            for (int i = 0; i < _unitCount; i++)
            {
                _targets[i] = _grid.FindNearest(_positions[i], new EnemyOfTeamFilter(_teams, EnemyTeam(_teams[i]), i));
                _nextRetargetTimes[i] = _config.RetargetInterval * (i + 1) / _unitCount;
            }
        }

        public void Tick()
        {
            float dt = _config.TickDeltaTime;
            _time += dt;

            Array.Copy(_positions, _prevPositions, _unitCount);

            // 1) 재탐색 (주기 + 유닛별 시차)
            _grid.Rebuild(_positions, _unitCount);
            for (int i = 0; i < _unitCount; i++)
            {
                if (_time >= _nextRetargetTimes[i])
                {
                    _targets[i] = _grid.FindNearest(_positions[i], new EnemyOfTeamFilter(_teams, EnemyTeam(_teams[i]), i));
                    _nextRetargetTimes[i] = _time + _config.RetargetInterval;
                }
            }

            // 2) 이동 + 넉백 적용/감쇠
            float contactDistance = _config.UnitRadius * 2f;
            float dampingFactor = MathF.Max(0f, 1f - _config.KnockbackDamping * dt);
            for (int i = 0; i < _unitCount; i++)
            {
                Vector2 position = _positions[i];
                int target = _targets[i];
                if (target != NoTarget)
                {
                    Vector2 toTarget = _positions[target] - position;
                    float distance = toTarget.Length();
                    if (distance > contactDistance)
                    {
                        position += toTarget * (_config.MoveSpeed * dt / distance);
                    }
                }

                position += _knockbackVelocities[i] * dt;
                _knockbackVelocities[i] *= dampingFactor;
                _positions[i] = ClampToArena(position);
            }

            // 3) 접촉 분리 + 상호 넉백 (이동 후 위치 기준으로 그리드 재구축)
            _grid.Rebuild(_positions, _unitCount);
            for (int i = 0; i < _unitCount; i++)
            {
                int neighborCount = _grid.QueryCircle(_positions[i], contactDistance, _queryBuffer);
                for (int k = 0; k < neighborCount; k++)
                {
                    int j = _queryBuffer[k];
                    if (j <= i)
                    {
                        continue;
                    }

                    Vector2 delta = _positions[j] - _positions[i];
                    float distance = delta.Length();
                    if (distance >= contactDistance)
                    {
                        continue;
                    }

                    // 완전히 겹친 경우에도 결정론을 유지하는 고정 분리축.
                    Vector2 normal = distance > 1e-5f ? delta / distance : new Vector2(1f, 0f);
                    float overlap = contactDistance - distance;
                    Vector2 separation = normal * (overlap * 0.5f);
                    _positions[i] = ClampToArena(_positions[i] - separation);
                    _positions[j] = ClampToArena(_positions[j] + separation);

                    if (_teams[i] != _teams[j])
                    {
                        _knockbackVelocities[i] -= normal * _config.KnockbackImpulse;
                        _knockbackVelocities[j] += normal * _config.KnockbackImpulse;
                    }
                }
            }
        }

        public Vector2 GetInterpolatedPosition(int index, float alpha)
        {
            return Vector2.Lerp(_prevPositions[index], _positions[index], alpha);
        }

        public byte GetTeam(int index) => _teams[index];

        public Vector2 GetPosition(int index) => _positions[index];

        private void SpawnTeamBlock(int startIndex, int endIndex, byte team, float blockCenterX)
        {
            int count = endIndex - startIndex;
            if (count <= 0)
            {
                return;
            }

            int rows = (int)MathF.Ceiling(MathF.Sqrt(count));
            float spacing = _config.UnitRadius * 2.5f;
            float jitter = _config.UnitRadius * 0.5f;

            for (int i = startIndex; i < endIndex; i++)
            {
                int local = i - startIndex;
                int row = local / rows;
                int col = local % rows;
                float x = blockCenterX + (col - rows * 0.5f) * spacing
                          + ((float)_random.NextDouble() * 2f - 1f) * jitter;
                float y = (row - count / (float)rows * 0.5f) * spacing
                          + ((float)_random.NextDouble() * 2f - 1f) * jitter;

                _positions[i] = ClampToArena(new Vector2(x, y));
                _knockbackVelocities[i] = Vector2.Zero;
                _teams[i] = team;
                _targets[i] = NoTarget;
            }
        }

        private Vector2 ClampToArena(Vector2 position)
        {
            return new Vector2(
                Math.Clamp(position.X, -_config.ArenaHalfWidth, _config.ArenaHalfWidth),
                Math.Clamp(position.Y, -_config.ArenaHalfHeight, _config.ArenaHalfHeight));
        }

        private static byte EnemyTeam(byte team) => team == 0 ? (byte)1 : (byte)0;
    }
}
