namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 병과 — 부여된 아이템으로 결정 (상세 기획 §5.5). 4병과 체제(2026-07-26 확정): 전사(Warrior,
    /// 검+방패 — enum/식별자는 그대로, 표시명만 "방패병"에서 "전사"로 개칭)·궁수(Archer, 활)·
    /// 사냥꾼(Hunter, 도끼)·암살자(Assassin, 단검). Cavalry/Spearman은 미사용 예약.
    /// </summary>
    public enum ArmyClass
    {
        None = 0,
        Archer = 1,
        Warrior = 11,
        Hunter = 12,
        Assassin = 13,

        // ── 예약 (1차 미사용) ──
        Cavalry = 2,
        Spearman = 10,
    }
}
