using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NHN.Simulation.Battle;

namespace BalanceLab
{
    public sealed class SquadRecord
    {
        public string squadId;
        /// <summary>이 분대의 레벨 (스탯 출처 추적용 — 리포트에서 레벨 스윕 결과를 구분한다).</summary>
        public int level;
        public int survivors;
        public bool hasGeneral;
        public bool generalAlive;
        public int activations;
    }

    public sealed class BattleRecord
    {
        public int seed;
        /// <summary>"left" | "right" | "draw" — 에디터 교차 검증(BalanceLabCrossCheckTests)과의 계약.</summary>
        public string winner;
        public int ticks;
        public List<SquadRecord> leftSquads;
        public List<SquadRecord> rightSquads;
    }

    public sealed class ScenarioResult
    {
        public string scenarioName;
        public int runs;
        public int leftWins;
        public int rightWins;
        public int draws;
        public double leftWinRate;
        public double rightWinRate;
        public long elapsedMs;
        public List<BattleRecord> battles;
    }

    /// <summary>
    /// 시드 0..N-1 고정으로 N판 실행 — 리포트의 모든 시드는 언제나 재현 가능 (작업 명세 v2).
    /// 매 판 새 BattleSimulation 인스턴스 (판 간 상태 공유 금지). 정의 객체는 불변(readonly)이라 공유한다.
    /// 병렬화 없음.
    /// </summary>
    public static class BattleRunner
    {
        public static ScenarioResult Run(Scenario scenario, AssetRepository repository)
        {
            BattleConfig config = repository.LoadBattleConfig();
            ArmyDefinition left = BuildArmy(scenario.left, repository, config);
            ArmyDefinition right = BuildArmy(scenario.right, repository, config);

            var result = new ScenarioResult
            {
                scenarioName = scenario.name,
                runs = scenario.runs,
                battles = new List<BattleRecord>(scenario.runs),
            };

            var stopwatch = Stopwatch.StartNew();
            for (int seed = 0; seed < scenario.runs; seed++)
            {
                var sim = new BattleSimulation(config, left, right, seed);
                int safetyTicks = 1_000_000; // 시뮬 자체 시간 상한(무승부)보다 넉넉한 하드 스톱
                while (!sim.Finished && safetyTicks-- > 0)
                {
                    sim.Tick();
                }
                if (!sim.Finished)
                {
                    throw new InvalidDataException($"시드 {seed}: 전투가 하드 스톱까지 끝나지 않았다 — 시뮬 시간 상한 확인");
                }

                BattleResult battle = sim.Result;
                string winner = battle.Winner == 0 ? "left"
                    : battle.Winner == 1 ? "right"
                    : "draw";
                if (battle.Winner == 0)
                {
                    result.leftWins++;
                }
                else if (battle.Winner == 1)
                {
                    result.rightWins++;
                }
                else
                {
                    result.draws++;
                }

                result.battles.Add(new BattleRecord
                {
                    seed = seed,
                    winner = winner,
                    ticks = battle.ElapsedTicks,
                    leftSquads = CollectSquadRecords(sim, scenario.left, squadIndexOffset: 0),
                    rightSquads = CollectSquadRecords(sim, scenario.right, squadIndexOffset: scenario.left.Count),
                });
            }
            stopwatch.Stop();

            result.elapsedMs = stopwatch.ElapsedMilliseconds;
            result.leftWinRate = (double)result.leftWins / scenario.runs;
            result.rightWinRate = (double)result.rightWins / scenario.runs;
            return result;
        }

        private static ArmyDefinition BuildArmy(List<SquadEntry> entries, AssetRepository repository, in BattleConfig config)
        {
            var squads = new SquadDefinition[entries.Count];
            for (int s = 0; s < entries.Count; s++)
            {
                SquadEntry entry = entries[s];
                squads[s] = new SquadDefinition(
                    repository.GetRole(entry.roleId, entry.level),
                    entry.soldierCount,
                    DeploymentGrid.SlotToAnchor(entry.slotX, entry.slotY, config.DeploymentDepth, config.DeploymentHalfWidth),
                    repository.GetGeneral(entry.generalId, entry.level));
            }
            return new ArmyDefinition(squads);
        }

        /// <summary>분대 인덱스 계약: 좌군 분대 0..L-1, 우군 L.. (ArmyDefinition 스폰 순서와 동일).</summary>
        private static List<SquadRecord> CollectSquadRecords(BattleSimulation sim, List<SquadEntry> entries, int squadIndexOffset)
        {
            var records = new List<SquadRecord>(entries.Count);
            for (int s = 0; s < entries.Count; s++)
            {
                int squadIndex = squadIndexOffset + s;
                records.Add(new SquadRecord
                {
                    squadId = entries[s].squadId,
                    level = entries[s].level,
                    survivors = sim.CountSquadSurvivors(squadIndex),
                    hasGeneral = sim.GetGeneralUnit(squadIndex) >= 0,
                    generalAlive = sim.IsGeneralAlive(squadIndex),
                    activations = sim.GetSquadActivationCount(squadIndex),
                });
            }
            return records;
        }
    }
}
