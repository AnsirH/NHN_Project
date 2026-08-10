using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    /// <summary>
    /// results/assets.js — .asset 원본 수치의 스냅샷. 뷰어 튜닝 패널이 슬라이더의 기준선으로 쓴다.
    ///
    /// 뷰어가 .asset을 직접 읽을 수 없으니(브라우저는 YAML 파서도 파일 접근권도 없다) CLI가 대신 내보낸다.
    /// 오버라이드 **적용 전** 값이라는 것이 계약이다 — 그래야 슬라이더가 델타만 축적하고,
    /// 내보내는 overrides.json이 정본 대비 최소 패치가 된다.
    ///
    /// 숫자로 해석되는 스칼라만 싣는다. 문자열/guid 참조는 조절 대상이 아니고, 실어 봐야 뷰어가 못 쓴다.
    /// </summary>
    public static class AssetSnapshotWriter
    {
        public const string FileName = "assets.js";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public static void Write(AssetRepository repository, string resultsDirectory)
        {
            var squads = new Dictionary<string, Dictionary<string, double>>();
            foreach (KeyValuePair<string, Dictionary<string, string>> squad in repository.SquadBaseline)
            {
                squads[squad.Key] = Numeric(squad.Value);
            }

            var payload = new Dictionary<string, object>
            {
                ["squads"] = squads,
                ["config"] = Numeric(repository.ConfigBaseline),
            };

            Directory.CreateDirectory(resultsDirectory);
            File.WriteAllText(
                Path.Combine(resultsDirectory, FileName),
                "window.BALANCE_ASSETS = " + JsonSerializer.Serialize(payload, Options) + ";\n");
        }

        private static Dictionary<string, double> Numeric(IReadOnlyDictionary<string, string> scalars)
        {
            var result = new Dictionary<string, double>();
            if (scalars == null)
            {
                return result;
            }
            foreach (KeyValuePair<string, string> field in scalars)
            {
                if (double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    result[field.Key] = value;
                }
            }
            return result;
        }
    }
}
