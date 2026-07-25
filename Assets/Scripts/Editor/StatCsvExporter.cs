using System.Collections.Generic;
using System.IO;
using System.Text;
using NHN.Data;
using NHN.Simulation.Balance;
using UnityEditor;
using UnityEngine;

namespace NHN.EditorTools
{
    /// <summary>
    /// .asset(레벨 0) → 스탯 CSV 역방향 익스포터.
    ///
    /// 이 경로가 있어야 밸런싱이 헛돌지 않는다: AI 튜닝 루프는 .asset 수치를 고치는데,
    /// 실제 게임의 스탯은 아웃게임이 CSV(시트) 기준으로 계산해 전달한다.
    /// 확정된 튜닝 결과를 CSV로 되돌려 시트에 반영해야 두 팀의 수치가 하나로 유지된다.
    ///
    /// 레벨 0 행만 갱신하고 주석·다른 레벨 행·열 순서는 원본 그대로 보존한다 (수동 편집 존중).
    /// </summary>
    public static class StatCsvExporter
    {
        [MenuItem("NHN/에셋 → 스탯 CSV 익스포트")]
        public static void Export()
        {
            var errors = new List<string>();
            int soldierRows = ExportFile(
                StatCsvPaths.SoldierCsv, StatCsvPaths.RoleAssetName,
                LoadStats<RoleData>(r => new StatTable.StatRow(
                    r.MaxHp, r.AttackDamage, r.Defense, r.CritChancePercent, r.MoveSpeed)),
                errors);
            int generalRows = ExportFile(
                StatCsvPaths.GeneralCsv, StatCsvPaths.GeneralAssetName,
                LoadStats<GeneralData>(g => new StatTable.StatRow(
                    g.MaxHp, g.AttackDamage, g.Defense, g.CritChancePercent, g.MoveSpeed)),
                errors);

            AssetDatabase.Refresh();

            if (errors.Count > 0)
            {
                Debug.LogError($"[스탯 익스포트] 실패:\n{string.Join("\n", errors)}");
                return;
            }
            Debug.Log($"[스탯 익스포트] 레벨 {StatCsvPaths.SnapshotLevel} 행 갱신 — " +
                      $"병사 {soldierRows}행, 장군 {generalRows}행. 시트에 반영해 아웃게임과 동기화하라.");
        }

        /// <summary>레벨 0 데이터 행만 에셋 값으로 치환한다. 그 외 줄(주석·헤더·다른 레벨)은 그대로 둔다.</summary>
        private static int ExportFile(
            string path, System.Func<string, string> assetNameOf,
            Dictionary<string, StatTable.StatRow> statsByAssetName, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add($"CSV 파일이 없다: {path}");
                return 0;
            }

            string[] lines = File.ReadAllText(path).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var output = new StringBuilder();
            bool headerSeen = false;
            int replaced = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    AppendLine(output, line, i, lines.Length);
                    continue;
                }
                if (!headerSeen)
                {
                    headerSeen = true;
                    AppendLine(output, line, i, lines.Length);
                    continue;
                }

                string[] cells = trimmed.Split(',');
                if (cells.Length != 7 || !int.TryParse(cells[1].Trim(), out int level)
                    || level != StatCsvPaths.SnapshotLevel)
                {
                    AppendLine(output, line, i, lines.Length); // 형식이 다르거나 다른 레벨 → 원본 유지
                    continue;
                }

                string classId = cells[0].Trim();
                string assetName = assetNameOf(classId);
                if (!statsByAssetName.TryGetValue(assetName, out StatTable.StatRow row))
                {
                    errors.Add($"{Path.GetFileName(path)}: 병과 '{classId}'의 에셋 '{assetName}'을 찾을 수 없다");
                    AppendLine(output, line, i, lines.Length);
                    continue;
                }

                AppendLine(output, string.Join(",",
                    classId,
                    level.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StatTable.FormatValue(row.MaxHp),
                    StatTable.FormatValue(row.AttackDamage),
                    StatTable.FormatValue(row.Defense),
                    StatTable.FormatValue(row.CritChancePercent),
                    StatTable.FormatValue(row.MoveSpeed)), i, lines.Length);
                replaced++;
            }

            File.WriteAllText(path, output.ToString());
            return replaced;
        }

        private static void AppendLine(StringBuilder output, string line, int index, int total)
        {
            output.Append(line);
            if (index < total - 1)
            {
                output.Append('\n');
            }
        }

        private static Dictionary<string, StatTable.StatRow> LoadStats<T>(System.Func<T, StatTable.StatRow> selector)
            where T : Object
        {
            var map = new Dictionary<string, StatTable.StatRow>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null)
                {
                    map[asset.name] = selector(asset);
                }
            }
            return map;
        }
    }
}
