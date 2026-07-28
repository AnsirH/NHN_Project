namespace OutGame.Flow
{
    /// <summary>씬 이름 상수 (§3.1) — SceneManager.LoadScene 호출 시 오타 방지용.</summary>
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string OutGame = "OutGame";
        /// <summary>인게임 전투 씬 (§7.4 — 배치 확정 시 전환, 인게임 개발자 담당).</summary>
        public const string Battle = "Battle";
    }
}
