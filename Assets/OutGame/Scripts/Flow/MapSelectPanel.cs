using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;

namespace OutGame.Flow
{
    /// <summary>
    /// 맵 선택 (§5.2): 지도 이미지 위 고정된 3개 지점(mapPointSlots)에 MapDefinition을 순서대로
    /// 배정하고, 나머지 지점은 잠금 표시한다. 선택 시 런 생성 후 <see cref="MapConfirmed"/> 발생.
    /// OutGame.unity 안의 한 패널(§3.1 씬 통합) — OutGameFlowController가 활성화하면
    /// Awake()에서 지점을 채우고, 선택 결과는 씬 전환이 아니라 이벤트로 다음 패널(캐릭터 선택)에
    /// 전달된다. 1차는 에셋 1개뿐이라 지점 1개만 활성화되고 나머지 2개는 "준비 중"으로 표시된다.
    ///
    /// 2026-08-08: MapSelectController → MapSelectPanel로 개명(Docs/OutGame/화면 명칭 정리.md) —
    /// 한 씬 안에서 배타적으로 토글되는 화면 콘텐츠는 전부 "Panel" 접미사로 통일(사용자 확정).
    /// </summary>
    public class MapSelectPanel : MonoBehaviour
    {
        [SerializeField] private MapEntryView[] mapPointSlots;
        [SerializeField] private RunConfigAsset runConfig;

        /// <summary>맵이 확정되어 RunState가 만들어졌을 때 발생 — OutGameFlowController가 구독해
        /// 캐릭터 선택 패널로 넘긴다.</summary>
        public event Action<RunState> MapConfirmed;

        private void Awake()
        {
            if (mapPointSlots == null || mapPointSlots.Length == 0)
                throw new InvalidOperationException("MapSelectPanel의 mapPointSlots가 배선되지 않았습니다.");

            if (runConfig == null)
                runConfig = Resources.Load<RunConfigAsset>(ResourcePaths.RunConfigDefault);
            if (runConfig == null)
                throw new InvalidOperationException($"RunConfigAsset({ResourcePaths.RunConfigDefault})을 찾을 수 없습니다.");

            List<MapDefinition> maps = ResourcePool.LoadAllOrThrow<MapDefinition>(
                ResourcePaths.Maps, "MapDefinition을 찾을 수 없습니다 — SceneSetupM5Data.Run() 실행 필요");
            List<MapDefinition> ordered = maps.OrderBy(m => m.DisplayName).ToList();

            for (int i = 0; i < mapPointSlots.Length; i++)
            {
                if (i < ordered.Count)
                {
                    MapDefinition map = ordered[i];
                    mapPointSlots[i].Bind(map, () => OnMapSelected(map));
                }
                else
                {
                    mapPointSlots[i].ShowLocked();
                }
            }
        }

        private void OnMapSelected(MapDefinition mapDef)
        {
            MapGenerationConfig config = mapDef.ToConfig();
            int seed = Environment.TickCount;
            MapState mapState = new MapGenerator(config, seed).Generate();
            RunState run = RunStateFactory.Create(mapState, runConfig.ToData());

            MapConfirmed?.Invoke(run);
        }
    }
}
