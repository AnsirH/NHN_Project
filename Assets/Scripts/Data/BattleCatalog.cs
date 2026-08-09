using System;
using NHN.Simulation.Battle;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>
    /// 문자열 키 → 에셋 해석 카탈로그 (키 = 에셋 이름) + 정규화 슬롯 → 전장 anchor 변환.
    /// 아웃게임 연동의 인게임 쪽 어휘 사전 — 키 협의가 바뀌어도 이 에셋 목록만 갱신하면 된다 (코드 0줄).
    /// 미등록 키는 노멀 병사로 관대하게 폴백한다 — 연동 초기 키 불일치로 전투가 죽지 않게 (경고 로그로 표면화).
    /// </summary>
    [CreateAssetMenu(fileName = "BattleCatalog", menuName = "NHN/BattleCatalog")]
    public sealed class BattleCatalog : ScriptableObject
    {
        /// <summary>
        /// 아웃게임 계약 skillId → 인게임 스킬 에셋. 롤·장군과 달리 명시적 쌍으로 두는 이유:
        /// 계약 id가 가칭(skill_char_1~4)이라 에셋 이름과 일치하지 않는다 — 캐릭터·스킬 이름이
        /// 확정되면 이 목록만 갱신하면 된다 (코드 0줄).
        /// </summary>
        [Serializable]
        public struct PlayerSkillEntry
        {
            public string skillId;
            public SkillData skill;
        }

        /// <summary>generalId 계약(roleId + "General")의 접미사 — MapClassToGeneralId와 동일 상수.</summary>
        private const string GeneralSuffix = "General";

        [Header("키 → 에셋 (키 = 에셋 이름)")]
        [Tooltip("병사·장군 데이터가 통합된 분대 에셋 — SquadData 1개 = 롤 1종 (2026-08-10 통합)")]
        [SerializeField] private SquadData[] squads;
        [Tooltip("roleId가 비었거나 미등록일 때 사용 — 기획 §5 노멀 병사")]
        [SerializeField] private SquadData normalSquad;

        [Header("플레이어 캐릭터 스킬 (§5.2.5 — 계약 playerCharacterSkillId → 스킬 에셋)")]
        [SerializeField] private PlayerSkillEntry[] playerSkillMap;

        public SquadData NormalRole => normalSquad;

        public SquadData ResolveRole(string roleId)
        {
            if (string.IsNullOrEmpty(roleId))
            {
                return normalSquad;
            }
            for (int i = 0; i < squads.Length; i++)
            {
                if (squads[i] != null && squads[i].name == roleId)
                {
                    return squads[i];
                }
            }
            Debug.LogWarning($"[BattleCatalog] 미등록 roleId '{roleId}' — 노멀 병사로 폴백 (키 협의/카탈로그 등록 필요)");
            return normalSquad;
        }

        /// <summary>
        /// generalId는 계약상 언제나 "roleId + General"(MapClassToGeneralId) 형태다 — 병사·장군이
        /// 하나의 SquadData로 통합된 뒤에도(2026-08-10) 이 문자열 계약은 그대로 두고, 여기서 접미사를
        /// 벗겨 같은 배열에서 찾는다.
        /// </summary>
        public SquadData ResolveGeneral(string generalId)
        {
            if (string.IsNullOrEmpty(generalId))
            {
                return null;
            }
            string roleId = generalId.EndsWith(GeneralSuffix, StringComparison.Ordinal)
                ? generalId.Substring(0, generalId.Length - GeneralSuffix.Length)
                : generalId;
            for (int i = 0; i < squads.Length; i++)
            {
                if (squads[i] != null && squads[i].name == roleId)
                {
                    return squads[i];
                }
            }
            Debug.LogWarning($"[BattleCatalog] 미등록 generalId '{generalId}' — 장군 없음으로 폴백");
            return null;
        }

        /// <summary>계약 skillId → 스킬 에셋. 비었거나 미등록이면 null — 호출자가 전체 스킬 폴백을 결정한다.</summary>
        public SkillData ResolveSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || playerSkillMap == null)
            {
                return null;
            }
            for (int i = 0; i < playerSkillMap.Length; i++)
            {
                if (playerSkillMap[i].skill != null && playerSkillMap[i].skillId == skillId)
                {
                    return playerSkillMap[i].skill;
                }
            }
            Debug.LogWarning($"[BattleCatalog] 미등록 skillId '{skillId}' — 전체 스킬 폴백 (키 협의/카탈로그 등록 필요)");
            return null;
        }

        // 슬롯→anchor 변환은 Simulation.DeploymentGrid로 이동 (CLI와 공유, 파라미터는 BattleConfig — 작업 1).
    }
}
