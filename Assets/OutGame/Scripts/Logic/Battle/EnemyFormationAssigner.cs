using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 생성된 적 구성(§4-28)을 배치 슬롯에 배정한다 — 근접 병과(방패병)는 앞열, 원거리 병과(궁수)는
    /// 뒷열에 배치되도록 열(column) 단위로 군집시킨다(2026-07-19 사용자 요청).
    ///
    /// 슬롯 좌표(SlotDefinition.x)는 원래 아군 기준으로 "0=후방, 1=전방"으로 정의됐다(§5.7) — 적
    /// 진영은 화면 반대쪽(중앙 기준 아군과 마주보는 방향)이라 앞/뒤가 뒤집힌다. 이 클래스는 슬롯을
    /// x가 아니라 "열 인덱스"로 다루고, **열 인덱스가 낮을수록 적 진영의 앞열(플레이어와 가까움)**로
    /// 재해석한다 — GenerateSlots()가 슬롯을 만드는 순서와 동일한 열 인덱스를 그대로 쓴다.
    /// </summary>
    public static class EnemyFormationAssigner
    {
        /// <summary>0 = 근접(전방), 1 = 원거리(후방). 기본/미정 병과는 중간 값.</summary>
        public static float DepthOf(ArmyClass armyClass) => armyClass switch
        {
            ArmyClass.Shieldman => 0f,
            ArmyClass.Archer => 1f,
            _ => 0.5f,
        };

        /// <summary>
        /// 각 적 유닛에 슬롯을 하나씩 배정한다. 슬롯보다 적 유닛이 많으면 예외 — 호출자가 슬롯 수를
        /// 충분히(현재는 아군과 같은 격자 크기) 준비해야 한다.
        /// </summary>
        public static List<(EnemyArmy enemy, SlotDefinition slot)> Assign(
            IReadOnlyList<EnemyArmy> composition, IReadOnlyList<SlotDefinition> slots, int columns)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (columns < 1) throw new ArgumentException($"columns는 1 이상이어야 합니다. 현재: {columns}");
            if (composition.Count > slots.Count)
                throw new ArgumentException(
                    $"배치할 적({composition.Count})이 슬롯 수({slots.Count})보다 많습니다.");

            // slotId % columns = 열 인덱스 — GenerateSlots()가 row-major(슬롯ID = row×columns+col)로
            // 만들기 때문에 성립한다(BattleFieldConfigData.GenerateSlots 참고).
            var slotsByColumn = new List<List<SlotDefinition>>(columns);
            for (int c = 0; c < columns; c++) slotsByColumn.Add(new List<SlotDefinition>());
            foreach (SlotDefinition slot in slots)
                slotsByColumn[slot.slotId % columns].Add(slot);

            var usedPerColumn = new int[columns];

            // 병과 depth 오름차순(앞→뒤) 정렬 — OrderBy는 안정 정렬이라 같은 병과 안에서는 생성 순서가 유지된다.
            var sortedComposition = composition.OrderBy(enemy => DepthOf(enemy.armyClass)).ToList();

            var result = new List<(EnemyArmy, SlotDefinition)>(composition.Count);
            foreach (EnemyArmy enemy in sortedComposition)
            {
                float targetDepth = DepthOf(enemy.armyClass);

                int bestColumn = -1;
                float bestDistance = float.MaxValue;
                for (int c = 0; c < columns; c++)
                {
                    if (usedPerColumn[c] >= slotsByColumn[c].Count) continue; // 그 열은 이미 가득 참

                    float columnDepth = columns > 1 ? (float)c / (columns - 1) : 0.5f;
                    float distance = Math.Abs(columnDepth - targetDepth);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestColumn = c;
                    }
                }

                // composition.Count <= slots.Count로 이미 가드했으므로 항상 여유 열이 있어야 한다.
                SlotDefinition chosen = slotsByColumn[bestColumn][usedPerColumn[bestColumn]];
                usedPerColumn[bestColumn]++;
                result.Add((enemy, chosen));
            }

            return result;
        }
    }
}
