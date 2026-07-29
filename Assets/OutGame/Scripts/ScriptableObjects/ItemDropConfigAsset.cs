using OutGame.Logic.Items;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>병과별 전투 승리 아이템 드롭 확률 에셋 (§4-28) — 작업자가 인스펙터에서 조정.</summary>
    [CreateAssetMenu(menuName = "OutGame/Item Drop Config", fileName = "ItemDropConfig")]
    public class ItemDropConfigAsset : ScriptableObject
    {
        [SerializeField] private ItemDropConfig config = new ItemDropConfig();

        /// <summary>검증 후 사본을 반환 — 호출자가 변형해도 이 에셋(디자인 타임 데이터)은 영향받지 않는다.</summary>
        public ItemDropConfig ToConfig()
        {
            DefinitionValidation.Validate(name, config.Validate);

            return config.Clone();
        }
    }
}
