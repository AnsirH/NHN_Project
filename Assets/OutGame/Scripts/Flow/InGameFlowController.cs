using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;
using OutGame.Logic.Characters;
using OutGame.Logic.Events;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace OutGame.Flow
{
    /// <summary>
    /// 방 그래프 진행 루프 (§3.1: OutGame.unity 안의 한 패널 — "InGame"이라는 이름과 달리 실제
    /// 전투 씬이 아니라 아웃게임 콘텐츠다): 방 그래프 표시 → 노드 선택 → 방문 확정 → 방 타입별
    /// 패널 → 복귀. 전투/보스 방은 ArmyDeploymentPanel → BattleBridge(§7.4, 실제 전투 씬으로 전환,
    /// 2026-07-29 실전 연동 완료) → 결과 처리로 이어진다. 리소스 로딩/이벤트 구독은 Awake()에서
    /// 한 번, 런이 준비된 뒤에만 가능한 활성화는 <see cref="Begin"/>에서 처리한다(OutGameFlowController가
    /// 캐릭터 선택 확정 직후, 또는 이어하기로 곧장 호출).
    /// </summary>
    public class InGameFlowController : MonoBehaviour
    {
        [SerializeField] private RoomGraphPanel mapPanel;
        // 2026-08-08: DummyRoomPanel/RestPanel → RunResultPanel/ReinforcementPanel로 개명
        // (Docs/OutGame/화면 명칭 정리.md) — 기존 씬에 이미 배선된 참조를 잃지 않도록
        // FormerlySerializedAs로 이전 필드명을 남겨둔다.
        [FormerlySerializedAs("roomPanel")] [SerializeField] private RunResultPanel runResultPanel; // 런 종료(클리어/패배) 화면
        [SerializeField] private EventPanel eventPanel;
        [FormerlySerializedAs("restPanel")] [SerializeField] private ReinforcementPanel reinforcementPanel;
        [SerializeField] private AugmentPanel augmentPanel;
        [SerializeField] private ArmyDeploymentPanel deploymentPanel;
        [SerializeField] private ArmyFormationPopup armyFormationPopup; // 2026-07-26: 방 그래프의 "진영" 팝업
        [SerializeField] private ItemRewardPopup itemRewardPopup; // 2026-07-26: 전투 승리 아이템 드롭 알림
        // 2026-08-10: 방 그래프 상단 바 "환경설정" 버튼이 여는 대상. 씬 오브젝트라 RoomGraphPanel
        // 프리팹이 직접 참조할 수 없어 여기서 받는다(진영 팝업과 동일한 구조).
        [SerializeField] private PausePopup pausePopup;
        [SerializeField] private RoomTypeVisualSet visuals;
        [SerializeField] private RunConfigAsset runConfig;
        [SerializeField] private EnemyCompositionConfigAsset enemyCompositionConfigAsset; // §4-28
        [SerializeField] private ItemDropConfigAsset itemDropConfigAsset; // §4-28

        /// <summary>테스트/툴링에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private RunState run;
        private System.Random rng;
        private List<EventData> eventDataPool;
        private Dictionary<string, EventDefinition> eventDefsById;
        private Dictionary<string, ArmyDefinition> armyDefsById;
        private Dictionary<string, ArmyData> armyDataById;
        private Dictionary<string, ItemDefinition> itemDefsById;
        private Dictionary<string, AugmentDefinition> augmentDefsById;
        private Dictionary<string, AugmentData> augmentDataById;
        private Dictionary<string, PlayerCharacterDefinition> characterDefsById; // §5.2.5
        private Dictionary<ArmyClass, string> itemIdByClass; // §4-28: 병과→아이템 매핑, Awake()에서 한 번만 계산
        private Dictionary<string, EnemyPresetData> presetsById; // 2026-08-02: 적 프리셋 조각 풀
        private string savePath;
        private bool runEnded;
        private RoomType currentBattleRoomType;
        private List<EnemyArmy> currentEnemyComposition;

        // 씬 교체 전투(§7.4)의 복귀 컨텍스트 — 이 컨트롤러는 전투 씬 로드와 함께 파괴되므로,
        // OnBattleResult가 쓰는 인스턴스 필드(방 종류·적 구성)를 씬 전환을 살아남는 정적으로 따로
        // 보관했다가 복귀한 새 인스턴스의 Begin()이 복원한다. 런 자체는 RunSessionContext(이어하기와
        // 같은 경로)로, 결과는 BattleBridge.SetPendingResult(인게임이 채움)로 나른다.
        private static RoomType pendingReturnRoomType;
        private static List<EnemyArmy> pendingReturnEnemies;

        // §4-28: 아웃게임이 확정하는 적 구성 밸런스(§9 RoomEncounterTable 역할) — 아이템 드롭 계산에
        // 쓰이는 동시에 2026-07-29부터 BattleSetupData.enemies로 §7 계약에도 그대로 실린다.
        // 위 Asset 필드에서 Awake()에 채워진다.
        private EnemyCompositionConfig enemyCompositionConfig;
        private ItemDropConfig itemDropConfig;

        private void Awake()
        {
            LoadResourcePools();
            WireRoomEvents();
            runResultPanel.Hide();
        }

        private void LoadResourcePools()
        {
            if (visuals == null)
                visuals = Resources.Load<RoomTypeVisualSet>(ResourcePaths.RoomTypeVisuals); // 배선 누락 대비 폴백
            if (runConfig == null)
                runConfig = Resources.Load<RunConfigAsset>(ResourcePaths.RunConfigDefault);
            if (enemyCompositionConfigAsset == null)
                enemyCompositionConfigAsset = Resources.Load<EnemyCompositionConfigAsset>(ResourcePaths.EnemyCompositionConfigDefault);
            if (itemDropConfigAsset == null)
                itemDropConfigAsset = Resources.Load<ItemDropConfigAsset>(ResourcePaths.ItemDropConfigDefault);

            if (mapPanel == null || runResultPanel == null || eventPanel == null || reinforcementPanel == null
                || augmentPanel == null || deploymentPanel == null || armyFormationPopup == null
                || itemRewardPopup == null
                || visuals == null || runConfig == null
                || enemyCompositionConfigAsset == null || itemDropConfigAsset == null)
                throw new InvalidOperationException(
                    "InGameFlowController의 필수 참조가 배선되지 않았습니다 — 씬 구성(SceneSetupOutGame) 확인");

            // ToConfig()가 Validate()를 포함하므로(다른 config 에셋과 동일 패턴) 잘못된 인스펙터
            // 값은 여기서 바로 예외로 드러난다 — 조용히 잘못된 값으로 동작하지 않는다.
            enemyCompositionConfig = enemyCompositionConfigAsset.ToConfig();
            itemDropConfig = itemDropConfigAsset.ToConfig();

            List<EventDefinition> eventPool = ResourcePool.LoadAllOrThrow<EventDefinition>(
                ResourcePaths.Events, "이벤트 정의를 찾을 수 없습니다 — SceneSetupM4Data.Run() 실행 필요");
            eventDataPool = eventPool.Select(e => e.ToData()).ToList(); // 한 번만 변환해 캐시 (매 방문마다 재파싱 방지)
            eventDefsById = eventPool.ToDictionary(e => e.ToData().id);

            List<AugmentDefinition> augmentPool = ResourcePool.LoadAllOrThrow<AugmentDefinition>(
                ResourcePaths.Augments, "증강 정의를 찾을 수 없습니다 — SceneSetupM4Data.Run() 실행 필요 (§4-27)");
            augmentDefsById = augmentPool.ToDictionary(a => a.ToData().id);

            List<PlayerCharacterDefinition> characterPool = ResourcePool.LoadAllOrThrow<PlayerCharacterDefinition>(
                ResourcePaths.Characters, "플레이어 캐릭터 정의를 찾을 수 없습니다 — SceneSetupM7Data.Run() 실행 필요 (§5.2.5)");
            characterDefsById = characterPool.ToDictionary(c => c.ToData().id);

            armyDefsById = Resources.LoadAll<ArmyDefinition>(ResourcePaths.Data)
                .ToDictionary(a => a.ToData().id);
            armyDataById = armyDefsById.ToDictionary(kv => kv.Key, kv => kv.Value.ToData());
            itemDefsById = Resources.LoadAll<ItemDefinition>(ResourcePaths.Data)
                .ToDictionary(i => i.ToData().id);
            // §4-28: 병과→아이템 매핑은 런 도중 안 바뀌므로 승리마다 다시 만들지 않고 한 번만 캐시.
            itemIdByClass = ItemEquipService.ResolveItemIdByClass(itemDefsById.Values.Select(d => d.ToData()));
            augmentDataById = augmentDefsById.ToDictionary(kv => kv.Key, kv => kv.Value.ToData());

            // 2026-08-02: 적 프리셋 조각 풀 — EnemyPresetEditorWindow로 만든 에셋을 전부 로드.
            List<EnemyPresetDefinition> presetPool = ResourcePool.LoadAllOrThrow<EnemyPresetDefinition>(
                ResourcePaths.EnemyPresets, "적 프리셋 정의를 찾을 수 없습니다 — 최소 1개 이상 만들어야 합니다 (EnemyPresetEditorWindow)");
            presetsById = presetPool.ToDictionary(p => p.ToData().presetId, p => p.ToData());

            savePath = RunSaveService.DefaultPath;
        }

        private void WireRoomEvents()
        {
            mapPanel.RoomSelected += OnRoomSelected;
            mapPanel.FormationRequested += OnFormationRequested;
            mapPanel.SettingsRequested += OnSettingsRequested;
            runResultPanel.Completed += OnRoomCompleted;
            eventPanel.Completed += OnRoomCompleted;
            reinforcementPanel.Completed += OnRoomCompleted;
            augmentPanel.Completed += OnRoomCompleted;
            deploymentPanel.Confirmed += OnBattleSetupConfirmed;
            armyFormationPopup.Changed += OnArmyFormationChanged;
        }

        /// <summary>OutGameFlowController가 이 패널을 활성화한 직후 호출 — 확정된 런으로 방 그래프를
        /// 시작한다(캐릭터 선택 확정 직후, 또는 "이어하기"로 곧장). Awake()의 리소스 로딩과 달리 매번
        /// 새 런에 대해 다시 실행된다.</summary>
        public void Begin(RunState run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            rng = new System.Random(Environment.TickCount);

            // BattleBridge.Implementation은 이 패널이 활성화될 때마다 재등록해야 한다 — Domain Reload가
            // 꺼져 있어도 파괴된 오브젝트의 클로저를 가리키지 않도록 (BattleBridge.cs 참조).
            //
            // 인게임 전투 씬 연결(§7.4 핸드오프, 2026-07-29 실전 연동 완료): setup/콜백을 static
            // 홀더에 보관하고 전투 씬으로 전환한다 — 더미 패널(DummyBattlePanel)은 전투 씬이 없던
            // 시절의 임시 구현이라 더 이상 쓰지 않는다.
            BattleBridge.Implementation = (setup, onResult) =>
            {
                // 씬 교체를 살아남을 복귀 컨텍스트(2026-07-30): 배치 화면에서 쓴 골드·방문 표시까지
                // 담긴 현재 런과, 결과 처리에 필요한 방 종류·적 구성을 보관한다 — 복귀 시 Begin이 소비.
                RunSessionContext.SetPendingRun(run);
                pendingReturnRoomType = setup.roomType;
                pendingReturnEnemies = setup.enemies;
                BattleBridge.SetPendingBattle(setup, onResult);
                // 로딩 씬을 한 단계 거쳐 Battle로 전환한다(2026-08-04) — Loading 씬이 LoadingHandoff에서
                // 대상 씬 이름을 꺼내 비동기로 이어받는다.
                LoadingHandoff.SetTarget(SceneNames.Battle);
                LoadSceneAction(SceneNames.Loading);
            };

            mapPanel.Open(run.mapState);
            mapPanel.SetGold(run.gold); // 2026-07-26: 방 그래프 우측 상단 재화 표시
            // 2026-08-10: 상단 바에 선택된 캐릭터 아이콘. id가 풀에 없으면 아이콘만 비운다 — 선택
            // 캐릭터의 실제 유효성은 DeploymentState가 이미 명확한 예외로 검증하므로, 여기서 런을
            // 중단시키면 진짜 원인보다 먼저 터져 헷갈린다.
            PlayerCharacterDefinition selectedCharacter;
            mapPanel.SetPlayerIcon(
                characterDefsById.TryGetValue(run.selectedCharacterId ?? string.Empty, out selectedCharacter)
                    ? selectedCharacter.Icon
                    : null);

            // 씬 교체 전투에서 복귀한 경우(§7.4): 파괴 전 보관해둔 컨텍스트를 복원하고 기존 결과 경로를
            // 그대로 태운다 — 패배: 세이브 삭제+패배 화면→메인 메뉴, 승리: 보상 팝업→맵 갱신+저장.
            BattleResultData returnedResult = BattleBridge.ConsumePendingResult();
            if (returnedResult != null)
            {
                currentBattleRoomType = pendingReturnRoomType;
                currentEnemyComposition = pendingReturnEnemies;
                pendingReturnEnemies = null;
                OnBattleResult(returnedResult);
            }
        }

        private void OnDestroy()
        {
            if (mapPanel != null) mapPanel.RoomSelected -= OnRoomSelected;
            if (mapPanel != null) mapPanel.FormationRequested -= OnFormationRequested;
            if (mapPanel != null) mapPanel.SettingsRequested -= OnSettingsRequested;
            if (runResultPanel != null) runResultPanel.Completed -= OnRoomCompleted;
            if (eventPanel != null) eventPanel.Completed -= OnRoomCompleted;
            if (reinforcementPanel != null) reinforcementPanel.Completed -= OnRoomCompleted;
            if (augmentPanel != null) augmentPanel.Completed -= OnRoomCompleted;
            if (deploymentPanel != null) deploymentPanel.Confirmed -= OnBattleSetupConfirmed;
            if (armyFormationPopup != null) armyFormationPopup.Changed -= OnArmyFormationChanged;
        }

        private void OnFormationRequested()
        {
            armyFormationPopup.Open(run, runConfig.ToData(),
                armyDefsById.Values.ToList(), itemDefsById.Values.ToList(), augmentDefsById.Values.ToList());
        }

        /// <summary>방 그래프 상단 바의 "환경설정" (2026-08-10) — 일시정지 팝업을 연다(사용자 확정).
        /// 그 안에 환경설정과 메인메뉴 복귀가 모두 있고, 모바일에는 ESC 키가 없어 PausePopup에 닿을
        /// 화면상 진입점이 필요했다. Toggle이 아니라 Show인 이유: 팝업이 열리면 dim(Canvas order 5)이
        /// 상단 바(order 2)를 덮어 이 버튼을 다시 누를 수 없다.</summary>
        private void OnSettingsRequested() => pausePopup.Show();

        /// <summary>진영 팝업 안에서 업그레이드로 골드를 쓰면 방 그래프 재화 표시도 즉시 최신 상태로
        /// 유지한다(2026-07-26 사용자 확정) — 팝업이 전체화면 dim이라 그 표시가 가려져 있는 동안에도
        /// 미리 동기화해둬야 팝업을 닫는 순간 바로 정확한 값이 보인다.</summary>
        private void OnArmyFormationChanged() => mapPanel.SetGold(run.gold);

        private void OnRoomSelected(MapNode node)
        {
            // 이 패널의 유일한 실제 진입 경로(OutGameFlowController)는 활성화 직후 반드시 Begin()을
            // 호출하지만, mapPanel.RoomSelected 자체는 그 보장에 기대지 않고 fail-fast로 확인한다
            // (CharacterSelectPanel.OnConfirmClicked와 동일한 방어 — 코드 리뷰 HIGH 수정).
            if (run == null)
                throw new InvalidOperationException("InGameFlowController.Begin(RunState)이 호출되지 않았습니다.");

            // 진영 팝업을 열어둔 채로 다른 방을 선택했을 가능성에 대비 — 다음 방 패널 위에 잔존해서
            // 보이지 않도록 방어적으로 닫는다(2026-07-26, 배치 패널의 팝업 잔존 방지와 동일한 이유).
            armyFormationPopup.Hide();

            MapProgress.Visit(run.mapState, node.point);
            mapPanel.Refresh();

            // 2026-08-10: 이벤트/증원/증강 방은 dim 배경 뒤로 맵이 비치도록 열어둔다(사용자 확정).
            // 원래는 세 방 모두 닫았다 — 65% 반투명 dim 뒤로 그래프가 겹쳐 보이던 문제(2026-08-04
            // 사용자 리포트) 때문이었는데, 그 원인은 "맵이 열려 있어서"가 아니라 NodeLayer가
            // Canvas(overrideSorting order 1)라 하이어라키와 무관한 전역 버킷으로 비교되어 노드가
            // 패널 위로 올라온 것이었다. 각 방 패널에 Canvas(order 3)를 줘서 노드(1)·상단 바(2)보다
            // 위, 팝업(5)보다는 아래에 오게 해결했다.
            //
            // 전투 방은 씬 자체가 교체되므로 여기서 닫아둔다 — 복귀 시 Begin()이 다시 연다.
            bool keepMapVisible = node.roomType == RoomType.Event
                                  || node.roomType == RoomType.Reinforcement
                                  || node.roomType == RoomType.Augment;
            if (!keepMapVisible) mapPanel.Close();

            // 주의: MapProgress.HasVisitedBoss는 보스 "방문" 여부이지 "승리" 여부가 아니다.
            // 런 클리어는 보스 전투에서 승리했을 때만 성립하므로(OnBattleResult), 여기서 미리 판정하지 않는다.

            // 저장은 방 결과(보상 적용 등)까지 반영된 뒤 OnRoomCompleted에서 한 번만 수행한다.
            // 여기서 먼저 저장하면 "방문함"만 기록되고 보상은 누락된 상태로 저장될 위험이 있다.
            //
            // 증원/증강/이벤트 방은 난이도 커브 기준점(powerRoomsVisited, §4-28 재설계)을 올린다 —
            // 전투방 승리 드롭은 확률적이라 이 카운터에는 포함하지 않는다. Open 호출이 끝난 뒤에
            // 증가시켜서, 혹시 Open 쪽에서 예외가 나면(예: 정의되지 않은 id 조회) 카운터만 먼저
            // 올라가고 방은 실제로 안 열리는 불일치가 생기지 않게 한다.
            switch (node.roomType)
            {
                case RoomType.Event:
                    OpenEventRoom();
                    run.powerRoomsVisited++;
                    break;
                case RoomType.Reinforcement:
                    reinforcementPanel.Open(run, runConfig.ToData(),
                        armyDefsById.Values.ToList(), itemDefsById.Values.ToList(), augmentDefsById.Values.ToList());
                    run.powerRoomsVisited++;
                    break;
                case RoomType.Augment:
                    OpenAugmentRoom();
                    run.powerRoomsVisited++;
                    break;
                default:
                    OpenBattleRoom(node);
                    break;
            }
        }

        private void OpenEventRoom()
        {
            EventData selected = EventSelector.SelectRandom(eventDataPool, run.visitedEventIds, rng);
            if (!run.visitedEventIds.Contains(selected.id))
                run.visitedEventIds.Add(selected.id); // 소진 후 재추첨된 반복 항목은 중복 기록하지 않음

            eventPanel.Open(eventDefsById[selected.id], run, runConfig.ToData().maxArmyCount);
        }

        private void OpenAugmentRoom()
        {
            augmentPanel.Open(run, augmentDefsById.Values.ToList(), rng);
        }

        private void OpenBattleRoom(MapNode node)
        {
            // 참고용 식별자(§9 RoomEncounterTable) — 적 구성이 정의되면 노드별 실제 값으로 대체.
            // 실제 적 구성은 encounterId가 아니라 아래에서 생성해 BattleSetupData.enemies로 직접 전달한다.
            string encounterId = $"enc_{node.roomType}";
            // §4-28: 적 구성을 미리 생성해둔다 — 아이템 드롭·전투력 계산에 쓰이는 동시에, 배치 확정
            // (BuildSetup) 시점에 이 값 그대로가 BattleSetupData.enemies로 실려 나간다(2026-07-29).
            // 2026-08-02: 개발자가 만들어둔 프리셋 조각(presetsById)을 등급 가중치로 조합한다 —
            // 최종 스탯은 아군과 동일한 ArmyStatCalculator 공식으로 계산되므로 armyDataById/
            // augmentDataById가 필요하다.
            // 2026-07-26: 난이도 기준을 "층수(node.point.y)"에서 run.powerRoomsVisited(증원·증강·
            // 이벤트 방 통과 횟수)로 교체 — 전투방만 연달아 나오는 런에서 플레이어 보강 없이 적만
            // 계속 세지는 불균형을 막기 위함(§4-28 재설계).
            currentEnemyComposition = EnemyCompositionGenerator.Generate(
                run.powerRoomsVisited, node.roomType, enemyCompositionConfig, presetsById,
                armyDataById, augmentDataById, rng);
            deploymentPanel.Open(run, node.id, node.roomType, encounterId,
                armyDefsById.Values.ToList(), itemDefsById.Values.ToList(), runConfig.ToData(),
                augmentDefsById.Values.ToList(), characterDefsById.Values.ToList(), currentEnemyComposition);
        }

        private void OnBattleSetupConfirmed(BattleSetupData setup)
        {
            currentBattleRoomType = setup.roomType;
            // 배치 패널을 여기서 미리 닫지 않는다(2026-08-04, 사용자 리포트) — Close() 직후 씬 전환이
            // 일어나기까지 한 프레임 정도 패널이 사라진 빈 OutGame 화면(스카이박스)이 비쳐 보였다.
            // 곧바로 Loading 씬으로 전환되며 이 씬 자체가 파괴되므로 따로 닫아둘 필요가 없다.
            BattleBridge.StartBattle(setup, OnBattleResult);
        }

        private void OnBattleResult(BattleResultData result)
        {
            // additive 경로(아웃게임 씬 생존)로 결과가 직접 오면 씬 교체 대비로 보관해둔 런이 소비되지
            // 않고 남는다 — 다음 OutGame 씬 진입이 그 잔존 런으로 방 그래프에 직행하지 않도록 비운다.
            // (씬 교체 경로에서는 복귀 시 OutGameFlowController.Start가 이미 소비해 null이라 무해하다.)
            RunSessionContext.ConsumePendingRun();

            // 씬 교체 복귀 경로(Begin())는 이 시점에 mapPanel을 이미 다시 열어둔 상태다.
            //
            // 2026-08-10: 예전엔 여기서 무조건 mapPanel.Close()를 했다 — dim 배경 뒤로 노드가 겹쳐
            // 보인다는 리포트(2026-08-04) 때문이었는데, 그 원인은 "맵이 열려 있어서"가 아니라
            // NodeLayer가 Canvas(order 1)라 하이어라키와 무관한 전역 버킷으로 비교되어 노드가 팝업
            // 위로 올라온 것이었다(OnRoomSelected 주석의 이벤트/증원/증강 방과 동일한 원인).
            // ItemRewardPopup에 Canvas(order 5)를 줘서 해결했으므로 이제 방 그래프를 열어둔 채로
            // 팝업만 위에 띄운다(사용자 요청). 런 종료 화면(패배/클리어)은 방 그래프를 남겨둘 이유가
            // 없으므로 각 분기에서 닫는다.

            if (!result.victory)
            {
                mapPanel.Close();
                runEnded = true;
                RunSaveService.DeleteSave(savePath); // 패배 — 런 종료 (§4-14)
                runResultPanel.ShowDefeat();
                return;
            }

            BattleRewardApplier.ApplyVictoryReward(run, runConfig.ToData().battleVictoryGold); // §4-20/§9: 초안값

            // §4-28: 격파한 적 병과 구성에 따라 확률적으로 아이템 드롭.
            List<ArmyClass> defeatedClasses = currentEnemyComposition.Select(e => e.armyClass).ToList();
            List<string> drops = ItemDropCalculator.RollDrops(defeatedClasses, itemDropConfig, itemIdByClass, rng);
            BattleRewardApplier.ApplyItemDrops(run, drops);
            currentEnemyComposition = null;

            bool bossVictory = currentBattleRoomType == RoomType.Boss;

            // 2026-07-26 사용자 요청: 드롭된 아이템을 조용히 넣기만 하지 않고 알림 팝업으로 보여준다.
            // 보스도 아이템을 드롭할 수 있으므로 팝업 로직은 일반전투/보스 공통 경로로 처리하고,
            // 팝업을 닫아야 그 다음(방 복귀 또는 런 클리어 화면)으로 진행되게 한다.
            if (drops.Count > 0)
            {
                void OnRewardPopupClosed()
                {
                    itemRewardPopup.Closed -= OnRewardPopupClosed;
                    ProceedAfterBattle(bossVictory);
                }

                // 팝업 뒤로 보이는 상단 바가 전투 보상 반영 전 골드를 들고 있으면 어색하다 —
                // ApplyVictoryReward가 이미 적용된 값으로 맞춰둔다(노드 상태는 방 완료 처리 전이라
                // 여기서 Refresh하지 않는다 — OnRoomCompleted가 담당).
                mapPanel.SetGold(run.gold);

                itemRewardPopup.Closed += OnRewardPopupClosed;
                itemRewardPopup.Open(drops, itemDefsById);
                return;
            }

            ProceedAfterBattle(bossVictory);
        }

        private void ProceedAfterBattle(bool bossVictory)
        {
            if (bossVictory)
            {
                mapPanel.Close(); // 런 클리어 화면 뒤로 방 그래프가 비쳐 보일 이유가 없다
                // 보스 "방문"이 아니라 "승리"가 런 클리어 조건이다 (MapProgress.HasVisitedBoss와 혼동 주의).
                runEnded = true;
                RunSaveService.DeleteSave(savePath); // 런 종료 — 이어하기 대상에서 제외 (§5.1)
                runResultPanel.ShowRunClear();
                return;
            }

            OnRoomCompleted();
        }

        private void OnRoomCompleted()
        {
            if (runEnded)
            {
                LoadSceneAction(SceneNames.MainMenu); // 런 종료 화면의 [완료] → 메인 메뉴 복귀
                return;
            }

            mapPanel.Show(); // OnRoomSelected/OnBattleResult에서 닫아둔 방 그래프를 다시 보여준다
            mapPanel.Refresh();
            mapPanel.SetGold(run.gold); // 이벤트/전투 보상으로 바뀐 골드를 방 그래프 복귀 시 반영
            // 방을 하나 끝냈으니 다음 선택지가 보이는 위치로 옮겨준다(2026-08-10 사용자 요청) —
            // 맵을 열어둔 채 진행하는 이벤트/증원/증강 방은 Rebuild가 돌지 않아 여기서 처리해야 한다.
            mapPanel.FocusOnNext();
            SaveProgress();
        }

        /// <summary>
        /// 방 진행 상황을 즉시 저장한다. 클라우드 동기화 잠금 등으로 인한 일시적 저장 실패가
        /// 게임 진행 자체를 막지 않도록 예외를 흡수하고 경고만 남긴다.
        /// </summary>
        private void SaveProgress()
        {
            try
            {
                RunSaveService.Save(run, savePath);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[InGameFlowController] 진행 저장 실패 — {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[InGameFlowController] 진행 저장 실패 — {e.Message}");
            }
        }
    }
}
