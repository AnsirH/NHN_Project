using System;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 런 종료 화면(클리어/패배) 패널. M6부터 전투/보스 방 진입은 ArmyDeploymentPanel이 담당하므로
    /// 이 패널은 더 이상 방 콘텐츠 자리 표시가 아니라 런 종료 알림 전용이다.
    /// </summary>
    public class DummyRoomPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button completeButton;

        /// <summary>[완료] 클릭 시 발행 — 플로우가 메인 메뉴 복귀를 처리한다.</summary>
        public event Action Completed;

        private void Awake()
        {
            if (titleText == null || bodyText == null || completeButton == null)
                throw new InvalidOperationException(
                    "DummyRoomPanel의 titleText/bodyText/completeButton이 배선되지 않았습니다 — 프리팹 구성 확인");

            completeButton.onClick.AddListener(OnCompleteClicked);
        }

        public void ShowRunClear()
        {
            titleText.text = "런 클리어!";
            bodyText.text = "보스를 물리쳤습니다. 메인 메뉴로 돌아갑니다.";
            gameObject.SetActive(true);
            PanelTransitions.FadeIn(gameObject);
        }

        public void ShowDefeat()
        {
            titleText.text = "패배";
            bodyText.text = "부대가 전멸했습니다. 런이 종료됩니다.";
            gameObject.SetActive(true);
            PanelTransitions.FadeIn(gameObject);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnCompleteClicked()
        {
            Hide();
            Completed?.Invoke();
        }
    }
}
