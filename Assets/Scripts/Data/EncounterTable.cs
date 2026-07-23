using System;
using UnityEngine;

namespace NHN.Data
{
    /// <summary>
    /// encounterId → 적 군대 구성 (계약상 적 구성 정의는 인게임 책임 — outGame BattleSetupData.encounterId의 해석처).
    /// 적 구성 추가 = 이 에셋에 엔트리 1개 (코드 0줄). 슬롯 좌표는 아군과 같은 정규화 의미 (시뮬이 B군을 미러링).
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterTable", menuName = "NHN/EncounterTable")]
    public sealed class EncounterTable : ScriptableObject
    {
        [Serializable]
        public struct EncounterSquad
        {
            public RoleData role;
            [Tooltip("비우면 장군 없는 분대")]
            public GeneralData general;
            public int count;
            [Tooltip("정규화 0~1, 1 = 전선 쪽")]
            public float slotX;
            [Tooltip("정규화 0~1, 0.5 = 측면 중앙")]
            public float slotY;
        }

        [Serializable]
        public struct Encounter
        {
            public string encounterId;
            public EncounterSquad[] squads;
        }

        [SerializeField] private Encounter[] encounters;
        [Tooltip("미등록 encounterId 폴백 — 연동 초기 키 불일치로 전투가 죽지 않게")]
        [SerializeField] private string fallbackEncounterId;

        public Encounter GetEncounterOrFallback(string encounterId)
        {
            if (TryGetEncounter(encounterId, out Encounter encounter))
            {
                return encounter;
            }
            Debug.LogWarning($"[EncounterTable] 미등록 encounterId '{encounterId}' — 폴백 '{fallbackEncounterId}' 사용");
            if (TryGetEncounter(fallbackEncounterId, out encounter))
            {
                return encounter;
            }
            throw new InvalidOperationException(
                $"EncounterTable에 encounterId '{encounterId}'도 폴백 '{fallbackEncounterId}'도 없습니다 — 테이블 구성 확인");
        }

        public bool TryGetEncounter(string encounterId, out Encounter encounter)
        {
            if (!string.IsNullOrEmpty(encounterId))
            {
                for (int i = 0; i < encounters.Length; i++)
                {
                    if (encounters[i].encounterId == encounterId)
                    {
                        encounter = encounters[i];
                        return true;
                    }
                }
            }
            encounter = default;
            return false;
        }
    }
}
