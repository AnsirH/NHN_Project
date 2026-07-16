using NHN.Data;
using NHN.Simulation.Battle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.Simulation.Tests
{
    /// <summary>
    /// 씬 없이 전투 1판을 실행하는 헤드리스 테스트 — 불변조건 2(로직/뷰 분리)의 검증 기준이자
    /// AI 자동 밸런싱 파이프라인(기획 §12)의 실행 단위.
    /// </summary>
    public sealed class BattleHeadlessTests
    {
        private const string WarriorPath = "Assets/Data/Roles/Warrior.asset";
        private const string ArcherPath = "Assets/Data/Roles/Archer.asset";
        private const string AssassinPath = "Assets/Data/Roles/Assassin.asset";
        private const string HunterPath = "Assets/Data/Roles/Hunter.asset";
        private const string LightningPath = "Assets/Data/Skills/Lightning.asset";
        private const string PoisonCloudPath = "Assets/Data/Skills/PoisonCloud.asset";
        private const string ConfigPath = "Assets/Data/BattleConfig.asset";

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

        private static SkillData LoadSkill(string path)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            Assert.IsNotNull(skill, $"{path} 에셋이 있어야 한다");
            return skill;
        }

        /// <summary>단일 분대 vs 단일 분대 전투 생성. 유닛 인덱스는 A군(0..countA-1) → B군 순.</summary>
        private static BattleSimulation CreateBattle(
            RoleData roleA, int countA, RoleData roleB, int countB, int seed,
            SkillDefinition[] skills = null)
        {
            var armyA = new ArmyDefinition(new[]
            {
                new SquadDefinition(roleA.ToDefinition(), countA, new System.Numerics.Vector2(0f, 0f)),
            });
            var armyB = new ArmyDefinition(new[]
            {
                new SquadDefinition(roleB.ToDefinition(), countB, new System.Numerics.Vector2(0f, 0f)),
            });
            return new BattleSimulation(LoadConfig().ToConfig(), armyA, armyB, seed, skills);
        }

        private static BattleResult RunToEnd(BattleSimulation sim)
        {
            // 시뮬 자체의 시간 상한(무승부 판정)보다 넉넉한 하드 스톱.
            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
            }
            Assert.IsTrue(sim.Finished, "전투가 시간 상한 안에 종료되어야 한다");
            return sim.Result;
        }

        [Test]
        public void Warrior20_Vs_Archer20_ProducesWinner()
        {
            var sim = CreateBattle(LoadRole(WarriorPath), 20, LoadRole(ArcherPath), 20, seed: 42);
            BattleResult result = RunToEnd(sim);

            string winnerName = result.Winner == 0 ? "A군(전사)"
                : result.Winner == 1 ? "B군(궁수)"
                : "무승부";
            Debug.Log($"[헤드리스 전투] 승자: {winnerName}, 생존 A={result.SurvivorsTeamA} B={result.SurvivorsTeamB}, " +
                      $"경과 {result.ElapsedTicks}틱 ({result.ElapsedTicks / 30f:F1}s)");

            Assert.AreNotEqual(BattleResult.DrawWinner, result.Winner, "전사 vs 궁수는 한쪽 전멸로 끝나야 한다");
        }

        [Test]
        public void SameSeed_ProducesSameResult()
        {
            BattleResult first = RunToEnd(CreateBattle(LoadRole(WarriorPath), 20, LoadRole(ArcherPath), 20, seed: 7));
            BattleResult second = RunToEnd(CreateBattle(LoadRole(WarriorPath), 20, LoadRole(ArcherPath), 20, seed: 7));

            Assert.AreEqual(first.Winner, second.Winner, "같은 시드는 같은 승자를 내야 한다");
            Assert.AreEqual(first.ElapsedTicks, second.ElapsedTicks, "같은 시드는 같은 틱 수로 끝나야 한다");
            Assert.AreEqual(first.SurvivorsTeamA, second.SurvivorsTeamA);
            Assert.AreEqual(first.SurvivorsTeamB, second.SurvivorsTeamB);
        }

        [Test]
        public void Assassin20_Vs_Archer20_ProducesWinner()
        {
            var sim = CreateBattle(LoadRole(AssassinPath), 20, LoadRole(ArcherPath), 20, seed: 42);
            BattleResult result = RunToEnd(sim);

            string winnerName = result.Winner == 0 ? "A군(암살자)"
                : result.Winner == 1 ? "B군(궁수)"
                : "무승부";
            Debug.Log($"[헤드리스 전투] 승자: {winnerName}, 생존 A={result.SurvivorsTeamA} B={result.SurvivorsTeamB}, " +
                      $"경과 {result.ElapsedTicks}틱 ({result.ElapsedTicks / 30f:F1}s)");

            Assert.AreNotEqual(BattleResult.DrawWinner, result.Winner, "암살자 vs 궁수는 한쪽 전멸로 끝나야 한다");
        }

        [Test]
        public void SameSeed_WithAssassin_ProducesSameResult()
        {
            BattleResult first = RunToEnd(CreateBattle(LoadRole(AssassinPath), 20, LoadRole(ArcherPath), 20, seed: 7));
            BattleResult second = RunToEnd(CreateBattle(LoadRole(AssassinPath), 20, LoadRole(ArcherPath), 20, seed: 7));

            Assert.AreEqual(first.Winner, second.Winner, "같은 시드는 같은 승자를 내야 한다");
            Assert.AreEqual(first.ElapsedTicks, second.ElapsedTicks, "같은 시드는 같은 틱 수로 끝나야 한다");
            Assert.AreEqual(first.SurvivorsTeamA, second.SurvivorsTeamA);
            Assert.AreEqual(first.SurvivorsTeamB, second.SurvivorsTeamB);
        }

        /// <summary>은신 규칙 검증 (1v1): 은신 중 무피해 + 은신 해제 첫 타에 치명타 배율 적용.</summary>
        [Test]
        public void Assassin_IsUntargetableWhileStealthed_And_FirstHitCrits()
        {
            RoleData assassinData = LoadRole(AssassinPath);
            RoleData archerData = LoadRole(ArcherPath);
            RoleDefinition assassin = assassinData.ToDefinition();
            RoleDefinition archer = archerData.ToDefinition();

            Assert.AreEqual(MovePattern.StealthDash, assassin.MovePattern, "암살자는 StealthDash 이동 패턴이어야 한다");
            Assert.Greater(assassin.MoveParamA, 0f, "은신 지속시간이 데이터로 정의되어야 한다");
            Assert.AreEqual(GimmickTrigger.StealthBreak, assassin.Gimmick.Trigger);
            Assert.AreEqual(GimmickEffect.NextAttackCrit, assassin.Gimmick.Effect);
            Assert.Greater(assassin.Gimmick.EffectParamA, 1f, "치명타 배율은 1보다 커야 한다");

            var sim = CreateBattle(assassinData, 1, archerData, 1, seed: 3);
            const int AssassinIndex = 0; // A군 먼저 스폰
            const int ArcherIndex = 1;

            Assert.IsTrue(sim.IsStealthed(AssassinIndex), "StealthDash 롤은 스폰 시 은신 상태여야 한다");

            float expectedFirstHit = assassin.AttackDamage * assassin.Gimmick.EffectParamA;
            bool firstHitObserved = false;
            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();

                if (sim.IsStealthed(AssassinIndex))
                {
                    Assert.AreEqual(assassin.MaxHp, sim.GetHp(AssassinIndex), "은신 중에는 피해를 받지 않아야 한다");
                }

                // 궁수의 첫 피해 = 은신 해제 첫 타 → 치명타 배율 검증 (1v1이라 다른 피해원 없음)
                if (!firstHitObserved && sim.GetHp(ArcherIndex) < archer.MaxHp)
                {
                    firstHitObserved = true;
                    Assert.AreEqual(archer.MaxHp - expectedFirstHit, sim.GetHp(ArcherIndex), 1e-3f,
                        "은신 해제 첫 타에는 치명타 배율이 적용되어야 한다");
                }
            }

            Assert.IsTrue(firstHitObserved, "암살자의 첫 타가 전투 안에 발생해야 한다");
        }

        /// <summary>독구름: 범위 내 적에게 중독 부여 + 접적 전 구간에서 도트로만 HP가 깎인다.</summary>
        [Test]
        public void PoisonCloud_AppliesPoisonDot()
        {
            RoleData warrior = LoadRole(WarriorPath);
            SkillDefinition poisonCloud = LoadSkill(PoisonCloudPath).ToDefinition();
            var sim = CreateBattle(warrior, 1, warrior, 1, seed: 5, new[] { poisonCloud });
            const int TargetIndex = 1; // B군 유닛

            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(TargetIndex)), "쿨다운 초기 상태에서 시전은 수락되어야 한다");
            Assert.IsFalse(sim.TryCastSkill(0, sim.GetPosition(TargetIndex)), "같은 슬롯 중복 예약은 거부되어야 한다");

            sim.Tick();
            Assert.IsTrue(sim.HasStatus(TargetIndex, StatusEffectType.Poison), "장판 범위의 적은 중독되어야 한다");

            // 총 2초 진행 (양군 접적 전) — HP 감소량 = 초당 도트 × 경과시간
            float maxHp = warrior.ToDefinition().MaxHp;
            int totalTicks = (int)(2f / sim.TickDeltaTime);
            for (int t = 1; t < totalTicks; t++)
            {
                sim.Tick();
            }
            float expectedDot = poisonCloud.StatusMagnitude * 2f;
            Assert.AreEqual(maxHp - expectedDot, sim.GetHp(TargetIndex), 0.5f,
                "접적 전 구간의 HP 감소는 중독 도트 총량과 일치해야 한다");
        }

        /// <summary>번개: 즉발 데미지 + 기절(행동 정지 — 기절 동안 이동 없음, 해제 후 재개).</summary>
        [Test]
        public void Lightning_DamagesAndStuns()
        {
            RoleData warrior = LoadRole(WarriorPath);
            RoleDefinition warriorDef = warrior.ToDefinition();
            SkillDefinition lightning = LoadSkill(LightningPath).ToDefinition();
            var sim = CreateBattle(warrior, 1, warrior, 1, seed: 5, new[] { lightning });
            const int TargetIndex = 1;

            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(TargetIndex)));
            sim.Tick();

            Assert.IsTrue(sim.HasStatus(TargetIndex, StatusEffectType.Stun), "번개 범위의 적은 기절해야 한다");
            Assert.AreEqual(warriorDef.MaxHp - lightning.Damage, sim.GetHp(TargetIndex), 1e-3f,
                "번개 즉발 데미지가 정확히 적용되어야 한다");

            // 기절 지속 동안 위치 불변 (1v1 원거리 상태라 분리/접촉 간섭 없음)
            System.Numerics.Vector2 stunnedPosition = sim.GetPosition(TargetIndex);
            int stunTicks = (int)(lightning.StatusDuration / sim.TickDeltaTime) - 2;
            for (int t = 0; t < stunTicks; t++)
            {
                sim.Tick();
                Assert.IsTrue(stunnedPosition == sim.GetPosition(TargetIndex), "기절 중에는 이동하지 않아야 한다");
            }

            // 기절 해제 후 이동 재개
            int resumeTicks = (int)(0.5f / sim.TickDeltaTime);
            for (int t = 0; t < resumeTicks; t++)
            {
                sim.Tick();
            }
            Assert.IsFalse(sim.HasStatus(TargetIndex, StatusEffectType.Stun), "기절은 지속시간 후 해제되어야 한다");
            Assert.IsFalse(stunnedPosition == sim.GetPosition(TargetIndex), "기절 해제 후에는 이동이 재개되어야 한다");
        }

        /// <summary>사냥꾼 갈아타기: 교전/추격 중이어도 중독 대상이 나타나면 우선순위 규칙(ReevaluateTarget)으로 타겟 교체.</summary>
        [Test]
        public void Hunter_SwitchesTargetToPoisoned()
        {
            RoleData hunter = LoadRole(HunterPath);
            RoleData warrior = LoadRole(WarriorPath);
            SkillDefinition poisonCloud = LoadSkill(PoisonCloudPath).ToDefinition();

            var armyA = new ArmyDefinition(new[]
            {
                new SquadDefinition(hunter.ToDefinition(), 1, new System.Numerics.Vector2(0f, 0f)),
            });
            var armyB = new ArmyDefinition(new[]
            {
                new SquadDefinition(warrior.ToDefinition(), 1, new System.Numerics.Vector2(0f, -5f)),
                new SquadDefinition(warrior.ToDefinition(), 1, new System.Numerics.Vector2(0f, 5f)),
            });
            var sim = new BattleSimulation(LoadConfig().ToConfig(), armyA, armyB, seed: 11, new[] { poisonCloud });
            const int HunterIndex = 0; // 1=워리어(y-5), 2=워리어(y+5)

            int initialTarget = sim.GetTargetIndex(HunterIndex);
            Assert.IsTrue(initialTarget == 1 || initialTarget == 2, "사냥꾼의 초기 타겟은 두 전사 중 하나여야 한다");
            int otherWarrior = initialTarget == 1 ? 2 : 1;

            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(otherWarrior)));

            // 재탐색 주기(0.4s) + 여유 1초 진행
            int waitTicks = (int)(1f / sim.TickDeltaTime);
            for (int t = 0; t < waitTicks; t++)
            {
                sim.Tick();
            }
            Assert.IsTrue(sim.HasStatus(otherWarrior, StatusEffectType.Poison));
            Assert.AreEqual(otherWarrior, sim.GetTargetIndex(HunterIndex),
                "사냥꾼은 중독 대상이 나타나면 그쪽으로 갈아타야 한다 (Poisoned 우선순위)");
        }

        /// <summary>사냥꾼 콤보 처형: 상태이상 대상에게 공격력 × 배율 (독구름 → 사냥꾼 콤보의 수치 검증).</summary>
        [Test]
        public void Hunter_DealsBonusDamageToStatusTarget()
        {
            RoleData hunter = LoadRole(HunterPath);
            RoleDefinition hunterDef = hunter.ToDefinition();
            RoleData warrior = LoadRole(WarriorPath);
            SkillDefinition poisonCloud = LoadSkill(PoisonCloudPath).ToDefinition();

            Assert.AreEqual(GimmickTrigger.TargetHasStatus, hunterDef.Gimmick.Trigger);
            Assert.AreEqual(GimmickEffect.DamageMultiplier, hunterDef.Gimmick.Effect);
            Assert.Greater(hunterDef.Gimmick.EffectParamA, 1f, "콤보 배율은 1보다 커야 한다");

            var sim = CreateBattle(hunter, 1, warrior, 1, seed: 5, new[] { poisonCloud });
            const int WarriorIndex = 1;

            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(WarriorIndex)));

            // 도트(틱당 소량)와 구분되는 첫 큰 피해 = 사냥꾼 화살 착탄 틱
            float previousHp = warrior.ToDefinition().MaxHp;
            bool bonusHitObserved = false;
            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
                float hp = sim.GetHp(WarriorIndex);
                float drop = previousHp - hp;
                if (drop > 5f)
                {
                    Assert.IsTrue(sim.HasStatus(WarriorIndex, StatusEffectType.Poison),
                        "착탄 시점에 대상이 중독 상태여야 콤보 검증이 유효하다");
                    float expected = hunterDef.AttackDamage * hunterDef.Gimmick.EffectParamA
                                     + poisonCloud.StatusMagnitude * sim.TickDeltaTime;
                    Assert.AreEqual(expected, drop, 0.05f,
                        "상태이상 대상 첫 타 = 공격력 × 배율 (+해당 틱 도트)이어야 한다");
                    bonusHitObserved = true;
                    break;
                }
                previousHp = hp;
            }
            Assert.IsTrue(bonusHitObserved, "사냥꾼의 첫 타가 중독 대상에게 발생해야 한다");
        }

        /// <summary>
        /// 화상 = 3종째 상태이상이 데이터+enum 수준임을 증명: 시뮬에 Burn 전용 코드 없이
        /// SkillDefinition 데이터만으로 도트가 동작한다. + 스킬 시전 포함 결정론 확인.
        /// </summary>
        [Test]
        public void Burn_WorksViaDataOnly_AndSkillCastsAreDeterministic()
        {
            RoleData warrior = LoadRole(WarriorPath);
            float maxHp = warrior.ToDefinition().MaxHp;
            var burnSkill = new SkillDefinition(
                "BurnTest", cooldown: 5f, radius: 3f, damage: 0f,
                StatusEffectType.Burn, statusDuration: 2f, statusMagnitude: 10f,
                zoneDuration: 0f);

            var sim = CreateBattle(warrior, 1, warrior, 1, seed: 9, new[] { burnSkill });
            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(1)));
            int ticks = (int)(1f / sim.TickDeltaTime);
            for (int t = 0; t < ticks; t++)
            {
                sim.Tick();
            }
            Assert.IsTrue(sim.HasStatus(1, StatusEffectType.Burn), "화상이 데이터만으로 부여되어야 한다");
            Assert.AreEqual(maxHp - burnSkill.StatusMagnitude * 1f, sim.GetHp(1), 0.5f,
                "화상 도트가 중독과 같은 공용 경로로 동작해야 한다");

            // 결정론: 같은 시드 + 같은 틱의 시전 → 같은 결과
            BattleResult first = RunBurnBattle(warrior, burnSkill);
            BattleResult second = RunBurnBattle(warrior, burnSkill);
            Assert.AreEqual(first.Winner, second.Winner, "스킬 시전 포함 같은 시드는 같은 승자를 내야 한다");
            Assert.AreEqual(first.ElapsedTicks, second.ElapsedTicks);
            Assert.AreEqual(first.SurvivorsTeamA, second.SurvivorsTeamA);
            Assert.AreEqual(first.SurvivorsTeamB, second.SurvivorsTeamB);
        }

        private static BattleResult RunBurnBattle(RoleData warrior, SkillDefinition burnSkill)
        {
            var sim = CreateBattle(warrior, 5, warrior, 5, seed: 9, new[] { burnSkill });
            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(5)));
            return RunToEnd(sim);
        }
    }
}
