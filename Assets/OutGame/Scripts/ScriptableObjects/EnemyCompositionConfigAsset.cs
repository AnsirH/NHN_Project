using System;
using OutGame.Logic.Battle;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 적 군대 구성 밸런스 에셋 (§4-28) — 작업자가 인스펙터에서 조정. RoomEncounterTable이
    /// 인게임과 협의되기 전까지의 임시 대체(아웃게임 내부 전용, §7 인터페이스에는 노출 안 함).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Enemy Composition Config", fileName = "EnemyCompositionConfig")]
    public class EnemyCompositionConfigAsset : ScriptableObject
    {
        [SerializeField] private EnemyCompositionConfig config = new EnemyCompositionConfig();

        /// <summary>검증 후 사본을 반환 — 호출자가 변형해도 이 에셋(디자인 타임 데이터)은 영향받지 않는다.</summary>
        public EnemyCompositionConfig ToConfig()
        {
            try
            {
                config.Validate();
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"{name}: 설정값이 유효하지 않습니다 — {e.Message}", e);
            }

            return config.Clone();
        }
    }
}
