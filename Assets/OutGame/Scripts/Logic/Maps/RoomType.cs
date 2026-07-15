namespace OutGame.Logic.Maps
{
    /// <summary>
    /// 방 타입. Elite/Shop/Treasure는 1차 미사용 예약값 (상세 기획 §4-2).
    /// </summary>
    public enum RoomType
    {
        NormalBattle = 0,
        Event = 1,
        Rest = 2,
        Boss = 3,

        // ── 예약 (1차 미사용) ──
        Elite = 10,
        Shop = 11,
        Treasure = 12,
    }
}
