using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// "선택지 여러 개 표시 → 선택 시 즉시 적용 → 결과 텍스트 → [계속] 복귀" 흐름을 공유하는
    /// 방 패널(EventPanel/AugmentPanel)의 공용 베이스 — 필드명까지 똑같이 중복 구현돼 있다가 한쪽
    /// 버그 수정이 다른 쪽에 반영 안 되는 사례가 있어 추출(코드 리뷰 HIGH 지적).
    /// </summary>
    public abstract class ChoicePanelBase : MonoBehaviour
    {
        [SerializeField] private RectTransform choiceContainer;
        [SerializeField] private Button choiceButtonPrefab;
        // 2026-08-10: UnityEngine.UI.Text → TMP_Text (EventPanel/AugmentPanel 공통).
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button continueButton;

        private readonly List<Button> spawnedChoiceButtons = new List<Button>();

        /// <summary>선택 완료 후 [계속] 클릭 시 발행 — 플로우가 맵 복귀를 처리한다.</summary>
        public event Action Completed;

        protected virtual void Awake()
        {
            if (choiceContainer == null || choiceButtonPrefab == null || resultText == null || continueButton == null)
                throw new InvalidOperationException($"{GetType().Name} 프리팹의 필드가 배선되지 않았습니다.");

            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.gameObject.SetActive(false);
        }

        protected virtual void OnDestroy() => continueButton.onClick.RemoveListener(OnContinueClicked);

        /// <summary>Open() 진입 시 결과 표시/이전 선택지를 지운다 — 하위 클래스가 자기 표시를 채우기 전에 호출.</summary>
        protected void ResetChoiceUI()
        {
            resultText.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            ClearChoiceButtons();
        }

        protected void SpawnChoiceButton(string label, Action onClick)
        {
            Button button = Instantiate(choiceButtonPrefab, choiceContainer);
            button.GetComponentInChildren<TMP_Text>().text = label;
            button.onClick.AddListener(() => onClick());
            spawnedChoiceButtons.Add(button);
        }

        /// <summary>선택지 버튼을 모두 숨기고 결과 텍스트+계속 버튼을 노출한다.</summary>
        protected void ShowResult(string text)
        {
            foreach (Button button in spawnedChoiceButtons)
                if (button != null) button.gameObject.SetActive(false);

            resultText.text = text;
            resultText.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);
        }

        private void OnContinueClicked()
        {
            gameObject.SetActive(false);
            Completed?.Invoke();
        }

        private void ClearChoiceButtons()
        {
            foreach (Button button in spawnedChoiceButtons)
                if (button != null) Destroy(button.gameObject);
            spawnedChoiceButtons.Clear();
        }
    }
}
