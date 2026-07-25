using System;
using System.Collections.Generic;
using System.Linq;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 배치 슬롯을 "어느 열을 먼저 채울지" + "그 열 안에서 어느 행부터 채울지" 순서로 정렬한다
    /// (2026-07-26 사용자 확정 — 아군/적 모두 "가운데 전방"이 기본 배치 기준점). 아군 자동 배치
    /// (ArmyDeploymentPanel)와 적 병과별 배치(EnemyFormationAssigner)가 공통으로 재사용한다.
    /// </summary>
    public static class SlotPriorityOrder
    {
        /// <summary>
        /// 가운데 행부터 시작해서 위아래로 번갈아 확장하는 행 순서를 만든다 (예: rows=7이면
        /// [3,2,4,1,5,0,6] — 0-indexed. 1-indexed로는 4행→3행→5행→2행→6행→1행→7행).
        /// </summary>
        public static List<int> CenterOutRowOrder(int rows)
        {
            if (rows < 1) throw new ArgumentException($"rows는 1 이상이어야 합니다. 현재: {rows}");

            var order = new List<int>(rows) { (rows - 1) / 2 };
            int center = order[0];
            for (int offset = 1; order.Count < rows; offset++)
            {
                int down = center - offset;
                int up = center + offset;
                if (down >= 0) order.Add(down);
                if (up < rows && order.Count < rows) order.Add(up);
            }
            return order;
        }

        /// <summary>
        /// columnPriority에 나열된 열을 그 순서대로(먼저 나온 열을 완전히 소진할 때까지) 훑고, 각
        /// 열 안에서는 CenterOutRowOrder로 슬롯을 나열한다. columnPriority에 없는 열은 결과에서
        /// 아예 빠진다 — 호출자가 그 열을 쓸 수 없는 병과/진영이라는 뜻이다.
        /// </summary>
        public static List<SlotDefinition> ByColumnPriority(
            IReadOnlyList<SlotDefinition> slots, int columns, IReadOnlyList<int> columnPriority)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (columnPriority == null) throw new ArgumentNullException(nameof(columnPriority));
            if (columns < 1) throw new ArgumentException($"columns는 1 이상이어야 합니다. 현재: {columns}");
            if (slots.Count % columns != 0)
                throw new ArgumentException(
                    $"슬롯 수({slots.Count})가 columns({columns})로 나누어떨어지지 않습니다 — GenerateSlots() 결과가 아닌 듯합니다.");

            int rows = slots.Count / columns;
            List<int> rowOrder = CenterOutRowOrder(rows);

            // slotId % columns = 열, slotId / columns = 행 — GenerateSlots()의 row-major 생성 규칙(§5.7).
            var slotByPosition = new Dictionary<(int row, int col), SlotDefinition>();
            foreach (SlotDefinition slot in slots)
                slotByPosition[(slot.slotId / columns, slot.slotId % columns)] = slot;

            var ordered = new List<SlotDefinition>();
            foreach (int col in columnPriority)
                foreach (int row in rowOrder)
                    if (slotByPosition.TryGetValue((row, col), out SlotDefinition slot))
                        ordered.Add(slot);
            return ordered;
        }
    }
}
