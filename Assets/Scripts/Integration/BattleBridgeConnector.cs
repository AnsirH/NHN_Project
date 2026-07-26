// 아웃게임 ↔ 인게임 연결 어댑터 (활성화본). 변환 규칙은 BattleSetupConverter(순수 함수)에 있고,
// 이 클래스는 씬 수준의 배선만 담당한다: 핸드오프 수신 → 전투 실행 → 결과 반환 → 복귀.
//
// 아웃게임이 업그레이드·증강 배율을 모두 곱한 **최종 스탯**을 직접 넘기므로(계약 §7.1, 2026-07-26 확정)
// 인게임은 ArmyDefinition/ArmyStatCalculator를 몰라도 되고 Resources 조회도 필요 없다.
// 씬 전환은 §7.4 핸드오프: 아웃게임이 SetPendingBattle → 전투 씬 로드 →
// 이 스크립트가 ConsumePendingSetup → 전투 → CompleteBattle → 아웃게임 씬 복귀.
using NHN.Presentation.Battle;
using OutGame.Logic.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NHN.Integration
{
    /// <summary>
    /// 전투 씬(Battle.unity)의 진입점. 씬에 하나만 두고 battleRunner를 배선한다.
    /// 아웃게임 씬에서 넘겨준 BattleSetupData를 받아 전투를 실행하고, 끝나면 결과를 돌려주고 복귀한다.
    /// 대기 중인 데이터가 없으면(씬 단독 실행) 인스펙터 구성으로 그냥 전투가 돌아간다.
    /// </summary>
    public sealed class BattleBridgeConnector : MonoBehaviour
    {
        /// <summary>아웃게임 진입점 씬 이름 (전투 종료 후 복귀 대상).</summary>
        private const string OutGameSceneName = "InGame";

        [SerializeField] private BattleTestBootstrap battleRunner;

        private void Start()
        {
            BattleSetupData setup = BattleBridge.ConsumePendingSetup();
            if (setup == null)
            {
                Debug.Log("[연동] 대기 중인 BattleSetupData가 없다 — 씬 단독 실행 모드");
                return;
            }

            battleRunner.RunBattle(BattleSetupConverter.ToBattleRequest(setup), outcome =>
            {
                BattleBridge.CompleteBattle(BattleSetupConverter.ToResultData(setup.roomId, outcome));
                SceneManager.LoadScene(OutGameSceneName);
            });
        }
    }
}
