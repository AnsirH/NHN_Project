using DG.Tweening;
using UnityEngine;

namespace OutGame.UI
{
    /// <summary>패널 오픈 연출 (§5.8 — 1차 수준: 정적 패널 + DOTween 페이드).</summary>
    public static class PanelTransitions
    {
        private const float FadeInDuration = 0.25f;

        /// <summary>
        /// CanvasGroup이 없으면 추가하고, alpha 0→1로 페이드 인한다.
        /// CanvasGroup.DOFade 확장 메서드(DOTweenModuleUI.cs)는 asmdef 기반 커스텀 어셈블리에서
        /// 보이지 않을 수 있어(루즈 스크립트라 자동 참조 대상이 아님), 코어 DLL에 있는
        /// DOTween.To를 직접 사용한다 — 동작은 동일하다.
        /// </summary>
        public static void FadeIn(GameObject panel)
        {
            if (panel == null) return;

            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();

            DOTween.Kill(group); // 연속으로 다시 열릴 때 이전 트윈이 남아있지 않게
            group.alpha = 0f;
            DOTween.To(() => group.alpha, x => group.alpha = x, 1f, FadeInDuration)
                .SetTarget(group);
        }
    }
}
