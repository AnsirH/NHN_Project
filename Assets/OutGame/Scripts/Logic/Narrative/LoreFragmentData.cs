using System;

namespace OutGame.Logic.Narrative
{
    /// <summary>
    /// "의지의 파편" 한 조각 (§2-2 세계관) — 로딩 오버레이가 아주 낮은 확률로만 노출하는 짧은
    /// 플레이버 텍스트. 이벤트 방과 달리 선택지/보상이 없다 — 순수 텍스트 한 줄로 여운만 남긴다.
    /// </summary>
    [Serializable]
    public class LoreFragmentData
    {
        public string id;
        public string text;
    }
}
