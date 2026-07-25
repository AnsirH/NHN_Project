using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Augments;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 증강 방 패널 (§5.6, §4-27): pool에서 3개를 무작위 노출 → 선택 시 즉시 런 전체 적용 → 결과 표시 → 복귀.
    /// EventPanel과 동일한 선택→적용→결과→계속 구조를 따르되, 소스가 고정 선택지가 아니라
    /// AugmentPoolService.PickRandomThree()가 매 방문마다 새로 뽑는 pool이라는 점만 다르다.
    /// </summary>
    public class AugmentPanel : MonoBehaviour
    {
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
            if (choiceContainer == null || choiceButtonPrefab == null || resultText == null || continueButton == null)
                throw new InvalidOperationException("AugmentPanel 프리팹의 필드가 배선되지 않았습니다.");

            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.gameObject.SetActive(false);
        }

        private void OnDestroy() => continueButton.onClick.RemoveListener(OnContinueClicked);

        public void Open(RunState runState, IReadOnlyList<AugmentDefinition> pool, System.Random rng)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            run = runState;

            resultText.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            ClearChoiceButtons();

            List<AugmentData> poolData = pool.Select(p => p.ToData()).ToList(); // 콘텐츠 결함은 여기서 즉시 드러남
            List<AugmentData> options = AugmentPoolService.PickRandomThree(poolData, rng);

            foreach (AugmentData option in options)
            {
                Button button = Instantiate(choiceButtonPrefab, choiceContainer);
                button.GetComponentInChildren<Text>().text = $"{option.displayName}\n{option.description}";
                button.onClick.AddListener(() => OnAugmentSelected(option));
                spawnedChoiceButtons.Add(button);
            }

            gameObject.SetActive(true);
            PanelTransitions.FadeIn(gameObject);
        }

        private void OnAugmentSelected(AugmentData augment)
        {
            AugmentApplier.Apply(run, augment);

            foreach (Button button in spawnedChoiceButtons)
                if (button != null) button.gameObject.SetActive(false);

            resultText.text = $"'{augment.displayName}'를 선택했습니다.";
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
