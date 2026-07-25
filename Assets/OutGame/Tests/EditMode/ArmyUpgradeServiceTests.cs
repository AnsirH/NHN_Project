using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 군대 업그레이드 검증 (§4-26): 재화 소비, 0~+5단계, 레벨당 +10% 스탯 배율.
    /// </summary>
    public class ArmyUpgradeServiceTests
    {
        private RunState run;
        private RunConfig config;
        private ArmyInstance army;

        [SetUp]
        public void SetUp()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), seed: 1).Generate();
            config = new RunConfig { startingArmyCount = 1, startingGold = 2000 }; // 5단계 총합(1250) 이상
            run = RunStateFactory.Create(map, config);
            army = run.armies[0];
        }

        [Test]
        public void CanUpgrade_BelowMaxLevelWithEnoughGold_ReturnsTrue()
        {
            Assert.IsTrue(ArmyUpgradeService.CanUpgrade(army, run, config));
        }

        [Test]
        public void CanUpgrade_InsufficientGold_ReturnsFalse()
        {
            run.gold = 0;
            Assert.IsFalse(ArmyUpgradeService.CanUpgrade(army, run, config));
        }

        [Test]
        public void CanUpgrade_AtMaxLevel_ReturnsFalse()
        {
            for (int i = 0; i < ArmyInstance.MaxUpgradeLevel; i++)
                ArmyUpgradeService.Upgrade(army, run, config);

            Assert.AreEqual(ArmyInstance.MaxUpgradeLevel, army.upgradeLevel);
            Assert.IsFalse(ArmyUpgradeService.CanUpgrade(army, run, config));
        }

        [Test]
        public void Upgrade_DeductsGoldAndIncrementsLevel()
        {
            int costForFirstLevel = config.armyUpgradeCosts[0];
            int goldBefore = run.gold;

            ArmyUpgradeService.Upgrade(army, run, config);

            Assert.AreEqual(1, army.upgradeLevel);
            Assert.AreEqual(goldBefore - costForFirstLevel, run.gold);
        }

        [Test]
        public void Upgrade_WhenCannotAfford_Throws()
        {
            run.gold = 0;
            Assert.Throws<System.InvalidOperationException>(() => ArmyUpgradeService.Upgrade(army, run, config));
            Assert.AreEqual(0, army.upgradeLevel, "실패 시 레벨 변화 없음");
        }

        [Test]
        public void Upgrade_AtMaxLevel_Throws()
        {
            for (int i = 0; i < ArmyInstance.MaxUpgradeLevel; i++)
                ArmyUpgradeService.Upgrade(army, run, config);

            Assert.Throws<System.InvalidOperationException>(() => ArmyUpgradeService.Upgrade(army, run, config));
        }

        [Test]
        public void GetStatMultiplier_ScalesLinearlyByTenPercentPerLevel()
        {
            Assert.AreEqual(1.0f, ArmyUpgradeService.GetStatMultiplier(0), 1e-3f);
            Assert.AreEqual(1.3f, ArmyUpgradeService.GetStatMultiplier(3), 1e-3f);
            Assert.AreEqual(1.5f, ArmyUpgradeService.GetStatMultiplier(ArmyInstance.MaxUpgradeLevel), 1e-3f);
        }
    }
}
