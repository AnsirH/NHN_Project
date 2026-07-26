using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;
using OutGame.Logic.Maps;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 아웃게임 → 인게임 전달 데이터 (상세 기획 §7.1 — 인터페이스 계약).
    /// 직렬화 가능, Unity 타입 미사용. 변경 시 인게임 개발자와 합의 필요.
    /// </summary>
    [Serializable]
    public class BattleSetupData
    {
        public string roomId;              // 방 노드 ID
        public RoomType roomType;          // NormalBattle | Boss
        public string encounterId;         // RoomEncounterTable 키 (적 구성 — 인게임 협의)
        public List<DeployedArmy> armies = new List<DeployedArmy>();
    }

    [Serializable]
    public class DeployedArmy
    {
        public string armyInstanceId;
        public string armyDefId;           // ArmyDefinition 키
        public ArmyClass armyClass;        // None | Archer | Warrior | ... (Cavalry는 미사용 예약, §4-25)
        public string equippedItemId;      // 없으면 null/empty
        public string generalSkillId;      // 병과 부여 시 장군 스킬 (§4-23, 없으면 null/empty)
        public int soldierCount;           // 증원 보정 반영된 최종 병사 수
        public int upgradeLevel;           // 군대 업그레이드 단계 (§4-26), 0~ArmyInstance.MaxUpgradeLevel
        public int slotId;                 // 배치 슬롯 ID
        public float slotX;                // 진영 내 정규화 좌표 (0~1), 아군 진영=좌측
        public float slotY;
    }
}
