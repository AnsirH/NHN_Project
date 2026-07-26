using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    /// <summary>시나리오 분대 항목 — 5.5단계 SquadRequest/outGame DeployedArmy와 같은 어휘 (스탯 필드 금지 — 컷 라인).</summary>
    public sealed class SquadEntry
    {
        public string squadId { get; set; }
        public string roleId { get; set; }
        public string generalId { get; set; }
        public int soldierCount { get; set; }
        public float slotX { get; set; }
        public float slotY { get; set; }

        // 업그레이드 레벨(0~5)은 여기 없다: 실전 스탯은 아웃게임이 armyDefId+upgradeLevel로 계산해
        // 넘기며(계약 §7.1.1), 배율 공식은 ArmyStatCalculator가 단일 출처다. 그 공식을 툴에서 재현하면
        // 양쪽 수치가 갈라지므로, 레벨 스윕은 머지 후 그 계산기를 링크해 되살린다.
    }

    public sealed class Scenario
    {
        public string name;
        public int runs;
        /// <summary>목표 밴드 태그 (bands.json의 id). 비우면 "none" = 판정 없음.</summary>
        public string band;
        public List<SquadEntry> left;
        public List<SquadEntry> right;
    }

    /// <summary>시나리오 세트 — 전체 매트릭스를 한 번에 돌리기 위한 목록 파일 (예: scenarios/all.json).</summary>
    public sealed class ScenarioSet
    {
        public string name;
        public List<string> scenarios;
    }

    public static class ScenarioLoader
    {
        private const string FileReferencePrefix = "@";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>세트 파일이면 목록을, 단일 시나리오면 null을 반환한다 (구분 기준: "scenarios" 배열의 존재).</summary>
        public static ScenarioSet TryLoadSet(string path)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (!document.RootElement.TryGetProperty("scenarios", out JsonElement scenariosElement)
                || scenariosElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var set = new ScenarioSet
            {
                name = document.RootElement.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString()
                    : Path.GetFileNameWithoutExtension(path),
                scenarios = new List<string>(),
            };
            string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            foreach (JsonElement item in scenariosElement.EnumerateArray())
            {
                string entry = item.GetString();
                if (string.IsNullOrEmpty(entry))
                {
                    throw new InvalidDataException($"{path}: scenarios 항목에 빈 경로가 있다");
                }
                set.scenarios.Add(Path.GetFullPath(Path.Combine(baseDirectory, entry)));
            }
            if (set.scenarios.Count == 0)
            {
                throw new InvalidDataException($"{path}: scenarios 목록이 비어 있다");
            }
            return set;
        }

        public static Scenario Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"시나리오 파일이 없다: {path}");
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            JsonElement root = document.RootElement;
            string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));

            var scenario = new Scenario
            {
                name = root.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null,
                runs = root.TryGetProperty("runs", out JsonElement runsElement) ? runsElement.GetInt32() : 0,
                band = root.TryGetProperty("band", out JsonElement bandElement) ? bandElement.GetString() : null,
                left = ReadSide(root, "left", baseDirectory, path),
                right = ReadSide(root, "right", baseDirectory, path),
            };
            Validate(scenario, path);
            return scenario;
        }

        /// <summary>군대 정의는 인라인 배열 또는 "@상대경로" 참조 — 참조는 재사용을 위한 것이라 중첩은 허용하지 않는다.</summary>
        private static List<SquadEntry> ReadSide(JsonElement root, string sideName, string baseDirectory, string path)
        {
            if (!root.TryGetProperty(sideName, out JsonElement side))
            {
                throw new InvalidDataException($"{path}: '{sideName}' 항목이 없다");
            }

            if (side.ValueKind == JsonValueKind.String)
            {
                string reference = side.GetString();
                if (reference == null || !reference.StartsWith(FileReferencePrefix))
                {
                    throw new InvalidDataException(
                        $"{path}: '{sideName}'이 문자열이면 '{FileReferencePrefix}상대경로' 형식이어야 한다 (현재 '{reference}')");
                }
                string referencedPath = Path.GetFullPath(
                    Path.Combine(baseDirectory, reference.Substring(FileReferencePrefix.Length)));
                if (!File.Exists(referencedPath))
                {
                    throw new FileNotFoundException($"{path}: '{sideName}'이 참조하는 군대 파일이 없다: {referencedPath}");
                }
                var squads = JsonSerializer.Deserialize<List<SquadEntry>>(File.ReadAllText(referencedPath), Options);
                if (squads == null || squads.Count == 0)
                {
                    throw new InvalidDataException($"{referencedPath}: 군대 정의가 비어 있다");
                }
                return squads;
            }

            if (side.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<SquadEntry>>(side.GetRawText(), Options);
            }

            throw new InvalidDataException($"{path}: '{sideName}'은 배열이거나 '{FileReferencePrefix}경로' 문자열이어야 한다");
        }

        private static void Validate(Scenario scenario, string path)
        {
            if (scenario == null || string.IsNullOrEmpty(scenario.name))
            {
                throw new InvalidDataException($"{path}: name이 비어 있다");
            }
            if (scenario.runs < 1)
            {
                throw new InvalidDataException($"{path}: runs는 1 이상이어야 한다 (현재 {scenario.runs})");
            }
            ValidateSide(scenario.left, "left", path);
            ValidateSide(scenario.right, "right", path);
        }

        private static void ValidateSide(List<SquadEntry> side, string label, string path)
        {
            if (side == null || side.Count == 0)
            {
                throw new InvalidDataException($"{path}: {label} 군대가 비어 있다");
            }
            var seenIds = new HashSet<string>();
            foreach (SquadEntry squad in side)
            {
                if (string.IsNullOrEmpty(squad.squadId) || !seenIds.Add(squad.squadId))
                {
                    throw new InvalidDataException($"{path}: {label}의 squadId는 비어 있지 않고 유일해야 한다 ('{squad.squadId}')");
                }
                if (squad.soldierCount < 1)
                {
                    throw new InvalidDataException($"{path}: {label}/{squad.squadId} soldierCount는 1 이상이어야 한다");
                }
            }
        }
    }
}
