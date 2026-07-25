using System;
using System.Linq;

namespace OutGame.Logic.Runs
{
    /// <summary>
    /// 런 시작 구성 (§4-13: 기본 군대만 지급, 개수는 config).
    /// </summary>
    [Serializable]
    public class RunConfig
    {
        public string startingArmyDefId = "army_basic";
        public int startingArmyCount = 3;
        public int startingGold = 0;
        public int battleVictoryGold = 20; // §4-20/§9: 전투 승리 시 재화 획득 — 수치는 밸런스 튜닝 전 초안

        // §4-7(2026-07-19 개정): 군대 보유 상한 = 배치 슬롯 수. BattleFieldConfig 기본(4×7=28)과
        // 일치해야 하지만 서로 다른 에셋이라 자동 동기화되지 않는다 — 슬롯 수를 바꾸면 같이 조정할 것.
        public int maxArmyCount = 28;

        // §4-26(2026-07-19): 군대 업그레이드 단계별 재화 비용 — 길이는 ArmyInstance.MaxUpgradeLevel과
        // 일치해야 한다(인덱스 0 = +1단계 비용). 초안값, 밸런스 튜닝 전.
        public int[] armyUpgradeCosts = { 50, 100, 200, 350, 550 };

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(startingArmyDefId))
                throw new ArgumentException("startingArmyDefId가 비어 있습니다.");
            if (startingArmyCount < 1)
                throw new ArgumentException($"startingArmyCount는 1 이상이어야 합니다. 현재: {startingArmyCount}");
            if (startingGold < 0)
                throw new ArgumentException($"startingGold는 0 이상이어야 합니다. 현재: {startingGold}");
            if (battleVictoryGold < 0)
                throw new ArgumentException($"battleVictoryGold는 0 이상이어야 합니다. 현재: {battleVictoryGold}");
            if (maxArmyCount < 1)
                throw new ArgumentException($"maxArmyCount는 1 이상이어야 합니다. 현재: {maxArmyCount}");
            if (startingArmyCount > maxArmyCount)
                throw new ArgumentException(
                    $"startingArmyCount({startingArmyCount})가 maxArmyCount({maxArmyCount})를 초과할 수 없습니다.");
            if (armyUpgradeCosts == null || armyUpgradeCosts.Length != ArmyInstance.MaxUpgradeLevel)
                throw new ArgumentException(
                    $"armyUpgradeCosts는 길이 {ArmyInstance.MaxUpgradeLevel}이어야 합니다. " +
                    $"현재: {armyUpgradeCosts?.Length.ToString() ?? "null"}");
            if (armyUpgradeCosts.Any(cost => cost < 0))
                throw new ArgumentException("armyUpgradeCosts는 모두 0 이상이어야 합니다.");
        }
    }
}
