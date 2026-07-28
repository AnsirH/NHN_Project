using System.Numerics;

namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 분대 정의. Anchor는 군대 로컬 좌표: x = 전선으로부터의 깊이(+뒤), y = 측면 오프셋.
    /// B군은 시뮬레이션이 x축을 미러링한다.
    /// v4: 리더(장군) 필드 확장 — General이 null이면 노멀/무장군 분대 (패시브·액티브 없음).
    /// 장군의 스폰 위치는 분대 선두 (기획 §5).
    /// </summary>
    public readonly struct SquadDefinition
    {
        public readonly RoleDefinition Role;
        public readonly int Count;
        public readonly Vector2 Anchor;
        /// <summary>분대 리더(장군). null = 장군 없는 분대.</summary>
        public readonly GeneralDefinition General;

        public SquadDefinition(RoleDefinition role, int count, Vector2 anchor)
            : this(role, count, anchor, null)
        {
        }

        public SquadDefinition(RoleDefinition role, int count, Vector2 anchor, GeneralDefinition general)
        {
            Role = role;
            Count = count;
            Anchor = anchor;
            General = general;
        }
    }

    /// <summary>
    /// 전투 입력이 되는 군대 정의. 유닛 인덱스는 (A군 분대 순서 → B군 분대 순서)로 배정된다 —
    /// 뷰가 같은 순서로 순회해 롤별 표현을 배정할 수 있다.
    /// 분대 내에서는 병사(Count명) 다음에 장군 1명이 온다.
    /// </summary>
    public sealed class ArmyDefinition
    {
        public readonly SquadDefinition[] Squads;

        public ArmyDefinition(SquadDefinition[] squads)
        {
            Squads = squads ?? System.Array.Empty<SquadDefinition>();
        }

        /// <summary>병사 + 장군을 포함한 총 유닛 수.</summary>
        public int TotalUnits
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Squads.Length; i++)
                {
                    total += Squads[i].Count;
                    if (Squads[i].General != null)
                    {
                        total++;
                    }
                }
                return total;
            }
        }
    }
}
