using System;
using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    /// <summary>
    /// 궤적 파일 저장 — results/replay/&lt;시나리오&gt;[.&lt;태그&gt;].&lt;시드&gt;.js
    ///
    /// .js 래퍼만 쓰고 .json은 쓰지 않는다. 결과 요약과 달리 궤적은 사람이 직접 읽을 물건이 아니고
    /// (base64 블록), 교차 검증 테스트도 읽지 않는다 — 소비자가 뷰어뿐이라 파일을 두 벌 둘 이유가 없다.
    /// </summary>
    public static class ReplayWriter
    {
        public const string DirectoryName = "replay";

        /// <summary>이 크기를 넘으면 경고한다. 거절하지는 않는다 — 대규모 난전이야말로 봐야 할 판이다.</summary>
        private const int WarnBytes = 2 * 1024 * 1024;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = false, // 궤적은 사람이 읽지 않는다 — 들여쓰기는 용량 낭비다
        };

        public static string FileNameFor(string scenarioName, string tag, int seed)
        {
            string stem = string.IsNullOrEmpty(tag) ? scenarioName : scenarioName + "." + tag;
            return stem + "." + seed + ".js";
        }

        public static string KeyFor(string scenarioName, string tag, int seed)
        {
            return scenarioName + ":" + (tag ?? string.Empty) + ":" + seed;
        }

        public static string Write(ReplayData data, string resultsDirectory, string tag)
        {
            string directory = Path.Combine(resultsDirectory, DirectoryName);
            Directory.CreateDirectory(directory);
            data.tag = string.IsNullOrEmpty(tag) ? null : tag;

            string path = Path.Combine(directory, FileNameFor(data.scenario, data.tag, data.seed));
            string body =
                "window.BALANCE_REPLAY = window.BALANCE_REPLAY || {};\n" +
                "window.BALANCE_REPLAY[" + JsonSerializer.Serialize(KeyFor(data.scenario, data.tag, data.seed)) + "] = "
                + JsonSerializer.Serialize(data, Options) + ";\n";
            File.WriteAllText(path, body);

            string size = FormatSize(body.Length);
            string warning = body.Length > WarnBytes ? "  ← 큰 파일이다 (샘플링을 줄이거나 판을 나눠라)" : string.Empty;
            Console.WriteLine(
                $"리플레이: {Path.GetFileName(path)}  {data.frameCount}프레임 · {data.events.Count}이벤트 · {size}{warning}");
            return path;
        }

        private static string FormatSize(int bytes)
        {
            if (bytes < 1024) { return bytes + "B"; }
            if (bytes < 1024 * 1024) { return Math.Round(bytes / 1024.0) + "KB"; }
            return Math.Round(bytes / (1024.0 * 1024.0), 1) + "MB";
        }
    }
}
