# 아웃게임(dev) 연동 가이드 — 연동 완료 상태

> 갱신: 2026-07-30 (적 구성 계약 `enemies` 반영 — 연동 실동작 확인 완료)
> 아웃게임 계약 원본은 `Docs/OutGame/상세 기획.md` §7, 적 구성은 `Docs/OutGame/적 데이터 연동 가이드.md`.

---

## 1. 계약 요약 (아웃게임 확정, 인게임이 따름)

### 입력 `BattleSetupData`
```
roomId, roomType(NormalBattle|Boss), encounterId   // encounterId는 2026-07-29부터 참고용 — 조회 키 아님
armies[]: armyDefId, armyClass, equippedItemId, generalSkillId,
          soldierCount,   // 증원 반영 최종 병력
          upgradeLevel,   // 0~5
          slotId, slotX, slotY   // 진영 내 정규화 0~1
enemies[]: armyDefId, armyClass, soldierCount, upgradeLevel,   // 아웃게임 확정 적 구성 (2026-07-29 추가)
          general/soldier 최종 스탯, slotId, slotX, slotY      // 2026-08-02: 스탯·배치 좌표도 실려 온다
          // 아군(DeployedArmy)과 동일 원칙 — 커넥터가 재계산·자체 진형 없이 그대로 복사한다.
          // 배치 화면(EnemyFormationAssigner)에 보이는 위치·수치 = 실제 전투. roleId/generalId만
          // 병과에서 인게임이 매핑 (에셋 선택 — 인게임 소유). 상세: Docs/OutGame/적 데이터 연동 가이드.md
playerCharacterId, playerCharacterSkillId       // §5.2.5 — 캐릭터가 플레이어 스킬을 결정 (2026-07-30 연결)
          // skillId(가칭 skill_char_1~4) → 스킬 에셋 매핑은 BattleCatalog.playerSkillMap (SO 데이터):
          // char_1=번개, char_2=독구름, char_3=힐 장판, char_4=전투 함성. 해석되면 그 스킬 1종만
          // 사용 가능, 미전달·미등록이면 전체 4종 폴백(씬 단독 실행·BalanceLab·키 불일치 안전망)
```

**최종 스탯이 직접 전달된다** (2026-07-26 확정 — 계산 로직이 두 곳에 흩어지는 위험을 없애기 위해
아웃게임이 배치 확정 시점에 업그레이드+증강 배율을 모두 곱해 담아 보낸다):

```
generalHealth, generalAttack, generalDefense
generalCritRate, generalMoveSpeed    // 배율 대상 아님, 원본 그대로. 유닛도 이 값 공유 (§5.7)
soldierHealth, soldierAttack, soldierDefense
generalSkillUpgradeCount             // 장군 스킬 강화 증강 선택 횟수 — 해석은 인게임 몫
upgradeLevel                         // 참고용(UI 배지·3D 모델 매핑). 배율은 이미 반영됨
```
인게임은 `ArmyDefinition`/`ArmyStatCalculator`를 몰라도 되고 `Resources` 조회도 필요 없다 — 받은 값을 그대로 쓴다.

**`generalSkillUpgradeCount` 해석 (인게임 결정)**: **충전 필요량 감소 = 발동 빈도 증가**로 번역한다.
`유효 필요량 = max(기본 × (1 − 감소율 × 횟수), 기본 × 하한비율)`, 초기값 감소율 0.15 / 하한 0.4.
장군 액티브는 기획 §6에서 "사건(event)" 계층이라 체감을 지배하는 것이 발동 빈도이고, 스킬 4종에
일관 적용되는 단일 규칙이라 밸런싱도 단순하다. 하한을 둔 이유는 강화가 쌓여도 매 순간 터지는
소음이 되지 않게 하기 위함(이벤트 희소성). 수치는 `BattleConfig` 데이터라 코드 수정 없이 튜닝된다.

### 출력 `BattleResultData`
```
roomId, victory, survivals[]{ armyInstanceId, survivedSoldierCount }
```
1차에서 아웃게임은 `victory`만 소비하지만, 인게임은 `survivals`까지 채워서 돌려준다.

### 씬 전환 핸드오프 (§7.4)
아웃게임 씬과 전투 씬이 다르므로 `BattleBridge`의 static 홀더로 주고받는다 (파일로 굽지 않는다):

```
[아웃게임] SetPendingBattle(setup, onResult) → SceneManager.LoadScene("Battle")
[인게임]   ConsumePendingSetup() → 전투 → CompleteBattle(result) → LoadScene("InGame")
```

---

## 2. 병과 매핑 (어휘 일치 — 2026-07-26 확정)

| 아웃게임 `ArmyClass` | 아이템 | 인게임 롤 | 장군 스킬 |
|---|---|---|---|
| `Warrior` | 검+방패 | 전사 | 방진 |
| `Archer` | 활 | 궁수 | 일제 사격 |
| `Hunter` | 도끼 | 사냥꾼 | 사냥 선포 |
| `Assassin` | 단검 | 암살자 | 그림자 습격 |
| `None` | 없음 | 노멀 병사 | 없음 (노멀 장군) |

장군은 **병과에서 파생**한다(`<병과>General`) — 병과가 장군 스킬을 결정하므로 `generalSkillId` 키 규약에
의존하지 않는 편이 안전하다. 모든 분대에 장군 1명이 있다(빈 슬롯은 분대 자체가 없음).

---

## 3. 인게임 쪽 준비물 (이 브랜치에 구현 완료)

| 준비물 | 위치 | 역할 |
|---|---|---|
| `BattleRequest`/`BattleOutcome` | Scripts/Data/BattleRequest.cs | 계약 대응 인게임 DTO |
| `BattleCatalog` (SO) | Assets/Data/BattleCatalog.asset | roleId/generalId → 에셋 해석 (3D 모델 매핑도 이 자리) |
| `EncounterTable` (SO) | Assets/Data/EncounterTable.asset | 폴백 전용 — `enemies`가 비었을 때(씬 단독 실행·BalanceLab)만 사용 |
| `BattleTestBootstrap.RunBattle(request, onFinished)` | Scripts/Presentation/Battle | 전투 1판 실행 + 결과 콜백 1회 |
| `RoleDefinition.WithStats(...)` | Scripts/Simulation/Battle | 인게임 속성(.asset) + 전달 스탯 결합 |
| `GeneralDefinition.WithSkillUpgrades(...)` | Scripts/Simulation/Battle | 스킬 강화 횟수 → 충전 필요량 감소 |
| 커넥터 템플릿 | 본 폴더 `BattleBridgeConnector.cs.txt` | 필드 복사 + 단위 환산 + 씬 핸드오프 어댑터 |

**인게임이 소유하는 것** (아웃게임이 모르는 값): 공격 주기·사거리·투사체 속도/궤적, 타겟팅(위치 필터·우선순위),
이동 패턴, 유닛 반경, 장군 능력(패시브·충전·액티브 4종), 전투 중 변동분(버프·상태이상·치명타 판정),
그리고 **3D 유닛 모델 매핑**(아웃게임 데이터에 없음 — `BattleCatalog`가 담당).

---

## 4. 연결 상태 (2026-07-30)

**연동 완료 — 실동작 확인됨** (OutGame 씬 → 맵/캐릭터 선택 → 전투 방 → 배치 확정 → Battle 씬에서
아웃게임 확정 적 구성으로 전투 실행·종료까지 플레이 검증).

| 항목 | 상태 |
|---|---|
| `Assets/Scripts/Integration/` + `NHN.Integration` asmdef (참조: NHN.Simulation/Data/Presentation, OutGame.Logic) | ✅ |
| `BattleBridgeConnector` (핸드오프 수신 → 전투 → additive면 결과 반환, 씬 교체면 경고) | ✅ |
| `BattleSetupConverter` (계약 → 요청, 결과 → 계약, 적 구성 → 분대+진형) + 단위 테스트 | ✅ |
| 전투 씬 `Assets/Scenes/Battle.unity` — 커넥터 배선 완료, 단독 실행도 동작 | ✅ |
| 아웃게임 라우팅 (`BattleBridge.Implementation` → `SceneNames.Battle` 전환) — 동료 쪽에서 정식 채택 | ✅ |
| 적 구성: `enemies` 수신 → 병과별 진형 배치 → `.asset` 스탯으로 전투 구성 | ✅ |
| 씬 이름: 하드코딩 제거, 공유 상수 `SceneNames`(OutGame.Logic) 참조 | ✅ |

---

## 5. 협의 이력·남은 항목

- ~~증강 id 전달~~ → **해소**: 증강 배율이 최종 스탯에 이미 반영돼 온다. 스킬 강화만
  `generalSkillUpgradeCount`로 전달되며 해석(충전 필요량 감소)은 인게임이 정했다(위 §1).
- ~~적 구성(encounterId) 출처~~ → **해소 (2026-07-29)**: 아웃게임이 정본. 확정 구성이
  `enemies`(병과·병사 수)로 실려 오고, 스탯·배치는 인게임 책임으로 확정됐다
  (`Docs/OutGame/적 데이터 연동 가이드.md`). `EncounterTable`은 폴백 전용으로 강등.
- ~~전투 후 복귀~~ → **해소 (2026-07-30)**: 씬 교체 경로에 "복귀 후 결과 소비"를 구현했다.
  전투 진입 시 런은 `RunSessionContext`(이어하기 경로), 방 종류·적 구성은 컨트롤러 정적 필드에
  보관 → 커넥터가 결과를 `BattleBridge.SetPendingResult`에 담고 OutGame 씬 로드 → 복귀한
  `InGameFlowController.Begin`이 소비해 기존 `OnBattleResult` 경로를 그대로 탄다.
  **패배**: 세이브 삭제 → 패배 화면 → [완료] → 메인 메뉴. **승리**: 보상(골드·아이템 팝업) →
  맵 그래프 갱신 + 저장 → 런 계속 (보스 승리면 런 클리어 → 메인 메뉴). 양쪽 다 플레이 실측 확인.
- ~~스탯 스케일 조율~~ → **구조적 문제는 해소 (2026-08-02)**: 적도 아웃게임이 계산한 최종 스탯을
  실어 보내므로(EnemyArmy 계약 확장) 아군·적이 같은 스케일이 됐다. 인게임 `.asset` 스탯은 씬 단독
  실행·BalanceLab 폴백 전용. 개별 수치 밸런스는 §9대로 플레이 테스트로 계속 조율(아웃게임
  ArmyDefinition_army_* 에셋 소유).

## 6. 인게임 쪽 정리 이력

- **스탯 CSV 파이프라인 제거** (2026-07-26): 플레이어 부대 스탯 정본이 아웃게임 `ArmyDefinition`으로
  확정되면서 인게임 CSV는 중복 출처가 됐다. 관련 파일(임포터·익스포터·StatTable·CSV)을 삭제했다.
  `.asset` 값은 **적 구성·로컬 테스트·BalanceLab 기준선**으로 계속 쓰인다.
- **BalanceLab의 레벨 스윕 제거**: 배율 공식을 툴에서 재현하면 아웃게임과 갈라지므로,
  머지 후 `ArmyStatCalculator`를 링크해 되살린다.
