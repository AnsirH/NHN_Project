using System;
using System.IO;

namespace BalanceLab
{
    /// <summary>
    /// BalanceLab CLI 러너 (작업 1) — 사용법: dotnet run -- &lt;scenario.json&gt;
    /// [시나리오 JSON] + [.asset 직독(단일 출처)] → 시드 0..N-1 N판 → 결과 JSON.
    /// 밴드 판정/리포트는 작업 2·3에서 확장한다.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("사용법: dotnet run -- <scenario.json 경로>");
                return 2;
            }

            try
            {
                string scenarioPath = Path.GetFullPath(args[0]);
                string projectRoot = FindUnityProjectRoot(scenarioPath);

                // guid→경로 인덱스는 캐싱 없이 매 실행 재구축 (작업 명세 v2 추가 조건)
                var repository = new AssetRepository(projectRoot);
                Scenario scenario = ScenarioLoader.Load(scenarioPath);
                ScenarioResult result = BattleRunner.Run(scenario, repository);

                string resultsDirectory = Path.Combine(projectRoot, "Tools", "BalanceLab", "results");
                string resultPath = ResultWriter.Write(result, resultsDirectory);

                Console.WriteLine(
                    $"[{result.scenarioName}] {result.runs}판  " +
                    $"좌 {result.leftWins}승 ({result.leftWinRate:P1})  우 {result.rightWins}승 ({result.rightWinRate:P1})  " +
                    $"무 {result.draws}  실행 {result.elapsedMs}ms");
                Console.WriteLine($"결과: {resultPath}");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"오류: {e.Message}");
                return 1;
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
