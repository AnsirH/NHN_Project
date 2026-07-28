using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Battle;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// 회귀 테스트 — 코드 리뷰에서 발견: MapProgress.HasVisitedBoss는 보스 "방문" 여부이지
    /// "승리" 여부가 아니라서, 예전 코드는 보스 노드를 선택하는 즉시(전투 없이) 런 클리어를 띄웠다.
    /// OutGame.unity(빌드 세팅 등록됨, §3.1 씬 통합)를 직접 로드해 실제 배선을 통합 검증한다.
    /// 방 그래프(RoomGraphRoot)는 씬 로드 시점엔 비활성 상태라 직접 활성화한 뒤 Begin(RunState)으로
    /// 진입시킨다 — 예전의 "MapSelect 없이 단독 실행 시 seed로 더미 런 생성" 폴백은 제거됐다.
    /// </summary>
    public class InGameFlowControllerBossFlowPlayTests
    {
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        // Single 모드로 로드하면 테스트 러너의 기본 씬을 통째로 대체해 이후 다른 테스트(예: UICaptureTests)에
        // 잔상을 남긴다 — Additive로 얹었다가 TearDown에서 반드시 걷어낸다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(SceneNames.OutGame);
        }

        private static T GetField<T>(object target, string name) =>
            (T)typeof(InGameFlowController).GetField(name, Priv).GetValue(target);

        /// <summary>OutGame.unity를 얹고 RoomGraphRoot를 활성화한 뒤 테스트용 RunState로 방 그래프에
        /// 진입시킨다 — 씬에 실제 배선된 InGameFlowController(패널·설정 에셋 전부 배선됨)를 그대로
        /// 쓰되, UI를 거치지 않고 Begin()을 직접 호출해 결정적으로 진입한다.</summary>
        private static IEnumerator LoadRoomGraph(int seed, System.Action<InGameFlowController, RunState> onReady)
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            yield return null;

            InGameFlowController flow = Object.FindFirstObjectByType<InGameFlowController>(FindObjectsInactive.Include);
            flow.gameObject.SetActive(true); // 최초 활성화 — Awake()가 이 시점에 동기 실행됨

            var runConfigAsset = GetField<RunConfigAsset>(flow, "runConfig");
            MapState map = new MapGenerator(new MapGenerationConfig(), seed).Generate();
            RunState run = RunStateFactory.Create(map, runConfigAsset.ToData());
            run.selectedCharacterId = "char_1";

            flow.Begin(run);
            yield return null;

            onReady(flow, run);
        }

        private static void WalkToJustBeforeBoss(MapState map)
        {
            while (true)
            {
                var selectable = MapProgress.GetSelectableNodes(map);
                if (selectable.Any(n => n.roomType == RoomType.Boss)) return;
                MapProgress.Visit(map, selectable[0].point);
            }
        }

        [UnityTest]
        public IEnumerator BossNodeSelected_OpensDeploymentPanel_NotImmediateRunClear()
        {
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });

            WalkToJustBeforeBoss(run.mapState);
            MapNode boss = MapProgress.GetSelectableNodes(run.mapState).First(n => n.roomType == RoomType.Boss);

            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { boss });
            yield return null;

            var deploymentPanel = GetField<ArmyDeploymentPanel>(flow, "deploymentPanel");
            var roomPanel = GetField<DummyRoomPanel>(flow, "roomPanel");

            Assert.IsTrue(deploymentPanel.gameObject.activeSelf,
                "보스 노드 선택 시 배치 UI가 열려야 함 (전투 없이 즉시 클리어되면 안 됨)");
            Assert.IsFalse(roomPanel.gameObject.activeSelf, "전투 전에는 런 클리어 화면이 뜨면 안 됨");
        }

        [UnityTest]
        public IEnumerator BossVictory_ShowsRunClear_AfterDeploymentAndBattle()
        {
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            // 이 테스트는 런 클리어 타이밍만 검증한다 — 드롭 확률이 기본값(0이 아님)이면 시드 고정이
            // 안 되는 System.Random(Environment.TickCount) 특성상 가끔 아이템 획득 팝업이 먼저 뜨면서
            // 런 클리어를 가로막아 이 테스트가 간헐적으로 실패할 수 있다. 0으로 고정해 결정적으로 만든다.
            var itemDropConfig = GetField<ItemDropConfig>(flow, "itemDropConfig");
            itemDropConfig.archerDropChance = 0f;
            itemDropConfig.warriorDropChance = 0f;
            itemDropConfig.hunterDropChance = 0f;
            itemDropConfig.assassinDropChance = 0f;

            WalkToJustBeforeBoss(run.mapState);
            MapNode boss = MapProgress.GetSelectableNodes(run.mapState).First(n => n.roomType == RoomType.Boss);
            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { boss });
            yield return null;

            var deploymentPanel = GetField<ArmyDeploymentPanel>(flow, "deploymentPanel");
            var battlePanel = GetField<DummyBattlePanel>(flow, "battlePanel");
            var roomPanel = GetField<DummyRoomPanel>(flow, "roomPanel");

            // 군대 보유 상한 = 배치 슬롯 수(§4-7)라 Open() 시점에 이미 전원 자동 배치돼 있다.
            var startButton = deploymentPanel.GetComponentsInChildren<Button>(true).First(b => b.name == "StartBattleButton");
            startButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(battlePanel.gameObject.activeSelf, "배치 확정 후 더미 전투 패널이 떠야 함");
            Assert.IsFalse(roomPanel.gameObject.activeSelf, "전투 결과가 나오기 전에는 런 클리어 화면이 뜨면 안 됨");

            var victoryButton = battlePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "VictoryButton");
            victoryButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(roomPanel.gameObject.activeSelf, "보스 승리 후에는 런 클리어 화면이 떠야 함");
        }

        [UnityTest]
        public IEnumerator BossVictory_WithGuaranteedDropChance_AddsItemsToInventory()
        {
            // §4-28 엔드투엔드 확인 — 드롭 확률을 100%로 강제해 결정적으로 검증한다(기본값은
            // System.Random(Environment.TickCount)라 시드 고정이 불가능하므로 확률 자체를 조작).
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            var itemDropConfig = GetField<ItemDropConfig>(flow, "itemDropConfig");
            itemDropConfig.archerDropChance = 1f;
            itemDropConfig.warriorDropChance = 1f;

            int ownedItemsBefore = run.ownedItemIds.Count;

            WalkToJustBeforeBoss(run.mapState);
            MapNode boss = MapProgress.GetSelectableNodes(run.mapState).First(n => n.roomType == RoomType.Boss);
            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { boss });
            yield return null;

            var deploymentPanel = GetField<ArmyDeploymentPanel>(flow, "deploymentPanel");
            var battlePanel = GetField<DummyBattlePanel>(flow, "battlePanel");
            var startButton = deploymentPanel.GetComponentsInChildren<Button>(true).First(b => b.name == "StartBattleButton");
            startButton.onClick.Invoke();
            yield return null;

            var victoryButton = battlePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "VictoryButton");
            victoryButton.onClick.Invoke();
            yield return null;

            // 기본 첫 티어 보스 구성(EnemyCompositionConfig.tiers[0].bossComposition)에 기본 병과만
            // 있어도, 궁수/전사가 드롭 매핑에 있고 확률을 100%로 강제했으므로 상관없다 —
            // 드롭 확률을 100%로 강제했으므로 최소 1개 이상은 반드시 늘어나야 한다.
            Assert.Greater(run.ownedItemIds.Count, ownedItemsBefore,
                "보스 승리 후 병과 기반 아이템 드롭으로 보유 아이템이 늘어나야 함 (§4-28)");
        }

        [UnityTest]
        public IEnumerator BossVictory_WithGuaranteedDropChance_BlocksRunClearUntilPopupClosed()
        {
            // 2026-07-26 사용자 요청: 아이템 획득 팝업이 뜬 동안은 다음 진행(런 클리어 화면)으로
            // 넘어가면 안 되고, 확인을 눌러야 이어져야 한다.
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            var itemDropConfig = GetField<ItemDropConfig>(flow, "itemDropConfig");
            itemDropConfig.archerDropChance = 1f;
            itemDropConfig.warriorDropChance = 1f;

            WalkToJustBeforeBoss(run.mapState);
            MapNode boss = MapProgress.GetSelectableNodes(run.mapState).First(n => n.roomType == RoomType.Boss);
            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { boss });
            yield return null;

            var deploymentPanel = GetField<ArmyDeploymentPanel>(flow, "deploymentPanel");
            var battlePanel = GetField<DummyBattlePanel>(flow, "battlePanel");
            var roomPanel = GetField<DummyRoomPanel>(flow, "roomPanel");
            var itemRewardPopup = GetField<ItemRewardPopup>(flow, "itemRewardPopup");

            var startButton = deploymentPanel.GetComponentsInChildren<Button>(true).First(b => b.name == "StartBattleButton");
            startButton.onClick.Invoke();
            yield return null;

            var victoryButton = battlePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "VictoryButton");
            victoryButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(itemRewardPopup.gameObject.activeSelf, "드롭이 있으면 획득 팝업이 떠야 함");
            Assert.IsFalse(roomPanel.gameObject.activeSelf, "획득 팝업을 닫기 전까지는 런 클리어 화면이 뜨면 안 됨");

            Button closeButton = itemRewardPopup.transform.Find("Window/CloseButton").GetComponent<Button>();
            closeButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(itemRewardPopup.gameObject.activeSelf, "확인 후에는 획득 팝업이 닫혀야 함");
            Assert.IsTrue(roomPanel.gameObject.activeSelf, "획득 팝업을 닫은 뒤에는 런 클리어 화면이 떠야 함");
        }

        [UnityTest]
        public IEnumerator BattleVictory_WithGuaranteedDropChance_ShowsItemRewardPopupForNonBossRoom()
        {
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            // 마지막 티어(카운터 6 이상)로 강제 — 전 병과 등장 가능 + enemyCount=9라 전원 기본(None)으로만
            // 나올 확률은 사실상 0에 가까워 결정적으로 취급해도 안전하다(§4-28).
            run.powerRoomsVisited = 6;
            var itemDropConfig = GetField<ItemDropConfig>(flow, "itemDropConfig");
            itemDropConfig.archerDropChance = 1f;
            itemDropConfig.warriorDropChance = 1f;
            itemDropConfig.hunterDropChance = 1f;
            itemDropConfig.assassinDropChance = 1f;

            MapNode battleNode = MapProgress.GetSelectableNodes(run.mapState).First(n => n.roomType == RoomType.NormalBattle);
            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { battleNode });
            yield return null;

            var deploymentPanel = GetField<ArmyDeploymentPanel>(flow, "deploymentPanel");
            var battlePanel = GetField<DummyBattlePanel>(flow, "battlePanel");
            var itemRewardPopup = GetField<ItemRewardPopup>(flow, "itemRewardPopup");

            var startButton = deploymentPanel.GetComponentsInChildren<Button>(true).First(b => b.name == "StartBattleButton");
            startButton.onClick.Invoke();
            yield return null;

            var victoryButton = battlePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "VictoryButton");
            victoryButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(itemRewardPopup.gameObject.activeSelf, "일반전투 승리 후에도 드롭이 있으면 획득 팝업이 떠야 함");

            Button closeButton = itemRewardPopup.transform.Find("Window/CloseButton").GetComponent<Button>();
            closeButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(itemRewardPopup.gameObject.activeSelf, "확인 후에는 획득 팝업이 닫혀야 함");
        }

        [UnityTest]
        public IEnumerator BattleVictory_WithZeroDropChance_SkipsPopupEntirely()
        {
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            var itemDropConfig = GetField<ItemDropConfig>(flow, "itemDropConfig");
            itemDropConfig.archerDropChance = 0f;
            itemDropConfig.warriorDropChance = 0f;
            itemDropConfig.hunterDropChance = 0f;
            itemDropConfig.assassinDropChance = 0f;

            MapNode battleNode = MapProgress.GetSelectableNodes(run.mapState).First(n => n.roomType == RoomType.NormalBattle);
            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { battleNode });
            yield return null;

            var deploymentPanel = GetField<ArmyDeploymentPanel>(flow, "deploymentPanel");
            var battlePanel = GetField<DummyBattlePanel>(flow, "battlePanel");
            var itemRewardPopup = GetField<ItemRewardPopup>(flow, "itemRewardPopup");

            var startButton = deploymentPanel.GetComponentsInChildren<Button>(true).First(b => b.name == "StartBattleButton");
            startButton.onClick.Invoke();
            yield return null;

            var victoryButton = battlePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "VictoryButton");
            victoryButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(itemRewardPopup.gameObject.activeSelf, "드롭이 없으면 획득 팝업이 뜨면 안 됨");
        }
    }
}
