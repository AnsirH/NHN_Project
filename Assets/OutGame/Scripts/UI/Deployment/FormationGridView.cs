using System;
using System.Collections.Generic;
using OutGame.Logic.Battle;
using OutGame.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 진영 슬롯 격자(원형 슬롯 + 연결 트랙 + 전투력 라벨)의 공용 뼈대 — 아군(AllyFormationView)과
    /// 적(ArmyDeploymentPanel) 양쪽이 완전히 동일한 형태를 쓰므로 하나의 프리팹(FormationGridPanel)으로
    /// 통합해 둘 다 중첩 인스턴스로 재사용한다(2026-08-07 사용자 지적).
    ///
    /// 슬롯/커넥터는 더 이상 런타임에 Instantiate하지 않는다 — FormationGridPanelGenerator(에디터
    /// 도구)가 BattleFieldConfig의 rows/columns를 바탕으로 프리팹에 미리 구워 넣는다(에디터에서 보이지
    /// 않던 문제 + GridLayoutGroup 고정 Cell Size의 해상도 미대응 문제, 둘 다 사용자 지적). 이 컴포넌트는
    /// 그렇게 미리 존재하는 슬롯들에 이벤트를 전달하고 전투력 라벨을 갱신하는 역할만 한다.
    /// 상호작용(드래그앤드롭/아이템/자동배치) 자체는 다루지 않는다 — 호스트(AllyFormationView)가
    /// 이 컴포넌트의 이벤트를 구독해서 처리한다(적 진영은 애초에 상호작용이 없다).
    ///
    /// 2026-08-08: <see cref="slots"/>는 런타임에 slotArea.GetComponentsInChildren로 매번 훑어
    /// 찾던 것을 인스펙터(프리팹)에 미리 배선해두는 배열로 바꿨다(사용자 지적) — FormationGridPanelGenerator가
    /// 슬롯을 생성하는 그 순간 이 배열도 같이 채워 넣는다.
    /// </summary>
    public class FormationGridView : MonoBehaviour
    {
        [SerializeField] private DeploySlotView[] slots; // 인스펙터(프리팹)에 미리 배선된 슬롯 전체
        [SerializeField] private TMP_Text powerLabel; // 2026-08-10: UnityEngine.UI.Text → TMP_Text
        [SerializeField] private BattleFieldConfig fieldConfig; // 슬롯 수 검증 전용(재생성 누락 감지)

        private readonly Dictionary<int, DeploySlotView> slotViewsById = new Dictionary<int, DeploySlotView>();
        private bool slotsWired;

        public IReadOnlyDictionary<int, DeploySlotView> SlotViewsById => slotViewsById;

        /// <summary>슬롯에 군대 카드가 드롭됐을 때(armyInstanceId, slotId) — 개별 DeploySlotView의
        /// 동명 이벤트를 그대로 전달(bubble)한다.</summary>
        public event Action<string, int> ArmyDropped;

        /// <summary>아이템이 슬롯 여백(카드 밖)에 드롭됐을 때, 슬롯에 배치된 카드로 위임 전달.</summary>
        public event Action<ArmyCardView, string> ItemDroppedOnOccupant;

        private void Awake() => WireSlotsIfNeeded();

        /// <summary>슬롯 배선(딕셔너리 채우기 + 이벤트 전달 구독)은 Awake()에만 기대지 않고 여기서
        /// 명시적으로도 보장한다 — 이 컴포넌트가 속한 계층이 비활성 상태에서(예: ArmyFormationPopup은
        /// 평소 숨겨져 있다가 Open()으로 열리는 팝업이라 프리팹 루트 자체가 비활성으로 저장돼 있다)
        /// Open()이 먼저 호출되면 Unity가 Awake()를 활성화 시점(Open() 끝의 SetActive(true))까지
        /// 미루므로, Initialize()가 SlotViewsById를 읽으려는 시점엔 아직 비어있을 수 있다(이번 세션에
        /// 이미 여러 번 겪은 것과 동일한 Awake 타이밍 문제 — 업그레이드 버튼 라벨, CurrencyDisplay 등).
        /// slotsWired 플래그로 중복 실행(이벤트 이중 구독)은 막는다 — Awake는 원래 한 번만 돌지만,
        /// Initialize()는 Open()마다 반복 호출되므로 가드가 없으면 두 번째 Open()부터 어긋난다.</summary>
        /// <param name="enemySide">true면 빈 슬롯을 'x'로 표기한다(적 진영 — 배치 불가).
        /// false(기본)면 '+'로 표기한다(아군 — 배치 가능). 2026-08-10 사용자 확정.</param>
        public void Initialize(bool enemySide = false)
        {
            WireSlotsIfNeeded();
            foreach (DeploySlotView slot in slotViewsById.Values) slot.SetEnemySlot(enemySide);
        }

        private void WireSlotsIfNeeded()
        {
            if (slotsWired) return;
            slotsWired = true;

            if (slots == null || slots.Length == 0 || powerLabel == null || fieldConfig == null)
                throw new InvalidOperationException("FormationGridView의 구조 참조가 배선되지 않았습니다.");

            foreach (DeploySlotView slot in slots)
            {
                slotViewsById[slot.SlotId] = slot;
                slot.ArmyDropped += (armyInstanceId, slotId) => ArmyDropped?.Invoke(armyInstanceId, slotId);
                slot.ItemDroppedOnOccupant += (card, itemId) => ItemDroppedOnOccupant?.Invoke(card, itemId);
            }

            BattleFieldConfigData fieldData = fieldConfig.ToData();
            int expectedCount = fieldData.rows * fieldData.columns;
            if (slots.Length != expectedCount)
                throw new InvalidOperationException(
                    $"FormationGridPanel에 배선된 슬롯 수({slots.Length})가 BattleFieldConfig" +
                    $"({fieldData.rows}×{fieldData.columns}={expectedCount})와 맞지 않습니다 — " +
                    "BattleFieldConfig의 rows/columns가 바뀌었다면 FormationGridPanelGenerator를 다시 실행하세요.");
        }

        public void SetPower(float power) => powerLabel.text = $"전투력: {power:0}";
    }
}
