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
    /// 사용자 요청) — 카드를 누르면 그 즉시 증원된다(확인 절차 없음, 기존 목록 UX와 동일). 아이템
    /// 장착은 이 화면과 무관해 아이템 버튼 없이 연다. 증원 결과는 진영 그리드를 그대로 보여준 채
    /// 그 위에 dim 팝업(resultPopupRoot, PopupDim)으로 띄운다(사용자 확정).
    /// </summary>
    public class RestPanel : MonoBehaviour
    {
        [SerializeField] private AllyFormationView allyFormationView;
        [SerializeField] private GameObject resultPopupRoot;
        [SerializeField] private Text resultText;
        [SerializeField] private Button continueButton;

        private RunState run;
        private Dictionary<string, ArmyDefinition> armyDefsById;
        private Dictionary<string, ItemData> itemDataById;

        public event Action Completed;

        private void Awake()
        {
            if (allyFormationView == null || resultPopupRoot == null || resultText == null || continueButton == null)
                throw new InvalidOperationException("RestPanel 프리팹의 필드가 배선되지 않았습니다.");

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

            resultPopupRoot.SetActive(false);

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
                ShowResult("증원할 수 있는 부대가 없습니다.");
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
            // 결과 팝업이 이미 떠 있으면(예: dim의 raycast 차단이 씬/프리팹 설정 실수로 풀린 경우)
            // 무시한다 — 그리드를 그대로 보여주는 채로 두기로 해서(사용자 확정) 카드가 여전히
            // 인터랙션 가능한 상태로 남아있는데, 이게 두 번째 클릭까지 막아주는 유일한 안전장치가
            // 되면 안 된다(코드 리뷰 지적 — 영구 스탯 변경인 RestService.Reinforce가 중복 적용될
            // 위험).
            if (resultPopupRoot.activeSelf) return;

            ArmyInstance army = run.GetArmy(armyInstanceId);
            if (army == null || !armyDefsById.TryGetValue(army.armyDefId, out ArmyDefinition def))
            {
                Debug.LogWarning($"[RestPanel] armyDefId에 대한 ArmyDefinition을 찾을 수 없어 증원할 수 없습니다: {armyInstanceId}");
                return;
            }

            var data = def.ToData();
            int added = RestService.Reinforce(army, data);
            string displayName = ItemEquipService.ResolveDisplayName(army, data.displayName, itemDataById);

            // 진영 그리드는 그대로 보여준 채(사용자 확정) 그 위에 결과 팝업만 띄운다 — 그리드를
            // 숨기지 않는다.
            allyFormationView.Close();
            ShowResult($"{displayName} 부대에 {added}명이 증원되었습니다.");
        }

        private void ShowResult(string message)
        {
            resultText.text = message;
            resultPopupRoot.SetActive(true);
        }

        private void OnContinueClicked()
        {
            allyFormationView.Close();
            gameObject.SetActive(false);
            Completed?.Invoke();
        }
    }
}
