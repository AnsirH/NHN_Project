using System;
using System.Collections.Generic;

namespace OutGame.Logic.Events
{
    /// <summary>이벤트 선택지 1개 (§5.4): 선택지 문구, 보상 목록, 결과 연출 텍스트.</summary>
    [Serializable]
    public class EventChoiceData
    {
        public string choiceText;
        public List<RewardGrant> rewards = new List<RewardGrant>();
        public string resultText;
    }
}
