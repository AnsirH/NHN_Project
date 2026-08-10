using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 배치 슬롯 1칸 (§5.7) — ArmyCardView의 드롭 타깃. 실제 배치 판정은 패널이 한다.
    /// </summary>
    public class DeploySlotView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private GameObject emptyIndicator; // 빈 슬롯 '+' (아군 — 배치 가능 표시)
        // 2026-08-10: 적 진영의 빈 슬롯은 '+'가 아니라 'x'로 표기한다(사용자 확정) — 배치가 불가능한
        // 자리라는 뜻이라, 아군의 "여기 놓을 수 있음"과 같은 기호를 쓰면 안 된다.
        [SerializeField] private GameObject enemyIndicator;
        [SerializeField] private int slotId; // 2026-08-07: 런타임 주입 대신 프리팹에 직접 구워 넣는다(에디터 가시성 요구사항).

        private bool isEnemySlot;

        public int SlotId => slotId;
        public RectTransform CardContainer => cardContainer;

        /// <summary>군대 카드가 이 슬롯에 드롭됐을 때 발행 (armyInstanceId, slotId).</summary>
        public event Action<string, int> ArmyDropped;

        /// <summary>아이템이 카드 밖 슬롯 여백에 드롭됐을 때, 슬롯에 배치된 카드로 위임 발행 (아래 참고).</summary>
        public event Action<ArmyCardView, string> ItemDroppedOnOccupant;

        private void Awake()
        {
            if (background == null || cardContainer == null || emptyIndicator == null || enemyIndicator == null)
                throw new InvalidOperationException("DeploySlotView 프리팹의 필드가 배선되지 않았습니다.");
        }

        /// <summary>이 슬롯이 적 진영인지 지정한다 — 빈 슬롯에 '+'(아군)를 띄울지 'x'(적)를 띄울지
        /// 가른다. FormationGridView.Initialize()가 격자 단위로 한 번에 지정한다.</summary>
        public void SetEnemySlot(bool value)
        {
            isEnemySlot = value;
            RefreshIndicators(!emptyIndicator.activeSelf && !enemyIndicator.activeSelf);
        }

        /// <summary>occupied=false면 빈 슬롯 표시를 보여준다 — 아군은 '+', 적은 'x'.</summary>
        public void SetOccupied(bool occupied) => RefreshIndicators(occupied);

        private void RefreshIndicators(bool occupied)
        {
            emptyIndicator.SetActive(!occupied && !isEnemySlot);
            enemyIndicator.SetActive(!occupied && isEnemySlot);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;

            if (eventData.pointerDrag.TryGetComponent(out ArmyCardView card))
            {
                ArmyDropped?.Invoke(card.ArmyInstanceId, SlotId);
                return;
            }

            // 슬롯 칸(cellSize)이 카드 고정 크기보다 커서(4×7 확장, §5.7) 카드 주위에 빈 여백이 생긴다 —
            // 그 여백에 아이템을 드롭해도 카드에 드롭한 것과 동일하게 처리해야 한다.
            if (eventData.pointerDrag.TryGetComponent(out ItemCardView item))
            {
                ArmyCardView occupant = cardContainer.GetComponentInChildren<ArmyCardView>();
                if (occupant != null) ItemDroppedOnOccupant?.Invoke(occupant, item.ItemId);
            }
        }
    }
}
