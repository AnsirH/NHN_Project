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

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(startingArmyDefId))
                throw new ArgumentException("startingArmyDefId가 비어 있습니다.");
            if (startingArmyCount < 1)
                throw new ArgumentException($"startingArmyCount는 1 이상이어야 합니다. 현재: {startingArmyCount}");
            if (startingGold < 0)
                throw new ArgumentException($"startingGold는 0 이상이어야 합니다. 현재: {startingGold}");
        }
    }
}
