using System;
using OutGame.Logic.Maps;
using UnityEngine;

namespace OutGame.ScriptableObjects
{
    /// <summary>
    /// 맵 선택 목록용 에셋 (§5.2, §6) — 표시 정보 + 맵 생성 설정.
    /// 1차는 에셋 1개만 배치하지만, 맵 선택 UI는 이 타입의 목록을 순회하도록 구현한다.
    /// </summary>
    [CreateAssetMenu(menuName = "OutGame/Map Definition", fileName = "MapDefinition")]
    public class MapDefinition : ScriptableObject
    {
        [SerializeField] private string mapId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private string difficultyLabel;

        [Header("생성 설정")]
        [SerializeField] private MapGenerationConfig generationConfig = new MapGenerationConfig();

        public string MapId => mapId;
        public string DisplayName => displayName;
        public Sprite Thumbnail => thumbnail;
        public string DifficultyLabel => difficultyLabel;

        /// <summary>
        /// 생성 설정을 검증 후 반환한다. 원본 필드가 아닌 사본을 반환하므로
        /// 호출자가 결과를 변형해도 이 에셋(디자인 타임 데이터)은 영향받지 않는다.
        /// </summary>
        public MapGenerationConfig ToConfig()
        {
            if (string.IsNullOrWhiteSpace(mapId))
                throw new InvalidOperationException($"{name}: mapId가 비어 있습니다.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException($"{name}: displayName이 비어 있습니다.");

            DefinitionValidation.Validate(name, generationConfig.Validate);

            return generationConfig.Clone();
        }
    }
}
