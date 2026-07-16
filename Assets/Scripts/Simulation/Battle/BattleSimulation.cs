using System;
using System.Numerics;
using NHN.Simulation.Spatial;

namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 전투 모듈: ArmyDefinition 2개 입력 → 고정 틱 진행 → BattleResult 출력.
    /// 진행 구조(메타 루프)와 독립적이며, 시드 주입 결정론 — 같은 입력과 시드는 항상 같은 결과.
    /// 틱 루프 중 힙 할당 없음. 롤별 분기 없음 — 모든 행동은 RoleDefinition 데이터로 결정된다.
    /// </summary>
    public sealed class BattleSimulation
    {
        private const int NoTarget = -1;
        private const byte TeamA = 0;
        private const byte TeamB = 1;

        /// <summary>생존한 적군만 수락. requireRanged가 켜지면 원거리 롤로 후보를 좁힌다 (TargetPriority.RangedRole).</summary>
        private readonly struct AliveEnemyFilter : IUnitFilter
        {
            private readonly bool[] _alives;
            private readonly byte[] _teams;
            private readonly bool[] _isRanged;
            private readonly byte _targetTeam;
            private readonly int _excludeIndex;
            private readonly bool _requireRanged;

            public AliveEnemyFilter(bool[] alives, byte[] teams, bool[] isRanged, byte targetTeam, int excludeIndex, bool requireRanged)
            {
                _alives = alives;
                _teams = teams;
                _isRanged = isRanged;
                _targetTeam = targetTeam;
                _excludeIndex = excludeIndex;
                _requireRanged = requireRanged;
            }

            public bool Accept(int unitIndex)
            {
                if (unitIndex == _excludeIndex || !_alives[unitIndex] || _teams[unitIndex] != _targetTeam)
                {
                    return false;
                }
                return !_requireRanged || _isRanged[unitIndex];
            }
        }

        private readonly BattleConfig _config;
        private readonly RoleDefinition[] _roles;
        private readonly SpatialHashGrid _grid;

        // 유닛 상태 (SoA)
        private readonly Vector2[] _positions;
        private readonly Vector2[] _prevPositions;
        private readonly float[] _hps;
        private readonly float[] _pendingDamage;
        private readonly int[] _roleIndices;
        private readonly byte[] _teams;
        private readonly bool[] _alives;
        private readonly bool[] _isRangedUnit;
        private readonly int[] _targets;
        private readonly float[] _nextRetargetTimes;
        private readonly float[] _attackCooldowns;
        private readonly int[] _queryBuffer;

        // 투사체 (고정 배열, swap-remove)
        private readonly Vector2[] _projLaunchPos;
        private readonly Vector2[] _projImpactPos;
        private readonly float[] _projLaunchTime;
        private readonly float[] _projImpactTime;
        private readonly float[] _projDamage;
        private readonly byte[] _projTeam;
        private readonly float[] _projArcHeight;
        private int _projectileCount;

        private readonly Random _random;
        private readonly int[] _teamAliveCounts = new int[2];
        private readonly float _maxUnitRadius;
        private int _unitCount;
        private float _time;
        private int _tick;
        private bool _finished;
        private BattleResult _result;

        public BattleSimulation(in BattleConfig config, ArmyDefinition armyA, ArmyDefinition armyB, int seed)
        {
            _config = config;
            _random = new Random(seed);

            int totalUnits = armyA.TotalUnits + armyB.TotalUnits;
            if (totalUnits > config.MaxUnits)
            {
                throw new ArgumentException($"유닛 수 {totalUnits}가 상한 {config.MaxUnits}를 초과");
            }

            _positions = new Vector2[totalUnits];
            _prevPositions = new Vector2[totalUnits];
            _hps = new float[totalUnits];
            _pendingDamage = new float[totalUnits];
            _roleIndices = new int[totalUnits];
            _teams = new byte[totalUnits];
            _alives = new bool[totalUnits];
            _isRangedUnit = new bool[totalUnits];
            _targets = new int[totalUnits];
            _nextRetargetTimes = new float[totalUnits];
            _attackCooldowns = new float[totalUnits];
            _queryBuffer = new int[totalUnits];

            _projLaunchPos = new Vector2[config.MaxProjectiles];
            _projImpactPos = new Vector2[config.MaxProjectiles];
            _projLaunchTime = new float[config.MaxProjectiles];
            _projImpactTime = new float[config.MaxProjectiles];
            _projDamage = new float[config.MaxProjectiles];
            _projTeam = new byte[config.MaxProjectiles];
            _projArcHeight = new float[config.MaxProjectiles];

            _roles = BuildRoleTable(armyA, armyB);

            SpawnArmy(armyA, TeamA);
            SpawnArmy(armyB, TeamB);

            float maxRadius = 0f;
            for (int r = 0; r < _roles.Length; r++)
            {
                maxRadius = MathF.Max(maxRadius, _roles[r].UnitRadius);
            }
            _maxUnitRadius = maxRadius;

            float cellSize = MathF.Max(maxRadius * 4f, 0.25f);
            _grid = new SpatialHashGrid(config.ArenaHalfWidth, config.ArenaHalfHeight, cellSize, totalUnits);

            Array.Copy(_positions, _prevPositions, _unitCount);

            // 최초 타겟 즉시 배정 + 재탐색 시차 균등 배분
            _grid.Rebuild(_positions, _unitCount);
            for (int i = 0; i < _unitCount; i++)
            {
                _targets[i] = SelectTarget(i);
                _nextRetargetTimes[i] = _config.RetargetInterval * (i + 1) / _unitCount;
            }
        }

        public int UnitCount => _unitCount;

        public float TickDeltaTime => _config.TickDeltaTime;

        public bool Finished => _finished;

        /// <summary>Finished가 true일 때만 유효.</summary>
        public BattleResult Result => _result;

        public int ProjectileCount => _projectileCount;

        public bool IsAlive(int index) => _alives[index];

        public byte GetTeam(int index) => _teams[index];

        public int GetRoleIndex(int index) => _roleIndices[index];

        public RoleDefinition GetRole(int roleIndex) => _roles[roleIndex];

        public Vector2 GetPosition(int index) => _positions[index];

        public Vector2 GetInterpolatedPosition(int index, float alpha)
        {
            return Vector2.Lerp(_prevPositions[index], _positions[index], alpha);
        }

        /// <summary>뷰 투사체 표현용 스냅샷. alpha는 틱 사이 보간 계수.</summary>
        public readonly struct ProjectileState
        {
            public readonly Vector2 LaunchPosition;
            public readonly Vector2 ImpactPosition;
            public readonly float Progress01;
            public readonly float ArcHeight;

            public ProjectileState(Vector2 launch, Vector2 impact, float progress01, float arcHeight)
            {
                LaunchPosition = launch;
                ImpactPosition = impact;
                Progress01 = progress01;
                ArcHeight = arcHeight;
            }
        }

        public ProjectileState GetProjectileState(int index, float alpha)
        {
            float renderTime = _time + (alpha - 1f) * _config.TickDeltaTime;
            float duration = _projImpactTime[index] - _projLaunchTime[index];
            float progress = duration > 0f
                ? Math.Clamp((renderTime - _projLaunchTime[index]) / duration, 0f, 1f)
                : 1f;
            return new ProjectileState(_projLaunchPos[index], _projImpactPos[index], progress, _projArcHeight[index]);
        }

        public void Tick()
        {
            if (_finished)
            {
                return;
            }

            float dt = _config.TickDeltaTime;
            _time += dt;
            _tick++;

            Array.Copy(_positions, _prevPositions, _unitCount);
            _grid.Rebuild(_positions, _unitCount);

            // 1) 재탐색: 주기 도래 또는 타겟 사망 시
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i])
                {
                    continue;
                }
                int target = _targets[i];
                bool targetInvalid = target == NoTarget || !_alives[target];
                if (targetInvalid || _time >= _nextRetargetTimes[i])
                {
                    _targets[i] = SelectTarget(i);
                    _nextRetargetTimes[i] = _time + _config.RetargetInterval;
                }
            }

            // 2) 이동 또는 공격
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i])
                {
                    continue;
                }

                if (_attackCooldowns[i] > 0f)
                {
                    _attackCooldowns[i] -= dt;
                }

                int target = _targets[i];
                if (target == NoTarget || !_alives[target])
                {
                    continue;
                }

                RoleDefinition role = _roles[_roleIndices[i]];
                RoleDefinition targetRole = _roles[_roleIndices[target]];
                Vector2 toTarget = _positions[target] - _positions[i];
                float centerDistance = toTarget.Length();
                float edgeDistance = centerDistance - role.UnitRadius - targetRole.UnitRadius;

                if (edgeDistance <= role.AttackRange)
                {
                    if (_attackCooldowns[i] <= 0f)
                    {
                        Attack(i, target, role, centerDistance);
                        _attackCooldowns[i] = role.AttackInterval;
                    }
                }
                else if (centerDistance > 1e-5f)
                {
                    // MovePattern.ApproachTarget — 새 패턴은 여기서 케이스 추가
                    _positions[i] = ClampToArena(_positions[i] + toTarget * (role.MoveSpeed * dt / centerDistance));
                }
            }

            // 3) 착탄 처리
            for (int p = 0; p < _projectileCount;)
            {
                if (_time >= _projImpactTime[p])
                {
                    ApplyProjectileImpact(p);
                    RemoveProjectileAt(p);
                }
                else
                {
                    p++;
                }
            }

            // 4) 누적 데미지 적용 + 사망 처리 (동시 공격의 순서 이점 제거)
            for (int i = 0; i < _unitCount; i++)
            {
                if (_pendingDamage[i] <= 0f)
                {
                    continue;
                }
                _hps[i] -= _pendingDamage[i];
                _pendingDamage[i] = 0f;
                if (_alives[i] && _hps[i] <= 0f)
                {
                    _alives[i] = false;
                    _teamAliveCounts[_teams[i]]--;
                }
            }

            // 5) 겹침 분리 (이동 후 위치 기준 재구축, 생존 유닛만)
            _grid.Rebuild(_positions, _unitCount);
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i])
                {
                    continue;
                }
                RoleDefinition role = _roles[_roleIndices[i]];
                float queryRadius = role.UnitRadius + _maxUnitRadius;
                int neighborCount = _grid.QueryCircle(_positions[i], queryRadius, _queryBuffer);
                for (int k = 0; k < neighborCount; k++)
                {
                    int j = _queryBuffer[k];
                    if (j <= i || !_alives[j])
                    {
                        continue;
                    }

                    float contactDistance = role.UnitRadius + _roles[_roleIndices[j]].UnitRadius;
                    Vector2 delta = _positions[j] - _positions[i];
                    float distance = delta.Length();
                    if (distance >= contactDistance)
                    {
                        continue;
                    }

                    Vector2 normal = distance > 1e-5f ? delta / distance : new Vector2(1f, 0f);
                    Vector2 separation = normal * ((contactDistance - distance) * 0.5f);
                    _positions[i] = ClampToArena(_positions[i] - separation);
                    _positions[j] = ClampToArena(_positions[j] + separation);
                }
            }

            // 6) 승패 판정
            if (_teamAliveCounts[TeamA] == 0 || _teamAliveCounts[TeamB] == 0)
            {
                int winner = _teamAliveCounts[TeamA] > 0 ? TeamA
                    : _teamAliveCounts[TeamB] > 0 ? TeamB
                    : BattleResult.DrawWinner;
                Finish(winner);
            }
            else if (_time >= _config.MaxBattleSeconds)
            {
                Finish(BattleResult.DrawWinner);
            }
        }

        private void Attack(int attacker, int target, RoleDefinition role, float centerDistance)
        {
            if (!role.IsRanged)
            {
                _pendingDamage[target] += role.AttackDamage;
                return;
            }

            if (_projectileCount >= _config.MaxProjectiles)
            {
                // 투사체 버퍼 상한 도달 시 즉시 착탄으로 대체 — 결정론 유지를 위한 예외 경로.
                _pendingDamage[target] += role.AttackDamage;
                return;
            }

            int p = _projectileCount++;
            _projLaunchPos[p] = _positions[attacker];
            _projImpactPos[p] = _positions[target];
            _projLaunchTime[p] = _time;
            float flightTime = MathF.Max(centerDistance / role.ProjectileSpeed, _config.TickDeltaTime);
            _projImpactTime[p] = _time + flightTime;
            _projDamage[p] = role.AttackDamage;
            _projTeam[p] = _teams[attacker];
            _projArcHeight[p] = role.ProjectileArcHeight;
        }

        private void ApplyProjectileImpact(int p)
        {
            byte enemyTeam = _projTeam[p] == TeamA ? TeamB : TeamA;
            float queryRadius = _config.ProjectileImpactRadius + _maxUnitRadius;
            int hitCount = _grid.QueryCircle(_projImpactPos[p], queryRadius, _queryBuffer);
            for (int k = 0; k < hitCount; k++)
            {
                int u = _queryBuffer[k];
                if (!_alives[u] || _teams[u] != enemyTeam)
                {
                    continue;
                }
                float hitDistance = _config.ProjectileImpactRadius + _roles[_roleIndices[u]].UnitRadius;
                if (Vector2.DistanceSquared(_projImpactPos[p], _positions[u]) <= hitDistance * hitDistance)
                {
                    _pendingDamage[u] += _projDamage[p];
                }
            }
        }

        private void RemoveProjectileAt(int p)
        {
            int last = --_projectileCount;
            _projLaunchPos[p] = _projLaunchPos[last];
            _projImpactPos[p] = _projImpactPos[last];
            _projLaunchTime[p] = _projLaunchTime[last];
            _projImpactTime[p] = _projImpactTime[last];
            _projDamage[p] = _projDamage[last];
            _projTeam[p] = _projTeam[last];
            _projArcHeight[p] = _projArcHeight[last];
        }

        /// <summary>타겟팅 2단계: ① 위치 필터 → ② 우선순위 목록 순서로 후보를 좁힌다. 실패 시 우선순위 없이 재시도.</summary>
        private int SelectTarget(int unitIndex)
        {
            RoleDefinition role = _roles[_roleIndices[unitIndex]];
            byte enemyTeam = _teams[unitIndex] == TeamA ? TeamB : TeamA;

            TargetPriority[] priorities = role.Priorities;
            for (int p = 0; p < priorities.Length; p++)
            {
                // 현재 케이스는 RangedRole 하나 — 우선순위 추가 시 여기서 필터 조합 확장
                bool requireRanged = priorities[p] == TargetPriority.RangedRole;
                int candidate = FindByPositionFilter(unitIndex, new AliveEnemyFilter(_alives, _teams, _isRangedUnit, enemyTeam, unitIndex, requireRanged), role.PositionFilter);
                if (candidate != NoTarget)
                {
                    return candidate;
                }
            }

            return FindByPositionFilter(unitIndex, new AliveEnemyFilter(_alives, _teams, _isRangedUnit, enemyTeam, unitIndex, requireRanged: false), role.PositionFilter);
        }

        private int FindByPositionFilter(int unitIndex, in AliveEnemyFilter filter, PositionFilter positionFilter)
        {
            if (positionFilter == PositionFilter.Nearest)
            {
                return _grid.FindNearest(_positions[unitIndex], filter);
            }

            // Farthest: 재탐색은 저빈도(스태거 주기)라 선형 순회로 충분하다.
            int best = NoTarget;
            float bestDistSq = -1f;
            for (int u = 0; u < _unitCount; u++)
            {
                if (!filter.Accept(u))
                {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(_positions[unitIndex], _positions[u]);
                if (distSq > bestDistSq)
                {
                    bestDistSq = distSq;
                    best = u;
                }
            }
            return best;
        }

        private RoleDefinition[] BuildRoleTable(ArmyDefinition armyA, ArmyDefinition armyB)
        {
            int maxRoles = armyA.Squads.Length + armyB.Squads.Length;
            var table = new RoleDefinition[maxRoles];
            int count = 0;
            for (int army = 0; army < 2; army++)
            {
                SquadDefinition[] squads = army == 0 ? armyA.Squads : armyB.Squads;
                for (int s = 0; s < squads.Length; s++)
                {
                    if (IndexOfRole(table, count, squads[s].Role) < 0)
                    {
                        table[count++] = squads[s].Role;
                    }
                }
            }
            Array.Resize(ref table, count);
            return table;
        }

        private static int IndexOfRole(RoleDefinition[] table, int count, RoleDefinition role)
        {
            for (int r = 0; r < count; r++)
            {
                if (ReferenceEquals(table[r], role))
                {
                    return r;
                }
            }
            return -1;
        }

        private void SpawnArmy(ArmyDefinition army, byte team)
        {
            // A군은 -x에서 +x를 향하고, B군은 미러. anchor.x = 전선에서 뒤로 물러난 깊이.
            float direction = team == TeamA ? -1f : 1f;
            for (int s = 0; s < army.Squads.Length; s++)
            {
                SquadDefinition squad = army.Squads[s];
                RoleDefinition role = squad.Role;
                int roleIndex = IndexOfRole(_roles, _roles.Length, role);

                var anchor = new Vector2(
                    direction * (_config.FrontLineOffsetX + squad.Anchor.X),
                    squad.Anchor.Y);

                int rows = (int)MathF.Ceiling(MathF.Sqrt(squad.Count));
                float spacing = role.UnitRadius * 2.5f;
                float jitter = role.UnitRadius * 0.5f;

                for (int k = 0; k < squad.Count; k++)
                {
                    int i = _unitCount++;
                    int row = k / rows;
                    int col = k % rows;
                    float x = anchor.X + direction * (row - rows * 0.5f) * spacing
                              + ((float)_random.NextDouble() * 2f - 1f) * jitter;
                    float y = anchor.Y + (col - rows * 0.5f) * spacing
                              + ((float)_random.NextDouble() * 2f - 1f) * jitter;

                    _positions[i] = ClampToArena(new Vector2(x, y));
                    _hps[i] = role.MaxHp;
                    _pendingDamage[i] = 0f;
                    _roleIndices[i] = roleIndex;
                    _teams[i] = team;
                    _alives[i] = true;
                    _isRangedUnit[i] = role.IsRanged;
                    _targets[i] = NoTarget;
                    _attackCooldowns[i] = 0f;
                    _teamAliveCounts[team]++;
                }
            }
        }

        private void Finish(int winner)
        {
            _finished = true;
            _result = new BattleResult(winner, _teamAliveCounts[TeamA], _teamAliveCounts[TeamB], _tick);
        }

        private Vector2 ClampToArena(Vector2 position)
        {
            return new Vector2(
                Math.Clamp(position.X, -_config.ArenaHalfWidth, _config.ArenaHalfWidth),
                Math.Clamp(position.Y, -_config.ArenaHalfHeight, _config.ArenaHalfHeight));
        }
    }
}
