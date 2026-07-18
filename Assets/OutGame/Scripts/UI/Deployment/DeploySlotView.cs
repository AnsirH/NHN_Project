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

        public int SlotId { get; private set; }
        public RectTransform CardContainer => cardContainer;

        /// <summary>군대 카드가 이 슬롯에 드롭됐을 때 발행 (armyInstanceId, slotId).</summary>
        public event Action<string, int> ArmyDropped;

        /// <summary>아이템이 카드 밖 슬롯 여백에 드롭됐을 때, 슬롯에 배치된 카드로 위임 발행 (아래 참고).</summary>
        public event Action<ArmyCardView, string> ItemDroppedOnOccupant;

        public void Initialize(int slotId)
        {
            if (background == null || cardContainer == null)
                throw new InvalidOperationException("DeploySlotView 프리팹의 필드가 배선되지 않았습니다.");

            SlotId = slotId;
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
