using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 생성된 적 구성(§4-28)을 배치 슬롯에 배정한다 (2026-07-26 사용자 확정 규칙):
    /// 근접(방패병)은 전방 절반 열을 1→2 순으로, 원거리(궁수)는 후방 절반 열을 3→4 순으로 채우고
    /// 서로의 구역을 절대 넘어오지 않는다 — 자기 구역에 자리가 없으면 예외. 기본(병과 없음)은 구역
    /// 제한 없이 최전방 열부터 전체 열을 순서대로 쓴다. 각 열 안에서는 SlotPriorityOrder가 정하는
    /// 가운데 행부터 위아래로 번갈아 채운다.
    ///
    /// 슬롯 좌표(SlotDefinition.x)는 원래 아군 기준으로 "0=후방, 1=전방"으로 정의됐다(§5.7) — 적
    /// 진영은 화면 반대쪽(중앙 기준 아군과 마주보는 방향)이라 앞/뒤가 뒤집힌다. 이 클래스는 슬롯을
    /// x가 아니라 "열 인덱스"로 다루고, **열 인덱스가 낮을수록 적 진영의 앞열(플레이어와 가까움)**로
    /// 재해석한다 — GenerateSlots()가 슬롯을 만드는 순서와 동일한 열 인덱스를 그대로 쓴다.
    /// </summary>
    public static class EnemyFormationAssigner
    {
        /// <summary>
        /// 각 적 유닛에 슬롯을 하나씩 배정한다. 슬롯보다 적 유닛이 많거나, 특정 병과가 자기 구역
        /// 안에서 자리를 못 찾으면 예외 — 호출자가 §4-28 밸런스 설정을 조정해야 한다는 뜻이다.
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

            // 전용 구역이 있는 병과(근접/원거리)를 먼저 배정해 자기 구역을 확보하게 하고, 구역 제한이
            // 없는 기본(None)은 항상 나중에 배정한다 — 그렇지 않으면 None도 최전방 열(0)을 근접과
            // 똑같이 최우선으로 노려서, 생성 순서에 따라 근접이 자기 구역을 다 못 쓰고도 예외가 나는
            // 상황이 생길 수 있다(코드 리뷰 HIGH 지적: 실패 여부가 용량이 아니라 생성 순서에 좌우됨).
            // 전용 구역 병과끼리는 구역의 첫 열(가장 앞) 오름차순, 같은 값이면 안정 정렬로 생성 순서 유지.
            var sortedComposition = composition
                .OrderBy(enemy => HasDedicatedZone(enemy.armyClass) ? 0 : 1)
                .ThenBy(enemy => ColumnZoneOf(enemy.armyClass, columns)[0])
                .ToList();

            var usedSlotIds = new HashSet<int>();
            var result = new List<(EnemyArmy, SlotDefinition)>(composition.Count);
            foreach (EnemyArmy enemy in sortedComposition)
            {
                IReadOnlyList<int> zone = ColumnZoneOf(enemy.armyClass, columns);
                List<SlotDefinition> candidates = SlotPriorityOrder.ByColumnPriority(slots, columns, zone);
                int chosenIndex = candidates.FindIndex(s => !usedSlotIds.Contains(s.slotId));
                if (chosenIndex < 0)
                    throw new ArgumentException(
                        $"{enemy.armyClass} 병과가 배치 가능한 구역(열: {string.Join(",", zone)})에 더 이상 빈 슬롯이 없습니다.");

                SlotDefinition chosen = candidates[chosenIndex];
                usedSlotIds.Add(chosen.slotId);
                result.Add((enemy, chosen));
            }

            return result;
        }

        /// <summary>근접/원거리만 전용 구역(서로 못 넘어옴)을 가진다 — 그 외 병과는 구역 제한이 없다.</summary>
        private static bool HasDedicatedZone(ArmyClass armyClass) =>
            armyClass == ArmyClass.Warrior || armyClass == ArmyClass.Archer;

        /// <summary>
        /// 병과별 배치 가능 열 목록(우선순위 순, 0-indexed) — 근접은 전방 절반, 원거리는 후방 절반,
        /// 그 외(기본)는 전체 열. columns가 1이면 구분할 여지가 없으므로 전부 유일한 열만 쓴다.
        /// </summary>
        private static IReadOnlyList<int> ColumnZoneOf(ArmyClass armyClass, int columns)
        {
            if (columns == 1) return new[] { 0 };

            int frontHalfSize = columns / 2;
            switch (armyClass)
            {
                case ArmyClass.Warrior:
                    return Enumerable.Range(0, frontHalfSize).ToList();
                case ArmyClass.Archer:
                    return Enumerable.Range(frontHalfSize, columns - frontHalfSize).ToList();
                default:
                    return Enumerable.Range(0, columns).ToList();
            }
        }
    }
}
