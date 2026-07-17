namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 병과 — 부여된 아이템으로 결정 (상세 기획 §5.5). Spearman/Shieldman은 예약값.
    /// </summary>
    public enum ArmyClass
    {
        None = 0,
        Archer = 1,
        Cavalry = 2,

        // ── 예약 (1차 미사용) ──
        Spearman = 10,
        Shieldman = 11,
    }
}
