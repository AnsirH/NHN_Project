using OutGame.Logic.Battle;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 적 프리셋 조각 에셋 (2026-08-02) — 개발자가 EnemyPresetEditorWindow로 직접 만든다.
    /// ArmyDefinition/ItemDefinition과 동일한 "에셋 1개 = 인스턴스 1개" 패턴 —
    /// Resources.LoadAll로 전부 로드해서 풀을 구성한다.
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Enemy Preset", fileName = "EnemyPreset")]
    public class EnemyPresetDefinition : ScriptableObject
    {
        [SerializeField] private EnemyPresetData data = new EnemyPresetData();

        public EnemyPresetData ToData()
        {
            DefinitionValidation.Validate(name, data.Validate);

            return data.Clone();
        }
    }
}
