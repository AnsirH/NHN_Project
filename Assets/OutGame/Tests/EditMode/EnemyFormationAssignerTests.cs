using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Armies;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 적 진영 4병과 구역 배치 검증 (2026-07-26 사용자 확정, 4열 격자 기준) — 전사/사냥꾼은
    /// 1,2열(0-indexed 0,1), 암살자는 2,3열(0-indexed 1,2), 궁수는 3,4열(0-indexed 2,3),
    /// 기본(병과 없음)은 구역 제한 없이 전체 열. 서로 다른 구역은 절대 넘어오지 않고, 구역이
    /// 겹치는 병과끼리는 전사→사냥꾼→암살자→궁수 순으로 우선순위가 있다(기본은 항상 마지막).
    /// </summary>
    public class EnemyFormationAssignerTests
    {
        private static EnemyArmy Enemy(ArmyClass cls) => new EnemyArmy { armyDefId = "army_basic", armyClass = cls, soldierCount = 30 };

        private static List<SlotDefinition> Grid(int columns, int rows) =>
            new BattleFieldConfigData { columns = columns, rows = rows }.GenerateSlots();

        // ── 단독 배치 — 각 병과의 기본(선호) 열 ──────────────────────

        [Test]
        public void Assign_Warrior_GoesToFrontmostColumn()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Warrior) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(0, result[0].slot.slotId % 4, "전사는 최전방 열(0)부터 채워야 함");
        }

        [Test]
        public void Assign_Hunter_GoesToFrontmostColumn_WhenZoneEmpty()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Hunter) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(0, result[0].slot.slotId % 4, "사냥꾼도 전사와 같은 구역(0,1열)의 첫 열부터 채워야 함");
        }

        [Test]
        public void Assign_Assassin_GoesToMiddleColumnStart()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Assassin) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(1, result[0].slot.slotId % 4, "암살자는 중간 구역(2,3열)의 앞쪽인 2열(인덱스 1)부터 채워야 함");
        }

        [Test]
        public void Assign_Archer_GoesToFrontOfRearZone()
        {
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
        public void Assign_WarriorAndArcher_PlacedInDifferentColumns()
        {
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Archer), Enemy(ArmyClass.Warrior) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            int warriorColumn = result.First(r => r.enemy.armyClass == ArmyClass.Warrior).slot.slotId % 4;
            int archerColumn = result.First(r => r.enemy.armyClass == ArmyClass.Archer).slot.slotId % 4;

            Assert.Less(warriorColumn, archerColumn, "전사는 궁수보다 앞열(더 낮은 열 인덱스)이어야 함");
        }

        // ── 구역이 겹치는 병과끼리의 우선순위 (전사>사냥꾼, 암살자>궁수) ──

        [Test]
        public void Assign_WarriorAndHunter_ShareZoneButWarriorGetsPriority()
        {
            // 사냥꾼을 입력에 먼저 넣어도(생성 순서와 무관하게) 전사가 항상 구역의 앞쪽을 차지해야 한다.
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Hunter), Enemy(ArmyClass.Warrior) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4);

            int warriorColumn = result.First(r => r.enemy.armyClass == ArmyClass.Warrior).slot.slotId % 4;
            int hunterColumn = result.First(r => r.enemy.armyClass == ArmyClass.Hunter).slot.slotId % 4;

            Assert.AreEqual(0, warriorColumn, "전사는 입력 순서와 무관하게 공유 구역의 첫 열을 확보해야 함");
            Assert.AreEqual(1, hunterColumn, "사냥꾼은 전사에게 밀려 같은 구역의 다음 열로 넘어가야 함");
        }

        [Test]
        public void Assign_HunterExceedsSharedZoneWithWarrior_ThrowsInsteadOfCrossingIntoAssassinZone()
        {
            // 전사·사냥꾼 공유 구역(0,1열)이 열당 1칸(rows=1)이라 총 용량 2 — 전사 2명이 다 차지하면
            // 사냥꾼은 암살자 구역(1,2열)에 빈 자리가 있어도 절대 넘어가면 안 되고 예외가 나야 한다.
            var composition = Enumerable.Repeat(ArmyClass.Warrior, 2).Select(Enemy)
                .Append(Enemy(ArmyClass.Hunter)).ToList();

            Assert.Throws<ArgumentException>(() =>
                EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4));
        }

        [Test]
        public void Assign_AssassinAndArcher_ColumnContentionResolvedByPriority()
        {
            // 암살자 구역(1,2열)과 궁수 구역(2,3열)은 2열에서 겹친다. 암살자가 우선순위가 높아
            // 2열을 포함한 자기 구역을 먼저 채우고, 궁수는 밀려서 3열로 넘어가야 한다.
            var composition = Enumerable.Repeat(ArmyClass.Assassin, 2).Select(Enemy)
                .Append(Enemy(ArmyClass.Archer)).ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4);

            var assassinColumns = result.Where(r => r.enemy.armyClass == ArmyClass.Assassin)
                .Select(r => r.slot.slotId % 4).OrderBy(c => c).ToList();
            int archerColumn = result.First(r => r.enemy.armyClass == ArmyClass.Archer).slot.slotId % 4;

            CollectionAssert.AreEqual(new[] { 1, 2 }, assassinColumns, "암살자 2명이 자기 구역(1,2열)을 채워야 함");
            Assert.AreEqual(3, archerColumn, "궁수는 암살자에게 2열을 뺏겨 3열로 넘어가야 함");
        }

        [Test]
        public void Assign_ArcherExceedsSharedZoneWithAssassinTakingColumn_Throws()
        {
            // 암살자 2명이 1,2열을 다 차지하면, 궁수 구역(2,3열)은 3열만 남는다 — 궁수 2명 중
            // 하나는 3열에 들어가지만 나머지는 자리가 없어 예외가 나야 한다(암살자 구역으로
            // 되돌아가는 것도 안 됨).
            var composition = Enumerable.Repeat(ArmyClass.Assassin, 2).Select(Enemy)
                .Concat(Enumerable.Repeat(ArmyClass.Archer, 2).Select(Enemy)).ToList();

            Assert.Throws<ArgumentException>(() =>
                EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4));
        }

        // ── 기본(None) 관련 ──────────────────────────────────────────

        [Test]
        public void Assign_None_SpreadsAcrossAllColumnsWhenNeeded()
        {
            var composition = Enumerable.Repeat(ArmyClass.None, 4).Select(Enemy).ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4);

            var columnsUsed = result.Select(r => r.slot.slotId % 4).OrderBy(c => c).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, columnsUsed);
        }

        [Test]
        public void Assign_NoneListedBeforeWarrior_DoesNotStarveWarriorZone()
        {
            // 코드 리뷰 HIGH 지적 회귀 테스트: None은 구역 제한이 없어 전사와 똑같이 0열부터
            // 노리는데, 우선순위 없이 처리하면 생성 순서상 None이 먼저 나오는 경우 전사 전용
            // 구역(0,1열, 열당 1칸)을 다 차지해버려 전사가 자리를 못 찾고 예외가 난다 — 총 용량
            // (4칸)은 충분한데도 순서 때문에 실패하면 안 된다.
            var composition = new List<EnemyArmy>
            {
                Enemy(ArmyClass.None), Enemy(ArmyClass.None), Enemy(ArmyClass.Warrior), Enemy(ArmyClass.Warrior),
            };

            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4);

            Assert.AreEqual(4, result.Count);
            var warriorColumns = result.Where(r => r.enemy.armyClass == ArmyClass.Warrior)
                .Select(r => r.slot.slotId % 4).OrderBy(c => c).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1 }, warriorColumns, "전사는 생성 순서와 무관하게 자기 구역(0,1열)을 확보해야 함");
        }

        [Test]
        public void Assign_ManyWarriorsExceedingOneColumn_OverflowsToNextFrontColumn()
        {
            var composition = Enumerable.Repeat(ArmyClass.Warrior, 4).Select(Enemy).ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 3), columns: 4);

            var columnsUsed = result.Select(r => r.slot.slotId % 4).OrderBy(c => c).ToList();
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1 }, columnsUsed, "앞열(0)이 가득 차면 다음 열(1)로 넘어가야 함");
        }

        [Test]
        public void Assign_WarriorExceedingOwnZoneCapacity_ThrowsInsteadOfCrossingIntoAssassinZone()
        {
            var composition = Enumerable.Repeat(ArmyClass.Warrior, 3).Select(Enemy).ToList();

            Assert.Throws<ArgumentException>(() =>
                EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4));
        }

        // ── 일반 방어 로직 ───────────────────────────────────────────

        [Test]
        public void Assign_AllSlotsDistinct_NoTwoEnemiesShareASlot()
        {
            var classes = new[] { ArmyClass.Archer, ArmyClass.Warrior, ArmyClass.Hunter, ArmyClass.Assassin, ArmyClass.None };
            var composition = Enumerable.Range(0, 15).Select(i => Enemy(classes[i % classes.Length])).ToList();
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4);

            Assert.AreEqual(15, result.Select(r => r.slot.slotId).Distinct().Count());
        }

        [Test]
        public void Assign_AllFourDedicatedClasses_ContendAcrossEveryOverlapSimultaneously()
        {
            // 전사/사냥꾼({0,1})·암살자({1,2})·궁수({2,3}) 세 겹침 지점(0열 없음, 1열, 2열)을 한 번에
            // 채워서 우선순위(전사>사냥꾼>암살자>궁수)가 전부 동시에 맞물려도 무너지지 않는지 확인.
            // 열당 1칸(rows=1)으로 압박을 줘서 겹침이 실제로 발생하게 만든다: 전사 1(0열 확보) +
            // 사냥꾼 1(0열 밀려서 1열) + 암살자 1(1열도 밀려서 2열) + 궁수 1(2열도 밀려서 3열).
            var composition = new List<EnemyArmy>
            {
                Enemy(ArmyClass.Archer), Enemy(ArmyClass.Assassin), Enemy(ArmyClass.Hunter), Enemy(ArmyClass.Warrior),
            };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 1), columns: 4);

            Assert.AreEqual(0, result.First(r => r.enemy.armyClass == ArmyClass.Warrior).slot.slotId % 4);
            Assert.AreEqual(1, result.First(r => r.enemy.armyClass == ArmyClass.Hunter).slot.slotId % 4);
            Assert.AreEqual(2, result.First(r => r.enemy.armyClass == ArmyClass.Assassin).slot.slotId % 4);
            Assert.AreEqual(3, result.First(r => r.enemy.armyClass == ArmyClass.Archer).slot.slotId % 4);
        }

        [Test]
        public void Assign_ReservedClass_Throws()
        {
            // Cavalry/Spearman은 미사용 예약(§4-25) — 조용히 "구역 없음"으로 넘기지 않고 즉시 예외로
            // 드러나야 한다(코드 리뷰 HIGH 지적 반영: bossComposition 등에 실수로 들어가도 티가 나야 함).
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Cavalry) };
            Assert.Throws<ArgumentException>(() =>
                EnemyFormationAssigner.Assign(composition, Grid(columns: 4, rows: 7), columns: 4));

            var spearmanComposition = new List<EnemyArmy> { Enemy(ArmyClass.Spearman) };
            Assert.Throws<ArgumentException>(() =>
                EnemyFormationAssigner.Assign(spearmanComposition, Grid(columns: 4, rows: 7), columns: 4));
        }

        [Test]
        public void Assign_NonFourColumnGrid_HunterAndAssassinUseHalfSplitFallback()
        {
            // 4열이 아닌 격자(예: 필드 config가 바뀌는 경우)에서는 4병과 전용 표 대신 절반 분할로
            // 대체된다 — 사냥꾼은 전사와 같은 전방 절반, 암살자는 궁수와 같은 후방 절반.
            var composition = new List<EnemyArmy> { Enemy(ArmyClass.Hunter), Enemy(ArmyClass.Assassin) };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 6, rows: 7), columns: 6);

            int hunterColumn = result.First(r => r.enemy.armyClass == ArmyClass.Hunter).slot.slotId % 6;
            int assassinColumn = result.First(r => r.enemy.armyClass == ArmyClass.Assassin).slot.slotId % 6;

            Assert.Less(hunterColumn, 3, "6열 격자에서 사냥꾼은 전방 절반(0~2열)에 들어가야 함");
            Assert.GreaterOrEqual(assassinColumn, 3, "6열 격자에서 암살자는 후방 절반(3~5열)에 들어가야 함");
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
            var composition = new List<EnemyArmy>
            {
                Enemy(ArmyClass.Warrior), Enemy(ArmyClass.Archer), Enemy(ArmyClass.Hunter), Enemy(ArmyClass.Assassin),
            };
            var result = EnemyFormationAssigner.Assign(composition, Grid(columns: 1, rows: 7), columns: 1);

            Assert.AreEqual(4, result.Count);
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
