using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 루트 캔버스로 잠깐 옮겨졌다가 드롭 실패 시 원래 자리로 복귀하는 uGUI 드래그 카드의 공용 베이스
    /// (ArmyCardView/ItemCardView 공유) — 완전히 중복 구현되다 한쪽 버그 수정(anchoredPosition 리셋)이
    /// 다른 쪽에 "ArmyCardView.OnEndDrag와 동일한 버그"라는 주석과 함께 뒤늦게 수동 반영된 전적이 있어
    /// 추출(코드 리뷰 HIGH 지적).
    /// </summary>
    public abstract class DraggableCardBase : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] protected CanvasGroup canvasGroup;

        private Canvas rootCanvas;
        private Transform dragOriginParent;
        private int dragOriginSiblingIndex;

        protected virtual void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        protected void ValidateCanvasGroupWired()
        {
            if (canvasGroup == null)
                throw new InvalidOperationException($"{GetType().Name} 프리팹의 canvasGroup이 배선되지 않았습니다.");
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

            // 유효한 드롭 타깃이 처리했다면(파괴됐거나 재구성됐을 수 있음) 그쪽이 재배치할 것 —
            // 처리 안 됐으면 원래 자리로 복귀. this != null은 드롭 처리 중 이 오브젝트 자체가
            // 파괴된 경우(예: 아이템 카드가 부여 성공으로 즉시 재구성될 때)를 방어한다.
            if (this != null && transform.parent == rootCanvas.transform)
            {
                transform.SetParent(dragOriginParent, worldPositionStays: false);
                transform.SetSiblingIndex(dragOriginSiblingIndex);
                // worldPositionStays:false는 로컬 위치 "값"을 그대로 유지한다 — 그런데 그 값은 방금까지
                // OnDrag가 덮어쓴 "루트 캔버스 기준 마우스 좌표"라 원래 부모 스케일과 전혀 안 맞는다.
                // 명시적으로 리셋해야 카드가 원위치 중앙에 정확히 되돌아온다 (버그 수정, 두 카드 공통).
                ((RectTransform)transform).anchoredPosition = Vector2.zero;
            }

            OnDragEndedInternal();
        }

        /// <summary>드래그가 끝난 뒤(원위치 복귀 처리 후) 하위 클래스가 자기 이벤트를 발행하는 훅.</summary>
        protected virtual void OnDragEndedInternal() { }
    }
}
