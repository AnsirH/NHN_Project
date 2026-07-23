using System.Collections.Generic;
using NHN.Simulation.Battle;

namespace NHN.Data
{
    /// <summary>
    /// BattleRequest/EncounterTable → 시뮬 입력(ArmyDefinition) 변환 + 결과(BattleOutcome) 집계.
    /// 분대 인덱스 계약: 플레이어(A군) 분대가 0..N-1 — ArmyDefinition의 스폰 순서(A군 먼저)와 일치한다.
    /// 뷰 표현용 에셋 목록(SquadAssets)을 함께 출력해 뷰가 같은 순서로 색/스케일을 배정할 수 있게 한다.
    /// </summary>
    public static class BattleRequestBuilder
    {
        /// <summary>뷰 표현용 분대 에셋 정보 — ArmyDefinition과 같은 분대 순서.</summary>
        public readonly struct SquadAssets
        {
            public readonly RoleData Role;
            public readonly GeneralData General;
            public readonly int Count;

            public SquadAssets(RoleData role, GeneralData general, int count)
            {
                Role = role;
                General = general;
                Count = count;
            }
        }

        public static ArmyDefinition BuildPlayerArmy(
            BattleRequest request, BattleCatalog catalog,
            List<SquadAssets> viewSquadsOut, List<string> squadIdsOut)
        {
            var squads = new SquadDefinition[request.playerSquads.Count];
            for (int s = 0; s < request.playerSquads.Count; s++)
            {
                SquadRequest squadRequest = request.playerSquads[s];
                RoleData role = catalog.ResolveRole(squadRequest.roleId);
                GeneralData general = catalog.ResolveGeneral(squadRequest.generalId);
                squads[s] = new SquadDefinition(
                    role.ToDefinition(),
                    squadRequest.soldierCount,
                    catalog.SlotToAnchor(squadRequest.slotX, squadRequest.slotY),
                    general != null ? general.ToDefinition() : null);
                viewSquadsOut?.Add(new SquadAssets(role, general, squadRequest.soldierCount));
                squadIdsOut?.Add(squadRequest.squadId);
            }
            return new ArmyDefinition(squads);
        }

        public static ArmyDefinition BuildEnemyArmy(
            string encounterId, EncounterTable table, BattleCatalog catalog,
            List<SquadAssets> viewSquadsOut)
        {
            EncounterTable.Encounter encounter = table.GetEncounterOrFallback(encounterId);
            var squads = new SquadDefinition[encounter.squads.Length];
            for (int s = 0; s < encounter.squads.Length; s++)
            {
                EncounterTable.EncounterSquad encounterSquad = encounter.squads[s];
                RoleData role = encounterSquad.role != null ? encounterSquad.role : catalog.NormalRole;
                squads[s] = new SquadDefinition(
                    role.ToDefinition(),
                    encounterSquad.count,
                    catalog.SlotToAnchor(encounterSquad.slotX, encounterSquad.slotY),
                    encounterSquad.general != null ? encounterSquad.general.ToDefinition() : null);
                viewSquadsOut?.Add(new SquadAssets(role, encounterSquad.general, encounterSquad.count));
            }
            return new ArmyDefinition(squads);
        }

        /// <summary>종료된 시뮬에서 아웃게임 계약 형태의 결과를 집계한다 (victory + 분대별 생존 병사 수).</summary>
        public static BattleOutcome BuildOutcome(BattleSimulation sim, IReadOnlyList<string> playerSquadIds)
        {
            var outcome = new BattleOutcome
            {
                // 시간 상한 무승부는 패배 처리 — 아웃게임의 "victory=false → 런 종료" 규칙과 합치
                Victory = sim.Result.Winner == 0,
            };
            for (int s = 0; s < playerSquadIds.Count; s++)
            {
                outcome.Survivals.Add(new SquadSurvival(playerSquadIds[s], sim.CountSquadSurvivors(s)));
            }
            return outcome;
        }
    }
}
