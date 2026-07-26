using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Augments;
using OutGame.Logic.Items;
using OutGame.Logic.Rest;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI.Deployment;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 증원 방 패널 (§5.5 재정의, 2026-07-26 "휴식 방"에서 개칭 — 실제 효과가 회복이 아니라 병사 수
    /// 영구 증원이라 용어를 맞췄다): 보유 군대 중 1개 선택 → 병사 수 영구 +20% 증원.
    /// 증원 대상 선택은 배치 화면과 동일한 진영 그리드(AllyFormationView)를 재사용한다(2026-07-26
    /// 사용자 요청) — 카드를 누르면 그 즉시 증원된다(확인 절차 없음, 기존 목록 UX와 동일).
    /// </summary>
    public class RestPanel : MonoBehaviour
    {
        [SerializeField] private AllyFormationView allyFormationView;
        [SerializeField] private Text resultText;
        [SerializeField] private Button continueButton;

        private RunState run;
        private Dictionary<string, ArmyDefinition> armyDefsById;
        private Dictionary<string, ItemData> itemDataById;

        public event Action Completed;

        private void Awake()
        {
            if (allyFormationView == null || resultText == null || continueButton == null)
                throw new InvalidOperationException("RestPanel 프리팹의 필드가 배선되지 않았습니다.");

            // continueButton의 초기 비활성화는 Open()이 매번 담당한다(중복 X) — 이 패널의
            // GameObject는 비활성 상태로 시작하므로 Open()이 먼저 실행되고 SetActive(true)로
            // 끝나는데, Unity가 Awake()를 그 활성화 시점까지 미루면 여기서 다시 SetActive(false)를
            // 걸 경우 Open()이 방금 true로 켜둔 값(예: "증원할 부대가 없습니다" 안내 분기)을 뒤늦게
            // 덮어써버린다(이번 세션에 반복된 Awake 타이밍 문제와 동일 유형 — 실측으로 발견).
            continueButton.onClick.AddListener(OnContinueClicked);
            allyFormationView.ArmySelected += OnArmySelected;
        }

        private void OnDestroy()
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
            allyFormationView.ArmySelected -= OnArmySelected;
        }

        public void Open(
            RunState runState,
            RunConfig runConfig,
            IReadOnlyList<ArmyDefinition> armyDefs,
            IReadOnlyList<ItemDefinition> itemDefs,
            IReadOnlyList<AugmentDefinition> augmentDefs)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            if (runConfig == null) throw new ArgumentNullException(nameof(runConfig));
            if (armyDefs == null) throw new ArgumentNullException(nameof(armyDefs));
            if (itemDefs == null) throw new ArgumentNullException(nameof(itemDefs));
            if (augmentDefs == null) throw new ArgumentNullException(nameof(augmentDefs));

            run = runState;
            armyDefsById = armyDefs.ToDictionary(d => d.ToData().id);
            itemDataById = itemDefs.Select(d => d.ToData()).ToDictionary(d => d.id);

            resultText.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);

            // 선택 가능한 군대가 없으면 소프트락 방지 — 그리드를 열지 않고 바로 나갈 수 있게 한다.
            // 단순히 run.armies가 비었을 때뿐 아니라, 보유 군대 전부가 armyDefId 정의 누락으로 실제
            // 증원 불가능한 경우도 포함해야 한다(예전 목록 UI는 이런 군대를 아예 옵션에서 뺐지만,
            // AllyFormationView는 정의가 없어도 카드를 대체 이름으로 그려서 그냥 보여준다 — 클릭해도
            // OnArmySelected가 조용히 무시하므로, 이 조건이 없으면 진행 불가능한 그리드만 남는
            // 소프트락이 재현된다 — 코드 리뷰로 발견).
            bool hasReinforceableArmy = run.armies.Any(a => armyDefsById.ContainsKey(a.armyDefId));
            if (!hasReinforceableArmy)
            {
                allyFormationView.gameObject.SetActive(false);
                resultText.text = "증원할 수 있는 부대가 없습니다.";
                resultText.gameObject.SetActive(true);
                continueButton.gameObject.SetActive(true);
            }
            else
            {
                allyFormationView.gameObject.SetActive(true);
                allyFormationView.Open(run, runConfig, armyDefs, itemDefs, augmentDefs, selectionMode: true);
            }

            gameObject.SetActive(true);
            PanelTransitions.FadeIn(gameObject);
        }

        private void OnArmySelected(string armyInstanceId)
        {
            ArmyInstance army = run.GetArmy(armyInstanceId);
            if (army == null || !armyDefsById.TryGetValue(army.armyDefId, out ArmyDefinition def))
            {
                Debug.LogWarning($"[RestPanel] armyDefId에 대한 ArmyDefinition을 찾을 수 없어 증원할 수 없습니다: {armyInstanceId}");
                return;
            }

            var data = def.ToData();
            int added = RestService.Reinforce(army, data);
            string displayName = ItemEquipService.ResolveDisplayName(army, data.displayName, itemDataById);

            allyFormationView.Close();
            allyFormationView.gameObject.SetActive(false);

            resultText.text = $"{displayName} 부대에 {added}명이 증원되었습니다.";
            resultText.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);
        }

        private void OnContinueClicked()
        {
            allyFormationView.Close();
            gameObject.SetActive(false);
            Completed?.Invoke();
        }
    }
}
