using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Runs;
using OutGame.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// MapSelectScreen 프리팹 스모크 테스트 — 프리팹은 SceneSetupM5UI.Run()으로 생성돼 있어야 하고,
    /// MapDefinition_Default.asset(SceneSetupM5Data.Run())이 있어야 한다.
    /// </summary>
    public class MapSelectControllerPlayTests
    {
        private GameObject canvasGo;
        private MapSelectController controller;
        private List<string> loadedScenes;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/MapSelectScreen");
            Assert.IsNotNull(prefab, "MapSelectScreen 프리팹 없음 — SceneSetupM5UI.Run() 실행 필요");
            controller = Object.Instantiate(prefab, canvasGo.transform).GetComponent<MapSelectController>();

            loadedScenes = new List<string>();
            controller.LoadSceneAction = name => loadedScenes.Add(name);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
            RunSessionContext.ConsumePendingRun(); // 정적 상태 정리
        }

        [UnityTest]
        public IEnumerator Start_SpawnsOneEntryPerMapDefinition()
        {
            yield return null;

            var entries = controller.GetComponentsInChildren<Button>()
                .Where(b => b.transform.parent.name == "Content").ToArray();
            Assert.AreEqual(1, entries.Length, "MapDefinition_Default 1개 → 항목 1개 (§5.2: 1차는 맵 1개)");
        }

        [UnityTest]
        public IEnumerator SelectMap_SetsPendingRunAndLoadsCharacterSelect()
        {
            yield return null;

            var entryButton = controller.GetComponentsInChildren<Button>()
                .First(b => b.transform.parent.name == "Content");
            entryButton.onClick.Invoke();

            // §5.2.5: 맵 확정 다음은 인게임이 아니라 캐릭터 선택 화면 — 캐릭터 선택 화면이 확정된
            // RunState에 selectedCharacterId를 채운 뒤 인게임으로 넘긴다.
            CollectionAssert.AreEqual(new[] { SceneNames.CharacterSelect }, loadedScenes);
            RunState pending = RunSessionContext.ConsumePendingRun();
            Assert.IsNotNull(pending);
            Assert.IsNotEmpty(pending.mapState.nodes);
            Assert.IsNotEmpty(pending.armies);
        }

        [UnityTest]
        public IEnumerator BackButton_LoadsMainMenu()
        {
            yield return null;

            var backButton = controller.transform.Find("BackButton").GetComponent<Button>();
            var navButton = backButton.GetComponent<SceneLoadButton>();
            var backLoadedScenes = new List<string>();
            navButton.LoadSceneAction = name => backLoadedScenes.Add(name);

            backButton.onClick.Invoke();

            CollectionAssert.AreEqual(new[] { SceneNames.MainMenu }, backLoadedScenes);
        }
    }
}
