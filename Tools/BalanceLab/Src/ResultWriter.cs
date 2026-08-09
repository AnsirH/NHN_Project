using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    /// <summary>결과 JSON 저장 — results/&lt;시나리오명&gt;.json (커밋 대상: 작업 3 튜닝 이력 비교의 원료).</summary>
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
        public static string Write(ScenarioResult result, string resultsDirectory, string outputTag = null)
        {
            Directory.CreateDirectory(resultsDirectory);
            string fileName = string.IsNullOrEmpty(outputTag)
                ? result.scenarioName + ".json"
                : result.scenarioName + "." + outputTag + ".json";
            string path = Path.Combine(resultsDirectory, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(result, Options));
            return path;
        }
    }
}
