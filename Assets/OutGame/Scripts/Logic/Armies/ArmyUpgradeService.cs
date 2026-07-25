using System;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 군대 업그레이드 (§4-26): 재화를 소비해 군대 1개를 0~+5단계로 강화한다. 실행할 때마다
    /// 해당 군대의 장군·유닛 체력/공격력/방어력이 함께 상승한다. 장군 경험치 시스템을 대체한다(§4-24).
    /// </summary>
    public static class ArmyUpgradeService
    {
        public const float StatBonusPerLevel = 0.1f; // 레벨당 +10% (초안 — 밸런스 튜닝 전)

        /// <summary>다음 단계로 업그레이드할 때 필요한 재화. 이미 최대 단계면 예외.</summary>
        public static int NextUpgradeCost(ArmyInstance army, RunConfig config)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (army.upgradeLevel >= ArmyInstance.MaxUpgradeLevel)
                throw new InvalidOperationException(
                    $"부대 {army.instanceId}는 이미 최대 업그레이드 단계({ArmyInstance.MaxUpgradeLevel})입니다.");

            return config.armyUpgradeCosts[army.upgradeLevel];
        }

        public static bool CanUpgrade(ArmyInstance army, RunState run, RunConfig config)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (config == null) throw new ArgumentNullException(nameof(config));

            if (army.upgradeLevel >= ArmyInstance.MaxUpgradeLevel) return false;
            return run.gold >= NextUpgradeCost(army, config);
        }

        /// <summary>업그레이드 실행 — 재화 차감 후 단계 상승. 불가 시 InvalidOperationException.</summary>
        public static void Upgrade(ArmyInstance army, RunState run, RunConfig config)
        {
            if (!CanUpgrade(army, run, config))
                throw new InvalidOperationException(
                    $"부대 {army?.instanceId}를 업그레이드할 수 없습니다 (최대 단계이거나 재화 부족).");

            int cost = NextUpgradeCost(army, config);
            run.gold -= cost;
            army.IncrementUpgradeLevel();
        }

        /// <summary>레벨당 +10% 가산 배율 (초안, §4-26) — 장군/유닛 체력·공격력·방어력에 곱해 쓴다.</summary>
        public static float GetStatMultiplier(int upgradeLevel) => 1f + upgradeLevel * StatBonusPerLevel;
    }
}
