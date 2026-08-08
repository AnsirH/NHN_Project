using System;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 런 종료 화면(클리어/패배) 패널. M6부터 전투/보스 방 진입은 ArmyDeploymentPanel이 담당하므로
    /// 이 패널은 더 이상 방 콘텐츠 자리 표시가 아니라 런 종료 알림 전용이다.
    /// 2026-08-08: 클래스명(DummyRoomPanel)이 그 사실을 안 따라가고 있어 RunResultPanel로 개명
    /// (Docs/OutGame/화면 명칭 정리.md).
    /// </summary>
    public class RunResultPanel : MonoBehaviour
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
                    "RunResultPanel의 titleText/bodyText/completeButton이 배선되지 않았습니다 — 프리팹 구성 확인");

            completeButton.onClick.AddListener(OnCompleteClicked);
        }

        public void ShowRunClear() => Show("런 클리어!",
            "쓰러진 군세 사이, 낡은 도구 하나가 유난히 매만진 자국을 남기고 있다. 누군가 이 부대를 오래 돌보았다는 사실만은, 부정할 수 없다.");

        public void ShowDefeat() => Show("패배", "부대가 전멸했습니다. 런이 종료됩니다.");

        private void Show(string title, string body)
        {
            titleText.text = title;
            bodyText.text = body;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnCompleteClicked()
        {
            Hide();
            Completed?.Invoke();
        }
    }
}
