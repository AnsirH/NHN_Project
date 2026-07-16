using NHN.Simulation.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NHN.Presentation.Battle
{
    /// <summary>
    /// 전투 결과 표시 + 재시작/스킬 버튼. 스킬 상태(쿨다운·무장)는 시뮬에서 읽어 표시만 한다 —
    /// 시전 로직은 부트스트랩(입력)과 시뮬(실행)에 있고 HUD는 뷰다.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        private static readonly Color CooldownColor = new Color(0.45f, 0.45f, 0.45f);

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

        public void Initialize(BattleTestBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            _buttonGraphics = new Graphic[skillButtons.Length];
            _skillColors = new Color[skillButtons.Length];
            _readyLabels = new string[skillButtons.Length];
            _shownCooldownTenths = new int[skillButtons.Length];
            for (int s = 0; s < skillButtons.Length; s++)
            {
                _buttonGraphics[s] = skillButtons[s].targetGraphic;
                _skillColors[s] = Color.white;
                _readyLabels[s] = skillLabels[s].text;
                _shownCooldownTenths[s] = int.MinValue;
            }
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
