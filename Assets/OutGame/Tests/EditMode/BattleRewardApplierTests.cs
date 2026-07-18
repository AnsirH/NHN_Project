using System;
using NUnit.Framework;
using OutGame.Logic.Battle;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>전투 승리 보상 적용 검증 (§4-20/§9).</summary>
    public class BattleRewardApplierTests
    {
        private static RunState NewRun()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 9).Generate();
            return RunStateFactory.Create(map, new RunConfig { startingGold = 10 });
        }

        [Test]
        public void ApplyVictoryReward_AddsGoldToRun()
        {
            RunState run = NewRun();
            BattleRewardApplier.ApplyVictoryReward(run, 20);
            Assert.AreEqual(30, run.gold);
        }

        [Test]
        public void ApplyVictoryReward_ZeroGold_DoesNotThrow()
        {
            RunState run = NewRun();
            Assert.DoesNotThrow(() => BattleRewardApplier.ApplyVictoryReward(run, 0));
            Assert.AreEqual(10, run.gold);
        }

        [Test]
        public void ApplyVictoryReward_NullRun_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BattleRewardApplier.ApplyVictoryReward(null, 10));
        }

        [Test]
        public void ApplyVictoryReward_NegativeGold_Throws()
        {
            Assert.Throws<ArgumentException>(() => BattleRewardApplier.ApplyVictoryReward(NewRun(), -1));
        }
    }
}
