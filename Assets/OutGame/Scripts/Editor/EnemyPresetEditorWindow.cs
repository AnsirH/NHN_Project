using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Battle;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace OutGame.Editor
{
    /// <summary>
    /// 적 프리셋 조각(EnemyPresetDefinition) 전용 에디터 (2026-08-02) — 목록에서 만들기/복제/삭제,
    /// 유닛별로 병과·병사 수·업그레이드 레벨·증강을 지정하면 아군과 동일한 ArmyStatCalculator
    /// 공식으로 실시간 전투력을 계산해 보여준다. 병과를 고르면 스탯 정의(ClassArmyDefinitions)가
    /// 자동으로 정해진다 — 예전엔 "기준 템플릿"을 별도로 골라야 해서 병과와 안 맞는 조합을 만들 수
    /// 있었는데, 병과=정의 통일(2026-08-02) 이후로는 그 선택지 자체가 없어졌다. 등급 목록은 이 창
    /// 안에서 바로 추가/삭제/설명 편집할 수 있다(PresetGradeConfigAsset을 직접 감싼다).
    /// </summary>
    public class EnemyPresetEditorWindow : EditorWindow
    {
        private const string DefaultCreateFolder = "Assets/OutGame/Resources/OutGame/Data/EnemyPresets";
        private const string GradeConfigPath = "Assets/OutGame/Resources/OutGame/Data/PresetGradeConfig_Default.asset";

        [MenuItem("OutGame/적 프리셋 에디터")]
        private static void Open() => GetWindow<EnemyPresetEditorWindow>("적 프리셋 에디터");

        private List<EnemyPresetDefinition> presets = new List<EnemyPresetDefinition>();
        private EnemyPresetDefinition selected;
        private SerializedObject selectedSO;

        private AugmentDefinition[] augmentDefs = System.Array.Empty<AugmentDefinition>();
        private Dictionary<string, ArmyData> armyDataById = new Dictionary<string, ArmyData>();

        private PresetGradeConfigAsset gradeConfigAsset;
        private SerializedObject gradeConfigSO;
        private PresetGradeConfig gradeConfig = new PresetGradeConfig();
        private bool showGradeManager;

        private Vector2 listScroll;
        private Vector2 detailScroll;

        private void OnEnable()
        {
            RefreshPools();
        }

        private void RefreshPools()
        {
            presets = FindAssets<EnemyPresetDefinition>().OrderBy(p => p.name).ToList();
            ArmyDefinition[] armyDefs = FindAssets<ArmyDefinition>().ToArray();
            armyDataById = armyDefs.ToDictionary(d => d.ToData().id, d => d.ToData());
            augmentDefs = FindAssets<AugmentDefinition>().OrderBy(a => a.name).ToArray();

            gradeConfigAsset = AssetDatabase.LoadAssetAtPath<PresetGradeConfigAsset>(GradeConfigPath);
            gradeConfigSO = gradeConfigAsset != null ? new SerializedObject(gradeConfigAsset) : null;
            try
            {
                gradeConfig = gradeConfigAsset != null ? gradeConfigAsset.ToConfig() : new PresetGradeConfig();
            }
            catch (System.Exception e)
            {
                // 등급 관리 섹션에서 편집 중 일시적으로 잘못된 값(중복 등급 등)이 들어가도 창 전체가
                // 깨지면 안 된다 — 캐시만 비우고 경고만 남긴다.
                gradeConfig = new PresetGradeConfig();
                Debug.LogWarning($"[EnemyPresetEditorWindow] PresetGradeConfig 로드 실패: {e.Message}");
            }

            if (selected != null && !presets.Contains(selected))
                Select(null);
        }

        /// <summary>목록 그룹핑용으로만 등급을 읽는다 — ToData()는 Validate()를 태우므로 편집 중인
        /// (아직 미완성인) 프리셋에서 예외가 나 목록 전체 렌더링이 깨질 수 있다.</summary>
        private static int ReadGradeForGrouping(EnemyPresetDefinition preset)
        {
            var so = new SerializedObject(preset);
            return so.FindProperty("data").FindPropertyRelative("grade").intValue;
        }

        private static T[] FindAssets<T>() where T : UnityEngine.Object
        {
            string filter = "t:" + typeof(T).Name;
            return AssetDatabase.FindAssets(filter)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null)
                .ToArray();
        }

        private void Select(EnemyPresetDefinition preset)
        {
            selected = preset;
            selectedSO = preset != null ? new SerializedObject(preset) : null;
        }

        private void OnGUI()
        {
            DrawGradeManager();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        // ── 등급 관리 ────────────────────────────────────────────────

        /// <summary>PresetGradeConfigAsset을 인스펙터를 따로 열지 않고 이 창 안에서 바로
        /// 추가/삭제/설명 편집할 수 있게 한다 — 예전엔 등급 상한도, 설명도 없어서 프리셋을 만들 때
        /// 참고할 게 전혀 없었다는 지적을 반영.</summary>
        private void DrawGradeManager()
        {
            showGradeManager = EditorGUILayout.Foldout(showGradeManager, "등급 관리 (PresetGradeConfig)", true);
            if (!showGradeManager) return;

            EditorGUILayout.BeginVertical("box");

            if (gradeConfigAsset == null || gradeConfigSO == null)
            {
                EditorGUILayout.HelpBox($"{GradeConfigPath}가 없습니다.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            gradeConfigSO.Update();
            SerializedProperty requirementsProp = gradeConfigSO.FindProperty("config").FindPropertyRelative("requirements");

            int removeIndex = -1;
            for (int i = 0; i < requirementsProp.arraySize; i++)
            {
                SerializedProperty req = requirementsProp.GetArrayElementAtIndex(i);
                SerializedProperty gradeProp = req.FindPropertyRelative("grade");
                SerializedProperty minProp = req.FindPropertyRelative("minPower");
                SerializedProperty maxProp = req.FindPropertyRelative("maxPower");
                SerializedProperty descProp = req.FindPropertyRelative("description");

                // 라벨을 EditorGUILayout.IntField/FloatField의 내장 라벨(드래그로 값이 스크럽되는
                // 핫존)로 쓰지 않는다 — 이전에 폭을 좁게 주니(GUILayout.Width) 실제 숫자 입력칸이
                // 거의 0px로 짜부라져 안 보이는데, 라벨 텍스트는 여전히 드래그로 값을 바꿀 수 있는
                // 상태로 남아서 무심코 클릭/드래그하다 등급 값이 엉뚱한 수(-51 등)로 망가지는 사고가
                // 실제로 발생했다(2026-08-02). 라벨은 순수 GUILayout.Label로, 숫자칸은 라벨 없는
                // 필드로 분리해서 핫존과 입력칸을 명확히 나눈다.
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("등급", GUILayout.Width(28));
                gradeProp.intValue = EditorGUILayout.IntField(gradeProp.intValue, GUILayout.Width(40));
                GUILayout.Space(10);
                GUILayout.Label("최소 전투력", GUILayout.Width(70));
                minProp.floatValue = EditorGUILayout.FloatField(minProp.floatValue, GUILayout.Width(60));
                GUILayout.Space(10);
                GUILayout.Label("최대 전투력", GUILayout.Width(70));
                maxProp.floatValue = EditorGUILayout.FloatField(maxProp.floatValue, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("삭제", GUILayout.Width(50))) removeIndex = i;
                EditorGUILayout.EndHorizontal();
                descProp.stringValue = EditorGUILayout.TextField("설명", descProp.stringValue);
                EditorGUILayout.Space(4);
            }
            if (removeIndex >= 0) requirementsProp.DeleteArrayElementAtIndex(removeIndex);

            if (GUILayout.Button("등급 추가"))
            {
                int nextGrade = 1;
                for (int i = 0; i < requirementsProp.arraySize; i++)
                    nextGrade = Mathf.Max(nextGrade, requirementsProp.GetArrayElementAtIndex(i).FindPropertyRelative("grade").intValue + 1);

                requirementsProp.arraySize++;
                SerializedProperty added = requirementsProp.GetArrayElementAtIndex(requirementsProp.arraySize - 1);
                added.FindPropertyRelative("grade").intValue = nextGrade;
                added.FindPropertyRelative("minPower").floatValue = 0f;
                added.FindPropertyRelative("maxPower").floatValue = 0f;
                added.FindPropertyRelative("description").stringValue = "";
            }

            gradeConfigSO.ApplyModifiedProperties();

            if (GUILayout.Button("등급 설정 저장"))
            {
                EditorUtility.SetDirty(gradeConfigAsset);
                AssetDatabase.SaveAssetIfDirty(gradeConfigAsset);
                RefreshPools();
            }

            EditorGUILayout.EndVertical();
        }

        // ── 좌측: 목록 ──────────────────────────────────────────────

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(240));

            if (GUILayout.Button("새로 만들기")) CreateNew();
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("복제")) Duplicate(selected);
                if (GUILayout.Button("삭제")) Delete(selected);
            }
            if (GUILayout.Button("새로고침")) RefreshPools();

            EditorGUILayout.Space();
            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            var byGrade = presets
                .Where(p => p != null)
                .GroupBy(ReadGradeForGrouping)
                .OrderBy(g => g.Key);

            foreach (var group in byGrade)
            {
                PresetGradeRequirement requirement = gradeConfig.RequirementFor(group.Key);
                string header = requirement != null
                    ? $"{group.Key}등급 ({requirement.minPower:0}~{requirement.maxPower:0})"
                    : $"{group.Key}등급";
                EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

                foreach (EnemyPresetDefinition preset in group.OrderBy(p => p.name))
                {
                    bool isSelected = preset == selected;
                    if (GUILayout.Toggle(isSelected, "   " + preset.name, "Button") && !isSelected)
                        Select(preset);
                }
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void CreateNew()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 프리셋 만들기", "EnemyPreset_New", "asset", "저장할 위치를 선택하세요", DefaultCreateFolder);
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<EnemyPresetDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            RefreshPools();
            Select(asset);
        }

        private void Duplicate(EnemyPresetDefinition source)
        {
            if (source == null) return;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (AssetDatabase.CopyAsset(sourcePath, newPath))
            {
                RefreshPools();
                Select(AssetDatabase.LoadAssetAtPath<EnemyPresetDefinition>(newPath));
            }
        }

        private void Delete(EnemyPresetDefinition target)
        {
            if (target == null) return;
            if (!EditorUtility.DisplayDialog("프리셋 삭제", $"'{target.name}'을(를) 삭제할까요? 되돌릴 수 없습니다.", "삭제", "취소"))
                return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(target));
            Select(null);
            RefreshPools();
        }

        // ── 우측: 편집 ──────────────────────────────────────────────

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical();

            if (selected == null || selectedSO == null)
            {
                EditorGUILayout.LabelField("왼쪽에서 프리셋을 선택하거나 새로 만드세요.");
                EditorGUILayout.EndVertical();
                return;
            }

            selectedSO.Update();
            SerializedProperty dataProp = selectedSO.FindProperty("data");
            SerializedProperty presetIdProp = dataProp.FindPropertyRelative("presetId");
            SerializedProperty displayNameProp = dataProp.FindPropertyRelative("displayName");
            SerializedProperty gradeProp = dataProp.FindPropertyRelative("grade");
            SerializedProperty unitsProp = dataProp.FindPropertyRelative("units");

            EditorGUILayout.LabelField(selected.name, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(presetIdProp, new GUIContent("프리셋 ID"));
            EditorGUILayout.PropertyField(displayNameProp, new GUIContent("표시 이름"));
            DrawGradeField(gradeProp);

            EditorGUILayout.Space();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

            int removeIndex = -1;
            for (int i = 0; i < unitsProp.arraySize; i++)
            {
                if (!DrawUnit(unitsProp.GetArrayElementAtIndex(i), i))
                    removeIndex = i;
            }
            if (removeIndex >= 0) unitsProp.DeleteArrayElementAtIndex(removeIndex);

            if (GUILayout.Button("유닛 추가")) unitsProp.arraySize++;

            EditorGUILayout.EndScrollView();

            selectedSO.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawPowerPreview(unitsProp, gradeProp.intValue);

            if (GUILayout.Button("저장")) AssetDatabase.SaveAssets();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 등급은 PresetGradeConfig_Default에 정의된 등급만 고를 수 있다(자유 입력 int 아님) — 이게
        /// 사실상의 등급 상한이다. 새 등급을 쓰려면 위 "등급 관리" 섹션에서 먼저 요구 범위를
        /// 추가해야 하므로, 등급과 기준이 항상 짝을 이룬다. 선택한 등급의 설명(있으면)도 바로 아래
        /// 보여준다.
        /// </summary>
        private void DrawGradeField(SerializedProperty gradeProp)
        {
            List<PresetGradeRequirement> defined = gradeConfig.requirements.OrderBy(r => r.grade).ToList();
            if (defined.Count == 0)
            {
                EditorGUILayout.PropertyField(gradeProp, new GUIContent("등급"));
                EditorGUILayout.HelpBox(
                    "PresetGradeConfig_Default에 정의된 등급이 없어 등급 상한/설명을 표시할 수 없습니다. " +
                    "위 '등급 관리' 섹션에서 등급 기준을 먼저 추가하세요.",
                    MessageType.Warning);
                return;
            }

            int[] grades = defined.Select(r => r.grade).ToArray();
            string[] labels = defined.Select(r => $"{r.grade}등급 ({r.minPower:0}~{r.maxPower:0})").ToArray();
            int currentIndex = System.Array.IndexOf(grades, gradeProp.intValue);
            if (currentIndex < 0) currentIndex = 0;
            int pickedIndex = EditorGUILayout.Popup("등급", currentIndex, labels);
            gradeProp.intValue = grades[pickedIndex];

            PresetGradeRequirement picked = defined[pickedIndex];
            if (!string.IsNullOrEmpty(picked.description))
                EditorGUILayout.HelpBox(picked.description, MessageType.None);
        }

        /// <summary>유닛 1개를 그린다. false를 반환하면 호출자가 삭제해야 한다는 뜻.</summary>
        private bool DrawUnit(SerializedProperty unitProp, int index)
        {
            bool keep = true;
            EditorGUILayout.BeginVertical("box");

            SerializedProperty armyClassProp = unitProp.FindPropertyRelative("armyClass");
            SerializedProperty soldierCountProp = unitProp.FindPropertyRelative("soldierCount");
            SerializedProperty upgradeLevelProp = unitProp.FindPropertyRelative("upgradeLevel");
            SerializedProperty augmentIdsProp = unitProp.FindPropertyRelative("selectedAugmentIds");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"유닛 {index + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("삭제", GUILayout.Width(50))) keep = false;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(armyClassProp, new GUIContent("병과"));
            var armyClass = (ArmyClass)armyClassProp.intValue;
            EditorGUILayout.LabelField("스탯 정의", ClassArmyDefinitions.DefIdFor(armyClass));
            EditorGUILayout.PropertyField(soldierCountProp, new GUIContent("병사 수"));
            upgradeLevelProp.intValue = EditorGUILayout.IntSlider(
                "업그레이드 레벨", upgradeLevelProp.intValue, 0, ArmyInstance.MaxUpgradeLevel);

            DrawAugmentMultiSelect(augmentIdsProp, armyClass);

            EditorGUILayout.EndVertical();
            return keep;
        }

        /// <summary>
        /// 병과에 적용되지 않는 증강(다른 병과 전용 ItemAugment)은 애초에 목록에서 뺀다. 각 항목은
        /// 이름만이 아니라 실제 효과(설명 + 스탯/수치 요약)를 함께 보여준다 — 파일명만 보고는 어떤
        /// 증강인지 알 수 없다는 지적을 반영. 판정은 ArmyStatCalculator/DeploymentState가 쓰는 것과
        /// 같은 AugmentTargeting.AppliesToClass를 재사용한다(판정 로직이 여러 곳에 흩어지면 서로
        /// 어긋나는 버그가 난 전적이 있다).
        /// </summary>
        private void DrawAugmentMultiSelect(SerializedProperty augmentIdsProp, ArmyClass armyClass)
        {
            EditorGUILayout.LabelField("증강 (이 병과에 적용 가능한 것만 표시)");
            EditorGUI.indentLevel++;

            AugmentDefinition[] applicable = augmentDefs
                .Where(def => AugmentTargeting.AppliesToClass(def.ToData(), armyClass))
                .ToArray();

            if (applicable.Length == 0)
                EditorGUILayout.HelpBox("이 병과에 적용 가능한 증강이 없습니다.", MessageType.Info);

            foreach (AugmentDefinition augmentDef in applicable)
            {
                AugmentData data = augmentDef.ToData();
                int existingIndex = FindStringElement(augmentIdsProp, data.id);
                bool wasSelected = existingIndex >= 0;

                string label = string.IsNullOrWhiteSpace(data.description)
                    ? $"{augmentDef.name} ({DescribeEffect(data)})"
                    : $"{augmentDef.name} — {data.description} ({DescribeEffect(data)})";
                bool isSelected = EditorGUILayout.ToggleLeft(label, wasSelected);

                if (isSelected && !wasSelected)
                {
                    augmentIdsProp.arraySize++;
                    augmentIdsProp.GetArrayElementAtIndex(augmentIdsProp.arraySize - 1).stringValue = data.id;
                }
                else if (!isSelected && wasSelected)
                {
                    augmentIdsProp.DeleteArrayElementAtIndex(existingIndex);
                }
            }

            DrawStaleAugmentWarning(augmentIdsProp, applicable);

            EditorGUI.indentLevel--;
        }

        /// <summary>효과를 한 줄 요약 — StatBoost면 "공격력 +15%" 식, 아니면 효과 유형 이름.</summary>
        private static string DescribeEffect(AugmentData data)
        {
            if (data.effectType != AugmentEffectType.StatBoost)
                return data.effectType == AugmentEffectType.GeneralSkillUpgrade ? "장군 스킬 강화" : data.effectType.ToString();

            string statName = data.targetStat switch
            {
                AugmentStat.Health => "체력",
                AugmentStat.Attack => "공격력",
                AugmentStat.Defense => "방어력",
                _ => data.targetStat.ToString(),
            };
            string sign = data.statBoostPercent >= 0 ? "+" : "";
            return $"{statName} {sign}{data.statBoostPercent * 100:0}%";
        }

        /// <summary>
        /// 병과를 바꾼 뒤 더 이상 적용되지 않는 증강이 selectedAugmentIds에 남아있으면, 필터링된
        /// 목록에는 안 보이니 사용자가 지울 방법이 없어진다 — 남아있다는 걸 알리고 한 번에 정리할
        /// 수 있게 한다.
        /// </summary>
        private void DrawStaleAugmentWarning(SerializedProperty augmentIdsProp, AugmentDefinition[] applicable)
        {
            var applicableIds = new HashSet<string>(applicable.Select(a => a.ToData().id));
            var staleIndices = new List<int>();
            for (int i = 0; i < augmentIdsProp.arraySize; i++)
                if (!applicableIds.Contains(augmentIdsProp.GetArrayElementAtIndex(i).stringValue))
                    staleIndices.Add(i);

            if (staleIndices.Count == 0) return;

            EditorGUILayout.HelpBox(
                $"병과와 맞지 않는 증강 {staleIndices.Count}개가 남아있습니다(병과를 바꾼 뒤 그대로일 수 있음) — 효과가 적용되지 않습니다.",
                MessageType.Warning);
            if (GUILayout.Button("맞지 않는 증강 정리"))
                for (int i = staleIndices.Count - 1; i >= 0; i--)
                    augmentIdsProp.DeleteArrayElementAtIndex(staleIndices[i]);
        }

        private static int FindStringElement(SerializedProperty arrayProp, string value)
        {
            for (int i = 0; i < arrayProp.arraySize; i++)
                if (arrayProp.GetArrayElementAtIndex(i).stringValue == value)
                    return i;
            return -1;
        }

        // ── 실시간 전투력 미리보기 ──────────────────────────────────

        private void DrawPowerPreview(SerializedProperty unitsProp, int grade)
        {
            float totalPower = ComputeTotalPower(unitsProp);
            EditorGUILayout.LabelField($"전투력: {totalPower:0}", EditorStyles.boldLabel);

            if (gradeConfig.requirements.Count == 0)
            {
                EditorGUILayout.HelpBox("PresetGradeConfig_Default.asset이 없어 등급 기준을 확인할 수 없습니다.", MessageType.Info);
                return;
            }

            PresetGradeRequirement requirement = gradeConfig.RequirementFor(grade);
            if (requirement == null)
            {
                EditorGUILayout.HelpBox($"{grade} 등급의 요구 범위가 정의돼 있지 않습니다.", MessageType.Info);
                return;
            }

            if (totalPower < requirement.minPower || totalPower > requirement.maxPower)
                EditorGUILayout.HelpBox(
                    $"{grade}등급 요구 범위({requirement.minPower:0}~{requirement.maxPower:0}) 밖입니다.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"{grade}등급 요구 범위({requirement.minPower:0}~{requirement.maxPower:0}) 안입니다.", MessageType.None);
        }

        /// <summary>
        /// 유닛마다 증강 선택이 다를 수 있어 BattlePowerCalculator.Calculate를 유닛 1개씩 따로
        /// 호출해 합산한다(그 함수는 전달받은 army 전체에 같은 증강 리스트를 적용하는 전제라
        /// 한 번에 여러 유닛을 넣을 수 없다) — 결과는 EnemyCompositionGenerator가 실제로 계산할
        /// 값과 동일한 공식이다.
        /// </summary>
        private float ComputeTotalPower(SerializedProperty unitsProp)
        {
            var powerConfig = new BattlePowerConfig();
            float total = 0f;

            for (int i = 0; i < unitsProp.arraySize; i++)
            {
                SerializedProperty unitProp = unitsProp.GetArrayElementAtIndex(i);
                var armyClass = (ArmyClass)unitProp.FindPropertyRelative("armyClass").intValue;
                string armyDefId = ClassArmyDefinitions.DefIdFor(armyClass);
                if (!armyDataById.ContainsKey(armyDefId)) continue;

                int soldierCount = unitProp.FindPropertyRelative("soldierCount").intValue;
                int upgradeLevel = unitProp.FindPropertyRelative("upgradeLevel").intValue;
                SerializedProperty augmentIdsProp = unitProp.FindPropertyRelative("selectedAugmentIds");

                var selectedAugments = new List<AugmentData>();
                for (int a = 0; a < augmentIdsProp.arraySize; a++)
                {
                    string augId = augmentIdsProp.GetArrayElementAtIndex(a).stringValue;
                    AugmentDefinition def = augmentDefs.FirstOrDefault(ad => ad.ToData().id == augId);
                    if (def != null) selectedAugments.Add(def.ToData());
                }

                var tempArmy = new DeployedArmy
                {
                    armyDefId = armyDefId,
                    armyClass = armyClass,
                    soldierCount = soldierCount,
                    upgradeLevel = upgradeLevel,
                };

                total += BattlePowerCalculator.Calculate(
                    new List<DeployedArmy> { tempArmy }, powerConfig, armyDataById, selectedAugments);
            }

            return total;
        }
    }
}
