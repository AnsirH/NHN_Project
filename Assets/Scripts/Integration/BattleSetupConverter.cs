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
                // 캐릭터가 스킬을 결정한다 (§5.2.5 — 캐릭터당 1개). 해석은 BattleCatalog 몫.
                playerSkillId = setup.playerCharacterSkillId,
            };
            foreach (DeployedArmy army in setup.armies)
            {
                request.playerSquads.Add(ToSquadRequest(army));
            }
            AddEnemySquads(request.enemySquads, setup.enemies);
            return request;
        }

        /// <summary>
        /// 아웃게임이 확정한 적 구성을 분대 요청으로 변환한다 — 아군(ToSquadRequest)과 동일 원칙
        /// (2026-08-02 계약: EnemyArmy에 최종 스탯·배치 좌표가 실려 온다. 재계산·자체 진형 없이
        /// 그대로 복사 — 배치 화면(EnemyFormationAssigner)에 보이는 위치·수치가 곧 실제 전투다).
        /// roleId/generalId만 병과에서 인게임이 매핑한다 (스탯이 아니라 에셋 선택이므로 인게임 소유).
        /// </summary>
        public static void AddEnemySquads(List<SquadRequest> output, IReadOnlyList<EnemyArmy> enemies)
        {
            if (enemies == null)
            {
                return;
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyArmy enemy = enemies[i];
                output.Add(new SquadRequest
                {
                    squadId = $"{enemy.armyDefId}#{i}", // 결과 집계엔 안 쓰이지만 로그·디버깅 식별용
                    roleId = MapClassToRoleId(enemy.armyClass),
                    generalId = MapClassToGeneralId(enemy.armyClass),
                    soldierCount = enemy.soldierCount,
                    slotX = enemy.slotX,
                    slotY = enemy.slotY,

                    // 최종 스탯 — 난이도·병과 배율이 이미 반영된 값이라 그대로 쓴다 (아군과 동일).
                    maxHp = enemy.soldierHealth,
                    attackDamage = enemy.soldierAttack,
                    defense = enemy.soldierDefense,
                    generalMaxHp = enemy.generalHealth,
                    generalAttackDamage = enemy.generalAttack,
                    generalDefense = enemy.generalDefense,

                    // 치명타·이동속도는 장군·유닛 공유 + 이동속도만 인게임 단위 환산 (아군과 동일).
                    critChancePercent = enemy.generalCritRate,
                    moveSpeed = enemy.generalMoveSpeed * MoveSpeedScale,

                    // 적은 장군 스킬 강화 증강 개념이 없다 — 0 = 강화 없음.
                    generalSkillUpgradeCount = 0,
                });
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
