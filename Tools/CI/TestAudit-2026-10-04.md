# 테스트 의미 검토 및 정리 — 2026-10-04

성공/실패 숫자보다 실제 결함을 검출하는지를 기준으로 기존 테스트를 정리했다.
게임 로직과 프리팹은 변경하지 않았다. 사용자 에셋·Packages·GraphicsSettings 변경은 보존했다.

## 변경과 검증 목적

| 작업 | 최종 검증 내용 |
|---|---|
| 증강 시드 전제 교체 | 같은 시드의 재현성, 원본 풀 보존, 제어된 난수 입력이 선택 결과에 영향을 주는지 검증 |
| 수동 캡처 분리 | UICaptureTests의 11개 사례에 Explicit/VisualCapture 지정. 일반 회귀 실행에서 제외하고 필요할 때 개별 선택하여 이미지 검사 |
| 단순 getter 테스트 제거 | MapDefinition/PlayerCharacterDefinition의 Accessors_ReflectSerializedFields 제거. 데이터 변환·필수값 검증 유지 |
| 배치 자기 비교 제거 | 같은 헬퍼의 결과끼리 비교하던 테스트를 명시적인 군대 ID·슬롯·병사 수 기대값 검증으로 교체 |
| SO 기본값 검증 교체 | 기본값과 다른 군대 필드, 격자 크기, 병과 계수, 런 구성, 난이도 티어, 드롭 확률을 입력하고 전달값 확인 |
| 비용 배열 검증 | 사용자 지정 비용의 전달·독립 복사 검증. 실제 자산은 최대 레벨과 배열 길이 일치·비음수 비용 검증(0 비용은 현재 계약상 허용) |
| 이벤트 오류 고립 | ID만 비우고 선택지를 정상 구성. eventId 오류 메시지도 확인하여 다른 검증에서 발생한 예외로 통과하지 않게 함 |
| UI 테스트 갱신 | 실제 직렬화된 버튼/라벨 참조 및 TMP 사용. 이름·스탯·증원 수는 현재 자산과의 연결을 검사하고 순환 선택·ID 전달·골드 차감 유지 |
| 실제 크기 검증 | 레이아웃 갱신 후 rect의 양수 너비와 정사각형 여부 검사. sizeDelta의 0==0으로 통과하지 않게 함 |
| 폐기된 흐름 교체 | 의지의 파편을 거치는 전투 복귀 사례 두 개를 즉시 방 그래프 진입·런 소비 검증 하나로 통합 |
| CI 실패 허용 제거 | KnownPlayModeFailures/Signatures 제거. CLI exit 0과 완결된 실패 없는 XML 모두 요구 |

## 실행 결과

Unity CLI 1.0.0-beta.9, Unity 6000.5.3f1로 실행했다.

| 실행 | 결과 | 로컬 증거 |
|---|---|---|
| 수정 전 전체 PlayMode | 82 통과 / 33 실패 / 11 스킵 | Logs/test-audit-before.xml |
| 최종 CI 범위 EditMode | 373 통과 / 0 실패 | Logs/test-audit-edit-final.xml |
| 최종 전체 PlayMode | 114 통과 / 0 실패 / 11 수동 캡처 스킵 | Logs/test-audit-play-final.xml |
| CI 가드 RED | 기존 허용 실패 보고서를 수락하여 `Guard accepted previously allowed failing test` 발생 | checkpoint 1362047 |
| CI 가드 GREEN | 정상 보고서 수락, 기존 허용 실패·취소·미완결·suite 실패·새 실패·inconclusive 6개 보고서 거부 | Tools/CI/Test-CIGuards.ps1 |

실행 명령:

```powershell
$cli = "$env:LOCALAPPDATA/Unity/bin/unity.exe"
$filter = 'OutGame;ClayWars.Build.Tests;NHN.Simulation.Tests.BattleHeadlessTests;NHN.Simulation.Tests.BattleRequestTests;NHN.Simulation.Tests.CombatStatsTests;NHN.Simulation.Tests.GeneralHeadlessTests;NHN.Simulation.Tests.BattleSetupConverterTests'
& $cli test . --mode EditMode --filter $filter --report-format nunit --output Logs/test-audit-edit-final.xml --timeout 1200 --non-interactive
& $cli test . --mode PlayMode --report-format nunit --output Logs/test-audit-play-final.xml --timeout 1200 --non-interactive
& Tools/CI/Test-CIGuards.ps1
& Tools/CI/Test-PlayModeResults.ps1 -ResultsPath Logs/test-audit-play-final.xml
```

## 결함 검출 확인

게임 로직에 아래 결함을 한 번에 하나씩 임시 주입하고 해당 테스트를 Unity CLI로 실행했다.
각 테스트가 실제 실행되어 실패함을 확인한 후 finally로 원래 파일 바이트를 복원했다.
마지막 정상 EditMode/PlayMode 실행은 모든 복원 이후 수행했다.

| 임시 결함 | 검출 증거 |
|---|---|
| RNG를 무시하고 풀의 앞 세 항목 반환 | UsesRandomInputToSelectItems: 서로 같은 aug_0/1/2 결과라 실패 |
| soldierAttack 입력 무시, 기본값 5 반환 | MapsConfiguredFields: 기대 11, 실제 5 |
| eventId 검증 제거 | EmptyEventId_Throws: 기대 예외, 실제 예외 없음 |
| 비용 배열 Clone 제거 | MapsConfiguredValuesAndCopiesCosts: 기대 원본 11, 실제 999 |

결과 XML: Logs/test-audit-mutation-*.xml. 네 가지 표적 결함 검출이며 전체 mutation score나 코드 커버리지 측정은 아니다.

## 검토와 한계

독립 읽기 전용 리뷰에서 재현성만으로는 RNG 무시를 잡지 못한다는 피드백을 반영해
BoundaryRandom 테스트 대역으로 선택 영향 검증을 추가했다. 이후 C#/Unity/TMP 및 엄격한 CI 가드 리뷰에서 추가 결함은 없었다.

- 기존 BalanceLab의 Mono/.NET 저장 결과 비교 2개는 CI EditMode 범위에서 계속 제외된다. 이번 작업은 그 교차 런타임 계약을 수정하지 않았다.
- 수동 캡처는 이번에 실행하거나 시각 판정하지 않았다. Explicit 분리는 자동 품질 보증을 의미하지 않는다.
- 이번에는 Unity 테스트 컴파일·실행과 CI 가드 검증을 수행했다. WebGL 재빌드, 원격 Actions 실행, 원격 푸시는 수행하지 않았다.
- 이전 CI 구축 이력은 Tools/CI/VERIFICATION.md에 남아 있다. 그 문서의 33개 허용 실패 수치는 당시 기록이며 현재 정책은 모두 실패 차단이다.
