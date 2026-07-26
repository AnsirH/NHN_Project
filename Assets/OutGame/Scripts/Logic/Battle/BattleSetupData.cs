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
        public int upgradeLevel;           // 군대 업그레이드 단계 (§4-26), 0~ArmyInstance.MaxUpgradeLevel — 참고용(UI 배지 등), 아래 최종 스탯 계산엔 이미 반영됨
        // 장군 스킬 강화 증강(§4-27, GeneralSkillUpgrade)을 선택한 횟수 — 스킬 자체가 정확히 어떻게
        // 강화되는지는 인게임 스킬 시스템 책임이라(§4-23과 동일 원칙) 전달하지 않는다. 몇 번 선택했는지
        // 숫자만 넘기고, 그 횟수를 스킬에 어떻게 반영할지는 인게임이 정한다(2026-07-26 사용자 확정).
        public int generalSkillUpgradeCount;

        // 최종 스탯 (업그레이드 §4-26 + 증강 §4-27 배율이 이미 곱해진 값, 2026-07-26 확정) — 인게임이
        // armyDefId/upgradeLevel로 재계산할 필요 없이 이 값을 그대로 쓰면 된다. 치명타율/이동속도는
        // 배율 대상이 아니라 원본 그대로이며 장군·유닛이 값을 공유한다(§5.7).
        public float generalHealth, generalAttack, generalDefense;
        public float generalCritRate, generalMoveSpeed;
        public float soldierHealth, soldierAttack, soldierDefense;

        public int slotId;                 // 배치 슬롯 ID
        public float slotX;                // 진영 내 정규화 좌표 (0~1), 아군 진영=좌측
        public float slotY;
    }
}
