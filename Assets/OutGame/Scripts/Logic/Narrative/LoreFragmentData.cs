using System;

namespace OutGame.Logic.Narrative
{
    /// <summary>"의지의 파편" 로딩 플레이버 텍스트 1개 — EventData와 동일한 패턴의 순수 데이터
    /// (Docs/OutGame/로딩 화면 - 의지의 파편 설계.md).</summary>
    [Serializable]
    public class LoreFragmentData
    {
        public string id;
        public string text;
    }
}
