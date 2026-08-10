using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    /// <summary>뷰어 매트릭스 한 줄 — 시나리오 결과의 요약. 시드별 기록은 여기 싣지 않는다(파일이 터진다).</summary>
    public sealed class IndexEntry
    {
        public string name;
        /// <summary>프로젝트 루트 기준 시나리오 경로 — 뷰어가 덤프/재현 명령을 조립한다.</summary>
        public string scenarioPath;
        /// <summary>--tag 라벨. null = 정본 실행. 항목의 식별자는 (name, tag) 쌍이다.</summary>
        public string tag;
        /// <summary>적용된 오버라이드 파일 경로 (없으면 null) — 재현 명령의 --overrides 인자.</summary>
        public string overridesPath;
        public string band;
        public double bandMin;
        public double bandMax;
        public bool passed;
        /// <summary>밴드 이탈 사유 (통과 시 null).</summary>
        public string detail;
        public int runs;
        public int leftWins;
        public int rightWins;
        public int draws;
        public double leftWinRate;
        public double rightWinRate;
        public long elapsedMs;
        public bool hasOverrides;
        /// <summary>오버라이드 파일의 note — 실험이 무엇이었는지 매트릭스에서 바로 읽히게 한다.</summary>
        public string overrideNote;
        public List<int> replaySeeds;
        /// <summary>이 항목의 상세 결과 파일명 (viewer가 &lt;script&gt;로 불러올 .js와 짝이다).</summary>
        public string file;
        public string generatedAt;
    }

    public sealed class IndexFile
    {
        public string generatedAt;
        public List<IndexEntry> scenarios = new List<IndexEntry>();
    }

    /// <summary>
    /// results/index.json(정본) + results/index.js(뷰어용 래퍼)를 갱신한다.
    ///
    /// 누적 갱신인 이유: 단일 시나리오를 돌릴 때마다 인덱스를 새로 쓰면 나머지 13개가 사라져 매트릭스가
    /// 비어 버린다. (name, tag) 쌍으로 upsert하고 나머지는 보존한다.
    ///
    /// .js 래퍼를 함께 쓰는 이유: file:// 로 index.html을 열었을 때 fetch()는 CORS로 막히지만
    /// &lt;script&gt; 태그는 로드된다. 뷰어를 더블클릭 한 번으로 열 수 있게 하는 값이 이 중복보다 크다.
    /// </summary>
    public static class IndexWriter
    {
        public const string IndexJsonName = "index.json";
        public const string IndexJsName = "index.js";

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
        };

        public static void Upsert(
            IReadOnlyList<ScenarioResult> results, string resultsDirectory, string generatedAt)
        {
            Directory.CreateDirectory(resultsDirectory);
            string jsonPath = Path.Combine(resultsDirectory, IndexJsonName);
            IndexFile index = Load(jsonPath);

            foreach (ScenarioResult result in results)
            {
                IndexEntry entry = ToEntry(result, generatedAt);
                int existing = index.scenarios.FindIndex(
                    e => string.Equals(e.name, entry.name, StringComparison.Ordinal)
                         && string.Equals(e.tag, entry.tag, StringComparison.Ordinal));
                if (existing >= 0)
                {
                    index.scenarios[existing] = entry;
                }
                else
                {
                    index.scenarios.Add(entry);
                }
            }

            // 결과 파일이 사라진 항목은 인덱스에서 뺀다 — 실험 결과를 지웠는데 매트릭스에 유령 행이
            // 남으면 클릭했을 때 "파일이 없다"만 뜨고, 그게 도구를 못 믿게 만든다.
            index.scenarios.RemoveAll(
                e => !File.Exists(Path.Combine(resultsDirectory, e.file ?? string.Empty)));

            // 정본(tag=null) 먼저, 그다음 이름순 — 매트릭스의 기본 정렬을 파일이 결정한다.
            index.scenarios.Sort(CompareEntries);
            index.generatedAt = generatedAt;

            string json = JsonSerializer.Serialize(index, WriteOptions);
            File.WriteAllText(jsonPath, json);
            File.WriteAllText(
                Path.Combine(resultsDirectory, IndexJsName),
                "window.BALANCE_INDEX = " + json + ";\n");
        }

        private static int CompareEntries(IndexEntry a, IndexEntry b)
        {
            bool aBase = string.IsNullOrEmpty(a.tag);
            bool bBase = string.IsNullOrEmpty(b.tag);
            if (aBase != bBase)
            {
                return aBase ? -1 : 1;
            }
            int byName = string.CompareOrdinal(a.name, b.name);
            return byName != 0 ? byName : string.CompareOrdinal(a.tag ?? "", b.tag ?? "");
        }

        /// <summary>인덱스가 깨져 있으면 조용히 새로 시작하지 않고 알린다 — 손으로 고친 흔적을 덮어쓰면 안 된다.</summary>
        private static IndexFile Load(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                return new IndexFile();
            }
            try
            {
                IndexFile loaded = JsonSerializer.Deserialize<IndexFile>(File.ReadAllText(jsonPath), ReadOptions);
                if (loaded?.scenarios == null)
                {
                    return new IndexFile();
                }
                return loaded;
            }
            catch (JsonException e)
            {
                throw new InvalidDataException(
                    $"{jsonPath}: 인덱스를 읽을 수 없다 — {e.Message}. 지우고 전체 매트릭스를 다시 돌려라");
            }
        }

        private static IndexEntry ToEntry(ScenarioResult result, string generatedAt)
        {
            return new IndexEntry
            {
                name = result.scenarioName,
                scenarioPath = result.scenarioPath,
                tag = string.IsNullOrEmpty(result.tag) ? null : result.tag,
                overridesPath = result.appliedOverridesPath,
                band = result.band,
                bandMin = result.verdict.minWinRate,
                bandMax = result.verdict.maxWinRate,
                passed = result.verdict.passed,
                detail = result.verdict.detail,
                runs = result.runs,
                leftWins = result.leftWins,
                rightWins = result.rightWins,
                draws = result.draws,
                leftWinRate = result.leftWinRate,
                rightWinRate = result.rightWinRate,
                elapsedMs = result.elapsedMs,
                hasOverrides = result.appliedOverrides != null,
                overrideNote = ReadNote(result),
                replaySeeds = result.replaySeeds,
                file = ResultWriter.FileNameFor(result.scenarioName, result.tag),
                generatedAt = generatedAt,
            };
        }

        private static string ReadNote(ScenarioResult result)
        {
            System.Text.Json.Nodes.JsonNode note = result.appliedOverrides?["note"];
            return note?.GetValue<string>();
        }

        public static string UtcStamp() =>
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
