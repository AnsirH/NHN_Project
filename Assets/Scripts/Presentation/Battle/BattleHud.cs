using NHN.Simulation.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NHN.Presentation.Battle
{
    /// <summary>
    /// 전투 결과 표시 + 재시작/스킬 버튼 + 시간 제어(일시정지·배속). 스킬 상태(쿨다운·무장)는
    /// 시뮬에서 읽어 표시만 한다 — 시전 로직은 부트스트랩(입력)과 시뮬(실행)에 있고 HUD는 뷰다.
    /// 시간 제어는 Time.timeScale만 만지는 순수 뷰 연출이라 시뮬 결정론에 영향이 없다.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        private static readonly Color CooldownColor = new Color(0.45f, 0.45f, 0.45f);

        /// <summary>클론 버튼 배치 간격 대체값 — 씬 버튼이 1개뿐일 때만 사용 (뷰 표현 상수).</summary>
        private static readonly Vector2 FallbackButtonStep = new Vector2(140f, 0f);

        /// <summary>재시작 버튼 왼쪽으로 시간 제어 버튼을 놓는 간격 — 오른쪽 스킬 버튼 간격과 대칭.</summary>
        private const float TimeButtonStepX = 250f;
        /// <summary>배속 토글 상한 (2026-08-05 사용자 결정: 2배까지만).</summary>
        private const float FastSpeedMultiplier = 2f;

        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button[] skillButtons;
        [SerializeField] private TMP_Text[] skillLabels;

        private BattleTestBootstrap _bootstrap;
        private Graphic[] _buttonGraphics;
        private Color[] _skillColors;
        /// <summary>씬에서 지은 버튼 라벨 — 쿨다운이 끝나면 이 이름으로 복원한다.</summary>
        private string[] _readyLabels;
        /// <summary>표시 중인 쿨다운(0.1초 단위) 캐시 — 값이 바뀐 프레임에만 텍스트를 재할당한다.</summary>
        private int[] _shownCooldownTenths;

        // 시간 제어 (일시정지·배속) — 씬 수정 없이 재시작 버튼을 복제해 만든다 (스킬 버튼 확장과 같은 방식).
        private Button _restartButton;
        private Button _pauseButton;
        private TMP_Text _pauseLabel;
        private TMP_Text _speedLabel;
        private bool _paused;
        private float _speedMultiplier = 1f;

        public void Initialize(BattleTestBootstrap bootstrap, SkillDefinition[] skills)
        {
            _bootstrap = bootstrap;
            EnsureButtonCount(skills);
            _buttonGraphics = new Graphic[skillButtons.Length];
            _skillColors = new Color[skillButtons.Length];
            _readyLabels = new string[skillButtons.Length];
            _shownCooldownTenths = new int[skillButtons.Length];
            for (int s = 0; s < skillButtons.Length; s++)
            {
                // 드래그 시전용 눌림 알림 — onClick은 릴리즈에만 발화해서 별도 훅이 필요하다
                // (모바일 UX: 버튼을 누른 채 전장으로 끌어와 놓으면 즉시 시전, 2026-08-04).
                if (skillButtons[s].GetComponent<EventTrigger>() == null)
                {
                    var trigger = skillButtons[s].gameObject.AddComponent<EventTrigger>();
                    var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                    int slot = s;
                    entry.callback.AddListener(_ => _bootstrap.OnSkillButtonPressed(slot));
                    trigger.triggers.Add(entry);
                }

                _buttonGraphics[s] = skillButtons[s].targetGraphic;
                _skillColors[s] = Color.white;
                // 라벨은 스킬 데이터가 정본 — 씬 텍스트는 자리표시자다. 캐릭터 선택으로 스킬이
                // 1종으로 제한되면 같은 버튼(슬롯 0)에 다른 스킬이 올 수 있어 매번 다시 쓴다.
                if (s < skills.Length)
                {
                    skillLabels[s].text = skills[s].SkillName;
                }
                _readyLabels[s] = skillLabels[s].text;
                _shownCooldownTenths[s] = int.MinValue;
            }
            EnsureTimeControlButtons();
        }

        /// <summary>
        /// 일시정지·배속 버튼을 재시작 버튼 복제로 만든다 (초기화 1회 경로 — Instantiate 허용).
        /// 씬의 재시작 버튼은 이름으로 찾는다 — 인스펙터 배선 없이 동작해 씬 수정을 피한다
        /// (Canvas 직계 자식 "Button_Restart", 이 컴포넌트가 Canvas에 붙어 있다).
        /// </summary>
        private void EnsureTimeControlButtons()
        {
            if (_pauseButton != null)
            {
                return;
            }
            Transform restart = transform.Find("Button_Restart");
            if (restart == null)
            {
                return; // 씬에 재시작 버튼이 없으면 시간 제어도 생략 (테스트 씬 전용 방어)
            }
            _restartButton = restart.GetComponent<Button>();
            _pauseButton = CreateTimeButton("Button_Pause", -TimeButtonStepX, OnPauseButton, out _pauseLabel);
            CreateTimeButton("Button_Speed", -TimeButtonStepX * 2f, OnSpeedButton, out _speedLabel);
            SetPaused(false);
            SetSpeed(1f);
        }

        private Button CreateTimeButton(
            string buttonName, float offsetX, UnityEngine.Events.UnityAction onClick, out TMP_Text label)
        {
            var restartRect = (RectTransform)_restartButton.transform;
            Button clone = Instantiate(_restartButton, _restartButton.transform.parent);
            clone.name = buttonName;
            clone.gameObject.SetActive(true); // 재시작 버튼이 숨겨진 상태에서 복제돼도 시간 제어는 항상 보인다
            ((RectTransform)clone.transform).anchoredPosition =
                restartRect.anchoredPosition + new Vector2(offsetX, 0f);
            clone.onClick = new Button.ButtonClickedEvent(); // 원본이 물고 온 씬 리스너(재시작) 제거
            clone.onClick.AddListener(onClick);
            label = clone.GetComponentInChildren<TMP_Text>();
            return clone;
        }

        /// <summary>
        /// 씬 버튼(기본 2개)보다 스킬이 많으면 첫 버튼을 복제해 슬롯을 확장한다 —
        /// 스킬 추가 = 데이터 1개 원칙을 씬 수정 없이 유지 (v4 §9: 4종 대응).
        /// 초기화 1회 경로라 Instantiate/클로저 허용 (전투 중 할당 아님).
        /// </summary>
        private void EnsureButtonCount(SkillDefinition[] skills)
        {
            if (skills.Length <= skillButtons.Length)
            {
                return;
            }
            var buttons = new Button[skills.Length];
            var labels = new TMP_Text[skills.Length];
            for (int s = 0; s < skillButtons.Length; s++)
            {
                buttons[s] = skillButtons[s];
                labels[s] = skillLabels[s];
            }

            var firstRect = (RectTransform)skillButtons[0].transform;
            Vector2 step = skillButtons.Length >= 2
                ? ((RectTransform)skillButtons[1].transform).anchoredPosition - firstRect.anchoredPosition
                : FallbackButtonStep;
            // 클론은 원본 줄 위로 쌓는다 — 화면 가장자리 밖으로 밀려나지 않게 (하단 바가 우측 정렬이어도 안전).
            Vector2 rowOffset = new Vector2(0f, firstRect.sizeDelta.y * 1.15f);
            int originalCount = skillButtons.Length;
            for (int s = originalCount; s < skills.Length; s++)
            {
                int column = (s - originalCount) % Mathf.Max(originalCount, 1);
                int row = 1 + (s - originalCount) / Mathf.Max(originalCount, 1);
                Button clone = Instantiate(skillButtons[0], skillButtons[0].transform.parent);
                clone.name = $"SkillButton{s}";
                ((RectTransform)clone.transform).anchoredPosition =
                    firstRect.anchoredPosition + step * column + rowOffset * row;
                clone.onClick = new Button.ButtonClickedEvent(); // 원본이 물고 온 씬 리스너(슬롯 0 고정) 제거
                int slot = s;
                clone.onClick.AddListener(() => OnSkillButton(slot));
                buttons[s] = clone;
                labels[s] = clone.GetComponentInChildren<TMP_Text>();
                labels[s].text = skills[s].SkillName;
            }
            skillButtons = buttons;
            skillLabels = labels;
        }

        /// <summary>슬롯별 스킬 색 — 버튼 색 = 이펙트 색 (가독성 1:1 대응).</summary>
        public void SetSkillColor(int slot, Color color)
        {
            if (slot >= 0 && slot < _skillColors.Length)
            {
                _skillColors[slot] = color;
            }
        }

        public void Clear()
        {
            resultText.text = string.Empty;
            for (int s = 0; s < _shownCooldownTenths.Length; s++)
            {
                _shownCooldownTenths[s] = int.MinValue;
            }
            // 전투 시작은 항상 기본 시간(1배속·재생 중)에서 — 이전 판의 정지/배속이 새어들지 않게.
            SetPaused(false);
            SetSpeed(1f);
        }

        /// <summary>
        /// 재시작 버튼 노출 — 테스트 실행(인스펙터 구성)에서만 보인다. 실전(아웃게임 연동)은
        /// 종료 후 복귀 흐름이 있어 재시작이 흐름을 깨뜨린다 (2026-08-05 사용자 결정).
        /// </summary>
        public void SetRestartVisible(bool visible)
        {
            if (_restartButton != null && _restartButton.gameObject.activeSelf != visible)
            {
                _restartButton.gameObject.SetActive(visible);
            }
        }

        private void OnPauseButton()
        {
            SetPaused(!_paused);
        }

        private void OnSpeedButton()
        {
            SetSpeed(_speedMultiplier == 1f ? FastSpeedMultiplier : 1f);
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : _speedMultiplier;
            if (_pauseLabel != null)
            {
                _pauseLabel.text = paused ? "재개" : "일시정지";
            }
        }

        private void SetSpeed(float multiplier)
        {
            _speedMultiplier = multiplier;
            if (!_paused)
            {
                Time.timeScale = multiplier;
            }
            if (_speedLabel != null)
            {
                _speedLabel.text = multiplier == 1f ? "x1" : "x2";
            }
        }

        /// <summary>씬 전환(전투 종료 복귀) 시 시간 배율 복원 — 아웃게임이 0배속/2배속을 물려받지 않게.</summary>
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        public void ShowResult(string message)
        {
            resultText.text = message;
        }

        /// <summary>씬의 Restart 버튼 OnClick(persistent listener)에서 호출된다.</summary>
        public void OnRestartButton()
        {
            _bootstrap.StartBattle();
        }

        /// <summary>씬의 스킬 버튼 OnClick(persistent listener, int 인자 = 슬롯)에서 호출된다.</summary>
        public void OnSkillButton(int slot)
        {
            _bootstrap.ToggleArmSkill(slot);
        }

        /// <summary>매 프레임 시뮬 스킬 상태를 버튼에 반영한다 (쿨다운 숫자·무장 하이라이트).</summary>
        public void SyncSkills(BattleSimulation sim, int armedSlot)
        {
            for (int s = 0; s < skillButtons.Length; s++)
            {
                if (s >= sim.SkillCount)
                {
                    if (skillButtons[s].gameObject.activeSelf)
                    {
                        skillButtons[s].gameObject.SetActive(false);
                    }
                    continue;
                }
                if (!skillButtons[s].gameObject.activeSelf)
                {
                    skillButtons[s].gameObject.SetActive(true);
                }

                float cooldown = sim.GetSkillCooldownRemaining(s);
                bool ready = cooldown <= 0f;
                skillButtons[s].interactable = ready;

                int tenths = ready ? -1 : (int)(cooldown * 10f) + 1;
                if (tenths != _shownCooldownTenths[s])
                {
                    _shownCooldownTenths[s] = tenths;
                    skillLabels[s].text = ready ? _readyLabels[s] : (tenths * 0.1f).ToString("F1");
                }

                _buttonGraphics[s].color = !ready ? CooldownColor
                    : s == armedSlot ? Color.Lerp(_skillColors[s], Color.white, 0.6f)
                    : _skillColors[s];
            }
        }
    }
}
