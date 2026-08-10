using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BalanceLab
{
    /// <summary>
    /// 수치 실험용 오버라이드 — .asset 파일을 건드리지 않고 ParsedAsset(필드 사전)에 패치를 얹는다.
    ///
    /// 주입 지점이 AssetRepository 한 곳인 이유: BuildRole·LoadBattleConfig·GetGeneral이 전부 같은 사전에서
    /// 읽으므로, 사전만 패치하면 5스탯·공격 주기·사거리·투사체·장군 배율·충전 조건·BattleConfig까지
    /// 전부 커버된다. 시뮬 코드는 오버라이드의 존재를 알 필요가 없다.
    ///
    /// 측정 도구의 fail-fast 원칙을 그대로 따른다 — 미등록 에셋/필드는 즉시 예외다.
    /// 오타 친 필드가 조용히 무시돼 "값을 바꿨는데 결과가 그대로"가 되는 것이 튜닝 루프 최악의 함정이다.
    /// </summary>
    public sealed class OverrideTable
    {
        /// <summary>장군 필드 지정용 접미사 계약 — AssetRepository.GeneralSuffix와 같은 규칙.</summary>
        private const string GeneralSuffix = "General";

        /// <summary>"&lt;롤&gt;General" 키로 지정할 수 있는 필드의 접두사 — 병사 스탯 오지정을 막는 가드.</summary>
        private const string GeneralFieldPrefix = "general";

        private readonly Dictionary<string, Dictionary<string, string>> _squads;
        private readonly Dictionary<string, string> _config;
        private readonly HashSet<string> _consumedSquadKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _warnings = new List<string>();
        private bool _configApplied;

        /// <summary>오버라이드 파일 원문 — 결과 JSON에 통째로 실려, 결과만 보고 수치를 복원할 수 있게 한다.</summary>
        public JsonNode Source { get; }

        public string SourcePath { get; }

        /// <summary>적용된 (에셋, 필드) 쌍의 수 — 요약 출력용.</summary>
        public int AppliedFieldCount { get; private set; }

        /// <summary>적용은 됐지만 원래 값과 같아 아무것도 바꾸지 않은 필드 등, 사람이 봐야 할 경고.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        private OverrideTable(
            string sourcePath,
            JsonNode source,
            Dictionary<string, Dictionary<string, string>> squads,
            Dictionary<string, string> config)
        {
            SourcePath = sourcePath;
            Source = source;
            _squads = squads;
            _config = config;
        }

        public static OverrideTable Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"오버라이드 파일이 없다: {path}");
            }

            JsonNode root;
            try
            {
                root = JsonNode.Parse(File.ReadAllText(path), null, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{path}: JSON 파싱 실패 — {e.Message}");
            }

            if (root is not JsonObject rootObject)
            {
                throw new InvalidDataException($"{path}: 최상위는 객체여야 한다");
            }

            var squads = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var config = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, JsonNode> entry in rootObject)
            {
                switch (entry.Key)
                {
                    case "squads":
                        ReadSquads(path, entry.Value, squads);
                        break;
                    case "config":
                        ReadFields(path, "config", entry.Value, config);
                        break;
                    case "note":
                        break; // 사람이 읽는 메모 — 그대로 원문에 남고 적용은 하지 않는다
                    default:
                        throw new InvalidDataException(
                            $"{path}: 알 수 없는 최상위 키 '{entry.Key}' (허용: squads, config, note)");
                }
            }

            if (squads.Count == 0 && config.Count == 0)
            {
                throw new InvalidDataException($"{path}: 적용할 오버라이드가 하나도 없다");
            }
            return new OverrideTable(path, root, squads, config);
        }

        private static void ReadSquads(
            string path, JsonNode node, Dictionary<string, Dictionary<string, string>> squads)
        {
            if (node is not JsonObject squadObject)
            {
                throw new InvalidDataException($"{path}: 'squads'는 객체여야 한다");
            }
            foreach (KeyValuePair<string, JsonNode> entry in squadObject)
            {
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                ReadFields(path, "squads/" + entry.Key, entry.Value, fields);
                if (fields.Count == 0)
                {
                    throw new InvalidDataException($"{path}: 'squads/{entry.Key}'에 필드가 하나도 없다");
                }
                squads[entry.Key] = fields;
            }
        }

        private static void ReadFields(
            string path, string context, JsonNode node, Dictionary<string, string> target)
        {
            if (node is not JsonObject fieldObject)
            {
                throw new InvalidDataException($"{path}: '{context}'는 객체여야 한다");
            }
            foreach (KeyValuePair<string, JsonNode> field in fieldObject)
            {
                target[field.Key] = ToAssetScalar(path, context + "/" + field.Key, field.Value);
            }
        }

        /// <summary>
        /// JSON 값을 .asset 스칼라 표기로 바꾼다. 숫자는 원문 그대로 쓴다 —
        /// 재포맷하면 부동소수 표기가 달라져 "원래 값과 같은지" 비교가 어긋난다.
        /// enum·bool 필드는 .asset이 정수로 직렬화하므로 true/false를 1/0으로 받아준다.
        /// </summary>
        private static string ToAssetScalar(string path, string context, JsonNode value)
        {
            if (value is JsonValue jsonValue && jsonValue.TryGetValue(out JsonElement element))
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Number:
                        return element.GetRawText();
                    case JsonValueKind.True:
                        return "1";
                    case JsonValueKind.False:
                        return "0";
                }
            }
            throw new InvalidDataException(
                $"{path}: '{context}' 값은 숫자 또는 true/false여야 한다 (.asset은 enum도 정수로 직렬화한다)");
        }

        /// <summary>
        /// SquadData 에셋 하나에 패치를 얹는다. 같은 에셋이 두 경로로 지정될 수 있다:
        ///   · "Warrior"        → 병사 필드 (제한 없음, 장군 필드도 여기서 지정 가능)
        ///   · "WarriorGeneral" → 장군 필드 전용 (general* 접두사 강제)
        /// 후자에 접두사 가드를 두는 이유: "WarriorGeneral": { "maxHp": 50 }은 장군 체력을 바꾸는 것처럼
        /// 보이지만 실제로는 병사 체력을 바꾼다 (장군 체력 = 병사 체력 × generalHpMultiplier). 조용히
        /// 다른 걸 바꾸느니 거절하는 편이 낫다.
        /// </summary>
        public void ApplySquad(ParsedAsset asset)
        {
            if (_squads.TryGetValue(asset.Name, out Dictionary<string, string> direct))
            {
                _consumedSquadKeys.Add(asset.Name);
                ApplyFields(asset, asset.Name, direct, requireGeneralPrefix: false);
            }

            string generalKey = asset.Name + GeneralSuffix;
            if (_squads.TryGetValue(generalKey, out Dictionary<string, string> viaGeneral))
            {
                _consumedSquadKeys.Add(generalKey);
                ApplyFields(asset, generalKey, viaGeneral, requireGeneralPrefix: true);
            }
        }

        public void ApplyConfig(ParsedAsset asset)
        {
            if (_config.Count == 0)
            {
                return;
            }
            _configApplied = true;
            ApplyFields(asset, "config", _config, requireGeneralPrefix: false);
        }

        private void ApplyFields(
            ParsedAsset asset, string contextKey, Dictionary<string, string> fields, bool requireGeneralPrefix)
        {
            foreach (KeyValuePair<string, string> field in fields)
            {
                if (requireGeneralPrefix && !field.Key.StartsWith(GeneralFieldPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"{SourcePath}: '{contextKey}/{field.Key}' — '{contextKey}' 키로는 general* 필드만 지정할 수 있다. " +
                        $"병사 필드를 바꾸려면 '{asset.Name}' 키를 써라 (장군 스탯은 병사 스탯 × 배율로 파생된다)");
                }
                if (!asset.Has(field.Key))
                {
                    throw new InvalidDataException(
                        $"{SourcePath}: '{contextKey}/{field.Key}' — {asset.Name} 에셋에 없는 필드다 " +
                        $"({Path.GetFileName(asset.FilePath)} 확인)");
                }
                if (asset.IsListKey(field.Key))
                {
                    throw new InvalidDataException(
                        $"{SourcePath}: '{contextKey}/{field.Key}'는 리스트 필드라 오버라이드할 수 없다 " +
                        "(타겟 우선순위 같은 구조 변경은 .asset에서 해야 한다)");
                }

                asset.TryGetRawScalar(field.Key, out string previous);
                if (string.Equals(previous, field.Value, StringComparison.Ordinal))
                {
                    _warnings.Add(
                        $"{contextKey}/{field.Key} = {field.Value} 는 .asset 원래 값과 같다 — 이 필드는 아무것도 바꾸지 않는다");
                }
                asset.AddScalar(field.Key, field.Value);
                AppliedFieldCount++;
            }
        }

        /// <summary>
        /// 스캔이 끝난 뒤 한 번도 적용되지 않은 키를 잡는다 — 존재하지 않는 에셋을 짚은 오타다.
        /// 필드 오타는 ApplyFields가 잡고, 에셋 이름 오타는 여기서 잡힌다.
        /// </summary>
        public void RequireAllApplied(IEnumerable<string> knownSquadNames)
        {
            var missing = new List<string>();
            foreach (string key in _squads.Keys)
            {
                if (!_consumedSquadKeys.Contains(key))
                {
                    missing.Add(key);
                }
            }
            if (missing.Count > 0)
            {
                var known = new List<string>(knownSquadNames);
                known.Sort(StringComparer.Ordinal);
                throw new InvalidDataException(
                    $"{SourcePath}: 존재하지 않는 분대 키 {string.Join(", ", missing)} — " +
                    $"Assets/Data의 SquadData 에셋 이름이거나 거기에 'General'을 붙인 형태여야 한다 " +
                    $"(등록됨: {string.Join(", ", known)})");
            }
            if (_config.Count > 0 && !_configApplied)
            {
                throw new InvalidDataException($"{SourcePath}: config 오버라이드가 적용되지 않았다 — BattleConfig 에셋을 찾지 못했다");
            }
        }
    }
}
