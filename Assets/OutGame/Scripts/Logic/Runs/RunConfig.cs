using System;

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

        // §4-7(2026-07-18 개정): 군대 보유 상한 = 배치 슬롯 수. BattleFieldConfig 기본(3×3=9)과
        // 일치해야 하지만 서로 다른 에셋이라 자동 동기화되지 않는다 — 슬롯 수를 바꾸면 같이 조정할 것.
        public int maxArmyCount = 9;

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
        }
    }
}
