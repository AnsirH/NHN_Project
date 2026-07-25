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

            Assert.IsTrue(table.TryGetEncounter("encounter_basic", out _));
            Assert.IsTrue(table.TryGetEncounter("encounter_boss", out _));
            Assert.IsFalse(table.TryGetEncounter("no_such_encounter", out _));

            EncounterTable.Encounter fallback = table.GetEncounterOrFallback("no_such_encounter");
            Assert.AreEqual("encounter_basic", fallback.encounterId, "미등록 encounterId는 폴백 구성을 써야 한다");
        }

        /// <summary>요청 → 전투 → 결과 왕복: 분대 id 보존 + 생존 수 집계 + victory 일관성 (계약 대응 핵심).</summary>
        [Test]
        public void BattleRequest_RoundTrip_ProducesContractOutcome()
        {
            BattleCatalog catalog = LoadCatalog();
            EncounterTable table = LoadEncounterTable();

            var request = new BattleRequest { encounterId = "encounter_basic", seed = 77 };
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
            Assert.AreEqual(29, enemy.TotalUnits, "encounter_basic = 전사 15+장군 + 궁수 12+장군 = 29유닛이어야 한다");

            var sim = new BattleSimulation(LoadConfig(), player, enemy, request.seed);
            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
            }
            Assert.IsTrue(sim.Finished, "요청 기반 전투가 종료되어야 한다");

            BattleOutcome outcome = BattleRequestBuilder.BuildOutcome(sim, squadIds);
            Assert.AreEqual((sim.Result.Winner == 0), outcome.Victory, "victory는 A군 승리와 일치해야 한다");
            Assert.AreEqual(2, outcome.Survivals.Count, "플레이어 분대 수만큼 생존 항목이 나와야 한다");
            Assert.AreEqual("army-1", outcome.Survivals[0].SquadId, "armyInstanceId(분대 id)가 보존되어야 한다");
            Assert.AreEqual("army-2", outcome.Survivals[1].SquadId);
            Assert.That(outcome.Survivals[0].SurvivedSoldierCount, Is.InRange(0, 20), "생존 수는 병사 수 이내(장군 제외)여야 한다");
            Assert.That(outcome.Survivals[1].SurvivedSoldierCount, Is.InRange(0, 10));
        }
    }
}
