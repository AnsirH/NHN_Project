using System;
using System.Collections.Generic;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 배치 슬롯 사양 (상세 기획 §5.7 — 기본 3×3, config로 조정).
    /// 슬롯 좌표는 진영 내 정규화(0~1) — 인게임이 실제 월드 좌표로 변환한다.
    /// </summary>
    [Serializable]
    public class BattleFieldConfigData
    {
        public int rows = 3;
        public int columns = 3;

        public List<SlotDefinition> GenerateSlots()
        {
            if (rows < 1) throw new ArgumentException($"rows는 1 이상이어야 합니다. 현재: {rows}");
            if (columns < 1) throw new ArgumentException($"columns는 1 이상이어야 합니다. 현재: {columns}");

            var slots = new List<SlotDefinition>();
            int slotId = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    slots.Add(new SlotDefinition
                    {
                        slotId = slotId++,
                        x = (col + 0.5f) / columns,
                        y = (row + 0.5f) / rows,
                    });
                }
            }

            return slots;
        }
    }

    [Serializable]
    public struct SlotDefinition
    {
        public int slotId;
        public float x; // 0~1, 진영 내 가로 (0=후방, 1=전방 — 인게임 협의 시 확정)
        public float y; // 0~1, 진영 내 세로
    }
}
