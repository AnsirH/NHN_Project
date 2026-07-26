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

        /// <summary>좌/우 공용 — SquadRequest 목록으로 군대를 만든다 (BalanceLab 교차 검증도 이 경로 사용).</summary>
        public static ArmyDefinition BuildSquads(
            IReadOnlyList<SquadRequest> squadRequests, BattleCatalog catalog, in BattleConfig config,
            List<SquadAssets> viewSquadsOut, List<string> squadIdsOut)
        {
            var squads = new SquadDefinition[squadRequests.Count];
            for (int s = 0; s < squadRequests.Count; s++)
            {
                SquadRequest squadRequest = squadRequests[s];
                RoleData role = catalog.ResolveRole(squadRequest.roleId);
                GeneralData general = catalog.ResolveGeneral(squadRequest.generalId);
                squads[s] = new SquadDefinition(
                    BuildSoldierRole(role, squadRequest),
                    squadRequest.soldierCount,
                    DeploymentGrid.SlotToAnchor(
                        squadRequest.slotX, squadRequest.slotY, config.DeploymentDepth, config.DeploymentHalfWidth),
                    BuildGeneral(general, squadRequest, config));
                viewSquadsOut?.Add(new SquadAssets(role, general, squadRequest.soldierCount));
                squadIdsOut?.Add(squadRequest.squadId);
            }
            return new ArmyDefinition(squads);
        }

        /// <summary>
        /// 병사 정의: 아웃게임이 스탯을 보냈으면 인게임 속성(.asset)과 결합하고, 아니면 .asset 값을 그대로 쓴다.
        /// 전달 여부와 무관하게 공격 주기·사거리·타겟팅·이동 패턴은 언제나 인게임 소유다.
        /// </summary>
        private static RoleDefinition BuildSoldierRole(RoleData role, SquadRequest request)
        {
            RoleDefinition baseRole = role.ToDefinition();
            if (!request.HasSoldierStats)
            {
                return baseRole;
            }
            return RoleDefinition.WithStats(
                baseRole,
                request.maxHp, request.attackDamage, request.defense,
                request.critChancePercent, request.moveSpeed);
        }

        /// <summary>
        /// 장군 정의: 능력(패시브·충전·액티브)은 에셋에서, 스탯은 전달값이 있으면 그것으로.
        /// 같은 장군 에셋이 여러 분대에 쓰여도 분대마다 레벨이 다를 수 있어 인스턴스를 분리한다.
        /// </summary>
        private static GeneralDefinition BuildGeneral(GeneralData general, SquadRequest request, in BattleConfig config)
        {
            if (general == null)
            {
                return null;
            }
            GeneralDefinition definition = general.ToDefinition();
            if (request.HasGeneralStats)
            {
                // 치명타·이동속도는 장군 값을 병사와 공유하므로 같은 필드를 쓴다 (아웃게임 §5.7).
                RoleDefinition combatRole = RoleDefinition.WithStats(
                    definition.CombatRole,
                    request.generalMaxHp, request.generalAttackDamage, request.generalDefense,
                    request.critChancePercent, request.moveSpeed);
                definition = GeneralDefinition.WithCombatRole(definition, combatRole);
            }
            // 스킬 강화 = 충전 필요량 감소 (발동 빈도 증가). 강화 0회면 원본이 그대로 돌아온다.
            return GeneralDefinition.WithSkillUpgrades(
                definition, request.generalSkillUpgradeCount,
                config.SkillUpgradeChargeReduction, config.MinChargeRequiredRatio);
        }

        public static ArmyDefinition BuildPlayerArmy(
            BattleRequest request, BattleCatalog catalog, in BattleConfig config,
            List<SquadAssets> viewSquadsOut, List<string> squadIdsOut)
        {
            return BuildSquads(request.playerSquads, catalog, config, viewSquadsOut, squadIdsOut);
        }

        public static ArmyDefinition BuildEnemyArmy(
            string encounterId, EncounterTable table, BattleCatalog catalog, in BattleConfig config,
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
                    DeploymentGrid.SlotToAnchor(
                        encounterSquad.slotX, encounterSquad.slotY, config.DeploymentDepth, config.DeploymentHalfWidth),
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
