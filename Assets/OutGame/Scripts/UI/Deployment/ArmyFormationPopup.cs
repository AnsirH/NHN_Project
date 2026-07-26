using System;
using System.Collections.Generic;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Items;
using OutGame.Logic.Runs;
using OutGame.ScriptableObjects;
using OutGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame.UI.Deployment
{
    /// <summary>
    /// 방 그래프 화면에서 언제든 열어볼 수 있는 "내 진영" 팝업(2026-07-26 사용자 요청) — 전투 진입
    /// 전에만 가능했던 배치 변경/아이템 장착/군대 업그레이드를 맵 이동 중에도 상시 쓸 수 있게 한다.
    /// 적 진영/전투 시작 버튼은 없다 — AllyFormationView 하나를 감싸는 얇은 껍데기.
    /// 전체화면 dim이라 방 그래프 우측 상단의 재화 표시가 가려지므로, 팝업 자체 헤더에도 재화를
    /// 표시한다(사용자 확정 — 다른 재화 표시와 마찬가지로 CurrencyDisplay 재사용).
    /// </summary>
    public class ArmyFormationPopup : MonoBehaviour
    {
        [SerializeField] private AllyFormationView allyFormationView;
        [SerializeField] private CurrencyDisplay currencyDisplay;
        [SerializeField] private Button closeButton;

        private RunState run;

        /// <summary>AllyFormationView.Changed를 그대로 전달 — 호출부가 방 그래프의 재화 표시 등을
        /// 즉시 동기화하는 데 쓴다.</summary>
        public event Action Changed;

        private void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            allyFormationView.Changed += OnAllyFormationChanged;
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Hide);
            allyFormationView.Changed -= OnAllyFormationChanged;
        }

        private void ValidateWiring()
        {
            if (allyFormationView == null || currencyDisplay == null || closeButton == null)
                throw new InvalidOperationException("ArmyFormationPopup의 필드가 배선되지 않았습니다.");
        }

        public void Open(
            RunState runState,
            RunConfig runConfig,
            IReadOnlyList<ArmyDefinition> armyDefs,
            IReadOnlyList<ItemDefinition> itemDefs,
            IReadOnlyList<AugmentDefinition> augmentDefs)
        {
            if (runState == null) throw new ArgumentNullException(nameof(runState));
            ValidateWiring();
            run = runState;

            allyFormationView.Open(run, runConfig, armyDefs, itemDefs, augmentDefs);
            // AllyFormationView.Changed 구독(Awake)에만 기대지 않고 최초 1회는 직접 동기화한다 —
            // 이 팝업의 GameObject가 비활성 상태에서 Open()이 먼저 호출되면 Unity가 Awake()를
            // 활성화 시점까지 미뤄서, 첫 Open() 때는 구독이 아직 안 걸려있을 수 있다(이번 세션에
            // 이미 겪은 것과 동일한 Awake 타이밍 문제 — 업그레이드 버튼 라벨, CurrencyDisplay).
            currencyDisplay.SetAmount(run.gold);

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            allyFormationView.Close();
            gameObject.SetActive(false);
        }

        private void OnAllyFormationChanged()
        {
            currencyDisplay.SetAmount(run.gold);
            Changed?.Invoke();
        }
    }
}
