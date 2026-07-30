using System.Collections.Generic;
using NHN.Data;
using NHN.Integration;
using NHN.Simulation.Battle;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;
using UnityEditor;

namespace NHN.Simulation.Tests
{
    /// <summary>
    /// 아웃게임 계약 ↔ 인게임 요청 변환 검증.
    /// 계약이 세 번 바뀐 이력이 있어(스탯 미전달 → 재계산 → 직접 전달) 매핑이 조용히 깨지는 것을 막는 그물이다.
    /// </summary>
    public sealed class BattleSetupConverterTests
    {
        private static DeployedArmy SampleArmy()
        {
            return new DeployedArmy
            {
                armyInstanceId = "army-7",
                armyDefId = "def-basic",
                armyClass = ArmyClass.Archer,
                soldierCount = 24,
                upgradeLevel = 3,
                generalSkillUpgradeCount = 2,
                slotId = 4,
                slotX = 0.75f,
                slotY = 0.25f,

                generalHealth = 1300f,
                generalAttack = 260f,
                generalDefense = 90f,
                generalCritRate = 12f,
                generalMoveSpeed = 100f,
                soldierHealth = 180f,
                soldierAttack = 40f,
                soldierDefense = 20f,
            };
        }

        [Test]
        public void ToSquadRequest_CopiesFinalStats_AndSharesCritAndSpeed()
        {
            SquadRequest squad = BattleSetupConverter.ToSquadRequest(SampleArmy());

            Assert.AreEqual("army-7", squad.squadId, "armyInstanceId는 결과 매칭 키라 보존되어야 한다");
            Assert.AreEqual("Archer", squad.roleId);
            Assert.AreEqual("ArcherGeneral", squad.generalId, "장군은 병과에서 파생한다");
            Assert.AreEqual(24, squad.soldierCount);
            Assert.AreEqual(0.75f, squad.slotX, 1e-3f);
            Assert.AreEqual(0.25f, squad.slotY, 1e-3f);

            // 최종 스탯은 배율이 이미 반영된 값이라 재계산 없이 그대로 옮긴다.
            Assert.AreEqual(180f, squad.maxHp, 1e-3f);
            Assert.AreEqual(40f, squad.attackDamage, 1e-3f);
            Assert.AreEqual(20f, squad.defense, 1e-3f);
            Assert.AreEqual(1300f, squad.generalMaxHp, 1e-3f);
            Assert.AreEqual(260f, squad.generalAttackDamage, 1e-3f);
            Assert.AreEqual(90f, squad.generalDefense, 1e-3f);

            // 치명타는 퍼센트 단위 그대로, 이동속도는 인게임 단위로 환산 (100 → 3.5).
            Assert.AreEqual(12f, squad.critChancePercent, 1e-3f, "치명타는 0~100 퍼센트 단위 그대로");
            Assert.AreEqual(3.5f, squad.moveSpeed, 1e-3f, "이동속도는 아웃게임 단위 100이 인게임 3.5에 대응");

            Assert.AreEqual(2, squad.generalSkillUpgradeCount, "스킬 강화 횟수가 전달되어야 한다");
        }

        [Test]
        public void MapClass_CoversAllFourClasses_AndNoneFallsBackToNormal()
        {
            Assert.AreEqual("Warrior", BattleSetupConverter.MapClassToRoleId(ArmyClass.Warrior));
            Assert.AreEqual("Archer", BattleSetupConverter.MapClassToRoleId(ArmyClass.Archer));
            Assert.AreEqual("Hunter", BattleSetupConverter.MapClassToRoleId(ArmyClass.Hunter));
            Assert.AreEqual("Assassin", BattleSetupConverter.MapClassToRoleId(ArmyClass.Assassin));
            Assert.IsNull(BattleSetupConverter.MapClassToRoleId(ArmyClass.None),
                "아이템 미부여는 노멀 병사로 폴백되도록 null이어야 한다");
            Assert.AreEqual("NormalGeneral", BattleSetupConverter.MapClassToGeneralId(ArmyClass.None),
                "아이템 미부여 부대도 능력 없는 노멀 장군을 갖는다");

            // 매핑된 키가 실제 카탈로그에서 해석되는지 — 이름 오타를 즉시 잡는다.
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/Data/BattleCatalog.asset");
            Assert.IsNotNull(catalog);
            foreach (ArmyClass armyClass in new[]
                     { ArmyClass.Warrior, ArmyClass.Archer, ArmyClass.Hunter, ArmyClass.Assassin, ArmyClass.None })
            {
                string roleId = BattleSetupConverter.MapClassToRoleId(armyClass);
                string generalId = BattleSetupConverter.MapClassToGeneralId(armyClass);
                Assert.IsNotNull(catalog.ResolveRole(roleId), $"{armyClass} 롤 에셋이 카탈로그에 있어야 한다");
                Assert.IsNotNull(catalog.ResolveGeneral(generalId), $"{generalId} 에셋이 카탈로그에 있어야 한다");
            }
        }

        /// <summary>
        /// 2026-07-29 계약: 적 구성은 아웃게임이 확정해 enemies로 실어 보낸다 (병과·병사 수만 —
        /// 스탯·배치는 인게임 책임). 변환이 스탯을 비워 .asset 폴백을 타는지, 진형 규칙이
        /// 병과별 깊이·측면 균등 분산을 지키는지 확인한다.
        /// </summary>
        [Test]
        public void Enemies_MapToEnemySquads_WithClassFormationAndAssetStats()
        {
            var enemies = new List<EnemyArmy>
            {
                new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Warrior, soldierCount = 10 },
                new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.None, soldierCount = 8 },
                new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Archer, soldierCount = 6 },
            };
            var output = new List<SquadRequest>();
            BattleSetupConverter.AddEnemySquads(output, enemies);

            Assert.AreEqual(3, output.Count);

            // 병과 → 롤/장군 매핑은 아군과 동일 규칙 (None → 노멀 병사 + 노멀 장군)
            Assert.AreEqual("Warrior", output[0].roleId);
            Assert.IsNull(output[1].roleId, "None은 노멀 병사 폴백");
            Assert.AreEqual("NormalGeneral", output[1].generalId);
            Assert.AreEqual("Archer", output[2].roleId);
            Assert.AreEqual(10, output[0].soldierCount);

            // 스탯은 비워 .asset 원형값을 쓴다 — 적은 업그레이드·증강이 없다
            foreach (SquadRequest squad in output)
            {
                Assert.IsFalse(squad.HasSoldierStats, "적 분대는 스탯 미전달 → .asset 폴백이어야 한다");
                Assert.IsFalse(squad.HasGeneralStats);
            }

            // 진형: 전사·기본은 전선(1.0)에서 측면 균등 분산, 궁수는 후방(0.2) 단독 중앙
            Assert.AreEqual(1f, output[0].slotX, 1e-3f, "전사는 전선");
            Assert.AreEqual(1f, output[1].slotX, 1e-3f, "기본도 전선");
            Assert.AreEqual(1f / 3f, output[0].slotY, 1e-3f, "전선 2분대 → 1/3, 2/3 분산");
            Assert.AreEqual(2f / 3f, output[1].slotY, 1e-3f);
            Assert.AreEqual(0.2f, output[2].slotX, 1e-3f, "궁수는 후방");
            Assert.AreEqual(0.5f, output[2].slotY, 1e-3f, "후방 1분대 → 중앙");
        }

        [Test]
        public void EmptyOrNullEnemies_LeaveEnemySquadsEmpty_ForFallbackPath()
        {
            var output = new List<SquadRequest>();
            BattleSetupConverter.AddEnemySquads(output, null);
            BattleSetupConverter.AddEnemySquads(output, new List<EnemyArmy>());
            Assert.AreEqual(0, output.Count, "빈 적 목록은 encounterId 폴백 경로를 위해 그대로 비워둔다");
        }

        [Test]
        public void StableSeed_IsDeterministic_PerRoom()
        {
            Assert.AreEqual(
                BattleSetupConverter.StableSeed("room-12"), BattleSetupConverter.StableSeed("room-12"),
                "같은 방은 같은 시드 — 재도전 시 같은 전개");
            Assert.AreNotEqual(
                BattleSetupConverter.StableSeed("room-12"), BattleSetupConverter.StableSeed("room-13"),
                "다른 방은 다른 시드");
            Assert.AreEqual(0, BattleSetupConverter.StableSeed(null));
        }

        /// <summary>변환 결과가 실제로 전투를 구성하고, 결과가 계약 형태로 되돌아오는지 (왕복 확인).</summary>
        [Test]
        public void ConvertedSetup_RunsBattle_AndResultMapsBack()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/Data/BattleCatalog.asset");
            var table = AssetDatabase.LoadAssetAtPath<EncounterTable>("Assets/Data/EncounterTable.asset");
            var configAsset = AssetDatabase.LoadAssetAtPath<BattleConfigSO>("Assets/Data/BattleConfig.asset");
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(table);
            Assert.IsNotNull(configAsset);
            BattleConfig config = configAsset.ToConfig();

            var setup = new BattleSetupData
            {
                roomId = "room-42",
                encounterId = "enc_NormalBattle",
                armies = new List<DeployedArmy> { SampleArmy() },
                enemies = new List<EnemyArmy>
                {
                    new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.None, soldierCount = 12 },
                    new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Archer, soldierCount = 6 },
                },
            };

            BattleRequest request = BattleSetupConverter.ToBattleRequest(setup);
            Assert.AreEqual("enc_NormalBattle", request.encounterId);
            Assert.AreEqual(1, request.playerSquads.Count);
            Assert.AreEqual(2, request.enemySquads.Count, "아웃게임 확정 적 구성이 요청에 실려야 한다");

            var squadIds = new List<string>();
            ArmyDefinition player = BattleRequestBuilder.BuildPlayerArmy(request, catalog, config, null, squadIds);
            // 부트스트랩(RunBattle)과 같은 분기: enemySquads가 정본, 비었을 때만 EncounterTable 폴백.
            ArmyDefinition enemy = request.enemySquads.Count > 0
                ? BattleRequestBuilder.BuildSquads(request.enemySquads, catalog, config, null, null)
                : BattleRequestBuilder.BuildEnemyArmy(request.encounterId, table, catalog, config, null);
            Assert.AreEqual(12 + 1 + 6 + 1, enemy.TotalUnits, "적 = 기본 12+장군 + 궁수 6+장군이어야 한다");
            var sim = new BattleSimulation(config, player, enemy, request.seed);

            // 전달 스탯이 실제 유닛에 반영됐는지 (분대 첫 병사 = 인덱스 0)
            Assert.AreEqual(180f, sim.GetHp(0), 1e-3f, "병사 체력은 전달값이어야 한다");
            Assert.AreEqual(1300f, sim.GetHp(sim.GetGeneralUnit(0)), 1e-3f, "장군 체력은 전달값이어야 한다");

            int safetyTicks = 1_000_000;
            while (!sim.Finished && safetyTicks-- > 0)
            {
                sim.Tick();
            }
            Assert.IsTrue(sim.Finished, "변환된 구성으로 전투가 종료되어야 한다");

            BattleOutcome outcome = BattleRequestBuilder.BuildOutcome(sim, squadIds);
            BattleResultData result = BattleSetupConverter.ToResultData(setup.roomId, outcome);
            Assert.AreEqual("room-42", result.roomId, "roomId는 그대로 되돌려준다");
            Assert.AreEqual(outcome.Victory, result.victory);
            Assert.AreEqual(1, result.survivals.Count);
            Assert.AreEqual("army-7", result.survivals[0].armyInstanceId, "생존 집계 키가 보존되어야 한다");
            Assert.That(result.survivals[0].survivedSoldierCount, Is.InRange(0, 24));
        }
    }
}
