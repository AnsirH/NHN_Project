using System;
using System.Numerics;

namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 정규화 배치 슬롯(0~1) → 전장 anchor 변환 (순수 계산 — Unity와 BalanceLab CLI가 공유, 중복 구현 금지).
    /// 슬롯 의미는 outGame 계약과 동일: slotX 1=전선/0=후방, slotY 0.5=측면 중앙.
    /// 변환 파라미터(깊이/절반 폭)의 단일 출처는 BattleConfig.
    /// </summary>
    public static class DeploymentGrid
    {
        /// <summary>anchor: x = 전선으로부터 깊이(+뒤), y = 측면 오프셋 (SquadDefinition.Anchor 규약).</summary>
        public static Vector2 SlotToAnchor(float slotX, float slotY, float deploymentDepth, float deploymentHalfWidth)
        {
            float clampedX = Math.Clamp(slotX, 0f, 1f);
            float clampedY = Math.Clamp(slotY, 0f, 1f);
            return new Vector2(
                (1f - clampedX) * deploymentDepth,
                (clampedY - 0.5f) * 2f * deploymentHalfWidth);
        }
    }
}
