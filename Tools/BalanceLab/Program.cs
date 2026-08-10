using System;
using System.Collections.Generic;
using System.IO;

namespace BalanceLab
{
    /// <summary>
    /// BalanceLab CLI 러너 — 사용법: dotnet run -- &lt;scenario.json | scenarios/all.json&gt; [옵션]
    /// [시나리오 JSON] + [.asset 인게임 속성] → 시드 0..N-1 N판 → 결과 JSON + 밴드 PASS/FAIL.
    /// 세트 파일(scenarios 배열)을 주면 전체 매트릭스를 한 번에 돌리고 요약을 출력한다.
    /// 종료 코드: 0 = 전 시나리오 PASS, 1 = 오류, 2 = 사용법 오류, 3 = 밴드 FAIL 존재.
    /// </summary>
    public static class Program
    {
        private const int ExitOk = 0;
        private const int ExitError = 1;
        private const int ExitUsage = 2;
        private const int ExitBandFailed = 3;

        private const string Usage =
            "사용법: dotnet run -- <scenario.json | scenarios/all.json> [옵션]\n" +
            "  --tag <라벨>          결과를 '<시나리오>.<라벨>.json'으로 저장 (정본 스냅샷 보호)\n" +
            "  --overrides <파일>    .asset 수치를 덮어써 실험 (--tag 필수)\n" +
            "  --replay <시드,...>   해당 시드의 궤적을 덤프해 뷰어에서 재생 가능하게 한다";

        public static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(Usage);
                return ExitUsage;
            }
            if (options == null)
            {
                Console.Error.WriteLine(Usage);
                return ExitUsage;
            }

            try
            {
                if (!File.Exists(options.InputPath))
                {
                    Console.Error.WriteLine($"파일이 없다: {options.InputPath}");
                    return ExitUsage;
                }
                string projectRoot = FindUnityProjectRoot(options.InputPath);

                OverrideTable overrides = null;
                if (options.OverridesPath != null)
                {
                    overrides = OverrideTable.Load(options.OverridesPath);
                }

                // guid→경로 인덱스는 캐싱 없이 매 실행 재구축 (작업 명세 v2 추가 조건)
                var repository = new AssetRepository(projectRoot, overrides);
                PrintOverrideSummary(overrides);

                BandTable bands = BandTable.Load(Path.Combine(projectRoot, "Tools", "BalanceLab", "bands.json"));
                string resultsDirectory = Path.Combine(projectRoot, "Tools", "BalanceLab", "results");

                ScenarioSet set = ScenarioLoader.TryLoadSet(options.InputPath);
                List<string> scenarioPaths = set?.scenarios ?? new List<string> { options.InputPath };

                var results = new List<ScenarioResult>(scenarioPaths.Count);
                foreach (string scenarioPath in scenarioPaths)
                {
                    Scenario scenario = ScenarioLoader.Load(scenarioPath);
                    ScenarioResult result = BattleRunner.Run(
                        scenario, repository, bands, options.ReplaySeeds,
                        replay => ReplayWriter.Write(replay, resultsDirectory, options.Tag));
                    result.scenarioPath = ToProjectRelative(projectRoot, scenarioPath);
                    result.tag = options.Tag;
                    result.appliedOverrides = overrides?.Source;
                    result.appliedOverridesPath = ToProjectRelative(projectRoot, overrides?.SourcePath);
                    ResultWriter.Write(result, resultsDirectory, options.Tag);
                    results.Add(result);
                    PrintScenarioLine(result);
                }

                IndexWriter.Upsert(results, resultsDirectory, IndexWriter.UtcStamp());
                AssetSnapshotWriter.Write(repository, resultsDirectory);

                if (set != null)
                {
                    PrintSummary(set, results);
                }
                Console.WriteLine($"결과: {resultsDirectory}");
                Console.WriteLine($"뷰어: {Path.Combine(projectRoot, "Tools", "BalanceLab", "viewer", "index.html")}");

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

        private static void PrintOverrideSummary(OverrideTable overrides)
        {
            if (overrides == null)
            {
                return;
            }
            Console.WriteLine($"오버라이드 적용: {overrides.SourcePath} — 필드 {overrides.AppliedFieldCount}개");
            foreach (string warning in overrides.Warnings)
            {
                Console.WriteLine($"  경고: {warning}");
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

        /// <summary>
        /// 결과 JSON에 절대 경로를 남기지 않는다 — 커밋되는 파일이고, 뷰어는 이 경로로 재현 명령을 만든다.
        /// 구분자는 항상 '/'로 통일해 명령이 어느 셸에서든 그대로 붙는다.
        /// </summary>
        private static string ToProjectRelative(string projectRoot, string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return null;
            }
            return Path.GetRelativePath(projectRoot, absolutePath).Replace('\\', '/');
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

        /// <summary>
        /// 명령줄 옵션. 모르는 플래그는 즉시 거절한다 — 오타 난 옵션이 조용히 무시되면
        /// "옵션을 줬는데 아무 일도 안 일어나는" 상황이 되고, 그건 측정 도구에서 가장 나쁜 실패다.
        /// </summary>
        public sealed class Options
        {
            public string InputPath;
            public string Tag;
            public string OverridesPath;
            /// <summary>궤적을 덤프할 시드 (비어 있으면 덤프 없음). HashSet인 이유는 판별이 판마다 일어나기 때문.</summary>
            public HashSet<int> ReplaySeeds = new HashSet<int>();

            public static Options Parse(string[] args)
            {
                if (args.Length < 1)
                {
                    return null;
                }

                var options = new Options { InputPath = Path.GetFullPath(args[0]) };
                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--tag":
                            options.Tag = RequireValue(args, ref i, "--tag");
                            break;
                        case "--overrides":
                            options.OverridesPath = Path.GetFullPath(RequireValue(args, ref i, "--overrides"));
                            break;
                        case "--replay":
                            ParseSeeds(RequireValue(args, ref i, "--replay"), options.ReplaySeeds);
                            break;
                        default:
                            throw new ArgumentException($"알 수 없는 옵션: {args[i]}");
                    }
                }

                // 오버라이드는 실험값이다. 태그 없이 돌면 results/<시나리오>.json(정본 스냅샷)을 덮어써
                // 이후의 모든 비교 기준이 오염된다 — 구조적으로 막는다.
                if (options.OverridesPath != null && string.IsNullOrEmpty(options.Tag))
                {
                    throw new ArgumentException("--overrides에는 --tag가 필수다 (실험값이 정본 스냅샷을 덮어쓰지 않도록)");
                }
                if (options.Tag != null && options.Tag.Length == 0)
                {
                    throw new ArgumentException("--tag 라벨이 비어 있다");
                }
                return options;
            }

            private static void ParseSeeds(string raw, HashSet<int> target)
            {
                foreach (string piece in raw.Split(','))
                {
                    string trimmed = piece.Trim();
                    if (!int.TryParse(trimmed, out int seed) || seed < 0)
                    {
                        throw new ArgumentException($"--replay 시드는 0 이상의 정수여야 한다 (받은 값: '{trimmed}')");
                    }
                    target.Add(seed);
                }
                if (target.Count == 0)
                {
                    throw new ArgumentException("--replay에 시드가 없다");
                }
            }

            private static string RequireValue(string[] args, ref int index, string flag)
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{flag}에 값이 필요하다");
                }
                index++;
                return args[index];
            }
        }
    }
}
