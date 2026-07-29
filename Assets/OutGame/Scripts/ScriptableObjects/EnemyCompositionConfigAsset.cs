using OutGame.Logic.Battle;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 적 군대 구성 밸런스 에셋 (§4-28) — 작업자가 인스펙터에서 조정. §9 RoomEncounterTable의
    /// 아웃게임 쪽 실제 구현체이며, 여기서 생성된 구성이 그대로 §7 계약(BattleSetupData.enemies)에
    /// 실려 인게임에 전달된다(2026-07-29).
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Enemy Composition Config", fileName = "EnemyCompositionConfig")]
    public class EnemyCompositionConfigAsset : ScriptableObject
    {
        [SerializeField] private EnemyCompositionConfig config = new EnemyCompositionConfig();

        /// <summary>검증 후 사본을 반환 — 호출자가 변형해도 이 에셋(디자인 타임 데이터)은 영향받지 않는다.</summary>
        public EnemyCompositionConfig ToConfig()
        {
            DefinitionValidation.Validate(name, config.Validate);

            return config.Clone();
        }
    }
}
