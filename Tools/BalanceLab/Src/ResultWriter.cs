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

        public static string Write(ScenarioResult result, string resultsDirectory)
        {
            Directory.CreateDirectory(resultsDirectory);
            string path = Path.Combine(resultsDirectory, result.scenarioName + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(result, Options));
            return path;
        }
    }
}
