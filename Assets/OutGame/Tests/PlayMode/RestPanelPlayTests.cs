using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// RestPanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM4UI.Run()으로 생성돼 있어야 한다.
    /// </summary>
    public class RestPanelPlayTests
    {
        private GameObject canvasGo;
        private RestPanel panel;
        private RunState run;
        private Dictionary<string, ArmyDefinition> armyDefsById;
        private Dictionary<string, ItemDefinition> itemDefsById;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/RestPanel");
            Assert.IsNotNull(prefab, "RestPanel 프리팹 없음 — SceneSetupM4UI.Run() 실행 필요");
            panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<RestPanel>();

            var armyDef = Resources.Load<ArmyDefinition>("OutGame/Data/ArmyDefinition_Basic");
            Assert.IsNotNull(armyDef);
            armyDefsById = new Dictionary<string, ArmyDefinition> { ["army_basic"] = armyDef };

            var bowDef = Resources.Load<ItemDefinition>("OutGame/Data/ItemDefinition_Bow");
            Assert.IsNotNull(bowDef);
            itemDefsById = new Dictionary<string, ItemDefinition> { ["item_bow"] = bowDef };

            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 2, startingArmyDefId = "army_basic" });
        }

        [TearDown]
        public void TearDown() => Object.Destroy(canvasGo);

        [UnityTest]
        public IEnumerator Open_SpawnsOptionPerArmy()
        {
            panel.Open(run, armyDefsById, itemDefsById);
            yield return null;

            var buttons = panel.GetComponentsInChildren<Button>()
                .Where(b => b.transform.parent.name == "ArmyListContainer").ToArray();
            Assert.AreEqual(2, buttons.Length);
        }

        [UnityTest]
        public IEnumerator Open_ContinueButtonAndResultText_StartInactive()
        {
            panel.Open(run, armyDefsById, itemDefsById);
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

            panel.Open(run, armyDefsById, itemDefsById);
            yield return null;

            var optionLabel = panel.GetComponentsInChildren<Button>()
                .First(b => b.transform.parent.name == "ArmyListContainer")
                .GetComponentInChildren<Text>();
            StringAssert.StartsWith("궁수 군대", optionLabel.text);
        }

        [UnityTest]
        public IEnumerator SelectArmy_ReinforcesBy20PercentOfBase()
        {
            panel.Open(run, armyDefsById, itemDefsById);
            yield return null;

            string targetId = run.armies[0].instanceId;
            Assert.AreEqual(0, run.armies[0].bonusSoldierCount);

            var optionButton = panel.GetComponentsInChildren<Button>()
                .First(b => b.transform.parent.name == "ArmyListContainer");
            optionButton.onClick.Invoke();
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
            panel.Open(run, armyDefsById, itemDefsById);
            yield return null;

            panel.GetComponentsInChildren<Button>().First(b => b.transform.parent.name == "ArmyListContainer").onClick.Invoke();
            yield return null;

            bool completed = false;
            panel.Completed += () => completed = true;

            panel.transform.Find("Window/ContinueButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(completed);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }
    }
}
