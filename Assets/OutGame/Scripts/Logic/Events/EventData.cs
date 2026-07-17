using System;
using System.Collections.Generic;

namespace OutGame.Logic.Events
{
    /// <summary>이벤트 방 콘텐츠 1개 (§5.4). 일러스트는 SO(EventDefinition)에서만 참조 — 순수 데이터엔 없음.</summary>
    [Serializable]
    public class EventData
    {
        public string id;
        public string bodyText;
        public List<EventChoiceData> choices = new List<EventChoiceData>();
    }
}
