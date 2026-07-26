# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# 프로젝트: NAN 2026 게임잼 — 3D 군대 전투 관람 게임 (Unity, 모바일 APK)

기획의 단일 진실 원천은 docs/nan2026_design_doc.md 이다. 구현 판단이 문서와 충돌하면 멈추고 질문하라.

## 아키텍처 불변 조건 (위반 금지)

1. **롤별 클래스 금지.** WarriorController, ArcherController 같은 롤 전용 클래스를 만들지 않는다.
   모든 롤은 RoleData(ScriptableObject) 하나로 정의되고, Unit은 RoleData를 읽어 실행하는 얇은 실행기다.
   - 검증 기준: 새 롤 추가 시 .cs 파일 수정 0줄, RoleData 에셋 1개 생성만으로 동작해야 한다.

2. **로직/뷰 분리.** 전투 시뮬레이션(이동, 타겟팅, 데미지, 상태이상, 기믹, 승패 판정)은
   MonoBehaviour/UnityEngine 렌더링에 의존하지 않는 순수 C# 레이어(Simulation)로 작성한다.
   뷰 레이어(Presentation)는 시뮬 상태를 읽어 표현만 한다.
   - 검증 기준: 씬 없이 배치 모드(headless)로 전투 1판을 실행해 승자를 출력하는 테스트가 존재해야 한다.
   - 이유: AI 자동 밸런싱 파이프라인(수천 판 시뮬레이션)의 전제 조건.

3. **상속보다 조합.** Unit 상속 트리를 만들지 않는다. 능력 차이는 데이터와 컴포넌트 조합으로 표현한다.

4. **타겟팅은 2단계 데이터 정의.** 위치 필터(enum) → 우선순위(enum list). 하드코딩된 if(role == ...) 분기 금지.

5. **상태이상은 공용 시스템 1개.** 기절/중독/화상/빙결/표식은 StatusEffectSystem 하나가 처리한다.
   상태이상별 클래스를 만들지 않는다 (enum + 파라미터 데이터).

6. **트리거 기믹은 조건-효과 데이터.** TriggerCondition(enum + params) + Effect(enum + params)로 정의하고
   단일 GimmickRunner가 평가한다. 기믹별 특수 클래스는 데이터로 표현 불가능할 때만 최후 수단으로 허용하며, 그 경우 사유를 주석으로 남긴다.

## 성능 규칙 (모바일 타겟)

- 타겟 재탐색은 매 프레임 금지: 0.3~0.5초 주기 + 유닛별 시차(스태거링).
- 최근접 탐색은 공간 분할(그리드 해시) 사용. 전 유닛 O(n²) 순회 금지.
- Update 안에서 GetComponent / Find 계열 호출 금지. 참조는 초기화 시 캐싱.
- 유닛 생성/사망은 오브젝트 풀 사용. 전투 중 Instantiate/Destroy 금지.
- GC 할당 최소화: 시뮬 틱 루프 안에서 LINQ, 클로저, string 연결 금지.

## 코드 규약

- 폴더: Scripts/Simulation (순수 C#), Scripts/Presentation (뷰), Scripts/Data (SO 정의), Scripts/Infra (풀, 유틸)
- 싱글톤 금지. 씬 진입점(Bootstrap) 하나가 시스템들을 생성/주입한다.
- public 필드 대신 [SerializeField] private + 프로퍼티.
- 매직 넘버 금지: 밸런스 수치는 전부 SO/JSON 데이터에.

## 작업 방식

- 모든 작업은 구현 전에 계획(파일 목록, 클래스 책임, 데이터 구조)을 먼저 제시하고 승인받는다.
- 한 번에 하나의 단계만. 요청 범위 밖 기능을 선제적으로 추가하지 않는다.
- 각 단계 완료 시 위 불변 조건 1~6에 대한 자가 검증 결과를 보고한다.