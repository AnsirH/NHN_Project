using UnityEngine;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 고정 디자인 크기의 자식(<see cref="target"/>)을 이 컴포넌트가 붙은 RectTransform(=실제로 화면
    /// 크기에 따라 늘어나는 뷰포트) 안에 항상 온전히 들어가도록 가로세로 "동일 비율"로만 스케일한다
    /// (레터박스 방식, 2026-08-07 — 참고 이미지처럼 슬롯 간격·연결선 두께가 화면비와 무관하게 항상 같은
    /// 상대 비율을 유지해야 해서, 셀마다 따로 스트레치시키는 대신 격자 전체를 통째로 스케일한다).
    ///
    /// <paramref name="target"/>은 자기 자신의 크기(sizeDelta)를 <see cref="designSize"/>로 고정해 두고
    /// 중앙 앵커/피벗을 쓴다 — 이 컴포넌트는 그 크기를 바꾸지 않고 오직 localScale만 조절한다. 가로세로
    /// 중 더 좁게 맞는 축 기준으로 스케일하므로(Mathf.Min) 뷰포트보다 커지는 일은 없다.
    /// </summary>
    public class UniformScaleToFit : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 designSize;

        private RectTransform selfRect;

        private void Awake() => selfRect = (RectTransform)transform;

        private void OnEnable() => Rescale();

        // 부모(뷰포트) 크기가 바뀌면 이 RectTransform 자신의 rect도 함께 바뀐다(스트레치 앵커 전제) —
        // Unity가 그때마다 이 메시지를 보내준다(AspectRatioFitter도 같은 방식으로 반응).
        private void OnRectTransformDimensionsChange() => Rescale();

        private void Rescale()
        {
            if (target == null || selfRect == null) return;
            float scale = ComputeUniformScale(selfRect.rect.size, designSize);
            target.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>순수 계산만 분리 — MonoBehaviour 없이도 EditMode 테스트에서 검증 가능하도록.</summary>
        public static float ComputeUniformScale(Vector2 availableSize, Vector2 designSize)
        {
            if (designSize.x <= 0f || designSize.y <= 0f) return 1f;
            if (availableSize.x <= 0f || availableSize.y <= 0f) return 1f;
            return Mathf.Min(availableSize.x / designSize.x, availableSize.y / designSize.y);
        }
    }
}
