using System.Collections.Generic;
using System.IO;
using NHN.Data;
using NHN.Simulation.Battle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.Simulation.Tests
{
    /// <summary>
    /// BalanceLab 크로스 런타임 교차 검증 (작업 1 — 완화 계약 승인본).
    ///
    /// 정본 분리 원칙:
    ///   · 밸런싱 통계(승률/분포)의 정본 = CLI(.NET)
    ///   · 시드 리플레이(눈 검증)의 정본 = 에디터 자체 시뮬(Mono)
    /// 두 런타임은 JIT float 정밀도 차이(Mono의 확장 정밀도 중간값 허용 — ECMA CIL 표준 동작)로
    /// 비트 단위 일치가 불가함이 실측으로 확인됐다 (FMA/인트린식 비활성화 실험으로 소거 — ai_usage_log 참조).
    /// 따라서 이 테스트는 결정론이 아니라 두 정본의 "통계적 등가성"을 감시한다:
    ///   승자 불일치 = 즉시 FAIL (무관용) / 종료 틱 오차 = CLI 기록 대비 상대 ±20% / 시드 100개.
    /// 오차가 절대 틱이 아니라 상대(%)인 이유: 드리프트는 나비효과(분기 후 궤적 발산)라 전투 길이에
    /// 비례한다 — 실측 분포(승자 뒤집힘 0/200, 최대 상대 드리프트 15.3%)에 근거해 ±20%로 확정.
    /// "100시드 승자 뒤집힘 0건"이 이 계약의 신뢰 근거다. 고정소수점 전환(완전 결정론)은 잼 이후 과제.
    /// 작업 4(시드 리플레이) 완료 기준도 같은 계약을 따른다: 승자 일치 + 종료 틱 상대 ±20%.
    /// 작업 2에서 미러 매치업 시나리오가 생기면 교차 검증 대상에 추가한다 (박빙 판의 승자 안정성 스트레스).
    /// </summary>
    public sealed class BalanceLabCrossCheckTests
    {
        private const int CrossCheckSeeds = 100;
        private const float TickTolerancePercent = 0.20f;

        // JsonUtility 파싱용 DTO — CLI 시나리오/결과 JSON과 필드명 일치가 계약이다.
        [System.Serializable]
        private sealed class ScenarioDto
        {
            public string name;
            public int runs;
            public List<SquadRequest> left;
            public List<SquadRequest> right;
        }

        [System.Serializable]
        private sealed class ResultDto
        {
            public string scenarioName;
            public List<BattleRecordDto> battles;
        }

        [System.Serializable]
        private sealed class BattleRecordDto
        {
            public int seed;
            public string winner;
            public int ticks;
        }

        // 레벨 확인 전용 DTO — JsonUtility는 선언된 필드만 읽으므로 나머지는 무시된다.
        [System.Serializable]
        private sealed class LevelProbeDto
        {
            public string squadId;
            public int level;
        }

        [System.Serializable]
        private sealed class LevelScenarioDto
        {
            public System.Collections.Generic.List<LevelProbeDto> left;
            public System.Collections.Generic.List<LevelProbeDto> right;
        }

        /// <summary>
        /// 교차 검증 대상 시나리오는 레벨 0이어야 한다.
        /// 레벨은 BalanceLab 전용 대역(CLI가 CSV에서 스탯을 조회하는 키)이라 에디터 경로에는 없다 —
        /// 레벨이 0이 아니면 두 쪽이 서로 다른 스탯으로 싸우게 되어 비교 자체가 무의미해진다.
        /// </summary>
        private static void RequireLevelZero(string scenarioJson, string scenarioName)
        {
            var probe = JsonUtility.FromJson<LevelScenarioDto>(scenarioJson);
            AssertSideLevelZero(probe.left, "left", scenarioName);
            AssertSideLevelZero(probe.right, "right", scenarioName);
        }

        private static void AssertSideLevelZero(
            System.Collections.Generic.List<LevelProbeDto> side, string label, string scenarioName)
        {
            if (side == null)
            {
                return;
            }
            foreach (LevelProbeDto squad in side)
            {
                Assert.AreEqual(0, squad.level,
                    $"{scenarioName}/{label}/{squad.squadId}: 교차 검증 시나리오는 레벨 0이어야 한다 " +
                    "(레벨은 CLI 전용 스탯 조회 키라 에디터 경로가 재현할 수 없다)");
            }
        }

        [TestCase("warrior_general_vs_plain")]
        [TestCase("archer_general_volley_cross")]
        public void CliAndEditor_ProduceIdenticalResults(string scenarioName)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string scenarioPath = Path.Combine(projectRoot, "Tools", "BalanceLab", "scenarios", scenarioName + ".json");
            string resultPath = Path.Combine(projectRoot, "Tools", "BalanceLab", "results", scenarioName + ".json");
            Assert.IsTrue(File.Exists(scenarioPath), $"시나리오 파일이 있어야 한다: {scenarioPath}");
            if (!File.Exists(resultPath))
            {
                Assert.Inconclusive(
                    $"CLI 결과가 없습니다 — 먼저 실행: dotnet run --project Tools/BalanceLab -- Tools/BalanceLab/scenarios/{scenarioName}.json");
            }

            string scenarioJson = File.ReadAllText(scenarioPath);
            RequireLevelZero(scenarioJson, scenarioName);
            var scenario = JsonUtility.FromJson<ScenarioDto>(scenarioJson);
            var cliResult = JsonUtility.FromJson<ResultDto>(File.ReadAllText(resultPath));
            Assert.AreEqual(scenarioName, cliResult.scenarioName, "결과 파일이 같은 시나리오의 것이어야 한다");
            Assert.GreaterOrEqual(cliResult.battles.Count, CrossCheckSeeds, $"교차 검증에는 최소 {CrossCheckSeeds}판 기록이 필요하다");

            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/Data/BattleCatalog.asset");
            var configAsset = AssetDatabase.LoadAssetAtPath<BattleConfigSO>("Assets/Data/BattleConfig.asset");
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(configAsset);
            BattleConfig config = configAsset.ToConfig();

            // 전 시드를 수집한 뒤 일괄 판정 — 첫 실패에서 멈추면 드리프트 분포(계약의 신뢰 근거)를 못 본다.
            int winnerFlips = 0;
            int maxDrift = 0;
            float maxRelativeDrift = 0f;
            int tickViolations = 0;
            var violations = new List<string>();
            for (int seed = 0; seed < CrossCheckSeeds; seed++)
            {
                ArmyDefinition left = BattleRequestBuilder.BuildSquads(scenario.left, catalog, config, null, null);
                ArmyDefinition right = BattleRequestBuilder.BuildSquads(scenario.right, catalog, config, null, null);
                var sim = new BattleSimulation(config, left, right, seed);
                int safetyTicks = 1_000_000;
                while (!sim.Finished && safetyTicks-- > 0)
                {
                    sim.Tick();
                }
                Assert.IsTrue(sim.Finished, $"시드 {seed}: 전투가 종료되어야 한다");

                string winner = sim.Result.Winner == 0 ? "left"
                    : sim.Result.Winner == 1 ? "right"
                    : "draw";
                BattleRecordDto cliRecord = cliResult.battles[seed]; // 시드 0..N-1 고정 계약 — 인덱스 = 시드
                Assert.AreEqual(seed, cliRecord.seed, "CLI 기록은 시드 0..N-1 순서여야 한다");

                if (cliRecord.winner != winner)
                {
                    winnerFlips++;
                    violations.Add($"시드 {seed}: 승자 CLI={cliRecord.winner} vs 에디터={winner}");
                }
                int drift = System.Math.Abs(cliRecord.ticks - sim.Result.ElapsedTicks);
                float relativeDrift = cliRecord.ticks > 0 ? drift / (float)cliRecord.ticks : 0f;
                if (drift > maxDrift)
                {
                    maxDrift = drift;
                }
                if (relativeDrift > maxRelativeDrift)
                {
                    maxRelativeDrift = relativeDrift;
                }
                if (relativeDrift > TickTolerancePercent)
                {
                    tickViolations++;
                    violations.Add($"시드 {seed}: 틱 CLI={cliRecord.ticks} vs 에디터={sim.Result.ElapsedTicks} " +
                                   $"(차 {drift}, 상대 {relativeDrift:P1})");
                }
            }

            Debug.Log($"[교차 검증] {scenarioName}: {CrossCheckSeeds}시드 — 승자 뒤집힘 {winnerFlips}건, " +
                      $"최대 틱 드리프트 {maxDrift}틱 (상대 {maxRelativeDrift:P1}, 허용 ±{TickTolerancePercent:P0}), " +
                      $"허용 초과 {tickViolations}건");
            Assert.AreEqual(0, winnerFlips,
                $"승자 불일치 무관용 FAIL — 통계 정본과 리플레이 정본의 등가성 훼손:\n{string.Join("\n", violations)}");
            Assert.AreEqual(0, tickViolations,
                $"종료 틱 상대 드리프트가 허용 오차(±{TickTolerancePercent:P0})를 넘었다:\n{string.Join("\n", violations)}");
        }
    }
}
