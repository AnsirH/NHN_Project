using System;
using NUnit.Framework;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>RunConfig 경계값 검증 (§4-13, §4-20).</summary>
    public class RunConfigTests
    {
        [Test]
        public void Validate_Defaults_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new RunConfig().Validate());
        }

        [Test]
        public void Validate_NegativeBattleVictoryGold_Throws()
        {
            var config = new RunConfig { battleVictoryGold = -1 };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_ZeroBattleVictoryGold_DoesNotThrow()
        {
            var config = new RunConfig { battleVictoryGold = 0 };
            Assert.DoesNotThrow(() => config.Validate());
        }

        [Test]
        public void Validate_MaxArmyCountBelowOne_Throws()
        {
            var config = new RunConfig { maxArmyCount = 0 };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_StartingArmyCountExceedsMaxArmyCount_Throws()
        {
            var config = new RunConfig { startingArmyCount = 5, maxArmyCount = 3 };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_StartingArmyCountEqualsMaxArmyCount_DoesNotThrow()
        {
            var config = new RunConfig { startingArmyCount = 3, maxArmyCount = 3 };
            Assert.DoesNotThrow(() => config.Validate());
        }
    }
}
