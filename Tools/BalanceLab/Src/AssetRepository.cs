using System;
using System.Collections.Generic;
using System.IO;
using NHN.Simulation.Balance;
using NHN.Simulation.Battle;

namespace BalanceLab
{
    /// <summary>
    /// Assets/Data의 .asset을 직접 읽어 순수 시뮬 정의로 변환한다 — 밸런스 수치의 단일 출처 = .asset.
    /// guid→경로 인덱스는 캐싱 없이 매 실행(생성자) 재구축한다 (작업 명세 v2 추가 조건).
    /// 측정 도구이므로 폴백 없음: 미등록 키/누락 필드는 즉시 실패한다 (게임 런타임의 관대한 폴백과 의도적으로 다름).
    /// </summary>
    public sealed class AssetRepository
    {
        private const string RoleClassId = "NHN.Data::NHN.Data.RoleData";
        private const string GeneralClassId = "NHN.Data::NHN.Data.GeneralData";
        private const string ConfigClassId = "NHN.Data::NHN.Data.BattleConfigSO";
        /// <summary>빈 roleId의 해석 — 기획 §5 "장군 없는 분대 = 노멀 병사" (Unity 쪽 BattleCatalog.normalRole과 동일 관례).</summary>
        private const string NormalRoleName = "Normal";

        private readonly Dictionary<string, ParsedAsset> _rolesByName = new Dictionary<string, ParsedAsset>();
        private readonly Dictionary<string, ParsedAsset> _generalsByName = new Dictionary<string, ParsedAsset>();
        private readonly Dictionary<string, ParsedAsset> _parsedByGuid = new Dictionary<string, ParsedAsset>();
        private ParsedAsset _battleConfig;

        // 실행 내 변환 캐시 (동일 실행 중 재변환 방지 — 디스크/실행 간 캐시 아님)
        private readonly Dictionary<string, RoleDefinition> _roleDefinitions = new Dictionary<string, RoleDefinition>();
        private readonly Dictionary<string, GeneralDefinition> _generalDefinitions = new Dictionary<string, GeneralDefinition>();

        public AssetRepository(string projectRoot)
        {
            string dataRoot = Path.Combine(projectRoot, "Assets", "Data");
            if (!Directory.Exists(dataRoot))
            {
                throw new DirectoryNotFoundException($"Assets/Data를 찾을 수 없다: {dataRoot}");
            }

            foreach (string assetPath in Directory.EnumerateFiles(dataRoot, "*.asset", SearchOption.AllDirectories))
            {
                // 1차 스캔: 클래스 식별자만 확인 — 관련 클래스만 전체 파싱한다
                // (무관한 에셋(EncounterTable 등 중첩 구조)에 fail-fast가 오발되지 않게. 읽는 에셋의 fail-fast는 유지).
                string classIdentifier = UnityAssetParser.ReadClassIdentifier(assetPath);
                if (classIdentifier != RoleClassId && classIdentifier != GeneralClassId && classIdentifier != ConfigClassId)
                {
                    continue;
                }

                string metaPath = assetPath + ".meta";
                if (!File.Exists(metaPath))
                {
                    throw new InvalidDataException($".meta 누락: {assetPath}");
                }
                string guid = UnityAssetParser.ParseMetaGuid(metaPath);
                ParsedAsset parsed = UnityAssetParser.ParseAssetFile(assetPath);
                _parsedByGuid[guid] = parsed;

                switch (parsed.ClassIdentifier)
                {
                    case RoleClassId:
                        _rolesByName[parsed.Name] = parsed;
                        break;
                    case GeneralClassId:
                        _generalsByName[parsed.Name] = parsed;
                        break;
                    case ConfigClassId:
                        if (_battleConfig != null)
                        {
                            throw new InvalidDataException($"BattleConfig 에셋이 2개 이상이다: {_battleConfig.FilePath} / {assetPath}");
                        }
                        _battleConfig = parsed;
                        break;
                }
            }

            if (_battleConfig == null)
            {
                throw new InvalidDataException($"BattleConfig 에셋을 찾을 수 없다 ({dataRoot})");
            }

            SoldierStats = LoadTable(Path.Combine(dataRoot, "Csv", "soldier_stats.csv"));
            GeneralStats = LoadTable(Path.Combine(dataRoot, "Csv", "general_stats.csv"));
            VerifyAssetsMatchCsv();
        }

        /// <summary>병사 스탯 전개 테이블 (병과 × 레벨) — 레벨 스윕 시나리오가 사용한다.</summary>
        public StatTable SoldierStats { get; }

        /// <summary>장군 스탯 전개 테이블 (병과 × 레벨).</summary>
        public StatTable GeneralStats { get; }

        private static StatTable LoadTable(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"스탯 CSV를 찾을 수 없다: {path}");
            }
            return StatTable.Parse(File.ReadAllText(path), Path.GetFileName(path));
        }

        /// <summary>
        /// CSV 레벨 0 행과 .asset 스탯이 일치하는지 검사한다 — "임포트 안 하고 돌린" 사고 차단.
        /// 불일치를 방치하면 CLI가 튜닝한 수치와 게임이 쓰는 수치가 갈라져 밸런싱 전체가 가짜가 된다.
        /// </summary>
        private void VerifyAssetsMatchCsv()
        {
            var mismatches = new List<string>();
            CompareTable(SoldierStats, _rolesByName, name => name, mismatches, isGeneral: false);
            CompareTable(GeneralStats, _generalsByName, name => name + "General", mismatches, isGeneral: true);
            if (mismatches.Count > 0)
            {
                throw new InvalidDataException(
                    "CSV 레벨 0과 .asset 스탯이 다르다 — Unity에서 [NHN/스탯 CSV → 에셋 임포트]를 먼저 실행하라:\n  "
                    + string.Join("\n  ", mismatches));
            }
        }

        private void CompareTable(
            StatTable table, Dictionary<string, ParsedAsset> assetsByName,
            Func<string, string> assetNameOf, List<string> mismatches, bool isGeneral)
        {
            for (int c = 0; c < table.ClassIds.Count; c++)
            {
                string classId = table.ClassIds[c];
                string assetName = assetNameOf(classId);
                if (!assetsByName.TryGetValue(assetName, out ParsedAsset asset))
                {
                    mismatches.Add($"{table.SourceName}: 병과 '{classId}'의 에셋 '{assetName}'이 없다");
                    continue;
                }
                StatTable.StatRow row = table.Get(classId, level: 0);

                // 장군 에셋은 스탯 미지정(maxHp 0) 시 배율 파생을 쓰므로, 그 경우는 CSV 미반영으로 본다.
                if (isGeneral && asset.GetFloat("maxHp") <= 0f)
                {
                    mismatches.Add($"{assetName}: 스탯이 비어 있다 (CSV 임포트 필요)");
                    continue;
                }

                CompareField(assetName, "maxHp", asset.GetFloat("maxHp"), row.MaxHp, mismatches);
                CompareField(assetName, "attackDamage", asset.GetFloat("attackDamage"), row.AttackDamage, mismatches);
                CompareField(assetName, "defense", asset.GetFloat("defense"), row.Defense, mismatches);
                CompareField(assetName, "critChancePercent", asset.GetFloat("critChancePercent"), row.CritChancePercent, mismatches);
                CompareField(assetName, "moveSpeed", asset.GetFloat("moveSpeed"), row.MoveSpeed, mismatches);
            }
        }

        private static void CompareField(string assetName, string field, float assetValue, float csvValue, List<string> mismatches)
        {
            if (Math.Abs(assetValue - csvValue) > 1e-3f)
            {
                mismatches.Add($"{assetName}.{field}: 에셋 {StatTable.FormatValue(assetValue)} vs CSV {StatTable.FormatValue(csvValue)}");
            }
        }

        public BattleConfig LoadBattleConfig()
        {
            ParsedAsset c = _battleConfig;
            return new BattleConfig(
                c.GetInt("ticksPerSecond"),
                c.GetFloat("arenaHalfWidth"),
                c.GetFloat("arenaHalfHeight"),
                c.GetFloat("retargetInterval"),
                c.GetFloat("projectileImpactRadius"),
                c.GetFloat("maxBattleSeconds"),
                c.GetInt("maxUnits"),
                c.GetInt("maxProjectiles"),
                c.GetInt("maxSkillZones"),
                c.GetFloat("frontLineOffsetX"),
                c.GetFloat("deploymentDepth"),
                c.GetFloat("deploymentHalfWidth"),
                c.GetFloat("defenseK"),
                c.GetFloat("critMultiplier"));
        }

        /// <summary>
        /// 병사 정의 = .asset(인게임 속성) + CSV 레벨별 5스탯 결합 —
        /// 실제 게임의 연결 경로(아웃게임이 스탯을 보내는 구조)와 **같은 결합 함수**를 타므로
        /// 밸런싱이 실전과 같은 코드로 검증된다.
        /// </summary>
        public RoleDefinition GetRole(string roleId, int level)
        {
            string key = string.IsNullOrEmpty(roleId) ? NormalRoleName : roleId;
            string cacheKey = key + "@" + level;
            if (_roleDefinitions.TryGetValue(cacheKey, out RoleDefinition cached))
            {
                return cached;
            }
            if (!_rolesByName.TryGetValue(key, out ParsedAsset parsed))
            {
                throw new InvalidDataException($"미등록 roleId '{key}' — Assets/Data의 RoleData 에셋 이름과 일치해야 한다");
            }
            RequireLevelInRange(SoldierStats, level);
            StatTable.StatRow row = SoldierStats.Get(key, level);
            RoleDefinition role = RoleDefinition.WithStats(
                BuildRole(parsed), row.MaxHp, row.AttackDamage, row.Defense, row.CritChancePercent, row.MoveSpeed);
            _roleDefinitions[cacheKey] = role;
            return role;
        }

        /// <summary>null/빈 문자열 = 장군 없음(null). 미등록 키는 예외.</summary>
        public GeneralDefinition GetGeneral(string generalId, int level)
        {
            if (string.IsNullOrEmpty(generalId))
            {
                return null;
            }
            string cacheKey = generalId + "@" + level;
            if (_generalDefinitions.TryGetValue(cacheKey, out GeneralDefinition cached))
            {
                return cached;
            }
            if (!_generalsByName.TryGetValue(generalId, out ParsedAsset parsed))
            {
                throw new InvalidDataException($"미등록 generalId '{generalId}' — Assets/Data의 GeneralData 에셋 이름과 일치해야 한다");
            }

            string baseGuid = parsed.GetGuidRef("baseRole");
            if (!_parsedByGuid.TryGetValue(baseGuid, out ParsedAsset baseParsed) || baseParsed.ClassIdentifier != RoleClassId)
            {
                throw parsed.Fail($"baseRole guid '{baseGuid}'가 RoleData 에셋으로 해석되지 않는다");
            }

            // 엘리트 파생 공식은 GeneralDefinition.CreateElite가 단일 출처 — Unity(GeneralData.ToDefinition)와 공유.
            GeneralDefinition general = GeneralDefinition.CreateElite(
                BuildRole(baseParsed), parsed.Name,
                parsed.GetFloat("hpMultiplier"),
                parsed.GetFloat("damageMultiplier"),
                parsed.GetFloat("sizeMultiplier"),
                ParseEnum<SquadPassive>(parsed, "passive"),
                parsed.GetFloat("passiveValue"),
                ParseEnum<ChargeCondition>(parsed, "chargeCondition"),
                parsed.GetFloat("chargeRequired"),
                ParseEnum<GimmickEffect>(parsed, "activeEffect"),
                parsed.GetFloat("activeParamA"),
                parsed.GetFloat("activeParamB"),
                parsed.GetFloat("activeDuration"));

            // 장군 스탯도 병과별 × 레벨별 표에서 온다 (능력은 에셋, 스탯은 표 — 연결 경로와 같은 분리).
            if (!StatTable.TryClassIdFromGeneralAssetName(parsed.Name, out string generalClassId))
            {
                throw parsed.Fail($"장군 에셋 이름이 규약('<병과>{StatTable.GeneralAssetSuffix}')을 벗어난다");
            }
            RequireLevelInRange(GeneralStats, level);
            StatTable.StatRow generalRow = GeneralStats.Get(generalClassId, level);
            general = GeneralDefinition.WithCombatRole(
                general,
                RoleDefinition.WithStats(
                    general.CombatRole,
                    generalRow.MaxHp, generalRow.AttackDamage, generalRow.Defense,
                    generalRow.CritChancePercent, generalRow.MoveSpeed));

            _generalDefinitions[cacheKey] = general;
            return general;
        }

        private static void RequireLevelInRange(StatTable table, int level)
        {
            if (level < 0 || level > table.MaxLevel)
            {
                throw new InvalidDataException(
                    $"{table.SourceName}: 레벨 {level}은 범위를 벗어난다 (0~{table.MaxLevel})");
            }
        }

        private static RoleDefinition BuildRole(ParsedAsset a)
        {
            List<int> priorityInts = a.GetIntList("priorities");
            var priorities = new TargetPriority[priorityInts.Count];
            for (int i = 0; i < priorityInts.Count; i++)
            {
                priorities[i] = ParseEnumValue<TargetPriority>(a, "priorities", priorityInts[i]);
            }
            return new RoleDefinition(
                a.Name,
                a.GetFloat("maxHp"),
                a.GetFloat("attackDamage"),
                a.GetFloat("defense"),
                a.GetFloat("critChancePercent"),
                a.GetFloat("attackInterval"),
                a.GetFloat("attackRange"),
                a.GetFloat("moveSpeed"),
                a.GetFloat("unitRadius"),
                a.GetFloat("projectileSpeed"),
                a.GetFloat("projectileArcHeight"),
                ParseEnum<PositionFilter>(a, "positionFilter"),
                priorities,
                ParseEnum<MovePattern>(a, "movePattern"),
                a.GetFloat("moveParamA"),
                a.GetFloat("moveParamB"));
        }

        private static T ParseEnum<T>(ParsedAsset asset, string key) where T : struct, Enum
        {
            return ParseEnumValue<T>(asset, key, asset.GetInt(key));
        }

        private static T ParseEnumValue<T>(ParsedAsset asset, string key, int rawValue) where T : struct, Enum
        {
            var value = (T)Enum.ToObject(typeof(T), rawValue);
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw asset.Fail($"'{key}' 값 {rawValue}는 {typeof(T).Name}에 정의되지 않은 케이스다");
            }
            return value;
        }
    }
}
