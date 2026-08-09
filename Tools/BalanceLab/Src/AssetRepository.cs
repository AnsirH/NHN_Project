using System;
using System.Collections.Generic;
using System.IO;
using NHN.Simulation.Battle;

namespace BalanceLab
{
    /// <summary>
    /// Assets/Data의 .asset을 직접 읽어 순수 시뮬 정의로 변환한다 — 인게임 로컬 수치의 출처 = .asset.
    /// 실전 플레이어 부대 스탯은 아웃게임이 armyDefId+upgradeLevel로 계산해 넘기며(계약 §7.1.1),
    /// 그 결합은 커넥터가 담당한다. 여기서 읽는 값은 인게임 속성(공격 주기·사거리·타겟팅 등)과
    /// 적 구성·로컬 시나리오의 기준선이다.
    /// 이름→에셋 인덱스는 캐싱 없이 매 실행(생성자) 재구축한다 (작업 명세 v2 추가 조건).
    /// 측정 도구이므로 폴백 없음: 미등록 키/누락 필드는 즉시 실패한다 (게임 런타임의 관대한 폴백과 의도적으로 다름).
    /// </summary>
    public sealed class AssetRepository
    {
        /// <summary>병사·장군 데이터가 통합된 분대 에셋 클래스 식별자 (2026-08-10 — 옛 RoleData/GeneralData 통합).</summary>
        private const string SquadClassId = "NHN.Data::NHN.Data.SquadData";
        private const string ConfigClassId = "NHN.Data::NHN.Data.BattleConfigSO";
        /// <summary>빈 roleId의 해석 — 기획 §5 "장군 없는 분대 = 노멀 병사" (Unity 쪽 BattleCatalog.normalSquad와 동일 관례).</summary>
        private const string NormalRoleName = "Normal";
        /// <summary>generalId 계약(roleId + "General") 접미사 — Unity BattleCatalog.ResolveGeneral과 동일 규칙.</summary>
        private const string GeneralSuffix = "General";

        private readonly Dictionary<string, ParsedAsset> _squadsByName = new Dictionary<string, ParsedAsset>();
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
                if (classIdentifier != SquadClassId && classIdentifier != ConfigClassId)
                {
                    continue;
                }

                ParsedAsset parsed = UnityAssetParser.ParseAssetFile(assetPath);

                switch (parsed.ClassIdentifier)
                {
                    case SquadClassId:
                        _squadsByName[parsed.Name] = parsed;
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
                c.GetFloat("critMultiplier"),
                c.GetFloat("skillUpgradeChargeReduction"),
                c.GetFloat("minChargeRequiredRatio"),
                c.GetFloat("separationOverlapRatio"),
                c.GetFloat("separationStrength"),
                c.GetFloat("formationTightnessTolerance"),
                c.GetFloat("formationEngageRangeMargin"),
                c.GetInt("formationColumnWidth"),
                c.GetFloat("formationSpacingMultiplier"),
                c.GetFloat("meleeSuppressDuration"),
                c.GetFloat("meleeSuppressMagnitude"));
        }

        /// <summary>
        /// 병사 정의 — .asset 값을 그대로 쓴다 (로컬 밸런싱 기준선).
        /// 실전 스탯은 아웃게임이 armyDefId+upgradeLevel로 계산해 넘기며, 그 결합은 커넥터가 담당한다.
        /// </summary>
        public RoleDefinition GetRole(string roleId)
        {
            string key = string.IsNullOrEmpty(roleId) ? NormalRoleName : roleId;
            if (_roleDefinitions.TryGetValue(key, out RoleDefinition cached))
            {
                return cached;
            }
            RoleDefinition role = BuildRole(RequireSquad(key));
            _roleDefinitions[key] = role;
            return role;
        }

        /// <summary>
        /// null/빈 문자열 = 장군 없음(null). 미등록 키는 예외. generalId는 계약상 언제나
        /// "roleId + General"(Unity BattleSetupConverter.MapClassToGeneralId) 형태라 접미사를 벗기고
        /// 병사와 같은 분대 에셋에서 장군 필드를 읽는다(2026-08-10 — RoleData/GeneralData 통합).
        /// </summary>
        public GeneralDefinition GetGeneral(string generalId)
        {
            if (string.IsNullOrEmpty(generalId))
            {
                return null;
            }
            if (_generalDefinitions.TryGetValue(generalId, out GeneralDefinition cached))
            {
                return cached;
            }
            string roleId = generalId.EndsWith(GeneralSuffix, StringComparison.Ordinal)
                ? generalId.Substring(0, generalId.Length - GeneralSuffix.Length)
                : generalId;
            ParsedAsset parsed = RequireSquad(roleId);

            // 엘리트 파생 공식은 GeneralDefinition.CreateElite가 단일 출처 — Unity(SquadData.ToGeneralDefinition)와 공유.
            GeneralDefinition general = GeneralDefinition.CreateElite(
                BuildRole(parsed), parsed.Name,
                parsed.GetFloat("generalHpMultiplier"),
                parsed.GetFloat("generalDamageMultiplier"),
                parsed.GetFloat("generalSizeMultiplier"),
                ParseEnum<SquadPassive>(parsed, "generalPassive"),
                parsed.GetFloat("generalPassiveValue"),
                ParseEnum<ChargeCondition>(parsed, "generalChargeCondition"),
                parsed.GetFloat("generalChargeRequired"),
                ParseEnum<GimmickEffect>(parsed, "generalActiveEffect"),
                parsed.GetFloat("generalActiveParamA"),
                parsed.GetFloat("generalActiveParamB"),
                parsed.GetFloat("generalActiveDuration"),
                RequireLeadRankOffsetInRange(parsed));

            _generalDefinitions[generalId] = general;
            return general;
        }

        private ParsedAsset RequireSquad(string roleId)
        {
            if (!_squadsByName.TryGetValue(roleId, out ParsedAsset parsed))
            {
                throw new InvalidDataException($"미등록 롤 '{roleId}' — Assets/Data의 SquadData 에셋 이름과 일치해야 한다");
            }
            return parsed;
        }

        /// <summary>
        /// 리드 오프셋 범위 검증 — GeneralDefinition이 어차피 클램프하지만, 데이터가 범위를 벗어난 채
        /// 조용히 보정되면 튜닝 루프가 "값을 바꿨는데 결과가 그대로"인 혼란에 빠진다. 여기서 즉시 드러낸다.
        /// </summary>
        private static float RequireLeadRankOffsetInRange(ParsedAsset asset)
        {
            float value = asset.GetFloat("generalLeadRankOffset");
            if (value < GeneralDefinition.MinLeadRankOffset || value > GeneralDefinition.MaxLeadRankOffset)
            {
                throw asset.Fail(
                    $"generalLeadRankOffset {value}가 허용 범위를 벗어난다 " +
                    $"({GeneralDefinition.MinLeadRankOffset}~{GeneralDefinition.MaxLeadRankOffset} 랭크)");
            }
            return value;
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
