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
        private const string ConfigPath = "Assets/Data/BattleConfig.asset";

        private static BattleSimulation CreateWarrior20VsArcher20(int seed)
        {
            var warrior = AssetDatabase.LoadAssetAtPath<RoleData>(WarriorPath);
            var archer = AssetDatabase.LoadAssetAtPath<RoleData>(ArcherPath);
            var config = AssetDatabase.LoadAssetAtPath<BattleConfigSO>(ConfigPath);
            Assert.IsNotNull(warrior, $"{WarriorPath} 에셋이 있어야 한다");
            Assert.IsNotNull(archer, $"{ArcherPath} 에셋이 있어야 한다");
            Assert.IsNotNull(config, $"{ConfigPath} 에셋이 있어야 한다");

            var armyA = new ArmyDefinition(new[]
            {
                new SquadDefinition(warrior.ToDefinition(), 20, new System.Numerics.Vector2(0f, 0f)),
            });
            var armyB = new ArmyDefinition(new[]
            {
                new SquadDefinition(archer.ToDefinition(), 20, new System.Numerics.Vector2(0f, 0f)),
            });
            return new BattleSimulation(config.ToConfig(), armyA, armyB, seed);
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
            var sim = CreateWarrior20VsArcher20(seed: 42);
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
            BattleResult first = RunToEnd(CreateWarrior20VsArcher20(seed: 7));
            BattleResult second = RunToEnd(CreateWarrior20VsArcher20(seed: 7));

            Assert.AreEqual(first.Winner, second.Winner, "같은 시드는 같은 승자를 내야 한다");
            Assert.AreEqual(first.ElapsedTicks, second.ElapsedTicks, "같은 시드는 같은 틱 수로 끝나야 한다");
            Assert.AreEqual(first.SurvivorsTeamA, second.SurvivorsTeamA);
            Assert.AreEqual(first.SurvivorsTeamB, second.SurvivorsTeamB);
        }
    }
}
