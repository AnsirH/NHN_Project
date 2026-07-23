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
        [Header("키 → 에셋 (키 = 에셋 이름)")]
        [SerializeField] private RoleData[] roles;
        [SerializeField] private GeneralData[] generals;
        [Tooltip("roleId가 비었거나 미등록일 때 사용 — 기획 §5 노멀 병사")]
        [SerializeField] private RoleData normalRole;

        [Header("정규화 슬롯(0~1) → anchor 변환 (계약: 월드 좌표 변환은 인게임 책임)")]
        [Tooltip("slotX 0(후방)~1(전선)이 펼쳐지는 깊이 범위")]
        [SerializeField] private float deploymentDepth = 10f;
        [Tooltip("slotY 0~1이 펼쳐지는 측면 절반 폭 (중앙 기준 ±)")]
        [SerializeField] private float deploymentHalfWidth = 14f;

        public RoleData NormalRole => normalRole;

        public RoleData ResolveRole(string roleId)
        {
            if (string.IsNullOrEmpty(roleId))
            {
                return normalRole;
            }
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i] != null && roles[i].name == roleId)
                {
                    return roles[i];
                }
            }
            Debug.LogWarning($"[BattleCatalog] 미등록 roleId '{roleId}' — 노멀 병사로 폴백 (키 협의/카탈로그 등록 필요)");
            return normalRole;
        }

        public GeneralData ResolveGeneral(string generalId)
        {
            if (string.IsNullOrEmpty(generalId))
            {
                return null;
            }
            for (int i = 0; i < generals.Length; i++)
            {
                if (generals[i] != null && generals[i].name == generalId)
                {
                    return generals[i];
                }
            }
            Debug.LogWarning($"[BattleCatalog] 미등록 generalId '{generalId}' — 장군 없음으로 폴백");
            return null;
        }

        /// <summary>정규화 슬롯 → SquadDefinition.Anchor (x = 전선으로부터 깊이, y = 측면 오프셋).</summary>
        public System.Numerics.Vector2 SlotToAnchor(float slotX, float slotY)
        {
            return new System.Numerics.Vector2(
                (1f - Mathf.Clamp01(slotX)) * deploymentDepth,
                (Mathf.Clamp01(slotY) - 0.5f) * 2f * deploymentHalfWidth);
        }
    }
}
