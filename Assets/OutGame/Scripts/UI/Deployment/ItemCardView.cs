using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 인벤토리 팝업의 아이템 카드 — 드래그해서 군대 카드 위에 놓으면 부여 시도 (§5.6, §5.7).
    /// 드롭 실패 시 원래 자리(인벤토리)로 복귀.
    /// </summary>
    public class ItemCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        private Canvas rootCanvas;
        private Transform dragOriginParent;
        private int dragOriginSiblingIndex;

        public string ItemId { get; private set; }

        public void Initialize(string itemId, string displayName, Sprite iconSprite)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("itemId가 비어 있습니다.", nameof(itemId));
            if (icon == null || nameLabel == null || canvasGroup == null)
                throw new InvalidOperationException("ItemCardView 프리팹의 필드가 배선되지 않았습니다.");

            ItemId = itemId;
            nameLabel.text = displayName;
            if (iconSprite != null) icon.sprite = iconSprite;
        }

        private void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragOriginParent = transform.parent;
            dragOriginSiblingIndex = transform.GetSiblingIndex();

            transform.SetParent(rootCanvas.transform, worldPositionStays: true);
            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.85f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            // 부여 성공 시 팝업 쪽에서 이 카드를 파괴/재구성 — 실패(또는 미확정) 시 복귀
            if (this != null && transform.parent == rootCanvas.transform)
            {
                transform.SetParent(dragOriginParent, worldPositionStays: false);
                transform.SetSiblingIndex(dragOriginSiblingIndex);
                // ArmyCardView.OnEndDrag와 동일한 버그 — worldPositionStays:false는 드래그 중이던
                // "루트 캔버스 기준 마우스 좌표" 값을 그대로 유지해 원래 부모 스케일과 어긋난다.
                ((RectTransform)transform).anchoredPosition = Vector2.zero;
            }
        }
    }
}
