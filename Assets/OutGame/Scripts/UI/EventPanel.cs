using System;
using OutGame.Logic.Events;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 이벤트 방 패널 (§5.4): 일러스트+본문+선택지 → 선택 시 보상 적용 → 결과 텍스트 → 복귀.
    /// 선택→적용→결과→계속 흐름 자체는 ChoicePanelBase가 담당 — 여기서는 일러스트/본문 표시와
    /// EventRewardApplier 연동만 다룬다.
    /// </summary>
    public class EventPanel : ChoicePanelBase
    {
        [SerializeField] private Image illustrationImage;
        // 2026-08-10: UnityEngine.UI.Text → TMP_Text.
        [SerializeField] private TMP_Text bodyText;

        private RunState run;
        private int maxArmyCount;

        protected override void Awake()
        {
            base.Awake();
            if (illustrationImage == null || bodyText == null)
                throw new InvalidOperationException("EventPanel 프리팹의 필드가 배선되지 않았습니다.");
        }

        public void Open(EventDefinition definition, RunState runState, int maxArmyCountValue)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (runState == null) throw new ArgumentNullException(nameof(runState));

            run = runState;
            maxArmyCount = maxArmyCountValue;
            EventData data = definition.ToData(); // 콘텐츠 결함(선택지 부족 등)은 여기서 즉시 드러남

            // 2026-08-10: SetActive가 아니라 enabled를 토글한다. illustrationImage가 붙은 Window는
            // 본문·선택지·계속 버튼을 모두 담은 부모라, 일러스트가 없다고 GameObject를 끄면 창 전체가
            // 사라진다(일러스트 미배정 상태에서 이벤트 방에 들어가면 아무것도 안 보이던 원인).
            illustrationImage.enabled = definition.Illustration != null;
            if (definition.Illustration != null) illustrationImage.sprite = definition.Illustration;
            bodyText.text = data.bodyText;

            ResetChoiceUI();

            foreach (EventChoiceData choice in data.choices)
                SpawnChoiceButton(choice.choiceText, () => OnChoiceSelected(choice));

            gameObject.SetActive(true);
        }

        private void OnChoiceSelected(EventChoiceData choice)
        {
            var skipped = EventRewardApplier.Apply(run, choice.rewards, maxArmyCount);

            string resultMessage = choice.resultText;
            if (skipped.Count > 0)
                resultMessage += "\n(군대 슬롯이 가득 차 있어 일부 보상을 받지 못했습니다.)"; // §4-7
            ShowResult(resultMessage);
        }
    }
}
