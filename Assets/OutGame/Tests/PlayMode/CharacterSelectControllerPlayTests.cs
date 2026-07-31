using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// CharacterSelectScreen 프리팹 스모크 테스트 — 프리팹은 SceneSetupM7UI.Run()으로 생성돼 있어야
    /// 하고, PlayerCharacterDefinition_Char1~4.asset(SceneSetupM7Data.Run())이 있어야 한다 (§5.2.5).
    /// </summary>
    public class CharacterSelectControllerPlayTests
    {
        private GameObject canvasGo;
        private CharacterSelectController controller;
        private List<RunState> confirmedRuns;

        [SetUp]
        public void SetUp()
        {
            // 이전 픽스처(예: ArmyDeploymentPanelPlayTests)가 PanelTransitions.FadeIn()으로 건 DOTween
            // 트윈을 TearDown에서 죽이지 않은 채 CanvasGroup을 파괴하면, 그 트윈이 다음 틱에 이미
            // 파괴된 CanvasGroup을 건드려 여기서 엉뚱하게 실패로 잡힌다 — 픽스처 시작 전 방어적으로 정리.
            DG.Tweening.DOTween.KillAll();

            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/CharacterSelectScreen");
            Assert.IsNotNull(prefab, "CharacterSelectScreen 프리팹 없음 — SceneSetupM7UI.Run() 실행 필요");
            controller = Object.Instantiate(prefab, canvasGo.transform).GetComponent<CharacterSelectController>();

            confirmedRuns = new List<RunState>();
            controller.CharacterConfirmed += run => confirmedRuns.Add(run);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
        }

        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 7).Generate();
            return RunStateFactory.Create(map, new RunConfig());
        }

        [UnityTest]
        public IEnumerator Start_SpawnsOneIconPerCharacterDefinitionAndSelectsFirst()
        {
            yield return null;

            var icons = controller.GetComponentsInChildren<CharacterIconView>();
            Assert.AreEqual(4, icons.Length, "PlayerCharacterDefinition 4종 (§5.2.5) → 아이콘 4개");

            Text nameText = controller.transform.Find("InfoCard/NameText").GetComponent<Text>();
            Assert.AreEqual("독 캐릭터", nameText.text, "sortOrder 0번이 초기 선택돼야 함");
        }

        [UnityTest]
        public IEnumerator RightArrow_AdvancesToNextCharacter()
        {
            yield return null;

            controller.transform.Find("NavRow/RightArrowButton").GetComponent<Button>().onClick.Invoke();

            Text nameText = controller.transform.Find("InfoCard/NameText").GetComponent<Text>();
            Assert.AreEqual("전기 캐릭터", nameText.text);
        }

        [UnityTest]
        public IEnumerator LeftArrow_FromFirstCharacter_WrapsToLast()
        {
            yield return null;

            controller.transform.Find("NavRow/LeftArrowButton").GetComponent<Button>().onClick.Invoke();

            Text nameText = controller.transform.Find("InfoCard/NameText").GetComponent<Text>();
            Assert.AreEqual("회복 캐릭터", nameText.text, "좌우 화살표는 순환 이동해야 함");
        }

        [UnityTest]
        public IEnumerator IconClick_SelectsThatCharacterDirectly()
        {
            yield return null;

            var icons = controller.GetComponentsInChildren<CharacterIconView>();
            icons[2].GetComponent<Button>().onClick.Invoke();

            Text nameText = controller.transform.Find("InfoCard/NameText").GetComponent<Text>();
            Assert.AreEqual("강화 캐릭터", nameText.text);
        }

        [UnityTest]
        public IEnumerator ConfirmButton_SetsSelectedCharacterIdAndRaisesCharacterConfirmed()
        {
            RunState run = NewRun();
            controller.Begin(run);
            yield return null;

            controller.transform.Find("NavRow/RightArrowButton").GetComponent<Button>().onClick.Invoke(); // 캐릭터 2 선택
            controller.transform.Find("ConfirmButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1, confirmedRuns.Count);
            Assert.AreSame(run, confirmedRuns[0]);
            Assert.AreEqual("char_2", run.selectedCharacterId);
        }

        [UnityTest]
        public IEnumerator ConfirmButton_WithoutBegin_Throws()
        {
            yield return null;

            Assert.Throws<System.InvalidOperationException>(() =>
                controller.transform.Find("ConfirmButton").GetComponent<Button>().onClick.Invoke());
        }
    }
}
