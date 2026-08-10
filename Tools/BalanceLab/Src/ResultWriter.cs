using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    /// <summary>결과 JSON 저장 — results/&lt;시나리오명&gt;.json (커밋 대상: 튜닝 이력 비교의 원료).</summary>
    public static class ResultWriter
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
        };

        /// <summary>
        /// outputTag가 있으면 "&lt;시나리오명&gt;.&lt;태그&gt;.json"으로 저장해 기존 스냅샷(정본)을 덮어쓰지 않는다 —
        /// 새 수치를 정본으로 승격하기 전에 나란히 놓고 비교하고 싶을 때 사용 (--tag 인자).
        /// </summary>
        public static string FileNameFor(string scenarioName, string outputTag)
        {
            return string.IsNullOrEmpty(outputTag)
                ? scenarioName + ".json"
                : scenarioName + "." + outputTag + ".json";
        }

        /// <summary>뷰어가 file:// 에서도 불러올 수 있는 &lt;script&gt; 래퍼의 이름 (JSON과 짝).</summary>
        public static string ScriptNameFor(string scenarioName, string outputTag)
        {
            return Path.ChangeExtension(FileNameFor(scenarioName, outputTag), ".js");
        }

        /// <summary>
        /// 시나리오 결과를 .json(정본·교차 검증용)과 .js(뷰어용 래퍼) 두 벌로 쓴다.
        /// .js 키는 "&lt;시나리오&gt;:&lt;태그 또는 빈문자&gt;" — 정본과 실험 결과가 한 페이지에 공존해도 섞이지 않는다.
        /// </summary>
        public static string Write(ScenarioResult result, string resultsDirectory, string outputTag = null)
        {
            Directory.CreateDirectory(resultsDirectory);
            string json = JsonSerializer.Serialize(result, Options);

            string path = Path.Combine(resultsDirectory, FileNameFor(result.scenarioName, outputTag));
            File.WriteAllText(path, json);

            string key = result.scenarioName + ":" + (outputTag ?? string.Empty);
            File.WriteAllText(
                Path.Combine(resultsDirectory, ScriptNameFor(result.scenarioName, outputTag)),
                "window.BALANCE_RESULT = window.BALANCE_RESULT || {};\n" +
                "window.BALANCE_RESULT[" + JsonSerializer.Serialize(key) + "] = " + json + ";\n");
            return path;
        }
    }
}
