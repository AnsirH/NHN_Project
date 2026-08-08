using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 진영 슬롯 격자의 배치 좌표 계산 — 순수 함수, Unity 오브젝트(GameObject/Component)에 의존하지
    /// 않는다(2026-08-07). 슬롯/커넥터를 런타임에 Instantiate하는 대신 에디터에 미리 구워 넣기 위해
    /// 도입 — GridLayoutGroup의 고정 Cell Size는 화면비가 바뀌면 컨테이너 실제 크기와 어긋나 해상도
    /// 대응이 안 됐다(사용자 지적).
    ///
    /// <b>설계 방식(2026-08-07, 3차 수정)</b> — 처음엔 슬롯 위치만 정규화 앵커로, 그다음엔 슬롯 크기까지
    /// 셀 비율 스트레치로 만들었는데, 둘 다 "슬롯마다 개별적으로 반응"하게 만드느라 여백/두께 비율
    /// 계산과 슬롯별 AspectRatioFitter+전용 래퍼가 필요해 과했다(사용자 지적 — 참고 이미지 속 게임은
    /// 슬롯 간격·연결선 두께가 항상 "고정된 비례 관계"를 유지한 채 화면 크기에 맞춰 통째로 커지거나
    /// 작아진다). 그래서 단순한 고정 픽셀 디자인으로 되돌려(<see cref="CellSize"/> 등 상수), 격자 전체를
    /// 하나의 "디자인 캔버스"(<see cref="DesignSize"/> 크기)에 굽는다.
    ///
    /// <b>2026-08-08</b>: 한때 이 캔버스를 UniformScaleToFit으로 컨테이너 크기에 맞춰 균일 스케일하는
    /// 레터박스 방식을 썼으나, 사용자가 프리팹을 직접 재구성하면서 그 컴포넌트를 완전히 들어냈다(사용자
    /// 확정) — GridContent는 이제 그냥 부모(뷰포트)에 풀스트레치되고, 슬롯/커넥터는 여전히 고정 픽셀
    /// 좌표를 쓴다. 즉 <b>현재는 화면비/컨테이너 크기 변화에 격자가 비례 대응하지 않는다</b> — 디자인
    /// 캔버스 크기(<see cref="DesignSize"/>)와 실제 컨테이너 크기가 다르면 격자가 잘리거나 빈 공간이
    /// 남을 수 있다. 이 상태로 가져가기로 한 사용자 결정이며, 재도입이 필요해지면 이 클래스는 그대로
    /// 재사용 가능하다(순수 좌표 계산이라 스케일링 방식과 무관).
    ///
    /// 에디터 생성 도구(FormationGridPanelGenerator)와 EditMode 테스트 양쪽에서 이 클래스 하나만
    /// 재사용한다 — 배치 규칙(전체 격자 연결)을 두 곳에서 각자 구현하면 어긋날 위험이 있다(이 프로젝트
    /// 반복 원칙).
    /// </summary>
    public static class FormationGridLayoutMath
    {
        /// <summary>정사각형 셀 한 칸의 디자인 픽셀 크기 — 리팩터 이전부터 쓰던 값 그대로 유지.</summary>
        public const float CellSize = 144f;

        /// <summary>슬롯(원형) 디자인 지름 — 셀보다 작게 남겨 커넥터가 지나갈 여백을 확보.</summary>
        public const float SlotSize = 118f;

        /// <summary>커넥터(연결 트랙) 디자인 두께.</summary>
        public const float ConnectorThickness = 22f;

        /// <summary>격자 전체를 담는 디자인 캔버스 크기 — 이 크기 기준으로 배치한 뒤 통째로 스케일된다.</summary>
        public static Vector2 DesignSize(int rows, int columns) => new Vector2(columns * CellSize, rows * CellSize);

        /// <summary>가로/세로로 인접한 슬롯 쌍마다 하나씩 — 전체 격자 연결(사용자 확정 사양).
        /// slotId = row*columns+col(BattleFieldConfigData.GenerateSlots)이므로 인접 판정에 그 규칙을
        /// 그대로 쓴다 — 별도 좌표 비교보다 안전(부동소수점 오차 없음).</summary>
        public static IEnumerable<(SlotDefinition from, SlotDefinition to, bool horizontal)> ConnectorPairs(
            IReadOnlyList<SlotDefinition> slots, int rows, int columns)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (slots.Count != rows * columns)
                throw new ArgumentException(
                    $"slots 개수({slots.Count})가 rows*columns({rows * columns})와 다릅니다.");

            var bySlotId = new SlotDefinition[slots.Count];
            foreach (SlotDefinition slot in slots) bySlotId[slot.slotId] = slot;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int id = row * columns + col;
                    if (col < columns - 1)
                        yield return (bySlotId[id], bySlotId[id + 1], true); // 가로 인접(다음 열)
                    if (row < rows - 1)
                        yield return (bySlotId[id], bySlotId[id + columns], false); // 세로 인접(다음 행)
                }
            }
        }

        /// <summary>슬롯 중심의 디자인 좌표 — 디자인 캔버스 "중앙"을 원점으로 한다(호출자가 캔버스
        /// 중앙에 피벗을 둔 RectTransform의 anchoredPosition으로 그대로 쓸 수 있도록).</summary>
        public static Vector2 SlotDesignPosition(SlotDefinition slot, int rows, int columns)
        {
            int row = slot.slotId / columns;
            int col = slot.slotId % columns;
            Vector2 designSize = DesignSize(rows, columns);
            float x = col * CellSize + CellSize * 0.5f - designSize.x * 0.5f;
            float y = designSize.y * 0.5f - (row * CellSize + CellSize * 0.5f);
            return new Vector2(x, y);
        }

        /// <summary>두 인접 슬롯 중심 사이를 잇는 커넥터의 디자인 좌표/크기 — 슬롯 원 뒤로 살짝 겹치도록
        /// 중심 간 거리에서 슬롯 지름만큼 뺀 길이를 쓴다(리팩터 이전부터 쓰던 "length - nodeSize" 방식).</summary>
        public static (Vector2 position, Vector2 size) ConnectorDesignRect(
            SlotDefinition from, SlotDefinition to, bool horizontal, int rows, int columns)
        {
            Vector2 a = SlotDesignPosition(from, rows, columns);
            Vector2 b = SlotDesignPosition(to, rows, columns);
            Vector2 mid = (a + b) * 0.5f;
            float centerDistance = horizontal ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.y - a.y);
            float length = Mathf.Max(0f, centerDistance - SlotSize);
            Vector2 size = horizontal ? new Vector2(length, ConnectorThickness) : new Vector2(ConnectorThickness, length);
            return (mid, size);
        }
    }
}
