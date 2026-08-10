using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI
{
    /// <summary>
    /// 범례 항목 1줄 (색상 견본 + 이름) — 프리팹으로 제작, 코드는 데이터 주입만.
    /// </summary>
    public class LegendEntry : MonoBehaviour
    {
        [SerializeField] private Image swatch;
        // 2026-08-10: UnityEngine.UI.Text → TMP_Text. 노드 라벨(RoomNodeView)과 같은 폰트를 쓰도록
        // 통일 — 둘 다 RoomTypeVisualSet에서 같은 문구를 받는데 폰트만 달라 보이던 문제.
        [SerializeField] private TMP_Text label;

        public void Setup(RoomTypeVisualSet.Entry entry, string displayName)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (swatch == null || label == null)
                throw new InvalidOperationException("LegendEntry 프리팹의 swatch/label이 배선되지 않았습니다");

            swatch.color = entry.color;
            if (entry.icon != null)
                swatch.sprite = entry.icon;
            label.text = displayName;
        }
    }
}
