using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OutGame.Flow;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// MainMenuScreen 프리팹 스모크 테스트 — 프리팹은 SceneSetupM5UI.Run()으로 생성돼 있어야 한다.
    /// </summary>
    public class MainMenuControllerPlayTests
    {
        private GameObject canvasGo;
        private MainMenuController controller;
        private List<string> loadedScenes;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/Panels/MainMenuScreen");
            Assert.IsNotNull(prefab, "MainMenuScreen 프리팹 없음 — SceneSetupM5UI.Run() 실행 필요");
            controller = Object.Instantiate(prefab, canvasGo.transform).GetComponent<MainMenuController>();

            loadedScenes = new List<string>();
            controller.LoadSceneAction = name => loadedScenes.Add(name);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(canvasGo);

        [UnityTest]
        public IEnumerator StartButton_LoadsOutGameScene()
        {
            yield return null;

            controller.transform.Find("StartButton").GetComponent<Button>().onClick.Invoke();

            CollectionAssert.AreEqual(new[] { SceneNames.OutGame }, loadedScenes);
        }
    }
}
