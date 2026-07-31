using NHN.Data;
using NHN.Simulation.Battle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.Simulation.Tests
{
    /// <summary>
    /// 장군 시스템 헤드리스 검증 (기획 §6, v4 파트 B 요구 테스트 3종):
    /// (1) 장군 유/무 승률 차이 (2) 장군 조기 사망 시 패시브·게이지 소멸 (3) 충전식 액티브의 전투당 2회 이상 발동.
    /// 장군은 GeneralDefinition 데이터만으로 구성된다 — 장군별 클래스 없음 (에셋 경로는 GeneralData가 동일 구조).
    /// </summary>
    public sealed class GeneralHeadlessTests
    {
        private const string WarriorPath = "Assets/Data/Roles/Warrior.asset";
        private const string ArcherPath = "Assets/Data/Roles/Archer.asset";
        private const string AssassinPath = "Assets/Data/Roles/Assassin.asset";
        private const string HunterPath = "Assets/Data/Roles/Hunter.asset";
        private const string ConfigPath = "Assets/Data/BattleConfig.asset";

        // 테스트 전용 장군 튜닝 (전투당 2~3회 수준 검증용 — 실전 수치는 GeneralData 에셋에서 튜닝)
        private const float EliteHpMultiplier = 3f;
        private const float EliteDamageMultiplier = 1.5f;
        private const float EliteSizeMultiplier = 1.3f;

        private static RoleData LoadRole(string path)
        {
            var role = AssetDatabase.LoadAssetAtPath<RoleData>(path);
            Assert.IsNotNull(role, $"{path} 에셋이 있어야 한다");
            return role;
        }

        private static BattleConfigSO LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} 에셋이 있어야 한다");
            return config;
        }

        /// <summary>
        /// 장군의 전투 능력 = 기반 롤 × 엘리트 배율 — GeneralData.ToDefinition과 같은 파생 규칙.
        /// hpMultiplier 재정의: 선두에 선 장군은 적 최근접 타겟팅의 집중 포화를 받으므로(기획 §5 —
        /// "근접 부대 장군은 구조적으로 위험") 장기 생존 시나리오는 높은 HP 배율로 표현한다.
        /// </summary>
        private static RoleDefinition Elite(RoleDefinition baseRole, float hpMultiplier = EliteHpMultiplier)
        {
            return new RoleDefinition(
                baseRole.RoleName + "General",
                baseRole.MaxHp * hpMultiplier,
                baseRole.AttackDamage * EliteDamageMultiplier,
                baseRole.Defense,
                baseRole.CritChancePercent,
                baseRole.AttackInterval,
                baseRole.AttackRange,
                baseRole.MoveSpeed,
                baseRole.UnitRadius * EliteSizeMultiplier,
                baseRole.ProjectileSpeed,
                baseRole.ProjectileArcHeight,
                baseRole.PositionFilter,
                baseRole.Priorities,
                baseRole.MovePattern,
                baseRole.MoveParamA,
                baseRole.MoveParamB);
        }

        private static BattleResult RunToEnd(BattleSimulation sim)
        {
            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
            }
            Assert.IsTrue(sim.Finished, "전투가 시간 상한 안에 종료되어야 한다");
            return sim.Result;
        }

        private static ArmyDefinition SingleSquad(RoleDefinition role, int count, GeneralDefinition general)
        {
            return new ArmyDefinition(new[]
            {
                new SquadDefinition(role, count, new System.Numerics.Vector2(0f, 0f), general),
            });
        }

        /// <summary>
        /// 완료 기준 자가 검증: 장군 1명 추가 = GeneralData 에셋 1개, 코드 수정 0줄.
        /// 에셋 로드 → ToDefinition → 시뮬 구동까지 데이터만으로 동작해야 한다.
        /// </summary>
        [Test]
        public void GeneralDataAsset_DrivesSimulation_WithoutCodeChanges()
        {
            var generalData = AssetDatabase.LoadAssetAtPath<GeneralData>("Assets/Data/Generals/WarriorGeneral.asset");
            Assert.IsNotNull(generalData, "WarriorGeneral 에셋이 있어야 한다");

            RoleDefinition warrior = LoadRole(WarriorPath).ToDefinition();
            GeneralDefinition general = generalData.ToDefinition();
            Assert.AreEqual(SquadPassive.AttackPercent, general.Passive);
            Assert.AreEqual(GimmickEffect.SquadDamageResist, general.ActiveEffect);

            var sim = new BattleSimulation(
                LoadConfig().ToConfig(),
                SingleSquad(warrior, 10, general),
                SingleSquad(warrior, 10, general: null),
                seed: 3);
            Assert.AreEqual(21, sim.UnitCount, "장군은 실재 유닛으로 스폰되어야 한다 (10+장군 vs 10)");
            int generalUnit = sim.GetGeneralUnit(0);
            Assert.IsTrue(sim.IsLeader(generalUnit), "분대 리더 플래그가 장군 유닛에 설정되어야 한다");
            Assert.AreEqual(warrior.MaxHp * 8f, sim.GetHp(generalUnit), 1e-3f, "엘리트 배율이 에셋 수치로 적용되어야 한다");
            RunToEnd(sim);
        }

        /// <summary>
        /// (1) 장군 유/무 승률: 같은 병사 구성에서 장군이 있는 쪽이 다수 시드에서 이겨야 한다.
        /// 패시브(공격력 +25%) + 방진 액티브 + 장군 유닛 자체가 배치 가치의 근거.
        /// </summary>
        [Test]
        public void GeneralPresence_ImprovesWinRate()
        {
            RoleDefinition warrior = LoadRole(WarriorPath).ToDefinition();
            BattleConfig config = LoadConfig().ToConfig();
            const int Battles = 30;
            const int SoldierCount = 20;

            int winsWithGeneral = 0;
            int winsWithout = 0;
            int draws = 0;
            for (int seed = 0; seed < Battles; seed++)
            {
                var general = new GeneralDefinition(
                    Elite(warrior),
                    SquadPassive.AttackPercent, passiveValue: 0.25f,
                    ChargeCondition.SquadDeaths, chargeRequired: 3f,
                    GimmickEffect.SquadDamageResist, activeParamA: 0.3f, activeParamB: 0f, activeDuration: 2.5f,
                    leadRankOffset: 1f);

                var sim = new BattleSimulation(
                    config,
                    SingleSquad(warrior, SoldierCount, general),
                    SingleSquad(warrior, SoldierCount, general: null),
                    seed);
                BattleResult result = RunToEnd(sim);
                if (result.Winner == 0)
                {
                    winsWithGeneral++;
                }
                else if (result.Winner == 1)
                {
                    winsWithout++;
                }
                else
                {
                    draws++;
                }
            }

            Debug.Log($"[장군 유무 승률] {Battles}판 (전사 {SoldierCount}+장군 vs 전사 {SoldierCount}): " +
                      $"장군측 승 {winsWithGeneral} ({100f * winsWithGeneral / Battles:F0}%), " +
                      $"무장군측 승 {winsWithout} ({100f * winsWithout / Battles:F0}%), 무승부 {draws}");

            Assert.Greater(winsWithGeneral, winsWithout,
                "장군(패시브+액티브+엘리트 유닛)이 있는 군대가 다수 시드에서 이겨야 한다");
        }

        /// <summary>
        /// (2) 장군 조기 사망: 패시브 소멸(유효 공격력 원복) + 충전 게이지 소멸 + 이후 발동 없음 (기획 §6 사망 규칙).
        /// </summary>
        [Test]
        public void GeneralEarlyDeath_RemovesPassiveAndCharge()
        {
            RoleDefinition warrior = LoadRole(WarriorPath).ToDefinition();
            BattleConfig config = LoadConfig().ToConfig();

            var general = new GeneralDefinition(
                Elite(warrior),
                SquadPassive.AttackPercent, passiveValue: 0.5f,
                ChargeCondition.TimeElapsed, chargeRequired: 6f,
                GimmickEffect.SquadDamageResist, activeParamA: 0.3f, activeParamB: 0f, activeDuration: 2f,
                leadRankOffset: 1f);

            // A: 원거리에 홀로 (개입 방지용 깊은 후방 배치) / B: 병사 1 + 장군. 처형 스킬은 B군(적군)만 타격한다.
            // 반경 0.4: 장군 선두 리드(1랭크 = 1.25유닛)보다 판정 반경(0.4+병사 반경)이 작아 장군만 맞는다.
            var executeSkill = new SkillDefinition(
                "Execute", cooldown: 1f, radius: 0.4f, damage: 9999f,
                StatusEffectType.Stun, statusDuration: 0f, statusMagnitude: 0f, zoneDuration: 0f);
            var armyA = new ArmyDefinition(new[]
            {
                new SquadDefinition(warrior, 1, new System.Numerics.Vector2(8f, 0f)),
            });
            var armyB = SingleSquad(warrior, 1, general);
            var sim = new BattleSimulation(config, armyA, armyB, seed: 21, new[] { executeSkill });

            const int SoldierIndex = 1;  // 유닛 순서: A(0) → B 병사(1) → B 장군(2)
            const int GeneralIndex = 2;
            const int SquadB = 1;        // 분대 순서: A(0) → B(1)

            Assert.IsTrue(sim.IsLeader(GeneralIndex), "분대 마지막 유닛이 장군이어야 한다");
            Assert.AreEqual(GeneralIndex, sim.GetGeneralUnit(SquadB));
            Assert.IsTrue(sim.IsGeneralAlive(SquadB));
            Assert.AreEqual(warrior.AttackDamage * 1.5f, sim.GetEffectiveAttackDamage(SoldierIndex), 1e-3f,
                "장군 생존 중에는 패시브(공격력 +50%)가 부대원에게 적용되어야 한다");

            // 2초 경과 → TimeElapsed 게이지 충전 확인
            int chargeTicks = (int)(2f / sim.TickDeltaTime);
            for (int t = 0; t < chargeTicks; t++)
            {
                sim.Tick();
            }
            Assert.AreEqual(2f, sim.GetSquadCharge(SquadB), 0.1f, "TimeElapsed 조건은 초당 1씩 충전되어야 한다");

            // 장군 처형 → 같은 틱에 사망 처리
            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(GeneralIndex)));
            sim.Tick();

            Assert.IsFalse(sim.IsAlive(GeneralIndex), "처형 스킬로 장군이 사망해야 한다");
            Assert.IsTrue(sim.IsAlive(SoldierIndex), "부대원은 처형 범위 밖이어야 한다 (롤 유지 규칙의 전제)");
            Assert.IsFalse(sim.IsGeneralAlive(SquadB));
            Assert.AreEqual(0f, sim.GetSquadCharge(SquadB), "장군 사망 시 충전 게이지가 소멸해야 한다");
            Assert.AreEqual(warrior.AttackDamage, sim.GetEffectiveAttackDamage(SoldierIndex), 1e-3f,
                "장군 사망 시 패시브가 소멸해 유효 공격력이 원복되어야 한다");

            // 이후 충전·발동이 재개되지 않아야 한다 (충전 중 스킬 소멸)
            int afterTicks = (int)(10f / sim.TickDeltaTime);
            for (int t = 0; t < afterTicks; t++)
            {
                sim.Tick();
            }
            Assert.AreEqual(0f, sim.GetSquadCharge(SquadB), "사망한 장군의 게이지는 다시 충전되지 않아야 한다");
            Assert.AreEqual(0, sim.GetSquadActivationCount(SquadB), "사망한 장군의 액티브는 발동하지 않아야 한다");
        }

        /// <summary>
        /// (3) 충전식 액티브가 한 전투에서 2회 이상 발동 가능함을 확정 4종 전부에 대해 확인 +
        /// 전투당 평균 발동 횟수 로그 (밸런싱 파이프라인 입력).
        /// </summary>
        [Test]
        public void ChargedActives_CanFireTwicePerBattle_AllFourSkills()
        {
            RoleDefinition warrior = LoadRole(WarriorPath).ToDefinition();
            RoleDefinition archer = LoadRole(ArcherPath).ToDefinition();
            RoleDefinition assassin = LoadRole(AssassinPath).ToDefinition();
            RoleDefinition hunter = LoadRole(HunterPath).ToDefinition();
            BattleConfig config = LoadConfig().ToConfig();
            const int Battles = 10;

            // (스킬명, 아군 분대, 적군 분대) — 충전 필요량은 "전투당 2~3회" 검증용 튜닝
            (string name, RoleDefinition role, int count, GeneralDefinition general, RoleDefinition enemyRole, int enemyCount)[] cases =
            {
                // 선두 장군이 집중 포화를 받는 구도이므로(§5) 장기 생존형 케이스는 높은 HP 배율(튼튼한 장군)로 튜닝.
                // 방진: 적을 다수(25)로 — 부대원 사망이 확실히 누적되도록.
                ("방진(누적 사망)", warrior, 20,
                    new GeneralDefinition(Elite(warrior, hpMultiplier: 10f), SquadPassive.AttackPercent, 0.15f,
                        ChargeCondition.SquadDeaths, 2f, GimmickEffect.SquadDamageResist, 0.3f, 0f, 2.5f, 1f),
                    warrior, 25),
                ("일제 사격(누적 공격)", archer, 20,
                    new GeneralDefinition(Elite(archer, hpMultiplier: 8f), SquadPassive.AttackPercent, 0.15f,
                        ChargeCondition.SquadAttacks, 40f, GimmickEffect.SquadVolley, 2.5f, 1f, 0f, 1f),
                    warrior, 30),
                ("그림자 습격(누적 킬)", assassin, 12,
                    new GeneralDefinition(Elite(assassin), SquadPassive.AttackPercent, 0.15f,
                        ChargeCondition.SquadKills, 4f, GimmickEffect.SquadRestealthCrit, 2f, 2f, 0f, 1f),
                    archer, 20),
                // 사냥 선포: 충전 3초 + 적 30 — 전투가 2주기(6초) 이상 지속되도록.
                // (부분 겹침 도입(separationOverlapRatio)으로 밀집 교전이 빨라져 4초 주기로는
                //  2회째 전에 전투가 끝난다 — 이 테스트는 충전 메커니즘 검증용이라 주기를 재튜닝했다.
                //  실제 발동 빈도 밸런스는 HunterGeneral.asset의 chargeRequired가 소유한다.)
                ("사냥 선포(시간 경과)", hunter, 15,
                    new GeneralDefinition(Elite(hunter, hpMultiplier: 10f), SquadPassive.AttackPercent, 0.15f,
                        ChargeCondition.TimeElapsed, 3f, GimmickEffect.MarkStrongestEnemy, 1.5f, 0f, 6f, 1f),
                    warrior, 30),
            };

            // 4종 전부 실행·로그를 남긴 뒤 마지막에 일괄 단언 — 한 케이스 실패가 나머지 검증을 가리지 않도록.
            var failures = new System.Text.StringBuilder();
            foreach (var testCase in cases)
            {
                int total = 0;
                int best = 0;
                for (int seed = 0; seed < Battles; seed++)
                {
                    var sim = new BattleSimulation(
                        config,
                        SingleSquad(testCase.role, testCase.count, testCase.general),
                        SingleSquad(testCase.enemyRole, testCase.enemyCount, general: null),
                        seed: 1000 + seed);
                    RunToEnd(sim);
                    int activations = sim.GetSquadActivationCount(0); // A군 첫 분대
                    total += activations;
                    best = Mathf.Max(best, activations);
                }

                Debug.Log($"[충전 발동] {testCase.name}: 평균 {(float)total / Battles:F1}회/전투, " +
                          $"최대 {best}회 ({Battles}판)");
                if (best < 2)
                {
                    failures.AppendLine($"{testCase.name}: 최대 {best}회 — 2회 이상 발동이 확인되지 않았다");
                }
            }
            Assert.IsTrue(failures.Length == 0, $"충전식 액티브 2회 이상 발동 실패:\n{failures}");
        }
    }
}
