using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// 방 그래프의 "진영" 팝업(2026-07-26 사용자 요청) 통합 검증 — RoomGraphPanel.FormationRequested →
    /// InGameFlowController → ArmyFormationPopup으로 이어지는 배선을 OutGame.unity를 직접 로드해
    /// 확인한다(§3.1 씬 통합). 방 그래프(RoomGraphRoot)는 씬 로드 시점엔 비활성 상태라 직접
    /// 활성화한 뒤 Begin(RunState)으로 진입시킨다.
    /// </summary>
    public class InGameFlowControllerFormationPopupPlayTests
    {
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(SceneNames.OutGame);
        }

        private static T GetField<T>(object target, string name) =>
            (T)typeof(InGameFlowController).GetField(name, Priv).GetValue(target);

        private static IEnumerator LoadRoomGraph(int seed, System.Action<InGameFlowController, RunState> onReady)
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            yield return null;

            InGameFlowController flow = Object.FindFirstObjectByType<InGameFlowController>(FindObjectsInactive.Include);
            flow.gameObject.SetActive(true);

            var runConfigAsset = GetField<RunConfigAsset>(flow, "runConfig");
            MapState map = new MapGenerator(new MapGenerationConfig(), seed).Generate();
            RunState run = RunStateFactory.Create(map, runConfigAsset.ToData());
            run.selectedCharacterId = "char_1";

            flow.Begin(run);
            yield return null;

            onReady(flow, run);
        }

        [UnityTest]
        public IEnumerator FormationButtonClicked_OpensArmyFormationPopup()
        {
            InGameFlowController flow = null;
            yield return LoadRoomGraph(1, (f, r) => flow = f);
            var mapPanel = GetField<RoomGraphPanel>(flow, "mapPanel");
            var armyFormationPopup = GetField<ArmyFormationPopup>(flow, "armyFormationPopup");

            Assert.IsFalse(armyFormationPopup.gameObject.activeSelf, "선행 조건: 진영 팝업은 처음엔 닫혀 있어야 함");

            Button formationButton = mapPanel.transform.Find("FormationButton").GetComponent<Button>();
            formationButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(armyFormationPopup.gameObject.activeSelf, "\"진영\" 버튼을 누르면 진영 팝업이 열려야 함");
        }

        [UnityTest]
        public IEnumerator ArmyFormationPopup_ShowsOnlyAllyCards_NoEnemyContent()
        {
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            var mapPanel = GetField<RoomGraphPanel>(flow, "mapPanel");
            var armyFormationPopup = GetField<ArmyFormationPopup>(flow, "armyFormationPopup");

            Button formationButton = mapPanel.transform.Find("FormationButton").GetComponent<Button>();
            formationButton.onClick.Invoke();
            yield return null;

            var cards = armyFormationPopup.GetComponentInChildren<AllyFormationView>()
                .GetComponentsInChildren<ArmyCardView>(includeInactive: true);
            Assert.AreEqual(run.armies.Count, cards.Length,
                "진영 팝업엔 보유 군대 카드만 보여야 함(적 진영 없음)");
            Assert.IsNull(armyFormationPopup.transform.Find("Window/EnemyColumn"),
                "진영 팝업엔 적 진영 컬럼이 아예 없어야 함");
            Assert.IsNull(armyFormationPopup.transform.Find("Window/StartBattleButton"),
                "진영 팝업엔 전투 시작 버튼이 없어야 함");
        }

        [UnityTest]
        public IEnumerator SelectingAnotherRoom_ForcesArmyFormationPopupClosed()
        {
            // 2026-07-26: 진영 팝업을 열어둔 채로 다른 방을 선택해도, 다음 방 패널 위에 잔존해서
            // 보이면 안 된다(배치 패널의 팝업 잔존 방지와 동일한 이유) — 방어적 Hide 검증.
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            var mapPanel = GetField<RoomGraphPanel>(flow, "mapPanel");
            var armyFormationPopup = GetField<ArmyFormationPopup>(flow, "armyFormationPopup");

            Button formationButton = mapPanel.transform.Find("FormationButton").GetComponent<Button>();
            formationButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(armyFormationPopup.gameObject.activeSelf, "선행 조건: 진영 팝업이 열려 있어야 함");

            MapNode firstNode = MapProgress.GetSelectableNodes(run.mapState).First();
            typeof(InGameFlowController).GetMethod("OnRoomSelected", Priv).Invoke(flow, new object[] { firstNode });
            yield return null;

            Assert.IsFalse(armyFormationPopup.gameObject.activeSelf,
                "다른 방을 선택하면 열려 있던 진영 팝업이 강제로 닫혀야 함");
        }

        [UnityTest]
        public IEnumerator Upgrade_InsideFormationPopup_SyncsMapGoldDisplayImmediately()
        {
            // 2026-07-26 사용자 확정: 진영 팝업 안에서 업그레이드해 골드를 쓰면, 방 그래프 우측 상단
            // 재화 표시도 팝업이 열려 있는 동안(닫기 전에) 즉시 갱신돼야 한다.
            InGameFlowController flow = null;
            RunState run = null;
            yield return LoadRoomGraph(1, (f, r) => { flow = f; run = r; });
            var mapPanel = GetField<RoomGraphPanel>(flow, "mapPanel");
            var armyFormationPopup = GetField<ArmyFormationPopup>(flow, "armyFormationPopup");
            // run.gold를 직접 바꾸는 것만으로는(정상 흐름의 보상 적용과 달리) 방 그래프 재화 표시가
            // 저절로 갱신되지 않는다 — mapPanel.SetGold를 거쳐야 화면에 반영되므로, "이미 500으로
            // 표시돼 있다"는 선행 조건을 이 호출로 직접 만들어준다(Begin()의 최초 동기화 시점엔
            // run.gold가 아직 0이었으므로).
            run.gold = 500;
            mapPanel.SetGold(run.gold);

            Button formationButton = mapPanel.transform.Find("FormationButton").GetComponent<Button>();
            formationButton.onClick.Invoke();
            yield return null;

            Text mapGoldLabel = mapPanel.transform.Find("CurrencyDisplay/Amount").GetComponent<Text>();
            Assert.AreEqual("500", mapGoldLabel.text, "선행 조건: 방 그래프 재화 표시가 최신 상태여야 함");

            var cardView = armyFormationPopup.GetComponentInChildren<AllyFormationView>()
                .GetComponentsInChildren<ArmyCardView>(includeInactive: true).First();
            cardView.OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;

            var infoPopup = armyFormationPopup.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Button upgradeButton = infoPopup.transform
                .Find("Window/BodyRow/GeneralColumn/UpgradeBand/UpgradeButton").GetComponent<Button>();
            upgradeButton.onClick.Invoke();
            yield return null;

            Assert.Less(run.gold, 500, "업그레이드로 골드가 차감돼야 함");
            Assert.AreEqual(run.gold.ToString(), mapGoldLabel.text,
                "진영 팝업 안에서 업그레이드해도 방 그래프 재화 표시가 팝업을 닫기 전에 즉시 갱신돼야 함");
        }
    }
}
