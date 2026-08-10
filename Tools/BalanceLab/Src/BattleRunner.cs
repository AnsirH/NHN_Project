using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NHN.Simulation.Battle;

namespace BalanceLab
{
    public sealed class SquadRecord
    {
        public string squadId;
        /// <summary>
        /// 초기 병력 수. 생존자만 기록하면 "0명 생존"의 분모를 알 수 없어 전멸 시나리오의 요약이
        /// 0/0이 된다 — 손실률을 읽으려면 시작 수가 결과에 함께 있어야 한다.
        /// </summary>
        public int soldiers;
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
        /// <summary>프로젝트 루트 기준 시나리오 파일 경로 — 뷰어가 재현 명령을 조립하는 재료다.</summary>
        public string scenarioPath;
        /// <summary>--tag 라벨 (정본 실행이면 null). 결과 파일명과 리플레이 키의 일부다.</summary>
        public string tag;
        /// <summary>
        /// 적용된 오버라이드 파일의 원문 (없으면 null). 결과 파일만 보고 "무슨 수치로 돈 판인지"를
        /// 항상 복원할 수 있어야 한다 — 이 필드가 그 계약이다.
        /// </summary>
        public System.Text.Json.Nodes.JsonNode appliedOverrides;
        /// <summary>오버라이드 파일 경로 (없으면 null) — 재현 명령을 만들 때 쓴다.</summary>
        public string appliedOverridesPath;
        /// <summary>궤적이 덤프된 시드 목록 (--replay). 뷰어가 재생 가능한 시드를 아는 경로다.</summary>
        public List<int> replaySeeds = new List<int>();
        /// <summary>목표 밴드 태그 (bands.json의 id).</summary>
        public string band;
        /// <summary>밴드 판정 결과 — PASS/FAIL과 어긋난 수치.</summary>
        public BandVerdict verdict;
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
        /// <summary>
        /// replaySeeds에 든 시드만 궤적을 기록해 onReplay로 넘긴다 (전 시드 덤프는 없다 — 용량 설계).
        /// 기록기는 시뮬 바깥에서 공개 접근자만 읽으므로, 덤프 여부가 전투 결과를 바꾸지 않는다.
        /// </summary>
        public static ScenarioResult Run(
            Scenario scenario, AssetRepository repository, BandTable bands,
            ICollection<int> replaySeeds = null, System.Action<ReplayData> onReplay = null)
        {
            BattleConfig config = repository.LoadBattleConfig();
            ArmyDefinition left = BuildArmy(scenario.left, repository, config);
            ArmyDefinition right = BuildArmy(scenario.right, repository, config);
            BandRule bandRule = bands.Get(scenario.band); // 미등록 태그는 여기서 즉시 실패

            var result = new ScenarioResult
            {
                scenarioName = scenario.name,
                band = bandRule.id,
                runs = scenario.runs,
                battles = new List<BattleRecord>(scenario.runs),
            };

            var stopwatch = Stopwatch.StartNew();
            for (int seed = 0; seed < scenario.runs; seed++)
            {
                var sim = new BattleSimulation(config, left, right, seed);
                ReplayRecorder recorder = null;
                if (replaySeeds != null && replaySeeds.Contains(seed))
                {
                    recorder = new ReplayRecorder(sim, config, scenario.name, null, seed);
                    recorder.DescribeSquads(scenario.left, scenario.right);
                }

                int safetyTicks = 1_000_000; // 시뮬 자체 시간 상한(무승부)보다 넉넉한 하드 스톱
                while (!sim.Finished && safetyTicks-- > 0)
                {
                    sim.Tick();
                    recorder?.AfterTick(sim);
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

                var record = new BattleRecord
                {
                    seed = seed,
                    winner = winner,
                    ticks = battle.ElapsedTicks,
                    leftSquads = CollectSquadRecords(sim, scenario.left, squadIndexOffset: 0),
                    rightSquads = CollectSquadRecords(sim, scenario.right, squadIndexOffset: scenario.left.Count),
                };
                result.battles.Add(record);

                if (recorder != null)
                {
                    onReplay?.Invoke(recorder.Finish(sim, record)); // 자기 교차 검증은 Finish 안에서 걸린다
                    result.replaySeeds.Add(seed);
                }
            }
            stopwatch.Stop();

            result.elapsedMs = stopwatch.ElapsedMilliseconds;
            result.leftWinRate = (double)result.leftWins / scenario.runs;
            result.rightWinRate = (double)result.rightWins / scenario.runs;
            result.verdict = BandTable.Judge(bandRule, result.leftWinRate);
            return result;
        }

        private static ArmyDefinition BuildArmy(List<SquadEntry> entries, AssetRepository repository, in BattleConfig config)
        {
            var squads = new SquadDefinition[entries.Count];
            for (int s = 0; s < entries.Count; s++)
            {
                SquadEntry entry = entries[s];
                squads[s] = new SquadDefinition(
                    repository.GetRole(entry.roleId),
                    entry.soldierCount,
                    DeploymentGrid.SlotToAnchor(entry.slotX, entry.slotY, config.DeploymentDepth, config.DeploymentHalfWidth),
                    repository.GetGeneral(entry.generalId));
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
                    soldiers = entries[s].soldierCount,
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
