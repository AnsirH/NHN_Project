# 인게임 → 아웃게임 요청서 (2026-07-26 갱신)

> 이전 요청서(스탯 8필드 추가)는 **철회**합니다. §7.1.1의 "인게임이 `ArmyStatCalculator`로 직접 재구성" 방식이
> 더 안전하다고 판단해 그대로 따릅니다 — 화면 수치와 어긋날 여지가 없어지니까요.
> 아래는 남은 3건입니다. **①만 코드 작업이고, ②③은 통보/확인입니다.**

---

## ✅ 요청 ① (코드 작업) — 증강 id 전달 필요합니다

증강이 `ArmyStatCalculator.GetStatMultiplier(...)`의 `selectedAugments`로 들어가는 구조라,
현재처럼 빈 목록을 넘기면 **증강 효과가 전투에 반영되지 않습니다.** 배치 화면에 표시되는 전투력·스탯과
실제 전투 결과가 어긋나게 되니, §7.1.1에 적어두신 대로 계약에 추가 부탁드립니다.

```csharp
// Assets/OutGame/Scripts/Logic/Battle/BattleSetupData.cs

[Serializable]
public class BattleSetupData
{
    public string roomId;
    public RoomType roomType;
    public string encounterId;
    public List<DeployedArmy> armies = new List<DeployedArmy>();

    // ─────────────────────────────────────────────────────────────
    // ▼ 추가 요청: 이번 런에서 선택한 증강 id 목록
    //
    //  · RunState.selectedAugmentIds를 그대로 넘겨주시면 됩니다.
    //  · 인게임은 이 id로 AugmentDefinition을 조회해 ArmyStatCalculator에 그대로 전달합니다
    //    (배율 계산은 그 헬퍼 하나만 씁니다 — 재구현하지 않습니다).
    //  · 증강이 없는 런이면 빈 리스트여도 됩니다.
    // ─────────────────────────────────────────────────────────────
    public List<string> selectedAugmentIds = new List<string>();
}
```

인게임 커넥터는 이미 이 자리를 비워두고 대기 중이라, 필드만 오면 한 줄 연결로 끝납니다.

---

## ✅ 요청 ② (통보) — 전투 씬 이름은 **`Battle`** 입니다

아웃게임 쪽 로딩 호출부에 이 이름을 넣어주세요.

```csharp
BattleBridge.Implementation = (setup, onResult) =>
{
    BattleBridge.SetPendingBattle(setup, onResult);
    SceneManager.LoadScene("Battle");
};
```

전투가 끝나면 인게임이 `BattleBridge.CompleteBattle(result)`를 호출한 뒤 `SceneManager.LoadScene("InGame")`으로
아웃게임 씬으로 복귀합니다. 복귀 씬 이름이 `InGame`이 맞는지만 확인 부탁드립니다.

머지 시 `ProjectSettings/EditorBuildSettings.asset`에서 씬 목록이 충돌합니다 —
아웃게임 3개(MainMenu/MapSelect/InGame) + 인게임 전투 씬을 **합치면** 됩니다. 인게임 쪽에서 정리하겠습니다.

---

## ✅ 요청 ③ (확인) — 3D 모델 매핑은 인게임이 관리합니다

`armyClass`/`armyDefId` → 3D 유닛 모델 매핑은 인게임 쪽 `BattleCatalog`가 이미 그 역할을 하고 있어
**추가 작업 없습니다.** 병과가 정해지면 인게임이 알아서 해당 롤의 외형·색·크기로 스폰합니다.

---

## 📌 이제 인게임이 맞춰서 처리하는 것 (아웃게임 작업 없음)

- **최종 스탯 재구성**: `armyDefId` → `ArmyData` 원형 조회 → `ArmyStatCalculator` 배율 곱 (§7.1.1 그대로)
- **이동속도 단위 환산**: 아웃게임 단위 100 → 인게임 기본 근접 속도에 대응하도록 변환
- **치명타·이동속도를 유닛에 공유**: 장군 값을 병사도 그대로 사용 (§5.7)
- **방어력 감쇠 계수 K**: 인게임 공식 `피해 × K/(K+방어력)`을 아웃게임 스탯 스케일에 맞춰 조정
  - 하나만 참고로 알려주시면 좋습니다: 방어력이 **만렙(+5)·증강 포함 대략 어디까지 올라갈 예정**인지.
    현재 원형값(장군 5 / 병사 2) 기준이면 감쇠가 거의 0에 가까워서, 그 상한을 알면 K를 맞추기 쉽습니다.
- **정규화 슬롯 좌표(0~1) → 전장 월드 좌표 변환**
- **씬 핸드오프 수신**: `ConsumePendingSetup` → 전투 → `CompleteBattle` → 복귀

## 📌 제약 1가지

전투 유닛 총합(아군 + 적군 + 장군 전부)이 **600을 넘으면 전투 생성이 실패**합니다.
증원·적 구성 규모를 정하실 때 참고 부탁드립니다.

## 📌 아직 열려 있는 안건 — 적 구성 출처

`encounterId`의 정본을 인게임(`EncounterTable`)에 둘지, 아웃게임(난이도 커브 티어 생성기)에 둘지가
아직 정해지지 않았습니다. 지금은 **표시되는 적**과 **실제 스폰되는 적**이 다른 데이터에서 나오는 상태입니다.

아웃게임이 정본이 되는 쪽을 권합니다 — 이미 티어 기반 생성기와 4병과 구역 배치를 갖고 계시고,
적 군대도 `armyDefId` + `upgradeLevel` 형태로 넘겨주시면 인게임이 **플레이어 부대와 똑같은 재구성 경로**로
처리할 수 있어 추가 규약이 거의 없습니다. 이 방향이면 알려주세요 — 인게임의 `EncounterTable`은
로컬 테스트·밸런싱 전용으로 남기겠습니다.

---

## 인게임 현재 상태 (참고)

- 방어력(감쇠)·치명타 시뮬 구현 완료 / 상태이상 8종(부정 5 + 긍정 3) 공용 시스템
- 장군 시스템 완료: 패시브 + 충전식 액티브 4종(방진 / 일제 사격 / 그림자 습격 / 사냥 선포)
- 플레이어 스킬 4종 완료 (번개 / 독구름 / 힐 장판 / 전투 함성)
- 밸런싱 자동화 도구 구축 완료 (헤드리스 시뮬 + 목표 밴드 PASS/FAIL 판정)
- 계약 수신 구조·커넥터 템플릿 준비 완료 — **머지 후 커넥터 활성화 + 전투 씬 배선이면 연결 완료**
