using System;

namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 군대 원형 데이터 (순수 POCO — ArmyDefinition SO가 이 데이터를 채워 넘긴다).
    /// 전투 스탯(체력·공격력·방어력 등)은 §9 미결 밸런스 초안값 — 인스펙터에서 조정 가능.
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

        // 장군 정보 팝업 스탯 (§5.7, §9 미결 — 밸런스 초안값, 인스펙터에서 조정)
        public float generalHealth = 100f;
        public float generalAttack = 10f;
        public float generalDefense = 5f;
        public float generalCritRate = 5f; // %
        public float generalMoveSpeed = 100f;

        // 군대(병사) 정보 — 치명타 확률·이동 속도는 장군을 따르므로 별도 필드 없음 (§5.7)
        public string description = "";
        public float soldierHealth = 50f;
        public float soldierAttack = 5f;
        public float soldierDefense = 2f;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ArmyData.id가 비어 있습니다.");
            if (baseSoldierCount < 1)
                throw new ArgumentException($"baseSoldierCount는 1 이상이어야 합니다. 현재: {baseSoldierCount}");
            if (generalPower < 0f)
                throw new ArgumentException($"generalPower는 0 이상이어야 합니다. 현재: {generalPower}");
            if (generalHealth < 0f)
                throw new ArgumentException($"generalHealth는 0 이상이어야 합니다. 현재: {generalHealth}");
            if (generalAttack < 0f)
                throw new ArgumentException($"generalAttack은 0 이상이어야 합니다. 현재: {generalAttack}");
            if (generalDefense < 0f)
                throw new ArgumentException($"generalDefense는 0 이상이어야 합니다. 현재: {generalDefense}");
            if (generalCritRate < 0f || generalCritRate > 100f)
                throw new ArgumentException($"generalCritRate는 0~100 사이여야 합니다. 현재: {generalCritRate}");
            if (generalMoveSpeed < 0f)
                throw new ArgumentException($"generalMoveSpeed는 0 이상이어야 합니다. 현재: {generalMoveSpeed}");
            if (soldierHealth < 0f)
                throw new ArgumentException($"soldierHealth는 0 이상이어야 합니다. 현재: {soldierHealth}");
            if (soldierAttack < 0f)
                throw new ArgumentException($"soldierAttack은 0 이상이어야 합니다. 현재: {soldierAttack}");
            if (soldierDefense < 0f)
                throw new ArgumentException($"soldierDefense는 0 이상이어야 합니다. 현재: {soldierDefense}");
        }
    }
}
