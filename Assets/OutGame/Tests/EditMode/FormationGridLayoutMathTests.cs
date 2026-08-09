using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Battle;
using UnityEngine;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// 진영 슬롯 격자를 런타임 Instantiate 대신 에디터에 미리 구워 넣기 위한 순수 배치 계산
    /// (FormationGridLayoutMath) 검증(2026-08-07, 3차 수정 — 셀 비율 스트레치 방식에서 고정 픽셀
    /// 디자인 + 격자 전체 균일 스케일 방식으로 되돌림). 실제 게임 격자 크기(BattleFieldConfig_Default:
    /// rows=7, columns=4)를 기준으로 슬롯 28개/커넥터 45개가 정확히 계산되는지 확인한다.
    /// </summary>
    public class FormationGridLayoutMathTests
    {
        private static List<SlotDefinition> Grid(int rows, int columns) =>
            new BattleFieldConfigData { rows = rows, columns = columns }.GenerateSlots();

        [Test]
        public void DesignSize_MatchesRowsColumnsTimesCellSize()
        {
            Vector2 size = FormationGridLayoutMath.DesignSize(rows: 7, columns: 4);

            Assert.AreEqual(4 * FormationGridLayoutMath.CellSize, size.x, 0.0001f);
            Assert.AreEqual(7 * FormationGridLayoutMath.CellSize, size.y, 0.0001f);
        }

        [Test]
        public void SlotDesignPosition_TopLeftSlot_IsInTopLeftQuadrantOfCanvas()
        {
            var slots = Grid(rows: 7, columns: 4);
            SlotDefinition topLeft = slots.First(s => s.slotId == 0);

            Vector2 pos = FormationGridLayoutMath.SlotDesignPosition(topLeft, rows: 7, columns: 4);

            // 캔버스 중앙이 원점이므로 좌상단 슬롯은 x<0(왼쪽), y>0(위쪽)이어야 함.
            Assert.Less(pos.x, 0f);
            Assert.Greater(pos.y, 0f);
        }

        [Test]
        public void SlotDesignPosition_AdjacentColumns_AreExactlyOneCellApart()
        {
            var slots = Grid(rows: 2, columns: 3);
            Vector2 a = FormationGridLayoutMath.SlotDesignPosition(slots.First(s => s.slotId == 0), rows: 2, columns: 3);
            Vector2 b = FormationGridLayoutMath.SlotDesignPosition(slots.First(s => s.slotId == 1), rows: 2, columns: 3);

            Assert.AreEqual(FormationGridLayoutMath.CellSize, b.x - a.x, 0.0001f,
                "가로로 인접한 슬롯 중심 간 거리는 항상 CellSize와 정확히 같아야 함(고정 디자인이므로 화면비와 무관)");
            Assert.AreEqual(a.y, b.y, 0.0001f);
        }

        [Test]
        public void SlotDesignPosition_AdjacentRows_AreExactlyOneCellApart()
        {
            var slots = Grid(rows: 2, columns: 3);
            Vector2 a = FormationGridLayoutMath.SlotDesignPosition(slots.First(s => s.slotId == 0), rows: 2, columns: 3);
            Vector2 c = FormationGridLayoutMath.SlotDesignPosition(slots.First(s => s.slotId == 3), rows: 2, columns: 3);

            Assert.AreEqual(FormationGridLayoutMath.CellSize, a.y - c.y, 0.0001f,
                "세로로 인접한 슬롯 중심 간 거리는 항상 CellSize와 정확히 같아야 함");
            Assert.AreEqual(a.x, c.x, 0.0001f);
        }

        [Test]
        public void ConnectorPairs_7x4Grid_ProducesExpectedCount()
        {
            // 가로 인접: rows*(columns-1) = 7*3 = 21, 세로 인접: (rows-1)*columns = 6*4 = 24, 합계 45.
            var slots = Grid(rows: 7, columns: 4);

            var pairs = FormationGridLayoutMath.ConnectorPairs(slots, rows: 7, columns: 4).ToList();

            Assert.AreEqual(45, pairs.Count);
            Assert.AreEqual(21, pairs.Count(p => p.horizontal));
            Assert.AreEqual(24, pairs.Count(p => !p.horizontal));
        }

        [Test]
        public void ConnectorPairs_EverySlotAppearsInAtLeastOneConnector()
        {
            // 전체 격자 연결(사용자 확정 사양) — 모서리 슬롯을 포함해 어떤 슬롯도 고립되면 안 된다.
            var slots = Grid(rows: 3, columns: 3);
            var pairs = FormationGridLayoutMath.ConnectorPairs(slots, rows: 3, columns: 3).ToList();

            var touchedSlotIds = pairs.SelectMany(p => new[] { p.from.slotId, p.to.slotId }).ToHashSet();

            CollectionAssert.AreEquivalent(slots.Select(s => s.slotId), touchedSlotIds);
        }

        [Test]
        public void ConnectorPairs_MismatchedSlotCount_Throws()
        {
            var slots = Grid(rows: 2, columns: 2);

            Assert.Throws<System.ArgumentException>(() =>
                FormationGridLayoutMath.ConnectorPairs(slots, rows: 3, columns: 3).ToList());
        }

        [Test]
        public void ConnectorDesignRect_HorizontalPair_IsCenteredBetweenSlotsWithFixedThickness()
        {
            var slots = Grid(rows: 2, columns: 3);
            SlotDefinition a = slots.First(s => s.slotId == 0); // row0,col0
            SlotDefinition b = slots.First(s => s.slotId == 1); // row0,col1

            (Vector2 position, Vector2 size) = FormationGridLayoutMath.ConnectorDesignRect(a, b, horizontal: true, rows: 2, columns: 3);

            Vector2 aPos = FormationGridLayoutMath.SlotDesignPosition(a, rows: 2, columns: 3);
            Vector2 bPos = FormationGridLayoutMath.SlotDesignPosition(b, rows: 2, columns: 3);
            Assert.AreEqual((aPos.x + bPos.x) * 0.5f, position.x, 0.0001f);
            Assert.AreEqual(aPos.y, position.y, 0.0001f);
            Assert.AreEqual(FormationGridLayoutMath.CellSize - FormationGridLayoutMath.SlotSize, size.x, 0.0001f,
                "가로 커넥터 길이는 '중심 간 거리 - 슬롯 지름'으로 슬롯 원 뒤에 살짝 겹쳐야 함");
            Assert.AreEqual(FormationGridLayoutMath.ConnectorThickness, size.y, 0.0001f);
        }

        [Test]
        public void ConnectorDesignRect_VerticalPair_IsCenteredBetweenSlotsWithFixedThickness()
        {
            var slots = Grid(rows: 2, columns: 3);
            SlotDefinition a = slots.First(s => s.slotId == 0); // row0,col0
            SlotDefinition c = slots.First(s => s.slotId == 3); // row1,col0

            (Vector2 position, Vector2 size) = FormationGridLayoutMath.ConnectorDesignRect(a, c, horizontal: false, rows: 2, columns: 3);

            Assert.AreEqual(FormationGridLayoutMath.CellSize - FormationGridLayoutMath.SlotSize, size.y, 0.0001f,
                "세로 커넥터 길이는 '중심 간 거리 - 슬롯 지름'으로 슬롯 원 뒤에 살짝 겹쳐야 함");
            Assert.AreEqual(FormationGridLayoutMath.ConnectorThickness, size.x, 0.0001f);
        }
    }
}
