using NHN.Data;
using NHN.Infra;
using NHN.Simulation.Stress;
using UnityEngine;

namespace NHN.Presentation.Stress
{
    /// <summary>
    /// 스트레스 테스트 씬 진입점. 시뮬/풀/HUD를 생성·주입하고 고정 틱을 구동한다.
    /// 뷰 동기화는 틱 사이 보간 알파로 Transform 위치만 갱신한다 (유닛별 MonoBehaviour 없음).
    /// </summary>
    public sealed class StressTestBootstrap : MonoBehaviour
    {
        /// <summary>프레임 급락 시 틱이 무한 누적되는 것을 막는 프레임당 틱 상한.</summary>
        private const int MaxTicksPerFrame = 4;

        [SerializeField] private StressTestConfigSO config;
        [SerializeField] private GameObject unitPrefab;
        [SerializeField] private Material teamAMaterial;
        [SerializeField] private Material teamBMaterial;
        [SerializeField] private StressHud hud;
        [SerializeField] private Transform unitContainer;
        [SerializeField] private int initialUnitCount = 100;

        private StressSimulation _simulation;
        private GameObjectPool _pool;
        private GameObject[] _unitObjects;
        private Transform[] _unitTransforms;
        private float _accumulator;
        private int _activeCount;

        private void Awake()
        {
            Application.targetFrameRate = config.TargetFrameRate;
            QualitySettings.vSyncCount = 0;

            _simulation = new StressSimulation(config.ToSimConfig());
            _pool = new GameObjectPool(unitPrefab, unitContainer, config.MaxUnits);
            _unitObjects = new GameObject[config.MaxUnits];
            _unitTransforms = new Transform[config.MaxUnits];

            hud.Initialize(this);
            SetUnitCount(initialUnitCount);
        }

        /// <summary>HUD 버튼에서 호출. 시뮬을 리셋하고 뷰 유닛을 풀에서 재배분한다.</summary>
        public void SetUnitCount(int unitCount)
        {
            for (int i = 0; i < _activeCount; i++)
            {
                _pool.Release(_unitObjects[i]);
                _unitObjects[i] = null;
                _unitTransforms[i] = null;
            }

            _simulation.Reset(unitCount, config.Seed);
            _activeCount = _simulation.UnitCount;
            _accumulator = 0f;

            for (int i = 0; i < _activeCount; i++)
            {
                GameObject unit = _pool.Get();
                _unitObjects[i] = unit;
                _unitTransforms[i] = unit.transform;
                // 초기화 시점 1회 조회 — Update 루프에서는 캐시된 참조만 사용한다.
                unit.GetComponentInChildren<Renderer>().sharedMaterial =
                    _simulation.GetTeam(i) == 0 ? teamAMaterial : teamBMaterial;
                _unitTransforms[i].localPosition = SimViewMapper.ToWorld(_simulation.GetPosition(i));
            }

            hud.DisplayUnitCount(_activeCount);
        }

        private void Update()
        {
            _accumulator += Time.deltaTime;

            float tickDeltaTime = _simulation.TickDeltaTime;
            int ticksThisFrame = 0;
            while (_accumulator >= tickDeltaTime && ticksThisFrame < MaxTicksPerFrame)
            {
                _simulation.Tick();
                _accumulator -= tickDeltaTime;
                ticksThisFrame++;
            }
            if (_accumulator >= tickDeltaTime)
            {
                // 상한 초과분은 버린다 — 느려진 프레임에서 틱 폭주로 더 느려지는 악순환 방지.
                _accumulator %= tickDeltaTime;
            }

            float alpha = _accumulator / tickDeltaTime;
            for (int i = 0; i < _activeCount; i++)
            {
                _unitTransforms[i].localPosition =
                    SimViewMapper.ToWorld(_simulation.GetInterpolatedPosition(i, alpha));
            }
        }
    }
}
