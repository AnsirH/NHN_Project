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
        [SerializeField] private Image portrait;         // 마스크 안 — 카드 안에 갇히는 몸통
        [SerializeField] private Image portraitOverflow; // 마스크 밖 상단 밴드 — 테두리를 넘는 상체
        [SerializeField] private TMP_Text soldierCountLabel;

        // 2026-08-10: 하체는 카드 안에서 잘리고 상체만 테두리 위로 살짝 넘치는 연출(사용자 확정).
        //
        // 초상화를 위쪽 기준으로 정렬하고 카드보다 크게 그린다 — 넘치는 아래쪽(허리·하체)은 마스크가
        // 잘라낸다. 좌우는 마스크가 카드 폭 그대로라 옆 슬롯을 절대 침범하지 않는다. 위로만
        // portraitTopOverflow 만큼 별도 밴드로 통과시킨다.
        //
        // 같은 스프라이트를 두 번 그리는 이유: 테두리를 넘으려면 초상화가 테두리보다 나중에
        // 그려져야 하는데, 한 장으로 그러면 몸통까지 테두리를 덮는다. portrait은 테두리 아래,
        // portraitOverflow는 테두리 위에 두고 상단 밴드만 보이게 한다.
        //
        // 원본 비가 제각각이라(1024x1024 vs 1536x1024) 고정 rect에 넣으면 넓은 그림만 눌린다.
        // 높이는 portraitHeightScale로 통일하고 폭만 원본 비율을 따르게 해 왜곡을 없앤다.
        [SerializeField, Range(1f, 2.5f)] private float portraitHeightScale = 1.55f;
        [SerializeField, Range(0f, 40f)] private float portraitTopOverflow = 12f;

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
            if (portrait == null || portraitOverflow == null || soldierCountLabel == null)
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
            if (portraitSprite != null)
            {
                portrait.sprite = portraitSprite;
                portraitOverflow.sprite = portraitSprite; // 몸통과 같은 그림이어야 목에서 이어진다
                ResizePortraitToAspect(portraitSprite);
            }
            soldierCountLabel.gameObject.SetActive(soldierCount.HasValue);
            soldierCountLabel.text = soldierCount.HasValue ? $"{soldierCount.Value}명" : string.Empty;
        }

        /// <summary>초상화 두 장(몸통/상체)의 rect를 스프라이트 비율에 맞춰 다시 잡는다.
        /// 높이는 카드 기준으로 통일하고 폭만 원본 비율을 따라가 어떤 유닛도 왜곡되지 않는다.
        ///
        /// 위쪽 기준 정렬이 핵심이다 — 머리 위치를 고정하고 넘치는 아래쪽(하체)을 마스크가 잘라내는
        /// 구조라, 확대해도 머리는 제자리에 있고 하체만 더 잘린다.
        ///
        /// 두 장은 부모(마스크)가 다르지만 각자 부모의 위쪽 끝이 같은 높이(카드 상단 + 넘침)라,
        /// 같은 크기·같은 정렬을 주면 화면상 절대 위치가 정확히 일치한다 — 목에서 그림이 어긋나지
        /// 않는 이유.</summary>
        private void ResizePortraitToAspect(Sprite sprite)
        {
            float height = ((RectTransform)transform).rect.height * portraitHeightScale;
            var size = new Vector2(height * (sprite.rect.width / sprite.rect.height), height);

            Apply((RectTransform)portrait.transform, size, portraitTopOverflow);
            Apply((RectTransform)portraitOverflow.transform, size, 0f);
        }

        private static void Apply(RectTransform rect, Vector2 size, float topOffset)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, topOffset);
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
