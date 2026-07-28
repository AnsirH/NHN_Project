using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OutGame.Flow
{
    /// <summary>
    /// 캐릭터 선택 (§5.2.5): 맵 확정 직후 → 캐릭터 목록 표시(좌우 화살표/아이콘 클릭으로 미리보기
    /// 전환, 선택된 아이콘만 하이라이트) → 확인 시 RunState.selectedCharacterId를 채우고 인게임
    /// 씬으로 이동한다. 캐릭터는 스킬을 정확히 하나만 가지며, 어떤 캐릭터를 골랐는지가 곧 인게임에서
    /// 쓸 스킬을 결정한다 — 스킬의 세부 효과는 정의하지 않는다(§4-2x, 인게임 스킬 시스템 책임).
    ///
    /// 아이콘 4개는 런타임 Instantiate가 아니라 화면 프리팹에 미리 배치돼 있다(SceneSetupM7UI,
    /// 2026-07-28 수정) — 개수가 고정된 정적 화면 요소라 매번 새로 만들 이유가 없다. 캐릭터가
    /// 5종 이상으로 늘어나면 프리팹의 아이콘 슬롯도 그만큼 늘려야 한다.
    /// </summary>
    public class CharacterSelectController : MonoBehaviour
    {
        [Header("표시 영역")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text skillNameText;
        [SerializeField] private Text skillDescriptionText;

        [Header("네비게이션")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private CharacterIconView[] iconViews;

        /// <summary>테스트에서 실제 씬 전환 없이 호출을 가로챌 수 있게 하는 훅.</summary>
        public Action<string> LoadSceneAction = SceneManager.LoadScene;

        private List<PlayerCharacterDefinition> characters;
        private int currentIndex;

        private void Awake()
        {
            if (portraitImage == null || nameText == null || descriptionText == null
                || skillNameText == null || skillDescriptionText == null || leftArrowButton == null
                || rightArrowButton == null || confirmButton == null
                || iconViews == null || iconViews.Length == 0)
                throw new InvalidOperationException("CharacterSelectController의 필드가 배선되지 않았습니다.");
        }

        private void Start()
        {
            characters = Resources.LoadAll<PlayerCharacterDefinition>("OutGame/Data/Characters")
                .OrderBy(c => c.SortOrder).ToList();
            if (characters.Count == 0)
                throw new InvalidOperationException(
                    "PlayerCharacterDefinition을 찾을 수 없습니다 — SceneSetupM7Data.Run() 실행 필요 (§5.2.5)");
            if (characters.Count != iconViews.Length)
                throw new InvalidOperationException(
                    $"캐릭터 정의 개수({characters.Count})와 화면에 미리 배치된 아이콘 슬롯 수({iconViews.Length})가 " +
                    "다릅니다 — SceneSetupM7UI.Run() 재실행 또는 아이콘 슬롯 수 조정이 필요합니다 (§5.2.5).");
            // 확인 버튼을 누른 캐릭터만 늦게 검증하면 화면을 다 둘러본 뒤에야 깨진 콘텐츠가 드러난다 —
            // 다른 콘텐츠 풀(enemyTemplate 등)과 같이 로드 시점에 전부 fail-fast (코드 리뷰 MEDIUM 수정).
            foreach (PlayerCharacterDefinition characterDef in characters) characterDef.ToData();

            for (int i = 0; i < characters.Count; i++)
            {
                int capturedIndex = i; // for 루프 변수는 재사용되므로 클로저용 로컬 복사본이 필요
                iconViews[i].Bind(characters[i], () => Select(capturedIndex));
            }

            leftArrowButton.onClick.AddListener(
                () => Select((currentIndex - 1 + characters.Count) % characters.Count));
            rightArrowButton.onClick.AddListener(
                () => Select((currentIndex + 1) % characters.Count));
            confirmButton.onClick.AddListener(OnConfirmClicked);

            Select(0);
        }

        private void Select(int index)
        {
            currentIndex = index;
            PlayerCharacterDefinition characterDef = characters[index];

            portraitImage.sprite = characterDef.Portrait;
            nameText.text = characterDef.DisplayName;
            descriptionText.text = characterDef.Description;
            skillNameText.text = characterDef.SkillName;
            skillDescriptionText.text = characterDef.SkillDescription;

            for (int i = 0; i < iconViews.Length; i++)
                iconViews[i].SetSelected(i == index);
        }

        private void OnConfirmClicked()
        {
            if (RunSessionContext.PendingRun == null)
                throw new InvalidOperationException(
                    "선택할 런이 없습니다 — 맵 선택을 거치지 않고 이 씬을 단독 실행했을 수 있습니다.");

            RunSessionContext.PendingRun.selectedCharacterId = characters[currentIndex].ToData().id;
            LoadSceneAction(SceneNames.InGame);
        }
    }
}
