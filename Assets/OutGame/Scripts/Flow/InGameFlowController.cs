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

namespace OutGame.Flow
{
    /// <summary>
    /// 인게임 방 진행 루프: 맵 생성 → 방 그래프 표시 → 노드 선택 → 방문 확정 → 방 타입별 패널 → 복귀.
    /// 전투/보스 방은 ArmyDeploymentPanel → BattleBridge(M6부터 더미 구현) → 결과 처리로 이어진다.
    /// </summary>
    public class InGameFlowController : MonoBehaviour
    {
        [SerializeField] private RoomMapPanel mapPanel;
        [SerializeField] private DummyRoomPanel roomPanel; // 런 종료(클리어/패배) 화면
        [SerializeField] private EventPanel eventPanel;
        [SerializeField] private RestPanel restPanel;
        [SerializeField] private AugmentPanel augmentPanel;
        [SerializeField] private ArmyDeploymentPanel deploymentPanel;
        [SerializeField] private ArmyFormationPopup armyFormationPopup; // 2026-07-26: 방 그래프의 "진영" 팝업
        [SerializeField] private ItemRewardPopup itemRewardPopup; // 2026-07-26: 전투 승리 아이템 드롭 알림
        [SerializeField] private DummyBattlePanel battlePanel; // BattleBridge.Implementation의 M6 더미 구현
        [SerializeField] private RoomTypeVisualSet visuals;
        [SerializeField] private RunConfigAsset runConfig;
        [SerializeField] private EnemyCompositionConfigAsset enemyCompositionConfigAsset; // §4-28
        [SerializeField] private ItemDropConfigAsset itemDropConfigAsset; // §4-28

        [Header("개발용 맵 생성 설정 — MapSelect 없이 씬을 단독 실행할 때만 사용 (§5.2)")]
        [SerializeField] private int seed = 42;
        [SerializeField] private bool randomizeSeed;

        /// <summary>테스트/툴링에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private RunState run;
        private System.Random rng;
        private List<EventData> eventDataPool;
        private Dictionary<string, EventDefinition> eventDefsById;
        private Dictionary<string, ArmyDefinition> armyDefsById;
        private Dictionary<string, ItemDefinition> itemDefsById;
        private Dictionary<string, AugmentDefinition> augmentDefsById;
        private Dictionary<string, PlayerCharacterDefinition> characterDefsById; // §5.2.5
        private Dictionary<ArmyClass, string> itemIdByClass; // §4-28: 병과→아이템 매핑, Start()에서 한 번만 계산
        private ArmyData enemyTemplate; // §4-28: 적 구성 스탯 템플릿, Start()에서 한 번만 확정
        private string savePath;
        private bool runEnded;
        private RoomType currentBattleRoomType;
        private List<EnemyArmy> currentEnemyComposition;

        // §4-28: RoomEncounterTable 협의 전 임시 대체 — 아웃게임 내부 전용(아이템 드롭 계산용),
        // §7 인터페이스(BattleSetupData)에는 노출하지 않는다. 위 Asset 필드에서 Start()에 채워진다.
        private EnemyCompositionConfig enemyCompositionConfig;
        private ItemDropConfig itemDropConfig;

        private void Start()
        {
            // 이후 검증에서 예외가 나더라도 PendingRun이 stale 상태로 남지 않도록 가장 먼저 소비한다.
            RunState pendingRun = RunSessionContext.ConsumePendingRun();

            if (visuals == null)
                visuals = Resources.Load<RoomTypeVisualSet>("OutGame/RoomTypeVisuals"); // 배선 누락 대비 폴백
            if (runConfig == null)
                runConfig = Resources.Load<RunConfigAsset>("OutGame/Data/RunConfig_Default");
            if (enemyCompositionConfigAsset == null)
                enemyCompositionConfigAsset = Resources.Load<EnemyCompositionConfigAsset>("OutGame/Data/EnemyCompositionConfig_Default");
            if (itemDropConfigAsset == null)
                itemDropConfigAsset = Resources.Load<ItemDropConfigAsset>("OutGame/Data/ItemDropConfig_Default");

            if (mapPanel == null || roomPanel == null || eventPanel == null || restPanel == null
                || augmentPanel == null || deploymentPanel == null || armyFormationPopup == null
                || itemRewardPopup == null || battlePanel == null
                || visuals == null || runConfig == null
                || enemyCompositionConfigAsset == null || itemDropConfigAsset == null)
                throw new InvalidOperationException(
                    "InGameFlowController의 필수 참조가 배선되지 않았습니다 — 씬 구성(SceneSetupM2/M4/M6) 확인");

            // ToConfig()가 Validate()를 포함하므로(다른 config 에셋과 동일 패턴) 잘못된 인스펙터
            // 값은 여기서 바로 예외로 드러난다 — 조용히 잘못된 값으로 동작하지 않는다.
            enemyCompositionConfig = enemyCompositionConfigAsset.ToConfig();
            itemDropConfig = itemDropConfigAsset.ToConfig();

            List<EventDefinition> eventPool = Resources.LoadAll<EventDefinition>("OutGame/Data/Events").ToList();
            if (eventPool.Count == 0)
                throw new InvalidOperationException("이벤트 정의를 찾을 수 없습니다 — SceneSetupM4Data.Run() 실행 필요");
            eventDataPool = eventPool.Select(e => e.ToData()).ToList(); // 한 번만 변환해 캐시 (매 방문마다 재파싱 방지)
            eventDefsById = eventPool.ToDictionary(e => e.ToData().id);

            List<AugmentDefinition> augmentPool = Resources.LoadAll<AugmentDefinition>("OutGame/Data/Augments").ToList();
            if (augmentPool.Count == 0)
                throw new InvalidOperationException("증강 정의를 찾을 수 없습니다 — SceneSetupM4Data.Run() 실행 필요 (§4-27)");
            augmentDefsById = augmentPool.ToDictionary(a => a.ToData().id);

            List<PlayerCharacterDefinition> characterPool = Resources.LoadAll<PlayerCharacterDefinition>("OutGame/Data/Characters").ToList();
            if (characterPool.Count == 0)
                throw new InvalidOperationException("플레이어 캐릭터 정의를 찾을 수 없습니다 — SceneSetupM7Data.Run() 실행 필요 (§5.2.5)");
            characterDefsById = characterPool.ToDictionary(c => c.ToData().id);

            armyDefsById = Resources.LoadAll<ArmyDefinition>("OutGame/Data")
                .ToDictionary(a => a.ToData().id);
            itemDefsById = Resources.LoadAll<ItemDefinition>("OutGame/Data")
                .ToDictionary(i => i.ToData().id);
            // §4-28: 병과→아이템 매핑은 런 도중 안 바뀌므로 승리마다 다시 만들지 않고 한 번만 캐시.
            itemIdByClass = ItemEquipService.ResolveItemIdByClass(itemDefsById.Values.Select(d => d.ToData()));

            // §4-28: 적 구성의 스탯 템플릿 — 매 전투방 진입마다 다시 찾지 않도록 한 번만 확정,
            // 여기서 실패하면(다른 dict 조회들과 달리) 조용히 넘어가지 않고 바로 fail-fast.
            string startingArmyDefId = runConfig.ToData().startingArmyDefId;
            if (!armyDefsById.TryGetValue(startingArmyDefId, out ArmyDefinition startingArmyDef))
                throw new InvalidOperationException(
                    $"RunConfig.startingArmyDefId('{startingArmyDefId}')에 해당하는 ArmyDefinition을 찾을 수 없습니다 " +
                    "— 적 구성 스탯 템플릿으로 쓸 수 없습니다.");
            enemyTemplate = startingArmyDef.ToData();

            savePath = RunSaveService.DefaultPath;
            run = pendingRun;
            if (run == null)
            {
                // MapSelect/캐릭터 선택(§5.2.5)을 거치지 않고 이 씬을 단독 실행했을 때의 개발용 폴백
                // — 정상 플로우라면 항상 CharacterSelectController가 selectedCharacterId를 채운 뒤 넘어온다.
                int usedSeed = randomizeSeed ? Environment.TickCount : seed;
                MapState map = new MapGenerator(new MapGenerationConfig(), usedSeed).Generate();
                run = RunStateFactory.Create(map, runConfig.ToData());
                // CharacterSelectController와 동일하게 SortOrder 기준 1번째를 기본값으로 —
                // Resources.LoadAll/Dictionary 열거 순서는 보장되지 않는다 (코드 리뷰 HIGH 수정).
                run.selectedCharacterId = characterDefsById.Values
                    .OrderBy(c => c.SortOrder).First().ToData().id;
            }
            rng = new System.Random(Environment.TickCount);

            // BattleBridge.Implementation은 씬이 로드될 때마다 재등록해야 한다 — Domain Reload가
            // 꺼져 있어도 파괴된 오브젝트의 클로저를 가리키지 않도록 (BattleBridge.cs 참조).
            BattleBridge.Implementation = battlePanel.Open;

            mapPanel.RoomSelected += OnRoomSelected;
            mapPanel.FormationRequested += OnFormationRequested;
            roomPanel.Completed += OnRoomCompleted;
            eventPanel.Completed += OnRoomCompleted;
            restPanel.Completed += OnRoomCompleted;
            augmentPanel.Completed += OnRoomCompleted;
            deploymentPanel.Confirmed += OnBattleSetupConfirmed;
            armyFormationPopup.Changed += OnArmyFormationChanged;

            roomPanel.Hide();
            mapPanel.Open(run.mapState);
            mapPanel.SetGold(run.gold); // 2026-07-26: 방 그래프 우측 상단 재화 표시
        }

        private void OnDestroy()
        {
            if (mapPanel != null) mapPanel.RoomSelected -= OnRoomSelected;
            if (mapPanel != null) mapPanel.FormationRequested -= OnFormationRequested;
            if (roomPanel != null) roomPanel.Completed -= OnRoomCompleted;
            if (eventPanel != null) eventPanel.Completed -= OnRoomCompleted;
            if (restPanel != null) restPanel.Completed -= OnRoomCompleted;
            if (augmentPanel != null) augmentPanel.Completed -= OnRoomCompleted;
            if (deploymentPanel != null) deploymentPanel.Confirmed -= OnBattleSetupConfirmed;
            if (armyFormationPopup != null) armyFormationPopup.Changed -= OnArmyFormationChanged;
        }

        private void OnFormationRequested()
        {
            armyFormationPopup.Open(run, runConfig.ToData(),
                armyDefsById.Values.ToList(), itemDefsById.Values.ToList(), augmentDefsById.Values.ToList());
        }

        /// <summary>진영 팝업 안에서 업그레이드로 골드를 쓰면 방 그래프 재화 표시도 즉시 최신 상태로
        /// 유지한다(2026-07-26 사용자 확정) — 팝업이 전체화면 dim이라 그 표시가 가려져 있는 동안에도
        /// 미리 동기화해둬야 팝업을 닫는 순간 바로 정확한 값이 보인다.</summary>
        private void OnArmyFormationChanged() => mapPanel.SetGold(run.gold);

        private void OnRoomSelected(MapNode node)
        {
            // 진영 팝업을 열어둔 채로 다른 방을 선택했을 가능성에 대비 — 다음 방 패널 위에 잔존해서
            // 보이지 않도록 방어적으로 닫는다(2026-07-26, 배치 패널의 팝업 잔존 방지와 동일한 이유).
            armyFormationPopup.Hide();

            MapProgress.Visit(run.mapState, node.point);
            mapPanel.Refresh();

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
                case RoomType.Rest:
                    restPanel.Open(run, runConfig.ToData(),
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
            // RoomEncounterTable 협의 전 임시 키(§9) — 적 구성이 정의되면 노드별 실제 값으로 대체
            string encounterId = $"enc_{node.roomType}";
            // §4-28: 적 구성을 미리 생성해둔다 — 순수 아웃게임 내부용(아이템 드롭·전투력 계산),
            // BattleSetupData에는 안 실음. 스탯 템플릿은 Start()에서 확정해둔 시작 군대(army_basic)를
            // 그대로 물려받는다 — ArmyDefinition이 여러 종류가 되면 이 자리를 풀(pool)에서 고르도록 확장.
            // 2026-07-26: 난이도 기준을 "층수(node.point.y)"에서 run.powerRoomsVisited(증원·증강·
            // 이벤트 방 통과 횟수)로 교체 — 전투방만 연달아 나오는 런에서 플레이어 보강 없이 적만
            // 계속 세지는 불균형을 막기 위함(§4-28 재설계).
            currentEnemyComposition = EnemyCompositionGenerator.Generate(
                run.powerRoomsVisited, node.roomType, enemyCompositionConfig, enemyTemplate, rng);
            deploymentPanel.Open(run, node.id, node.roomType, encounterId,
                armyDefsById.Values.ToList(), itemDefsById.Values.ToList(), runConfig.ToData(),
                augmentDefsById.Values.ToList(), characterDefsById.Values.ToList(), currentEnemyComposition);
        }

        private void OnBattleSetupConfirmed(BattleSetupData setup)
        {
            currentBattleRoomType = setup.roomType;
            deploymentPanel.Close();
            BattleBridge.StartBattle(setup, OnBattleResult);
        }

        private void OnBattleResult(BattleResultData result)
        {
            if (!result.victory)
            {
                runEnded = true;
                RunSaveService.DeleteSave(savePath); // 패배 — 런 종료 (§4-14)
                roomPanel.ShowDefeat();
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
                // 보스 "방문"이 아니라 "승리"가 런 클리어 조건이다 (MapProgress.HasVisitedBoss와 혼동 주의).
                runEnded = true;
                RunSaveService.DeleteSave(savePath); // 런 종료 — 이어하기 대상에서 제외 (§5.1)
                roomPanel.ShowRunClear();
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

            mapPanel.Refresh();
            mapPanel.SetGold(run.gold); // 이벤트/전투 보상으로 바뀐 골드를 방 그래프 복귀 시 반영
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
