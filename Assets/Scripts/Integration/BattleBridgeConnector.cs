// 아웃게임 ↔ 인게임 연결 어댑터 (활성화본). 변환 규칙은 BattleSetupConverter(순수 함수)에 있고,
// 이 클래스는 씬 수준의 배선만 담당한다: 핸드오프 수신 → 전투 실행 → 결과 반환 → 복귀.
//
// 아웃게임이 업그레이드·증강 배율을 모두 곱한 **최종 스탯**을 직접 넘기므로(계약 §7.1, 2026-07-26 확정)
// 인게임은 ArmyDefinition/ArmyStatCalculator를 몰라도 되고 Resources 조회도 필요 없다.
// 씬 전환은 §7.4 핸드오프: 아웃게임이 SetPendingBattle → 전투 씬 로드 →
// 이 스크립트가 ConsumePendingSetup → 전투 → CompleteBattle → 아웃게임 씬 복귀.
using NHN.Presentation.Battle;
using OutGame.Flow;
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
        /// <summary>
        /// 아웃게임 씬 이름 (복귀 대상 / additive 여부 판정 기준) — 문자열 하드코딩 대신 공유 상수를
        /// 참조한다. 2026-07-29 씬 통합으로 실제 이름이 InGame → OutGame으로 바뀐 것을 리터럴이
        /// 조용히 놓친 전례가 있다 (적 데이터 연동 가이드).
        /// </summary>
        private const string OutGameSceneName = SceneNames.OutGame;

        [SerializeField] private BattleTestBootstrap battleRunner;

        private void Start()
        {
            BattleSetupData setup = BattleBridge.ConsumePendingSetup();
            if (setup == null)
            {
                Debug.Log("[연동] 대기 중인 BattleSetupData가 없다 — 씬 단독 실행 모드");
                return;
            }

            Debug.Log($"[연동] 전투 시작 — room={setup.roomId} ({setup.roomType}), " +
                      $"encounter={setup.encounterId}, 아군 {setup.armies.Count}분대, " +
                      $"적 {setup.enemies.Count}분대 (아웃게임 확정 구성), " +
                      $"캐릭터={setup.playerCharacterId}, 스킬={setup.playerCharacterSkillId}");
            battleRunner.RunBattle(BattleSetupConverter.ToBattleRequest(setup), outcome =>
            {
                CompleteAndReturn(BattleSetupConverter.ToResultData(setup.roomId, outcome));
            });
        }

        /// <summary>
        /// 결과 반환과 복귀. 아웃게임 씬이 살아 있는지에 따라 두 경로로 갈린다:
        ///
        /// · **additive** (아웃게임 씬 유지): 결과 콜백이 유효하므로 그대로 전달하고 전투 씬만 내린다.
        /// · **씬 교체** (아웃게임 씬 파괴): 아웃게임의 결과 핸들러가 자기 씬 오브젝트(방 패널·팝업 등)를
        ///   참조하기 때문에 콜백을 그대로 호출하면 파괴된 참조 예외가 난다. 아웃게임 쪽에
        ///   "복귀 후 결과 소비" 처리가 생기기 전까지는 결과를 전달하지 않고 경고만 남긴다 —
        ///   전투 실행 자체를 검증하는 단계에서는 이 경로로도 충분하다.
        /// </summary>
        private void CompleteAndReturn(BattleResultData result)
        {
            Scene outGameScene = SceneManager.GetSceneByName(OutGameSceneName);
            if (outGameScene.IsValid() && outGameScene.isLoaded)
            {
                BattleBridge.CompleteBattle(result);
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }

            Debug.LogWarning(
                $"[연동] 전투 종료 (victory={result.victory}) — 아웃게임 씬('{OutGameSceneName}')이 이미 내려가 " +
                "결과를 전달하지 않았다. 씬 교체 방식으로 복귀까지 연결하려면 아웃게임 쪽에 " +
                "'복귀 후 결과 소비' 처리가 필요하다 (또는 전투 씬을 additive로 로드).");
        }
    }
}
