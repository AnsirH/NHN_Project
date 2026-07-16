using System.Numerics;

namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 분대 정의. Anchor는 군대 로컬 좌표: x = 전선으로부터의 깊이(+뒤), y = 측면 오프셋.
    /// B군은 시뮬레이션이 x축을 미러링한다.
    /// </summary>
    public readonly struct SquadDefinition
    {
        public readonly RoleDefinition Role;
        public readonly int Count;
        public readonly Vector2 Anchor;

        public SquadDefinition(RoleDefinition role, int count, Vector2 anchor)
        {
            Role = role;
            Count = count;
            Anchor = anchor;
        }
    }

    /// <summary>
    /// 전투 입력이 되는 군대 정의. 유닛 인덱스는 (A군 분대 순서 → B군 분대 순서)로 배정된다 —
    /// 뷰가 같은 순서로 순회해 롤별 표현을 배정할 수 있다.
    /// </summary>
    public sealed class ArmyDefinition
    {
        public readonly SquadDefinition[] Squads;

        public ArmyDefinition(SquadDefinition[] squads)
        {
            Squads = squads ?? System.Array.Empty<SquadDefinition>();
        }

        public int TotalUnits
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Squads.Length; i++)
                {
                    total += Squads[i].Count;
                }
                return total;
            }
        }
    }
}
