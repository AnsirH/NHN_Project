using NUnit.Framework;
using OutGame.UI.Deployment;
using UnityEngine;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 진영 슬롯 격자 전체를 레터박스 방식으로 균일 스케일하는 UniformScaleToFit의 순수 계산 검증
    /// (2026-08-07) — 셀마다 개별 반응하던 이전 방식 대신, 디자인 캔버스 하나를 통째로 스케일해서
    /// 슬롯 간격·커넥터 두께 비율이 화면비와 무관하게 항상 보존되도록 한다.
    /// </summary>
    public class UniformScaleToFitTests
    {
        [Test]
        public void ComputeUniformScale_WiderContainer_ScalesByHeight()
        {
            // 디자인보다 가로로 훨씬 넓은 컨테이너 — 세로가 더 좁은 제약이므로 세로 기준으로 스케일.
            float scale = UniformScaleToFit.ComputeUniformScale(
                availableSize: new Vector2(4000f, 500f), designSize: new Vector2(576f, 1008f));

            Assert.AreEqual(500f / 1008f, scale, 0.0001f);
        }

        [Test]
        public void ComputeUniformScale_TallerContainer_ScalesByWidth()
        {
            float scale = UniformScaleToFit.ComputeUniformScale(
                availableSize: new Vector2(300f, 4000f), designSize: new Vector2(576f, 1008f));

            Assert.AreEqual(300f / 576f, scale, 0.0001f);
        }

        [Test]
        public void ComputeUniformScale_ExactMatch_ReturnsOne()
        {
            float scale = UniformScaleToFit.ComputeUniformScale(
                availableSize: new Vector2(576f, 1008f), designSize: new Vector2(576f, 1008f));

            Assert.AreEqual(1f, scale, 0.0001f);
        }

        [Test]
        public void ComputeUniformScale_NeverDistortsAspectRatio()
        {
            // 회귀 방지 — x/y에 서로 다른 스케일을 반환하면 슬롯이 타원으로 찌그러진다.
            // 이 메서드는 항상 단일 float 하나만 반환하므로 애초에 왜곡이 불가능하다는 걸 문서화하는 목적.
            float scale = UniformScaleToFit.ComputeUniformScale(
                availableSize: new Vector2(1234f, 987f), designSize: new Vector2(576f, 1008f));

            Assert.Greater(scale, 0f);
        }

        [Test]
        public void ComputeUniformScale_ZeroAvailableSize_ReturnsOneInsteadOfNaN()
        {
            float scale = UniformScaleToFit.ComputeUniformScale(
                availableSize: Vector2.zero, designSize: new Vector2(576f, 1008f));

            Assert.AreEqual(1f, scale, 0.0001f);
        }
    }
}
