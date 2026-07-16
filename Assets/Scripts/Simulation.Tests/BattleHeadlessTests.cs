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

        /// <summary>단일 분대 vs 단일 분대 전투 생성. 유닛 인덱스는 A군(0..countA-1) → B군 순.</summary>
        private static BattleSimulation CreateBattle(RoleData roleA, int countA, RoleData roleB, int countB, int seed)
        {
            var armyA = new ArmyDefinition(new[]
            {
                new SquadDefinition(roleA.ToDefinition(), countA, new System.Numerics.Vector2(0f, 0f)),
            });
            var armyB = new ArmyDefinition(new[]
            {
                new SquadDefinition(roleB.ToDefinition(), countB, new System.Numerics.Vector2(0f, 0f)),
            });
            return new BattleSimulation(LoadConfig().ToConfig(), armyA, armyB, seed);
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
    }
}
