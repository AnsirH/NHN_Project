using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 적 진영 병과별 앞열/뒷열 배치 검증 (2026-07-19 사용자 요청) — 근접(방패병)은 앞열,
    /// 원거리(궁수)는 뒷열에 군집돼야 한다.
    /// </summary>
    public class EnemyFormationAssignerTests
    {
        private static EnemyArmy Enemy(ArmyClass cls) => new EnemyArmy { armyDefId = "army_basic", armyClass = cls, soldierCount = 30 };

        private static List<SlotDefinition> Grid(int columns, int rows) =>
            new BattleFieldConfigData { columns = columns, rows = rows }.GenerateSlots();

        [Test]
        public void Assign_ShieldmanAndArcher_PlacedInDifferentColumns()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Archer), Enemy(ArmyClass.Shieldman) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            int shieldmanColumn = result.First(r => r.enemy.armyClass == ArmyClass.Shieldman).slot.slotId % 4;
            int archerColumn = result.First(r => r.enemy.armyClass == ArmyClass.Archer).slot.slotId % 4;

            Assert.Less(shieldmanColumn, archerColumn, "근접(방패병)은 원거리(궁수)보다 앞열(더 낮은 열 인덱스)이어야 함");
        }

        [Test]
        public void Assign_Shieldman_GoesToFrontmostColumn()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Shieldman) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(0, result[0].slot.slotId % 4, "방패병은 가장 앞열(열 인덱스 0)에 배치돼야 함");
        }

        [Test]
        public void Assign_Archer_GoesToRearmostColumn()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Archer) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(3, result[0].slot.slotId % 4, "궁수는 가장 뒷열(마지막 열)에 배치돼야 함");
        }

        [Test]
        public void Assign_ManyShieldmenExceedingOneColumn_OverflowsToNextFrontColumn()
        {
            // 열당 3칸(rows=3)인데 방패병 4명 — 앞열이 가득 차면 두 번째로 앞쪽인 열로 넘어가야 한다.
            var composition = Enumerable.Repeat(ArmyClass.Shieldman, 4).Select(Enemy).ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 3), columns: 4);

            var columnsUsed = result.Select(r => r.slot.slotId % 4).OrderBy(c => c).ToList();
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1 }, columnsUsed, "앞열(0)이 가득 차면 다음 열(1)로 넘어가야 함");
        }

        [Test]
        public void Assign_AllSlotsDistinct_NoTwoEnemiesShareASlot()
        {
            var composition = Enumerable.Range(0, 9)
                .Select(i => Enemy(i % 2 == 0 ? ArmyClass.Archer : ArmyClass.Shieldman))
                .ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(9, result.Select(r => r.slot.slotId).Distinct().Count());
        }

        [Test]
        public void Assign_MoreEnemiesThanSlots_Throws()
        {
            var composition = Enumerable.Repeat(ArmyClass.Archer, 5).Select(Enemy).ToList();
            Assert.Throws<ArgumentException>(() =>
                EnemyFormationAssigner.Assign(composition, Grid(columns: 2, rows: 2), columns: 2));
        }

        [Test]
        public void Assign_EmptyComposition_ReturnsEmptyList()
        {
            var result = EnemyFormationAssigner.Assign(new List<EnemyArmy>(), Grid(columns: 4, rows: 7), columns: 4);
            Assert.IsEmpty(result);
        }

        [Test]
        public void Assign_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                EnemyFormationAssigner.Assign(null, Grid(columns: 4, rows: 7), columns: 4));
            Assert.Throws<ArgumentNullException>(() =>
                EnemyFormationAssigner.Assign(new List<EnemyArmy>(), null, columns: 4));
        }

        [Test]
        public void Assign_SingleColumnGrid_PlacesAllRegardlessOfClass()
        {
            // columns=1이면 columnDepth가 항상 0.5(고정)라 모든 병과가 유일한 열에 몰린다.
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Shieldman), Enemy(ArmyClass.Archer) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 1, rows: 7), columns: 1);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => r.slot.slotId % 1 == 0));
        }

        [Test]
        public void Assign_TiedDistanceBetweenTwoColumns_PrefersLowerColumnThenOverflows()
        {
            // columns=2 → columnDepth는 {0, 1}. depth 0.5(ArmyClass.None)는 두 열까지 거리가 동일(0.5)해서
            // 동률이면 먼저 스캔되는 낮은 열 인덱스가 선택돼야 한다(예외 없이 결정적). rows=1이라 첫 유닛이
            // 열 0을 채우면 두 번째 유닛은 남은 열 1로 넘어간다.
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.None), Enemy(ArmyClass.None) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 2, rows: 1), columns: 2);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.Select(r => r.slot.slotId % 2).OrderBy(c => c).ToList());
        }

        [Test]
        public void DepthOf_OrdersClassesFrontToBack()
        {
            Assert.Less(EnemyFormationAssigner.DepthOf(ArmyClass.Shieldman), EnemyFormationAssigner.DepthOf(ArmyClass.None));
            Assert.Less(EnemyFormationAssigner.DepthOf(ArmyClass.None), EnemyFormationAssigner.DepthOf(ArmyClass.Archer));
        }
    }
}
