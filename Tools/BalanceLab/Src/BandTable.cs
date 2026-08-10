using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    public sealed class BandRule
    {
        public string id { get; set; }
        /// <summary>무엇을 재는가 — "leftWinRate"(기본) 또는 "sideBalance". bands.json의 주석 참조.</summary>
        public string metric { get; set; }
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

        public const string MetricLeftWinRate = "leftWinRate";
        public const string MetricSideBalance = "sideBalance";

        /// <summary>
        /// 밴드가 지정한 지표로 PASS/FAIL을 판정하고, 실패 시 어긋난 수치를 문장으로 남긴다.
        ///
        /// sideBalance가 따로 있는 이유: 완전 대칭 매치업의 올바른 결과는 50:50 승리가 아니라 무승부다.
        /// 좌군 승률로 재면 무승부가 분자·분모 어디에도 안 들어가 원리적으로 통과할 수 없다.
        /// 승부가 난 판만 모아 좌/우가 기울었는지만 본다.
        /// </summary>
        public static BandVerdict Judge(BandRule rule, int leftWins, int rightWins, double leftWinRate)
        {
            string metric = string.IsNullOrEmpty(rule.metric) ? MetricLeftWinRate : rule.metric;
            double value;
            string label;
            switch (metric)
            {
                case MetricLeftWinRate:
                    value = leftWinRate;
                    label = "좌군 승률";
                    break;
                case MetricSideBalance:
                    int decisive = leftWins + rightWins;
                    if (decisive == 0)
                    {
                        // 전 판 무승부 = 어느 쪽도 유리하지 않다는 가장 강한 증거다. 통과.
                        return new BandVerdict
                        {
                            bandId = rule.id, metric = metric, value = 0.5,
                            minWinRate = rule.minWinRate, maxWinRate = rule.maxWinRate,
                            passed = true, detail = null,
                        };
                    }
                    value = leftWins / (double)decisive;
                    label = "승부 난 판의 좌군 비율";
                    break;
                default:
                    throw new InvalidDataException(
                        $"밴드 '{rule.id}'의 metric '{rule.metric}'을 알 수 없다 " +
                        $"(허용: {MetricLeftWinRate}, {MetricSideBalance})");
            }

            bool passed = value >= rule.minWinRate - 1e-9 && value <= rule.maxWinRate + 1e-9;
            string detail = passed
                ? null
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1:P1}이 목표 밴드 {2:P0}~{3:P0}를 벗어남 ({4})",
                    label, value, rule.minWinRate, rule.maxWinRate,
                    value < rule.minWinRate ? "미달" : "초과");
            return new BandVerdict
            {
                bandId = rule.id,
                metric = metric,
                value = value,
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
        /// <summary>판정에 쓴 지표 이름 — 뷰어가 무엇을 그려야 하는지 알려면 필요하다.</summary>
        public string metric;
        /// <summary>실제로 판정된 값. 지표에 따라 좌군 승률이거나 승부 난 판의 좌군 비율이다.</summary>
        public double value;
        public double minWinRate;
        public double maxWinRate;
        public bool passed;
        /// <summary>실패 사유 (통과 시 null).</summary>
        public string detail;
    }
}
