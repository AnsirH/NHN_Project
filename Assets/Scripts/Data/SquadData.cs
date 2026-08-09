using System.Collections.Generic;
using NHN.Simulation.Battle;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>
    /// 분대 1종의 단일 정의 (기획 §6/§10) — 병사 롤 + 장군 능력을 한 에셋에 통합한다.
    /// 분대 추가 = 이 에셋 1개 생성, 코드 수정 0줄. 시뮬에는 ToDefinition()/ToGeneralDefinition()으로
    /// 순수 RoleDefinition/GeneralDefinition만 넘긴다.
    ///
    /// (2026-08-10, 사용자 확정) 기존에는 RoleData(병사)와 GeneralData(장군)가 별도 에셋이었으나,
    /// 장군은 baseRole 참조 + 배율만 가질 뿐 독립 데이터가 없어 항상 "Warrior"+"WarriorGeneral"처럼
    /// 1:1 고정 페어로만 존재했다(코드도 MapClassToGeneralId에서 이 페어링을 이름 조합으로 하드코딩).
    /// 관리 부담(에셋 2배, 이름 동기화)만 있고 이점이 없어 하나로 합쳤다.
    /// </summary>
    [CreateAssetMenu(fileName = "Squad", menuName = "NHN/Squad")]
    public sealed class SquadData : ScriptableObject
    {
        [Header("병사 스탯")]
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float attackDamage = 10f;
        [Tooltip("피해 감쇠 K/(K+방어력) — K는 BattleConfig.defenseK. 0 = 감쇠 없음")]
        [SerializeField] private float defense;
        [Tooltip("치명타 확률 (0~100 퍼센트 — 아웃게임 표기 단위). 0이면 추첨하지 않는다")]
        [SerializeField] private float critChancePercent;
        [SerializeField] private float attackInterval = 1f;
        [Tooltip("유닛 가장자리 기준 사거리")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float unitRadius = 0.5f;

        [Header("타겟팅 (2단계: 위치 필터 → 우선순위 목록)")]
        [SerializeField] private PositionFilter positionFilter = PositionFilter.Nearest;
        [SerializeField] private List<TargetPriority> priorities = new List<TargetPriority>();

        [Header("이동 패턴")]
        [SerializeField] private MovePattern movePattern = MovePattern.ApproachTarget;
        [Tooltip("이동 패턴 파라미터 A — StealthDash: 은신 지속시간(초)")]
        [SerializeField] private float moveParamA;
        [Tooltip("이동 패턴 파라미터 B — StealthDash: 은신 중 이속 배율 (1 = 미적용)")]
        [SerializeField] private float moveParamB = 1f;

        [Header("투사체 (속도 0 = 근접)")]
        [SerializeField] private float projectileSpeed;
        [Tooltip("포물선 정점 높이 — 뷰 표현 전용")]
        [SerializeField] private float projectileArcHeight = 2f;

        [Header("장군 — 전투 능력 배율 (기반 = 위 병사 스탯 × 배율)")]
        [Tooltip("로컬 경로(테스트 씬·BalanceLab) 전용 — 연결 경로에서는 아웃게임이 계산한 스탯이 들어온다")]
        [SerializeField] private float generalHpMultiplier = 3f;
        [SerializeField] private float generalDamageMultiplier = 1.5f;
        [Tooltip("장군 즉시 구분용 크기 배율 (기획 §4) — 반경은 인게임 소유 속성이라 연결 경로에서도 항상 적용")]
        [SerializeField] private float generalSizeMultiplier = 1.3f;

        [Header("장군 — 패시브 (부대 지속 버프 — 장군 생존 중에만, 사망 시 소멸)")]
        [SerializeField] private SquadPassive generalPassive = SquadPassive.AttackPercent;
        [Tooltip("의미는 케이스별 — AttackPercent: 증가 비율 (0.15 = +15%)")]
        [SerializeField] private float generalPassiveValue = 0.15f;

        [Header("장군 — 액티브 (조건 충전식 — 임계치 도달 시 자동 발동 후 리셋, 전투당 2~3회 튜닝)")]
        [SerializeField] private ChargeCondition generalChargeCondition = ChargeCondition.TimeElapsed;
        [Tooltip("발동 임계치 — 이벤트형: 횟수, TimeElapsed: 초")]
        [SerializeField] private float generalChargeRequired = 20f;
        [Tooltip("부대 스코프 케이스만 사용 (SquadDamageResist/SquadVolley/SquadRestealthCrit/MarkStrongestEnemy)")]
        [SerializeField] private GimmickEffect generalActiveEffect = GimmickEffect.SquadDamageResist;
        [SerializeField] private float generalActiveParamA;
        [SerializeField] private float generalActiveParamB;
        [Tooltip("지속형 효과(방진/표식)의 지속시간 초")]
        [SerializeField] private float generalActiveDuration;

        [Header("장군 — 배치")]
        [Tooltip("분대 선두 오프셋 (랭크): +1 = 병사 최전열보다 한 줄 앞, 0 = 같은 줄, -1 = 한 줄 뒤. 범위 -1~+1로 강제된다")]
        [Range(GeneralDefinition.MinLeadRankOffset, GeneralDefinition.MaxLeadRankOffset)]
        [SerializeField] private float generalLeadRankOffset = 1f;

        [Header("뷰 (가독성 1:1:1 — 롤:실루엣:색)")]
        [SerializeField] private Color roleColor = Color.white;
        [Tooltip("병사 3D 모델 프리팹 — 비우면 기본 캡슐 프리팹 사용 (뷰 전용, 시뮬 무관)")]
        [SerializeField] private GameObject soldierViewPrefab;
        [Tooltip("장군 3D 모델 프리팹 — 비우면 병사 프리팹을 그대로 쓴다 (뷰 전용, 시뮬 무관)")]
        [SerializeField] private GameObject generalViewPrefab;
        [Tooltip("공격 사운드 목록 — 스윙마다 무작위 1개 재생. 비우면 무음 (뷰 전용, 시뮬 무관)")]
        [SerializeField] private AudioClip[] attackSounds;

        public Color RoleColor => roleColor;

        public GameObject ViewPrefab => soldierViewPrefab;

        /// <summary>장군 전용 모델 — 비어 있으면 병사 프리팹으로 자연히 대체된다.</summary>
        public GameObject GeneralViewPrefab => generalViewPrefab != null ? generalViewPrefab : soldierViewPrefab;

        public AudioClip[] AttackSounds => attackSounds;

        public float UnitRadius => unitRadius;

        /// <summary>뷰 전용 조회 — 공격 애니메이션 재생 속도를 공격 주기에 동기화하는 데 쓴다.</summary>
        public float AttackInterval => attackInterval;

        private void OnValidate()
        {
            // 튜닝 루프가 "전장 밖 도피"로 수렴하지 못하도록 데이터 단계에서 범위를 강제한다.
            generalLeadRankOffset = GeneralDefinition.ClampLeadRankOffset(generalLeadRankOffset);
        }

        public RoleDefinition ToDefinition()
        {
            return new RoleDefinition(
                name,
                maxHp, attackDamage, defense, critChancePercent,
                attackInterval, attackRange,
                moveSpeed, unitRadius,
                projectileSpeed, projectileArcHeight,
                positionFilter, priorities.ToArray(), movePattern, moveParamA, moveParamB);
        }

        /// <summary>
        /// 엘리트 파생 공식은 GeneralDefinition.CreateElite가 단일 출처 — BalanceLab CLI와 공유.
        /// 연결 경로에서는 이 파생값의 5스탯이 아웃게임 계산값으로 교체된다 (BattleRequestBuilder).
        /// </summary>
        public GeneralDefinition ToGeneralDefinition()
        {
            return GeneralDefinition.CreateElite(
                ToDefinition(), name,
                generalHpMultiplier, generalDamageMultiplier, generalSizeMultiplier,
                generalPassive, generalPassiveValue,
                generalChargeCondition, generalChargeRequired,
                generalActiveEffect, generalActiveParamA, generalActiveParamB, generalActiveDuration,
                generalLeadRankOffset);
        }
    }
}
