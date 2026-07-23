# AI 활용 로그 (제출 요건 — 기획 §13: 프롬프트·활용 내역 즉시 기록)

> 형식: 세션 단위. 도구 / 목적 / 주요 프롬프트 요지 / 산출물 / 검증 방법.
> 이전 세션(0~4단계)은 git 커밋 메시지와 대화 로그로 소급 정리 예정.

---

## 2026-07-23 — 5단계: 기획 v4 반영 (병사 기믹 삭제 + 장군 스킬 시스템)

- **도구**: Claude Code (Fable 5) + MCP for Unity (컴파일 확인·테스트 실행 자동화)
- **작업 방식**: 파트 A(문서) 완료 → 사용자 승인 → 파트 B(코드) 구현 → 헤드리스 테스트로 검증

### 프롬프트 요지 (사용자 → AI)
- 기획 v4 확정 변경점 3개를 제시하고 문서 갱신 + 모순 서술 일괄 수정 지시:
  1. 병사(일반 유닛) 트리거 기믹 전면 삭제 — 개성은 이동 패턴 + 타겟팅 + 스탯만
  2. 장군 스킬 확정: 패시브 1 + 액티브 1, "전투당 1회" 폐기 → 조건 충전식 (전투당 2~3회), 액티브 4종 확정
  3. 플레이어 스킬 4종 확정 (번개/독구름/힐 장판/전투 함성)
- 파트 B: 기믹 실행 경로 제거(시스템은 존치), SquadDefinition 리더 확장, GeneralData 정의,
  충전 시스템, 긍정 상태 효과 재사용, 장군 사망 규칙, TargetPriority.Leader, 헤드리스 테스트 3종

### AI 산출물
- 문서: nan2026_design_doc.md v3 → v4 (14개 섹션 일관성 수정)
- 시뮬 (순수 C#): BattleEnums(ChargeCondition/SquadPassive/부대 스코프 GimmickEffect/긍정 StatusEffectType),
  GeneralDefinition 신설, RoleDefinition 기믹 필드 제거, SquadDefinition 리더 확장,
  BattleSimulation 장군·충전·부대 스킬 4종·Leader 타겟팅·아군 스킬·회복/피해 배율 정산,
  StatusEffectSystem 긍정 케이스(HealOverTime/AttackUp/DamageResist)
- 데이터: GeneralData(SO) 신설, RoleData 기믹 필드 제거, SkillData targetsAllies 추가,
  에셋 6개 생성 — 확정 액티브 4종 장군 전원 (Warrior/Archer/Assassin/HunterGeneral) + 스킬 2종 (HealZone/WarCry)
- 뷰/씬 (에디터 확인용): SquadSetup 장군 슬롯 + 장군 뷰(금색·1.3배), 긍정 효과 틴트 4종,
  HUD 스킬 버튼 자동 복제(2×2), BattleTest 씬 전 분대 장군 배치 — "롤 부여 = 장군" 규칙(§5)에 맞춤
- 테스트: 신규 5개 + 기존 정리 → EditMode 14/14 통과

### AI 밸런싱 파이프라인 실측 (헤드리스 시뮬 — 시드 결정론)
- 장군 유/무 승률: 30판, 전사 20+장군 vs 전사 20 → 장군측 30승 (100%)
- 충전식 액티브 전투당 발동 (10판 평균/최대): 방진 2.1/3회, 일제 사격 1.9/2회,
  그림자 습격 0.3/3회, 사냥 선포 1.1/2회 → 4종 전부 "한 전투 2회 이상 발동 가능" 확인
- 관찰 (다음 밸런싱 입력): 분대 선두에 서는 장군은 적 최근접 타겟팅의 집중 포화를 받아
  HP 배율이 낮으면 액티브 1회 발동 전에 죽는다 → 기획 §5 "롤별 장군 스탯 차등"의 실측 근거.
  그림자 습격(누적 킬)은 킬 크레딧 조건이 까다로워 평균 발동이 낮음 — 필요 킬 수 튜닝 여지.

### 검증 방법
- MCP for Unity로 컴파일 에러 0 확인 → EditMode 테스트 14/14 통과 확인 (아래 명령 재현 가능)
- 아키텍처 자가 검증: 장군 1명 추가 = GeneralData 에셋 1개 (GeneralDataAsset_DrivesSimulation_WithoutCodeChanges 테스트로 강제)

---

## 2026-07-23 — 5.5단계: 아웃게임(feature/outgame) 연동 선행 준비

- **도구**: Claude Code (Fable 5) — outGame 브랜치를 머지 없이 git으로 조사(BattleBridge 계약 분석) 후 인게임 쪽 연결면 구현
- **결정**: 병과 키는 인게임 어휘(롤 4종 에셋 이름) 기준, outGame ArmyClass enum 매핑은 머지 시 커넥터 한 곳에서 조정

### AI 산출물
- 계약 대응 DTO: BattleRequest/BattleOutcome (outGame BattleSetupData/BattleResultData와 1:1)
- BattleCatalog(SO): 문자열 키→에셋 해석 + 정규화 슬롯(0~1)→anchor 변환, 미등록 키 노멀 폴백
- EncounterTable(SO): encounterId→적 구성 (encounter_basic/boss + 폴백) — 계약상 인게임 책임
- BattleTestBootstrap.RunBattle(request, onFinished): 연동 진입점 — 결과 콜백 1회 보장, Restart 중복 보고 방지
- 커넥터 템플릿 + 활성화 절차/협의 목록 문서: Assets/Docs/Integration/
- Normal 롤 에셋 (기획 §5 — 장군 없는 분대 = 노멀 병사)
- 검증 테스트 4종 (카탈로그 해석/슬롯 변환/테이블 폴백/요청→전투→결과 왕복)
