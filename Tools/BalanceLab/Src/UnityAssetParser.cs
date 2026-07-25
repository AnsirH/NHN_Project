using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace BalanceLab
{
    /// <summary>
    /// Unity .asset(YAML) 1개의 파싱 결과 — 평평한 key: value 필드로 한정.
    /// 필수 키 누락/형식 불일치는 즉시 예외 (파서가 조용히 틀리면 모든 밸런싱이 가짜가 된다 — 작업 명세 v2).
    /// 모르는 키는 무시한다 (구버전 잔존 필드 등 — Unity 디시리얼라이저와 같은 태도).
    /// </summary>
    public sealed class ParsedAsset
    {
        public string FilePath;
        /// <summary>m_EditorClassIdentifier — 예: "NHN.Data::NHN.Data.RoleData".</summary>
        public string ClassIdentifier;
        /// <summary>m_Name — 에셋 이름 (카탈로그 키).</summary>
        public string Name;

        private readonly Dictionary<string, string> _scalars = new Dictionary<string, string>();
        private readonly Dictionary<string, List<string>> _lists = new Dictionary<string, List<string>>();

        public void AddScalar(string key, string value) => _scalars[key] = value;

        public void BeginList(string key) => _lists[key] = new List<string>();

        public bool TryAppendListItem(string key, string item)
        {
            if (!_lists.TryGetValue(key, out List<string> list))
            {
                return false;
            }
            list.Add(item);
            return true;
        }

        public bool Has(string key) => _scalars.ContainsKey(key) || _lists.ContainsKey(key);

        public float GetFloat(string key)
        {
            string raw = GetRequiredScalar(key);
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                throw Fail($"'{key}' 값 '{raw}'를 float으로 해석할 수 없다");
            }
            return value;
        }

        public int GetInt(string key)
        {
            string raw = GetRequiredScalar(key);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw Fail($"'{key}' 값 '{raw}'를 int로 해석할 수 없다");
            }
            return value;
        }

        private static readonly Regex GuidRefPattern = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

        /// <summary>오브젝트 참조 필드("{fileID: ..., guid: X, type: 2}")에서 guid를 얻는다.</summary>
        public string GetGuidRef(string key)
        {
            string raw = GetRequiredScalar(key);
            Match match = GuidRefPattern.Match(raw);
            if (!match.Success)
            {
                throw Fail($"'{key}' 값 '{raw}'가 guid 참조 형식이 아니다");
            }
            return match.Groups[1].Value;
        }

        public List<int> GetIntList(string key)
        {
            if (_lists.TryGetValue(key, out List<string> items))
            {
                var values = new List<int>(items.Count);
                foreach (string item in items)
                {
                    if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    {
                        throw Fail($"'{key}' 리스트 항목 '{item}'을 int로 해석할 수 없다");
                    }
                    values.Add(value);
                }
                return values;
            }
            if (_scalars.TryGetValue(key, out string scalar) && scalar == "[]")
            {
                return new List<int>();
            }
            throw Fail($"필수 리스트 키 '{key}' 누락 또는 형식 불일치");
        }

        private string GetRequiredScalar(string key)
        {
            if (!_scalars.TryGetValue(key, out string value) || string.IsNullOrEmpty(value))
            {
                throw Fail($"필수 키 '{key}' 누락 — Unity에서 에셋을 재저장해 전 필드를 직렬화하거나 라인을 보정하라");
            }
            return value;
        }

        public InvalidDataException Fail(string message) => new InvalidDataException($"{FilePath}: {message}");
    }

    public static class UnityAssetParser
    {
        private static readonly Regex MetaGuidPattern = new Regex(@"^guid:\s*([0-9a-f]{32})\s*$", RegexOptions.Compiled);

        public static string ParseMetaGuid(string metaPath)
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                Match match = MetaGuidPattern.Match(line.Trim());
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            throw new InvalidDataException($"{metaPath}: guid 라인을 찾을 수 없다");
        }

        /// <summary>
        /// 클래스 식별자만 가볍게 읽는다 (전체 파싱 없이) — 관련 없는 에셋(중첩 구조 등)을 걸러내는 1차 스캔용.
        /// MonoBehaviour 에셋이 아니면 null.
        /// </summary>
        public static string ReadClassIdentifier(string assetPath)
        {
            const string Prefix = "  m_EditorClassIdentifier: ";
            foreach (string line in File.ReadLines(assetPath))
            {
                if (line.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    return line.Substring(Prefix.Length).Trim();
                }
            }
            return null;
        }

        /// <summary>단일 MonoBehaviour 문서 에셋만 지원 — 예상 밖 구조는 즉시 예외.</summary>
        public static ParsedAsset ParseAssetFile(string assetPath)
        {
            var asset = new ParsedAsset { FilePath = assetPath };
            bool inBehaviour = false;
            bool documentSeen = false;
            string pendingListKey = null;

            foreach (string rawLine in File.ReadLines(assetPath))
            {
                string line = rawLine.TrimEnd();
                if (line.StartsWith("%", StringComparison.Ordinal))
                {
                    continue; // %YAML / %TAG 헤더
                }
                if (line.StartsWith("---", StringComparison.Ordinal))
                {
                    if (documentSeen)
                    {
                        throw asset.Fail("멀티 문서 에셋은 지원하지 않는다 (예상 밖 구조)");
                    }
                    documentSeen = true;
                    continue;
                }
                if (line == "MonoBehaviour:")
                {
                    inBehaviour = true;
                    continue;
                }
                if (!inBehaviour || line.Length == 0)
                {
                    continue;
                }
                if (!line.StartsWith("  ", StringComparison.Ordinal))
                {
                    throw asset.Fail($"예상 밖 들여쓰기: '{line}'");
                }

                string body = line.Substring(2);
                if (body.StartsWith("- ", StringComparison.Ordinal))
                {
                    // 직전 키의 리스트 항목
                    if (pendingListKey == null || !asset.TryAppendListItem(pendingListKey, body.Substring(2).Trim()))
                    {
                        throw asset.Fail($"소속 키 없는 리스트 항목: '{line}'");
                    }
                    continue;
                }
                if (body.StartsWith(" ", StringComparison.Ordinal))
                {
                    // 중첩 구조(들여쓰기 4+) — 우리가 읽는 평면 에셋엔 없어야 정상
                    throw asset.Fail($"중첩 구조는 지원하지 않는다 (예상 밖 구조): '{line}'");
                }

                int colon = body.IndexOf(':');
                if (colon <= 0)
                {
                    throw asset.Fail($"key: value 형식이 아니다: '{line}'");
                }
                string key = body.Substring(0, colon);
                string value = body.Length > colon + 1 ? body.Substring(colon + 1).Trim() : string.Empty;

                if (key == "m_Name")
                {
                    asset.Name = value;
                    pendingListKey = null;
                    continue;
                }
                if (key == "m_EditorClassIdentifier")
                {
                    asset.ClassIdentifier = value;
                    pendingListKey = null;
                    continue;
                }
                if (key.StartsWith("m_", StringComparison.Ordinal))
                {
                    pendingListKey = null;
                    continue; // 그 외 유니티 내부 키 무시
                }

                if (value.Length == 0)
                {
                    // 값 없는 키 = 리스트 시작 (비어 있으면 빈 리스트로 남는다 — "priorities:" 케이스)
                    asset.BeginList(key);
                    pendingListKey = key;
                }
                else
                {
                    asset.AddScalar(key, value);
                    pendingListKey = null;
                }
            }

            if (!documentSeen || asset.Name == null || asset.ClassIdentifier == null)
            {
                throw asset.Fail("MonoBehaviour 에셋 구조가 아니다 (m_Name/m_EditorClassIdentifier 누락)");
            }
            return asset;
        }
    }
}
