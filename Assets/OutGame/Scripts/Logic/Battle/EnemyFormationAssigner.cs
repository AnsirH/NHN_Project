using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 생성된 적 구성(§4-28)을 배치 슬롯에 배정한다 (2026-07-26 사용자 확정, 4병과 확장):
    /// 4열 격자 기준으로 병과별 전용 구역(열 목록)을 두고, 서로 다른 구역끼리는 절대 넘어오지
    /// 않는다 — 자기 구역에 자리가 없으면 예외.
    ///   전사(Warrior)  = 1,2열(0-indexed 0,1) — 최전방
    ///   사냥꾼(Hunter) = 1,2열(0-indexed 0,1) — 전사와 같은 구역이지만 전사 다음 우선순위
    ///   암살자(Assassin) = 2,3열(0-indexed 1,2) — 중간
    ///   궁수(Archer)   = 3,4열(0-indexed 2,3) — 최후방
    ///   기본(None)     = 구역 제한 없이 전체 열, 1열부터 시작 — 항상 가장 마지막에 배정(다른
    ///                    병과가 자기 구역을 먼저 확보하게 해서, None이 같은 열을 먼저 차지해
    ///                    전용 구역 병과를 밀어내는 순서 의존 버그를 막는다)
    /// 각 열 안에서는 SlotPriorityOrder가 정하는 가운데 행부터 위아래로 번갈아 채운다.
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

            // 전사→사냥꾼→암살자→궁수→기본 순으로 배정 — 앞쪽 우선순위가 겹치는 구역을 먼저
            // 확보하게 해서, 뒤 우선순위 병과(특히 구역 제한이 없는 기본)가 같은 열을 먼저
            // 차지해버리는 순서 의존 버그를 막는다(코드 리뷰 HIGH 지적 사례와 동일 원칙).
            var sortedComposition = composition
                .OrderBy(enemy => PriorityOf(enemy.armyClass))
                .ToList();

            var usedSlotIds = new HashSet<int>();
            var result = new List<(EnemyArmy, SlotDefinition)>(composition.Count);
            // 같은 병과(=같은 구역)를 가진 적이 여러 마리면 후보 슬롯 목록은 매번 같으므로 병과별로
            // 한 번만 계산해 재사용한다 — columns는 이 호출 안에서 고정이라 병과만으로 캐시 키가 된다.
            var candidatesByClass = new Dictionary<ArmyClass, List<SlotDefinition>>();
            foreach (EnemyArmy enemy in sortedComposition)
            {
                IReadOnlyList<int> zone = ColumnZoneOf(enemy.armyClass, columns); // 계산 자체는 가벼워 매번 다시 구해도 무방 — 캐시 대상은 아래 ByColumnPriority뿐
                if (!candidatesByClass.TryGetValue(enemy.armyClass, out List<SlotDefinition> candidates))
                {
                    candidates = SlotPriorityOrder.ByColumnPriority(slots, columns, zone);
                    candidatesByClass[enemy.armyClass] = candidates;
                }
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

        /// <summary>
        /// 배정 처리 순서(오름차순) — 전용 구역이 겹치는 병과 사이의 우선순위를 명시적으로 정한다.
        /// 기본(None)은 구역 제한이 없어 항상 마지막이어야 다른 병과 구역을 먼저 확보시킬 수 있다.
        /// </summary>
        private static int PriorityOf(ArmyClass armyClass)
        {
            switch (armyClass)
            {
                case ArmyClass.Warrior: return 0;
                case ArmyClass.Hunter: return 1;
                case ArmyClass.Assassin: return 2;
                case ArmyClass.Archer: return 3;
                case ArmyClass.None: return 4;
                default:
                    // Cavalry/Spearman 등 예약 병과 — 조용히 "구역 없음 취급"으로 넘기면 나중에
                    // bossComposition 등에 실수로 들어가도 티가 안 난다(§4-22/§4-28의 다른 config들과
                    // 동일하게 throw, not silently degrade 원칙 — 코드 리뷰 HIGH 지적 반영).
                    throw new ArgumentException($"{armyClass} 병과의 배치 우선순위가 아직 정의되지 않았습니다.");
            }
        }

        /// <summary>
        /// 병과별 배치 가능 열 목록(우선순위 순, 0-indexed). 4열 기준 전용 표를 쓰고, 그 외 열
        /// 수(테스트용 소규모 격자 등)는 기존 절반 분할로 대체한다 — 4병과 표는 4열에서만 의미가
        /// 있다. columns가 1이면 구분할 여지가 없으므로 전부 유일한 열만 쓴다.
        /// </summary>
        private static IReadOnlyList<int> ColumnZoneOf(ArmyClass armyClass, int columns)
        {
            if (columns == 1) return new[] { 0 };

            if (columns == 4)
            {
                switch (armyClass)
                {
                    case ArmyClass.Warrior: return new[] { 0, 1 };
                    case ArmyClass.Hunter: return new[] { 0, 1 };
                    case ArmyClass.Assassin: return new[] { 1, 2 };
                    case ArmyClass.Archer: return new[] { 2, 3 };
                    case ArmyClass.None: return new[] { 0, 1, 2, 3 };
                    default:
                        throw new ArgumentException($"{armyClass} 병과의 배치 구역이 아직 정의되지 않았습니다.");
                }
            }

            switch (armyClass)
            {
                case ArmyClass.Warrior:
                case ArmyClass.Hunter:
                    return Enumerable.Range(0, columns / 2).ToList();
                case ArmyClass.Assassin:
                case ArmyClass.Archer:
                    return Enumerable.Range(columns / 2, columns - columns / 2).ToList();
                case ArmyClass.None:
                    return Enumerable.Range(0, columns).ToList();
                default:
                    throw new ArgumentException($"{armyClass} 병과의 배치 구역이 아직 정의되지 않았습니다.");
            }
        }
    }
}
