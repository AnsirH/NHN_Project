using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.Flow
{
    /// <summary>
    /// 캐릭터 선택 (§5.2.5): 맵 확정 직후 → 캐릭터 목록 표시(좌우 화살표/아이콘 클릭으로 미리보기
    /// 전환, 선택된 아이콘만 하이라이트) → 확인 시 RunState.selectedCharacterId를 채우고
    /// <see cref="CharacterConfirmed"/>를 발생시킨다. OutGame.unity 안의 한 패널(§3.1 씬 통합) —
    /// 현재 런은 <see cref="Begin"/>으로 전달받는다(정적 필드 아님). 캐릭터는 스킬을 정확히 하나만
    /// 가지며, 어떤 캐릭터를 골랐는지가 곧 인게임에서 쓸 스킬을 결정한다 — 스킬의 세부 효과는
    /// 정의하지 않는다(§4-2x, 인게임 스킬 시스템 책임).
    ///
    /// 아이콘 4개는 런타임 Instantiate가 아니라 화면 프리팹에 미리 배치돼 있다(SceneSetupM7UI,
    /// 2026-07-28 수정) — 개수가 고정된 정적 화면 요소라 매번 새로 만들 이유가 없다. 캐릭터가
    /// 5종 이상으로 늘어나면 프리팹의 아이콘 슬롯도 그만큼 늘려야 한다.
    ///
    /// 2026-08-08: CharacterSelectController → CharacterSelectPanel로 개명(Docs/OutGame/화면 명칭
    /// 정리.md) — 한 씬 안에서 배타적으로 토글되는 화면 콘텐츠는 전부 "Panel" 접미사로 통일(사용자 확정).
    ///
    /// 2026-08-09: 텍스트 4개를 UnityEngine.UI.Text → TMP_Text로 교체. 프리팹 쪽이 이미
    /// TextMeshProUGUI로 바뀌어 있어서 기존 타입으로는 배선 자체가 불가능했고, 이 패널은 부팅 시
    /// 꺼져 있다가 맵 확정 시점에 처음 SetActive(true)되므로 Awake()의 배선 가드가 그때서야 터졌다.
    /// </summary>
    public class CharacterSelectPanel : MonoBehaviour
    {
        [Header("표시 영역")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField] private TMP_Text skillDescriptionText;

        [Header("네비게이션")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;
        [SerializeField] private CharacterIconView[] iconViews;

        /// <summary>캐릭터가 확정되었을 때 발생 — OutGameFlowController가 구독해 방 그래프로 넘긴다.</summary>
        public event Action<RunState> CharacterConfirmed;

        /// <summary>[뒤로]를 눌렀을 때 발생 — OutGameFlowController가 구독해 맵 선택 패널로 되돌린다.
        /// 맵 선택과 달리 여기서는 씬을 벗어나지 않는다(같은 OutGame.unity 안의 형제 패널이라
        /// SceneLoadButton이 아니라 이벤트로 처리한다).</summary>
        public event Action BackRequested;

        private List<PlayerCharacterDefinition> characters;
        private int currentIndex;
        private RunState run;

        private void Awake()
        {
            if (portraitImage == null || nameText == null || descriptionText == null
                || skillNameText == null || skillDescriptionText == null || leftArrowButton == null
                || rightArrowButton == null || confirmButton == null || backButton == null
                || iconViews == null || iconViews.Length == 0)
                throw new InvalidOperationException("CharacterSelectPanel의 필드가 배선되지 않았습니다.");

            characters = ResourcePool.LoadAllOrThrow<PlayerCharacterDefinition>(
                    ResourcePaths.Characters, "PlayerCharacterDefinition을 찾을 수 없습니다 — SceneSetupM7Data.Run() 실행 필요 (§5.2.5)")
                .OrderBy(c => c.SortOrder).ToList();
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
            backButton.onClick.AddListener(OnBackClicked);

            // 미리보기 탐색(화살표/아이콘 클릭)은 Begin() 없이도 동작해야 하므로 초기 선택은
            // 여기서도 해둔다 — Begin()의 Select(0)은 재진입 시 리셋을 보장하기 위한 것으로,
            // 최초 1회에 한해 이 호출과 중복이지만 멱등이라 문제없다.
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

        /// <summary>OutGameFlowController가 이 패널을 활성화하기 직전에 호출 — 현재 런을 전달한다.
        /// Select(0)으로 선택 상태를 초기화한다 — 지금은 이 패널이 세션당 한 번만 열리지만, 나중에
        /// 재진입 경로가 생기면 이전 선택 인덱스가 새 런으로 새는 것을 막기 위한 방어(코드 리뷰).</summary>
        public void Begin(RunState run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            Select(0);
        }

        private void OnConfirmClicked()
        {
            if (run == null)
                throw new InvalidOperationException(
                    "CharacterSelectPanel.Begin(RunState)이 호출되지 않았습니다.");

            run.selectedCharacterId = characters[currentIndex].ToData().id;
            CharacterConfirmed?.Invoke(run);
        }

        /// <summary>맵 선택으로 되돌아간다 — 확정 전이라 이 런은 버린다. 맵을 다시 고르면
        /// MapSelectPanel이 새 RunState를 만들어 Begin()으로 넘겨준다. run을 비워두면 혹시라도
        /// Begin() 없이 이 패널이 열렸을 때 [시작하기]가 stale한 런을 확정하는 대신 명확히 실패한다.</summary>
        private void OnBackClicked()
        {
            run = null;
            BackRequested?.Invoke();
        }
    }
}
