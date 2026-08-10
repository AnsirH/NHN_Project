using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 인벤토리 팝업의 아이템 카드 — 드래그해서 군대 카드 위에 놓으면 부여 시도 (§5.6, §5.7).
    /// 드롭 실패 시 원래 자리(인벤토리)로 복귀. 드래그 자체는 ArmyCardView와 공유하는
    /// DraggableCardBase가 담당한다.
    /// </summary>
    public class ItemCardView : DraggableCardBase
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;

        public string ItemId { get; private set; }

        public void Initialize(string itemId, string displayName, Sprite iconSprite)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("itemId가 비어 있습니다.", nameof(itemId));
            if (icon == null || nameLabel == null)
                throw new InvalidOperationException("ItemCardView 프리팹의 필드가 배선되지 않았습니다.");
            ValidateCanvasGroupWired();

            ItemId = itemId;
            nameLabel.text = displayName;
            if (iconSprite != null) icon.sprite = iconSprite;
        }
    }
}
