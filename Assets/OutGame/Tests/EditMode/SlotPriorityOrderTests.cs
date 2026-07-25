using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Battle;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// "가운데 전방부터 채운다"는 기본 배치 규칙(2026-07-26 사용자 확정)의 핵심 유틸 검증.
    /// </summary>
    public class SlotPriorityOrderTests
    {
        [Test]
        public void CenterOutRowOrder_SevenRows_StartsAtCenterAndAlternates()
        {
            CollectionAssert.AreEqual(new[] { 3, 2, 4, 1, 5, 0, 6 }, SlotPriorityOrder.CenterOutRowOrder(7));
        }

        [Test]
        public void CenterOutRowOrder_OneRow_ReturnsSingleZero()
        {
            CollectionAssert.AreEqual(new[] { 0 }, SlotPriorityOrder.CenterOutRowOrder(1));
        }

        [Test]
        public void CenterOutRowOrder_EvenRows_CoversAllIndicesExactlyOnce()
        {
            List<int> order = SlotPriorityOrder.CenterOutRowOrder(4);
            Assert.AreEqual(4, order.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, order);
        }

        [Test]
        public void CenterOutRowOrder_ZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentException>(() => SlotPriorityOrder.CenterOutRowOrder(0));
        }

        private static List<SlotDefinition> Grid(int columns, int rows) =>
            new BattleFieldConfigData { columns = columns, rows = rows }.GenerateSlots();

        [Test]
        public void ByColumnPriority_ExhaustsFirstColumnBeforeMovingToNext()
        {
            List<SlotDefinition> slots = Grid(columns: 4, rows: 3);
            List<SlotDefinition> ordered = SlotPriorityOrder.ByColumnPriority(slots, columns: 4, columnPriority: new[] { 2, 0 });

            // 열 2(3개) 전부가 열 0(3개) 앞에 나와야 함
            var columns = ordered.Select(s => s.slotId % 4).ToList();
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 0, 0, 0 }, columns);
        }

        [Test]
        public void ByColumnPriority_WithinColumn_FollowsCenterOutRowOrder()
        {
            List<SlotDefinition> slots = Grid(columns: 1, rows: 7);
            List<SlotDefinition> ordered = SlotPriorityOrder.ByColumnPriority(slots, columns: 1, columnPriority: new[] { 0 });

            CollectionAssert.AreEqual(new[] { 3, 2, 4, 1, 5, 0, 6 }, ordered.Select(s => s.slotId).ToList());
        }

        [Test]
        public void ByColumnPriority_ColumnNotInPriorityList_IsExcludedFromResult()
        {
            List<SlotDefinition> slots = Grid(columns: 4, rows: 7);
            List<SlotDefinition> ordered = SlotPriorityOrder.ByColumnPriority(slots, columns: 4, columnPriority: new[] { 1 });

            Assert.AreEqual(7, ordered.Count, "우선순위 목록에 없는 열은 결과에서 완전히 빠져야 함");
            Assert.IsTrue(ordered.All(s => s.slotId % 4 == 1));
        }

        [Test]
        public void ByColumnPriority_NullArguments_Throw()
        {
            List<SlotDefinition> slots = Grid(columns: 4, rows: 7);
            Assert.Throws<ArgumentNullException>(() => SlotPriorityOrder.ByColumnPriority(null, 4, new[] { 0 }));
            Assert.Throws<ArgumentNullException>(() => SlotPriorityOrder.ByColumnPriority(slots, 4, null));
        }
    }
}
