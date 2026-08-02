using System;
using System.Collections.Generic;
using NHN.Data;
using NHN.Infra;
using NHN.Simulation.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NHN.Presentation.Battle
{
    /// <summary>
    /// 전투 뷰 테스트 씬 진입점. 시뮬 생성/고정 틱 구동/뷰 동기화를 담당한다.
    /// 유닛·투사체별 MonoBehaviour 없음 — 캐시된 Transform 배열만 갱신한다.
    /// </summary>
    public sealed class BattleTestBootstrap : MonoBehaviour
    {
        /// <summary>프레임 급락 시 틱 폭주를 막는 프레임당 틱 상한.</summary>
        private const int MaxTicksPerFrame = 4;

        /// <summary>투사체 발사 높이(유닛 몸통) — 뷰 표현 상수.</summary>
        private const float ProjectileLaunchHeight = 1f;

        /// <summary>은신 유닛 표시 알파 — 뷰 표현 상수.</summary>
        private const float StealthAlpha = 0.35f;

        // 스킬 조준/장판/플래시 디스크 표현 상수 — 전부 뷰 전용.
        private const float DiscThickness = 0.02f;
        private const float AimDiscY = 0.05f;
        private const float ZoneDiscY = 0.03f;
        private const float AimAlpha = 0.3f;
        private const float ZoneAlpha = 0.25f;
        /// <summary>장판이 사라지기 전 알파 페이드 구간(초).</summary>
        private const float ZoneFadeSeconds = 1f;
        private const int MaxFlashFx = 8;
        private const float FlashDuration = 0.4f;
        private const float FlashAlpha = 0.55f;

        // 상태이상 유닛 틴트 — 가독성: 기절=노랑, 중독=초록, 화상=주황,
        // 표식=마젠타, 공버프=주황금(발광 표시 필수 — v4 §9), 회복=연녹, 방진=청은 (마스크 비트 순).
        private const float StatusTintStrength = 0.55f;
        private const byte StunMask = 1;
        private const byte PoisonMask = 2;
        private const byte BurnMask = 4;
        private const byte MarkMask = 8;
        private const byte AttackUpMask = 16;
        private const byte HealMask = 32;
        private const byte ResistMask = 64;
        private static readonly Color StunTint = new Color(1f, 0.92f, 0.3f);
        private static readonly Color PoisonTint = new Color(0.2f, 0.9f, 0.2f);
        private static readonly Color BurnTint = new Color(1f, 0.45f, 0.1f);
        private static readonly Color MarkTint = new Color(0.9f, 0.25f, 0.8f);
        private static readonly Color AttackUpTint = new Color(1f, 0.68f, 0.1f);
        private static readonly Color HealTint = new Color(0.5f, 1f, 0.6f);
        private static readonly Color ResistTint = new Color(0.55f, 0.75f, 1f);
        /// <summary>장군 하이라이트 — 롤 색에 금색 혼합으로 병사와 즉시 구분 (기획 §4).</summary>
        private static readonly Color GeneralHighlight = new Color(1f, 0.85f, 0.25f);

        [Serializable]
        private struct SquadSetup
        {
            public RoleData role;
            public int count;
            [Tooltip("x=전선으로부터 깊이(+뒤), y=측면 오프셋")]
            public Vector2 anchor;
            [Tooltip("분대 리더(장군) — 비우면 노멀/무장군 분대 (v4 §5)")]
            public GeneralData general;
        }

        [SerializeField] private BattleConfigSO config;
        [SerializeField] private SquadSetup[] armyA;
        [SerializeField] private SquadSetup[] armyB;
        [SerializeField] private GameObject unitPrefab;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform unitContainer;
        [SerializeField] private Transform projectileContainer;
        [SerializeField] private BattleHud hud;

        [Header("플레이어 스킬 (기획 §8 — 스킬 버튼 탭 → 전장 탭으로 위치 지정)")]
        [SerializeField] private SkillData[] playerSkills;
        [Tooltip("조준 레이캐스트용 카메라 — 미지정 시 Camera.main")]
        [SerializeField] private Camera worldCamera;

        [Header("아웃게임 연동 선행 준비 (RunBattle 경로 — BattleBridge 커넥터가 사용)")]
        [SerializeField] private BattleCatalog catalog;
        [SerializeField] private EncounterTable encounterTable;

        private BattleSimulation _sim;
        private GameObjectPool _unitPool;
        private GameObjectPool _projectilePool;
        private GameObject[] _unitObjects;
        private Transform[] _unitTransforms;
        private bool[] _unitVisible;
        private Color[] _unitColors;
        private bool[] _unitStealthShown;
        /// <summary>표시 중인 상태이상 마스크 캐시 — 변화가 있는 프레임에만 색을 갱신한다.</summary>
        private byte[] _unitStatusShown;
        private Material _baseMaterial;
        private Material _stealthMaterial;

        // 롤별 3D 모델 뷰 (RoleData.viewPrefab — 비면 기본 캡슐 프리팹):
        // 프리팹마다 풀을 따로 두고, 전투 시작 시 실제 분대 구성 수량만큼만 프리웜한다
        // (상한 기준 프리웜은 모델 5종 × MaxUnits라 낭비가 너무 큼).
        private readonly Dictionary<GameObject, GameObjectPool> _unitPoolsByPrefab =
            new Dictionary<GameObject, GameObjectPool>();
        /// <summary>전투 시작 시 프리팹별 수요 집계 버퍼 (매 전투 재사용 — 틱 루프 밖).</summary>
        private readonly Dictionary<GameObject, int> _poolDemand = new Dictionary<GameObject, int>();
        /// <summary>유닛 인덱스 → 꺼내온 풀 — 사망/정리 시 올바른 풀로 되돌린다.</summary>
        private GameObjectPool[] _unitSourcePools;

        /// <summary>
        /// 풀 인스턴스별 렌더러·머티리얼 캐시. 모델 프리팹은 렌더러가 여러 개(몸체+모자+무기)라
        /// 은신 스왑 복원에 원본 머티리얼 배열이 필요하고, 스폰마다 GetComponentsInChildren를
        /// 다시 돌지 않도록 인스턴스 수명 동안 1회만 만든다.
        /// </summary>
        private sealed class UnitViewCache
        {
            public Renderer[] Renderers;
            public Material[][] OriginalMaterials;
            public Material[][] StealthMaterials;
            /// <summary>모델 프리팹의 애니메이터 — 리깅 없는 프리팹(기본 몸체·캡슐)은 null.</summary>
            public Animator Animator;
        }

        /// <summary>사망 애니메이션을 보여준 뒤 풀로 되돌리기까지의 시간 (Die 클립 앞부분만 사용).</summary>
        private const float DeathLingerSeconds = 1.6f;
        /// <summary>이동 방향 회전 속도(도/초)와 회전을 시작하는 최소 이동 속도(유닛/초) — 뷰 표현 상수.</summary>
        private const float UnitTurnDegreesPerSecond = 540f;
        private const float TurnSpeedThreshold = 0.5f;
        private static readonly int SpeedParamId = Animator.StringToHash("Speed");
        private static readonly int DieParamId = Animator.StringToHash("Die");
        private static readonly int AttackParamId = Animator.StringToHash("Attack");
        private static readonly int CritParamId = Animator.StringToHash("Crit");
        private static readonly int HitParamId = Animator.StringToHash("Hit");
        private static readonly int IdleStateId = Animator.StringToHash("Idle");

        /// <summary>이동속도 산출용 직전 프레임 위치 — Idle/Run 전환은 뷰가 위치 변화로 판단한다.</summary>
        private Vector3[] _unitPrevPositions;
        /// <summary>사망 연출 중(시뮬에서는 이미 죽음) — 타이머가 끝나면 풀로 반환.</summary>
        private bool[] _unitDying;
        private float[] _unitDeathTimers;

        private readonly Dictionary<GameObject, UnitViewCache> _viewCaches =
            new Dictionary<GameObject, UnitViewCache>();
        private UnitViewCache[] _unitViewSets;

        // 팀별 바라보는 방향 — 진행 방향(적진 ±X)과 카메라(-Z에서 내려다봄)를 절충한 3/4 사선.
        // 정면(±90)으로 두면 클레이 모델의 얇은 옆면(두께 0.26)만 보여 실루엣이 죽는다.
        // 모델 프리팹의 정면은 +Z 기준 (캡슐은 회전 무의미라 영향 없음).
        private static readonly Quaternion TeamAFacing = Quaternion.Euler(0f, -135f, 0f);
        private static readonly Quaternion TeamBFacing = Quaternion.Euler(0f, 135f, 0f);

        // 플레이어 스킬 뷰 상태
        /// <summary>현재 적용된 스킬 구성 원본 — 같은 구성 재적용(Restart)을 건너뛰기 위한 참조 비교용.</summary>
        private SkillData[] _activeSkillAssets;

        // 스킬 시전 이펙트 (SkillData.castEffectPrefab — 비면 기존 디스크만):
        // 슬롯당 소형 링 풀을 전투 시작(로드아웃 적용) 시 프리웜 — 전투 중 Instantiate 금지 규칙 준수.
        // 장판 스킬은 장판 지속시간만큼 재생 후 정지, 즉발 스킬은 고정 시간 재생. 정지 후에는
        // 파티클 잔향이 자연 소멸할 시간을 주고 비활성화한다.
        private const int SkillFxPerSlot = 2;
        private const float InstantFxSeconds = 2.5f;
        private const float FxFadeTailSeconds = 1.5f;
        private GameObject[,] _skillFxObjects;
        private ParticleSystem[,] _skillFxParticles;
        private float[,] _skillFxRemainings;
        private bool[,] _skillFxStopping;
        private int[] _skillFxNext;
        private SkillDefinition[] _skillDefinitions;
        private Color[] _skillColors;
        private int _armedSkillSlot = -1;
        private Transform _aimIndicator;
        private Renderer _aimRenderer;
        private Transform[] _zoneDiscs;
        private Renderer[] _zoneRenderers;
        private int _zoneShownCount;
        private Transform[] _flashTransforms;
        private Renderer[] _flashRenderers;
        private float[] _flashRemainings;
        private int _nextFlashIndex;
        private GameObject[] _projObjects;
        private Transform[] _projTransforms;
        private int _projVisibleCount;
        private float _accumulator;
        private bool _resultShown;
        private MaterialPropertyBlock _propertyBlock;

        // 아웃게임 연동(RunBattle) 상태 — 요청이 있으면 Restart도 같은 요청을 재실행한다 (콜백은 1회만).
        private BattleRequest _activeRequest;
        private Action<BattleOutcome> _onFinished;
        private readonly List<string> _playerSquadIds = new List<string>();
        private readonly List<BattleRequestBuilder.SquadAssets> _viewSquadsA = new List<BattleRequestBuilder.SquadAssets>();
        private readonly List<BattleRequestBuilder.SquadAssets> _viewSquadsB = new List<BattleRequestBuilder.SquadAssets>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        private void Awake()
        {
            // 에디터 자동 검증 편의 — 모바일 런타임에는 영향 없음.
            Application.runInBackground = true;

            _propertyBlock = new MaterialPropertyBlock();

            // 상한(config.MaxUnits) 기준 프리웜 — 인스펙터 구성뿐 아니라 런타임 RunBattle 요청(아웃게임 연동)도
            // 재할당 없이 수용하기 위함 (풀은 초과 시 예외로 즉시 드러난다).
            int maxUnits = config.MaxUnits;
            _unitPool = new GameObjectPool(unitPrefab, unitContainer, maxUnits);
            _projectilePool = new GameObjectPool(projectilePrefab, projectileContainer, config.MaxUnits);
            _unitObjects = new GameObject[maxUnits];
            _unitTransforms = new Transform[maxUnits];
            _unitVisible = new bool[maxUnits];
            _unitViewSets = new UnitViewCache[maxUnits];
            _unitSourcePools = new GameObjectPool[maxUnits];
            _unitPrevPositions = new Vector3[maxUnits];
            _unitDying = new bool[maxUnits];
            _unitDeathTimers = new float[maxUnits];
            _unitColors = new Color[maxUnits];
            _unitStealthShown = new bool[maxUnits];
            _unitStatusShown = new byte[maxUnits];
            _projObjects = new GameObject[config.MaxUnits];
            _projTransforms = new Transform[config.MaxUnits];

            _baseMaterial = unitPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
            _stealthMaterial = CreateStealthMaterial(_baseMaterial);

            ApplySkillLoadout(playerSkills);
            if (worldCamera == null)
            {
                worldCamera = Camera.main; // 초기화 시점 1회 조회
            }
            CreateSkillFxObjects();
            StartBattle();
        }

        /// <summary>
        /// 활성 스킬 구성(시뮬 정의 + HUD 라벨·색)을 적용한다. 씬 기본은 인스펙터 전체 목록이고,
        /// 연동 경로는 캐릭터 선택으로 거른 목록을 전투 시작마다 다시 적용한다 (초기화 경로 — 틱 루프 아님).
        /// </summary>
        private void ApplySkillLoadout(SkillData[] skills)
        {
            if (_activeSkillAssets == skills)
            {
                return; // Restart 등 같은 구성 재적용은 건너뛴다
            }
            _activeSkillAssets = skills;
            int skillCount = skills == null ? 0 : skills.Length;
            _skillDefinitions = new SkillDefinition[skillCount];
            _skillColors = new Color[skillCount];
            for (int s = 0; s < skillCount; s++)
            {
                _skillDefinitions[s] = skills[s].ToDefinition();
                _skillColors[s] = skills[s].SkillColor;
            }
            RebuildSkillFxPools(skills, skillCount);
            hud.Initialize(this, _skillDefinitions);
            for (int s = 0; s < skillCount; s++)
            {
                hud.SetSkillColor(s, _skillColors[s]);
            }
        }

        /// <summary>HUD Restart 버튼에서도 호출된다 — 연동 요청이 있으면 같은 요청을 재실행한다 (결과 콜백은 1회만).</summary>
        public void StartBattle()
        {
            if (_activeRequest != null)
            {
                StartRequestBattle(_activeRequest, onFinished: null);
                return;
            }

            _viewSquadsA.Clear();
            _viewSquadsB.Clear();
            AppendViewSquads(armyA, _viewSquadsA);
            AppendViewSquads(armyB, _viewSquadsB);
            BeginBattle(new BattleSimulation(
                config.ToConfig(), BuildArmy(armyA), BuildArmy(armyB), config.Seed, _skillDefinitions));
        }

        /// <summary>
        /// 아웃게임 연동 진입점 (선행 준비): 요청 → 카탈로그/적 구성 해석 → 전투 실행 → 종료 시 결과 콜백 1회.
        /// 머지 후 BattleBridge 커넥터(Assets/Docs/Integration 템플릿)가 이 메서드만 호출하면 된다.
        /// </summary>
        public void RunBattle(BattleRequest request, Action<BattleOutcome> onFinished)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (catalog == null || encounterTable == null)
            {
                throw new InvalidOperationException(
                    "BattleTestBootstrap의 catalog/encounterTable이 배선되지 않았습니다 — 연동 경로 사용 불가");
            }
            _activeRequest = request;
            StartRequestBattle(request, onFinished);
        }

        private void StartRequestBattle(BattleRequest request, Action<BattleOutcome> onFinished)
        {
            _onFinished = onFinished;
            // 캐릭터가 스킬을 결정한다 (§5.2.5) — 해석 실패·미전달이면 전체 스킬 폴백.
            ApplySkillLoadout(BattleRequestBuilder.SelectPlayerSkills(request.playerSkillId, catalog, playerSkills));
            _playerSquadIds.Clear();
            _viewSquadsA.Clear();
            _viewSquadsB.Clear();
            BattleConfig battleConfig = config.ToConfig();
            ArmyDefinition playerArmy = BattleRequestBuilder.BuildPlayerArmy(
                request, catalog, battleConfig, _viewSquadsA, _playerSquadIds);
            // 적 구성: 아웃게임이 실어 보낸 enemySquads가 정본(2026-07-29 계약), 비어 있으면(씬 단독
            // 실행·연동 스모크) encounterId로 EncounterTable을 조회하는 기존 폴백을 쓴다.
            ArmyDefinition enemyArmy = request.enemySquads.Count > 0
                ? BattleRequestBuilder.BuildSquads(request.enemySquads, catalog, battleConfig, _viewSquadsB, null)
                : BattleRequestBuilder.BuildEnemyArmy(
                    request.encounterId, encounterTable, catalog, battleConfig, _viewSquadsB);
            BeginBattle(new BattleSimulation(battleConfig, playerArmy, enemyArmy, request.seed, _skillDefinitions));
        }

        private void BeginBattle(BattleSimulation sim)
        {
            ReleaseAllViews();
            _sim = sim;
            _accumulator = 0f;
            _resultShown = false;
            _armedSkillSlot = -1;
            ResetSkillFxViews();
            hud.Clear();

            // 유닛 인덱스는 (A군 분대 순서 → B군 분대 순서) — ArmyDefinition의 계약과 동일하게 순회한다.
            PrewarmUnitPools();
            int unitIndex = 0;
            SpawnArmyViews(_viewSquadsA, isTeamB: false, ref unitIndex);
            SpawnArmyViews(_viewSquadsB, isTeamB: true, ref unitIndex);
        }

        /// <summary>롤의 뷰 프리팹 — 미지정 롤(또는 롤 없음)은 기본 캡슐 프리팹 폴백.</summary>
        private GameObject ViewPrefabOf(RoleData role)
        {
            return role != null && role.ViewPrefab != null ? role.ViewPrefab : unitPrefab;
        }

        private GameObjectPool GetUnitPool(GameObject prefab)
        {
            if (prefab == unitPrefab)
            {
                return _unitPool;
            }
            if (!_unitPoolsByPrefab.TryGetValue(prefab, out GameObjectPool pool))
            {
                pool = new GameObjectPool(prefab, unitContainer, 0);
                _unitPoolsByPrefab[prefab] = pool;
            }
            return pool;
        }

        /// <summary>
        /// 이번 전투의 분대 구성으로 프리팹별 필요 수량을 집계해 부족분만 프리웜한다 —
        /// 전투 시작(초기화) 경로라 Instantiate 허용, 틱 루프에서는 풀 Get/Release만 쓴다.
        /// </summary>
        private void PrewarmUnitPools()
        {
            _poolDemand.Clear();
            TallyPoolDemand(_viewSquadsA);
            TallyPoolDemand(_viewSquadsB);
            foreach (KeyValuePair<GameObject, int> demand in _poolDemand)
            {
                GetUnitPool(demand.Key).EnsureCapacity(demand.Value);
            }
        }

        private void TallyPoolDemand(List<BattleRequestBuilder.SquadAssets> squads)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                GameObject prefab = ViewPrefabOf(squads[s].Role);
                if (prefab == unitPrefab)
                {
                    continue; // 기본 풀은 Awake에서 상한만큼 프리웜돼 있다
                }
                _poolDemand.TryGetValue(prefab, out int count);
                _poolDemand[prefab] = count + squads[s].Count + (squads[s].General != null ? 1 : 0);
            }
        }

        private static void AppendViewSquads(SquadSetup[] setups, List<BattleRequestBuilder.SquadAssets> target)
        {
            for (int s = 0; s < setups.Length; s++)
            {
                target.Add(new BattleRequestBuilder.SquadAssets(setups[s].role, setups[s].general, setups[s].count));
            }
        }

        /// <summary>연동 경로 스모크 테스트 — 플레이 중 컴포넌트 컨텍스트 메뉴에서 실행 (머지 전 개발용).</summary>
        [ContextMenu("연동 경로 테스트: RunBattle(enc_NormalBattle)")]
        private void RunBridgePathSample()
        {
            var request = new BattleRequest { encounterId = "enc_NormalBattle", seed = config.Seed };
            request.playerSquads.Add(new SquadRequest
            {
                squadId = "sample-1", roleId = "Warrior", generalId = "WarriorGeneral",
                soldierCount = 20, slotX = 1f, slotY = 0.5f,
            });
            request.playerSquads.Add(new SquadRequest
            {
                squadId = "sample-2", roleId = "Hunter", generalId = "HunterGeneral",
                soldierCount = 8, slotX = 0.4f, slotY = 0.5f,
            });
            request.playerSquads.Add(new SquadRequest
            {
                squadId = "sample-3", soldierCount = 15, slotX = 0.8f, slotY = 0.2f, // roleId 없음 = 노멀 분대
            });
            RunBattle(request, outcome => Debug.Log(
                $"[연동 스모크] victory={outcome.Victory}, 생존: " +
                string.Join(", ", outcome.Survivals.ConvertAll(sv => $"{sv.SquadId}={sv.SurvivedSoldierCount}"))));
        }

        private void Update()
        {
            if (_sim == null)
            {
                return;
            }

            _accumulator += Time.deltaTime;
            float tickDeltaTime = _sim.TickDeltaTime;
            int ticksThisFrame = 0;
            while (_accumulator >= tickDeltaTime && ticksThisFrame < MaxTicksPerFrame)
            {
                _sim.Tick();
                _accumulator -= tickDeltaTime;
                ticksThisFrame++;
            }
            if (_accumulator >= tickDeltaTime)
            {
                _accumulator %= tickDeltaTime;
            }

            ConsumeViewEvents();

            // 종료 후에는 보간하지 않는다: Tick()이 종료 가드로 이전 위치를 더 갱신하지 않아
            // (이전 ≠ 현재)가 영구히 남고, alpha가 매 틱 주기 0→1을 반복하며 그 사이를
            // 왕복 보간해 생존 유닛이 부들부들 떨리는 현상이 생긴다 — 현재 위치 고정으로 해결.
            float alpha = _sim.Finished ? 1f : _accumulator / tickDeltaTime;
            SyncUnitViews(alpha);
            SyncProjectileViews(alpha);
            HandleSkillInput();
            SyncZoneViews();
            UpdateFlashFx(Time.deltaTime);
            UpdateSkillFx(Time.deltaTime);
            hud.SyncSkills(_sim, _armedSkillSlot);

            if (_sim.Finished && !_resultShown)
            {
                _resultShown = true;
                BattleResult result = _sim.Result;
                string winner = result.Winner == 0 ? "A군 승리"
                    : result.Winner == 1 ? "B군 승리"
                    : "무승부";
                hud.ShowResult($"{winner}  (생존 A:{result.SurvivorsTeamA}  B:{result.SurvivorsTeamB})");

                if (_onFinished != null)
                {
                    // 콜백은 1회 — Restart로 같은 요청을 재실행해도 아웃게임에 결과가 중복 보고되지 않는다.
                    Action<BattleOutcome> callback = _onFinished;
                    _onFinished = null;
                    callback(BattleRequestBuilder.BuildOutcome(_sim, _playerSquadIds));
                }
            }
        }

        /// <summary>
        /// 시뮬이 이번 프레임 동안 쌓은 전투 이벤트(공격/치명타/피격)를 애니메이터 트리거로 옮긴다.
        /// 시뮬은 스스로 비우지 않으므로(한 프레임 다중 틱 누적) 소비 후 여기서 비운다.
        /// </summary>
        private void ConsumeViewEvents()
        {
            int count = _sim.ViewEventCount;
            for (int e = 0; e < count; e++)
            {
                BattleSimulation.ViewEvent viewEvent = _sim.GetViewEvent(e);
                int unit = viewEvent.Unit;
                if (!_unitVisible[unit] || _unitDying[unit])
                {
                    continue; // 이미 정리됐거나 사망 연출 중 — 트리거가 Die를 덮지 않게
                }
                Animator animator = _unitViewSets[unit].Animator;
                if (animator == null)
                {
                    continue; // 리깅 없는 프리팹(캡슐 폴백)
                }
                switch (viewEvent.Type)
                {
                    case BattleSimulation.ViewEventType.Attack:
                        animator.SetTrigger(AttackParamId);
                        break;
                    case BattleSimulation.ViewEventType.CritAttack:
                        animator.SetTrigger(CritParamId);
                        break;
                    case BattleSimulation.ViewEventType.Damaged:
                        animator.SetTrigger(HitParamId);
                        break;
                }
            }
            _sim.ClearViewEvents();
        }

        private void SyncUnitViews(float alpha)
        {
            for (int i = 0; i < _sim.UnitCount; i++)
            {
                if (!_unitVisible[i])
                {
                    continue;
                }
                if (_unitDying[i])
                {
                    // 사망 연출 중 — 자리 고정, 타이머가 끝나면 풀로 반환.
                    _unitDeathTimers[i] -= Time.deltaTime;
                    if (_unitDeathTimers[i] <= 0f)
                    {
                        ReleaseUnitView(i);
                    }
                    continue;
                }
                if (!_sim.IsAlive(i))
                {
                    Animator animator = _unitViewSets[i].Animator;
                    if (animator != null)
                    {
                        // 애니메이터가 있으면 즉시 제거하지 않고 사망 클립을 잠깐 보여준다.
                        animator.SetFloat(SpeedParamId, 0f);
                        animator.SetTrigger(DieParamId);
                        _unitDying[i] = true;
                        _unitDeathTimers[i] = DeathLingerSeconds;
                    }
                    else
                    {
                        ReleaseUnitView(i);
                    }
                    continue;
                }

                bool stealthed = _sim.IsStealthed(i);
                byte statusMask = ComputeStatusMask(i);
                if (stealthed != _unitStealthShown[i] || statusMask != _unitStatusShown[i])
                {
                    ApplyUnitVisual(i, stealthed, statusMask);
                }

                Vector3 position = SimViewMapper.ToWorld(_sim.GetInterpolatedPosition(i, alpha));
                float deltaTime = Time.deltaTime;
                if (deltaTime > 0.0001f)
                {
                    Vector3 delta = position - _unitPrevPositions[i];
                    float speed = delta.magnitude / deltaTime;
                    // Idle/Run 전환은 뷰가 위치 변화(속도)로 판단 — 시뮬에 뷰 전용 API를 요구하지 않는다.
                    Animator unitAnimator = _unitViewSets[i].Animator;
                    if (unitAnimator != null)
                    {
                        unitAnimator.SetFloat(SpeedParamId, speed);
                    }
                    // 이동 방향으로 부드럽게 회전 — 밀림·분리 같은 미세 이동(문턱 미만)에는 돌지 않아
                    // 난전에서 방향이 파닥거리지 않는다. 멈추면 마지막 방향을 유지한다.
                    delta.y = 0f;
                    if (speed > TurnSpeedThreshold && delta.sqrMagnitude > 1e-8f)
                    {
                        _unitTransforms[i].localRotation = Quaternion.RotateTowards(
                            _unitTransforms[i].localRotation,
                            Quaternion.LookRotation(delta),
                            UnitTurnDegreesPerSecond * deltaTime);
                    }
                }
                _unitPrevPositions[i] = position;
                _unitTransforms[i].localPosition = position;
            }
        }

        /// <summary>유닛 뷰를 풀로 되돌리고 인덱스 상태를 정리한다 (사망·전투 정리 공용).</summary>
        private void ReleaseUnitView(int unitIndex)
        {
            _unitSourcePools[unitIndex].Release(_unitObjects[unitIndex]);
            _unitObjects[unitIndex] = null;
            _unitTransforms[unitIndex] = null;
            _unitViewSets[unitIndex] = null;
            _unitSourcePools[unitIndex] = null;
            _unitVisible[unitIndex] = false;
            _unitDying[unitIndex] = false;
        }

        private byte ComputeStatusMask(int unitIndex)
        {
            byte mask = 0;
            if (_sim.HasStatus(unitIndex, StatusEffectType.Stun))
            {
                mask |= StunMask;
            }
            if (_sim.HasStatus(unitIndex, StatusEffectType.Poison))
            {
                mask |= PoisonMask;
            }
            if (_sim.HasStatus(unitIndex, StatusEffectType.Burn))
            {
                mask |= BurnMask;
            }
            if (_sim.HasStatus(unitIndex, StatusEffectType.Mark))
            {
                mask |= MarkMask;
            }
            if (_sim.HasStatus(unitIndex, StatusEffectType.AttackUp))
            {
                mask |= AttackUpMask;
            }
            if (_sim.HasStatus(unitIndex, StatusEffectType.HealOverTime))
            {
                mask |= HealMask;
            }
            if (_sim.HasStatus(unitIndex, StatusEffectType.DamageResist))
            {
                mask |= ResistMask;
            }
            return mask;
        }

        /// <summary>
        /// 유닛 표시 갱신: 은신 = 반투명 재질 스왑 + 알파, 상태이상 = 롤 색에 틴트 혼합.
        /// 상태 변화가 있는 프레임에만 호출된다.
        /// </summary>
        private void ApplyUnitVisual(int unitIndex, bool stealthed, byte statusMask)
        {
            _unitStealthShown[unitIndex] = stealthed;
            _unitStatusShown[unitIndex] = statusMask;

            Color color = _unitColors[unitIndex];
            if ((statusMask & StunMask) != 0)
            {
                color = Color.Lerp(color, StunTint, StatusTintStrength);
            }
            if ((statusMask & PoisonMask) != 0)
            {
                color = Color.Lerp(color, PoisonTint, StatusTintStrength);
            }
            if ((statusMask & BurnMask) != 0)
            {
                color = Color.Lerp(color, BurnTint, StatusTintStrength);
            }
            if ((statusMask & MarkMask) != 0)
            {
                color = Color.Lerp(color, MarkTint, StatusTintStrength);
            }
            if ((statusMask & AttackUpMask) != 0)
            {
                color = Color.Lerp(color, AttackUpTint, StatusTintStrength);
            }
            if ((statusMask & HealMask) != 0)
            {
                color = Color.Lerp(color, HealTint, StatusTintStrength);
            }
            if ((statusMask & ResistMask) != 0)
            {
                color = Color.Lerp(color, ResistTint, StatusTintStrength);
            }
            color.a = stealthed ? StealthAlpha : 1f;

            // 모델 프리팹은 렌더러가 여러 개(몸체+모자+무기) — 전 슬롯을 은신/원본 배열로 스왑하고
            // 틴트를 각각 적용한다. 상태 변화 프레임에만 호출되므로 루프 비용은 무시 가능.
            UnitViewCache view = _unitViewSets[unitIndex];
            _propertyBlock.SetColor(BaseColorId, color);
            for (int r = 0; r < view.Renderers.Length; r++)
            {
                Renderer renderer = view.Renderers[r];
                renderer.sharedMaterials = stealthed ? view.StealthMaterials[r] : view.OriginalMaterials[r];
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>기본 재질의 투명(URP Lit Transparent) 변형을 런타임에 1개 생성 — 은신 유닛이 공유한다.</summary>
        private static Material CreateStealthMaterial(Material source)
        {
            var material = new Material(source);
            material.SetFloat(SurfaceId, 1f); // 1 = Transparent
            material.SetFloat(BlendId, 0f);   // 0 = Alpha
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat(ZWriteId, 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            return material;
        }

        private void SyncProjectileViews(float alpha)
        {
            int activeCount = _sim.ProjectileCount;

            for (int p = _projVisibleCount; p < activeCount; p++)
            {
                _projObjects[p] = _projectilePool.Get();
                _projTransforms[p] = _projObjects[p].transform;
            }
            for (int p = activeCount; p < _projVisibleCount; p++)
            {
                _projectilePool.Release(_projObjects[p]);
                _projObjects[p] = null;
                _projTransforms[p] = null;
            }
            _projVisibleCount = activeCount;

            for (int p = 0; p < activeCount; p++)
            {
                BattleSimulation.ProjectileState state = _sim.GetProjectileState(p, alpha);
                float t = state.Progress01;
                Vector3 start = SimViewMapper.ToWorld(state.LaunchPosition);
                Vector3 end = SimViewMapper.ToWorld(state.ImpactPosition);
                Vector3 position = Vector3.Lerp(start, end, t);
                // 포물선: 발사 높이에서 착탄점(바닥)으로 + 정점 높이 arcHeight의 아치.
                position.y = Mathf.Lerp(ProjectileLaunchHeight, 0f, t) + 4f * state.ArcHeight * t * (1f - t);
                _projTransforms[p].localPosition = position;
            }
        }

        private void SpawnArmyViews(List<BattleRequestBuilder.SquadAssets> squads, bool isTeamB, ref int unitIndex)
        {
            for (int s = 0; s < squads.Count; s++)
            {
                BattleRequestBuilder.SquadAssets squad = squads[s];
                Color color = squad.Role.RoleColor;
                if (isTeamB)
                {
                    // 같은 롤이 양 진영에 있을 때를 위한 팀 구분 톤 다운.
                    color = Color.Lerp(color, Color.black, 0.35f);
                }
                float scale = squad.Role.UnitRadius / 0.5f; // 뷰 프리팹 표준 크기(반경 0.5 = 키 1) 기준
                GameObjectPool pool = GetUnitPool(ViewPrefabOf(squad.Role));
                Quaternion facing = isTeamB ? TeamBFacing : TeamAFacing;

                for (int k = 0; k < squad.Count; k++)
                {
                    SpawnUnitView(unitIndex++, color, scale, pool, facing);
                }

                if (squad.General != null)
                {
                    // 장군 뷰: 크기 배율 + 금색 혼합 — 병사와 즉시 구분 (기획 §4). 모델은 병과와 공유.
                    // 시뮬의 분대 내 유닛 순서(병사 → 장군)와 일치해야 한다 (ArmyDefinition 계약).
                    Color generalColor = Color.Lerp(color, GeneralHighlight, 0.5f);
                    SpawnUnitView(unitIndex++, generalColor, squad.General.UnitRadius / 0.5f, pool, facing);
                }
            }
        }

        private void SpawnUnitView(int unitIndex, Color color, float scale, GameObjectPool pool, Quaternion facing)
        {
            GameObject unit = pool.Get();
            _unitObjects[unitIndex] = unit;
            _unitSourcePools[unitIndex] = pool;
            _unitTransforms[unitIndex] = unit.transform;
            _unitVisible[unitIndex] = true;

            // 초기화 시점 1회 조회 — Update에서는 캐시만 사용.
            _unitViewSets[unitIndex] = GetViewCache(unit);
            _unitColors[unitIndex] = color;
            // 재질·색을 함께 리셋 — 풀 재사용 시 이전 은신 재질/틴트가 남지 않도록 항상 호출.
            ApplyUnitVisual(unitIndex, _sim.IsStealthed(unitIndex), ComputeStatusMask(unitIndex));

            unit.transform.localScale = Vector3.one * scale;
            unit.transform.localRotation = facing;
            Vector3 spawnPosition = SimViewMapper.ToWorld(_sim.GetPosition(unitIndex));
            unit.transform.localPosition = spawnPosition;

            // 풀 재사용 대비 리셋 — 이전 수명의 사망 연출 상태가 남지 않도록.
            _unitPrevPositions[unitIndex] = spawnPosition;
            _unitDying[unitIndex] = false;
            Animator animator = _unitViewSets[unitIndex].Animator;
            if (animator != null)
            {
                animator.ResetTrigger(DieParamId);
                animator.ResetTrigger(AttackParamId);
                animator.ResetTrigger(CritParamId);
                animator.ResetTrigger(HitParamId);
                animator.SetFloat(SpeedParamId, 0f);
                animator.Play(IdleStateId, 0, 0f);
            }
        }

        /// <summary>인스턴스별 렌더러·머티리얼 캐시 조회 — 처음 보는 인스턴스만 1회 구축한다.</summary>
        private UnitViewCache GetViewCache(GameObject unit)
        {
            if (_viewCaches.TryGetValue(unit, out UnitViewCache cache))
            {
                return cache;
            }
            Renderer[] renderers = unit.GetComponentsInChildren<Renderer>(true);
            var originals = new Material[renderers.Length][];
            var stealths = new Material[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
            {
                originals[r] = renderers[r].sharedMaterials;
                stealths[r] = new Material[originals[r].Length];
                for (int m = 0; m < stealths[r].Length; m++)
                {
                    stealths[r][m] = _stealthMaterial;
                }
            }
            cache = new UnitViewCache
            {
                Renderers = renderers,
                OriginalMaterials = originals,
                StealthMaterials = stealths,
                Animator = unit.GetComponentInChildren<Animator>(true), // 리깅 없는 프리팹은 null
            };
            _viewCaches.Add(unit, cache);
            return cache;
        }

        private void ReleaseAllViews()
        {
            if (_unitObjects == null)
            {
                return;
            }
            for (int i = 0; i < _unitObjects.Length; i++)
            {
                if (_unitObjects[i] != null)
                {
                    ReleaseUnitView(i);
                }
            }
            for (int p = 0; p < _projVisibleCount; p++)
            {
                _projectilePool.Release(_projObjects[p]);
                _projObjects[p] = null;
                _projTransforms[p] = null;
            }
            _projVisibleCount = 0;
        }

        /// <summary>HUD 스킬 버튼에서 호출: 슬롯 무장/해제 토글. 시전 위치는 이후 전장 탭이 정한다.</summary>
        public void ToggleArmSkill(int slot)
        {
            if (_sim == null || slot < 0 || slot >= _skillDefinitions.Length)
            {
                return;
            }
            if (_armedSkillSlot == slot)
            {
                _armedSkillSlot = -1;
                return;
            }
            if (_sim.GetSkillCooldownRemaining(slot) > 0f)
            {
                return;
            }
            _armedSkillSlot = slot;
        }

        /// <summary>
        /// 무장 상태의 조준 표시(포인터 아래 스킬 반경 링)와 전장 탭 시전.
        /// 입력은 여기(Presentation)서 받아 TryCastSkill로 틱 정렬 명령만 주입한다 — 시뮬 결정론 유지.
        /// </summary>
        private void HandleSkillInput()
        {
            if (_armedSkillSlot < 0 || _sim.Finished)
            {
                if (_sim.Finished)
                {
                    _armedSkillSlot = -1;
                }
                if (_aimIndicator.gameObject.activeSelf)
                {
                    _aimIndicator.gameObject.SetActive(false);
                }
                return;
            }

            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }
            Vector2 screenPosition = pointer.position.ReadValue();
            if (!TryGetGroundPoint(screenPosition, out Vector3 groundPoint))
            {
                _aimIndicator.gameObject.SetActive(false);
                return;
            }

            SkillDefinition skill = _skillDefinitions[_armedSkillSlot];
            _aimIndicator.gameObject.SetActive(true);
            _aimIndicator.position = groundPoint + Vector3.up * AimDiscY;
            _aimIndicator.localScale = new Vector3(skill.Radius * 2f, DiscThickness, skill.Radius * 2f);
            Color aimColor = _skillColors[_armedSkillSlot];
            aimColor.a = AimAlpha;
            SetDiscColor(_aimRenderer, aimColor);

            if (pointer.press.wasPressedThisFrame && !IsPointerOverUi()
                && _sim.TryCastSkill(_armedSkillSlot, SimViewMapper.ToSim(groundPoint)))
            {
                SpawnCastFlash(_armedSkillSlot, groundPoint, skill.Radius);
                PlaySkillFx(_armedSkillSlot, groundPoint, skill);
                _armedSkillSlot = -1;
                _aimIndicator.gameObject.SetActive(false);
            }
        }

        /// <summary>화면 좌표 → 바닥 평면(y=0) 교점. 카메라가 지평선 위를 가리키면 실패.</summary>
        private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 groundPoint)
        {
            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            if (ray.direction.y > -1e-4f)
            {
                groundPoint = default;
                return false;
            }
            float t = -ray.origin.y / ray.direction.y;
            groundPoint = ray.origin + ray.direction * t;
            return true;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>조준/장판/플래시 디스크를 런타임 생성 — 프리팹·씬 오브젝트 추가 없이 뷰 전용.</summary>
        private void CreateSkillFxObjects()
        {
            _aimIndicator = CreateDisc("SkillAimIndicator", out _aimRenderer);

            _zoneDiscs = new Transform[config.MaxSkillZones];
            _zoneRenderers = new Renderer[config.MaxSkillZones];
            for (int z = 0; z < _zoneDiscs.Length; z++)
            {
                _zoneDiscs[z] = CreateDisc("SkillZoneDisc", out _zoneRenderers[z]);
            }

            _flashTransforms = new Transform[MaxFlashFx];
            _flashRenderers = new Renderer[MaxFlashFx];
            _flashRemainings = new float[MaxFlashFx];
            for (int f = 0; f < MaxFlashFx; f++)
            {
                _flashTransforms[f] = CreateDisc("SkillCastFlash", out _flashRenderers[f]);
            }
        }

        private void ResetSkillFxViews()
        {
            _aimIndicator.gameObject.SetActive(false);
            for (int z = 0; z < _zoneDiscs.Length; z++)
            {
                _zoneDiscs[z].gameObject.SetActive(false);
            }
            _zoneShownCount = 0;
            for (int f = 0; f < MaxFlashFx; f++)
            {
                _flashRemainings[f] = 0f;
                _flashTransforms[f].gameObject.SetActive(false);
            }
            if (_skillFxObjects != null)
            {
                for (int s = 0; s < _skillFxObjects.GetLength(0); s++)
                {
                    for (int r = 0; r < SkillFxPerSlot; r++)
                    {
                        _skillFxRemainings[s, r] = 0f;
                        if (_skillFxObjects[s, r] != null)
                        {
                            _skillFxObjects[s, r].SetActive(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 로드아웃 변경 시 시전 이펙트 링 풀 재구축 (전투 시작 경로 — Instantiate/Destroy 허용).
        /// 프리팹이 없는 스킬은 슬롯을 비워두고 기존 디스크 표시만 쓴다.
        /// </summary>
        private void RebuildSkillFxPools(SkillData[] skills, int skillCount)
        {
            if (_skillFxObjects != null)
            {
                for (int s = 0; s < _skillFxObjects.GetLength(0); s++)
                {
                    for (int r = 0; r < SkillFxPerSlot; r++)
                    {
                        if (_skillFxObjects[s, r] != null)
                        {
                            Destroy(_skillFxObjects[s, r]);
                        }
                    }
                }
            }
            _skillFxObjects = new GameObject[skillCount, SkillFxPerSlot];
            _skillFxParticles = new ParticleSystem[skillCount, SkillFxPerSlot];
            _skillFxRemainings = new float[skillCount, SkillFxPerSlot];
            _skillFxStopping = new bool[skillCount, SkillFxPerSlot];
            _skillFxNext = new int[skillCount];
            for (int s = 0; s < skillCount; s++)
            {
                GameObject prefab = skills[s].CastEffectPrefab;
                if (prefab == null)
                {
                    continue;
                }
                // 스킬 반경에 맞춰 스케일 — 프리팹의 기본 반경은 에셋이 알고 있다.
                float scale = _skillDefinitions[s].Radius / Mathf.Max(skills[s].CastEffectBaseRadius, 0.01f);
                for (int r = 0; r < SkillFxPerSlot; r++)
                {
                    GameObject fx = Instantiate(prefab, transform);
                    fx.name = $"SkillFx_{skills[s].name}_{r}";
                    fx.transform.localScale = Vector3.one * scale;
                    fx.SetActive(false);
                    _skillFxObjects[s, r] = fx;
                    _skillFxParticles[s, r] = fx.GetComponentInChildren<ParticleSystem>(true);
                }
            }
        }

        /// <summary>시전 순간 이펙트 재생 — 장판 스킬은 장판 지속시간, 즉발은 고정 시간.</summary>
        private void PlaySkillFx(int slot, Vector3 groundPoint, in SkillDefinition skill)
        {
            if (_skillFxObjects == null || slot >= _skillFxObjects.GetLength(0) || _skillFxObjects[slot, 0] == null)
            {
                return;
            }
            int r = _skillFxNext[slot];
            _skillFxNext[slot] = (r + 1) % SkillFxPerSlot;
            GameObject fx = _skillFxObjects[slot, r];
            fx.transform.position = groundPoint + new Vector3(0f, 0.05f, 0f);
            fx.SetActive(true);
            ParticleSystem particle = _skillFxParticles[slot, r];
            if (particle != null)
            {
                particle.Clear(true);
                particle.Play(true);
            }
            _skillFxRemainings[slot, r] = skill.ZoneDuration > 0f ? skill.ZoneDuration : InstantFxSeconds;
            _skillFxStopping[slot, r] = false;
        }

        /// <summary>재생 시간 만료 → 방출 정지 → 잔향 소멸 후 비활성 (매 프레임, 무할당).</summary>
        private void UpdateSkillFx(float deltaTime)
        {
            if (_skillFxObjects == null)
            {
                return;
            }
            for (int s = 0; s < _skillFxObjects.GetLength(0); s++)
            {
                for (int r = 0; r < SkillFxPerSlot; r++)
                {
                    if (_skillFxRemainings[s, r] <= 0f || _skillFxObjects[s, r] == null)
                    {
                        continue;
                    }
                    _skillFxRemainings[s, r] -= deltaTime;
                    if (_skillFxRemainings[s, r] > 0f)
                    {
                        continue;
                    }
                    if (!_skillFxStopping[s, r])
                    {
                        _skillFxStopping[s, r] = true;
                        _skillFxRemainings[s, r] = FxFadeTailSeconds;
                        if (_skillFxParticles[s, r] != null)
                        {
                            _skillFxParticles[s, r].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        }
                    }
                    else
                    {
                        _skillFxRemainings[s, r] = 0f;
                        _skillFxObjects[s, r].SetActive(false);
                    }
                }
            }
        }

        /// <summary>시뮬 장판 상태를 디스크로 동기화. 마지막 1초 구간은 알파 페이드.</summary>
        private void SyncZoneViews()
        {
            int activeCount = _sim.ZoneCount;
            for (int z = 0; z < activeCount; z++)
            {
                BattleSimulation.SkillZoneState state = _sim.GetZoneState(z);
                Transform disc = _zoneDiscs[z];
                if (!disc.gameObject.activeSelf)
                {
                    disc.gameObject.SetActive(true);
                }
                disc.position = SimViewMapper.ToWorld(state.Position) + Vector3.up * ZoneDiscY;
                disc.localScale = new Vector3(state.Radius * 2f, DiscThickness, state.Radius * 2f);

                Color color = _skillColors[state.SkillSlot];
                color.a = ZoneAlpha * Mathf.Clamp01(state.RemainingSeconds / ZoneFadeSeconds);
                SetDiscColor(_zoneRenderers[z], color);
            }
            for (int z = activeCount; z < _zoneShownCount; z++)
            {
                _zoneDiscs[z].gameObject.SetActive(false);
            }
            _zoneShownCount = activeCount;
        }

        /// <summary>시전 피드백 플래시 — 짧게 밝아졌다 사라지는 디스크 (뷰 전용, 순환 사용).</summary>
        private void SpawnCastFlash(int slot, Vector3 groundPoint, float radius)
        {
            int f = _nextFlashIndex;
            _nextFlashIndex = (_nextFlashIndex + 1) % MaxFlashFx;
            _flashRemainings[f] = FlashDuration;
            Transform flash = _flashTransforms[f];
            flash.gameObject.SetActive(true);
            flash.position = groundPoint + Vector3.up * (ZoneDiscY + 0.02f);
            flash.localScale = new Vector3(radius * 2f, DiscThickness, radius * 2f);
            Color color = _skillColors[slot];
            color.a = FlashAlpha;
            SetDiscColor(_flashRenderers[f], color);
        }

        private void UpdateFlashFx(float deltaTime)
        {
            for (int f = 0; f < MaxFlashFx; f++)
            {
                if (_flashRemainings[f] <= 0f)
                {
                    continue;
                }
                _flashRemainings[f] -= deltaTime;
                if (_flashRemainings[f] <= 0f)
                {
                    _flashTransforms[f].gameObject.SetActive(false);
                    continue;
                }
                // 색은 시전 시점에 설정됨 — 여기서는 알파만 감쇠
                _flashRenderers[f].GetPropertyBlock(_propertyBlock);
                Color color = _propertyBlock.GetColor(BaseColorId);
                color.a = FlashAlpha * (_flashRemainings[f] / FlashDuration);
                _propertyBlock.SetColor(BaseColorId, color);
                _flashRenderers[f].SetPropertyBlock(_propertyBlock);
            }
        }

        private Transform CreateDisc(string discName, out Renderer renderer)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = discName;
            Destroy(disc.GetComponent<Collider>());
            renderer = disc.GetComponent<Renderer>();
            renderer.sharedMaterial = _stealthMaterial; // 공유 투명 재질 + 프로퍼티 블록 색
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disc.transform.SetParent(transform, false);
            disc.SetActive(false);
            return disc.transform;
        }

        private void SetDiscColor(Renderer renderer, Color color)
        {
            _propertyBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnDestroy()
        {
            if (_stealthMaterial != null)
            {
                Destroy(_stealthMaterial);
            }
        }

        private ArmyDefinition BuildArmy(SquadSetup[] setups)
        {
            var squads = new SquadDefinition[setups.Length];
            for (int s = 0; s < setups.Length; s++)
            {
                squads[s] = new SquadDefinition(
                    setups[s].role.ToDefinition(),
                    setups[s].count,
                    new System.Numerics.Vector2(setups[s].anchor.x, setups[s].anchor.y),
                    setups[s].general != null ? setups[s].general.ToDefinition() : null);
            }
            return new ArmyDefinition(squads);
        }

    }
}
