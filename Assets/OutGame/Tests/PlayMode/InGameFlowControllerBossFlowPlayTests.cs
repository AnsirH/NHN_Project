using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Battle;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
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
    /// InGame.unity(빌드 세팅 등록됨)를 직접 로드해 실제 배선을 통합 검증한다.
    /// </summary>
    public class InGameFlowControllerBossFlowPlayTests
    {
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        // Single 모드로 로드하면 테스트 러너의 기본 씬을 통째로 대체해 이후 다른 테스트(예: UICaptureTests)에
        // 잔상을 남긴다 — Additive로 얹었다가 TearDown에서 반드시 걷어낸다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(SceneNames.InGame);
        }

        private static T GetField<T>(object target, string name) =>
            (T)typeof(InGameFlowController).GetField(name, Priv).GetValue(target);

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
            yield return SceneManager.LoadSceneAsync(SceneNames.InGame, LoadSceneMode.Additive);
            yield return null;

            InGameFlowController flow = GameObject.Find("InGameFlow").GetComponent<InGameFlowController>();
            RunState run = GetField<RunState>(flow, "run");

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
            yield return SceneManager.LoadSceneAsync(SceneNames.InGame, LoadSceneMode.Additive);
            yield return null;

            InGameFlowController flow = GameObject.Find("InGameFlow").GetComponent<InGameFlowController>();
            RunState run = GetField<RunState>(flow, "run");

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
            yield return SceneManager.LoadSceneAsync(SceneNames.InGame, LoadSceneMode.Additive);
            yield return null;

            InGameFlowController flow = GameObject.Find("InGameFlow").GetComponent<InGameFlowController>();
            RunState run = GetField<RunState>(flow, "run");
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

            // 기본 보스 구성(EnemyCompositionConfig.bossComposition)에 궁수/전사가 포함돼 있고
            // 드롭 확률을 100%로 강제했으므로 최소 1개 이상은 반드시 늘어나야 한다.
            Assert.Greater(run.ownedItemIds.Count, ownedItemsBefore,
                "보스 승리 후 병과 기반 아이템 드롭으로 보유 아이템이 늘어나야 함 (§4-28)");
        }
    }
}
