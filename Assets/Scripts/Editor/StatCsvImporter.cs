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
    /// 스탯 CSV(원본) → .asset(레벨 0 스냅샷) 임포터.
    /// 파싱은 StatTable(Simulation) 하나가 담당한다 — BalanceLab CLI와 같은 파서를 공유한다.
    /// 대상 에셋을 못 찾으면 조용히 넘어가지 않고 실패로 보고한다 (임포트 누락이 밸런싱을 가짜로 만든다).
    /// </summary>
    public static class StatCsvImporter
    {
        [MenuItem("NHN/스탯 CSV → 에셋 임포트")]
        public static void Import()
        {
            var report = new StringBuilder();
            var errors = new List<string>();

            StatTable soldierTable = LoadTable(StatCsvPaths.SoldierCsv, errors);
            StatTable generalTable = LoadTable(StatCsvPaths.GeneralCsv, errors);
            if (errors.Count > 0)
            {
                Debug.LogError($"[스탯 임포트] 실패:\n{string.Join("\n", errors)}");
                return;
            }

            Dictionary<string, RoleData> roles = LoadAssetsByName<RoleData>();
            Dictionary<string, GeneralData> generals = LoadAssetsByName<GeneralData>();
            int updated = 0;

            for (int c = 0; c < soldierTable.ClassIds.Count; c++)
            {
                string classId = soldierTable.ClassIds[c];
                StatTable.StatRow row = soldierTable.Get(classId, StatCsvPaths.SnapshotLevel);
                string assetName = StatCsvPaths.RoleAssetName(classId);
                if (!roles.TryGetValue(assetName, out RoleData role))
                {
                    errors.Add($"병사 병과 '{classId}'에 대응하는 RoleData 에셋 '{assetName}'을 찾을 수 없다");
                    continue;
                }
                role.SetStats(row.MaxHp, row.AttackDamage, row.Defense, row.CritChancePercent, row.MoveSpeed);
                EditorUtility.SetDirty(role);
                updated++;
                report.AppendLine($"  {assetName}: HP {StatTable.FormatValue(row.MaxHp)} / " +
                                  $"공격 {StatTable.FormatValue(row.AttackDamage)} / 방어 {StatTable.FormatValue(row.Defense)} / " +
                                  $"치명 {StatTable.FormatValue(row.CritChancePercent)}% / 이속 {StatTable.FormatValue(row.MoveSpeed)}");
            }

            for (int c = 0; c < generalTable.ClassIds.Count; c++)
            {
                string classId = generalTable.ClassIds[c];
                StatTable.StatRow row = generalTable.Get(classId, StatCsvPaths.SnapshotLevel);
                string assetName = StatCsvPaths.GeneralAssetName(classId);
                if (!generals.TryGetValue(assetName, out GeneralData general))
                {
                    errors.Add($"장군 병과 '{classId}'에 대응하는 GeneralData 에셋 '{assetName}'을 찾을 수 없다");
                    continue;
                }
                general.SetStats(row.MaxHp, row.AttackDamage, row.Defense, row.CritChancePercent, row.MoveSpeed);
                EditorUtility.SetDirty(general);
                updated++;
                report.AppendLine($"  {assetName}: HP {StatTable.FormatValue(row.MaxHp)} / " +
                                  $"공격 {StatTable.FormatValue(row.AttackDamage)} / 방어 {StatTable.FormatValue(row.Defense)} / " +
                                  $"치명 {StatTable.FormatValue(row.CritChancePercent)}% / 이속 {StatTable.FormatValue(row.MoveSpeed)}");
            }

            AssetDatabase.SaveAssets();

            if (errors.Count > 0)
            {
                Debug.LogError($"[스탯 임포트] {updated}개 반영, 누락 {errors.Count}건:\n{string.Join("\n", errors)}");
                return;
            }
            Debug.Log($"[스탯 임포트] 레벨 {StatCsvPaths.SnapshotLevel} 스냅샷 {updated}개 에셋에 반영\n{report}");
        }

        private static StatTable LoadTable(string path, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add($"CSV 파일이 없다: {path}");
                return null;
            }
            try
            {
                return StatTable.Parse(File.ReadAllText(path), Path.GetFileName(path));
            }
            catch (System.Exception e)
            {
                errors.Add(e.Message);
                return null;
            }
        }

        private static Dictionary<string, T> LoadAssetsByName<T>() where T : Object
        {
            var map = new Dictionary<string, T>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    map[asset.name] = asset;
                }
            }
            return map;
        }
    }
}
