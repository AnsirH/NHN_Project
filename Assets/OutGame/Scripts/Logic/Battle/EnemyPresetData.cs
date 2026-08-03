using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 프리셋 조각 안의 유닛 1개 (2026-08-02) — 최종 스탯을 직접 담지 않는다. 아군
    /// 배치(DeploymentState.BuildDeployedArmies)와 같은 입력값(템플릿+병과+업그레이드+증강)을
    /// 담아두고, 최종 스탯은 EnemyCompositionGenerator가 생성 시점에 ArmyStatCalculator로
    /// 계산한다 — 그래야 프리셋을 만들 때(에디터)와 실제 게임에서 같은 공식을 쓰는 게 보장된다.
    /// </summary>
    [Serializable]
    public class EnemyPresetUnit
    {
        public ArmyClass armyClass;
        public int soldierCount = 30;
        public int upgradeLevel;
        public List<string> selectedAugmentIds = new List<string>();

        public EnemyPresetUnit Clone() => new EnemyPresetUnit
        {
            armyClass = armyClass,
            soldierCount = soldierCount,
            upgradeLevel = upgradeLevel,
            selectedAugmentIds = new List<string>(selectedAugmentIds),
        };

        public void Validate(string context)
        {
            if (soldierCount < 1)
                throw new ArgumentException($"{context}: soldierCount는 1 이상이어야 합니다. 현재: {soldierCount}");
            if (upgradeLevel < 0)
                throw new ArgumentException($"{context}: upgradeLevel은 0 이상이어야 합니다. 현재: {upgradeLevel}");
        }
    }

    /// <summary>
    /// 개발자가 커스텀 에디터(EnemyPresetEditorWindow)로 직접 만드는 적 구성 조각 (2026-08-02) —
    /// 유닛 1개짜리일 수도, 여러 유닛 묶음일 수도 있다. 완성된 적 부대 1개가 될 수도, 여러 프리셋이
    /// 합쳐져 하나의 부대를 이룰 수도 있다. grade는 "이 프리셋이 대략 어느 정도 세기인지"를
    /// 나타내는 참고용 분류로, PresetGradeConfig가 등급별 요구 전투력 범위를 정의해 만들 때
    /// 검증한다 — 런타임 생성 로직(EnemyCompositionGenerator)은 grade를 "어느 풀에서 뽑을지"
    /// 고르는 데만 쓰고, 전투력 범위 자체는 참고하지 않는다(에디터 authoring-time 전용).
    /// </summary>
    [Serializable]
    public class EnemyPresetData
    {
        public string presetId;
        public string displayName;
        public int grade = 1;
        public List<EnemyPresetUnit> units = new List<EnemyPresetUnit>();

        public EnemyPresetData Clone() => new EnemyPresetData
        {
            presetId = presetId,
            displayName = displayName,
            grade = grade,
            units = units.Select(u => u.Clone()).ToList(),
        };

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(presetId))
                throw new ArgumentException("EnemyPresetData.presetId가 비어 있습니다.");
            if (units == null || units.Count == 0)
                throw new ArgumentException($"{presetId}: units는 최소 1개 이상이어야 합니다.");
            if (grade < 1)
                throw new ArgumentException($"{presetId}: grade는 1 이상이어야 합니다. 현재: {grade}");

            for (int i = 0; i < units.Count; i++)
                units[i].Validate($"{presetId}.units[{i}]");
        }
    }
}
