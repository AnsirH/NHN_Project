using System;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 캐릭터 선택 화면(§5.2.5) 하단 초상화 아이콘 1개 — 선택된 캐릭터만 하이라이트, 나머지는
    /// 회색 틴트로 흑백에 가깝게 표시한다(실제 그레이스케일 셰이더 대신 최소 구현).
    /// </summary>
    public class CharacterIconView : MonoBehaviour
    {
        // #6B6F76 — SceneSetupM7UI의 ColorIconDimmed 토큰과 동일(§5.2.5 디자인 토큰, 2026-07-28).
        private static readonly Color UnselectedTint = new Color(0.420f, 0.435f, 0.463f, 1f);

        [SerializeField] private Image portrait;
        [SerializeField] private GameObject highlightFrame;
        [SerializeField] private Button button;

        public void Bind(PlayerCharacterDefinition characterDef, Action onSelected)
        {
            if (characterDef == null) throw new ArgumentNullException(nameof(characterDef));
            if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));
            if (portrait == null || highlightFrame == null || button == null)
                throw new InvalidOperationException("CharacterIconView 프리팹의 필드가 배선되지 않았습니다.");

            portrait.sprite = characterDef.Icon;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected());
        }

        public void SetSelected(bool selected)
        {
            portrait.color = selected ? Color.white : UnselectedTint;
            highlightFrame.SetActive(selected);
        }
    }
}
