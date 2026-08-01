using System.Collections;
using System.Linq;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OutGame.Tests.PlayMode
{
    /// <summary>
    /// PausePopup(2026-07-31, 아웃게임 전용) 스모크 테스트 — OutGame.unity에 직접 배선돼 있어(별도
    /// 프리팹 아님) 씬을 얹어서 검증한다. ESC/뒤로가기 키 입력 자체(새 Input System)는 시뮬레이션하지
    /// 않고, 그 결과인 Toggle()/Show()/Hide() 공개 API와 버튼 클릭만 검증한다.
    /// </summary>
    public class PausePopupPlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(SceneNames.OutGame);
        }

        private static IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.OutGame, LoadSceneMode.Additive);
            yield return null;
        }

        private static PausePopup FindPausePopup() =>
            Object.FindFirstObjectByType<PausePopup>(FindObjectsInactive.Include);

        [UnityTest]
        public IEnumerator Show_ActivatesPanelRoot()
        {
            yield return LoadScene();

            PausePopup pausePopup = FindPausePopup();
            Transform panelRoot = pausePopup.transform.Find("PanelRoot");
            Assert.IsFalse(panelRoot.gameObject.activeSelf, "초기에는 닫혀 있어야 함");

            pausePopup.Show();

            Assert.IsTrue(panelRoot.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Toggle_OpensThenClosesPanelRoot()
        {
            yield return LoadScene();

            PausePopup pausePopup = FindPausePopup();
            Transform panelRoot = pausePopup.transform.Find("PanelRoot");

            pausePopup.Toggle();
            Assert.IsTrue(panelRoot.gameObject.activeSelf, "닫혀 있었으면 토글 시 열려야 함");

            pausePopup.Toggle();
            Assert.IsFalse(panelRoot.gameObject.activeSelf, "열려 있었으면 토글 시 닫혀야 함(게임 복귀)");
        }

        [UnityTest]
        public IEnumerator SettingsButtonClick_OpensSettingsPopupOnTop()
        {
            yield return LoadScene();

            PausePopup pausePopup = FindPausePopup();
            pausePopup.Show();
            yield return null;

            SettingsPopup settingsPopup = pausePopup.GetComponentInChildren<SettingsPopup>(includeInactive: true);
            Assert.IsFalse(settingsPopup.gameObject.activeSelf);

            Button settingsButton = pausePopup.transform.Find("PanelRoot/Window/SettingsButton").GetComponent<Button>();
            settingsButton.onClick.Invoke();

            Assert.IsTrue(settingsPopup.gameObject.activeSelf, "환경 설정 버튼을 누르면 그 위에 환경설정 팝업이 떠야 함");
        }

        [UnityTest]
        public IEnumerator MainMenuButton_LoadsMainMenuScene()
        {
            yield return LoadScene();

            PausePopup pausePopup = FindPausePopup();
            pausePopup.Show();
            yield return null;

            Transform mainMenuButtonTransform = pausePopup.transform.Find("PanelRoot/Window/MainMenuButton");
            SceneLoadButton navButton = mainMenuButtonTransform.GetComponent<SceneLoadButton>();
            string requestedScene = null;
            navButton.LoadSceneAction = name => requestedScene = name;

            mainMenuButtonTransform.GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(SceneNames.MainMenu, requestedScene, "메인메뉴로 돌아가기는 MainMenu 씬을 요청해야 함");
        }
    }
}
