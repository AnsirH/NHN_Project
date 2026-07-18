using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
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
        private string tempSavePath;
        private List<string> loadedScenes;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = Resources.Load<GameObject>("OutGame/MainMenuScreen");
            Assert.IsNotNull(prefab, "MainMenuScreen 프리팹 없음 — SceneSetupM5UI.Run() 실행 필요");
            controller = Object.Instantiate(prefab, canvasGo.transform).GetComponent<MainMenuController>();

            loadedScenes = new List<string>();
            controller.LoadSceneAction = name => loadedScenes.Add(name);

            tempSavePath = Path.Combine(Path.GetTempPath(), $"mainmenu_test_{System.Guid.NewGuid():N}.json");
            controller.SetSavePath(tempSavePath);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvasGo);
            if (File.Exists(tempSavePath)) File.Delete(tempSavePath);
        }

        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 5).Generate();
            return RunStateFactory.Create(map, new RunConfig());
        }

        [UnityTest]
        public IEnumerator NoSaveFile_ContinueButtonIsNotInteractable()
        {
            yield return null;

            Button continueButton = controller.transform.Find("ContinueButton").GetComponent<Button>();
            Assert.IsFalse(continueButton.interactable);
        }

        [UnityTest]
        public IEnumerator SetSavePath_WithExistingSave_MakesContinueButtonInteractable()
        {
            RunSaveService.Save(NewRun(), tempSavePath);
            controller.SetSavePath(tempSavePath);
            yield return null;

            Button continueButton = controller.transform.Find("ContinueButton").GetComponent<Button>();
            Assert.IsTrue(continueButton.interactable);
        }

        [UnityTest]
        public IEnumerator StartButton_LoadsMapSelectScene()
        {
            yield return null;

            controller.transform.Find("StartButton").GetComponent<Button>().onClick.Invoke();

            CollectionAssert.AreEqual(new[] { SceneNames.MapSelect }, loadedScenes);
        }

        [UnityTest]
        public IEnumerator ContinueButton_WithValidSave_SetsPendingRunAndLoadsInGame()
        {
            RunState saved = NewRun();
            saved.gold = 77;
            RunSaveService.Save(saved, tempSavePath);
            controller.SetSavePath(tempSavePath);
            yield return null;

            controller.transform.Find("ContinueButton").GetComponent<Button>().onClick.Invoke();

            CollectionAssert.AreEqual(new[] { SceneNames.InGame }, loadedScenes);
            RunState pending = RunSessionContext.ConsumePendingRun();
            Assert.IsNotNull(pending);
            Assert.AreEqual(77, pending.gold);
        }

        [UnityTest]
        public IEnumerator ContinueButton_WithCorruptSave_ShowsMessageAndDoesNotLoadScene()
        {
            File.WriteAllText(tempSavePath, "not valid json");
            controller.SetSavePath(tempSavePath);
            yield return null;

            controller.transform.Find("ContinueButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsEmpty(loadedScenes, "손상된 저장 파일이면 씬 전환이 없어야 함");
            Text messageText = controller.transform.Find("MessageText").GetComponent<Text>();
            Assert.IsTrue(messageText.gameObject.activeSelf);
            Assert.IsFalse(File.Exists(tempSavePath), "손상된 저장 파일은 정리되어야 함");
        }
    }
}
