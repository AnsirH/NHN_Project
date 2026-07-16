using System.Collections.Generic;
using UnityEngine;

namespace NHN.Infra
{
    /// <summary>
    /// SetActive 기반 프리팹 풀. 생성자에서 전량 프리웜하며,
    /// 이후 Get/Release는 Instantiate/Destroy 없이 동작한다.
    /// </summary>
    public sealed class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _inactive;

        public GameObjectPool(GameObject prefab, Transform parent, int capacity)
        {
            _prefab = prefab;
            _parent = parent;
            _inactive = new Stack<GameObject>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                GameObject instance = Object.Instantiate(prefab, parent);
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
