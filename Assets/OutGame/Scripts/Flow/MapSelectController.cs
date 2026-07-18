using System;
using System.Linq;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutGame.Flow
{
    /// <summary>
    /// 맵 선택 (§5.2): MapDefinition 목록 표시 → 선택 시 런 생성 후 인게임 씬 로드.
    /// 1차는 에셋 1개뿐이지만 목록 순회 구조로 구현해 복수 맵을 전제한다.
    /// </summary>
    public class MapSelectController : MonoBehaviour
    {
        [SerializeField] private RectTransform mapListContainer;
        [SerializeField] private MapEntryView mapEntryPrefab;
        [SerializeField] private RunConfigAsset runConfig;

        /// <summary>테스트에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private void Awake()
        {
            if (mapListContainer == null || mapEntryPrefab == null)
                throw new InvalidOperationException("MapSelectController의 mapListContainer/mapEntryPrefab이 배선되지 않았습니다.");
        }

        private void Start()
        {
            if (runConfig == null)
                runConfig = Resources.Load<RunConfigAsset>("OutGame/Data/RunConfig_Default");
            if (runConfig == null)
                throw new InvalidOperationException("RunConfigAsset(OutGame/Data/RunConfig_Default)을 찾을 수 없습니다.");

            MapDefinition[] maps = Resources.LoadAll<MapDefinition>("OutGame/Data/Maps");
            if (maps.Length == 0)
                throw new InvalidOperationException("MapDefinition을 찾을 수 없습니다 — SceneSetupM5Data.Run() 실행 필요");

            foreach (MapDefinition map in maps.OrderBy(m => m.DisplayName))
            {
                MapEntryView entry = Instantiate(mapEntryPrefab, mapListContainer);
                entry.Bind(map, () => OnMapSelected(map));
            }
        }

        private void OnMapSelected(MapDefinition mapDef)
        {
            MapGenerationConfig config = mapDef.ToConfig();
            int seed = Environment.TickCount;
            MapState mapState = new MapGenerator(config, seed).Generate();
            RunState run = RunStateFactory.Create(mapState, runConfig.ToData());

            RunSessionContext.SetPendingRun(run);
            LoadSceneAction(SceneNames.InGame);
        }
    }
}
