using System;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 진영 슬롯 격자의 연결 트랙(커넥터) 배치 — 아군(AllyFormationView)/적(ArmyDeploymentPanel) 양쪽이
    /// 공유한다(2026-08-05, 참고 이미지: 원형 슬롯이 서로 연결된 격자). 판정/배치 로직을 두 곳에서
    /// 각자 구현하면 어긋날 위험이 있어 여기 하나로 모은다(이 프로젝트 반복 원칙).
    ///
    /// 슬롯 위치는 GridLayoutGroup이 실제로 배치를 끝낸 뒤 읽는 대신, 그 설정값(cellSize/spacing/
    /// padding, 기본 UpperLeft·Horizontal 정렬)으로 직접 계산한다 — Open() 흐름이 호스트 GameObject를
    /// 비활성 상태에서 먼저 구성한 뒤 맨 끝에 SetActive(true)하는 구조라(ArmyDeploymentPanel.Open 등),
    /// 이 시점엔 LayoutRebuilder로 강제해도 비활성 계층에서는 유효한 값이 나오지 않는다(실측 확인 —
    /// 모든 슬롯이 같은 위치로 읽혀 커넥터가 전부 길이 0에 가까운 점으로 찍혔었다). uGUI 함정 #6과
    /// 동일한 이유로 "처음부터 앵커를 직접 계산"하는 쪽을 택한다.
    /// </summary>
    public static class FormationSlotGridBuilder
    {
        private const float DefaultSlotDiameter = 118f;

        public static void BuildConnectors(
            RectTransform connectorLayer,
            Image connectorPrefab,
            GridLayoutGroup grid,
            int rows,
            int columns,
            float slotDiameter = DefaultSlotDiameter)
        {
            if (grid.startCorner != GridLayoutGroup.Corner.UpperLeft || grid.startAxis != GridLayoutGroup.Axis.Horizontal)
                throw new ArgumentException(
                    "FormationSlotGridBuilder는 GridLayoutGroup의 UpperLeft/Horizontal 정렬(기본값)만 지원합니다.");

            foreach (Transform child in connectorLayer) UnityEngine.Object.Destroy(child.gameObject);

            Vector2 CellCenter(int row, int col) => new Vector2(
                grid.padding.left + col * (grid.cellSize.x + grid.spacing.x) + grid.cellSize.x * 0.5f,
                -(grid.padding.top + row * (grid.cellSize.y + grid.spacing.y) + grid.cellSize.y * 0.5f));

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    Vector2 center = CellCenter(row, col);
                    if (col < columns - 1)
                        CreateConnector(connectorLayer, connectorPrefab, center, CellCenter(row, col + 1), slotDiameter);
                    if (row < rows - 1)
                        CreateConnector(connectorLayer, connectorPrefab, center, CellCenter(row + 1, col), slotDiameter);
                }
            }
        }

        private static void CreateConnector(
            RectTransform connectorLayer, Image connectorPrefab, Vector2 from, Vector2 to, float nodeSize)
        {
            Image image = UnityEngine.Object.Instantiate(connectorPrefab, connectorLayer);

            Vector2 delta = to - from;
            float length = Mathf.Max(0f, delta.magnitude - nodeSize); // 슬롯 원 뒤로 겹치게 축소

            var rect = (RectTransform)image.transform;
            // GridLayoutGroup 자식과 동일한 앵커(UpperLeft, 0,1) — from/to 좌표계와 일치시킨다.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(length, rect.sizeDelta.y);
            rect.anchoredPosition = from + delta * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
