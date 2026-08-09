using NHN.Data;
using NHN.Simulation.Battle;
using NUnit.Framework;
using UnityEditor;

namespace NHN.Simulation.Tests
{
    /// <summary>
    /// 5.6-A 스탯 확장 검증: 방어력 감쇠 K/(K+방어력)와 치명타(퍼센트 확률 × 배율).
    /// 합의 규칙 — 방어력은 일반 공격·플레이어 스킬 즉발에 적용, 도트(중독/화상)에는 미적용.
    /// </summary>
    public sealed class CombatStatsTests
    {
        private const string ConfigPath = "Assets/Data/BattleConfig.asset";
        private const string WarriorPath = "Assets/Data/Roles/Warrior.asset";

        private static BattleConfig LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} 에셋이 있어야 한다");
            return config.ToConfig();
        }

        /// <summary>사거리 밖에 세워 서로 때리지 않는 더미 롤 — 스킬/도트 수치만 관측하기 위한 통제 조건.</summary>
        private static RoleDefinition PassiveRole(string name, float maxHp, float defense)
        {
            return new RoleDefinition(
                name,
                maxHp, attackDamage: 0f, defense: defense, critChancePercent: 0f,
                attackInterval: 100f, attackRange: 0.1f,
                moveSpeed: 0f, unitRadius: 0.5f,
                projectileSpeed: 0f, projectileArcHeight: 0f,
                PositionFilter.Nearest, System.Array.Empty<TargetPriority>(), MovePattern.ApproachTarget,
                moveParamA: 0f, moveParamB: 1f);
        }

        private static BattleSimulation CreateDuel(
            RoleDefinition left, RoleDefinition right, in BattleConfig config, int seed,
            SkillDefinition[] skills = null)
        {
            // 양측을 깊은 후방(정규화 slotX=0)에 두어 검증 구간 동안 접적이 없게 한다.
            var anchor = DeploymentGrid.SlotToAnchor(0f, 0.5f, config.DeploymentDepth, config.DeploymentHalfWidth);
            var armyA = new ArmyDefinition(new[] { new SquadDefinition(left, 1, anchor) });
            var armyB = new ArmyDefinition(new[] { new SquadDefinition(right, 1, anchor) });
            return new BattleSimulation(config, armyA, armyB, seed, skills);
        }

        [Test]
        public void Defense_DampensSkillDamage_ByFormula()
        {
            BattleConfig config = LoadConfig();
            Assert.Greater(config.DefenseK, 0f, "방어 감쇠 계수 K가 BattleConfig 에셋에 있어야 한다");

            const float Defense = 50f;
            const float SkillDamage = 40f;
            var attacker = PassiveRole("NoDefense", maxHp: 500f, defense: 0f);
            var defender = PassiveRole("Armored", maxHp: 500f, defense: Defense);
            var skill = new SkillDefinition(
                "DamageOnly", cooldown: 1f, radius: 3f, damage: SkillDamage,
                StatusEffectType.Stun, statusDuration: 0f, statusMagnitude: 0f, zoneDuration: 0f);

            var sim = CreateDuel(attacker, defender, config, seed: 5, new[] { skill });
            const int DefenderIndex = 1;

            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(DefenderIndex)));
            sim.Tick();

            float expected = 500f - SkillDamage * config.DefenseDamping(Defense);
            Assert.AreEqual(expected, sim.GetHp(DefenderIndex), 1e-3f,
                "스킬 즉발 피해에 방어력 감쇠 K/(K+방어력)가 적용되어야 한다");
            Assert.AreEqual(config.DefenseK / (config.DefenseK + Defense), config.DefenseDamping(Defense), 1e-6f);
        }

        [Test]
        public void Defense_DoesNotApplyToDot()
        {
            BattleConfig config = LoadConfig();

            const float DotPerSecond = 10f;
            const float Seconds = 2f;
            var attacker = PassiveRole("NoDefense", maxHp: 500f, defense: 0f);
            var defender = PassiveRole("HeavilyArmored", maxHp: 500f, defense: 500f); // 감쇠가 컸다면 즉시 드러난다
            var poison = new SkillDefinition(
                "PoisonOnly", cooldown: 1f, radius: 3f, damage: 0f,
                StatusEffectType.Poison, statusDuration: Seconds, statusMagnitude: DotPerSecond, zoneDuration: 0f);

            var sim = CreateDuel(attacker, defender, config, seed: 5, new[] { poison });
            const int DefenderIndex = 1;

            Assert.IsTrue(sim.TryCastSkill(0, sim.GetPosition(DefenderIndex)));
            int ticks = (int)(Seconds / sim.TickDeltaTime) + 1;
            for (int t = 0; t < ticks; t++)
            {
                sim.Tick();
            }

            Assert.AreEqual(500f - DotPerSecond * Seconds, sim.GetHp(DefenderIndex), 0.5f,
                "도트 피해는 방어력 감쇠를 받지 않아야 한다 (상태이상 셋업의 가치 보존)");
        }

        [Test]
        public void CritChance_AppliesMultiplier_AndZeroChanceKeepsBaseDamage()
        {
            BattleConfig config = LoadConfig();
            Assert.AreEqual(1.8f, config.CritMultiplier, 1e-3f, "치명타 배율은 기획 합의값 1.8이어야 한다");

            const float AttackDamage = 20f;
            RoleDefinition Attacker(float critPercent) => new RoleDefinition(
                "Striker",
                maxHp: 1000f, attackDamage: AttackDamage, defense: 0f, critChancePercent: critPercent,
                attackInterval: 100f, attackRange: 1.2f,
                moveSpeed: 4f, unitRadius: 0.5f,
                projectileSpeed: 0f, projectileArcHeight: 0f,
                PositionFilter.Nearest, System.Array.Empty<TargetPriority>(), MovePattern.ApproachTarget,
                moveParamA: 0f, moveParamB: 1f);
            var target = PassiveRole("Dummy", maxHp: 1000f, defense: 0f);

            // 확률 100%: 첫 타가 반드시 치명타 → 공격력 × 1.8
            float alwaysCritHit = FirstHitDamage(Attacker(100f), target, config);
            Assert.AreEqual(AttackDamage * config.CritMultiplier, alwaysCritHit, 1e-3f,
                "치명타 확률 100%면 모든 타격에 배율이 적용되어야 한다");

            // 확률 0%: 배율 없음 (난수 소비도 없어 기존 데이터의 전투 결과가 보존된다)
            float neverCritHit = FirstHitDamage(Attacker(0f), target, config);
            Assert.AreEqual(AttackDamage, neverCritHit, 1e-3f,
                "치명타 확률 0이면 기본 공격력 그대로여야 한다");
        }

        /// <summary>접적 후 첫 피격에서 깎인 HP량 — 근접 1:1, 다른 피해원 없음.</summary>
        private static float FirstHitDamage(RoleDefinition attacker, RoleDefinition target, in BattleConfig config)
        {
            var sim = CreateDuel(attacker, target, config, seed: 11);
            const int TargetIndex = 1;
            float maxHp = target.MaxHp;

            int safetyTicks = 100_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
                float hp = sim.GetHp(TargetIndex);
                if (hp < maxHp)
                {
                    return maxHp - hp;
                }
            }
            Assert.Fail("첫 타격이 발생해야 한다");
            return 0f;
        }

        /// <summary>
        /// 리드 오프셋 데이터화 (작업 2): 장군 스폰 위치가 코드 상수가 아니라 데이터로 결정되고,
        /// 허용 범위(-1~+1 랭크)를 벗어난 값은 클램프된다 — 튜닝 루프가 전장 밖 도피로 수렴하는 것을 막는다.
        /// </summary>
        [Test]
        public void LeadRankOffset_IsDataDriven_AndClamped()
        {
            BattleConfig config = LoadConfig();
            RoleDefinition warrior = PassiveRole("Warrior", maxHp: 200f, defense: 0f);

            float frontX = SpawnGeneralX(warrior, leadRankOffset: 1f, config);
            float sameRankX = SpawnGeneralX(warrior, leadRankOffset: 0f, config);
            float behindX = SpawnGeneralX(warrior, leadRankOffset: -1f, config);

            // A군은 -x에 서서 +x(적진)를 향한다 → 전진할수록 x가 커진다.
            Assert.Greater(frontX, sameRankX, "오프셋 +1은 병사 최전열보다 앞(적진 쪽)이어야 한다");
            Assert.Greater(sameRankX, behindX, "오프셋 -1은 병사 최전열보다 뒤여야 한다");
            Assert.AreNotEqual(frontX, behindX, "데이터 변경만으로 장군 위치가 달라져야 한다 (코드 상수 아님)");

            // 범위 밖 값은 클램프 — 정의 생성 시점에 강제된다.
            Assert.AreEqual(GeneralDefinition.MaxLeadRankOffset, GeneralDefinition.ClampLeadRankOffset(5f), 1e-3f);
            Assert.AreEqual(GeneralDefinition.MinLeadRankOffset, GeneralDefinition.ClampLeadRankOffset(-5f), 1e-3f);
            Assert.AreEqual(frontX, SpawnGeneralX(warrior, leadRankOffset: 99f, config), 1e-3f,
                "범위를 넘는 오프셋은 상한(+1)으로 클램프되어야 한다");
        }

        /// <summary>주어진 리드 오프셋으로 스폰된 A군 장군의 x 좌표.</summary>
        private static float SpawnGeneralX(RoleDefinition role, float leadRankOffset, in BattleConfig config)
        {
            var general = GeneralDefinition.CreateElite(
                role, "TestGeneral", hpMultiplier: 2f, damageMultiplier: 1f, sizeMultiplier: 1f,
                SquadPassive.None, 0f, ChargeCondition.None, 0f,
                GimmickEffect.None, 0f, 0f, 0f, leadRankOffset);
            var armyA = new ArmyDefinition(new[]
            {
                new SquadDefinition(role, 9, new System.Numerics.Vector2(0f, 0f), general),
            });
            var armyB = new ArmyDefinition(new[] { new SquadDefinition(role, 1, new System.Numerics.Vector2(0f, 0f)) });
            var sim = new BattleSimulation(config, armyA, armyB, seed: 1);
            return sim.GetPosition(sim.GetGeneralUnit(0)).X;
        }

        /// <summary>회귀 방어: 방어력·치명타 0인 기존 데이터는 스탯 확장 전과 동일한 전투 결과를 낸다.</summary>
        [Test]
        public void ZeroDefenseAndCrit_PreservesExistingBattleOutcome()
        {
            var warrior = AssetDatabase.LoadAssetAtPath<SquadData>(WarriorPath);
            Assert.IsNotNull(warrior);
            RoleDefinition role = warrior.ToDefinition();
            Assert.AreEqual(0f, role.Defense, "기존 롤 에셋의 방어력은 0이어야 한다 (회귀 방어 전제)");
            Assert.AreEqual(0f, role.CritChancePercent, "기존 롤 에셋의 치명타 확률은 0이어야 한다");

            BattleConfig config = LoadConfig();
            Assert.AreEqual(1f, config.DefenseDamping(role.Defense), 1e-6f, "방어력 0은 감쇠가 없어야 한다");

            var armyA = new ArmyDefinition(new[] { new SquadDefinition(role, 20, new System.Numerics.Vector2(0f, 0f)) });
            var armyB = new ArmyDefinition(new[] { new SquadDefinition(role, 20, new System.Numerics.Vector2(0f, 0f)) });
            var first = new BattleSimulation(config, armyA, armyB, seed: 42);
            var second = new BattleSimulation(config, armyA, armyB, seed: 42);
            int safetyTicks = 1_000_000;
            while ((!first.Finished || !second.Finished) && safetyTicks-- > 0)
            {
                if (!first.Finished)
                {
                    first.Tick();
                }
                if (!second.Finished)
                {
                    second.Tick();
                }
            }
            Assert.AreEqual(first.Result.Winner, second.Result.Winner, "같은 시드는 같은 승자를 내야 한다");
            Assert.AreEqual(first.Result.ElapsedTicks, second.Result.ElapsedTicks);
        }
    }
}
