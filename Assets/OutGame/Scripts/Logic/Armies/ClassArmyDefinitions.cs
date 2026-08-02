namespace OutGame.Logic.Armies
{
    /// <summary>
    /// 병과 → ArmyDefinition id 매핑 (2026-08-02) — 병과마다 스탯·초상화·장군 이름을 authored로 담은
    /// ArmyDefinition 1개가 대응한다(비율 보정 없음). 플레이어(DeploymentState)·적
    /// (EnemyCompositionGenerator)·에디터 미리보기(EnemyPresetEditorWindow)가 전부 이 매핑 하나를
    /// 공유해야 한다 — 각자 판정하면 병과 정의가 갈라지는 버그가 난다(이 프로젝트 반복 원칙).
    /// </summary>
    public static class ClassArmyDefinitions
    {
        public static string DefIdFor(ArmyClass armyClass) => armyClass switch
        {
            ArmyClass.Warrior => "army_warrior",
            ArmyClass.Hunter => "army_hunter",
            ArmyClass.Assassin => "army_assassin",
            ArmyClass.Archer => "army_archer",
            _ => "army_none",
        };
    }
}
