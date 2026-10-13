# Unity 작업 지침

> 2026-10-13 — Codex용 `.agents/skills/`·`.codex/agents/` 폐기 시 회수한 내용.
> 이 프로젝트는 Claude로만 작업하며, Unity 자동화는 공식 `unity-cli`(unity@synced 플러그인)를 쓴다.
> 아래는 `unity-cli`에 **없는** 이 프로젝트 고유 지식만 남긴 것이다.

## 1. 코드 영역 경계

| 영역 | 경로 | 규칙 |
|---|---|---|
| 아웃게임 (담당) | `Assets/OutGame/` | 모든 아웃게임 코드는 이 루트 안에만. 다른 작업자 폴더와 충돌 방지 |
| 인게임 (타인 담당) | `Assets/Scripts/` | **직접 수정 금지.** 계약 변경이 필요하면 연동 가이드 문서로 전달 (예: `Docs/OutGame/적 데이터 연동 가이드.md`) |

어셈블리: `OutGame`, `OutGame.Logic`, `OutGame.ScriptableObjects`, `OutGame.Editor`, `OutGame.Tests.EditMode`, `OutGame.Tests.PlayMode`, `Tests.EditorBridge`

## 2. 테스트 실행 — `unity test`로 전환

구 `unity-test-runner` 스킬(플래그 파일 + `TestRunnerBridge` 폴링 + LockFile 감지)은 전부 불필요하다.

```bash
unity test . --mode EditMode  --output ./results/edit.xml
unity test . --mode PlayMode  --output ./results/play.xml
unity test . --filter "OutGame.Tests.EditMode.ArmyStatCalculatorTests"
unity test . --report-format nunit,junit --coverage --coverage-output ./coverage
```

종료 코드: `0` 전체 통과 / `8` 테스트 실패 / `6` 판정 실패(컴파일 에러·라이선스·크래시·타임아웃).
**CI는 6만 재시도하고 8은 절대 재시도하지 않을 것.** 구 스크립트는 둘을 exit 1로 뭉갰다.

### 예외 — PlayMode 캡처 테스트는 `unity test`로 못 돌린다

`UICaptureTests.cs`는 `if (Application.isBatchMode) Assert.Ignore(...)`로 시작한다. `unity test`는 배치모드가 자동이고 `-batchmode`는 예약 플래그라 끌 수 없어서, 캡처 테스트는 **무조건 Ignore된다.** 렌더 프레임이 필요한 캡처는 라이브 에디터 경로로 가야 한다:

```bash
unity status    # state "ready" 확인
unity command   # 이 에디터가 노출하는 커맨드 확인
unity command eval --caller plugin --skill unity-cli '<캡처 트리거 C#>'
```

## 3. 에디터 자동화 주의사항 (실제 사고 기록)

- **모달 다이얼로그를 절대 유발하지 말 것.** `SaveOpenScenes()`를 저장 경로 없는(Untitled) 씬에 호출하면 "다른 이름으로 저장" 네이티브 다이얼로그가 떠서 자동화가 영구 정지한다 (2026-07-18 실제 발생). 저장 경로가 있는 dirty 씬만 골라 `EditorSceneManager.SaveScene(scene)`.
- **어떤 셋업 스크립트가 파괴적인지 먼저 파악할 것.** `Assets/OutGame/Editor/`에서 `EditorSceneManager.NewScene`을 grep하면 바로 나온다 — 현재 재생성형은 `SceneSetupOutGame.cs`(`OutGame.unity` 전체 재생성)과 `SceneSetupM5UI.cs`(`MainMenu.unity`)이고, `SceneSetupM2/M3UI/M4UI/M7UI`는 프리펛만 만들어 재실행이 안전하다(`SceneSetupLoading.cs`는 `File.Exists` 가드로 이미 있으면 건너뜀).
- **`SceneSetupOutGame.Run()` 재실행은 뒤에 붙인 추가 배선을 전부 지운다.** `AddSettingsPopup`(우상단 환경설정 버튼+팝업), `AddPausePopup`(ESC/뒤로가기 팝업), `WireLoadingOverlayIntoOutGameScene`(로딩 오버레이)는 모두 기존 씬에 "추가"만 하는 스크립트이고 `SceneSetupOutGame`은 이들을 만들지 않는다 — 재생성했다면 위 세 스크립트를 다시 실행해 복구하고, `OutGameFlowController`의 참조 필드가 `{fileID: 0}`으로 남지 않았는지 확인한다.
- 이 위험은 이미 한 번 현실화됐다 — 2026-07-19, 당시 씬을 전체 재생성하던 `SceneSetupM2.cs`를 에셋 추가 때문에 재실행했다가 다른 스크립트가 배선해둔 필드 참조가 전부 null로 리셋됐고, PlayMode 테스트가 DOTween "CanvasGroup has been destroyed"로 무더기 실패해서 발견됐다(그 후 `SceneSetupM2.cs`는 프리펛 전용으로 바뀜).
- `EditorSceneManager.NewScene(...)` 직후엔 이전 에셋 참조가 끊길 수 있다 — `AssetDatabase.LoadAssetAtPath`로 재로드 후 할당.
- `SerializedProperty` 이름은 private 포함 C# 필드명과 정확히 일치해야 한다. 저장은 `ApplyModifiedPropertiesWithoutUndo()` → `AssetDatabase.SaveAssets()` → `AssetDatabase.Refresh()`.
- 일회성 씬 설정 스크립트는 `Assets/OutGame/Editor/SceneSetupXxx.cs`에 두고, 실행 후 삭제하거나 격리한다.

## 4. UI 제작 원칙

1. **코드는 배치만, 비주얼은 프리팹.** 반복 요소마다 프리팹 1개. 코드는 개수·위치·상태만 정하고 크기·이미지·색·폰트·계층은 프리팹 인스펙터에 남긴다 — 비개발 작업자가 코드를 열지 않고 튜닝할 수 있어야 한다.
2. **모든 비주얼 값은 토큰에서.** 색·크기·간격을 흩뿌리지 말고 테마 ScriptableObject 또는 프리팹 직렬화 필드로 모은다.
3. **테스트 전체 통과는 화면 정상을 뜻하지 않는다.** 검증 사례: LayoutGroup `childControl` 미설정으로 20px 지정 요소가 90px로 출력됐는데 기능 테스트 7개는 전부 통과했다.

### 정적 화면 vs 런타임 반복 요소 — 먼저 구분

| 구분 | 예 | 방식 |
|---|---|---|
| 정적 화면/골격 | 메인 메뉴, 배치 화면 진영 영역, 팝업 창 | 에디터 스크립트가 **편집 시점 1회** 실행해 완성 프리팹 생성. 런타임 코드 생성 없음 |
| 데이터 반복 요소 | 맵 노드, 배치 슬롯, 부대 카드 | 요소 프리팹을 런타임 `Instantiate`. 코드는 개수·위치만 |

## 5. uGUI 함정 목록 (실전 검증분)

1. **`LayoutGroup.childControlWidth/Height` 기본값 주의** — false면 자식 `LayoutElement` 선호 크기가 무시되고 RectTransform 원래 크기(기본 100×100)가 쓰인다. `LayoutElement`를 쓰면 `childControl`을 반드시 켤 것.
2. **ScrollRect 위치 지정은 레이아웃 확정 후** — `Canvas.ForceUpdateCanvases()` 호출 뒤에 `verticalNormalizedPosition` 설정.
3. **레거시 Text는 개발용 placeholder까지만** — 출시 UI는 TMP + 한글 SDF 폰트(Noto Sans KR 등). 플레이스홀더 폰트는 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` (Arial.ttf는 삭제됨).
4. **재부모화(`SetParent`)된 반복 요소는 앵커를 명시하라** — 프리팹 루트에 `anchorMin/Max`를 명시하지 않으면 Unity 기본값(부모 좌하단 점 앵커)이 적용돼, LayoutGroup이 아닌 컨테이너에 재배치될 때 옆 요소를 침범한다 (실제: 배치 슬롯 카드가 옆 칸 침범).
5. **`RectTransform`을 만들 때마다 `anchorMin`/`anchorMax`를 빠짐없이 명시하라** — 생략하면 앵커가 정의되지 않은 상태다. LayoutGroup이 살아있는 동안은 매 프레임 덮어써서 티가 안 나지만, 그 LayoutGroup을 치우면 조용히 깨진다 (실제: 확인 모달 체크박스 토글에 앵커가 아예 없었음).
6. **정적 화면에서 LayoutGroup을 "굽고(bake) 지우는" 방식은 쓰지 말 것.**

   `LayoutRebuilder.ForceRebuildLayoutImmediate`로 값을 얼린 뒤 LayoutGroup/LayoutElement를 지우는 시도는 실패한다 — Unity는 그 컴포넌트가 제어하는 자식을 항상 **점 앵커(`anchorMin==anchorMax`) + 고정 픽셀 크기**로만 배치하므로 구운 값도 점 앵커다. 부모를 에디터에서 리사이즈해도 자식이 따라오지 않는 "드래그는 되는" 가짜 반응형이 된다. 게다가 굽는 시점에 대상이 Canvas 없는 loose GameObject 트리에 있으면 rect가 불확정이라, 임시 Canvas로 우회해도 중첩 LayoutGroup이 서로 겹치게 잘못 구워지는 회귀가 실측됐다 (2026-07-19, ArmyInfoPopup·ArmyDeploymentPanel 두 번 모두).

   **해결책 — 처음부터 앵커를 직접 계산해 명시한다.** 컨테이너 픽셀 크기를 아는 상태에서 자식 앵커 구간을 계산하는 순수 함수 헬퍼를 만들고 LayoutGroup을 아예 쓰지 않는다:
   - 세로 스택(고정 높이 / flex 비중 혼합): `ComputeVerticalStackYFractions(containerHeight, padding, spacing, rows[])` — `rows`는 `Fixed(px)`/`Flex(weight)`
   - 가로 가운데 정렬 고정폭 아이템: `ComputeCenteredHorizontalStackXFractions(containerWidth, spacing, itemWidths[])`
   - 적용 헬퍼 하나(`BuildStackChild`) — 한 축은 계산된 `(min,max)`를 앵커로, 다른 축은 고정 픽셀 인셋 스트레치

   검증: 프리팹 YAML에서 `anchorMin`/`anchorMax`가 실제로 다른 값인지 확인 (예: `{x:0,y:0.51}` ~ `{x:1,y:0.945}`. 같으면 아직 점 앵커).

   **단, 런타임에 코드가 `Instantiate`로 자식 수를 바꾸는 컨테이너**(배치 슬롯 그리드, 인벤토리 그리드 등)**의 LayoutGroup은 살아있어야 한다** — 지우면 게임이 깨진다. 새 UI마다 "이 LayoutGroup의 자식 수가 생성 시점 고정인가, 런타임에 변하는가"부터 확인.

### 웹에서 uGUI로 매핑

| 웹 개념 | uGUI |
|---|---|
| CSS 변수 (design token) | 테마 ScriptableObject + 프리팹 직렬화 필드 |
| flexbox | Horizontal/VerticalLayoutGroup (**`childControlWidth/Height = true` 필수**) + 자식 `LayoutElement`. 정적 화면이면 함정 6번에 따라 앵커 직접 계산 |
| `position: absolute` + inset | 앵커 (stretch = inset 0, 코너 고정 = `anchorMin==anchorMax`) + `anchoredPosition` |
| `width: fit-content` | `ContentSizeFitter`(PreferredSize) — LayoutGroup과 **같은 오브젝트에 중첩 금지**(부모까지만) |
| media query | `CanvasScaler`(ScaleWithScreenSize, 1920x1080, match 0.5) + 앵커 설계 + SafeArea |
| gap | `LayoutGroup.spacing` / 수동 배치는 스페이싱 상수 |
| z-index | 하이어라키 순서 (아래가 위에 그려짐). 팝업은 마지막 형제 |
| `:hover`/`:disabled` | Button transition + `SetState` 메서드에서 직렬화된 상태 색 사용 |

### 모바일(안드로이드, 가로 전용) 기본기

- `CanvasScaler`: ScaleWithScreenSize + **1920x1080 기준, match 0.5**
- 터치 타깃 **88px 이상** (1080p 기준, 약 1cm)
- 드래그 시작 임계값과 ScrollRect 방향을 분리

## 6. 캡처 검증 — 언제 쓰는가

비용이 있다(에디터 열림 필요 + PlayMode 진입 + 파일 IO). **의심될 때만** 쓰고 습관적으로 붙이지 않는다.

**쓸 때**: 레이아웃·배치·크기·겹침을 새로 만들었을 때 / 복잡한 앵커·LayoutGroup 조합이나 재부모화가 얽힌 프리팹 / 전에 시각 버그가 났던 영역 재수정(회귀 확인) / 사용자가 직접 요청할 때

**안 써도 될 때**: 컴포넌트 추가·필드 배선·순수 로직 변경 / 검증된 프리팹 구조에 값만 바꾸는 반복 작업 / EditMode 전용 변경

**절차**: 캡처 테스트 작성 → 라이브 에디터로 PlayMode 실행(2절 예외 참조) → 캡처 폴더의 `*.png`를 Read 툴로 판독 → 수정 → 반복(보통 2~3회 수렴). 캡처 테스트는 상태가 자주 바뀌는 화면이면 회귀 방지용으로 남긴다.

**함정**: Game 뷰 리페인트 지연으로 **직전 프레임이 재사용**될 수 있다 — 내용이 100% 동일한 캡처 파일 쌍이 나오면 버그 의심. 두 상태가 원래 같아야 하는지부터 판단할 것. 캡처 시 더미 데이터 금지 — **실데이터로 찍어야 실제 문제가 보인다**. 캡처 저장 폴더는 gitignore 대상(개인 검증 산출물).

## 7. Unity C# 리뷰 체크리스트

> ECC `csharp-reviewer` 에이전트는 .NET 일반 규약만 보고 아래 Unity 특수 항목은 커버하지 않는다.

**CRITICAL — 머지 전 수정**
- `new MyMonoBehaviour()` 금지 — `AddComponent` 또는 `Instantiate`
- 싱글톤 중복 인스턴스에서 `Destroy(gameObject)` 누락 금지
- `Awake` 실행 전 `Instance` 접근 금지 (초기화 순서)
- `OnDestroy`에서 정적 참조 정리 (`Instance = null`)
- `Update`에서 `FindObjectOfType`/`FindObjectsByType` 금지 — `Awake`/`Start`에 캐시
- `Update`에서 `GetComponent<T>()` 금지 — `Awake`에 캐시
- 문자열 기반 `Animator.SetBool("name")` 금지 — `Animator.StringToHash` 사용
- 핫 패스 프레임당 GC 할당 금지 (`Update`에서 LINQ·`new`·문자열 연결 금지)
- 물리 쿼리(`Raycast`, `OverlapSphere`)에 layerMask 지정
- `NavMeshAgent` 경로 쿼리는 `isOnNavMesh` 확인 후 사용

**HIGH — 같은 세션에 수정**
- 이벤트 구독(`+=`)에 대응하는 해제(`-=`)가 `OnDisable`/`OnDestroy`에 있는가
- 코루틴을 `Coroutine` 핸들로 보관하고 `OnDisable`/`OnDestroy`에서 정지하는가
- 자주 스폰되는 오브젝트(적·투사체)에 오브젝트 풀링 — 생 `Instantiate`/`Destroy` 금지
- 공유 config는 ScriptableObject로 (하드코딩 상수 금지)
- 인스펙터 노출 필드는 `public`보다 `[SerializeField] private`
- 비자명한 필드에 `[Header]`/`[Tooltip]`
- 옵셔널 싱글톤 참조에 널 조건 연산자 (`UIManager.Instance?.UpdateX()`)
- 컴포넌트 존재가 불확실하면 `GetComponent`보다 `TryGetComponent`

**MEDIUM — 기록만, 유예 가능**
- 매직 넘버를 명명 상수 또는 ScriptableObject 필드로
- `Debug.Log` 제거 또는 `#if UNITY_EDITOR` 가드
- `GamePhase` 등 enum의 큰 `switch`/`if-else` 체인을 전략 패턴 또는 메서드 테이블로
- 40줄 넘는 메서드 분할

**Unity 6 특이사항**
- `FindObjectsOfType<T>()`(deprecated) 대신 `FindObjectsByType<T>(FindObjectsSortMode.None)`
- 컴포넌트·에셋은 `GameObject.Destroy`가 아니라 `Object.Destroy`
- 새 입력 처리는 `Input.GetKey*`보다 InputSystem 패키지 (프로젝트가 혼용 중이면 명시)
