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
using TMPro;
using OutGame.ScriptableObjects;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// CharacterSelectPanel 프리팹 스모크 테스트 — 프리팹은 SceneSetupM7UI.Run()으로 생성돼 있어야
    /// 하고, PlayerCharacterDefinition_Char1~4.asset(SceneSetupM7Data.Run())이 있어야 한다 (§5.2.5).
    /// </summary>
    public class CharacterSelectPanelPlayTests
    {
        private GameObject canvasGo;
        private CharacterSelectPanel controller;
        private List<RunState> confirmedRuns;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/Panels/CharacterSelectPanel");
            Assert.IsNotNull(prefab, "CharacterSelectPanel 프리팹 없음 — SceneSetupM7UI.Run() 실행 필요");
            controller = Object.Instantiate(prefab, canvasGo.transform).GetComponent<CharacterSelectPanel>();

            confirmedRuns = new List<RunState>();
            controller.CharacterConfirmed += run => confirmedRuns.Add(run);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
        }

        private static PlayerCharacterDefinition[] Definitions() =>
            Resources.LoadAll<PlayerCharacterDefinition>("OutGame/Data/Characters").OrderBy(c => c.SortOrder).ToArray();

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
            Assert.AreEqual(Definitions().Length, icons.Length, "PlayerCharacterDefinition 4종 (§5.2.5) → 아이콘 4개");

            TMP_Text nameText = PrefabBinding.Get<TMP_Text>(controller, "nameText");
            Assert.AreEqual(Definitions()[0].DisplayName, nameText.text, "sortOrder 0번이 초기 선택돼야 함");
        }

        [UnityTest]
        public IEnumerator RightArrow_AdvancesToNextCharacter()
        {
            yield return null;

            PrefabBinding.Get<Button>(controller, "rightArrowButton").onClick.Invoke();

            TMP_Text nameText = PrefabBinding.Get<TMP_Text>(controller, "nameText");
            Assert.AreEqual(Definitions()[1].DisplayName, nameText.text);
        }

        [UnityTest]
        public IEnumerator LeftArrow_FromFirstCharacter_WrapsToLast()
        {
            yield return null;

            PrefabBinding.Get<Button>(controller, "leftArrowButton").onClick.Invoke();

            TMP_Text nameText = PrefabBinding.Get<TMP_Text>(controller, "nameText");
            Assert.AreEqual(Definitions().Last().DisplayName, nameText.text, "좌우 화살표는 순환 이동해야 함");
        }

        [UnityTest]
        public IEnumerator IconClick_SelectsThatCharacterDirectly()
        {
            yield return null;

            var icons = controller.GetComponentsInChildren<CharacterIconView>();
            icons[2].GetComponent<Button>().onClick.Invoke();

            TMP_Text nameText = PrefabBinding.Get<TMP_Text>(controller, "nameText");
            Assert.AreEqual(Definitions()[2].DisplayName, nameText.text);
        }

        [UnityTest]
        public IEnumerator ConfirmButton_SetsSelectedCharacterIdAndRaisesCharacterConfirmed()
        {
            RunState run = NewRun();
            controller.Begin(run);
            yield return null;

            PrefabBinding.Get<Button>(controller, "rightArrowButton").onClick.Invoke(); // 캐릭터 2 선택
            PrefabBinding.Get<Button>(controller, "confirmButton").onClick.Invoke();

            Assert.AreEqual(1, confirmedRuns.Count);
            Assert.AreSame(run, confirmedRuns[0]);
            Assert.AreEqual(Definitions()[1].ToData().id, run.selectedCharacterId);
        }

        [UnityTest]
        public IEnumerator ConfirmButton_WithoutBegin_Throws()
        {
            yield return null;

            Assert.Throws<System.InvalidOperationException>(() =>
                PrefabBinding.Get<Button>(controller, "confirmButton").onClick.Invoke());
        }
    }
}
