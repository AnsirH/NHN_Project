# 아웃게임(feature/outgame) 연동 가이드 — 인게임 선행 준비 완료 상태

> 목적: 머지 시 **커넥터 파일 1개 활성화 + 씬 배선**만으로 연결이 끝나도록 인게임 쪽 준비를 마쳐둔 상태의 문서화.
> outGame 계약(BattleBridge/BattleSetupData/BattleResultData)은 상세 기획 §7.1~7.3 참조.

## 계약 요약 (outGame 쪽, 변경 금지 합의 대상)

- 호출 지점: `BattleBridge.Implementation` = `Action<BattleSetupData, Action<BattleResultData>>` (정적 델리게이트, **씬 로드마다 재등록**)
- 입력 `BattleSetupData`: roomId / roomType(일반·보스) / **encounterId(적 구성 — 인게임 책임)** / armies[]
  - `DeployedArmy`: armyInstanceId, armyDefId, armyClass, equippedItemId, generalSkillId, soldierCount, slotId, **slotX/slotY(진영 내 정규화 0~1, 아군=좌측)**
- 출력 `BattleResultData`: roomId, victory(false → 런 종료), survivals[](armyInstanceId별 생존 병사 수 — 예약)

## 인게임 쪽 준비물 (이 브랜치에 이미 구현됨)

| 준비물 | 위치 | 역할 |
|---|---|---|
| `BattleRequest`/`BattleOutcome` | Scripts/Data/BattleRequest.cs | 계약과 1:1 대응하는 인게임 DTO (키는 인게임 어휘) |
| `BattleCatalog` (SO) | Assets/Data/BattleCatalog.asset | 문자열 키(에셋 이름) → RoleData/GeneralData 해석 + 정규화 슬롯→anchor 변환. 미등록 키는 노멀 폴백 |
| `EncounterTable` (SO) | Assets/Data/EncounterTable.asset | encounterId → 적 군대 구성 (encounter_basic / encounter_boss + 폴백) |
| `BattleTestBootstrap.RunBattle(request, onFinished)` | Scripts/Presentation/Battle | 전투 1판 실행 + 종료 시 결과 콜백 1회 (Restart 재실행 시 중복 보고 없음) |
| 커넥터 템플릿 | 본 폴더 BattleBridgeConnector.cs.txt | BattleBridge ↔ RunBattle 어댑터 (아래 절차로 활성화) |
| 스모크 테스트 | BattleTestBootstrap 컨텍스트 메뉴 "연동 경로 테스트" | 머지 전에도 RunBattle 경로를 플레이 모드에서 확인 가능 |

## 머지 후 활성화 절차

1. `Assets/Scripts/Integration/` 폴더 생성, `BattleBridgeConnector.cs.txt` → `BattleBridgeConnector.cs`로 복사.
2. 같은 폴더에 asmdef 생성: 이름 `NHN.Integration`, 참조 `NHN.Data`, `NHN.Presentation`, `OutGame.Logic`.
3. 전투가 실행될 씬에 `BattleBridgeConnector` 배치, `battleRunner`에 BattleTestBootstrap 배선.
   (전투 씬 방식 — 같은 씬 패널 vs 별도 씬 로드 — 은 협의 ④에 따라 결정)
4. 커넥터의 `MapClassToRoleId` 표를 enum 합의(협의 ①)대로 채운다.
5. outGame의 DummyBattlePanel 배선 제거(또는 비활성) — Implementation을 커넥터가 덮어쓰므로 호출부 수정은 불필요.

## 합의 완료 (2026-07-24 회의)

- **스탯 소유권 분리**: 체력/공격력/방어력/치명타확률/이동속도 **5스탯은 아웃게임이 계산해 전달**
  (병사 1명 기준 세트 + 장군 세트, 각각). 인게임은 공격 주기·사거리·투사체 속도/궤적·타겟팅·
  이동 패턴·유닛 반경·장군 능력(패시브/충전/액티브)·전투 중 변동분을 소유한다.
- **레벨 축은 하나 (0~5)**: 분대 레벨 = 장군 레벨 = 군대 강화 레벨. 병사 스탯도 이 레벨을 따른다.
  스탯이 계산돼서 오므로 인게임은 레벨 값 자체가 필요 없다.
- **모든 분대에 장군 1명** (빈 슬롯은 분대 자체가 없음). 병과는 **아이템**이 부여하며,
  병과가 장군 스킬을 결정한다 → GeneralData는 4종 + 노멀 1종이면 충분하고,
  같은 장군 에셋이 여러 분대에 동시에 쓰일 수 있다.
- **방어력 = 감쇠 공식** `피해 × K/(K+방어력)`, **치명타 확률 = 퍼센트 정수(0~100)**, **치명타 배율 1.8**.
  방어력은 일반 공격·스킬 즉발에 적용, 도트에는 미적용.
- **전투 진행 = 별도 전투 씬**(additive 로드 권장 — 아웃게임 씬이 파괴되면 결과 콜백이 죽는다).
- **아이템은 병과 부여 수단일 뿐** → 인게임은 `equippedItemId`를 사용하지 않는다.
- **전투력 계산기는 표기 전용** → 인게임 전투 결과와 정합 작업 불필요.

### 인게임 구현 상태 (5.6-A/B 완료)

- 방어력·치명타 시뮬 구현 완료 (BattleConfig의 `defenseK`/`critMultiplier`가 튜닝 손잡이)
- `SquadRequest`에 병사 5스탯 + 장군 5스탯 필드 존재. **`maxHp > 0`이면 전달값 사용**,
  없으면 .asset 값(로컬 테스트·BalanceLab 경로) — 두 경로가 같은 코드로 처리된다.
- 결합은 `RoleDefinition.WithStats(...)`가 담당: 인게임 속성은 .asset에서 승계, 5스탯만 교체.
  분대마다 별도 인스턴스라 같은 장군 에셋이 레벨이 다른 여러 분대에 쓰여도 간섭이 없다.

## 협의 목록 (머지 전 팀 합의 필요)

1. **ArmyClass enum ↔ 롤 4종 매핑** — 인게임 롤 기준으로 enum 개정(머지 시) 합의됨.
   `None|Warrior|Archer|Assassin|Hunter`로 정리하고 커넥터 표만 채우면 된다.
2. **`DeployedArmy`에 5스탯 2세트 필드 추가** (아웃게임 작업) — 병사 세트 + 장군 세트.
   커넥터 템플릿은 `soldierMaxHp`/`generalMaxHp` 등의 이름을 가정 중이니 확정 시 맞춘다.
3. **generalSkillId 키 규약** — 제안: GeneralData 에셋 이름
   (`WarriorGeneral`/`ArcherGeneral`/`AssassinGeneral`/`HunterGeneral`, 아이템 미부여 시 `NormalGeneral`).
4. **encounterId 키 목록** — 현재 인게임 제공: `encounter_basic`, `encounter_boss` (+미등록 시 basic 폴백).
   outGame RoomEncounterTable과 키 동기화.
5. **soldierCount 상한** — 인게임 전투 상한 = BattleConfig.maxUnits(600, 양군 + 장군 합계).

## 인게임 쪽 키 어휘 (현재 기준)

- roleId: `Warrior` `Archer` `Assassin` `Hunter` (비움/미등록 = 노멀 병사)
- generalId: `WarriorGeneral` `ArcherGeneral` `AssassinGeneral` `HunterGeneral` (비움 = 장군 없음)
- 슬롯 좌표: slotX 1=전선/0=후방, slotY 0.5=중앙 — 변환 파라미터는 BattleCatalog(deploymentDepth/HalfWidth)에서 튜닝
