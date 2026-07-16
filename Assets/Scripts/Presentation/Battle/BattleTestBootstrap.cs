using System;
using NHN.Data;
using NHN.Infra;
using NHN.Simulation.Battle;
using UnityEngine;

namespace NHN.Presentation.Battle
{
    /// <summary>
    /// 전투 뷰 테스트 씬 진입점. 시뮬 생성/고정 틱 구동/뷰 동기화를 담당한다.
    /// 유닛·투사체별 MonoBehaviour 없음 — 캐시된 Transform 배열만 갱신한다.
    /// </summary>
    public sealed class BattleTestBootstrap : MonoBehaviour
    {
        /// <summary>프레임 급락 시 틱 폭주를 막는 프레임당 틱 상한.</summary>
        private const int MaxTicksPerFrame = 4;

        /// <summary>투사체 발사 높이(유닛 몸통) — 뷰 표현 상수.</summary>
        private const float ProjectileLaunchHeight = 1f;

        /// <summary>은신 유닛 표시 알파 — 뷰 표현 상수.</summary>
        private const float StealthAlpha = 0.35f;

        [Serializable]
        private struct SquadSetup
        {
            public RoleData role;
            public int count;
            [Tooltip("x=전선으로부터 깊이(+뒤), y=측면 오프셋")]
            public Vector2 anchor;
        }

        [SerializeField] private BattleConfigSO config;
        [SerializeField] private SquadSetup[] armyA;
        [SerializeField] private SquadSetup[] armyB;
        [SerializeField] private GameObject unitPrefab;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform unitContainer;
        [SerializeField] private Transform projectileContainer;
        [SerializeField] private BattleHud hud;

        private BattleSimulation _sim;
        private GameObjectPool _unitPool;
        private GameObjectPool _projectilePool;
        private GameObject[] _unitObjects;
        private Transform[] _unitTransforms;
        private bool[] _unitVisible;
        private Renderer[] _unitRenderers;
        private Color[] _unitColors;
        private bool[] _unitStealthShown;
        private Material _baseMaterial;
        private Material _stealthMaterial;
        private GameObject[] _projObjects;
        private Transform[] _projTransforms;
        private int _projVisibleCount;
        private float _accumulator;
        private bool _resultShown;
        private MaterialPropertyBlock _propertyBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        private void Awake()
        {
            // 에디터 자동 검증 편의 — 모바일 런타임에는 영향 없음.
            Application.runInBackground = true;

            _propertyBlock = new MaterialPropertyBlock();

            int maxUnits = TotalUnits(armyA) + TotalUnits(armyB);
            _unitPool = new GameObjectPool(unitPrefab, unitContainer, maxUnits);
            _projectilePool = new GameObjectPool(projectilePrefab, projectileContainer, config.MaxUnits);
            _unitObjects = new GameObject[maxUnits];
            _unitTransforms = new Transform[maxUnits];
            _unitVisible = new bool[maxUnits];
            _unitRenderers = new Renderer[maxUnits];
            _unitColors = new Color[maxUnits];
            _unitStealthShown = new bool[maxUnits];
            _projObjects = new GameObject[config.MaxUnits];
            _projTransforms = new Transform[config.MaxUnits];

            _baseMaterial = unitPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
            _stealthMaterial = CreateStealthMaterial(_baseMaterial);

            hud.Initialize(this);
            StartBattle();
        }

        /// <summary>HUD Restart 버튼에서도 호출된다.</summary>
        public void StartBattle()
        {
            ReleaseAllViews();

            _sim = new BattleSimulation(config.ToConfig(), BuildArmy(armyA), BuildArmy(armyB), config.Seed);
            _accumulator = 0f;
            _resultShown = false;
            hud.Clear();

            // 유닛 인덱스는 (A군 분대 순서 → B군 분대 순서) — ArmyDefinition의 계약과 동일하게 순회한다.
            int unitIndex = 0;
            SpawnSquadViews(armyA, isTeamB: false, ref unitIndex);
            SpawnSquadViews(armyB, isTeamB: true, ref unitIndex);
        }

        private void Update()
        {
            if (_sim == null)
            {
                return;
            }

            _accumulator += Time.deltaTime;
            float tickDeltaTime = _sim.TickDeltaTime;
            int ticksThisFrame = 0;
            while (_accumulator >= tickDeltaTime && ticksThisFrame < MaxTicksPerFrame)
            {
                _sim.Tick();
                _accumulator -= tickDeltaTime;
                ticksThisFrame++;
            }
            if (_accumulator >= tickDeltaTime)
            {
                _accumulator %= tickDeltaTime;
            }

            float alpha = _accumulator / tickDeltaTime;
            SyncUnitViews(alpha);
            SyncProjectileViews(alpha);

            if (_sim.Finished && !_resultShown)
            {
                _resultShown = true;
                BattleResult result = _sim.Result;
                string winner = result.Winner == 0 ? "A군 승리"
                    : result.Winner == 1 ? "B군 승리"
                    : "무승부";
                hud.ShowResult($"{winner}  (생존 A:{result.SurvivorsTeamA}  B:{result.SurvivorsTeamB})");
            }
        }

        private void SyncUnitViews(float alpha)
        {
            for (int i = 0; i < _sim.UnitCount; i++)
            {
                if (!_unitVisible[i])
                {
                    continue;
                }
                if (!_sim.IsAlive(i))
                {
                    _unitPool.Release(_unitObjects[i]);
                    _unitObjects[i] = null;
                    _unitTransforms[i] = null;
                    _unitRenderers[i] = null;
                    _unitVisible[i] = false;
                    continue;
                }

                bool stealthed = _sim.IsStealthed(i);
                if (stealthed != _unitStealthShown[i])
                {
                    ApplyStealthVisual(i, stealthed);
                }

                _unitTransforms[i].localPosition = SimViewMapper.ToWorld(_sim.GetInterpolatedPosition(i, alpha));
            }
        }

        /// <summary>은신 표시 전환: 반투명 재질 스왑 + 롤 색 알파 조정. 상태 변화 시에만 호출된다.</summary>
        private void ApplyStealthVisual(int unitIndex, bool stealthed)
        {
            _unitStealthShown[unitIndex] = stealthed;
            Renderer renderer = _unitRenderers[unitIndex];
            renderer.sharedMaterial = stealthed ? _stealthMaterial : _baseMaterial;

            Color color = _unitColors[unitIndex];
            color.a = stealthed ? StealthAlpha : 1f;
            _propertyBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>기본 재질의 투명(URP Lit Transparent) 변형을 런타임에 1개 생성 — 은신 유닛이 공유한다.</summary>
        private static Material CreateStealthMaterial(Material source)
        {
            var material = new Material(source);
            material.SetFloat(SurfaceId, 1f); // 1 = Transparent
            material.SetFloat(BlendId, 0f);   // 0 = Alpha
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat(ZWriteId, 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            return material;
        }

        private void SyncProjectileViews(float alpha)
        {
            int activeCount = _sim.ProjectileCount;

            for (int p = _projVisibleCount; p < activeCount; p++)
            {
                _projObjects[p] = _projectilePool.Get();
                _projTransforms[p] = _projObjects[p].transform;
            }
            for (int p = activeCount; p < _projVisibleCount; p++)
            {
                _projectilePool.Release(_projObjects[p]);
                _projObjects[p] = null;
                _projTransforms[p] = null;
            }
            _projVisibleCount = activeCount;

            for (int p = 0; p < activeCount; p++)
            {
                BattleSimulation.ProjectileState state = _sim.GetProjectileState(p, alpha);
                float t = state.Progress01;
                Vector3 start = SimViewMapper.ToWorld(state.LaunchPosition);
                Vector3 end = SimViewMapper.ToWorld(state.ImpactPosition);
                Vector3 position = Vector3.Lerp(start, end, t);
                // 포물선: 발사 높이에서 착탄점(바닥)으로 + 정점 높이 arcHeight의 아치.
                position.y = Mathf.Lerp(ProjectileLaunchHeight, 0f, t) + 4f * state.ArcHeight * t * (1f - t);
                _projTransforms[p].localPosition = position;
            }
        }

        private void SpawnSquadViews(SquadSetup[] setups, bool isTeamB, ref int unitIndex)
        {
            for (int s = 0; s < setups.Length; s++)
            {
                Color color = setups[s].role.RoleColor;
                if (isTeamB)
                {
                    // 같은 롤이 양 진영에 있을 때를 위한 팀 구분 톤 다운.
                    color = Color.Lerp(color, Color.black, 0.35f);
                }
                float scale = setups[s].role.UnitRadius / 0.5f; // 프리팹 캡슐 기본 반경 0.5 기준

                for (int k = 0; k < setups[s].count; k++)
                {
                    GameObject unit = _unitPool.Get();
                    _unitObjects[unitIndex] = unit;
                    _unitTransforms[unitIndex] = unit.transform;
                    _unitVisible[unitIndex] = true;

                    // 초기화 시점 1회 조회 — Update에서는 캐시만 사용.
                    _unitRenderers[unitIndex] = unit.GetComponentInChildren<Renderer>();
                    _unitColors[unitIndex] = color;
                    // 재질·색을 함께 리셋 — 풀 재사용 시 이전 은신 재질이 남지 않도록 항상 호출.
                    ApplyStealthVisual(unitIndex, _sim.IsStealthed(unitIndex));

                    unit.transform.localScale = Vector3.one * scale;
                    unit.transform.localPosition = SimViewMapper.ToWorld(_sim.GetPosition(unitIndex));
                    unitIndex++;
                }
            }
        }

        private void ReleaseAllViews()
        {
            if (_unitObjects == null)
            {
                return;
            }
            for (int i = 0; i < _unitObjects.Length; i++)
            {
                if (_unitObjects[i] != null)
                {
                    _unitPool.Release(_unitObjects[i]);
                    _unitObjects[i] = null;
                    _unitTransforms[i] = null;
                    _unitRenderers[i] = null;
                    _unitVisible[i] = false;
                }
            }
            for (int p = 0; p < _projVisibleCount; p++)
            {
                _projectilePool.Release(_projObjects[p]);
                _projObjects[p] = null;
                _projTransforms[p] = null;
            }
            _projVisibleCount = 0;
        }

        private void OnDestroy()
        {
            if (_stealthMaterial != null)
            {
                Destroy(_stealthMaterial);
            }
        }

        private ArmyDefinition BuildArmy(SquadSetup[] setups)
        {
            var squads = new SquadDefinition[setups.Length];
            for (int s = 0; s < setups.Length; s++)
            {
                squads[s] = new SquadDefinition(
                    setups[s].role.ToDefinition(),
                    setups[s].count,
                    new System.Numerics.Vector2(setups[s].anchor.x, setups[s].anchor.y));
            }
            return new ArmyDefinition(squads);
        }

        private static int TotalUnits(SquadSetup[] setups)
        {
            int total = 0;
            for (int s = 0; s < setups.Length; s++)
            {
                total += setups[s].count;
            }
            return total;
        }
    }
}
