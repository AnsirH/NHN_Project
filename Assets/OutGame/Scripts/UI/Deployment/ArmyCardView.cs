using System;
using OutGame.Logic.Armies;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 군대 카드 — 목록/슬롯 어디에서든 표시되는 동일 인스턴스 (§5.7).
    /// 드래그 소스(자기 자신을 슬롯/목록으로 이동)이면서 동시에 아이템 드롭 타깃이다. 드래그 자체는
    /// ItemCardView와 공유하는 DraggableCardBase가 담당한다.
    /// </summary>
    public class ArmyCardView : DraggableCardBase, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text soldierCountLabel;

        public string ArmyInstanceId { get; private set; }

        /// <summary>드래그가 끝났을 때 항상 발행 — 패널이 드롭 성공 여부와 무관하게 레이아웃을 재계산한다.</summary>
        public event Action<ArmyCardView> DragEnded;

        /// <summary>아이템 카드가 이 군대 카드 위에 드롭됐을 때 발행.</summary>
        public event Action<ArmyCardView, string> ItemDropped;

        /// <summary>드래그 없이 카드를 클릭했을 때 발행 — 군대 정보 팝업(§5.7)을 연다.</summary>
        public event Action<ArmyCardView> Clicked;

        /// <summary>
        /// interactable=false면 드래그/클릭/아이템 드롭을 전부 비활성화한다 — 적 진영처럼 순수
        /// 표시 전용으로 같은 카드 UI를 재사용할 때 쓴다(2026-07-26). CanvasGroup.blocksRaycasts를
        /// 끄면 레이캐스트 자체가 이 카드를 건너뛰므로 각 이벤트 핸들러를 개별적으로 막을 필요가 없다.
        /// </summary>
        public void Initialize(string armyInstanceId, bool interactable = true)
        {
            if (string.IsNullOrEmpty(armyInstanceId))
                throw new ArgumentException("armyInstanceId가 비어 있습니다.", nameof(armyInstanceId));
            if (portrait == null || soldierCountLabel == null)
                throw new InvalidOperationException("ArmyCardView 프리팹의 필드가 배선되지 않았습니다.");
            ValidateCanvasGroupWired();

            ArmyInstanceId = armyInstanceId;
            canvasGroup.blocksRaycasts = interactable;
        }

        /// <summary>soldierCount를 주면 "N명" 형식으로 표시하고, null이면 숨긴다(적 진영 카드는
        /// 병사 수를 표시하지 않는 게 의도된 동작 — ArmyDeploymentPanel.BuildEnemySlots 참고). 카드에는
        /// 더 이상 군대 이름을 표시하지 않는다(2026-08-05 사용자 요청 — 유닛 수만 표시).</summary>
        public void SetDisplay(Sprite portraitSprite, int? soldierCount = null)
        {
            if (portraitSprite != null) portrait.sprite = portraitSprite;
            soldierCountLabel.gameObject.SetActive(soldierCount.HasValue);
            soldierCountLabel.text = soldierCount.HasValue ? $"{soldierCount.Value}명" : string.Empty;
        }

        protected override void OnDragEndedInternal() => DragEnded?.Invoke(this);

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null || !eventData.pointerDrag.TryGetComponent(out ItemCardView item))
                return;

            ItemDropped?.Invoke(this, item.ItemId);
        }

        // uGUI는 드래그 임계값을 넘으면 eventData.dragging=true로 클릭을 자동 억제한다 — 드래그 핸들러와
        // 별도 가드 없이 공존 가능.
        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);
    }
}
