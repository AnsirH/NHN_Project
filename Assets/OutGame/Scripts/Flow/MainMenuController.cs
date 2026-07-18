using System;
using OutGame.Logic.Runs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OutGame.Flow
{
    /// <summary>메인 메뉴 (§5.1): 게임 시작 / 이어하기(저장 런 있을 때만 활성) / 종료.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text messageText;

        /// <summary>테스트에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private string savePath;

        private void Awake()
        {
            if (startButton == null || continueButton == null || quitButton == null)
                throw new InvalidOperationException("MainMenuController의 startButton/continueButton/quitButton이 배선되지 않았습니다.");

            savePath = RunSaveService.DefaultPath; // Application.persistentDataPath는 필드 초기화식에서 호출 불가 (Awake/Start에서만 허용)

            startButton.onClick.AddListener(OnStartClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            quitButton.onClick.AddListener(OnQuitClicked);

            if (messageText != null) messageText.gameObject.SetActive(false);
        }

        private void Start() => RefreshContinueButton();

        private void OnDestroy()
        {
            startButton.onClick.RemoveListener(OnStartClicked);
            continueButton.onClick.RemoveListener(OnContinueClicked);
            quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        /// <summary>저장 경로를 대체하고 이어하기 버튼 상태를 갱신한다 (테스트/툴링용).</summary>
        public void SetSavePath(string path)
        {
            savePath = path;
            RefreshContinueButton();
        }

        private void RefreshContinueButton() =>
            continueButton.interactable = RunSaveService.HasSave(savePath);

        private void OnStartClicked() => LoadSceneAction(SceneNames.MapSelect);

        private void OnContinueClicked()
        {
            RunState run = RunSaveService.Load(savePath);
            if (run == null)
            {
                RunSaveService.DeleteSave(savePath); // 손상된 저장 파일 정리
                RefreshContinueButton();
                ShowMessage("저장 데이터를 불러올 수 없습니다.");
                return;
            }

            RunSessionContext.SetPendingRun(run);
            LoadSceneAction(SceneNames.InGame);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowMessage(string text)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.gameObject.SetActive(true);
        }
    }
}
