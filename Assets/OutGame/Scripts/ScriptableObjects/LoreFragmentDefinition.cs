using System;
using OutGame.Logic.Narrative;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// "의지의 파편" 플레이버 텍스트 에셋 (§2-2 세계관) — 로딩 오버레이가 낮은 확률로 노출한다.
    /// EventDefinition과 달리 선택지/보상이 없다 — 텍스트 한 줄뿐.
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Lore Fragment Definition", fileName = "LoreFragmentDefinition")]
    public class LoreFragmentDefinition : ScriptableObject
    {
        [SerializeField] private string fragmentId;
        [TextArea(2, 5)] [SerializeField] private string text;

        public LoreFragmentData ToData()
        {
            if (string.IsNullOrWhiteSpace(fragmentId))
                throw new InvalidOperationException($"{name}: fragmentId가 비어 있습니다.");
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException($"{name}: text가 비어 있습니다.");

            return new LoreFragmentData { id = fragmentId, text = text };
        }
    }
}
