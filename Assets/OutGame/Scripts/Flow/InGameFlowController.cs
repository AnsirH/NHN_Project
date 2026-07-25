using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;
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
        private Dictionary<ArmyClass, string> itemIdByClass; // §4-28: 병과→아이템 매핑, Start()에서 한 번만 계산
        private string savePath;
        private bool runEnded;
        private RoomType currentBattleRoomType;
        private List<ArmyClass> currentEnemyComposition;

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
                || augmentPanel == null || deploymentPanel == null || battlePanel == null
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

            armyDefsById = Resources.LoadAll<ArmyDefinition>("OutGame/Data")
                .ToDictionary(a => a.ToData().id);
            itemDefsById = Resources.LoadAll<ItemDefinition>("OutGame/Data")
                .ToDictionary(i => i.ToData().id);
            // §4-28: 병과→아이템 매핑은 런 도중 안 바뀌므로 승리마다 다시 만들지 않고 한 번만 캐시.
            itemIdByClass = ItemEquipService.ResolveItemIdByClass(itemDefsById.Values.Select(d => d.ToData()));

            savePath = RunSaveService.DefaultPath;
            run = pendingRun;
            if (run == null)
            {
                // MainMenu/MapSelect를 거치지 않고 이 씬을 단독 실행했을 때의 개발용 폴백
                int usedSeed = randomizeSeed ? Environment.TickCount : seed;
                MapState map = new MapGenerator(new MapGenerationConfig(), usedSeed).Generate();
                run = RunStateFactory.Create(map, runConfig.ToData());
            }
            rng = new System.Random(Environment.TickCount);

            // BattleBridge.Implementation은 씬이 로드될 때마다 재등록해야 한다 — Domain Reload가
            // 꺼져 있어도 파괴된 오브젝트의 클로저를 가리키지 않도록 (BattleBridge.cs 참조).
            BattleBridge.Implementation = battlePanel.Open;

            mapPanel.RoomSelected += OnRoomSelected;
            roomPanel.Completed += OnRoomCompleted;
            eventPanel.Completed += OnRoomCompleted;
            restPanel.Completed += OnRoomCompleted;
            augmentPanel.Completed += OnRoomCompleted;
            deploymentPanel.Confirmed += OnBattleSetupConfirmed;

            roomPanel.Hide();
            mapPanel.Open(run.mapState);
        }

        private void OnDestroy()
        {
            if (mapPanel != null) mapPanel.RoomSelected -= OnRoomSelected;
            if (roomPanel != null) roomPanel.Completed -= OnRoomCompleted;
            if (eventPanel != null) eventPanel.Completed -= OnRoomCompleted;
            if (restPanel != null) restPanel.Completed -= OnRoomCompleted;
            if (augmentPanel != null) augmentPanel.Completed -= OnRoomCompleted;
            if (deploymentPanel != null) deploymentPanel.Confirmed -= OnBattleSetupConfirmed;
        }

        private void OnRoomSelected(MapNode node)
        {
            MapProgress.Visit(run.mapState, node.point);
            mapPanel.Refresh();

            // 주의: MapProgress.HasVisitedBoss는 보스 "방문" 여부이지 "승리" 여부가 아니다.
            // 런 클리어는 보스 전투에서 승리했을 때만 성립하므로(OnBattleResult), 여기서 미리 판정하지 않는다.

            // 저장은 방 결과(보상 적용 등)까지 반영된 뒤 OnRoomCompleted에서 한 번만 수행한다.
            // 여기서 먼저 저장하면 "방문함"만 기록되고 보상은 누락된 상태로 저장될 위험이 있다.
            switch (node.roomType)
            {
                case RoomType.Event:
                    OpenEventRoom();
                    break;
                case RoomType.Rest:
                    restPanel.Open(run, armyDefsById, itemDefsById);
                    break;
                case RoomType.Augment:
                    OpenAugmentRoom();
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
            // §4-28: 적 구성을 미리 생성해둔다 — 순수 아웃게임 내부용(아이템 드롭 계산), BattleSetupData에는 안 실음.
            currentEnemyComposition = EnemyCompositionGenerator.Generate(
                node.point.y, node.roomType, enemyCompositionConfig, rng);
            deploymentPanel.Open(run, node.id, node.roomType, encounterId,
                armyDefsById.Values.ToList(), itemDefsById.Values.ToList(), runConfig.ToData(),
                augmentDefsById.Values.ToList(), currentEnemyComposition);
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
            List<string> drops = ItemDropCalculator.RollDrops(currentEnemyComposition, itemDropConfig, itemIdByClass, rng);
            BattleRewardApplier.ApplyItemDrops(run, drops);
            currentEnemyComposition = null;

            if (currentBattleRoomType == RoomType.Boss)
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
