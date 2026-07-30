using System.Collections.Generic;
using NHN.Data;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;

namespace NHN.Integration
{
    /// <summary>
    /// 아웃게임 계약(BattleSetupData) → 인게임 전투 요청(BattleRequest) 순수 변환.
    ///
    /// MonoBehaviour(커넥터)에서 분리한 이유: 변환 규칙은 씬·오브젝트와 무관한 순수 함수라
    /// 단위 테스트로 검증할 수 있어야 한다 — 계약이 세 번 바뀐 이력이 있어(스탯 재계산 → 직접 전달 등)
    /// 다음 변경에서도 매핑이 조용히 깨지지 않게 붙잡아 둘 그물이 필요하다.
    ///
    /// 스탯은 업그레이드·증강 배율이 이미 곱해진 최종값이라 그대로 옮긴다 (계약 §7.1).
    /// </summary>
    public static class BattleSetupConverter
    {
        /// <summary>
        /// 아웃게임 이동속도 단위 → 인게임 단위(초당 이동 거리) 환산 계수.
        /// 아웃게임 기본값 100이 인게임 기본 근접 속도 3.5에 대응한다 (100 × 0.035 = 3.5).
        /// </summary>
        public const float MoveSpeedScale = 0.035f;

        public static BattleRequest ToBattleRequest(BattleSetupData setup)
        {
            var request = new BattleRequest
            {
                encounterId = setup.encounterId,
                seed = StableSeed(setup.roomId), // 방마다 결정론적 시드 — 같은 방 재도전은 같은 전개
            };
            foreach (DeployedArmy army in setup.armies)
            {
                request.playerSquads.Add(ToSquadRequest(army));
            }
            AddEnemySquads(request.enemySquads, setup.enemies);
            return request;
        }

        // ── 적 진형 깊이 (병과 → slotX, 1 = 전선) ──
        // 계약상 적 배치 좌표는 인게임 책임 (적 데이터 연동 가이드 2026-07-29). 아웃게임 배치 화면의
        // 구역 규칙(EnemyFormationAssigner: 전사·사냥꾼 앞열, 암살자 중간, 궁수 후열, 기본 앞부터)과
        // 같은 어휘를 쓰되, 값은 인게임 전투 감각에 맞춘 진형 상수다 — 전투력 밸런스 수치가 아니라
        // 배치 레이아웃이라 SO로 빼지 않았다 (튜닝 대상이 되면 BattleConfig로 승격).
        private const float FrontDepth = 1f;    // 전사·기본 — 최전선 벽
        private const float MidFrontDepth = 0.8f; // 사냥꾼 — 전사 바로 뒤
        private const float MidDepth = 0.55f;   // 암살자 — 중간(측면 침투 전 대기)
        private const float BackDepth = 0.2f;   // 궁수 — 최후방

        /// <summary>
        /// 아웃게임이 확정한 적 구성(병과·병사 수)을 분대 요청으로 변환한다. 스탯 필드는 채우지 않는다 —
        /// 적은 업그레이드·증강이 없어 인게임 .asset 원형값이 곧 최종값이다 (HasSoldierStats=false 경로).
        /// 좌표는 병과별 깊이 + 같은 깊이 안에서 계약 목록 순서대로 균등 분산(결정론적).
        /// </summary>
        public static void AddEnemySquads(List<SquadRequest> output, IReadOnlyList<EnemyArmy> enemies)
        {
            if (enemies == null)
            {
                return;
            }

            // 같은 깊이(열)에 몇 분대가 서는지 먼저 세어 측면(slotY)을 균등 분산한다.
            var depthCounts = new Dictionary<float, int>();
            for (int i = 0; i < enemies.Count; i++)
            {
                float depth = ClassToDepth(enemies[i].armyClass);
                depthCounts.TryGetValue(depth, out int count);
                depthCounts[depth] = count + 1;
            }

            var depthFilled = new Dictionary<float, int>();
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyArmy enemy = enemies[i];
                float depth = ClassToDepth(enemy.armyClass);
                depthFilled.TryGetValue(depth, out int filled);
                depthFilled[depth] = filled + 1;
                output.Add(new SquadRequest
                {
                    squadId = $"{enemy.armyDefId}#{i}", // 결과 집계엔 안 쓰이지만 로그·디버깅 식별용
                    roleId = MapClassToRoleId(enemy.armyClass),
                    generalId = MapClassToGeneralId(enemy.armyClass),
                    soldierCount = enemy.soldierCount,
                    slotX = depth,
                    slotY = (filled + 1) / (float)(depthCounts[depth] + 1), // K개면 1/(K+1)..K/(K+1) 균등
                });
            }
        }

        private static float ClassToDepth(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.Hunter: return MidFrontDepth;
                case ArmyClass.Assassin: return MidDepth;
                case ArmyClass.Archer: return BackDepth;
                default: return FrontDepth; // Warrior·None(기본) — 전선
            }
        }

        public static SquadRequest ToSquadRequest(DeployedArmy army)
        {
            return new SquadRequest
            {
                squadId = army.armyInstanceId,
                roleId = MapClassToRoleId(army.armyClass),
                generalId = MapClassToGeneralId(army.armyClass),
                soldierCount = army.soldierCount,
                slotX = army.slotX,
                slotY = army.slotY,

                // 최종 스탯 — 업그레이드·증강 배율이 이미 반영된 값이라 그대로 쓴다.
                maxHp = army.soldierHealth,
                attackDamage = army.soldierAttack,
                defense = army.soldierDefense,
                generalMaxHp = army.generalHealth,
                generalAttackDamage = army.generalAttack,
                generalDefense = army.generalDefense,

                // 치명타·이동속도는 배율 대상이 아니며 장군·유닛이 값을 공유한다 (아웃게임 §5.7).
                critChancePercent = army.generalCritRate,
                moveSpeed = army.generalMoveSpeed * MoveSpeedScale,

                // 강화 횟수의 해석은 인게임 몫 — 충전 필요량 감소(발동 빈도 증가)로 번역한다.
                generalSkillUpgradeCount = army.generalSkillUpgradeCount,
            };
        }

        /// <summary>전투 결과를 아웃게임 계약 형태로 변환.</summary>
        public static BattleResultData ToResultData(string roomId, BattleOutcome outcome)
        {
            var result = new BattleResultData { roomId = roomId, victory = outcome.Victory };
            foreach (SquadSurvival survival in outcome.Survivals)
            {
                result.survivals.Add(new ArmySurvival
                {
                    armyInstanceId = survival.SquadId,
                    survivedSoldierCount = survival.SurvivedSoldierCount,
                });
            }
            return result;
        }

        /// <summary>아웃게임 병과 → 인게임 롤 (4병과 확정, 2026-07-26 — 어휘가 일치한다).</summary>
        public static string MapClassToRoleId(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.Warrior: return "Warrior";
                case ArmyClass.Archer: return "Archer";
                case ArmyClass.Hunter: return "Hunter";
                case ArmyClass.Assassin: return "Assassin";
                default: return null; // None(아이템 미부여) → 노멀 병사 (BattleCatalog 폴백)
            }
        }

        /// <summary>
        /// 장군은 병과에서 파생한다 — 병과가 장군 스킬을 결정하므로(§4-23) generalSkillId 키 규약에
        /// 의존하지 않는 편이 안전하다. 아이템 미부여 부대도 능력 없는 노멀 장군을 갖는다.
        /// </summary>
        public static string MapClassToGeneralId(ArmyClass armyClass)
        {
            return (MapClassToRoleId(armyClass) ?? "Normal") + "General";
        }

        /// <summary>프로세스 간 안정적인 문자열 해시 — string.GetHashCode는 런타임마다 달라질 수 있다.</summary>
        public static int StableSeed(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                return 0;
            }
            unchecked
            {
                int hash = 23;
                foreach (char c in roomId)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }
    }
}
