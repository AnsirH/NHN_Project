using System;

namespace NHN.Simulation.Battle
{
    /// <summary>
    /// 상태이상 공용 시스템 1개 (불변조건 5) — 부정 5종 + 긍정 효과(회복/공버프/피해감소)까지
    /// 전부 여기서 처리한다. v4: 힐 장판·전투 함성·장군 방진도 신규 시스템 없이 이 경로를 쓴다.
    /// 상태이상별 클래스 없음: (유닛 × 타입) 평면 배열에 남은시간/세기만 저장하고,
    /// 타입별 거동 차이는 IsDotType 같은 분류 함수로만 갈라진다. 틱 루프 중 힙 할당 없음.
    /// </summary>
    public sealed class StatusEffectSystem
    {
        public const int TypeCount = 9;

        /// <summary>[unit * TypeCount + type] 남은 지속시간(초). 0 이하 = 비활성.</summary>
        private readonly float[] _remainings;
        /// <summary>[unit * TypeCount + type] 세기 — 도트: 초당 데미지, 회복: 초당 회복량, 배율형: 배율.</summary>
        private readonly float[] _magnitudes;
        /// <summary>유닛별 활성 상태이상 개수 — HasAnyActive를 O(1)로.</summary>
        private readonly int[] _activeCounts;

        public StatusEffectSystem(int unitCount)
        {
            _remainings = new float[unitCount * TypeCount];
            _magnitudes = new float[unitCount * TypeCount];
            _activeCounts = new int[unitCount];
        }

        /// <summary>도트형(지속 데미지) 분류 — 새 도트 상태이상은 여기 한 줄 추가로 동작한다.</summary>
        private static bool IsDotType(StatusEffectType type)
        {
            return type == StatusEffectType.Poison || type == StatusEffectType.Burn;
        }

        /// <summary>부여/재부여. 재부여는 지속시간 연장(max)과 세기 덮어쓰기 — 장판이 매 틱 갱신하는 전제.</summary>
        public void Apply(int unitIndex, StatusEffectType type, float duration, float magnitude)
        {
            if (duration <= 0f)
            {
                return;
            }
            int slot = unitIndex * TypeCount + (int)type;
            if (_remainings[slot] <= 0f)
            {
                _activeCounts[unitIndex]++;
            }
            _remainings[slot] = Math.Max(_remainings[slot], duration);
            _magnitudes[slot] = magnitude;
        }

        public bool IsActive(int unitIndex, StatusEffectType type)
        {
            return _remainings[unitIndex * TypeCount + (int)type] > 0f;
        }

        /// <summary>활성 상태의 세기. 비활성이면 0 — 배율형은 호출부에서 0 이하를 1로 취급한다.</summary>
        public float GetMagnitude(int unitIndex, StatusEffectType type)
        {
            int slot = unitIndex * TypeCount + (int)type;
            return _remainings[slot] > 0f ? _magnitudes[slot] : 0f;
        }

        public bool HasAnyActive(int unitIndex)
        {
            return _activeCounts[unitIndex] > 0;
        }

        /// <summary>
        /// 지속시간 감쇠 + 도트 데미지를 pendingDotDamage에, 지속 회복을 pendingHeal에 누적.
        /// 도트는 방어력 감쇠를 받지 않으므로(기획 합의) 일반 피해와 별도 누적기에 쌓인다.
        /// 사망 유닛은 건너뛴다. 배율형(Mark/AttackUp/DamageResist)은 여기서 아무것도 하지 않는다 —
        /// 세기는 데미지 계산 시점에 GetMagnitude로 조회된다.
        /// </summary>
        public void Tick(float dt, int unitCount, bool[] alives, float[] pendingDotDamage, float[] pendingHeal)
        {
            for (int i = 0; i < unitCount; i++)
            {
                if (_activeCounts[i] == 0 || !alives[i])
                {
                    continue;
                }
                int baseSlot = i * TypeCount;
                for (int t = 0; t < TypeCount; t++)
                {
                    int slot = baseSlot + t;
                    if (_remainings[slot] <= 0f)
                    {
                        continue;
                    }
                    if (IsDotType((StatusEffectType)t))
                    {
                        pendingDotDamage[i] += _magnitudes[slot] * dt;
                    }
                    else if ((StatusEffectType)t == StatusEffectType.HealOverTime)
                    {
                        pendingHeal[i] += _magnitudes[slot] * dt;
                    }
                    _remainings[slot] -= dt;
                    if (_remainings[slot] <= 0f)
                    {
                        _remainings[slot] = 0f;
                        _activeCounts[i]--;
                    }
                }
            }
        }
    }
}
