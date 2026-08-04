namespace OutGame.Flow
{
    /// <summary>
    /// 씬 이름 상수 (§3.1) — SceneManager.LoadScene 호출 시 오타 방지용.
    /// OutGame.Logic 어셈블리 소속(2026-07-29 이동)이라 인게임 쪽(NHN.Integration, OutGame.Logic을
    /// 이미 참조 중)도 그대로 참조할 수 있다 — 씬 이름을 양쪽에서 각자 문자열로 하드코딩하다 어긋나는
    /// 것을 막기 위함(§7.4 핸드오프 복귀 대상 씬 이름 등).
    /// </summary>
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string OutGame = "OutGame";
        /// <summary>인게임 전투 씬 (§7.4 — 배치 확정 시 전환, 인게임 개발자 담당).</summary>
        public const string Battle = "Battle";
        /// <summary>인게임 진입 로딩 씬 (2026-08-04) — OutGame → Battle 사이에 거치는 중계 씬.</summary>
        public const string Loading = "Loading";
    }
}
