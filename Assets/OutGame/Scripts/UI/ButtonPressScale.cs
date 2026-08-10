using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 누르고 있는 동안 살짝 줄어드는 버튼 피드백 — 아무 UI 버튼에나 추가로 붙여서 쓴다.
    ///
    /// 2026-08-09: Unity Button의 내장 Transition으로는 스케일을 못 준다(ColorTint=색,
    /// SpriteSwap=이미지 교체, Animation=버튼마다 Animator+Controller+클립 5개 필요). 여러 버튼에
    /// 반복 적용할 거라 에셋이 증식하지 않는 추가형 컴포넌트로 만들었다(사용자 확정).
    /// Selectable을 상속해 DoStateTransition을 오버라이드하면 상태 머신을 물려받을 수 있지만
    /// 기존 프리팹의 Button을 교체해야 해서 onClick 배선이 날아간다 — 그래서 상속이 아니라 추가형.
    ///
    /// 상속을 포기한 대신 Selectable이 해주던 상태 처리를 직접 한다:
    /// 누른 채 밖으로 끌고 나가는 경우(OnPointerExit), 눌린 채 꺼지는 경우(OnDisable),
    /// 누르는 도중 비활성화되는 경우(Update의 interactable 확인).
    /// </summary>
    public class ButtonPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("프리팹 배선 (비우면 자기 자신)")]
        [SerializeField] private RectTransform target;

        [Header("연출 (인스펙터 튜닝)")]
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.94f;
        [SerializeField, Range(1f, 40f)] private float responseSpeed = 18f;

        private Selectable selectable;
        private Vector3 baseScale;
        private bool pressed;

        private void Awake()
        {
            if (target == null) target = (RectTransform)transform;
            selectable = GetComponent<Selectable>(); // 없어도 동작한다 — 그때는 interactable 확인만 생략
            baseScale = target.localScale;           // 프리팹마다 1이 아닐 수 있어 하드코딩하지 않는다
        }

        private void OnDisable()
        {
            // 눌린 상태로 패널이 꺼지면 다음에 켤 때 작아진 채로 나온다.
            pressed = false;
            if (target != null) target.localScale = baseScale;
        }

        private void Update()
        {
            if (pressed && selectable != null && !selectable.IsInteractable())
                pressed = false; // 누르는 도중 비활성화된 경우

            Vector3 goal = pressed ? baseScale * pressedScale : baseScale;
            if (target.localScale == goal) return; // 정지 상태에서는 매 프레임 아무 것도 하지 않는다

            // 프레임률에 좌우되지 않는 지수 감쇠. timeScale=0인 팝업 위에서도 동작해야 하므로 unscaled.
            float t = 1f - Mathf.Exp(-responseSpeed * Time.unscaledDeltaTime);
            Vector3 next = Vector3.Lerp(target.localScale, goal, t);
            if ((next - goal).sqrMagnitude < 1e-7f) next = goal; // 수렴만 하고 안 끝나는 것 방지
            target.localScale = next;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.IsInteractable()) return;
            pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        public void OnPointerExit(PointerEventData eventData) => pressed = false;
    }
}
