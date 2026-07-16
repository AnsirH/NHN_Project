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

        /// <summary>
        /// 생존·비은신 적군만 수락. priority가 지정되면 해당 우선순위(TargetPriority)로 후보를 좁힌다.
        /// 새 우선순위는 Accept의 switch에 케이스 추가로 확장한다 (4단계: 중독·표식 등 상태이상 우선).
        /// </summary>
        private readonly struct AliveEnemyFilter : IUnitFilter
        {
            private readonly BattleSimulation _sim;
            private readonly byte _targetTeam;
            private readonly int _excludeIndex;
            private readonly bool _hasPriority;
            private readonly TargetPriority _priority;

            public AliveEnemyFilter(BattleSimulation sim, byte targetTeam, int excludeIndex, bool hasPriority, TargetPriority priority)
            {
                _sim = sim;
                _targetTeam = targetTeam;
                _excludeIndex = excludeIndex;
                _hasPriority = hasPriority;
                _priority = priority;
            }

            public bool Accept(int unitIndex)
            {
                if (unitIndex == _excludeIndex
                    || !_sim._alives[unitIndex]
                    || _sim._teams[unitIndex] != _targetTeam
                    || _sim._stealthRemaining[unitIndex] > 0f)
                {
                    return false;
                }
                if (!_hasPriority)
                {
                    return true;
                }
                switch (_priority)
                {
                    case TargetPriority.RangedRole:
                        return _sim._isRangedUnit[unitIndex];
                    default:
                        return true;
                }
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
        /// <summary>남은 은신 시간(초). 0 이하 = 비은신. 은신 중엔 피타겟·충돌 분리 제외.</summary>
        private readonly float[] _stealthRemaining;
        /// <summary>다음 공격 데미지 배율 (기본 1). NextAttackCrit 기믹이 무장하면 배율로 설정, 공격 시 소비.</summary>
        private readonly float[] _critPending;
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
            _stealthRemaining = new float[totalUnits];
            _critPending = new float[totalUnits];
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

        public bool IsStealthed(int index) => _stealthRemaining[index] > 0f;

        public float GetHp(int index) => _hps[index];

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

            // 0) 은신 타이머 — 시간 만료로 해제 (StealthBreak 기믹 무장)
            for (int i = 0; i < _unitCount; i++)
            {
                if (_alives[i] && _stealthRemaining[i] > 0f)
                {
                    _stealthRemaining[i] -= dt;
                    if (_stealthRemaining[i] <= 0f)
                    {
                        BreakStealth(i);
                    }
                }
            }

            // 1) 재탐색: 타겟 무효(사망/은신)는 즉시 재선택, 주기 도래 시엔 교전 유지 규칙 적용
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i])
                {
                    continue;
                }
                int target = _targets[i];
                bool targetInvalid = target == NoTarget || !_alives[target] || _stealthRemaining[target] > 0f;
                if (targetInvalid)
                {
                    _targets[i] = SelectTarget(i);
                    _nextRetargetTimes[i] = _time + _config.RetargetInterval;
                }
                else if (_time >= _nextRetargetTimes[i])
                {
                    _targets[i] = ReevaluateTarget(i, target);
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
                if (target == NoTarget || !_alives[target] || _stealthRemaining[target] > 0f)
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
                    // ApproachTarget과 StealthDash 모두 타겟 접근 — StealthDash의 차이(은신)는 상태로 처리.
                    // 이동 궤적이 다른 새 패턴은 여기서 케이스 추가.
                    float speed = role.MoveSpeed;
                    if (_stealthRemaining[i] > 0f && role.MoveParamB > 0f)
                    {
                        speed *= role.MoveParamB; // StealthDash: 은신 중 이속 배율 (돌진 가속)
                    }
                    _positions[i] = ClampToArena(_positions[i] + toTarget * (speed * dt / centerDistance));
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

            // 5) 겹침 분리 (이동 후 위치 기준 재구축, 생존 유닛만).
            //    쌍 중 한쪽만 은신이면 스킵 — 은신 유닛이 전열을 '통과'해 돌진하기 위한 규칙.
            //    은신 유닛끼리는 분리를 유지한다: 꺼두면 같은 타겟으로 돌진하는 은신 블롭이 한 점에
            //    완전히 겹쳐 스플래시 한 발을 전원이 공유하는 동시 몰살이 난다 (헤드리스 실측으로 확인).
            _grid.Rebuild(_positions, _unitCount);
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i])
                {
                    continue;
                }
                bool stealthedI = _stealthRemaining[i] > 0f;
                RoleDefinition role = _roles[_roleIndices[i]];
                float queryRadius = role.UnitRadius + _maxUnitRadius;
                int neighborCount = _grid.QueryCircle(_positions[i], queryRadius, _queryBuffer);
                for (int k = 0; k < neighborCount; k++)
                {
                    int j = _queryBuffer[k];
                    if (j <= i || !_alives[j] || stealthedI != (_stealthRemaining[j] > 0f))
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
            if (_stealthRemaining[attacker] > 0f)
            {
                // 첫 공격으로 은신 해제 — 이 공격이 "은신 해제 첫 타"가 되어 무장된 치명타를 소비한다.
                BreakStealth(attacker);
            }

            float damage = role.AttackDamage * _critPending[attacker];
            _critPending[attacker] = 1f;

            if (!role.IsRanged)
            {
                _pendingDamage[target] += damage;
                return;
            }

            if (_projectileCount >= _config.MaxProjectiles)
            {
                // 투사체 버퍼 상한 도달 시 즉시 착탄으로 대체 — 결정론 유지를 위한 예외 경로.
                _pendingDamage[target] += damage;
                return;
            }

            int p = _projectileCount++;
            _projLaunchPos[p] = _positions[attacker];
            _projImpactPos[p] = _positions[target];
            _projLaunchTime[p] = _time;
            float flightTime = MathF.Max(centerDistance / role.ProjectileSpeed, _config.TickDeltaTime);
            _projImpactTime[p] = _time + flightTime;
            _projDamage[p] = damage;
            _projTeam[p] = _teams[attacker];
            _projArcHeight[p] = role.ProjectileArcHeight;
        }

        /// <summary>
        /// 은신 해제 공통 경로 (시간 만료·첫 공격 공용). StealthBreak 트리거 기믹이 있으면 효과를 무장한다 —
        /// 롤 무관 데이터 평가이므로 이 기믹을 가진 어떤 롤이든 동작한다.
        /// </summary>
        private void BreakStealth(int unitIndex)
        {
            _stealthRemaining[unitIndex] = 0f;
            GimmickDefinition gimmick = _roles[_roleIndices[unitIndex]].Gimmick;
            if (gimmick.Trigger == GimmickTrigger.StealthBreak && gimmick.Effect == GimmickEffect.NextAttackCrit)
            {
                _critPending[unitIndex] = gimmick.EffectParamA;
            }
        }

        private void ApplyProjectileImpact(int p)
        {
            // 의도된 규칙: 은신 유닛도 스플래시(유탄)에는 맞는다 — 은신은 '인지'를 숨기는 것이지
            // 떨어지는 화살(물리)을 통과시키지 않는다. 단 피격으로 은신이 해제되지는 않는다.
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

        /// <summary>
        /// 주기 재탐색의 타겟 유지 규칙: 교전 중(사거리 내)인 타겟은 사망·은신·사거리 이탈 전까지 바꾸지 않는다.
        /// 단 우선순위 목록에서 현재 타겟보다 앞서는 후보가 나타나면 교체한다 (셋업-페이오프 콤보의 전제 —
        /// 예: 사냥꾼은 교전 중이어도 중독 대상이 생기면 갈아탄다). 교전 전(추격 중)이면 최신 평가로 다시 고른다.
        /// </summary>
        private int ReevaluateTarget(int unitIndex, int currentTarget)
        {
            RoleDefinition role = _roles[_roleIndices[unitIndex]];
            byte enemyTeam = _teams[unitIndex] == TeamA ? TeamB : TeamA;

            TargetPriority[] priorities = role.Priorities;
            int currentRank = PriorityRank(currentTarget, priorities);
            for (int p = 0; p < currentRank; p++)
            {
                int candidate = FindByPositionFilter(unitIndex, new AliveEnemyFilter(this, enemyTeam, unitIndex, hasPriority: true, priorities[p]), role.PositionFilter);
                if (candidate != NoTarget)
                {
                    return candidate;
                }
            }

            RoleDefinition targetRole = _roles[_roleIndices[currentTarget]];
            float centerDistance = Vector2.Distance(_positions[unitIndex], _positions[currentTarget]);
            if (centerDistance - role.UnitRadius - targetRole.UnitRadius <= role.AttackRange)
            {
                return currentTarget;
            }

            return SelectTarget(unitIndex);
        }

        /// <summary>현재 타겟이 만족하는 가장 앞선 우선순위 인덱스. 아무것도 만족하지 못하면 priorities.Length.</summary>
        private int PriorityRank(int targetIndex, TargetPriority[] priorities)
        {
            for (int p = 0; p < priorities.Length; p++)
            {
                if (SatisfiesPriority(targetIndex, priorities[p]))
                {
                    return p;
                }
            }
            return priorities.Length;
        }

        /// <summary>AliveEnemyFilter의 우선순위 switch와 짝을 이룬다 — 새 우선순위는 두 곳 모두에 케이스 추가.</summary>
        private bool SatisfiesPriority(int targetIndex, TargetPriority priority)
        {
            switch (priority)
            {
                case TargetPriority.RangedRole:
                    return _isRangedUnit[targetIndex];
                default:
                    return true;
            }
        }

        /// <summary>타겟팅 2단계: ① 위치 필터 → ② 우선순위 목록 순서로 후보를 좁힌다. 실패 시 우선순위 없이 재시도.</summary>
        private int SelectTarget(int unitIndex)
        {
            RoleDefinition role = _roles[_roleIndices[unitIndex]];
            byte enemyTeam = _teams[unitIndex] == TeamA ? TeamB : TeamA;

            TargetPriority[] priorities = role.Priorities;
            for (int p = 0; p < priorities.Length; p++)
            {
                int candidate = FindByPositionFilter(unitIndex, new AliveEnemyFilter(this, enemyTeam, unitIndex, hasPriority: true, priorities[p]), role.PositionFilter);
                if (candidate != NoTarget)
                {
                    return candidate;
                }
            }

            return FindByPositionFilter(unitIndex, new AliveEnemyFilter(this, enemyTeam, unitIndex, hasPriority: false, default), role.PositionFilter);
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
                    _stealthRemaining[i] = role.MovePattern == MovePattern.StealthDash ? role.MoveParamA : 0f;
                    _critPending[i] = 1f;
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
