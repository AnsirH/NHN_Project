using System;
using OutGame.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>맵 선택 목록 항목 1개 (배경 일러스트 + 이름) — 프리팹 인스턴스화 (§5.2).
    ///
    /// 2026-08-09: 해금 여부를 두 가지로 표시한다(사용자 확정) —
    /// (1) 배경 Image 색: 해금 <see cref="unlockedColor"/>(흰색, 일러스트 원색) / 잠금
    ///     <see cref="lockedColor"/>(회색으로 죽임). Button이 ColorTint라 disabledColor가 이 색에
    ///     곱해지므로 프리팹의 disabledColor는 흰색으로 중립화해 뒀다 — 안 그러면 두 번 어두워진다.
    /// (2) 하단 라벨: 해금이면 <see cref="textPanel"/>(맵 이름), 잠금이면 <see cref="noDataPanel"/>
    ///     ("준비중")을 켠다. 잠금이라고 nameText를 덮어쓰지 않는다 — 문구가 프리팹 쪽에 남아 있어야
    ///     디자이너가 에디터에서 그대로 보고 편집할 수 있다.</summary>
    public class MapEntryView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Button button;

        [Header("하단 라벨")]
        [SerializeField] private GameObject textPanel;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject noDataPanel;

        [Header("해금 상태 색")]
        [SerializeField] private Color unlockedColor = Color.white;
        [SerializeField] private Color lockedColor = new Color(0.443f, 0.443f, 0.443f, 1f);

        public void Bind(MapDefinition mapDef, Action onSelected)
        {
            if (mapDef == null) throw new ArgumentNullException(nameof(mapDef));
            if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));
            RequireWiring();

            background.color = unlockedColor;
            textPanel.SetActive(true);
            noDataPanel.SetActive(false);
            nameText.text = mapDef.DisplayName;

            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected());
        }

        /// <summary>지도 위 지점에 배치됐지만 아직 연결된 MapDefinition이 없는 경우(§5.2 3지점 배치,
        /// 1개만 활성) — 클릭 불가 상태로 "준비중" 패널만 띄운다.</summary>
        public void ShowLocked()
        {
            RequireWiring();

            background.color = lockedColor;
            textPanel.SetActive(false);
            noDataPanel.SetActive(true);

            button.interactable = false;
            button.onClick.RemoveAllListeners();
        }

        private void RequireWiring()
        {
            if (background == null || button == null || textPanel == null || nameText == null || noDataPanel == null)
                throw new InvalidOperationException("MapEntryView 프리팹의 필드가 배선되지 않았습니다.");
        }
    }
}
