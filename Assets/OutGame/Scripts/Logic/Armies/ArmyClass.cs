namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 병과 — 부여된 아이템으로 결정 (상세 기획 §5.5). 1차 병과는 Archer/Shieldman 2종
    /// (§4-25, 2026-07-19 — 기존 Cavalry/안장을 방패/방패병으로 교체). Cavalry/Spearman은 미사용 예약.
    /// </summary>
    public enum ArmyClass
    {
        None = 0,
        Archer = 1,
        Shieldman = 11,

        // ── 예약 (1차 미사용) ──
        Cavalry = 2,
        Spearman = 10,
    }
}
