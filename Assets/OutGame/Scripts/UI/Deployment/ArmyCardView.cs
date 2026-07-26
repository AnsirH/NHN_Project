using System;
using OutGame.Logic.Armies;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 군대 카드 — 목록/슬롯 어디에서든 표시되는 동일 인스턴스 (§5.7).
    /// 드래그 소스(자기 자신을 슬롯/목록으로 이동)이면서 동시에 아이템 드롭 타깃이다.
    /// </summary>
    public class ArmyCardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text soldierCountLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        private Canvas rootCanvas;
        private Transform dragOriginParent;
        private int dragOriginSiblingIndex;

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
            if (portrait == null || nameLabel == null || soldierCountLabel == null || canvasGroup == null)
                throw new InvalidOperationException("ArmyCardView 프리팹의 필드가 배선되지 않았습니다.");

            ArmyInstanceId = armyInstanceId;
            canvasGroup.blocksRaycasts = interactable;
        }

        /// <summary>soldierCount를 주면 "N명" 형식으로 표시하고, null이면 숨긴다(적 진영 카드는
        /// 병사 수를 표시하지 않는 게 의도된 동작 — ArmyDeploymentPanel.BuildEnemySlots 참고).</summary>
        public void SetDisplay(string displayName, Sprite portraitSprite, int? soldierCount = null)
        {
            nameLabel.text = displayName;
            if (portraitSprite != null) portrait.sprite = portraitSprite;
            soldierCountLabel.gameObject.SetActive(soldierCount.HasValue);
            soldierCountLabel.text = soldierCount.HasValue ? $"{soldierCount.Value}명" : string.Empty;
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
            canvasGroup.blocksRaycasts = false; // 드롭 타깃이 포인터 이벤트를 받도록
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

            // 유효한 드롭 타깃이 처리했다면 패널이 재배치할 것 — 처리 안 됐으면 원래 자리로 복귀
            if (transform.parent == rootCanvas.transform)
            {
                transform.SetParent(dragOriginParent, worldPositionStays: false);
                transform.SetSiblingIndex(dragOriginSiblingIndex);
                // worldPositionStays:false는 로컬 위치 "값"을 그대로 유지한다 — 그런데 그 값은 방금까지
                // OnDrag가 덮어쓴 "루트 캔버스 기준 마우스 좌표"라 원래 부모 스케일과 전혀 안 맞는다.
                // 명시적으로 리셋해야 카드가 원위치 중앙에 정확히 되돌아온다 (버그 수정).
                ((RectTransform)transform).anchoredPosition = Vector2.zero;
            }

            DragEnded?.Invoke(this);
        }

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
