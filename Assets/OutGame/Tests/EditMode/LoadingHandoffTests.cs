using System;
using NUnit.Framework;
using OutGame.Flow;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// LoadingHandoff — OutGame → Loading → Battle 씬 전환 시 대상 씬 이름을 넘기는 static 홀더
    /// (2026-08-04). BattleBridge와 동일한 1회성 소비 계약을 검증한다.
    /// </summary>
    public class LoadingHandoffTests
    {
        [TearDown]
        public void TearDown() => LoadingHandoff.ResetToDefault(); // 테스트 간 정적 상태 오염 방지

        [Test]
        public void SetTarget_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => LoadingHandoff.SetTarget(null));
            Assert.Throws<ArgumentException>(() => LoadingHandoff.SetTarget(""));
            Assert.Throws<ArgumentException>(() => LoadingHandoff.SetTarget("   "));
        }

        [Test]
        public void ConsumeTarget_AfterSetTarget_ReturnsSameValue()
        {
            LoadingHandoff.SetTarget(SceneNames.Battle);

            Assert.AreEqual(SceneNames.Battle, LoadingHandoff.ConsumeTarget());
        }

        [Test]
        public void ConsumeTarget_CalledTwice_SecondCallReturnsNull()
        {
            LoadingHandoff.SetTarget(SceneNames.Battle);

            LoadingHandoff.ConsumeTarget();
            Assert.IsNull(LoadingHandoff.ConsumeTarget(), "한 번 꺼내가면 비워져야 함 — 다음 전환과 섞이면 안 됨");
        }

        [Test]
        public void ConsumeTarget_WithoutSetTarget_ReturnsNull()
        {
            Assert.IsNull(LoadingHandoff.ConsumeTarget());
        }

        [Test]
        public void ResetToDefault_ClearsPendingTarget()
        {
            LoadingHandoff.SetTarget(SceneNames.Battle);

            LoadingHandoff.ResetToDefault();

            Assert.IsNull(LoadingHandoff.ConsumeTarget());
        }
    }
}
