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
        /// 2026-08-02 계약: EnemyArmy에도 최종 스탯·배치 좌표가 실려 온다 — 아군과 동일하게
        /// 재계산·자체 진형 없이 그대로 복사되는지 확인한다 (배치 화면 = 실제 전투 보장).
        /// roleId/generalId만 병과에서 인게임이 매핑한다 (에셋 선택 — 인게임 소유).
        /// </summary>
        [Test]
        public void Enemies_MapToEnemySquads_CopyingStatsAndSlots()
        {
            var enemies = new List<EnemyArmy>
            {
                new EnemyArmy
                {
                    armyDefId = "army_warrior", armyClass = ArmyClass.Warrior, soldierCount = 10,
                    upgradeLevel = 2, slotId = 3, slotX = 1f, slotY = 0.4f,
                    generalHealth = 150f, generalAttack = 15f, generalDefense = 5f,
                    generalCritRate = 7f, generalMoveSpeed = 100f,
                    soldierHealth = 60f, soldierAttack = 6f, soldierDefense = 2f,
                },
                new EnemyArmy
                {
                    armyDefId = "army_none", armyClass = ArmyClass.None, soldierCount = 8,
                    slotX = 0.5f, slotY = 0.75f,
                    generalHealth = 100f, generalAttack = 10f, generalDefense = 0f,
                    generalCritRate = 0f, generalMoveSpeed = 90f,
                    soldierHealth = 50f, soldierAttack = 5f, soldierDefense = 0f,
                },
            };
            var output = new List<SquadRequest>();
            BattleSetupConverter.AddEnemySquads(output, enemies);

            Assert.AreEqual(2, output.Count);

            // 병과 → 롤/장군 매핑은 아군과 동일 규칙 (None → 노멀 병사 + 노멀 장군)
            Assert.AreEqual("Warrior", output[0].roleId);
            Assert.IsNull(output[1].roleId, "None은 노멀 병사 폴백");
            Assert.AreEqual("NormalGeneral", output[1].generalId);
            Assert.AreEqual(10, output[0].soldierCount);

            // 배치 좌표 그대로 복사 — 배치 화면(EnemyFormationAssigner)과 실제 스폰이 일치해야 한다
            Assert.AreEqual(1f, output[0].slotX, 1e-3f);
            Assert.AreEqual(0.4f, output[0].slotY, 1e-3f);
            Assert.AreEqual(0.5f, output[1].slotX, 1e-3f);
            Assert.AreEqual(0.75f, output[1].slotY, 1e-3f);

            // 최종 스탯 그대로 복사 (아군 ToSquadRequest와 동일 원칙)
            Assert.IsTrue(output[0].HasSoldierStats, "이제 적도 스탯이 전달된다 (2026-08-02)");
            Assert.IsTrue(output[0].HasGeneralStats);
            Assert.AreEqual(60f, output[0].maxHp, 1e-3f);
            Assert.AreEqual(6f, output[0].attackDamage, 1e-3f);
            Assert.AreEqual(2f, output[0].defense, 1e-3f);
            Assert.AreEqual(150f, output[0].generalMaxHp, 1e-3f);
            Assert.AreEqual(15f, output[0].generalAttackDamage, 1e-3f);
            Assert.AreEqual(5f, output[0].generalDefense, 1e-3f);
            Assert.AreEqual(7f, output[0].critChancePercent, 1e-3f, "치명타는 장군·유닛 공유, 퍼센트 그대로");
            Assert.AreEqual(100f * 0.035f, output[0].moveSpeed, 1e-3f, "이동속도는 인게임 단위 환산");
            Assert.AreEqual(0, output[0].generalSkillUpgradeCount, "적은 스킬 강화 증강이 없다");
        }

        /// <summary>
        /// §5.2.5: 캐릭터가 스킬을 결정한다 — 계약 skillId(가칭 skill_char_1~4)가 요청에 실리고,
        /// 카탈로그 매핑을 거쳐 스킬 1종으로 제한된다. 미전달·미등록이면 전체 스킬 폴백.
        /// </summary>
        [Test]
        public void PlayerCharacterSkill_RestrictsLoadout_WithLenientFallback()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/Data/BattleCatalog.asset");
            Assert.IsNotNull(catalog);

            // 계약 → 요청 복사
            var setup = new BattleSetupData { roomId = "room-1", playerCharacterSkillId = "skill_char_3" };
            Assert.AreEqual("skill_char_3", BattleSetupConverter.ToBattleRequest(setup).playerSkillId);

            // 카탈로그 매핑 (char_1~4 → 독구름/번개/전투 함성/힐 장판 순 — 2026-08-03 사용자 확정)
            Assert.AreEqual("PoisonCloud", catalog.ResolveSkill("skill_char_1").name);
            Assert.AreEqual("Lightning", catalog.ResolveSkill("skill_char_2").name);
            Assert.AreEqual("WarCry", catalog.ResolveSkill("skill_char_3").name);
            Assert.AreEqual("HealZone", catalog.ResolveSkill("skill_char_4").name);
            Assert.IsNull(catalog.ResolveSkill(null), "미전달은 null — 호출자가 전체 폴백");
            Assert.IsNull(catalog.ResolveSkill("skill_unknown"), "미등록도 null(경고 로그) — 전투가 죽지 않게");

            // 로드아웃 선택: 해석되면 1종, 아니면 전체
            var all = new[]
            {
                AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Data/Skills/Lightning.asset"),
                AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Data/Skills/HealZone.asset"),
            };
            SkillData[] restricted = BattleRequestBuilder.SelectPlayerSkills("skill_char_4", catalog, all);
            Assert.AreEqual(1, restricted.Length, "캐릭터 스킬 1종으로 제한되어야 한다");
            Assert.AreEqual("HealZone", restricted[0].name);
            Assert.AreSame(all, BattleRequestBuilder.SelectPlayerSkills(null, catalog, all), "미전달 → 전체 스킬");
            Assert.AreSame(all, BattleRequestBuilder.SelectPlayerSkills("skill_unknown", catalog, all), "미등록 → 전체 스킬");
        }

        [Test]
        public void EmptyOrNullEnemies_LeaveEnemySquadsEmpty_ForFallbackPath()
        {
            var output = new List<SquadRequest>();
            BattleSetupConverter.AddEnemySquads(output, null);
            BattleSetupConverter.AddEnemySquads(output, new List<EnemyArmy>());
            Assert.AreEqual(0, output.Count, "빈 적 목록은 encounterId 폴백 경로를 위해 그대로 비워둔다");
        }

        /// <summary>
        /// §7.4 복귀 경로: 씬 교체 전투에서 인게임이 결과를 보관하면 복귀한 아웃게임이 정확히 한 번
        /// 소비한다 — 두 번 나오면 다음 전투 결과와 섞이고, 리셋 후 남으면 테스트 간 오염이 생긴다.
        /// </summary>
        [Test]
        public void BattleBridge_PendingResult_RoundTripsExactlyOnce()
        {
            var result = new BattleResultData { roomId = "room-9", victory = true };
            BattleBridge.SetPendingResult(result);
            Assert.AreSame(result, BattleBridge.ConsumePendingResult(), "보관한 결과가 그대로 나와야 한다");
            Assert.IsNull(BattleBridge.ConsumePendingResult(), "소비는 1회 — 다음 전투와 섞이면 안 된다");

            BattleBridge.SetPendingResult(result);
            BattleBridge.ResetToDefault();
            Assert.IsNull(BattleBridge.ConsumePendingResult(), "ResetToDefault는 보관 결과도 비워야 한다");
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

        /// <summary>
        /// 뷰 전용 전투 이벤트(공격/치명타/피격): 전투가 돌면 이벤트가 쌓이고, 유닛 인덱스가 유효하며,
        /// 소비(Clear) 후 비워지는지 — 애니메이션 트리거 연결의 시뮬 쪽 계약을 검증한다.
        /// </summary>
        [Test]
        public void Simulation_EmitsViewEvents_ForAttacksAndDamage()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/Data/BattleCatalog.asset");
            var table = AssetDatabase.LoadAssetAtPath<EncounterTable>("Assets/Data/EncounterTable.asset");
            var configAsset = AssetDatabase.LoadAssetAtPath<BattleConfigSO>("Assets/Data/BattleConfig.asset");
            BattleConfig config = configAsset.ToConfig();

            var request = new BattleRequest { encounterId = "enc_NormalBattle", seed = 7 };
            request.playerSquads.Add(new SquadRequest
            {
                squadId = "ev-1", roleId = "Warrior", generalId = "WarriorGeneral",
                soldierCount = 10, slotX = 1f, slotY = 0.5f,
            });
            ArmyDefinition player = BattleRequestBuilder.BuildPlayerArmy(request, catalog, config, null, null);
            ArmyDefinition enemy = BattleRequestBuilder.BuildEnemyArmy(
                request.encounterId, table, catalog, config, null);
            var sim = new BattleSimulation(config, player, enemy, request.seed);

            bool sawAttack = false, sawDamaged = false;
            int safetyTicks = 200_000;
            while (!sim.Finished && safetyTicks-- > 0 && !(sawAttack && sawDamaged))
            {
                sim.Tick();
                for (int e = 0; e < sim.ViewEventCount; e++)
                {
                    BattleSimulation.ViewEvent viewEvent = sim.GetViewEvent(e);
                    Assert.That(viewEvent.Unit, Is.InRange(0, sim.UnitCount - 1), "이벤트 유닛 인덱스는 유효 범위");
                    if (viewEvent.Type != BattleSimulation.ViewEventType.Damaged)
                    {
                        sawAttack = true;
                    }
                    else
                    {
                        sawDamaged = true;
                    }
                }
                sim.ClearViewEvents();
            }
            Assert.IsTrue(sawAttack, "전투 중 공격 이벤트가 나와야 한다");
            Assert.IsTrue(sawDamaged, "전투 중 피격 이벤트가 나와야 한다");
            Assert.AreEqual(0, sim.ViewEventCount, "Clear 후에는 비어 있어야 한다");
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
                    new EnemyArmy
                    {
                        armyDefId = "army_none", armyClass = ArmyClass.None, soldierCount = 12,
                        slotX = 1f, slotY = 0.5f,
                        generalHealth = 120f, generalAttack = 12f, generalMoveSpeed = 100f,
                        soldierHealth = 55f, soldierAttack = 5f,
                    },
                    new EnemyArmy
                    {
                        armyDefId = "army_archer", armyClass = ArmyClass.Archer, soldierCount = 6,
                        slotX = 0.3f, slotY = 0.5f,
                        generalHealth = 130f, generalAttack = 14f, generalMoveSpeed = 100f,
                        soldierHealth = 45f, soldierAttack = 7f,
                    },
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
            // 적 유닛도 전달 스탯 (2026-08-02) — 유닛 순서는 A군 전체 다음이 B군: 병사 24+장군 1 = 25번부터 적
            Assert.AreEqual(55f, sim.GetHp(25), 1e-3f, "적 병사 체력도 전달값이어야 한다 (.asset 폴백 아님)");

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
