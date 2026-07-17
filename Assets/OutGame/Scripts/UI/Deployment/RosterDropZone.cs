using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 보유 군대 목록 영역 — 슬롯에서 이 영역으로 드래그하면 배치 해제된다 (§5.7).
    /// </summary>
    public class RosterDropZone : MonoBehaviour, IDropHandler
    {
        /// <summary>슬롯에 있던 군대가 목록으로 돌아왔을 때 발행.</summary>
        public event Action<string> ArmyReturned;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null || !eventData.pointerDrag.TryGetComponent(out ArmyCardView card))
                return;

            ArmyReturned?.Invoke(card.ArmyInstanceId);
        }
    }
}
