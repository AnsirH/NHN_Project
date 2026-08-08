using System;
using System.Collections.Generic;

namespace NHN.Data
{
    /// <summary>
    /// 전투 1판 실행 요청 — 아웃게임 연동 선행 준비 (outGame BattleBridge/BattleSetupData 계약과 1:1 대응).
    /// 문자열 키는 인게임 어휘 기준: roleId/generalId = BattleCatalog 키(에셋 이름).
    /// outGame의 ArmyClass/generalSkillId → 이 키로의 변환은 머지 후 커넥터 한 곳에서 담당한다
    /// (Assets/Docs/Integration/BattleBridgeConnector.cs.txt).
    /// </summary>
    [Serializable]
    public sealed class BattleRequest
    {
        /// <summary>
        /// 적 구성 참고 식별자 (로그·디버깅용). 2026-07-29부터 실제 적 구성은 enemySquads에 실려 오고,
        /// 이 키로 EncounterTable을 조회하는 것은 enemySquads가 비었을 때(씬 단독 실행·BalanceLab)의 폴백이다.
        /// </summary>
        public string encounterId;
        public int seed;
        public List<SquadRequest> playerSquads = new List<SquadRequest>();

        /// <summary>
        /// 적 분대 구성 — 아웃게임이 확정한 병과·병사 수(BattleSetupData.enemies)를 커넥터가 변환해 채운다.
        /// 적은 업그레이드·증강이 없어 스탯 필드는 비워두며(.asset 값 사용), 배치 좌표는 인게임 책임이라
        /// 커넥터의 진형 규칙이 정한다. 비어 있으면 encounterId 폴백 경로를 쓴다.
        /// </summary>
        public List<SquadRequest> enemySquads = new List<SquadRequest>();

        /// <summary>
        /// 선택한 플레이어 캐릭터의 스킬 (계약 playerCharacterSkillId, §5.2.5 — 캐릭터당 정확히 1개).
        /// BattleCatalog.ResolveSkill로 해석해 그 스킬만 사용 가능하게 제한한다.
        /// 비었거나 미등록이면(씬 단독 실행·BalanceLab·키 불일치) 전체 스킬을 쓴다 — 전투가 죽지 않게 관대한 폴백.
        /// </summary>
        public string playerSkillId;
    }

    [Serializable]
    public sealed class SquadRequest
    {
        /// <summary>outGame armyInstanceId 보존용 — 결과 생존 집계의 키.</summary>
        public string squadId;
        /// <summary>비우면 노멀 병사 (기획 §5 — 장군 없는 분대는 노멀).</summary>
        public string roleId;
        /// <summary>비우면 장군 없음.</summary>
        public string generalId;
        public int soldierCount;
        /// <summary>진영 내 정규화 0~1, 1 = 전선 쪽 — outGame 슬롯 좌표와 동일 의미.</summary>
        public float slotX;
        /// <summary>진영 내 정규화 0~1, 0.5 = 측면 중앙.</summary>
        public float slotY;

        // ── 전투 스탯 (아웃게임 계약 §7.1.1 모델) ──
        // 커넥터가 armyDefId로 원형(ArmyData)을 조회하고 ArmyStatCalculator 배율을 곱해 채운다.
        // maxHp > 0 이면 전달된 것으로 보고 .asset 스탯 대신 사용한다.
        // 전달이 없으면(로컬 테스트·BalanceLab 경로) .asset 값을 그대로 쓴다.

        /// <summary>병사 "1명" 기준 체력 (분대 합계 아님).</summary>
        public float maxHp;
        public float attackDamage;
        public float defense;

        public float generalMaxHp;
        public float generalAttackDamage;
        public float generalDefense;

        // ── 장군·병사 공유 스탯 ──
        // 아웃게임 기획 §5.7: 치명타 확률과 이동속도는 유닛도 장군 값을 그대로 쓴다 (배율 미적용).
        /// <summary>0~100 퍼센트 (아웃게임 표기 단위와 동일). 장군·병사 공용.</summary>
        public float critChancePercent;
        /// <summary>인게임 단위(초당 이동 거리) — 아웃게임 단위 변환은 커넥터가 한다. 장군·병사 공용.</summary>
        public float moveSpeed;

        /// <summary>
        /// 장군 스킬 강화 증강을 선택한 횟수 (아웃게임 generalSkillUpgradeCount).
        /// 아웃게임은 횟수만 넘기고 해석은 인게임 몫 — 인게임은 이를 **충전 필요량 감소**(= 발동 빈도 증가)로
        /// 번역한다. 배율은 BattleConfig의 감소율·하한 비율이 결정한다.
        /// </summary>
        public int generalSkillUpgradeCount;

        /// <summary>병사 스탯이 외부에서 전달됐는지 — 체력은 0일 수 없으므로 판정 기준으로 쓴다.</summary>
        public bool HasSoldierStats => maxHp > 0f;

        /// <summary>장군 스탯이 외부에서 전달됐는지.</summary>
        public bool HasGeneralStats => generalMaxHp > 0f;
    }

    /// <summary>전투 1판 결과 — outGame BattleResultData 계약과 1:1 대응 (victory + 분대별 생존).</summary>
    public sealed class BattleOutcome
    {
        /// <summary>플레이어(A군) 승리 여부. 시간 상한 무승부는 패배로 치지 않는다 — B군 승리만 패배 (2026-08-06).</summary>
        public bool Victory;
        public readonly List<SquadSurvival> Survivals = new List<SquadSurvival>();
    }

    public readonly struct SquadSurvival
    {
        public readonly string SquadId;
        public readonly int SurvivedSoldierCount;

        public SquadSurvival(string squadId, int survivedSoldierCount)
        {
            SquadId = squadId;
            SurvivedSoldierCount = survivedSoldierCount;
        }
    }
}
