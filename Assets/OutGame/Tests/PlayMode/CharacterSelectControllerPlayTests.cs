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
        private List<string> loadedScenes;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/CharacterSelectScreen");
            Assert.IsNotNull(prefab, "CharacterSelectScreen 프리팹 없음 — SceneSetupM7UI.Run() 실행 필요");
            controller = Object.Instantiate(prefab, canvasGo.transform).GetComponent<CharacterSelectController>();

            loadedScenes = new List<string>();
            controller.LoadSceneAction = name => loadedScenes.Add(name);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
            RunSessionContext.ConsumePendingRun(); // 정적 상태 정리
        }

        private static RunState NewPendingRun()
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
            Assert.AreEqual("캐릭터 1", nameText.text, "sortOrder 0번(캐릭터 1)이 초기 선택돼야 함");
        }

        [UnityTest]
        public IEnumerator RightArrow_AdvancesToNextCharacter()
        {
            yield return null;

            controller.transform.Find("NavRow/RightArrowButton").GetComponent<Button>().onClick.Invoke();

            Text nameText = controller.transform.Find("InfoCard/NameText").GetComponent<Text>();
            Assert.AreEqual("캐릭터 2", nameText.text);
        }

        [UnityTest]
        public IEnumerator LeftArrow_FromFirstCharacter_WrapsToLast()
        {
            yield return null;

            controller.transform.Find("NavRow/LeftArrowButton").GetComponent<Button>().onClick.Invoke();

            Text nameText = controller.transform.Find("InfoCard/NameText").GetComponent<Text>();
            Assert.AreEqual("캐릭터 4", nameText.text, "좌우 화살표는 순환 이동해야 함");
        }

        [UnityTest]
        public IEnumerator IconClick_SelectsThatCharacterDirectly()
        {
            yield return null;

            var icons = controller.GetComponentsInChildren<CharacterIconView>();
            icons[2].GetComponent<Button>().onClick.Invoke();

            Text nameText = controller.transform.Find("InfoCard/NameText").GetComponent<Text>();
            Assert.AreEqual("캐릭터 3", nameText.text);
        }

        [UnityTest]
        public IEnumerator ConfirmButton_SetsPendingRunSelectedCharacterIdAndLoadsInGame()
        {
            RunSessionContext.SetPendingRun(NewPendingRun());
            yield return null;

            controller.transform.Find("NavRow/RightArrowButton").GetComponent<Button>().onClick.Invoke(); // 캐릭터 2 선택
            controller.transform.Find("ConfirmButton").GetComponent<Button>().onClick.Invoke();

            CollectionAssert.AreEqual(new[] { SceneNames.InGame }, loadedScenes);
            Assert.AreEqual("char_2", RunSessionContext.PendingRun.selectedCharacterId);
        }
    }
}
