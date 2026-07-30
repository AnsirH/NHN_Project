using System.Collections.Generic;
using UnityEngine;

namespace NHN.Infra
{
    /// <summary>
    /// SetActive 기반 프리팹 풀. 생성자(또는 EnsureCapacity)에서 프리웜하며,
    /// 이후 Get/Release는 Instantiate/Destroy 없이 동작한다.
    /// </summary>
    public sealed class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _inactive;
        private int _createdCount;

        public GameObjectPool(GameObject prefab, Transform parent, int capacity)
        {
            _prefab = prefab;
            _parent = parent;
            _inactive = new Stack<GameObject>(capacity);
            EnsureCapacity(capacity);
        }

        /// <summary>
        /// 누적 생성 수가 totalCapacity에 이르도록 부족분만 프리웜한다. 전투 "시작" 시점(초기화 경로)
        /// 전용 — 롤별 프리팹 풀은 상한(MaxUnits)만큼 미리 만들면 낭비가 커서, 매 전투의 실제
        /// 구성 수량으로 키운다. 전투 중 Instantiate 금지 규칙은 그대로 유지된다 (Get은 여전히 예외).
        /// </summary>
        public void EnsureCapacity(int totalCapacity)
        {
            for (; _createdCount < totalCapacity; _createdCount++)
            {
                GameObject instance = Object.Instantiate(_prefab, _parent);
                instance.SetActive(false);
                _inactive.Push(instance);
            }
        }

        public GameObject Get()
        {
            // 용량 초과는 프리웜 계획 오류 — 런타임 Instantiate 금지 규칙 위반이므로 즉시 드러낸다.
            GameObject instance = _inactive.Count > 0
                ? _inactive.Pop()
                : throw new System.InvalidOperationException($"풀 용량 초과: {_prefab.name}");
            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject instance)
        {
            instance.SetActive(false);
            _inactive.Push(instance);
        }
    }
}
