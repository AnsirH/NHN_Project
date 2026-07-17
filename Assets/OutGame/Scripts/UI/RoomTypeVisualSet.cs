using System;
using System.Collections.Generic;
using OutGame.Logic.Maps;
using UnityEngine;

namespace OutGame.UI
{
    /// <summary>
    /// 방 타입별 표시 정보 (색/이름/아이콘). 노드 뷰와 범례가 공용으로 사용한다.
    /// 아트 확정 전에는 색상 사각형이 placeholder.
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Room Type Visual Set", fileName = "RoomTypeVisuals")]
    public class RoomTypeVisualSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public RoomType roomType;
            public string displayName;
            public Color color = Color.white;
            public Sprite icon;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Get(RoomType type) => entries.Find(e => e.roomType == type);

        public Color GetColor(RoomType type) => Get(type)?.color ?? Color.magenta;

        public string GetName(RoomType type)
        {
            Entry entry = Get(type);
            return entry != null && !string.IsNullOrEmpty(entry.displayName)
                ? entry.displayName
                : type.ToString();
        }

#if UNITY_EDITOR
        /// <summary>에디터 자동화 스크립트 전용 — 런타임 호출 금지.</summary>
        public void SetEntriesForEditor(List<Entry> value) => entries = value;
#endif
    }
}
