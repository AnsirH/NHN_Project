using System.Collections.Generic;
using OutGame.Logic.Battle;
using OutGame.ScriptableObjects;
using OutGame.UI.Deployment;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.Editor
{
    /// <summary>
    /// 진영 슬롯 격자(FormationGridPanel.prefab)를 BattleFieldConfig의 rows/columns로부터 굽는
    /// 에디터 도구(2026-08-07) — 슬롯/커넥터를 런타임에 Instantiate하는 대신 프리팹에 정적으로
    /// 미리 배치한다(에디터 가시성 요구사항).
    ///
    /// 배치는 고정 픽셀 디자인(<see cref="FormationGridLayoutMath.CellSize"/> 등)으로 하나의 "디자인
    /// 캔버스"(GridContent)에 굽는다. GridContent는 부모(루트)에 풀스트레치되며, 슬롯/커넥터는 그 안에서
    /// 고정 픽셀 좌표를 쓴다 — 해상도/화면비에 따라 격자가 비례 스케일되지는 않는다(한때
    /// UniformScaleToFit으로 균일 스케일하는 레터박스 방식을 썼으나, 사용자가 프리팹을 직접 재구성하며
    /// 완전히 들어냈다, 2026-08-08 확정 — <see cref="FormationGridLayoutMath"/> 클래스 주석 참고).
    ///
    /// <b>BattleFieldConfig_Default의 rows/columns를 바꾼 뒤에는 반드시 이 메뉴를 다시 실행해야 한다</b>
    /// — 슬롯 수가 코드에서 자동으로 따라가지 않는다(FormationGridView.Awake()가 어긋나면 예외를 던짐).
    /// </summary>
    public static class FormationGridPanelGenerator
    {
        private const float TitleGapTop = 30f; // 원래 SlotGrid가 쓰던 anchoredPos.y=-30과 동일

        private const string FieldConfigPath = "Assets/OutGame/Resources/OutGame/Data/BattleFieldConfig_Default.asset";
        private const string SlotPrefabPath = "Assets/OutGame/Resources/OutGame/Widgets/DeploySlotView.prefab";
        private const string ConnectorPrefabPath = "Assets/OutGame/Resources/OutGame/Widgets/FormationConnector.prefab";
        private const string OutputPath = "Assets/OutGame/Resources/OutGame/Widgets/FormationGridPanel.prefab";

        [MenuItem("OutGame/Formation/Regenerate Grid Panel")]
        private static void Regenerate()
        {
            var fieldConfig = AssetDatabase.LoadAssetAtPath<BattleFieldConfig>(FieldConfigPath);
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            var connectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConnectorPrefabPath);
            if (fieldConfig == null || slotPrefab == null || connectorPrefab == null)
            {
                Debug.LogError("[FormationGridPanelGenerator] 필요한 에셋을 찾을 수 없습니다 — " +
                    $"{FieldConfigPath} / {SlotPrefabPath} / {ConnectorPrefabPath} 확인 필요.");
                return;
            }

            BattleFieldConfigData fieldData = fieldConfig.ToData();
            List<SlotDefinition> slots = fieldData.GenerateSlots();
            Vector2 designSize = FormationGridLayoutMath.DesignSize(fieldData.rows, fieldData.columns);

            // 비활성 상태로 만든다 — FormationGridView.Awake()는 slots/powerLabel/fieldConfig가
            // 배선돼 있어야 통과하는데, 그건 아래에서 다 만든 뒤에야 채워진다. 활성 상태로 만들면
            // 컴포넌트를 붙이는 순간 바로 Awake가 돌아 미배선 예외가 난다.
            GameObject root = new GameObject("FormationGridPanel", typeof(RectTransform));
            root.SetActive(false);
            root.AddComponent<FormationGridView>();
            try
            {
                var rootRect = (RectTransform)root.transform;
                SetStretch(rootRect, offsetMin: Vector2.zero, offsetMax: new Vector2(0f, -TitleGapTop));

                // GridContent = 슬롯/커넥터가 고정 픽셀 좌표로 배치되는 디자인 캔버스. 루트에 풀스트레치
                // 되지만 내부 좌표는 스케일되지 않는다(2026-08-08, 사용자 확정 — 클래스 주석 참고).
                GameObject gridContentGo = new GameObject("GridContent", typeof(RectTransform));
                gridContentGo.transform.SetParent(root.transform, worldPositionStays: false);
                var gridContentRect = (RectTransform)gridContentGo.transform;
                SetStretch(gridContentRect, offsetMin: Vector2.zero, offsetMax: Vector2.zero);

                GameObject powerLabelGo = CreatePowerLabel(root.transform);

                // 커넥터를 슬롯보다 먼저 배치 — sibling 순서상 슬롯이 나중(위)에 그려져 커넥터가
                // 슬롯 원 뒤로 겹친다(RoomGraphPanel.CreateLine과 동일한 z-order 관례).
                foreach ((SlotDefinition from, SlotDefinition to, bool horizontal) in
                         FormationGridLayoutMath.ConnectorPairs(slots, fieldData.rows, fieldData.columns))
                {
                    CreateConnector(gridContentRect, connectorPrefab, from, to, horizontal, fieldData.rows, fieldData.columns);
                }

                var slotViews = new List<DeploySlotView>(slots.Count);
                foreach (SlotDefinition slot in slots)
                    slotViews.Add(CreateSlot(gridContentRect, slotPrefab, slot, fieldData.rows, fieldData.columns));

                var gridView = root.GetComponent<FormationGridView>();
                var gridViewSO = new SerializedObject(gridView);
                SerializedProperty slotsProp = gridViewSO.FindProperty("slots");
                slotsProp.arraySize = slotViews.Count;
                for (int i = 0; i < slotViews.Count; i++)
                    slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
                gridViewSO.FindProperty("powerLabel").objectReferenceValue = powerLabelGo.GetComponent<Text>();
                gridViewSO.FindProperty("fieldConfig").objectReferenceValue = fieldConfig;
                gridViewSO.ApplyModifiedPropertiesWithoutUndo();

                // 필드가 다 채워진 뒤에 활성화 — Awake()가 여기서 돌며 슬롯 수 검증까지 통과하는지
                // 확인된다(생성 직후 바로 문제를 알 수 있음). 저장되는 프리팹도 활성 상태여야 한다.
                root.SetActive(true);

                PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
                Debug.Log($"[FormationGridPanelGenerator] {OutputPath} 생성 완료 — 슬롯 {slots.Count}개, " +
                    $"커넥터 {fieldData.rows * (fieldData.columns - 1) + fieldData.columns * (fieldData.rows - 1)}개, " +
                    $"디자인 크기 {designSize}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static GameObject CreatePowerLabel(Transform parent)
        {
            GameObject go = new GameObject("PowerLabel", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(300f, 30f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = string.Empty; // 런타임에 FormationGridView.SetPower()가 채운다
            return go;
        }

        private static DeploySlotView CreateSlot(RectTransform gridContent, GameObject slotPrefab, SlotDefinition slot, int rows, int columns)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, gridContent);
            instance.name = $"Slot_{slot.slotId}";

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = FormationGridLayoutMath.SlotDesignPosition(slot, rows, columns);
            rect.sizeDelta = new Vector2(FormationGridLayoutMath.SlotSize, FormationGridLayoutMath.SlotSize);

            var slotView = instance.GetComponent<DeploySlotView>();
            var slotSO = new SerializedObject(slotView);
            slotSO.FindProperty("slotId").intValue = slot.slotId;
            slotSO.ApplyModifiedPropertiesWithoutUndo();
            return slotView;
        }

        private static void CreateConnector(
            RectTransform gridContent, GameObject connectorPrefab, SlotDefinition from, SlotDefinition to, bool horizontal, int rows, int columns)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(connectorPrefab, gridContent);
            instance.name = $"Connector_{from.slotId}_{to.slotId}";

            (Vector2 position, Vector2 size) = FormationGridLayoutMath.ConnectorDesignRect(from, to, horizontal, rows, columns);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
