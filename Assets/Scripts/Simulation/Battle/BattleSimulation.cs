using System;
using System.Numerics;
using NHN.Simulation.Spatial;

namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 전투 모듈: ArmyDefinition 2개 입력 → 고정 틱 진행 → BattleResult 출력.
    /// 진행 구조(메타 루프)와 독립적이며, 시드 주입 결정론 — 같은 입력과 시드는 항상 같은 결과.
    /// 틱 루프 중 힙 할당 없음. 롤/장군별 분기 없음 — 모든 행동은 RoleDefinition/GeneralDefinition 데이터로 결정된다.
    /// v4: 병사 트리거 기믹 경로 제거. 장군(분대 리더) = 패시브(생존 중 부대 버프) + 충전식 액티브(부대 스코프).
    /// </summary>
    public sealed class BattleSimulation
    {
        private const int NoTarget = -1;
        private const int NoSquad = -1;
        private const byte TeamA = 0;
        private const byte TeamB = 1;

        /// <summary>
        /// 생존·비은신 적군만 수락. priority가 지정되면 해당 우선순위(TargetPriority)로 후보를 좁힌다.
        /// 새 우선순위는 Accept의 switch에 케이스 추가로 확장한다 (SatisfiesPriority와 짝).
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
                    case TargetPriority.Poisoned:
                        return _sim._statusEffects.IsActive(unitIndex, StatusEffectType.Poison);
                    case TargetPriority.Marked:
                        return _sim._statusEffects.IsActive(unitIndex, StatusEffectType.Mark);
                    case TargetPriority.Leader:
                        return _sim._isLeaderUnit[unitIndex];
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
        private readonly float[] _maxHps;
        /// <summary>틱 내 누적 피해 — 방어력 감쇠 적용 대상 (일반 공격·투사체·장군 액티브·플레이어 스킬 즉발).</summary>
        private readonly float[] _pendingDamage;
        /// <summary>틱 내 누적 도트 피해 — 방어력 감쇠 미적용 (기획 합의: 상태이상 셋업의 가치 보존).</summary>
        private readonly float[] _pendingDotDamage;
        /// <summary>틱 내 누적 회복량 — HealOverTime(힐 장판)이 채우고 데미지 적용 단계에서 함께 정산.</summary>
        private readonly float[] _pendingHeal;
        private readonly int[] _roleIndices;
        private readonly byte[] _teams;
        private readonly bool[] _alives;
        private readonly bool[] _isRangedUnit;
        /// <summary>분대 리더(장군) 여부 — TargetPriority.Leader(참수)의 판정 근거.</summary>
        private readonly bool[] _isLeaderUnit;
        /// <summary>유닛이 속한 분대 인덱스 (A군 분대 → B군 분대 순 전역 번호).</summary>
        private readonly int[] _squadIndices;
        /// <summary>마지막 직접 피해(근접/투사체)를 준 분대 — 킬 크레딧(SquadKills 충전) 귀속용. NoSquad = 없음.</summary>
        private readonly int[] _lastDamageSourceSquad;
        private readonly int[] _targets;
        private readonly float[] _nextRetargetTimes;
        private readonly float[] _attackCooldowns;
        /// <summary>남은 은신 시간(초). 0 이하 = 비은신. 은신 중엔 피타겟·충돌 분리 제외.</summary>
        private readonly float[] _stealthRemaining;
        /// <summary>다음 공격 데미지 배율 (기본 1). 그림자 습격이 배율로 설정, 공격 시 소비.</summary>
        private readonly float[] _critPending;
        private readonly int[] _queryBuffer;

        /// <summary>상태이상 공용 시스템 — 부정 5종 + 긍정 효과 전부 이 하나가 처리 (불변조건 5).</summary>
        private readonly StatusEffectSystem _statusEffects;

        // 분대(장군) 상태 — 분대 수는 소수(그리드 칸 수준)라 선형 순회로 충분.
        private readonly GeneralDefinition[] _squadGenerals;
        private readonly byte[] _squadTeams;
        /// <summary>분대 장군의 유닛 인덱스. NoTarget = 장군 없는 분대.</summary>
        private readonly int[] _squadGeneralUnits;
        /// <summary>액티브 충전 게이지. 장군 사망 시 0으로 소멸 (기획 §6 사망 규칙).</summary>
        private readonly float[] _squadCharges;
        private readonly int[] _squadActivationCounts;
        /// <summary>마지막 액티브 발동 시각 (없으면 음수) — 뷰의 슬로모/발광 연출 트리거.</summary>
        private readonly float[] _squadLastActivationTimes;
        private readonly int _squadCount;

        // 플레이어 스킬 (A군 시전 전제) — 쿨다운을 시뮬이 소유해 헤드리스 밸런싱에서도 동일 규칙.
        // 시전은 큐에 쌓였다가 다음 틱 시작에 실행된다 (틱 정렬 명령: 결정론·리플레이 전제).
        private readonly SkillDefinition[] _playerSkills;
        private readonly float[] _skillCooldowns;
        private readonly bool[] _skillCastQueued;
        private readonly Vector2[] _skillCastPositions;

        // 스킬 장판 (고정 배열, swap-remove)
        private readonly SkillDefinition[] _zoneSkills;
        private readonly int[] _zoneSkillSlots;
        private readonly Vector2[] _zonePositions;
        private readonly float[] _zoneRemainings;
        private readonly byte[] _zoneTargetTeams;
        private int _zoneCount;

        // 투사체 (고정 배열, swap-remove)
        private readonly Vector2[] _projLaunchPos;
        private readonly Vector2[] _projImpactPos;
        private readonly float[] _projLaunchTime;
        private readonly float[] _projImpactTime;
        private readonly float[] _projDamage;
        private readonly byte[] _projTeam;
        private readonly float[] _projArcHeight;
        /// <summary>발사한 분대 (킬 크레딧용). NoSquad = 분대 귀속 없음.</summary>
        private readonly int[] _projSourceSquad;
        private int _projectileCount;

        private readonly Random _random;
        private readonly int[] _teamAliveCounts = new int[2];
        private readonly float _maxUnitRadius;
        private int _unitCount;
        private float _time;
        private int _tick;
        private bool _finished;
        private BattleResult _result;

        public BattleSimulation(
            in BattleConfig config, ArmyDefinition armyA, ArmyDefinition armyB, int seed,
            SkillDefinition[] playerSkills = null)
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
            _maxHps = new float[totalUnits];
            _pendingDamage = new float[totalUnits];
            _pendingDotDamage = new float[totalUnits];
            _pendingHeal = new float[totalUnits];
            _roleIndices = new int[totalUnits];
            _teams = new byte[totalUnits];
            _alives = new bool[totalUnits];
            _isRangedUnit = new bool[totalUnits];
            _isLeaderUnit = new bool[totalUnits];
            _squadIndices = new int[totalUnits];
            _lastDamageSourceSquad = new int[totalUnits];
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
            _projSourceSquad = new int[config.MaxProjectiles];

            _statusEffects = new StatusEffectSystem(totalUnits);

            _playerSkills = playerSkills ?? Array.Empty<SkillDefinition>();
            _skillCooldowns = new float[_playerSkills.Length];
            _skillCastQueued = new bool[_playerSkills.Length];
            _skillCastPositions = new Vector2[_playerSkills.Length];

            _zoneSkills = new SkillDefinition[config.MaxSkillZones];
            _zoneSkillSlots = new int[config.MaxSkillZones];
            _zonePositions = new Vector2[config.MaxSkillZones];
            _zoneRemainings = new float[config.MaxSkillZones];
            _zoneTargetTeams = new byte[config.MaxSkillZones];

            _squadCount = armyA.Squads.Length + armyB.Squads.Length;
            _squadGenerals = new GeneralDefinition[_squadCount];
            _squadTeams = new byte[_squadCount];
            _squadGeneralUnits = new int[_squadCount];
            _squadCharges = new float[_squadCount];
            _squadActivationCounts = new int[_squadCount];
            _squadLastActivationTimes = new float[_squadCount];

            _roles = BuildRoleTable(armyA, armyB);

            int squadCursor = 0;
            SpawnArmy(armyA, TeamA, ref squadCursor);
            SpawnArmy(armyB, TeamB, ref squadCursor);

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

        public bool HasStatus(int index, StatusEffectType type) => _statusEffects.IsActive(index, type);

        /// <summary>현재 타겟 유닛 인덱스 (없으면 -1) — 테스트·디버그용.</summary>
        public int GetTargetIndex(int index) => _targets[index];

        // ── 분대/장군 조회 (뷰·테스트용) ──

        public int SquadCount => _squadCount;

        public int GetSquadIndex(int unitIndex) => _squadIndices[unitIndex];

        public bool IsLeader(int unitIndex) => _isLeaderUnit[unitIndex];

        /// <summary>분대 장군의 유닛 인덱스 (장군 없는 분대는 -1).</summary>
        public int GetGeneralUnit(int squadIndex) => _squadGeneralUnits[squadIndex];

        public bool IsGeneralAlive(int squadIndex)
        {
            int unit = _squadGeneralUnits[squadIndex];
            return unit != NoTarget && _alives[unit];
        }

        public float GetSquadCharge(int squadIndex) => _squadCharges[squadIndex];

        /// <summary>분대의 생존 병사 수 (장군 제외) — 전투 결과 집계·밸런싱 파이프라인용 (아웃게임 계약 survivals).</summary>
        public int CountSquadSurvivors(int squadIndex)
        {
            int count = 0;
            for (int i = 0; i < _unitCount; i++)
            {
                if (_alives[i] && _squadIndices[i] == squadIndex && !_isLeaderUnit[i])
                {
                    count++;
                }
            }
            return count;
        }

        public int GetSquadActivationCount(int squadIndex) => _squadActivationCounts[squadIndex];

        /// <summary>마지막 액티브 발동 시각(초). 발동 이력 없으면 음수 — 뷰 슬로모/발광 연출 트리거용.</summary>
        public float GetSquadLastActivationTime(int squadIndex) => _squadLastActivationTimes[squadIndex];

        /// <summary>패시브·버프가 반영된 유효 공격력 (치명타 제외) — 테스트·뷰 표시용.</summary>
        public float GetEffectiveAttackDamage(int index)
        {
            return _roles[_roleIndices[index]].AttackDamage * OutgoingDamageMultiplier(index);
        }

        public int SkillCount => _playerSkills.Length;

        public SkillDefinition GetSkill(int slot) => _playerSkills[slot];

        public float GetSkillCooldownRemaining(int slot) => _skillCooldowns[slot];

        /// <summary>
        /// 스킬 시전 예약 — 다음 틱 시작에 실행된다 (틱 정렬 명령: 시뮬 결정론·리플레이의 전제).
        /// 쿨다운 중이거나 같은 슬롯이 이미 예약돼 있으면 거부.
        /// </summary>
        public bool TryCastSkill(int slot, Vector2 position)
        {
            if (_finished || slot < 0 || slot >= _playerSkills.Length)
            {
                return false;
            }
            if (_skillCooldowns[slot] > 0f || _skillCastQueued[slot])
            {
                return false;
            }
            _skillCastQueued[slot] = true;
            _skillCastPositions[slot] = ClampToArena(position);
            return true;
        }

        public int ZoneCount => _zoneCount;

        /// <summary>뷰 장판 표현용 스냅샷.</summary>
        public readonly struct SkillZoneState
        {
            public readonly Vector2 Position;
            public readonly float Radius;
            public readonly float RemainingSeconds;
            /// <summary>이 장판을 만든 스킬 슬롯 — 뷰가 스킬 색을 조회하는 키.</summary>
            public readonly int SkillSlot;

            public SkillZoneState(Vector2 position, float radius, float remainingSeconds, int skillSlot)
            {
                Position = position;
                Radius = radius;
                RemainingSeconds = remainingSeconds;
                SkillSlot = skillSlot;
            }
        }

        public SkillZoneState GetZoneState(int index)
        {
            return new SkillZoneState(
                _zonePositions[index], _zoneSkills[index].Radius, _zoneRemainings[index], _zoneSkillSlots[index]);
        }

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

            // 0) 은신 타이머 — 시간 만료로 해제
            for (int i = 0; i < _unitCount; i++)
            {
                if (_alives[i] && _stealthRemaining[i] > 0f)
                {
                    _stealthRemaining[i] -= dt;
                    if (_stealthRemaining[i] <= 0f)
                    {
                        _stealthRemaining[i] = 0f;
                    }
                }
            }

            // 0.5) 플레이어 스킬: 예약된 시전 실행 → 쿨다운 감소 → 장판 유지(상태 재부여)
            for (int s = 0; s < _playerSkills.Length; s++)
            {
                if (_skillCastQueued[s])
                {
                    _skillCastQueued[s] = false;
                    ExecuteSkillCast(s);
                }
                if (_skillCooldowns[s] > 0f)
                {
                    _skillCooldowns[s] -= dt;
                }
            }
            for (int z = 0; z < _zoneCount;)
            {
                ApplySkillArea(_zoneSkills[z], _zonePositions[z], _zoneTargetTeams[z], damage: 0f);
                _zoneRemainings[z] -= dt;
                if (_zoneRemainings[z] <= 0f)
                {
                    RemoveZoneAt(z);
                }
                else
                {
                    z++;
                }
            }

            // 0.75) 장군 액티브 시간 충전 (TimeElapsed) — 사망/공격/킬 충전은 각 이벤트 지점에서.
            for (int s = 0; s < _squadCount; s++)
            {
                AddCharge(s, ChargeCondition.TimeElapsed, dt);
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

                if (_statusEffects.IsActive(i, StatusEffectType.Stun))
                {
                    continue; // 기절: 행동 정지 — 이동·공격 불가, 쿨다운 회복만 진행
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

            // 3.5) 상태이상 틱: 지속시간 감쇠 + 도트/회복 누적 — 정산(4번)보다 먼저.
            //      도트는 방어력 감쇠를 받지 않으므로 별도 누적기에 쌓인다.
            _statusEffects.Tick(dt, _unitCount, _alives, _pendingDotDamage, _pendingHeal);

            // 4) 누적 데미지·회복 정산 + 사망 처리 (동시 공격의 순서 이점 제거).
            //    받는 피해 배율(표식 증가/방진 감소/패시브 감소)은 여기서 일괄 적용된다.
            for (int i = 0; i < _unitCount; i++)
            {
                float damage = _pendingDamage[i];
                float dotDamage = _pendingDotDamage[i];
                float heal = _pendingHeal[i];
                if (damage <= 0f && dotDamage <= 0f && heal <= 0f)
                {
                    continue;
                }
                _pendingDamage[i] = 0f;
                _pendingDotDamage[i] = 0f;
                _pendingHeal[i] = 0f;
                if (!_alives[i])
                {
                    continue;
                }

                // 피해 계산 순서: [일반 피해 × 방어력 감쇠] + [도트(감쇠 없음)] → 표식/방진/패시브 배율.
                if (damage > 0f)
                {
                    damage *= _config.DefenseDamping(_roles[_roleIndices[i]].Defense);
                }
                damage += dotDamage;
                if (damage > 0f)
                {
                    float markMultiplier = _statusEffects.GetMagnitude(i, StatusEffectType.Mark);
                    if (markMultiplier > 0f)
                    {
                        damage *= markMultiplier;
                    }
                    float resistMultiplier = _statusEffects.GetMagnitude(i, StatusEffectType.DamageResist);
                    if (resistMultiplier > 0f)
                    {
                        damage *= resistMultiplier;
                    }
                    damage *= IncomingDamageMultiplier(i);
                }
                _hps[i] = MathF.Min(_hps[i] - damage + heal, _maxHps[i]);
                if (_hps[i] <= 0f)
                {
                    KillUnit(i);
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
                _stealthRemaining[attacker] = 0f; // 첫 공격으로 은신 해제
            }

            float damage = role.AttackDamage * RollCritMultiplier(attacker, role) * OutgoingDamageMultiplier(attacker);

            int attackerSquad = _squadIndices[attacker];
            AddCharge(attackerSquad, ChargeCondition.SquadAttacks, 1f);

            if (!role.IsRanged)
            {
                _pendingDamage[target] += damage;
                _lastDamageSourceSquad[target] = attackerSquad;
                return;
            }

            if (_projectileCount >= _config.MaxProjectiles)
            {
                // 투사체 버퍼 상한 도달 시 즉시 착탄으로 대체 — 결정론 유지를 위한 예외 경로.
                _pendingDamage[target] += damage;
                _lastDamageSourceSquad[target] = attackerSquad;
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
            _projSourceSquad[p] = attackerSquad;
        }

        /// <summary>
        /// 이번 공격의 치명타 배율. 무장된 확정 치명타(그림자 습격)가 있으면 그것을 소비하고 확률 추첨은 건너뛴다 —
        /// 확정 치명타의 정체성을 지키고 이중 적용을 막는다.
        /// 확률 추첨은 CritChancePercent가 0보다 클 때만 시드 RNG를 뽑는다:
        /// 확률 0인 데이터는 난수 소비 자체가 없어 기존 전투 결과가 그대로 재현된다.
        /// </summary>
        private float RollCritMultiplier(int attacker, RoleDefinition role)
        {
            float armed = _critPending[attacker];
            if (armed > 1f)
            {
                _critPending[attacker] = 1f;
                return armed;
            }
            if (role.CritChancePercent > 0f && _random.NextDouble() * 100.0 < role.CritChancePercent)
            {
                return _config.CritMultiplier;
            }
            return 1f;
        }

        /// <summary>공격력 배율: 장군 패시브(AttackPercent, 생존 중) × 상태 버프(AttackUp — 전투 함성).</summary>
        private float OutgoingDamageMultiplier(int unitIndex)
        {
            float multiplier = 1f;
            int squad = _squadIndices[unitIndex];
            GeneralDefinition general = _squadGenerals[squad];
            if (general != null && general.Passive == SquadPassive.AttackPercent && IsGeneralAlive(squad))
            {
                multiplier *= 1f + general.PassiveValue;
            }
            float attackUp = _statusEffects.GetMagnitude(unitIndex, StatusEffectType.AttackUp);
            if (attackUp > 0f)
            {
                multiplier *= attackUp;
            }
            return multiplier;
        }

        /// <summary>받는 피해 배율: 장군 패시브(DamageResistPercent, 생존 중). 상태 배율(표식/방진)은 정산부에서.</summary>
        private float IncomingDamageMultiplier(int unitIndex)
        {
            int squad = _squadIndices[unitIndex];
            GeneralDefinition general = _squadGenerals[squad];
            if (general != null && general.Passive == SquadPassive.DamageResistPercent && IsGeneralAlive(squad))
            {
                return MathF.Max(1f - general.PassiveValue, 0f);
            }
            return 1f;
        }

        /// <summary>사망 공통 경로: 카운트 갱신 + 충전 이벤트(사망/킬) + 장군 사망 시 게이지 소멸.</summary>
        private void KillUnit(int unitIndex)
        {
            _alives[unitIndex] = false;
            _teamAliveCounts[_teams[unitIndex]]--;

            int squad = _squadIndices[unitIndex];
            AddCharge(squad, ChargeCondition.SquadDeaths, 1f);

            int killerSquad = _lastDamageSourceSquad[unitIndex];
            if (killerSquad != NoSquad)
            {
                AddCharge(killerSquad, ChargeCondition.SquadKills, 1f);
            }

            if (_isLeaderUnit[unitIndex])
            {
                // 장군 사망 (기획 §6): 롤 유지 / 패시브는 생존 검사로 자연 소멸 / 충전 중 스킬·게이지 소멸.
                _squadCharges[squad] = 0f;
            }
        }

        /// <summary>
        /// 충전 게이지 가산 — 장군 생존 + 조건 일치 분대만. 임계치 도달 시 자동 발동 후 0으로 리셋 (재충전 가능).
        /// </summary>
        private void AddCharge(int squadIndex, ChargeCondition condition, float amount)
        {
            GeneralDefinition general = _squadGenerals[squadIndex];
            if (general == null || general.ChargeCondition != condition || general.ChargeRequired <= 0f)
            {
                return;
            }
            if (!IsGeneralAlive(squadIndex))
            {
                return;
            }
            _squadCharges[squadIndex] += amount;
            if (_squadCharges[squadIndex] >= general.ChargeRequired)
            {
                _squadCharges[squadIndex] = 0f;
                ActivateSquadSkill(squadIndex, general);
            }
        }

        /// <summary>
        /// 장군 액티브 발동 — 효과는 GimmickEffect 부대 스코프 케이스(데이터)로 결정된다. 장군별 분기 없음.
        /// 발동은 저빈도(전투당 2~3회 튜닝)라 분대원 선형 순회로 충분하다.
        /// </summary>
        private void ActivateSquadSkill(int squadIndex, GeneralDefinition general)
        {
            _squadActivationCounts[squadIndex]++;
            _squadLastActivationTimes[squadIndex] = _time;

            switch (general.ActiveEffect)
            {
                case GimmickEffect.SquadDamageResist: // 방진: 부대 전원 받는 피해 감소 (paramA=배율, duration=K초)
                    for (int i = 0; i < _unitCount; i++)
                    {
                        if (_alives[i] && _squadIndices[i] == squadIndex)
                        {
                            _statusEffects.Apply(i, StatusEffectType.DamageResist, general.ActiveDuration, general.ActiveParamA);
                        }
                    }
                    break;

                case GimmickEffect.SquadRestealthCrit: // 그림자 습격: 재은신(paramA초) + 다음 공격 치명타(paramB배)
                    for (int i = 0; i < _unitCount; i++)
                    {
                        if (_alives[i] && _squadIndices[i] == squadIndex)
                        {
                            _stealthRemaining[i] = general.ActiveParamA;
                            _critPending[i] = general.ActiveParamB;
                        }
                    }
                    break;

                case GimmickEffect.SquadVolley: // 일제 사격: 적 최대 밀집 지점에 부대 전원 발사
                    ExecuteSquadVolley(squadIndex, general);
                    break;

                case GimmickEffect.MarkStrongestEnemy: // 사냥 선포: 현재 체력 최고 적 표식 (paramA=받는 피해 배율)
                {
                    byte enemyTeam = _squadTeams[squadIndex] == TeamA ? TeamB : TeamA;
                    int best = NoTarget;
                    float bestHp = -1f;
                    for (int u = 0; u < _unitCount; u++)
                    {
                        if (_alives[u] && _teams[u] == enemyTeam && _hps[u] > bestHp)
                        {
                            bestHp = _hps[u];
                            best = u;
                        }
                    }
                    if (best != NoTarget)
                    {
                        _statusEffects.Apply(best, StatusEffectType.Mark, general.ActiveDuration, general.ActiveParamA);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 일제 사격: 적 최대 밀집 지점(반경 paramA 내 아군 최다 적 유닛 위치)을 찾아
        /// 부대 전원이 그 주변 산포 지점으로 일제 발사한다. 화살당 데미지 = 공격력 × paramB.
        /// 밀집 탐색은 발동 시에만 실행되는 저빈도 경로.
        /// </summary>
        private void ExecuteSquadVolley(int squadIndex, GeneralDefinition general)
        {
            byte enemyTeam = _squadTeams[squadIndex] == TeamA ? TeamB : TeamA;
            float scatterRadius = general.ActiveParamA;

            Vector2 densest = default;
            int bestCount = -1;
            for (int u = 0; u < _unitCount; u++)
            {
                if (!_alives[u] || _teams[u] != enemyTeam)
                {
                    continue;
                }
                int neighborCount = _grid.QueryCircle(_positions[u], scatterRadius, _queryBuffer);
                int count = 0;
                for (int k = 0; k < neighborCount; k++)
                {
                    int j = _queryBuffer[k];
                    if (_alives[j] && _teams[j] == enemyTeam)
                    {
                        count++;
                    }
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    densest = _positions[u];
                }
            }
            if (bestCount < 0)
            {
                return; // 적 전멸
            }

            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i] || _squadIndices[i] != squadIndex)
                {
                    continue;
                }
                if (_projectileCount >= _config.MaxProjectiles)
                {
                    break; // 버퍼 상한 — 남은 화살 생략 (상태 기반이라 결정론 유지)
                }
                RoleDefinition role = _roles[_roleIndices[i]];
                float angle = (float)_random.NextDouble() * MathF.PI * 2f;
                float offset = MathF.Sqrt((float)_random.NextDouble()) * scatterRadius;
                Vector2 impact = ClampToArena(densest + new Vector2(MathF.Cos(angle) * offset, MathF.Sin(angle) * offset));

                int p = _projectileCount++;
                _projLaunchPos[p] = _positions[i];
                _projImpactPos[p] = impact;
                _projLaunchTime[p] = _time;
                float speed = MathF.Max(role.ProjectileSpeed, 1f); // 근접 유닛 방어적 하한 — 데이터는 원거리 부대 전제
                _projImpactTime[p] = _time + MathF.Max(Vector2.Distance(_positions[i], impact) / speed, _config.TickDeltaTime);
                _projDamage[p] = role.AttackDamage * general.ActiveParamB;
                _projTeam[p] = _teams[i];
                _projArcHeight[p] = role.ProjectileArcHeight;
                _projSourceSquad[p] = squadIndex;
            }
        }

        /// <summary>예약된 스킬 시전 실행: 즉발 데미지·상태이상 1회 적용 + 장판이면 장판 등록.</summary>
        private void ExecuteSkillCast(int slot)
        {
            SkillDefinition skill = _playerSkills[slot];
            _skillCooldowns[slot] = skill.Cooldown;
            Vector2 position = _skillCastPositions[slot];
            // 플레이어 = A군 전제 (기획 §9) — 적대 스킬은 적군에, 아군 스킬(힐/함성)은 아군에 작용.
            byte targetTeam = skill.TargetsAllies ? TeamA : TeamB;

            ApplySkillArea(skill, position, targetTeam, skill.Damage);

            if (skill.ZoneDuration > 0f && _zoneCount < _config.MaxSkillZones)
            {
                int z = _zoneCount++;
                _zoneSkills[z] = skill;
                _zoneSkillSlots[z] = slot;
                _zonePositions[z] = position;
                _zoneRemainings[z] = skill.ZoneDuration;
                _zoneTargetTeams[z] = targetTeam;
            }
        }

        /// <summary>스킬 범위 효과 공용 경로 (즉발 시전·장판 틱 공용): 범위 내 대상 팀에 데미지/상태이상 부여.</summary>
        private void ApplySkillArea(SkillDefinition skill, Vector2 position, byte targetTeam, float damage)
        {
            float queryRadius = skill.Radius + _maxUnitRadius;
            int hitCount = _grid.QueryCircle(position, queryRadius, _queryBuffer);
            for (int k = 0; k < hitCount; k++)
            {
                int u = _queryBuffer[k];
                if (!_alives[u] || _teams[u] != targetTeam)
                {
                    continue;
                }
                float hitDistance = skill.Radius + _roles[_roleIndices[u]].UnitRadius;
                if (Vector2.DistanceSquared(position, _positions[u]) > hitDistance * hitDistance)
                {
                    continue;
                }
                if (damage > 0f)
                {
                    _pendingDamage[u] += damage;
                }
                if (skill.StatusDuration > 0f)
                {
                    _statusEffects.Apply(u, skill.AppliesStatus, skill.StatusDuration, skill.StatusMagnitude);
                }
            }
        }

        private void RemoveZoneAt(int z)
        {
            int last = --_zoneCount;
            _zoneSkills[z] = _zoneSkills[last];
            _zoneSkillSlots[z] = _zoneSkillSlots[last];
            _zonePositions[z] = _zonePositions[last];
            _zoneRemainings[z] = _zoneRemainings[last];
            _zoneTargetTeams[z] = _zoneTargetTeams[last];
            _zoneSkills[last] = null;
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
                    _lastDamageSourceSquad[u] = _projSourceSquad[p];
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
            _projSourceSquad[p] = _projSourceSquad[last];
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
                case TargetPriority.Poisoned:
                    return _statusEffects.IsActive(targetIndex, StatusEffectType.Poison);
                case TargetPriority.Marked:
                    return _statusEffects.IsActive(targetIndex, StatusEffectType.Mark);
                case TargetPriority.Leader:
                    return _isLeaderUnit[targetIndex];
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
            // 분대당 최대 2종 (병사 롤 + 장군 전투 롤)
            int maxRoles = (armyA.Squads.Length + armyB.Squads.Length) * 2;
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
                    GeneralDefinition general = squads[s].General;
                    if (general != null && IndexOfRole(table, count, general.CombatRole) < 0)
                    {
                        table[count++] = general.CombatRole;
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

        private void SpawnArmy(ArmyDefinition army, byte team, ref int squadCursor)
        {
            // A군은 -x에서 +x를 향하고, B군은 미러. anchor.x = 전선에서 뒤로 물러난 깊이.
            float direction = team == TeamA ? -1f : 1f;
            for (int s = 0; s < army.Squads.Length; s++)
            {
                SquadDefinition squad = army.Squads[s];
                RoleDefinition role = squad.Role;
                int roleIndex = IndexOfRole(_roles, _roles.Length, role);

                int squadIndex = squadCursor++;
                _squadGenerals[squadIndex] = squad.General;
                _squadTeams[squadIndex] = team;
                _squadGeneralUnits[squadIndex] = NoTarget;
                _squadCharges[squadIndex] = 0f;
                _squadActivationCounts[squadIndex] = 0;
                _squadLastActivationTimes[squadIndex] = -1f;

                var anchor = new Vector2(
                    direction * (_config.FrontLineOffsetX + squad.Anchor.X),
                    squad.Anchor.Y);

                int rows = (int)MathF.Ceiling(MathF.Sqrt(squad.Count));
                float spacing = role.UnitRadius * 2.5f;
                float jitter = role.UnitRadius * 0.5f;

                for (int k = 0; k < squad.Count; k++)
                {
                    int row = k / rows;
                    int col = k % rows;
                    float x = anchor.X + direction * (row - rows * 0.5f) * spacing
                              + ((float)_random.NextDouble() * 2f - 1f) * jitter;
                    float y = anchor.Y + (col - rows * 0.5f) * spacing
                              + ((float)_random.NextDouble() * 2f - 1f) * jitter;

                    SpawnUnit(role, roleIndex, team, squadIndex, new Vector2(x, y), isLeader: false);
                }

                if (squad.General != null)
                {
                    // 장군 스폰 위치 = 분대 선두 (기획 §5), 측면 중앙.
                    // 앞뒤 정도는 데이터(LeadRankOffset, 랭크 단위 −1~+1)가 결정한다 — 밸런싱 튜닝 대상.
                    RoleDefinition generalRole = squad.General.CombatRole;
                    int generalRoleIndex = IndexOfRole(_roles, _roles.Length, generalRole);
                    float frontX = anchor.X + direction * (-(rows * 0.5f) - squad.General.LeadRankOffset) * spacing;
                    int generalUnit = SpawnUnit(generalRole, generalRoleIndex, team, squadIndex, new Vector2(frontX, anchor.Y), isLeader: true);
                    _squadGeneralUnits[squadIndex] = generalUnit;
                }
            }
        }

        /// <summary>유닛 공통 스폰 경로 — 병사와 장군이 같은 초기화를 탄다 (전용 클래스 없음).</summary>
        private int SpawnUnit(RoleDefinition role, int roleIndex, byte team, int squadIndex, Vector2 position, bool isLeader)
        {
            int i = _unitCount++;
            _positions[i] = ClampToArena(position);
            _hps[i] = role.MaxHp;
            _maxHps[i] = role.MaxHp;
            _pendingDamage[i] = 0f;
            _pendingDotDamage[i] = 0f;
            _pendingHeal[i] = 0f;
            _roleIndices[i] = roleIndex;
            _teams[i] = team;
            _alives[i] = true;
            _isRangedUnit[i] = role.IsRanged;
            _isLeaderUnit[i] = isLeader;
            _squadIndices[i] = squadIndex;
            _lastDamageSourceSquad[i] = NoSquad;
            _targets[i] = NoTarget;
            _attackCooldowns[i] = 0f;
            _stealthRemaining[i] = role.MovePattern == MovePattern.StealthDash ? role.MoveParamA : 0f;
            _critPending[i] = 1f;
            _teamAliveCounts[team]++;
            return i;
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
