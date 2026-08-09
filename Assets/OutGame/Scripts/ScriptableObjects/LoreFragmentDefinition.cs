using System;
using OutGame.Logic.Narrative;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// "의지의 파편" 로딩 플레이버 텍스트 에셋 (Docs/OutGame/로딩 화면 - 의지의 파편 설계.md) —
    /// EventDefinition과 동일 패턴이지만 선택지/보상이 없어 fragmentId + text 두 필드뿐이다.
    /// 다른 Definition들과 동일하게 Resources.LoadAll로 무등록 자동 수집한다.
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
