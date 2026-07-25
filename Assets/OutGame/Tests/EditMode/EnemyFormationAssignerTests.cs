using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 적 진영 병과별 구역 배치 검증 (2026-07-26 사용자 확정) — 근접(방패병)은 전방 절반 열을
    /// 1→2 순으로, 원거리(궁수)는 후방 절반 열을 3→4 순으로, 기본(병과 없음)은 구역 제한 없이
    /// 최전방부터 전체 열을 쓴다. 서로 다른 병과는 상대 구역을 절대 넘어오지 않는다.
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

            Assert.AreEqual(0, result[0].slot.slotId % 4, "방패병은 전방 구역의 첫 열(0)부터 채워야 함");
        }

        [Test]
        public void Assign_Archer_GoesToFrontOfRearZone()
        {
            // 2026-07-26: 궁수 시작 열이 기존 4열(맨 뒤)에서 3열(후방 구역의 앞쪽)로 변경됐다.
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Archer) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(2, result[0].slot.slotId % 4, "궁수는 후방 구역(3,4열)의 앞쪽인 3열(인덱스 2)부터 채워야 함");
        }

        [Test]
        public void Assign_None_StartsAtFrontmostColumn()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.None) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(0, result[0].slot.slotId % 4, "기본 병과는 무조건 최전방 열(0)부터 채워야 함");
        }

        [Test]
        public void Assign_None_SpreadsAcrossAllColumnsWhenNeeded()
        {
            // 열당 1칸(rows=1)이라 기본 병과 4명은 반드시 4개 열 전부를 하나씩 써야 한다 — 근접/원거리
            // 처럼 구역 제한이 없다는 걸 확인.
            var composition = Enumerable.Repeat(ArmyClass.None, 4).Select(Enemy).ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4);

            var columnsUsed = result.Select(r => r.slot.slotId % 4).OrderBy(c => c).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, columnsUsed);
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
        public void Assign_ShieldmanExceedingOwnZoneCapacity_ThrowsInsteadOfCrossingIntoArcherZone()
        {
            // 방패병 구역(0,1열)은 열당 1칸(rows=1)이라 총 용량 2 — 3번째 방패병은 궁수 구역(2,3열)에
            // 빈 자리가 있어도 절대 넘어가면 안 되고 예외가 나야 한다(2026-07-26 사용자 확정: 자기
            // 구역 밖으로는 절대 안 넘어감).
            var composition = Enumerable.Repeat(ArmyClass.Shieldman, 3).Select(Enemy).ToList();

            Assert.Throws<ArgumentException>(() =>
                EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4));
        }

        [Test]
        public void Assign_NoneListedBeforeShieldman_DoesNotStarveShieldmanZone()
        {
            // 코드 리뷰 HIGH 지적 회귀 테스트: None은 구역 제한이 없어 방패병과 똑같이 0열부터
            // 노리는데, 정렬이 병과 우선순위 없이 열 번호로만 매겨지면(둘 다 zone[0]=0) 생성 순서상
            // None이 먼저 나오는 경우 방패병 전용 구역(0,1열, 열당 1칸)을 다 차지해버려 방패병이
            // 자리를 못 찾고 예외가 난다 — 총 용량(4칸)은 충분한데도 순서 때문에 실패하면 안 된다.
            var composition = new List<EnemyArmy>
            {
                Enemy(ArmyClass.None), Enemy(ArmyClass.None), Enemy(ArmyClass.Shieldman), Enemy(ArmyClass.Shieldman),
            };

            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4);

            Assert.AreEqual(4, result.Count);
            var shieldmanColumns = result.Where(r => r.enemy.armyClass == ArmyClass.Shieldman)
                .Select(r => r.slot.slotId % 4).OrderBy(c => c).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1 }, shieldmanColumns, "방패병은 생성 순서와 무관하게 자기 구역(0,1열)을 확보해야 함");
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
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Shieldman), Enemy(ArmyClass.Archer) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 1, rows: 7), columns: 1);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => r.slot.slotId % 1 == 0));
        }

        [Test]
        public void Assign_RowFillOrder_IsCenterOutAlternating()
        {
            // columns=1이면 slotId가 곧 행 번호라, Assign 결과 순서로 행 채우기 순서를 직접 검증할 수 있다.
            var composition = Enumerable.Repeat(ArmyClass.None, 7).Select(Enemy).ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 1, rows: 7), columns: 1);

            CollectionAssert.AreEqual(new[] { 3, 2, 4, 1, 5, 0, 6 }, result.Select(r => r.slot.slotId).ToList());
        }
    }
}
