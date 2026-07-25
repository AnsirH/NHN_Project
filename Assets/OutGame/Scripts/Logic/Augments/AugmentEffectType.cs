namespace OutGame.Logic.Augments
{
    /// <summary>
    /// 증강 효과 종류 (§5.6, §4-27). StatBoost는 아웃게임이 수치를 직접 계산·적용한다.
    /// GeneralSkillUpgrade는 실제 효과 구현이 인게임 스킬 시스템 책임(§4-23과 동일 원칙) — 아웃게임은
    /// 선택 사실만 RunState.selectedAugmentIds에 기록한다.
    /// </summary>
    public enum AugmentEffectType
    {
        StatBoost = 0,
        GeneralSkillUpgrade = 1,
    }
}
