using System;
using OutGame.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>맵 선택 목록 항목 1개 (썸네일 + 이름 + 난이도) — 프리팹 인스턴스화 (§5.2).</summary>
    public class MapEntryView : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private Text nameText;
        [SerializeField] private Text difficultyText;
        [SerializeField] private Button button;

        public void Bind(MapDefinition mapDef, Action onSelected)
        {
            if (mapDef == null) throw new ArgumentNullException(nameof(mapDef));
            if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));
            if (thumbnail == null || nameText == null || difficultyText == null || button == null)
                throw new InvalidOperationException("MapEntryView 프리팹의 필드가 배선되지 않았습니다.");

            thumbnail.enabled = mapDef.Thumbnail != null;
            thumbnail.sprite = mapDef.Thumbnail;
            nameText.text = mapDef.DisplayName;
            difficultyText.text = mapDef.DifficultyLabel;

            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected());
        }

        /// <summary>지도 위 지점에 배치됐지만 아직 연결된 MapDefinition이 없는 경우(§5.2 3지점 배치,
        /// 1개만 활성) — 클릭 불가 상태로 "준비 중"만 표시한다.</summary>
        public void ShowLocked(string label = "준비 중")
        {
            if (thumbnail == null || nameText == null || difficultyText == null || button == null)
                throw new InvalidOperationException("MapEntryView 프리팹의 필드가 배선되지 않았습니다.");

            thumbnail.enabled = false;
            nameText.text = label;
            difficultyText.text = string.Empty;

            button.interactable = false;
            button.onClick.RemoveAllListeners();
        }
    }
}
