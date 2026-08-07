using UnityEngine;

namespace NHN.Presentation
{
    /// <summary>
    /// 시뮬 평면 좌표(System.Numerics.Vector2)와 월드 좌표(UnityEngine.Vector3)의 유일한 변환점.
    /// 시뮬 (x, y) → 월드 (x, 0, -y). 변환 코드를 이 클래스 밖에 두지 않는다.
    /// 측면 축 반전 (2026-08-04): 배치 UI의 위쪽 행(slotY 작음 → 시뮬 Y 음수)이 화면에서
    /// 먼 쪽(+Z)에 서도록 한다 — 반전 전에는 UI 위쪽이 카메라 가까운 쪽에 떨어졌다.
    /// </summary>
    public static class SimViewMapper
    {
        public static Vector3 ToWorld(System.Numerics.Vector2 simPosition)
        {
            return new Vector3(simPosition.X, 0f, -simPosition.Y);
        }

        public static System.Numerics.Vector2 ToSim(Vector3 worldPosition)
        {
            return new System.Numerics.Vector2(worldPosition.x, -worldPosition.z);
        }
    }
}
