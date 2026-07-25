namespace OutGame.Logic.Augments
{
    /// <summary>증강 종류 (§5.6, §4-27). ItemAugment는 특정 병과 군대에만, StatAugment는 보유한 모든 군대에 적용.</summary>
    public enum AugmentCategory
    {
        ItemAugment = 0,
        StatAugment = 1,
    }
}
