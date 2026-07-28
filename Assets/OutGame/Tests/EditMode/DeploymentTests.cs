using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;
using OutGame.Logic.Characters;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 배치 슬롯 생성(§5.7)과 배치 편집 규칙, BattleSetupData 생성(§7.1) 검증.
    /// </summary>
    public class DeploymentTests
    {
        private static readonly Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>
        {
            ["item_bow"] = new ItemData { id = "item_bow", armyClass = ArmyClass.Archer, generalSkillId = "skill_volley" },
        };

        private static readonly Dictionary<string, ArmyData> Defs = new Dictionary<string, ArmyData>
        {
            ["army_basic"] = new ArmyData { id = "army_basic", baseSoldierCount = 30, generalPower = 10f },
        };

        private static readonly List<AugmentData> NoAugments = new List<AugmentData>();

        private const string TestCharacterId = "char_test";

        private static readonly Dictionary<string, PlayerCharacterData> Characters = new Dictionary<string, PlayerCharacterData>
        {
            [TestCharacterId] = new PlayerCharacterData { id = TestCharacterId, skillId = "skill_test" },
        };

        // BuildSetup의 enemyComposition 파라미터는 non-null 검증 외에는 이 테스트 파일의 다른 관심사와
        // 무관하므로 빈 리스트를 기본값으로 쓰고, 실제로 실려 가는지는 아래 전용 테스트에서 확인한다.
        private static readonly List<EnemyArmy> NoEnemies = new List<EnemyArmy>();

        private RunState run;
        private DeploymentState deployment;

        [SetUp]
        public void SetUp()
        {
            MapState map = new MapGenerator(new MapGenerationConfig(), 42).Generate();
            run = RunStateFactory.Create(map, new RunConfig { startingArmyCount = 3 });
            run.selectedCharacterId = TestCharacterId;
            deployment = new DeploymentState(new BattleFieldConfigData().GenerateSlots());
        }

        // ── 슬롯 생성 ────────────────────────────────────────────────

        [Test]
        public void GenerateSlots_Default3x3_Produces9UniqueSlotsInUnitRange()
        {
            List<SlotDefinition> slots = new BattleFieldConfigData().GenerateSlots();

            Assert.AreEqual(9, slots.Count);
            Assert.AreEqual(9, slots.Select(s => s.slotId).Distinct().Count());
            Assert.IsTrue(slots.All(s => s.x > 0f && s.x < 1f && s.y > 0f && s.y < 1f),
                "정규화 좌표는 (0,1) 개구간 안이어야 함 (§5.7)");
        }

        [Test]
        public void GenerateSlots_CustomGrid_RespectsRowsAndColumns()
        {
            var config = new BattleFieldConfigData { rows = 2, columns = 4 };
            Assert.AreEqual(8, config.GenerateSlots().Count);
        }

        [Test]
        public void GenerateSlots_InvalidGrid_Throws()
        {
            Assert.Throws<ArgumentException>(() => new BattleFieldConfigData { rows = 0 }.GenerateSlots());
            Assert.Throws<ArgumentException>(() => new BattleFieldConfigData { columns = 0 }.GenerateSlots());
        }

        [Test]
        public void GenerateSlots_1x1_ProducesSingleCenteredSlot()
        {
            List<SlotDefinition> slots = new BattleFieldConfigData { rows = 1, columns = 1 }.GenerateSlots();

            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual(0.5f, slots[0].x, 1e-3f);
            Assert.AreEqual(0.5f, slots[0].y, 1e-3f);
        }

        // ── 배치 편집 ────────────────────────────────────────────────

        [Test]
        public void Place_IntoEmptySlot_Deploys()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 0);

            Assert.AreEqual(army, deployment.GetArmyAt(0));
            Assert.AreEqual(0, deployment.GetSlotOf(army));
            Assert.AreEqual(1, deployment.DeployedCount);
        }

        [Test]
        public void Place_SameArmyToAnotherSlot_Moves()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 0);
            deployment.Place(army, 4);

            Assert.IsNull(deployment.GetArmyAt(0));
            Assert.AreEqual(army, deployment.GetArmyAt(4));
            Assert.AreEqual(1, deployment.DeployedCount, "이동은 중복 배치가 아님");
        }

        [Test]
        public void Place_DeployedArmyOntoOccupiedSlot_Swaps()
        {
            string armyA = run.armies[0].instanceId;
            string armyB = run.armies[1].instanceId;
            deployment.Place(armyA, 0);
            deployment.Place(armyB, 1);

            deployment.Place(armyA, 1); // 슬롯 간 드래그 — 스왑

            Assert.AreEqual(armyA, deployment.GetArmyAt(1));
            Assert.AreEqual(armyB, deployment.GetArmyAt(0));
        }

        [Test]
        public void Place_FromListOntoOccupiedSlot_DisplacesOccupant()
        {
            string armyA = run.armies[0].instanceId;
            string armyB = run.armies[1].instanceId;
            deployment.Place(armyA, 0);

            deployment.Place(armyB, 0); // 목록 → 점유 슬롯: 기존 부대는 목록으로

            Assert.AreEqual(armyB, deployment.GetArmyAt(0));
            Assert.IsNull(deployment.GetSlotOf(armyA));
            Assert.AreEqual(1, deployment.DeployedCount);
        }

        [Test]
        public void Place_InvalidSlotOrArmy_Throws()
        {
            Assert.Throws<ArgumentException>(() => deployment.Place(run.armies[0].instanceId, 99));
            Assert.Throws<ArgumentException>(() => deployment.Place("", 0));
        }

        [Test]
        public void Place_SameArmyOntoOwnSlot_IsNoOp()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 4);

            deployment.Place(army, 4); // 자기 자신의 슬롯에 재배치

            Assert.AreEqual(army, deployment.GetArmyAt(4));
            Assert.AreEqual(4, deployment.GetSlotOf(army));
            Assert.AreEqual(1, deployment.DeployedCount);
        }

        [Test]
        public void Remove_DeployedArmy_ReturnsToList()
        {
            string army = run.armies[0].instanceId;
            deployment.Place(army, 0);
            deployment.Remove(army);

            Assert.IsNull(deployment.GetArmyAt(0));
            Assert.IsNull(deployment.GetSlotOf(army));
            Assert.AreEqual(0, deployment.DeployedCount);
        }

        [Test]
        public void Remove_ArmyNotDeployed_IsNoOp()
        {
            string army = run.armies[0].instanceId;
            Assert.DoesNotThrow(() => deployment.Remove(army));
            Assert.AreEqual(0, deployment.DeployedCount);
        }

        [Test]
        public void SlotCount_ReflectsGeneratedSlots()
        {
            Assert.AreEqual(9, deployment.SlotCount, "기본 3×3 배치판 = 9슬롯");
        }

        [Test]
        public void IsValidSlot_InRangeAndOutOfRange()
        {
            Assert.IsTrue(deployment.IsValidSlot(0));
            Assert.IsTrue(deployment.IsValidSlot(8));
            Assert.IsFalse(deployment.IsValidSlot(9), "3×3 배치판은 슬롯 0~8만 존재");
            Assert.IsFalse(deployment.IsValidSlot(-1));
        }

        [Test]
        public void Placements_ReflectsCurrentAssignments()
        {
            string armyA = run.armies[0].instanceId;
            string armyB = run.armies[1].instanceId;
            deployment.Place(armyA, 0);
            deployment.Place(armyB, 4);

            Dictionary<string, int> placements = deployment.Placements.ToDictionary(kv => kv.Key, kv => kv.Value);

            Assert.AreEqual(2, placements.Count);
            Assert.AreEqual(0, placements[armyA]);
            Assert.AreEqual(4, placements[armyB]);
        }

        [Test]
        public void CanStartBattle_RequiresAtLeastOneArmy()
        {
            Assert.IsFalse(deployment.CanStartBattle, "빈 배치로는 전투 시작 불가 (§5.7)");
            deployment.Place(run.armies[0].instanceId, 0);
            Assert.IsTrue(deployment.CanStartBattle);
        }

        // ── BattleSetupData 생성 (§7.1) ──────────────────────────────

        [Test]
        public void BuildSetup_ProducesContractFields()
        {
            run.ownedItemIds.Add("item_bow");
            string archer = run.armies[0].instanceId;
            ItemEquipService.Equip(run, archer, "item_bow");
            run.armies[0].bonusSoldierCount = 6;

            deployment.Place(archer, 4);
            deployment.Place(run.armies[1].instanceId, 0);

            BattleSetupData setup = deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs, NoAugments, Characters, NoEnemies);

            Assert.AreEqual("room_2_0", setup.roomId);
            Assert.AreEqual(RoomType.NormalBattle, setup.roomType);
            Assert.AreEqual("enc_default", setup.encounterId);
            Assert.AreEqual(2, setup.armies.Count);
            Assert.AreEqual(TestCharacterId, setup.playerCharacterId, "§5.2.5: 선택된 캐릭터 id 전달");
            Assert.AreEqual("skill_test", setup.playerCharacterSkillId, "캐릭터의 스킬 id만 전달 (세부 효과는 인게임 책임)");

            DeployedArmy deployed = setup.armies.First(a => a.armyInstanceId == archer);
            Assert.AreEqual("army_basic", deployed.armyDefId);
            Assert.AreEqual(ArmyClass.Archer, deployed.armyClass);
            Assert.AreEqual("item_bow", deployed.equippedItemId);
            Assert.AreEqual("skill_volley", deployed.generalSkillId, "병과 부여 시 장군 스킬 전달 (§4-23)");
            Assert.AreEqual(36, deployed.soldierCount, "기본 30 + 증원 6 (§5.5)");
            Assert.AreEqual(4, deployed.slotId);
            Assert.IsTrue(deployed.slotX > 0f && deployed.slotX < 1f);
            Assert.IsTrue(deployed.slotY > 0f && deployed.slotY < 1f);
            // 업그레이드/증강 없음 → 배율 1.0, ArmyData 기본값(생성자 초안값) 그대로 전달돼야 함.
            Assert.AreEqual(100f, deployed.generalHealth, 1e-3f);
            Assert.AreEqual(10f, deployed.generalAttack, 1e-3f);
            Assert.AreEqual(5f, deployed.generalDefense, 1e-3f);
            Assert.AreEqual(5f, deployed.generalCritRate, 1e-3f);
            Assert.AreEqual(100f, deployed.generalMoveSpeed, 1e-3f);
            Assert.AreEqual(50f, deployed.soldierHealth, 1e-3f);
            Assert.AreEqual(5f, deployed.soldierAttack, 1e-3f);
            Assert.AreEqual(2f, deployed.soldierDefense, 1e-3f);

            DeployedArmy basic = setup.armies.First(a => a.armyInstanceId != archer);
            Assert.AreEqual(ArmyClass.None, basic.armyClass);
            Assert.IsTrue(string.IsNullOrEmpty(basic.generalSkillId), "기본 군대는 장군 스킬 없음 (§5.5)");
        }

        [Test]
        public void BuildSetup_IncludesEnemyComposition()
        {
            // 2026-07-29: 적 구성은 아웃게임이 확정해서 §7 계약(BattleSetupData.enemies)에 그대로
            // 실어 보낸다 — 인게임이 encounterId만으로 별도 재생성할 필요가 없어야 한다.
            deployment.Place(run.armies[0].instanceId, 0);
            var enemyComposition = new List<EnemyArmy>
            {
                new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.Archer, soldierCount = 20 },
                new EnemyArmy { armyDefId = "army_basic", armyClass = ArmyClass.None, soldierCount = 15 },
            };

            BattleSetupData setup = deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs, NoAugments, Characters,
                enemyComposition);

            Assert.AreEqual(2, setup.enemies.Count);
            Assert.AreEqual(ArmyClass.Archer, setup.enemies[0].armyClass);
            Assert.AreEqual(20, setup.enemies[0].soldierCount);
        }

        [Test]
        public void BuildSetup_NullEnemyComposition_Throws()
        {
            deployment.Place(run.armies[0].instanceId, 0);
            Assert.Throws<ArgumentNullException>(() => deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs, NoAugments, Characters, null));
        }

        [Test]
        public void BuildSetup_UnknownSelectedCharacter_Throws()
        {
            run.selectedCharacterId = "char_does_not_exist";
            deployment.Place(run.armies[0].instanceId, 0);

            Assert.Throws<ArgumentException>(() => deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs, NoAugments, Characters, NoEnemies));
        }

        [Test]
        public void BuildSetup_NullOrEmptySelectedCharacterId_ThrowsArgumentException()
        {
            // 회귀 방지 — run.selectedCharacterId가 null이면 Dictionary.TryGetValue가 의미 없는
            // ArgumentNullException을 던졌던 버그(코드 리뷰로 발견, 2026-07-26)를 명확한
            // ArgumentException으로 fail-fast 처리하도록 고쳤다.
            deployment.Place(run.armies[0].instanceId, 0);

            run.selectedCharacterId = null;
            Assert.Throws<ArgumentException>(() => deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs, NoAugments, Characters, NoEnemies));

            run.selectedCharacterId = "   ";
            Assert.Throws<ArgumentException>(() => deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs, NoAugments, Characters, NoEnemies));
        }

        [Test]
        public void BuildSetup_FinalStats_ReflectUpgradeAndAugments()
        {
            // 2026-07-26 확정: 최종 스탯은 아웃게임이 계산해서 넘긴다 — 인게임이 재계산하지 않도록.
            // 업그레이드(+10%×레벨)와 증강(StatBoost)이 장군·유닛 동일 배율로 반영되는지 검증.
            string archer = run.armies[0].instanceId;
            run.armies[0].IncrementUpgradeLevel(); // 레벨 1 → 배율 1.1
            run.ownedItemIds.Add("item_bow");
            ItemEquipService.Equip(run, archer, "item_bow");
            deployment.Place(archer, 0);

            var attackAugment = new AugmentData
            {
                id = "aug_archer_attack", category = AugmentCategory.ItemAugment, targetArmyClass = ArmyClass.Archer,
                effectType = AugmentEffectType.StatBoost, targetStat = AugmentStat.Attack, statBoostPercent = 0.15f,
            };

            BattleSetupData setup = deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs,
                new List<AugmentData> { attackAugment }, Characters, NoEnemies);

            DeployedArmy deployed = setup.armies.First(a => a.armyInstanceId == archer);
            Assert.AreEqual(110f, deployed.generalHealth, 1e-3f, "체력은 업그레이드 배율(1.1)만 적용");
            Assert.AreEqual(10f * 1.25f, deployed.generalAttack, 1e-3f, "공격력은 업그레이드(1.1)+증강(0.15) = 1.25, 장군도 증강 혜택 받음(2026-07-26)");
            Assert.AreEqual(5f * 1.1f, deployed.generalDefense, 1e-3f, "방어력은 업그레이드 배율(1.1)만 적용");
            Assert.AreEqual(5f, deployed.generalCritRate, 1e-3f, "치명타율은 배율 대상 아님");
            Assert.AreEqual(50f * 1.1f, deployed.soldierHealth, 1e-3f);
            Assert.AreEqual(5f * 1.25f, deployed.soldierAttack, 1e-3f);
            Assert.AreEqual(2f * 1.1f, deployed.soldierDefense, 1e-3f);
        }

        [Test]
        public void BuildSetup_GeneralSkillUpgradeAugments_CountedPerMatchingClassOnly()
        {
            // 스킬이 정확히 어떻게 강화되는지는 인게임 책임이라(§4-23) 넘기지 않고, 몇 번 선택했는지
            // 횟수만 DeployedArmy.generalSkillUpgradeCount로 전달한다(2026-07-26 사용자 확정).
            run.ownedItemIds.Add("item_bow");
            string archer = run.armies[0].instanceId;
            ItemEquipService.Equip(run, archer, "item_bow");
            deployment.Place(archer, 4);
            deployment.Place(run.armies[1].instanceId, 0); // 기본 군대(궁수 스킬 강화 대상 아님)

            var archerSkillAugment = new AugmentData
            {
                id = "aug_archer_skill",
                category = AugmentCategory.ItemAugment,
                targetArmyClass = ArmyClass.Archer,
                effectType = AugmentEffectType.GeneralSkillUpgrade,
            };
            // 같은 증강을 2번 선택(중복 선택/스택 허용, §4-27) — 리스트에 두 번 들어있으면 카운트도 2.
            var selectedAugments = new List<AugmentData> { archerSkillAugment, archerSkillAugment };

            BattleSetupData setup = deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc_default", run, Items, Defs, selectedAugments, Characters, NoEnemies);

            DeployedArmy archerDeployed = setup.armies.First(a => a.armyInstanceId == archer);
            Assert.AreEqual(2, archerDeployed.generalSkillUpgradeCount, "같은 스킬 강화 증강을 2번 선택하면 카운트도 2");

            DeployedArmy basicDeployed = setup.armies.First(a => a.armyInstanceId != archer);
            Assert.AreEqual(0, basicDeployed.generalSkillUpgradeCount, "궁수 전용 스킬 강화는 다른 병과에 적용되면 안 됨");
        }

        [Test]
        public void BuildSetup_NonBattleRoomType_Throws()
        {
            deployment.Place(run.armies[0].instanceId, 0);
            Assert.Throws<ArgumentException>(() => deployment.BuildSetup(
                "room_0_3", RoomType.Rest, "enc", run, Items, Defs, NoAugments, Characters, NoEnemies),
                "배치는 전투/보스 방에서만 (§4-8)");
        }

        [Test]
        public void BuildSetup_EmptyDeployment_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc", run, Items, Defs, NoAugments, Characters, NoEnemies));
        }

        [Test]
        public void BuildDeployedArmies_EmptyDeployment_ReturnsEmptyList_DoesNotThrow()
        {
            // BuildSetup과 달리 전투 시작 가능 여부/방 타입 제약이 없다 — 배치 화면의 실시간 전투력
            // 미리보기(§4-28)처럼 배치가 비어 있어도 항상 호출 가능해야 한다.
            List<DeployedArmy> result = null;
            Assert.DoesNotThrow(() => result = deployment.BuildDeployedArmies(run, Items, Defs, NoAugments));
            Assert.IsEmpty(result);
        }

        [Test]
        public void BuildDeployedArmies_MatchesBuildSetupArmies()
        {
            deployment.Place(run.armies[0].instanceId, 0);
            deployment.Place(run.armies[1].instanceId, 3);

            List<DeployedArmy> viaHelper = deployment.BuildDeployedArmies(run, Items, Defs, NoAugments);
            BattleSetupData setup = deployment.BuildSetup("room_2_0", RoomType.NormalBattle, "enc", run, Items, Defs, NoAugments, Characters, NoEnemies);

            CollectionAssert.AreEqual(
                setup.armies.Select(a => (a.armyInstanceId, a.slotId, a.soldierCount)),
                viaHelper.Select(a => (a.armyInstanceId, a.slotId, a.soldierCount)),
                "BuildSetup은 이 헬퍼 결과를 그대로 감싸는 것이어야 함 — 별도 로직이면 어긋날 수 있음");
        }

        [Test]
        public void BuildSetup_OrderIsStableAcrossMovesAndSwaps()
        {
            // 여러 이동/스왑을 거쳐도 결과 리스트는 slotId 오름차순으로 고정돼야 한다 (§7.1 계약 안정성)
            string a = run.armies[0].instanceId;
            string b = run.armies[1].instanceId;
            string c = run.armies[2].instanceId;

            deployment.Place(a, 5);
            deployment.Place(b, 1);
            deployment.Place(c, 3);
            deployment.Place(a, 1); // a↔b 스왑 → a:1, b:5
            deployment.Place(c, 3); // 자기 슬롯 재배치 (no-op)

            BattleSetupData setup = deployment.BuildSetup(
                "room_2_0", RoomType.NormalBattle, "enc", run, Items, Defs, NoAugments, Characters, NoEnemies);

            CollectionAssert.AreEqual(
                new[] { 1, 3, 5 }, setup.armies.Select(x => x.slotId).ToArray());
        }
    }
}
