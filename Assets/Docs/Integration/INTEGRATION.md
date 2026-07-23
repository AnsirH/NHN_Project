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

## 협의 목록 (머지 전 팀 합의 필요)

1. **ArmyClass enum ↔ 롤 4종 매핑** — outGame: `None|Archer|Cavalry(+예약)` vs 인게임 확정 롤: `Warrior|Archer|Assassin|Hunter`. 합의 방향: 인게임 롤 기준으로 enum 개정(머지 시), 커넥터 표만 수정.
2. **generalSkillId 키 규약** — 제안: GeneralData 에셋 이름 (`WarriorGeneral`/`ArcherGeneral`/`AssassinGeneral`/`HunterGeneral`).
3. **encounterId 키 목록** — 현재 인게임 제공: `encounter_basic`, `encounter_boss` (+미등록 시 basic 폴백). outGame RoomEncounterTable과 키 동기화.
4. **전투 진행 방식** — 같은 씬 내 패널 전환 vs 별도 전투 씬 로드 (outGame InGameFlowController에 LoadSceneAction 훅 있음).
5. **soldierCount 상한** — 인게임 전투 상한 = BattleConfig.maxUnits(600, 양군 합계). 증원 보정 후 총합이 넘지 않도록 합의.

## 인게임 쪽 키 어휘 (현재 기준)

- roleId: `Warrior` `Archer` `Assassin` `Hunter` (비움/미등록 = 노멀 병사)
- generalId: `WarriorGeneral` `ArcherGeneral` `AssassinGeneral` `HunterGeneral` (비움 = 장군 없음)
- 슬롯 좌표: slotX 1=전선/0=후방, slotY 0.5=중앙 — 변환 파라미터는 BattleCatalog(deploymentDepth/HalfWidth)에서 튜닝
