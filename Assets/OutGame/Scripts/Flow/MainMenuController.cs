using System;
using OutGame.Logic.Audio;
using OutGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OutGame.Flow
{
    /// <summary>메인 메뉴 (§5.1): 게임 시작 / 환경 설정 / 종료.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private SettingsPopup settingsPopup;

        /// <summary>테스트에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private void Awake()
        {
            if (startButton == null || settingsButton == null || quitButton == null || settingsPopup == null)
                throw new InvalidOperationException(
                    "MainMenuController의 startButton/settingsButton/quitButton/settingsPopup이 배선되지 않았습니다.");

            // 앱을 새로 시작한 직후엔 AudioListener.volume이 항상 기본값(1)이라, 저장된 볼륨을 여기서
            // 한 번 적용해야 한다 — 메인 메뉴가 사실상 유일한 진입 씬이라 이 한 곳으로 충분하다.
            AudioListener.volume = SoundSettings.MasterVolume;

            startButton.onClick.AddListener(OnStartClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnDestroy()
        {
            startButton.onClick.RemoveListener(OnStartClicked);
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        private void OnStartClicked() => LoadSceneAction(SceneNames.OutGame);

        private void OnSettingsClicked() => settingsPopup.Show();

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
