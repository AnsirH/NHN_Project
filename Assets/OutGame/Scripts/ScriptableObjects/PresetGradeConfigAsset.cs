using OutGame.Logic.Battle;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 프리셋 등급별 요구 전투력 범위 에셋 (2026-08-02) — 작업자가 인스펙터(또는
    /// EnemyPresetEditorWindow)에서 조정. EnemyCompositionConfigAsset과 동일한 패턴.
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Preset Grade Config", fileName = "PresetGradeConfig")]
    public class PresetGradeConfigAsset : ScriptableObject
    {
        [SerializeField] private PresetGradeConfig config = new PresetGradeConfig();

        public PresetGradeConfig ToConfig()
        {
            DefinitionValidation.Validate(name, config.Validate);

            return config.Clone();
        }
    }
}
