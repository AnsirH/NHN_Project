using System;
using System.Collections.Generic;
using OutGame.Logic.Events;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 이벤트 방 패널 (§5.4): 일러스트+본문+선택지 → 선택 시 보상 적용 → 결과 텍스트 → 복귀.
    /// </summary>
    public class EventPanel : MonoBehaviour
    {
        [SerializeField] private Image illustrationImage;
        [SerializeField] private Text bodyText;
        [SerializeField] private RectTransform choiceContainer;
        [SerializeField] private Button choiceButtonPrefab;
        [SerializeField] private Text resultText;
        [SerializeField] private Button continueButton;

        private readonly List<Button> spawnedChoiceButtons = new List<Button>();
        private RunState run;

        /// <summary>선택 완료 후 [계속] 클릭 시 발행 — 플로우가 맵 복귀를 처리한다.</summary>
        public event Action Completed;

        private void Awake()
        {
            if (illustrationImage == null || bodyText == null || choiceContainer == null
                || choiceButtonPrefab == null || resultText == null || continueButton == null)
                throw new InvalidOperationException("EventPanel 프리팹의 필드가 배선되지 않았습니다.");

            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.gameObject.SetActive(false);
        }

        private void OnDestroy() => continueButton.onClick.RemoveListener(OnContinueClicked);

        public void Open(EventDefinition definition, RunState runState)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (runState == null) throw new ArgumentNullException(nameof(runState));

            run = runState;
            EventData data = definition.ToData(); // 콘텐츠 결함(선택지 부족 등)은 여기서 즉시 드러남

            illustrationImage.gameObject.SetActive(definition.Illustration != null);
            if (definition.Illustration != null) illustrationImage.sprite = definition.Illustration;
            bodyText.text = data.bodyText;

            resultText.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            ClearChoiceButtons();

            foreach (EventChoiceData choice in data.choices)
            {
                Button button = Instantiate(choiceButtonPrefab, choiceContainer);
                button.GetComponentInChildren<Text>().text = choice.choiceText;
                button.onClick.AddListener(() => OnChoiceSelected(choice));
                spawnedChoiceButtons.Add(button);
            }

            gameObject.SetActive(true);
        }

        private void OnChoiceSelected(EventChoiceData choice)
        {
            EventRewardApplier.Apply(run, choice.rewards);

            foreach (Button button in spawnedChoiceButtons)
                if (button != null) button.gameObject.SetActive(false);

            resultText.text = choice.resultText;
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
