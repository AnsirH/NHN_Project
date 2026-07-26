using System;
using System.Collections.Generic;
using System.IO;

namespace BalanceLab
{
    /// <summary>
    /// BalanceLab CLI 러너 — 사용법: dotnet run -- &lt;scenario.json | scenarios/all.json&gt;
    /// [시나리오 JSON] + [.asset 인게임 속성 + CSV 레벨별 5스탯] → 시드 0..N-1 N판 → 결과 JSON + 밴드 PASS/FAIL.
    /// 세트 파일(scenarios 배열)을 주면 전체 매트릭스를 한 번에 돌리고 요약을 출력한다.
    /// 종료 코드: 0 = 전 시나리오 PASS, 1 = 오류, 2 = 사용법 오류, 3 = 밴드 FAIL 존재.
    /// </summary>
    public static class Program
    {
        private const int ExitOk = 0;
        private const int ExitError = 1;
        private const int ExitUsage = 2;
        private const int ExitBandFailed = 3;

        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("사용법: dotnet run -- <scenario.json | scenarios/all.json>");
                return ExitUsage;
            }

            try
            {
                string inputPath = Path.GetFullPath(args[0]);
                if (!File.Exists(inputPath))
                {
                    Console.Error.WriteLine($"파일이 없다: {inputPath}");
                    return ExitUsage;
                }
                string projectRoot = FindUnityProjectRoot(inputPath);

                // guid→경로 인덱스는 캐싱 없이 매 실행 재구축 (작업 명세 v2 추가 조건)
                var repository = new AssetRepository(projectRoot);
                BandTable bands = BandTable.Load(Path.Combine(projectRoot, "Tools", "BalanceLab", "bands.json"));
                string resultsDirectory = Path.Combine(projectRoot, "Tools", "BalanceLab", "results");

                ScenarioSet set = ScenarioLoader.TryLoadSet(inputPath);
                List<string> scenarioPaths = set?.scenarios ?? new List<string> { inputPath };

                var results = new List<ScenarioResult>(scenarioPaths.Count);
                foreach (string scenarioPath in scenarioPaths)
                {
                    Scenario scenario = ScenarioLoader.Load(scenarioPath);
                    ScenarioResult result = BattleRunner.Run(scenario, repository, bands);
                    ResultWriter.Write(result, resultsDirectory);
                    results.Add(result);
                    PrintScenarioLine(result);
                }

                if (set != null)
                {
                    PrintSummary(set, results);
                }
                Console.WriteLine($"결과: {resultsDirectory}");

                foreach (ScenarioResult result in results)
                {
                    if (!result.verdict.passed)
                    {
                        return ExitBandFailed;
                    }
                }
                return ExitOk;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"오류: {e.Message}");
                return ExitError;
            }
        }

        private static void PrintScenarioLine(ScenarioResult result)
        {
            string verdict = result.verdict.passed ? "PASS" : "FAIL";
            Console.WriteLine(
                $"[{verdict}] {result.scenarioName,-28} {result.runs,4}판  " +
                $"좌 {result.leftWinRate,6:P1}  우 {result.rightWinRate,6:P1}  무 {result.draws,3}  " +
                $"밴드 {result.band}({result.verdict.minWinRate:P0}~{result.verdict.maxWinRate:P0})  {result.elapsedMs,5}ms");
        }

        private static void PrintSummary(ScenarioSet set, List<ScenarioResult> results)
        {
            int passed = 0;
            var failures = new List<ScenarioResult>();
            foreach (ScenarioResult result in results)
            {
                if (result.verdict.passed)
                {
                    passed++;
                }
                else
                {
                    failures.Add(result);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"=== {set.name}: {results.Count}개 시나리오 중 {passed}개 PASS / {failures.Count}개 FAIL ===");
            foreach (ScenarioResult failure in failures)
            {
                Console.WriteLine($"  FAIL {failure.scenarioName} [{failure.band}] — {failure.verdict.detail}");
            }
        }

        /// <summary>시나리오 경로에서 위로 올라가며 Unity 프로젝트 루트(Assets + ProjectSettings 보유)를 찾는다.</summary>
        private static string FindUnityProjectRoot(string startPath)
        {
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(startPath));
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Assets"))
                    && Directory.Exists(Path.Combine(directory.FullName, "ProjectSettings")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException($"Unity 프로젝트 루트를 찾을 수 없다 (시작점: {startPath})");
        }
    }
}
