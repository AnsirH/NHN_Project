using NHN.Data;
using NHN.Simulation.Battle;
using NUnit.Framework;
using UnityEditor;

namespace NHN.Simulation.Tests
{
    /// <summary>
    /// 아웃게임 연동 선행 준비 검증 — 카탈로그 키 해석 / 정규화 슬롯 변환 / 요청→전투→결과 왕복이
    /// outGame BattleBridge 계약(armyInstanceId 보존, victory, 생존 수)에 맞는 형태로 동작하는지 확인한다.
    /// </summary>
    public sealed class BattleRequestTests
    {
        private const string CatalogPath = "Assets/Data/BattleCatalog.asset";
        private const string EncounterPath = "Assets/Data/EncounterTable.asset";
        private const string ConfigPath = "Assets/Data/BattleConfig.asset";

        private static BattleCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} 에셋이 있어야 한다");
            return catalog;
        }

        private static EncounterTable LoadEncounterTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTable>(EncounterPath);
            Assert.IsNotNull(table, $"{EncounterPath} 에셋이 있어야 한다");
            return table;
        }

        private static BattleConfig LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} 에셋이 있어야 한다");
            return config.ToConfig();
        }

        [Test]
        public void Catalog_ResolvesKeys_AndFallsBackToNormal()
        {
            BattleCatalog catalog = LoadCatalog();

            Assert.AreEqual("Warrior", catalog.ResolveRole("Warrior").name);
            Assert.AreEqual("Hunter", catalog.ResolveRole("Hunter").name);
            Assert.AreEqual("Normal", catalog.ResolveRole(null).name, "빈 roleId는 노멀 병사여야 한다 (기획 §5)");
            Assert.AreEqual("Normal", catalog.ResolveRole("Cavalry").name, "미등록 키는 노멀 폴백 — 연동 초기 키 불일치 대비");

            Assert.AreEqual("WarriorGeneral", catalog.ResolveGeneral("WarriorGeneral").name);
            Assert.IsNull(catalog.ResolveGeneral(null), "빈 generalId는 장군 없음이어야 한다");
            Assert.IsNull(catalog.ResolveGeneral("UnknownGeneral"), "미등록 장군 키는 장군 없음 폴백");
        }

        [Test]
        public void SlotToAnchor_MapsNormalizedCoordinates()
        {
            // 변환 수식은 DeploymentGrid(Simulation — CLI 공유), 파라미터는 BattleConfig가 단일 출처.
            BattleConfig config = LoadConfig();
            Assert.AreEqual(10f, config.DeploymentDepth, 1e-3f, "배치 깊이 파라미터가 BattleConfig 에셋에서 와야 한다");
            Assert.AreEqual(14f, config.DeploymentHalfWidth, 1e-3f);

            var front = DeploymentGrid.SlotToAnchor(1f, 0.5f, config.DeploymentDepth, config.DeploymentHalfWidth);
            Assert.AreEqual(0f, front.X, 1e-3f, "slotX=1은 전선(깊이 0)이어야 한다");
            Assert.AreEqual(0f, front.Y, 1e-3f, "slotY=0.5는 측면 중앙이어야 한다");

            var backTop = DeploymentGrid.SlotToAnchor(0f, 1f, config.DeploymentDepth, config.DeploymentHalfWidth);
            Assert.AreEqual(10f, backTop.X, 1e-3f, "slotX=0은 최후방(깊이 최대)이어야 한다");
            Assert.AreEqual(14f, backTop.Y, 1e-3f, "slotY=1은 측면 최대여야 한다");

            var backBottom = DeploymentGrid.SlotToAnchor(0f, 0f, config.DeploymentDepth, config.DeploymentHalfWidth);
            Assert.AreEqual(-14f, backBottom.Y, 1e-3f, "slotY=0은 측면 최소여야 한다");
        }

        [Test]
        public void EncounterTable_FallsBackOnUnknownId()
        {
            EncounterTable table = LoadEncounterTable();

            Assert.IsTrue(table.TryGetEncounter("enc_NormalBattle", out _));
            Assert.IsTrue(table.TryGetEncounter("enc_Boss", out _));
            Assert.IsFalse(table.TryGetEncounter("no_such_encounter", out _));

            EncounterTable.Encounter fallback = table.GetEncounterOrFallback("no_such_encounter");
            Assert.AreEqual("enc_NormalBattle", fallback.encounterId, "미등록 encounterId는 폴백 구성을 써야 한다");
        }

        /// <summary>
        /// 5.6-B 결합 팩토리: 아웃게임이 보낸 5스탯이 병사·장군 각각에 적용되고,
        /// 인게임 소유 속성(공격 주기·사거리·투사체·타겟팅·이동 패턴·반경)은 .asset 값이 그대로 유지되어야 한다.
        /// </summary>
        [Test]
        public void ProvidedStats_OverrideAssetStats_ButKeepInGameAttributes()
        {
            BattleCatalog catalog = LoadCatalog();
            BattleConfig config = LoadConfig();
            RoleDefinition assetRole = catalog.ResolveRole("Archer").ToDefinition();
            GeneralDefinition assetGeneral = catalog.ResolveGeneral("ArcherGeneral").ToDefinition();

            var squads = new System.Collections.Generic.List<SquadRequest>
            {
                new SquadRequest
                {
                    squadId = "lv5", roleId = "Archer", generalId = "ArcherGeneral",
                    soldierCount = 3, slotX = 1f, slotY = 0.5f,
                    maxHp = 675f, attackDamage = 236f, defense = 69f,
                    generalMaxHp = 3123f, generalAttackDamage = 565f, generalDefense = 143f,
                    critChancePercent = 20f, moveSpeed = 4.2f, // 장군·병사 공유 (아웃게임 §5.7)
                },
            };

            ArmyDefinition army = BattleRequestBuilder.BuildSquads(squads, catalog, config, null, null);
            RoleDefinition soldier = army.Squads[0].Role;
            GeneralDefinition general = army.Squads[0].General;

            // 전달된 5스탯이 적용된다
            Assert.AreEqual(675f, soldier.MaxHp, 1e-3f);
            Assert.AreEqual(236f, soldier.AttackDamage, 1e-3f);
            Assert.AreEqual(69f, soldier.Defense, 1e-3f);
            Assert.AreEqual(20f, soldier.CritChancePercent, 1e-3f);
            Assert.AreEqual(4.2f, soldier.MoveSpeed, 1e-3f);
            Assert.AreEqual(3123f, general.CombatRole.MaxHp, 1e-3f, "장군 체력·공격력·방어력은 병사와 별도로 전달된다");
            Assert.AreEqual(565f, general.CombatRole.AttackDamage, 1e-3f);
            Assert.AreEqual(143f, general.CombatRole.Defense, 1e-3f);
            Assert.AreEqual(20f, general.CombatRole.CritChancePercent, 1e-3f, "치명타는 장군·병사가 같은 값을 공유한다");
            Assert.AreEqual(4.2f, general.CombatRole.MoveSpeed, 1e-3f, "이동속도도 장군·병사 공유");

            // 인게임 소유 속성은 .asset 그대로
            Assert.AreEqual(assetRole.AttackInterval, soldier.AttackInterval, 1e-3f, "공격 주기는 인게임 소유");
            Assert.AreEqual(assetRole.AttackRange, soldier.AttackRange, 1e-3f, "사거리는 인게임 소유");
            Assert.AreEqual(assetRole.ProjectileSpeed, soldier.ProjectileSpeed, 1e-3f, "투사체 속도는 인게임 소유");
            Assert.AreEqual(assetRole.UnitRadius, soldier.UnitRadius, 1e-3f, "유닛 반경은 인게임 소유");
            Assert.AreEqual(assetRole.MovePattern, soldier.MovePattern);
            Assert.AreEqual(assetGeneral.CombatRole.UnitRadius, general.CombatRole.UnitRadius, 1e-3f,
                "장군 크기 배율(반경)은 연결 경로에서도 인게임 소유다");

            // 장군 능력(패시브·충전·액티브)은 에셋 정의 유지
            Assert.AreEqual(assetGeneral.Passive, general.Passive);
            Assert.AreEqual(assetGeneral.ChargeCondition, general.ChargeCondition);
            Assert.AreEqual(assetGeneral.ChargeRequired, general.ChargeRequired, 1e-3f);
            Assert.AreEqual(assetGeneral.ActiveEffect, general.ActiveEffect);
        }

        /// <summary>스탯 미전달(로컬 테스트·BalanceLab 경로)이면 .asset 값을 그대로 쓴다 — 회귀 방어.</summary>
        [Test]
        public void OmittedStats_FallBackToAssetValues()
        {
            BattleCatalog catalog = LoadCatalog();
            BattleConfig config = LoadConfig();
            RoleDefinition assetRole = catalog.ResolveRole("Warrior").ToDefinition();
            GeneralDefinition assetGeneral = catalog.ResolveGeneral("WarriorGeneral").ToDefinition();

            var squads = new System.Collections.Generic.List<SquadRequest>
            {
                new SquadRequest
                {
                    squadId = "local", roleId = "Warrior", generalId = "WarriorGeneral",
                    soldierCount = 5, slotX = 1f, slotY = 0.5f,
                },
            };

            ArmyDefinition army = BattleRequestBuilder.BuildSquads(squads, catalog, config, null, null);
            Assert.AreEqual(assetRole.MaxHp, army.Squads[0].Role.MaxHp, 1e-3f);
            Assert.AreEqual(assetRole.AttackDamage, army.Squads[0].Role.AttackDamage, 1e-3f);
            Assert.AreEqual(assetGeneral.CombatRole.MaxHp, army.Squads[0].General.CombatRole.MaxHp, 1e-3f,
                "장군 스탯 미전달 시 엘리트 배율(.asset) 파생값이 유지되어야 한다");
        }

        /// <summary>
        /// 같은 장군 에셋이 여러 분대에 쓰이되 분대마다 레벨(스탯)이 다를 수 있다 —
        /// 결합 팩토리가 분대별 인스턴스를 만들어 서로 간섭하지 않아야 한다.
        /// </summary>
        [Test]
        public void SameGeneralAsset_DifferentLevels_DoNotInterfere()
        {
            BattleCatalog catalog = LoadCatalog();
            BattleConfig config = LoadConfig();

            var squads = new System.Collections.Generic.List<SquadRequest>
            {
                new SquadRequest
                {
                    squadId = "lv1", roleId = "Warrior", generalId = "WarriorGeneral",
                    soldierCount = 3, slotX = 1f, slotY = 0.3f,
                    maxHp = 100f, attackDamage = 10f, defense = 5f,
                    generalMaxHp = 500f, generalAttackDamage = 50f, generalDefense = 20f,
                    critChancePercent = 0f, moveSpeed = 3f,
                },
                new SquadRequest
                {
                    squadId = "lv5", roleId = "Warrior", generalId = "WarriorGeneral",
                    soldierCount = 3, slotX = 1f, slotY = 0.7f,
                    maxHp = 300f, attackDamage = 30f, defense = 15f,
                    generalMaxHp = 1500f, generalAttackDamage = 150f, generalDefense = 60f,
                    critChancePercent = 10f, moveSpeed = 3.6f,
                },
            };

            ArmyDefinition army = BattleRequestBuilder.BuildSquads(squads, catalog, config, null, null);
            Assert.AreEqual(100f, army.Squads[0].Role.MaxHp, 1e-3f);
            Assert.AreEqual(300f, army.Squads[1].Role.MaxHp, 1e-3f);
            Assert.AreEqual(500f, army.Squads[0].General.CombatRole.MaxHp, 1e-3f);
            Assert.AreEqual(1500f, army.Squads[1].General.CombatRole.MaxHp, 1e-3f);
            Assert.AreNotSame(army.Squads[0].General, army.Squads[1].General,
                "같은 에셋이라도 분대별로 별도 정의 인스턴스여야 한다");

            // 스탯이 실제 전투에 반영되는지 (레벨 높은 분대의 장군이 더 오래 버틴다는 최소 확인)
            var enemy = new System.Collections.Generic.List<SquadRequest>
            {
                new SquadRequest { squadId = "e1", roleId = "Warrior", soldierCount = 6, slotX = 1f, slotY = 0.5f },
            };
            var sim = new BattleSimulation(
                config, army, BattleRequestBuilder.BuildSquads(enemy, catalog, config, null, null), seed: 31);
            Assert.AreEqual(500f, sim.GetHp(sim.GetGeneralUnit(0)), 1e-3f, "분대 0 장군 HP = 전달값");
            Assert.AreEqual(1500f, sim.GetHp(sim.GetGeneralUnit(1)), 1e-3f, "분대 1 장군 HP = 전달값");
        }

        /// <summary>
        /// 장군 스킬 강화(아웃게임 generalSkillUpgradeCount) → 충전 필요량 감소.
        /// 아웃게임은 횟수만 넘기고 해석은 인게임 몫이며, 인게임은 "더 자주 발동"으로 번역한다.
        /// </summary>
        [Test]
        public void SkillUpgradeCount_ReducesChargeRequirement_WithFloor()
        {
            BattleCatalog catalog = LoadCatalog();
            BattleConfig config = LoadConfig();
            float baseRequired = catalog.ResolveGeneral("WarriorGeneral").ToDefinition().ChargeRequired;
            Assert.Greater(baseRequired, 0f, "충전 필요량이 데이터로 정의돼 있어야 한다");
            Assert.Greater(config.SkillUpgradeChargeReduction, 0f, "감소율이 BattleConfig에 있어야 한다");

            // 강화 0회 = 원본 (회귀 방어)
            Assert.AreEqual(baseRequired, ChargeRequiredFor(0, catalog, config), 1e-3f,
                "강화 0회면 충전 필요량이 그대로여야 한다");

            // 강화 1·2회 = 감소율 × 횟수만큼 선형 감소
            for (int count = 1; count <= 2; count++)
            {
                float expected = baseRequired * (1f - config.SkillUpgradeChargeReduction * count);
                Assert.AreEqual(expected, ChargeRequiredFor(count, catalog, config), 1e-3f,
                    $"강화 {count}회의 충전 필요량이 공식과 일치해야 한다");
            }

            // 과다 강화는 하한 비율에서 멈춘다 (발동이 소음이 되지 않게 — 기획 §6 이벤트 희소성)
            float floor = baseRequired * config.MinChargeRequiredRatio;
            Assert.AreEqual(floor, ChargeRequiredFor(99, catalog, config), 1e-3f,
                "강화가 아무리 많아도 하한 비율에서 클램프되어야 한다");
            Assert.Greater(floor, 0f, "하한은 0보다 커야 한다 (충전 없이 무한 발동 금지)");
        }

        private static float ChargeRequiredFor(int upgradeCount, BattleCatalog catalog, in BattleConfig config)
        {
            var squads = new System.Collections.Generic.List<SquadRequest>
            {
                new SquadRequest
                {
                    squadId = "u", roleId = "Warrior", generalId = "WarriorGeneral",
                    soldierCount = 2, slotX = 1f, slotY = 0.5f,
                    generalSkillUpgradeCount = upgradeCount,
                },
            };
            ArmyDefinition army = BattleRequestBuilder.BuildSquads(squads, catalog, config, null, null);
            return army.Squads[0].General.ChargeRequired;
        }

        /// <summary>강화된 장군은 같은 전투에서 액티브를 더 많이 발동한다 (수치가 아니라 실제 거동 확인).</summary>
        [Test]
        public void SkillUpgrade_IncreasesActivationCount_InBattle()
        {
            BattleCatalog catalog = LoadCatalog();
            BattleConfig config = LoadConfig();

            int plain = ActivationsInBattle(upgradeCount: 0, catalog, config);
            int upgraded = ActivationsInBattle(upgradeCount: 4, catalog, config);

            UnityEngine.Debug.Log($"[스킬 강화] 발동 횟수 — 강화 0회: {plain}, 강화 4회: {upgraded}");
            Assert.Greater(upgraded, plain, "강화된 장군의 액티브가 더 많이 발동해야 한다");
        }

        private static int ActivationsInBattle(int upgradeCount, BattleCatalog catalog, in BattleConfig config)
        {
            // 시간 충전(사냥 선포)을 쓰는 장군이라 전투 길이만으로 발동 횟수 차이가 드러난다.
            var left = new System.Collections.Generic.List<SquadRequest>
            {
                new SquadRequest
                {
                    squadId = "L", roleId = "Hunter", generalId = "HunterGeneral",
                    soldierCount = 15, slotX = 0.5f, slotY = 0.5f,
                    generalSkillUpgradeCount = upgradeCount,
                },
            };
            var right = new System.Collections.Generic.List<SquadRequest>
            {
                new SquadRequest { squadId = "R", roleId = "Warrior", soldierCount = 25, slotX = 1f, slotY = 0.5f },
            };
            var sim = new BattleSimulation(
                config,
                BattleRequestBuilder.BuildSquads(left, catalog, config, null, null),
                BattleRequestBuilder.BuildSquads(right, catalog, config, null, null),
                seed: 17);
            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
            }
            return sim.GetSquadActivationCount(0);
        }

        /// <summary>요청 → 전투 → 결과 왕복: 분대 id 보존 + 생존 수 집계 + victory 일관성 (계약 대응 핵심).</summary>
        [Test]
        public void BattleRequest_RoundTrip_ProducesContractOutcome()
        {
            BattleCatalog catalog = LoadCatalog();
            EncounterTable table = LoadEncounterTable();

            var request = new BattleRequest { encounterId = "enc_NormalBattle", seed = 77 };
            request.playerSquads.Add(new SquadRequest
            {
                squadId = "army-1", roleId = "Warrior", generalId = "WarriorGeneral",
                soldierCount = 20, slotX = 1f, slotY = 0.5f,
            });
            request.playerSquads.Add(new SquadRequest
            {
                squadId = "army-2", soldierCount = 10, slotX = 0.6f, slotY = 0.2f, // 노멀 분대 (roleId 없음)
            });

            BattleConfig config = LoadConfig();
            var squadIds = new System.Collections.Generic.List<string>();
            ArmyDefinition player = BattleRequestBuilder.BuildPlayerArmy(request, catalog, config, viewSquadsOut: null, squadIds);
            ArmyDefinition enemy = BattleRequestBuilder.BuildEnemyArmy(request.encounterId, table, catalog, config, viewSquadsOut: null);

            Assert.AreEqual(31, player.TotalUnits, "전사 20+장군 + 노멀 10 = 31유닛이어야 한다");
            Assert.AreEqual(29, enemy.TotalUnits, "enc_NormalBattle = 전사 15+장군 + 궁수 12+장군 = 29유닛이어야 한다");

            var sim = new BattleSimulation(LoadConfig(), player, enemy, request.seed);
            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
            }
            Assert.IsTrue(sim.Finished, "요청 기반 전투가 종료되어야 한다");

            BattleOutcome outcome = BattleRequestBuilder.BuildOutcome(sim, squadIds);
            Assert.AreEqual((sim.Result.Winner != 1), outcome.Victory,
                "victory는 B군 승리가 아닐 때 참이어야 한다 (무승부는 패배 아님, 2026-08-06)");
            Assert.AreEqual(2, outcome.Survivals.Count, "플레이어 분대 수만큼 생존 항목이 나와야 한다");
            Assert.AreEqual("army-1", outcome.Survivals[0].SquadId, "armyInstanceId(분대 id)가 보존되어야 한다");
            Assert.AreEqual("army-2", outcome.Survivals[1].SquadId);
            Assert.That(outcome.Survivals[0].SurvivedSoldierCount, Is.InRange(0, 20), "생존 수는 병사 수 이내(장군 제외)여야 한다");
            Assert.That(outcome.Survivals[1].SurvivedSoldierCount, Is.InRange(0, 10));
        }
    }
}
