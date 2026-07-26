# 아웃게임(dev) 연동 가이드 — 인게임 선행 준비 완료 상태

> 갱신: 2026-07-26 (아웃게임 계약 §7 확정본 반영)
> 목적: dev 머지 시 **커넥터 파일 1개 활성화 + 전투 씬 배선**만으로 연결이 끝나도록 준비해둔 상태의 문서.
> 아웃게임 계약 원본은 `Docs/OutGame/상세 기획.md` §7 (머지 후 접근 가능).

---

## 1. 계약 요약 (아웃게임 확정, 인게임이 따름)

### 입력 `BattleSetupData`
```
roomId, roomType(NormalBattle|Boss), encounterId
armies[]: armyDefId, armyClass, equippedItemId, generalSkillId,
          soldierCount,   // 증원 반영 최종 병력
          upgradeLevel,   // 0~5
          slotId, slotX, slotY   // 진영 내 정규화 0~1
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
| `EncounterTable` (SO) | Assets/Data/EncounterTable.asset | encounterId → 적 구성 (적 구성 출처는 협의 중) |
| `BattleTestBootstrap.RunBattle(request, onFinished)` | Scripts/Presentation/Battle | 전투 1판 실행 + 결과 콜백 1회 |
| `RoleDefinition.WithStats(...)` | Scripts/Simulation/Battle | 인게임 속성(.asset) + 전달 스탯 결합 |
| `GeneralDefinition.WithSkillUpgrades(...)` | Scripts/Simulation/Battle | 스킬 강화 횟수 → 충전 필요량 감소 |
| 커넥터 템플릿 | 본 폴더 `BattleBridgeConnector.cs.txt` | 필드 복사 + 단위 환산 + 씬 핸드오프 어댑터 |

**인게임이 소유하는 것** (아웃게임이 모르는 값): 공격 주기·사거리·투사체 속도/궤적, 타겟팅(위치 필터·우선순위),
이동 패턴, 유닛 반경, 장군 능력(패시브·충전·액티브 4종), 전투 중 변동분(버프·상태이상·치명타 판정),
그리고 **3D 유닛 모델 매핑**(아웃게임 데이터에 없음 — `BattleCatalog`가 담당).

---

## 4. 머지 후 활성화 절차

1. `Assets/Scripts/Integration/` 생성 후 `BattleBridgeConnector.cs.txt` → `BattleBridgeConnector.cs`로 복사
2. 같은 폴더에 asmdef 생성: 이름 `NHN.Integration`, 참조 `NHN.Data`, `NHN.Presentation`, `OutGame.Logic`
   (`OutGame.ScriptableObjects`는 불필요 — 스탯을 직접 받으므로 에셋 조회를 하지 않는다)
3. **전투 씬 `Battle.unity` 생성** (BattleTest 기반) — `BattleBridgeConnector` 배치 + `battleRunner` 배선
4. `ProjectSettings/EditorBuildSettings`에 `Battle` 씬 추가 (아웃게임 3개 씬과 함께)
5. 아웃게임 쪽 `BattleBridge.Implementation`이 `SetPendingBattle` + `LoadScene("Battle")`을 호출하도록 확인
6. 아웃게임의 `DummyBattlePanel` 배선 제거(또는 비활성)

머지 시 충돌은 **프로젝트 설정 파일에만** 발생한다 (코드·콘텐츠는 완전 분리):
`.gitignore` / `ProjectSettings/*` / `Assets/Settings/*_RPAsset` / `URP.png`.
그중 **`EditorBuildSettings.asset`(씬 목록)만 실질적**이며 양쪽 씬을 합쳐야 한다.

---

## 5. 남은 협의 1건

- ~~증강 id 전달~~ → **해소**: 증강 배율이 최종 스탯에 이미 반영돼 오므로 id를 받을 필요가 없어졌다.
  스킬 강화만 `generalSkillUpgradeCount`로 별도 전달되며, 그 해석은 인게임이 정했다(위 §1).
- **적 구성(encounterId) 출처** — 인게임 `EncounterTable`(현재) vs 아웃게임 생성기(난이도 커브 티어) 중
   어느 쪽을 정본으로 할지. 아웃게임이 정본이 되면 적 군대도 `armyDefId`+`upgradeLevel` 형태로
   넘겨주면 되고, 인게임은 같은 재구성 경로를 그대로 쓴다.

## 6. 인게임 쪽 정리 이력

- **스탯 CSV 파이프라인 제거** (2026-07-26): 플레이어 부대 스탯 정본이 아웃게임 `ArmyDefinition`으로
  확정되면서 인게임 CSV는 중복 출처가 됐다. 관련 파일(임포터·익스포터·StatTable·CSV)을 삭제했다.
  `.asset` 값은 **적 구성·로컬 테스트·BalanceLab 기준선**으로 계속 쓰인다.
- **BalanceLab의 레벨 스윕 제거**: 배율 공식을 툴에서 재현하면 아웃게임과 갈라지므로,
  머지 후 `ArmyStatCalculator`를 링크해 되살린다.
