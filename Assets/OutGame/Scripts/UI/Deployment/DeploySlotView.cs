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

        public void Initialize(int slotId)
        {
            if (background == null || cardContainer == null)
                throw new InvalidOperationException("DeploySlotView 프리팹의 필드가 배선되지 않았습니다.");

            SlotId = slotId;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null || !eventData.pointerDrag.TryGetComponent(out ArmyCardView card))
                return;

            ArmyDropped?.Invoke(card.ArmyInstanceId, SlotId);
        }
    }
}
