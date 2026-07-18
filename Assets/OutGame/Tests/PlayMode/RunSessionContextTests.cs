using System;
using NUnit.Framework;
using OutGame.Flow;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.PlayMode
{
    /// <summary>씬 전환 간 RunState 전달 보관소 검증 (§3.1 흐름).</summary>
    public class RunSessionContextTests
    {
        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 3).Generate();
            return RunStateFactory.Create(map, new RunConfig());
        }

        [TearDown]
        public void TearDown() => RunSessionContext.ConsumePendingRun(); // 정적 상태 정리 — 테스트 간 오염 방지

        [Test]
        public void SetThenConsume_ReturnsRunAndClears()
        {
            RunState run = NewRun();
            RunSessionContext.SetPendingRun(run);

            Assert.AreSame(run, RunSessionContext.ConsumePendingRun());
            Assert.IsNull(RunSessionContext.ConsumePendingRun(), "한 번 소비하면 다시 null이어야 함");
        }

        [Test]
        public void SetPendingRun_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RunSessionContext.SetPendingRun(null));
        }

        [Test]
        public void ConsumePendingRun_WhenEmpty_ReturnsNull()
        {
            Assert.IsNull(RunSessionContext.ConsumePendingRun());
        }
    }
}
