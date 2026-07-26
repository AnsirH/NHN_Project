using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// RestPanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM4UI.Run()으로 생성돼 있어야 한다.
    /// 증원 대상 선택은 배치 화면과 동일한 진영 그리드(AllyFormationView)를 재사용한다(2026-07-26
    /// 사용자 요청) — 카드를 누르면 그 즉시 증원된다(확인 절차 없음).
    /// </summary>
    public class RestPanelPlayTests
    {
        private GameObject canvasGo;
        private GameObject eventSystemGo;
        private RestPanel panel;
        private RunState run;
        private ArmyDefinition armyDef;
        private ItemDefinition bowDef;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            GameObject prefab = Resources.Load<GameObject>("OutGame/RestPanel");
            Assert.IsNotNull(prefab, "RestPanel 프리팹 없음 — SceneSetupM4UI.Run() 실행 필요");
            panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<RestPanel>();

            armyDef = Resources.Load<ArmyDefinition>("OutGame/Data/ArmyDefinition_Basic");
            Assert.IsNotNull(armyDef);
            bowDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Bow");
            Assert.IsNotNull(bowDef);

            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 2, startingArmyDefId = "army_basic" });
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
            Object.Destroy(eventSystemGo);
        }

        private void OpenPanel() =>
            panel.Open(run, new RunConfig { startingArmyCount = 2, startingArmyDefId = "army_basic" },
                new[] { armyDef }, new[] { bowDef }, new AugmentDefinition[0]);

        private ArmyCardView[] Cards() =>
            panel.GetComponentInChildren<AllyFormationView>().GetComponentsInChildren<ArmyCardView>(includeInactive: true);

        [UnityTest]
        public IEnumerator Open_SpawnsCardPerArmy()
        {
            OpenPanel();
            yield return null;

            Assert.AreEqual(2, Cards().Length);
        }

        [UnityTest]
        public IEnumerator Open_ContinueButtonAndResultText_StartInactive()
        {
            OpenPanel();
            yield return null;

            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            Text resultText = panel.transform.Find("Window/ResultText").GetComponent<Text>();

            Assert.IsFalse(continueButton.gameObject.activeSelf, "선택 전에는 계속 버튼이 비활성이어야 함");
            Assert.IsFalse(resultText.gameObject.activeSelf, "선택 전에는 결과 텍스트가 비활성이어야 함");
        }

        [UnityTest]
        public IEnumerator Open_ArmyWithEquippedItem_ShowsClassAwareName()
        {
            // 배치 UI와 동일하게 병과 반영 이름이 나와야 한다 (§2 용어) — 각자 계산해서 한쪽만 반영됐던
            // 회귀 버그 재발 방지 (2026-07-19).
            run.ownedItemIds.Add("item_bow");
            OutGame.Logic.Items.ItemEquipService.Equip(run, run.armies[0].instanceId, "item_bow");

            OpenPanel();
            yield return null;

            var card = Cards().First(c => c.ArmyInstanceId == run.armies[0].instanceId);
            StringAssert.StartsWith("궁수 군대", card.GetComponentInChildren<Text>().text);
        }

        [UnityTest]
        public IEnumerator ClickCard_DoesNotOpenArmyInfoPopup()
        {
            // 2026-07-26: 증원 방은 AllyFormationView를 "선택 모드"로 연다 — 카드 클릭이 정보 팝업
            // 대신 즉시 증원으로 이어져야 하므로, 정보 팝업이 뜨면 안 된다(선택 모드 회귀 방지).
            OpenPanel();
            yield return null;

            Cards().First().OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;

            var infoPopup = panel.GetComponentInChildren<ArmyInfoPopup>(includeInactive: true);
            Assert.IsFalse(infoPopup.gameObject.activeSelf, "선택 모드에서는 카드를 눌러도 군대 정보 팝업이 뜨면 안 됨");
        }

        [UnityTest]
        public IEnumerator ClickCard_ReinforcesBy20PercentOfBase()
        {
            OpenPanel();
            yield return null;

            string targetId = run.armies[0].instanceId;
            Assert.AreEqual(0, run.armies[0].bonusSoldierCount);

            Cards().First(c => c.ArmyInstanceId == targetId).OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;

            Assert.AreEqual(6, run.GetArmy(targetId).bonusSoldierCount, "30명 기본 × 20% = 6명 증원");

            Text resultText = panel.transform.Find("Window/ResultText").GetComponent<Text>();
            Assert.IsTrue(resultText.gameObject.activeSelf);
            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            Assert.IsTrue(continueButton.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Continue_FiresCompletedAndHidesPanel()
        {
            OpenPanel();
            yield return null;

            Cards().First().OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;

            bool completed = false;
            panel.Completed += () => completed = true;

            panel.transform.Find("Window/ContinueButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(completed);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Open_WithNoArmies_SkipsGridAndShowsFallbackMessage()
        {
            // 소프트락 방지(기존 코드의 의도 유지) — 보유 군대가 없으면 그리드를 열지 않고 바로
            // 안내 메시지 + 계속 버튼만 보여줘야 한다.
            run.armies.Clear();
            OpenPanel();
            yield return null;

            var allyFormationView = panel.GetComponentInChildren<AllyFormationView>(includeInactive: true);
            Assert.IsFalse(allyFormationView.gameObject.activeSelf, "군대가 없으면 진영 그리드를 열면 안 됨");

            Text resultText = panel.transform.Find("Window/ResultText").GetComponent<Text>();
            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            Assert.IsTrue(resultText.gameObject.activeSelf);
            Assert.IsTrue(continueButton.gameObject.activeSelf);
            Assert.AreEqual("증원할 수 있는 부대가 없습니다.", resultText.text);
        }

        [UnityTest]
        public IEnumerator Open_WithOnlyUnresolvableArmyDefinitions_SkipsGridAndShowsFallbackMessage()
        {
            // 코드 리뷰로 발견된 소프트락 회귀 방지 — 보유 군대는 있지만(run.armies.Count > 0)
            // 그중 어느 것도 armyDefsById에서 정의를 찾을 수 없으면(예: 정의 자산이 삭제/개명된
            // 저장 데이터), AllyFormationView는 카드를 대체 이름으로 그냥 그려버려서 예전 목록
            // UI처럼 옵션에서 빠지지 않는다 — 클릭해도 조용히 무시되는 그리드만 남는 소프트락이
            // 재현되면 안 된다.
            panel.Open(run, new RunConfig { startingArmyCount = 2, startingArmyDefId = "army_basic" },
                new ArmyDefinition[0], new[] { bowDef }, new AugmentDefinition[0]);
            yield return null;

            var allyFormationView = panel.GetComponentInChildren<AllyFormationView>(includeInactive: true);
            Assert.IsFalse(allyFormationView.gameObject.activeSelf,
                "보유 군대 전부가 정의를 찾을 수 없으면 진영 그리드를 열면 안 됨");

            Text resultText = panel.transform.Find("Window/ResultText").GetComponent<Text>();
            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            Assert.IsTrue(resultText.gameObject.activeSelf);
            Assert.IsTrue(continueButton.gameObject.activeSelf);
        }
    }
}
