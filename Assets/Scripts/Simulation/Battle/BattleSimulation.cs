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

        // ── 근접 공격 위치 (2026-08-09) ──
        /// <summary>공격 목적지를 AttackRange 경계보다 살짝 안쪽으로 잡는 여유 배율 — 정확히 경계에
        /// 도착하면 부동소수 오차로 "<=AttackRange" 판정이 영원히 거짓이 되는 문제를 막는다
        /// (실측으로 확인: 유닛이 dist=AttackRange에서 멈춘 채 공격을 영원히 못 함, 2026-08-09).</summary>
        private const float AttackApproachMargin = 0.95f;

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
            /// <summary>집중 대상 적 분대 (NoTarget = 제한 없음) — 분대 단위 타게팅(2026-08-04).</summary>
            private readonly int _focusSquad;

            public AliveEnemyFilter(
                BattleSimulation sim, byte targetTeam, int excludeIndex,
                bool hasPriority, TargetPriority priority, int focusSquad)
            {
                _sim = sim;
                _targetTeam = targetTeam;
                _excludeIndex = excludeIndex;
                _hasPriority = hasPriority;
                _priority = priority;
                _focusSquad = focusSquad;
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
                if (_focusSquad != NoTarget && _sim._squadIndices[unitIndex] != _focusSquad)
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
        /// <summary>공격자(자기 자신) 유닛 인덱스로 색인 — 근접 공격 시 타겟의 좌(-1)/우(+1)
        /// 어느 쪽에 설 지. 새 타겟이 배정될 때 한 번만 무작위로 정해지고 그 타겟을 유지하는
        /// 동안 고정된다 (2026-08-09: 점유/예약 없이 각자 독립적으로 좌우만 무작위 선택).</summary>
        private readonly float[] _attackSide;

        // ── 뷰 전용 전투 이벤트 (공격/치명타/피격 연출용) ──
        // 시뮬 상태에는 아무 영향이 없다: 결과·RNG 소비·틱 순서 불변 (결정론 유지, BalanceLab 무관).
        // 고정 버퍼에 쌓기만 하고(틱 루프 무할당) 소비는 뷰 책임 — 헤드리스(CLI)처럼 아무도 안 읽으면
        // 상한에서 조용히 멈출 뿐이다. 한 프레임에 여러 틱이 돌 수 있어 시뮬은 스스로 비우지 않는다.
        private const int ViewEventCapacity = 2048;
        private readonly ViewEvent[] _viewEvents = new ViewEvent[ViewEventCapacity];
        private int _viewEventCount;

        /// <summary>상태이상 공용 시스템 — 부정 5종 + 긍정 효과 전부 이 하나가 처리 (불변조건 5).</summary>
        private readonly StatusEffectSystem _statusEffects;

        // 분대(장군) 상태 — 분대 수는 소수(그리드 칸 수준)라 선형 순회로 충분.
        private readonly GeneralDefinition[] _squadGenerals;
        private readonly byte[] _squadTeams;
        /// <summary>분대 장군의 유닛 인덱스. NoTarget = 장군 없는 분대.</summary>
        private readonly int[] _squadGeneralUnits;
        // 분대 단위 집중 타게팅 (2026-08-04): 분대마다 중심점이 가장 가까운 적 분대를 정하고
        // 소속 유닛은 그 분대의 유닛만 노린다 — 한 분대가 반으로 갈라져 흩어지는 것을 막는다.
        // NoTarget = 살아있는 적 분대 없음 (유닛 타게팅은 무제한 폴백).
        private readonly int[] _squadFocusEnemies;
        private readonly Vector2[] _squadCentroids;
        private readonly int[] _squadAliveCounts;

        // 분대 대형/교전 상태 (2026-08-09): 분대는 항상 두 상태 중 하나 — Formation(대형을 지키며
        // focus 분대 쪽으로 이동, 개별 타게팅 없음) / Fighting(개별 유닛이 알아서 싸움). 분대 하나는
        // 전부 같은 역할군이라(SpawnArmy) 교전 판정 반경도 분대당 하나로 충분하다.
        /// <summary>분대의 역할군 — 스폰 시 1회 캡처 (장군은 다른 역할군일 수 있으나 대형 계산엔 무시).</summary>
        private readonly RoleDefinition[] _squadRoles;
        /// <summary>true = Fighting(개별 전투 중), false = Formation(대형 이동/재정렬 중).</summary>
        private readonly bool[] _squadFighting;
        /// <summary>대형의 가상 기준점 — 타이트할 때만 focus 분대 쪽으로 전진한다.</summary>
        private readonly Vector2[] _squadFormationAnchors;
        /// <summary>이번 틱 기준, 대형 슬롯(앵커+오프셋)에서 가장 많이 벗어난 생존 유닛까지의 거리.</summary>
        private readonly float[] _squadTightness;
        /// <summary>대형 중심 간 거리가 이 값 이하로 좁혀지면 Fighting 전환 (역할군 사거리 + 대형 반경 기반, 스폰 시 1회 계산).</summary>
        private readonly float[] _squadEngageRanges;
        /// <summary>유닛별 대형 내 상대 위치 — 대형 이동 목적지 계산에 사용. 스폰 시 1회 캡처하고,
        /// 이후 그 분대의 생존자 수가 바뀌면 ReflowSquadFormation()이 다시 채운다.</summary>
        private readonly Vector2[] _formationOffsets;
        /// <summary>분대 명단(장군 포함) 시작 인덱스 — 그 분대의 유닛은 스폰 시 항상 연속 구간을 차지한다.</summary>
        private readonly int[] _squadMemberStart;
        /// <summary>분대 명단 총원(죽어도 안 줄어듦) — [start, start+count)가 그 분대의 전체 유닛 인덱스 범위.</summary>
        private readonly int[] _squadMemberCount;
        /// <summary>분대 명단 재사용 스크래치 버퍼 — CollectAliveSquadMembers 결과를 담는다 (틱당 할당 없음).
        /// 대형 재정렬과 타겟 인덱스 매칭이 공유(한 호출 안에서 순차 사용이라 안전).</summary>
        private readonly int[] _squadRosterBuffer;
        /// <summary>마지막으로 ReflowSquadFormation을 실행했을 때의 생존자 수 — 이 값이 현재와 다르면 재정렬 트리거.</summary>
        private readonly int[] _squadLastReflowedAliveCount;

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
        /// <summary>한 팀이 전멸해 승자는 정해졌지만, 생존 팀 전 분대가 대형 복귀할 때까지 종료를 보류 중.</summary>
        private bool _pendingFinish;
        private int _pendingWinner;

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
            _attackSide = new float[totalUnits];

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
            _squadFocusEnemies = new int[_squadCount];
            _squadCentroids = new Vector2[_squadCount];
            _squadAliveCounts = new int[_squadCount];
            _squadRoles = new RoleDefinition[_squadCount];
            _squadFighting = new bool[_squadCount];
            _squadFormationAnchors = new Vector2[_squadCount];
            _squadTightness = new float[_squadCount];
            _squadEngageRanges = new float[_squadCount];
            _formationOffsets = new Vector2[totalUnits];
            _squadMemberStart = new int[_squadCount];
            _squadMemberCount = new int[_squadCount];
            _squadRosterBuffer = new int[totalUnits];
            _squadLastReflowedAliveCount = new int[_squadCount];

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

            _grid.Rebuild(_positions, _unitCount);
            UpdateSquadFocus();

            // 대형 기준점 초기화: 스폰 위치가 곧 대형이다 — 분대 중심을 앵커로,
            // 각 유닛의 상대 위치를 대형 오프셋으로 1회 캡처해 이후 불변으로 유지한다.
            // 모든 분대는 Formation 상태로 시작(_squadFighting 기본값 false) — 개별 타게팅은
            // Fighting 전환 후 Tick()의 재탐색 루프가 담당한다(스폰 시 즉시 배정하지 않음).
            var squadFormationRadius = new float[_squadCount];
            for (int s = 0; s < _squadCount; s++)
            {
                _squadFormationAnchors[s] = _squadCentroids[s];
            }
            for (int i = 0; i < _unitCount; i++)
            {
                int s = _squadIndices[i];
                _formationOffsets[i] = _positions[i] - _squadCentroids[s];
                float offsetLength = _formationOffsets[i].Length();
                if (offsetLength > squadFormationRadius[s])
                {
                    squadFormationRadius[s] = offsetLength;
                }
            }
            for (int s = 0; s < _squadCount; s++)
            {
                _squadEngageRanges[s] = _squadRoles[s].AttackRange + squadFormationRadius[s] + _config.FormationEngageRangeMargin;
                // 스폰 시점엔 전원 생존이라 위에서 캡처한 오프셋이 곧 "이미 재정렬됨" 상태 —
                // ReflowSquadFormation()은 생존자 수가 이 값과 달라질 때만(=사상자 발생) 트리거된다.
                _squadLastReflowedAliveCount[s] = _squadAliveCounts[s];
            }

            // 재탐색 시차 균등 배분 (Fighting 전환 후 첫 재탐색 주기에 사용).
            for (int i = 0; i < _unitCount; i++)
            {
                _nextRetargetTimes[i] = _config.RetargetInterval * (i + 1) / _unitCount;
            }
        }

        public int UnitCount => _unitCount;

        public float TickDeltaTime => _config.TickDeltaTime;

        public bool Finished => _finished;

        /// <summary>Finished가 true일 때만 유효.</summary>
        public BattleResult Result => _result;

        /// <summary>뷰 전용 전투 이벤트 종류 — 애니메이션 트리거에 1:1 대응한다.</summary>
        public enum ViewEventType : byte
        {
            Attack,     // 일반 공격 실행 (근접 타격/투사체 발사 시점)
            CritAttack, // 치명타 공격 실행
            Damaged,    // 직접 피해를 받음 (도트 제외 — 매 틱 반복이라 피격 모션 스팸이 된다)
        }

        public readonly struct ViewEvent
        {
            public readonly int Unit;
            public readonly ViewEventType Type;

            public ViewEvent(int unit, ViewEventType type)
            {
                Unit = unit;
                Type = type;
            }
        }

        public int ViewEventCount => _viewEventCount;

        public ViewEvent GetViewEvent(int index) => _viewEvents[index];

        /// <summary>이번 프레임의 이벤트를 소비한 뒤 뷰가 호출한다 — 시뮬은 스스로 비우지 않는다.</summary>
        public void ClearViewEvents() => _viewEventCount = 0;

        private void EmitViewEvent(int unit, ViewEventType type)
        {
            if (_viewEventCount < _viewEvents.Length)
            {
                _viewEvents[_viewEventCount++] = new ViewEvent(unit, type);
            }
        }

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

            // 1) 분대 대형/교전 상태 갱신 → focus 분대 재계산(Fighting 중엔 고정) →
            //    재탐색: 타겟 무효(사망/은신)는 즉시 재선택, 주기 도래 시엔 교전 유지 규칙 적용.
            //    Formation 상태인 분대는 아직 교전 전이라 개별 타게팅을 하지 않는다.
            UpdateSquadFocus();
            UpdateSquadFormationState(dt);
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i] || !_squadFighting[_squadIndices[i]])
                {
                    continue;
                }
                int target = _targets[i];
                bool targetInvalid = target == NoTarget || !_alives[target] || _stealthRemaining[target] > 0f;
                if (targetInvalid)
                {
                    SetTarget(i, SelectTarget(i));
                    _nextRetargetTimes[i] = _time + _config.RetargetInterval;
                }
                else if (_time >= _nextRetargetTimes[i])
                {
                    SetTarget(i, ReevaluateTarget(i, target));
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

                int squadIndex = _squadIndices[i];
                if (!_squadFighting[squadIndex])
                {
                    // 대형 이동: 목적지는 분대 앵커 + 내 대형 오프셋 — 개별 타겟팅/공격 없음.
                    RoleDefinition formationRole = _roles[_roleIndices[i]];
                    Vector2 slot = _squadFormationAnchors[squadIndex] + _formationOffsets[i];
                    Vector2 toSlot = slot - _positions[i];
                    float slotDistance = toSlot.Length();
                    if (slotDistance > 1e-5f)
                    {
                        float step = MathF.Min(formationRole.MoveSpeed * dt, slotDistance);
                        _positions[i] = ClampToArena(_positions[i] + toSlot * (step / slotDistance));
                    }
                    continue;
                }

                int target = _targets[i];
                if (target == NoTarget || !_alives[target] || _stealthRemaining[target] > 0f)
                {
                    continue;
                }

                RoleDefinition role = _roles[_roleIndices[i]];
                // 공격 판정은 실제 타겟과의 거리(사거리 이내)로 — 목적지 좌표는 "어디로 이동할지"만
                // 정할 뿐 공격 허가의 하드 게이트로 쓰지 않는다. 도착을 하드 게이트로 쓰면, 타겟이
                // 아직 자기 타겟을 쫓아 계속 움직이는 중일 때(교전 락이 없어 멈추지 않음) 목적지도
                // 계속 흔들려서 영원히 못 붙는다(실측으로 확인 — 전투가 시간 상한까지 안 끝나고
                // 무승부가 남). 캡 제거로는 이 문제가 안 풀린다 — 별도로 교전 락을 넣기 전까지는
                // 이 완화된 판정을 유지한다.
                float centerDistance = Vector2.Distance(_positions[i], _positions[target]);

                if (centerDistance <= role.AttackRange)
                {
                    if (_attackCooldowns[i] <= 0f)
                    {
                        Attack(i, target, role, centerDistance);
                        _attackCooldowns[i] = role.AttackInterval;
                    }
                }
                else
                {
                    // ApproachTarget과 StealthDash 모두 목적지 접근 — StealthDash의 차이(은신)는 상태로 처리.
                    // 이동 궤적이 다른 새 패턴은 여기서 케이스 추가.
                    Vector2 destination = ComputeApproachDestination(i, target, role);
                    Vector2 toDestination = destination - _positions[i];
                    float destDistance = toDestination.Length();
                    if (destDistance > 1e-5f)
                    {
                        float speed = role.MoveSpeed;
                        if (_stealthRemaining[i] > 0f && role.MoveParamB > 0f)
                        {
                            speed *= role.MoveParamB; // StealthDash: 은신 중 이속 배율 (돌진 가속)
                        }
                        // 목적지를 지나치지 않게 스텝을 남은 거리로 클램프 — 밀림·떨림 방지.
                        float step = MathF.Min(speed * dt, destDistance);
                        _positions[i] = ClampToArena(_positions[i] + toDestination * (step / destDistance));
                    }
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
                    EmitViewEvent(i, ViewEventType.Damaged); // 직접 피해만 — 정산 시점 = 실제 맞는 순간
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

                    // 부분 겹침 허용: 반경 합 × 비율 안까지 파고들어야 분리를 시작하고,
                    // 남은 겹침도 틱당 일정 비율만 해소한다 — 하드 제약(닿는 즉시 전량 분리)이
                    // 난전에서 만들던 밀림·튕김을 없애고 밀집 전투를 허용한다 (수치는 BattleConfig).
                    float separationDistance =
                        (role.UnitRadius + _roles[_roleIndices[j]].UnitRadius) * _config.SeparationOverlapRatio;
                    Vector2 delta = _positions[j] - _positions[i];
                    float distance = delta.Length();
                    if (distance >= separationDistance)
                    {
                        continue;
                    }

                    Vector2 normal = distance > 1e-5f ? delta / distance : new Vector2(1f, 0f);
                    Vector2 separation =
                        normal * ((separationDistance - distance) * 0.5f * _config.SeparationStrength);
                    _positions[i] = ClampToArena(_positions[i] - separation);
                    _positions[j] = ClampToArena(_positions[j] + separation);
                }
            }

            // 6) 승패 판정: 팀 전멸은 승자만 확정해두고 즉시 끝내지 않는다 — 생존 팀 전 분대가
            //    대형으로 복귀할 때까지 보류(2026-08-09, 분대 대형 이동 도입). 시간 초과는 안전판이라
            //    대형 복귀를 기다리지 않고 무조건 즉시 종료한다.
            if (!_pendingFinish && (_teamAliveCounts[TeamA] == 0 || _teamAliveCounts[TeamB] == 0))
            {
                _pendingWinner = _teamAliveCounts[TeamA] > 0 ? TeamA
                    : _teamAliveCounts[TeamB] > 0 ? TeamB
                    : BattleResult.DrawWinner;
                _pendingFinish = true;
            }
            if (_pendingFinish && AllSquadsReformed())
            {
                Finish(_pendingWinner);
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

            float critMultiplier = RollCritMultiplier(attacker, role);
            float damage = role.AttackDamage * critMultiplier * OutgoingDamageMultiplier(attacker);
            EmitViewEvent(attacker, critMultiplier > 1f ? ViewEventType.CritAttack : ViewEventType.Attack);

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

            // 죽은 유닛은 다음 틱부터 순회에서 스킵되므로 SetTarget을 다시 탈 일이 없다 —
            // 물고 있던 타겟의 배정 카운트를 여기서 직접 반납하지 않으면 영구 누수로 남는다.
            SetTarget(unitIndex, NoTarget);

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
            int focusSquad = _squadFocusEnemies[_squadIndices[unitIndex]];

            TargetPriority[] priorities = role.Priorities;
            int currentRank = PriorityRank(currentTarget, priorities);
            for (int p = 0; p < currentRank; p++)
            {
                // 우선순위 기믹도 분대 경계를 넘지 않는다 (2026-08-09: 분대는 항상 같이 다닌다 — 사용자 결정).
                int candidate = FindByPositionFilter(unitIndex, new AliveEnemyFilter(this, enemyTeam, unitIndex, hasPriority: true, priorities[p], focusSquad), role.PositionFilter);
                if (candidate != NoTarget)
                {
                    return candidate;
                }
            }

            float centerDistance = Vector2.Distance(_positions[unitIndex], _positions[currentTarget]);
            if (centerDistance <= role.AttackRange)
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

        /// <summary>
        /// 분대의 집중 대상 적 분대 갱신 — 살아있는 유닛의 중심점끼리 가장 가까운 적 분대를 고른다
        /// (동률은 낮은 인덱스, 결정론 유지). 유닛 타게팅은 이 분대 안에서만 후보를 찾아
        /// 분대가 반으로 갈라져 흩어지지 않는다 (2026-08-04 사용자 결정).
        /// </summary>
        private void UpdateSquadFocus()
        {
            for (int s = 0; s < _squadCount; s++)
            {
                _squadCentroids[s] = Vector2.Zero;
                _squadAliveCounts[s] = 0;
            }
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i])
                {
                    continue;
                }
                int s = _squadIndices[i];
                _squadCentroids[s] += _positions[i];
                _squadAliveCounts[s]++;
            }
            for (int s = 0; s < _squadCount; s++)
            {
                if (_squadAliveCounts[s] > 0)
                {
                    _squadCentroids[s] /= _squadAliveCounts[s];
                }
            }
            for (int s = 0; s < _squadCount; s++)
            {
                if (_squadAliveCounts[s] == 0)
                {
                    _squadFocusEnemies[s] = NoTarget;
                    continue;
                }
                // Fighting 중인 분대는 focus를 고정한다 — 안 그러면 싸우던 상대가 전멸하기도
                // 전에 "지금 더 가까운 다른 분대"로 갈아타 버려서, 전멸 판정(UpdateSquadFormationState)이
                // 엉뚱한 분대를 보고 있게 된다. 전멸 후에만(Formation 복귀 후) 다시 계산한다.
                if (_squadFighting[s])
                {
                    continue;
                }
                _squadFocusEnemies[s] = NoTarget;
                float best = float.MaxValue;
                for (int e = 0; e < _squadCount; e++)
                {
                    if (_squadTeams[e] == _squadTeams[s] || _squadAliveCounts[e] == 0)
                    {
                        continue;
                    }
                    float distanceSquared = Vector2.DistanceSquared(_squadCentroids[s], _squadCentroids[e]);
                    if (distanceSquared < best)
                    {
                        best = distanceSquared;
                        _squadFocusEnemies[s] = e;
                    }
                }
            }
        }

        /// <summary>
        /// 분대 대형/교전 상태 전환 — UpdateSquadFocus() 직후에 호출해야 한다(신선한
        /// _squadCentroids·_squadFocusEnemies 필요). Formation: 대형 슬롯(앵커+오프셋)이 전부
        /// 타이트할 때만 앵커가 focus 분대 쪽으로 전진하고, 타이트 + 사거리 안이면 Fighting 전환.
        /// Fighting: focus 분대가 전멸하면 Formation으로 복귀(개별 타겟은 건드리지 않아도 Tick()의
        /// 이동/공격 루프가 상태로 분기하므로 자연히 멈춘다).
        /// </summary>
        private void UpdateSquadFormationState(float dt)
        {
            for (int s = 0; s < _squadCount; s++)
            {
                _squadTightness[s] = 0f;
            }
            for (int i = 0; i < _unitCount; i++)
            {
                if (!_alives[i])
                {
                    continue;
                }
                int s = _squadIndices[i];
                Vector2 slot = _squadFormationAnchors[s] + _formationOffsets[i];
                float dist = Vector2.Distance(_positions[i], slot);
                if (dist > _squadTightness[s])
                {
                    _squadTightness[s] = dist;
                }
            }

            for (int s = 0; s < _squadCount; s++)
            {
                if (_squadAliveCounts[s] == 0)
                {
                    continue;
                }

                if (_squadFighting[s])
                {
                    int focus = _squadFocusEnemies[s];
                    if (focus == NoTarget || _squadAliveCounts[focus] == 0)
                    {
                        // Formation으로 복귀 — 자기 피해가 0이었어도(무손실 승리) 무조건 재정렬한다.
                        // 아래 "생존자 수가 바뀌었으면"만으로는 무손실 승리 시 트리거가 전혀 안 걸려서
                        // 앵커·오프셋이 스폰 시점 값 그대로 남고, 그 결과 장군을 포함한 전원이 전투로
                        // 흩어진 지금 위치에서 엉뚱하게 먼 스폰 슬롯까지 걸어가 버렸다(2026-08-09 확인).
                        _squadFighting[s] = false;
                        ReflowSquadFormation(s);
                    }
                    else
                    {
                        continue; // 계속 Fighting — 대형 로직 불필요
                    }
                }
                else if (_squadAliveCounts[s] != _squadLastReflowedAliveCount[s])
                {
                    // 이미 Formation 상태 — 행군 중 광역기 등으로 사상자가 나면 그 자리에서 재정렬.
                    ReflowSquadFormation(s);
                }

                int focusSquad = _squadFocusEnemies[s];
                if (focusSquad == NoTarget)
                {
                    continue; // 갈 곳 없음 — 대형 유지한 채 대기
                }

                bool tight = _squadTightness[s] <= _config.FormationTightnessTolerance;
                if (tight)
                {
                    Vector2 toEnemy = _squadCentroids[focusSquad] - _squadFormationAnchors[s];
                    float distance = toEnemy.Length();
                    if (distance > 1e-5f)
                    {
                        float step = MathF.Min(_squadRoles[s].MoveSpeed * dt, distance);
                        _squadFormationAnchors[s] += toEnemy * (step / distance);
                    }
                }

                float centroidDistance = Vector2.Distance(_squadCentroids[s], _squadCentroids[focusSquad]);
                if (tight && centroidDistance <= _squadEngageRanges[s])
                {
                    _squadFighting[s] = true;
                }
            }
        }

        /// <summary>분대 squadIndex의 생존 유닛을 스폰 순서 그대로 buffer 앞부터 채워 넣고 개수를
        /// 반환한다. 그 분대의 유닛은 스폰 시 항상 연속 구간을 차지하므로(SpawnArmy) 죽은 유닛만
        /// 건너뛰면 빈틈없이 나열된 "지금 살아있는 명단"이 매번 즉석에서 나온다 — 별도 리스트
        /// 자료구조나 스왑 제거 없이도 충분하다. excludeStealthed=true면 은신 중인 유닛도 건너뛴다
        /// (적 타겟 후보 조회용 — 은신 중엔 피타겟 제외가 불변조건). 대형 재정렬처럼 아군 자신의
        /// 명단을 볼 때는 은신 여부가 무관하므로 기본값 false.</summary>
        private int CollectAliveSquadMembers(int squadIndex, int[] buffer, bool excludeStealthed = false)
        {
            int start = _squadMemberStart[squadIndex];
            int count = _squadMemberCount[squadIndex];
            int n = 0;
            for (int k = 0; k < count; k++)
            {
                int unit = start + k;
                if (_alives[unit] && (!excludeStealthed || _stealthRemaining[unit] <= 0f))
                {
                    buffer[n++] = unit;
                }
            }
            return n;
        }

        /// <summary>
        /// 대형 내 상대 오프셋 — 장군(또는 앵커) 바로 뒤 rankOffset칸째부터 시작해 병사를
        /// FormationColumnWidth열 종대로 채운다(넘치면 다음 랭크). followerIndex는 그 분대 안에서
        /// 병사(장군 제외) 순서(0-index, 스폰/생존 순서 — 스폰 시와 재정렬 시 공통 사용). "뒤"는
        /// retreatDirection 부호 방향(적과 반대쪽, 팀별로 다름) — 랭크가 커질수록 그만큼 물러난다.
        /// 좌우(열)는 폭 중앙 기준으로 대칭 배치.
        /// </summary>
        private Vector2 ColumnFormationOffset(int followerIndex, int rankOffset, float retreatDirection, float spacing)
        {
            int columns = Math.Max(_config.FormationColumnWidth, 1); // 0 이하 설정값 방어
            int rank = followerIndex / columns + rankOffset;
            int col = followerIndex % columns;
            float x = retreatDirection * rank * spacing;
            float y = (col - (columns - 1) * 0.5f) * spacing;
            return new Vector2(x, y);
        }

        /// <summary>
        /// 분대 squadIndex의 대형을 지금 생존자 수에 맞게 다시 짠다. 죽은 유닛의 원래 슬롯을
        /// 비워두지 않고, 생존자를 스폰 순서 그대로 장군 맨 앞 + N열 종대(ColumnFormationOffset,
        /// SpawnArmy 최초 배치와 같은 공식)로 채운다. 중심(앵커)은 장군이 살아있으면 장군의 현재
        /// 위치 — 장군은 그 자리에 서 있고 나머지가 장군 뒤로 도열한다(오프셋 0으로 고정, 이동
        /// 없음). 장군이 없거나 죽었으면 지금 살아있는 유닛들의 실제 위치 중심(_squadCentroids)을
        /// 대체 앵커로 쓰고, 이 경우 병사들은 랭크 0(앵커 바로 그 줄)부터 채운다. 장군 위치가
        /// 아니라 평균 중심(대형 밖 허공일 수 있음)을 쓰면 전투 직후 다들 엉뚱한 곳까지 걸어가게
        /// 되는 문제가 있었다(2026-08-09 사용자 피드백). 새 오프셋 기준 타이트니스도 여기서 바로
        /// 재계산해, 호출 직후 이번 틱 판정에 즉시 반영된다.
        /// </summary>
        private void ReflowSquadFormation(int squadIndex)
        {
            int n = CollectAliveSquadMembers(squadIndex, _squadRosterBuffer);
            _squadLastReflowedAliveCount[squadIndex] = n;
            if (n == 0)
            {
                return; // 전멸 — 재정렬할 대상이 없다.
            }

            int generalUnit = _squadGeneralUnits[squadIndex];
            bool hasLivingGeneral = generalUnit != NoTarget && _alives[generalUnit];
            Vector2 anchor = hasLivingGeneral ? _positions[generalUnit] : _squadCentroids[squadIndex];
            _squadFormationAnchors[squadIndex] = anchor;

            RoleDefinition role = _squadRoles[squadIndex];
            float retreatDirection = _squadTeams[squadIndex] == TeamA ? -1f : 1f;
            float spacing = role.UnitRadius * _config.FormationSpacingMultiplier;
            int rankOffset = hasLivingGeneral ? 1 : 0;

            float formationRadius = 0f;
            float tightness = 0f;
            int followerIndex = 0;
            for (int k = 0; k < n; k++)
            {
                int unit = _squadRosterBuffer[k];
                if (hasLivingGeneral && unit == generalUnit)
                {
                    _formationOffsets[unit] = Vector2.Zero; // 장군은 그 자리(앵커)에 그대로 — 이동 없음
                    continue;
                }

                Vector2 offset = ColumnFormationOffset(followerIndex, rankOffset, retreatDirection, spacing);
                followerIndex++;
                _formationOffsets[unit] = offset;

                float offsetLength = offset.Length();
                if (offsetLength > formationRadius)
                {
                    formationRadius = offsetLength;
                }

                float slotDistance = Vector2.Distance(_positions[unit], anchor + offset);
                if (slotDistance > tightness)
                {
                    tightness = slotDistance;
                }
            }

            _squadEngageRanges[squadIndex] = role.AttackRange + formationRadius + _config.FormationEngageRangeMargin;
            _squadTightness[squadIndex] = tightness;
        }

        /// <summary>승패 판정 보류 중, 생존 팀의 모든 분대가 Formation 상태 + 대형 타이트까지 끝났는가.</summary>
        private bool AllSquadsReformed()
        {
            for (int s = 0; s < _squadCount; s++)
            {
                if (_squadAliveCounts[s] == 0)
                {
                    continue;
                }
                if (_squadFighting[s] || _squadTightness[s] > _config.FormationTightnessTolerance)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// _targets 배정을 갱신한다. 근접 유닛이 새 타겟을 받으면 좌/우 공격 위치(_attackSide)를
        /// 한 번 무작위로 정해 그 타겟을 유지하는 동안 고정한다 (2026-08-09: 점유/예약 없이
        /// 각자 독립적으로 선택 — 여러 명이 같은 쪽을 골라도 충돌이 없으니 무방하다).
        /// _targets[unitIndex]를 직접 대입하는 대신 반드시 이 메서드를 거칠 것 (KillUnit의 반납도
        /// 동일 경로).
        /// </summary>
        private void SetTarget(int unitIndex, int newTarget)
        {
            int oldTarget = _targets[unitIndex];
            if (oldTarget == newTarget)
            {
                // 타겟이 실제로 안 바뀌었으면 아무것도 하지 않는다 — 재확인마다 다시 굴리면
                // 좌/우가 매번 바뀌어 목적지가 흔들리는 문제가 생긴다 (실측으로 확인).
                return;
            }
            _targets[unitIndex] = newTarget;
            if (newTarget != NoTarget && !_isRangedUnit[unitIndex])
            {
                _attackSide[unitIndex] = _random.NextDouble() < 0.5 ? -1f : 1f;
            }
        }

        /// <summary>
        /// 공격 목적지 좌표 계산 (2026-08-09). 반경 합은 쓰지 않는다 — 충돌이 없으니 AttackRange
        /// 하나만 "거리" 기준으로 삼는다.
        /// · 원거리: 공격자→타겟 직선상에서 AttackRange만큼 떨어진 지점.
        /// · 근접: 타겟 위치에서 화면 좌/우(시뮬 X축) 중 SetTarget()이 정해둔 방향으로
        ///   AttackRange만큼 떨어진 지점.
        /// </summary>
        private Vector2 ComputeApproachDestination(int attacker, int target, RoleDefinition role)
        {
            Vector2 targetPos = _positions[target];

            if (!role.IsRanged)
            {
                return targetPos + new Vector2(_attackSide[attacker] * role.AttackRange * AttackApproachMargin, 0f);
            }

            Vector2 toTarget = targetPos - _positions[attacker];
            float distance = toTarget.Length();
            Vector2 direction = distance > 1e-5f ? toTarget / distance : new Vector2(1f, 0f);
            return targetPos - direction * role.AttackRange * AttackApproachMargin;
        }

        /// <summary>
        /// 타겟팅: 우선순위 목록(중독·표식 기믹)도 기본 탐색도 전부 focusSquad(분대가 집중하는
        /// 적 분대) 안에서만 찾는다 (2026-08-09: 분대는 항상 같이 다닌다 — 예외 없음).
        /// focusSquad 안에 유효한 후보가 없으면 NoTarget — 다음 재탐색 주기에 다시 시도한다.
        /// </summary>
        private int SelectTarget(int unitIndex)
        {
            RoleDefinition role = _roles[_roleIndices[unitIndex]];
            byte enemyTeam = _teams[unitIndex] == TeamA ? TeamB : TeamA;
            int focusSquad = _squadFocusEnemies[_squadIndices[unitIndex]];

            if (focusSquad == NoTarget)
            {
                // 살아있는 적 분대가 없다 — 곧 전투 종료. 분대 경계를 넘어서까지 찾지 않는다.
                return NoTarget;
            }

            // 분대는 항상 같이 다닌다 (2026-08-09 사용자 결정) — 우선순위 기믹도 일반 탐색도
            // focusSquad 경계를 절대 넘지 않는다. 그 분대가 전멸하면 UpdateSquadFocus()가
            // 다음 틱에 자동으로 새 분대를 배정한다.
            TargetPriority[] priorities = role.Priorities;
            for (int p = 0; p < priorities.Length; p++)
            {
                int candidate = FindByPositionFilter(unitIndex, new AliveEnemyFilter(this, enemyTeam, unitIndex, hasPriority: true, priorities[p], focusSquad), role.PositionFilter);
                if (candidate != NoTarget)
                {
                    return candidate;
                }
            }

            return SelectIndexPairedTarget(unitIndex, focusSquad, role.PositionFilter,
                new AliveEnemyFilter(this, enemyTeam, unitIndex, hasPriority: false, default, focusSquad));
        }

        /// <summary>
        /// 인덱스 기반 타겟 매칭 (2026-08-09) — Nearest 필터일 때만 적용: 내 분대의 "지금 살아있는
        /// 유닛 명단"에서 내가 몇 번째인지(내 순번, 사상자가 나면 매번 다시 계산되어 당겨짐) 구하고,
        /// 상대 focus 분대의 "지금 살아있고 타겟 가능한(은신 제외) 명단"에서 내 순번 % 명단 길이
        /// 번째를 고른다. 양쪽 생존자 수가 같으면 "생존자 기준 K번째 ↔ K번째"로 정확히 대칭
        /// 매칭되고, 내 순번이 상대보다 많으면 모듈로로 여러 명이 같은 상대에게 겹쳐 화력이 자연히
        /// 집중된다(사용자 요청 — 타겟당 캡을 강제하는 대신 화력 분산 자체를 없앰). 순수 정수
        /// 연산이라 난수를 소비하지 않는다. Farthest 필터(현재 암살자)거나 상대 명단이 비어있으면
        /// (전원 은신 등) 기존 FindByPositionFilter로 폴백한다.
        /// </summary>
        private int SelectIndexPairedTarget(int unitIndex, int focusSquad, PositionFilter positionFilter, in AliveEnemyFilter fallbackFilter)
        {
            if (positionFilter != PositionFilter.Nearest)
            {
                return FindByPositionFilter(unitIndex, fallbackFilter, positionFilter);
            }

            int mySquad = _squadIndices[unitIndex];
            int myAliveCount = CollectAliveSquadMembers(mySquad, _squadRosterBuffer);
            int mySlot = 0;
            for (int k = 0; k < myAliveCount; k++)
            {
                if (_squadRosterBuffer[k] == unitIndex)
                {
                    mySlot = k;
                    break;
                }
            }

            int enemyAliveCount = CollectAliveSquadMembers(focusSquad, _squadRosterBuffer, excludeStealthed: true);
            if (enemyAliveCount == 0)
            {
                // focusSquad 전원 은신 등 — 명단이 비어 매칭할 상대가 없다.
                return FindByPositionFilter(unitIndex, fallbackFilter, positionFilter);
            }

            return _squadRosterBuffer[mySlot % enemyAliveCount];
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
                _squadRoles[squadIndex] = role;
                _squadGeneralUnits[squadIndex] = NoTarget;
                _squadCharges[squadIndex] = 0f;
                _squadActivationCounts[squadIndex] = 0;
                _squadLastActivationTimes[squadIndex] = -1f;
                // 분대 유닛은 이 for문 안에서 항상 연속된 인덱스로 스폰된다(장군 포함) —
                // 별도 리스트 없이 (시작, 개수)만으로 "분대 명단"을 얻는다 (2026-08-09).
                int memberStart = _unitCount;

                var anchor = new Vector2(
                    direction * (_config.FrontLineOffsetX + squad.Anchor.X),
                    squad.Anchor.Y);
                float spacing = role.UnitRadius * _config.FormationSpacingMultiplier;

                // 최초 스폰 배치도 재정렬(ReflowSquadFormation)과 같은 공식 — 장군이 맨 앞(대형
                // 기준점)에 서고 병사는 그 뒤로 N열 종대 (2026-08-09, 사용자 요청). 장군의 정확한
                // 선두 위치는 데이터(LeadRankOffset, 랭크 단위 −1~+1)로 살짝 조정 가능.
                Vector2 generalPosition = anchor;
                int rankOffset = 0;
                if (squad.General != null)
                {
                    generalPosition = anchor + new Vector2(direction * -squad.General.LeadRankOffset * spacing, 0f);
                    rankOffset = 1; // 병사는 장군 바로 뒤 랭크부터 시작
                }

                for (int k = 0; k < squad.Count; k++)
                {
                    Vector2 offset = ColumnFormationOffset(k, rankOffset, direction, spacing);
                    SpawnUnit(role, roleIndex, team, squadIndex, generalPosition + offset, isLeader: false);
                }

                if (squad.General != null)
                {
                    RoleDefinition generalRole = squad.General.CombatRole;
                    int generalRoleIndex = IndexOfRole(_roles, _roles.Length, generalRole);
                    int generalUnit = SpawnUnit(generalRole, generalRoleIndex, team, squadIndex, generalPosition, isLeader: true);
                    _squadGeneralUnits[squadIndex] = generalUnit;
                }

                _squadMemberStart[squadIndex] = memberStart;
                _squadMemberCount[squadIndex] = _unitCount - memberStart;
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
