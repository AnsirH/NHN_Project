using System;

namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 군대 원형 데이터 (순수 POCO — ArmyDefinition SO가 이 데이터를 채워 넘긴다).
    /// 전투 스탯 상세(공격력·체력 등)는 인게임 협의 영역이라 SO 쪽 예약 필드로 둔다.
    /// </summary>
    [Serializable]
    public class ArmyData
    {
        public string id;
        public string displayName;
        public int baseSoldierCount = 30;

        // 장군 (§5.5 — 군대당 1명 상시 존재)
        public string generalName;
        public float generalPower = 10f; // 전투력 계산 보정값 (§4-22)

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ArmyData.id가 비어 있습니다.");
            if (baseSoldierCount < 1)
                throw new ArgumentException($"baseSoldierCount는 1 이상이어야 합니다. 현재: {baseSoldierCount}");
            if (generalPower < 0f)
                throw new ArgumentException($"generalPower는 0 이상이어야 합니다. 현재: {generalPower}");
        }
    }
}
