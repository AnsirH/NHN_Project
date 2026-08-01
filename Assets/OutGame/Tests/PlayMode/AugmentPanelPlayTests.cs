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
    /// AugmentPanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM4UI.Run()으로 생성돼 있어야 한다.
    /// EventPanel과 공유하는 ChoicePanelBase(선택→적용→결과→계속) 흐름과 §4-27 증강 적용(중복 선택
    /// 허용)을 검증한다. 이전엔 AugmentPanel 전용 동작 테스트가 없었다(코드 리뷰 HIGH 지적).
    /// </summary>
    public class AugmentPanelPlayTests
    {
        private GameObject canvasGo;
        private AugmentPanel panel;
        private RunState run;
        private AugmentDefinition[] pool;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/AugmentPanel");
            Assert.IsNotNull(prefab, "AugmentPanel 프리팹 없음 — SceneSetupM4UI.Run() 실행 필요");
            panel = Object.Instantiate(prefab, canvasGo.transform).GetComponent<AugmentPanel>();

            pool = Resources.LoadAll<AugmentDefinition>("OutGame/Data/Augments");
            Assert.IsTrue(pool.Length >= 3, "증강 정의가 3개 이상 있어야 함 — SceneSetupM4Data.Run() 실행 필요");

            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 1 });
        }

        [TearDown]
        public void TearDown()
        {
            // ChoicePanelBase.Open()이 PanelTransitions.FadeIn()으로 건 DOTween 트윈이 테스트 종료
            // 시점에도 아직 살아있을 수 있다 — 죽이지 않고 CanvasGroup을 파괴하면 다음 틱에 그 트윈이
            // 이미 파괴된 CanvasGroup을 건드려 바로 다음 픽스처(실행 순서상 CharacterSelectControllerPlayTests)에서
            // 엉뚱하게 실패로 잡힌다(실제 발생, 2026-07-30).
            DG.Tweening.DOTween.KillAll();
            Object.Destroy(canvasGo);
        }

        private Button[] ChoiceButtons() =>
            panel.GetComponentsInChildren<Button>().Where(b => b.transform.parent.name == "ChoiceContainer").ToArray();

        [UnityTest]
        public IEnumerator Open_SpawnsThreeChoiceButtons()
        {
            panel.Open(run, pool, new System.Random(1));
            yield return null;

            Assert.AreEqual(3, ChoiceButtons().Length, "증강 방은 항상 3개 중 하나 선택(§4-27)");
        }

        [UnityTest]
        public IEnumerator SelectChoice_AddsToSelectedAugmentIdsAndShowsResult()
        {
            panel.Open(run, pool, new System.Random(1));
            yield return null;

            Assert.IsEmpty(run.selectedAugmentIds);
            ChoiceButtons().First().onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, run.selectedAugmentIds.Count);

            Text resultText = panel.transform.Find("Window/ResultText").GetComponent<Text>();
            Assert.IsTrue(resultText.gameObject.activeSelf);
            Assert.IsNotEmpty(resultText.text);

            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            Assert.IsTrue(continueButton.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator SelectChoice_HidesChoiceButtons()
        {
            panel.Open(run, pool, new System.Random(1));
            yield return null;

            Button[] buttons = ChoiceButtons();
            buttons.First().onClick.Invoke();
            yield return null;

            Assert.IsTrue(buttons.All(b => !b.gameObject.activeSelf), "선택 후에는 선택지 버튼이 전부 숨겨져야 함");
        }

        [UnityTest]
        public IEnumerator SelectChoice_AllowsStackingSameAugmentAcrossVisits()
        {
            // 같은 증강을 두 번 골라도 막지 않는다(§4-27: 중복 선택/스택 허용, AugmentApplier 참고).
            panel.Open(run, pool, new System.Random(1));
            yield return null;
            ChoiceButtons().First().onClick.Invoke();
            yield return null;
            string firstPick = run.selectedAugmentIds[0];

            panel.Open(run, pool, new System.Random(1)); // 같은 시드 → 같은 첫 옵션이 다시 뽑힘
            yield return null;
            ChoiceButtons().First().onClick.Invoke();
            yield return null;

            Assert.AreEqual(2, run.selectedAugmentIds.Count);
            Assert.AreEqual(firstPick, run.selectedAugmentIds[1], "같은 rng 시드면 같은 증강이 다시 뽑혀야 함");
        }

        [UnityTest]
        public IEnumerator Continue_FiresCompletedAndHidesPanel()
        {
            panel.Open(run, pool, new System.Random(1));
            yield return null;

            ChoiceButtons().First().onClick.Invoke();
            yield return null;

            bool completed = false;
            panel.Completed += () => completed = true;

            Button continueButton = panel.transform.Find("Window/ContinueButton").GetComponent<Button>();
            continueButton.onClick.Invoke();

            Assert.IsTrue(completed);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }
    }
}
