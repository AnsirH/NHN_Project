using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BalanceLab
{
    /// <summary>시나리오 분대 항목 — 5.5단계 SquadRequest/outGame DeployedArmy와 같은 어휘 (스탯 필드 금지 — 컷 라인).</summary>
    public sealed class SquadEntry
    {
        public string squadId;
        public string roleId;
        public string generalId;
        public int soldierCount;
        public float slotX;
        public float slotY;

        /// <summary>
        /// 분대 레벨 (0~5) = 장군 레벨 = 군대 강화 레벨, 단일 축. 스탯은 이 레벨로 CSV에서 조회한다.
        /// 스탯 값이 아니라 참조 키이므로 "시나리오에 스탯 금지" 컷 라인에 저촉되지 않는다.
        /// 실전에서 이 자리는 아웃게임이 계산해 보내는 5스탯이 대신한다 — BalanceLab 전용 대역이다.
        /// </summary>
        public int level;
    }

    public sealed class Scenario
    {
        public string name;
        public int runs;
        public List<SquadEntry> left;
        public List<SquadEntry> right;
    }

    public static class ScenarioLoader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static Scenario Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"시나리오 파일이 없다: {path}");
            }
            Scenario scenario = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(path), Options);
            Validate(scenario, path);
            return scenario;
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
                if (squad.level < 0)
                {
                    throw new InvalidDataException($"{path}: {label}/{squad.squadId} level은 0 이상이어야 한다 (현재 {squad.level})");
                }
            }
        }
    }
}
