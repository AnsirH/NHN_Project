using System;
using OutGame.Logic.Maps;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// M2 더미 방 패널 — 방 진입 연출 자리 표시. M3~M4에서 배치 UI/이벤트 패널로 대체된다.
    /// </summary>
    public class DummyRoomPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button completeButton;

        /// <summary>[완료] 클릭 시 발행 — 플로우가 맵 복귀를 처리한다.</summary>
        public event Action Completed;

        private void Awake()
        {
            if (titleText == null || bodyText == null || completeButton == null)
                throw new InvalidOperationException(
                    "DummyRoomPanel의 titleText/bodyText/completeButton이 배선되지 않았습니다 — 프리팹 구성 확인");

            completeButton.onClick.AddListener(OnCompleteClicked);
        }

        public void Show(MapNode node, RoomTypeVisualSet visuals)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            titleText.text = visuals != null ? visuals.GetName(node.roomType) : node.roomType.ToString();
            bodyText.text = $"{node.id}\n(더미 방 — M3에서 배치 UI, M4에서 이벤트 패널로 대체)";
            gameObject.SetActive(true);
        }

        public void ShowRunClear()
        {
            titleText.text = "런 클리어!";
            bodyText.text = "보스를 물리쳤습니다. 메인 메뉴로 돌아갑니다.";
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
