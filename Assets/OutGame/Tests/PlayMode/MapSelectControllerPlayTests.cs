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
        private List<RunState> confirmedRuns;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/MapSelectScreen");
            Assert.IsNotNull(prefab, "MapSelectScreen 프리팹 없음 — SceneSetupM5UI.Run() 실행 필요");
            controller = Object.Instantiate(prefab, canvasGo.transform).GetComponent<MapSelectController>();

            confirmedRuns = new List<RunState>();
            controller.MapConfirmed += run => confirmedRuns.Add(run);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
        }

        [UnityTest]
        public IEnumerator Start_BindsOneSlotAndLocksTheRest()
        {
            yield return null;

            var mapPointNames = new[] { "MapPoint_1_SmallCastle", "MapPoint_2_CentralCastle", "MapPoint_3_Fortress" };
            var slotButtons = mapPointNames
                .Select(name => controller.transform.Find(name).GetComponent<Button>())
                .ToArray();

            Assert.AreEqual(3, slotButtons.Length, "지도 위 3지점이 모두 배치돼 있어야 함");
            Assert.AreEqual(1, slotButtons.Count(b => b.interactable),
                "MapDefinition_Default 1개 → 3지점 중 1개만 활성화 (§5.2: 1차는 맵 1개, 나머지는 잠금)");
        }

        [UnityTest]
        public IEnumerator SelectMap_RaisesMapConfirmedWithRunState()
        {
            yield return null;

            var mapPointNames = new[] { "MapPoint_1_SmallCastle", "MapPoint_2_CentralCastle", "MapPoint_3_Fortress" };
            var entryButton = mapPointNames
                .Select(name => controller.transform.Find(name).GetComponent<Button>())
                .First(b => b.interactable);
            entryButton.onClick.Invoke();

            // §3.1 씬 통합: 맵 확정은 씬 전환이 아니라 MapConfirmed 이벤트로 알린다 —
            // OutGameFlowController가 이 RunState를 캐릭터 선택 패널로 그대로 넘긴다.
            Assert.AreEqual(1, confirmedRuns.Count);
            RunState confirmed = confirmedRuns[0];
            Assert.IsNotEmpty(confirmed.mapState.nodes);
            Assert.IsNotEmpty(confirmed.armies);
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
