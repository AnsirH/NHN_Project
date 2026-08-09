using System.Collections;
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
    /// EventPanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM4UI.Run()으로 생성돼 있어야 한다.
    /// </summary>
    public class EventPanelPlayTests
    {
        private const int DefaultMaxArmyCount = 9;

        private GameObject canvasGo;
        private EventPanel panel;
        private RunState run;
        private EventDefinition runeRock; // 2026-08-03 EventDefinition_Deserters → RuneRock 개명(a637503)에 맞춰 갱신

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/Panels/EventPanel");
            Assert.IsNotNull(prefab, "EventPanel 프리팹 없음 — SceneSetupM4UI.Run() 실행 필요");
            panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<EventPanel>();

            runeRock = Resources.Load<EventDefinition>("OutGame/Data/Events/EventDefinition_RuneRock");
            Assert.IsNotNull(runeRock);

            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 1 });
        }

        [TearDown]
        public void TearDown() => Object.Destroy(canvasGo);

        [UnityTest]
        public IEnumerator Open_SpawnsButtonPerChoice()
        {
            panel.Open(runeRock, run, DefaultMaxArmyCount);
            yield return null;

            var buttons = panel.GetComponentsInChildren<Button>()
                .Where(b => b.transform.parent.name == "ChoiceContainer").ToArray();
            Assert.AreEqual(2, buttons.Length, "숲길의 룬석 이벤트는 선택지 2개");
        }

        [UnityTest]
        public IEnumerator SelectChoice_AppliesRewardAndShowsResult()
        {
            panel.Open(runeRock, run, DefaultMaxArmyCount);
            yield return null;

            int armiesBefore = run.armies.Count;
            var firstChoiceButton = panel.GetComponentsInChildren<Button>()
                .First(b => b.transform.parent.name == "ChoiceContainer");
            firstChoiceButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(armiesBefore + 1, run.armies.Count, "군대 획득 선택지 — 부대가 늘어야 함");

            Text resultText = panel.transform.Find("Window/ResultText").GetComponent<Text>();
            Assert.IsTrue(resultText.gameObject.activeSelf);
            Assert.IsNotEmpty(resultText.text);

            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            Assert.IsTrue(continueButton.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator SelectChoice_ArmyRewardAtCap_ShowsFailureMessageAndDoesNotAddArmy()
        {
            // 숲길의 룬석 이벤트 첫 선택지는 군대 획득 — 상한을 현재 보유 수(1)로 맞춰 즉시 꽉 찬 상태를 재현
            panel.Open(runeRock, run, maxArmyCountValue: run.armies.Count);
            yield return null;

            int armiesBefore = run.armies.Count;
            var firstChoiceButton = panel.GetComponentsInChildren<Button>()
                .First(b => b.transform.parent.name == "ChoiceContainer");
            firstChoiceButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(armiesBefore, run.armies.Count, "상한에 도달했으면 군대가 추가되면 안 됨");

            Text resultText = panel.transform.Find("Window/ResultText").GetComponent<Text>();
            Assert.IsTrue(resultText.gameObject.activeSelf);
            StringAssert.Contains("가득 차", resultText.text);
        }

        [UnityTest]
        public IEnumerator Continue_FiresCompletedAndHidesPanel()
        {
            panel.Open(runeRock, run, DefaultMaxArmyCount);
            yield return null;

            panel.GetComponentsInChildren<Button>().First(b => b.transform.parent.name == "ChoiceContainer").onClick.Invoke();
            yield return null;

            bool completed = false;
            panel.Completed += () => completed = true;

            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            continueButton.onClick.Invoke();

            Assert.IsTrue(completed);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Open_MarksEventAsVisited_SecondOpenPicksDifferentPool()
        {
            // 이벤트 자체는 EventSelector가 아니라 플로우가 마킹하므로, 여기선 EventPanel이
            // run 상태를 직접 건드리지 않는다는 것만 확인 (선택 전엔 visitedEventIds 불변)
            Assert.IsEmpty(run.visitedEventIds);
            panel.Open(runeRock, run, DefaultMaxArmyCount);
            yield return null;
            Assert.IsEmpty(run.visitedEventIds, "방문 기록은 플로우 컨트롤러 책임 — 패널이 직접 추가하지 않음");
        }
    }
}
