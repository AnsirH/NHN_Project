using System;
using System.Collections.Generic;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 인게임 → 아웃게임 전투 결과 (상세 기획 §7.2). 1차에서는 victory만 소비한다.
    /// </summary>
    [Serializable]
    public class BattleResultData
    {
        public string roomId;
        public bool victory;               // false → 아웃게임이 런 종료 처리 (§4-14)

        // 예약 (1차 미사용): 병력 손실 모델 변경 대비 (§9)
        public List<ArmySurvival> survivals = new List<ArmySurvival>();
    }

    [Serializable]
    public class ArmySurvival
    {
        public string armyInstanceId;
        public int survivedSoldierCount;
    }
}
