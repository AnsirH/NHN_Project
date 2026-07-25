using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    public sealed class BandRule
    {
        public string id { get; set; }
        public double minWinRate { get; set; }
        public double maxWinRate { get; set; }
        public string description { get; set; }
    }

    public sealed class BandFile
    {
        public List<BandRule> bands { get; set; }
    }

    /// <summary>
    /// 목표 밴드 판정 — 시나리오의 band 태그로 규칙을 고르고 좌군 승률이 범위 안인지 본다.
    /// 미등록 태그는 즉시 실패한다 (오타 난 태그가 조용히 "판정 없음"이 되면 밴드가 무력해진다).
    /// </summary>
    public sealed class BandTable
    {
        public const string DefaultBandId = "none";

        private readonly Dictionary<string, BandRule> _rules;

        private BandTable(Dictionary<string, BandRule> rules)
        {
            _rules = rules;
        }

        public static BandTable Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"밴드 정의 파일이 없다: {path}");
            }
            var file = JsonSerializer.Deserialize<BandFile>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (file?.bands == null || file.bands.Count == 0)
            {
                throw new InvalidDataException($"{path}: bands 항목이 비어 있다");
            }

            var rules = new Dictionary<string, BandRule>();
            foreach (BandRule rule in file.bands)
            {
                if (string.IsNullOrEmpty(rule.id))
                {
                    throw new InvalidDataException($"{path}: id가 없는 밴드 항목이 있다");
                }
                if (rule.minWinRate > rule.maxWinRate)
                {
                    throw new InvalidDataException($"{path}: 밴드 '{rule.id}'의 min({rule.minWinRate}) > max({rule.maxWinRate})");
                }
                rules[rule.id] = rule;
            }
            return new BandTable(rules);
        }

        public BandRule Get(string bandId)
        {
            string key = string.IsNullOrEmpty(bandId) ? DefaultBandId : bandId;
            if (!_rules.TryGetValue(key, out BandRule rule))
            {
                throw new InvalidDataException(
                    $"미등록 band 태그 '{key}' — bands.json에 정의하거나 시나리오의 오타를 고쳐라 " +
                    $"(등록됨: {string.Join(", ", _rules.Keys)})");
            }
            return rule;
        }

        /// <summary>좌군 승률로 PASS/FAIL을 판정하고, 실패 시 어긋난 수치를 문장으로 남긴다.</summary>
        public static BandVerdict Judge(BandRule rule, double leftWinRate)
        {
            bool passed = leftWinRate >= rule.minWinRate - 1e-9 && leftWinRate <= rule.maxWinRate + 1e-9;
            string detail = passed
                ? null
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "좌군 승률 {0:P1}이 목표 밴드 {1:P0}~{2:P0}를 벗어남 ({3})",
                    leftWinRate, rule.minWinRate, rule.maxWinRate,
                    leftWinRate < rule.minWinRate ? "미달" : "초과");
            return new BandVerdict
            {
                bandId = rule.id,
                minWinRate = rule.minWinRate,
                maxWinRate = rule.maxWinRate,
                passed = passed,
                detail = detail,
            };
        }
    }

    /// <summary>결과 JSON에 실리는 밴드 판정 (리포트·튜닝 루프의 입력).</summary>
    public sealed class BandVerdict
    {
        public string bandId;
        public double minWinRate;
        public double maxWinRate;
        public bool passed;
        /// <summary>실패 사유 (통과 시 null).</summary>
        public string detail;
    }
}
