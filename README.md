# ClayWars

> **점토 병사들에게 아이템으로 "역할"을 부여하고, 로그라이크 맵을 헤쳐 나가며
> 수백 명 규모의 군단 전투를 관람·개입하는 3D 전략 게임.**

|         |                          |
| ------- | ------------------------ |
| **장르**  | 3D 전략 / 자동 전투 관람 + 로그라이크 |
| **플랫폼** | Android (APK) — 가로 화면 전용 |
| **엔진**  | Unity 6 (6000.5.3f1)     |
| **팀**   | 프로그래머 2인 (AnsirH · lhwsam)  |
| **대회**  | NHN NAN 2026 게임잼 사전 과제   |

<p align="center">
  <img src="images/01_hero.png" alt="군단 전투 전경" width="680">
</p>

| 바로 보기 | |
|---|---|
| **플레이 영상 (30~60초)** | https://youtu.be/k1a6I0x0kqA |
| **APK 다운로드** | [Google Drive](https://drive.google.com/file/d/1TB-QQHgXv2MgILYnDDCQKNK0Fu_H8idj/view?usp=drive_link) |
| **게임 소개 및 설치 안내** | [게임 소개 및 설명 문서.md](게임%20소개%20및%20설명%20문서.md) — 게임 방법·조작·APK 설치 절차(차단 해제 포함) |

---

## 게임 소개

평범한 점토 병사 한 무리로 시작합니다. 이들은 **병과가 없습니다.** 활을 쥐어 주면 궁수가 되고,
단검을 쥐어 주면 암살자가 됩니다. 부대를 전장에 배치하고 나면 전투는 자동으로 흐르고,
플레이어의 입력은 **스킬 한 방**뿐입니다.

1. **군단 전투 스펙터클** — 수백 명이 꽝 부딪히는 자동 전투를 지켜보는 것 자체가 본체입니다.
2. **되돌릴 수 없는 역할 부여** — 아이템은 한 번 주면 부대에 **영구 귀속**됩니다. "이 활을 어느 부대에 줄 것인가"가 런 전체를 좌우합니다.
3. **한 방의 개입** — 잘 고른 스킬 타이밍 하나가 전황을 뒤집는 것이 보여야 합니다.

여기에 로그라이크(랜덤 방 그래프 + 증강 선택) 진행을 얹어 매 런마다 다른 군단이 만들어집니다.
상세한 게임 방법과 시스템 설명은 [게임 소개 및 설명 문서](게임%20소개%20및%20설명%20문서.md)에 있습니다.

## 실행 방법

**APK (권장)** — 위 표의 링크에서 내려받아 설치합니다. Android 8.0(API 26) 이상 · ARM64 · 가로 화면 전용,
오프라인 동작, 요구 권한 없음. 스토어를 거치지 않은 APK라 설치 차단 안내가 뜰 수 있습니다 —
해제 절차는 [게임 소개 및 설명 문서의 3장](게임%20소개%20및%20설명%20문서.md#3-실행-방법-android-apk)을 따라 주세요.

**Unity 에디터** — Unity `6000.5.3f1`로 프로젝트를 열고 `Assets/OutGame/Scenes/MainMenu.unity`를 실행합니다.
빌드 씬 구성은 `MainMenu → OutGame(방 그래프) → Battle(전투) → Loading` 4개입니다.

## 프로젝트 구조

```
Assets/
├── Scripts/                  # 인게임(전투) — NHN.* 어셈블리
│   ├── Simulation/           #   전투 시뮬 본체. 순수 C#, UnityEngine 참조 없음 (noEngineReferences)
│   ├── Simulation.Tests/     #   헤드리스 EditMode 테스트 (CLI 교차 검증 포함)
│   ├── Data/                 #   ScriptableObject 데이터·전투 요청 빌더
│   ├── Integration/          #   아웃게임 ↔ 전투 연결 (커넥터·컨버터)
│   ├── Presentation/         #   전투 뷰 (카메라·HUD·부트스트랩)
│   └── Infra/                #   공용 인프라 (오브젝트 풀 등)
├── OutGame/                  # 아웃게임 — 방 그래프·배치·증강·메타 진행 (OutGame.* 어셈블리)
└── Docs/                     # 기획 정본·AI 활용 로그
Tools/
└── BalanceLab/               # 밸런싱 파이프라인 — dotnet CLI + 웹 뷰어 (Unity 불필요)
```

## 아키텍처 — 전투 시뮬이 순수 C#인 이유

전투 시뮬(`NHN.Simulation`)은 UnityEngine을 참조하지 않는 순수 C#이며, 시드 주입 결정론(같은 입력과
시드 = 항상 같은 결과)을 유지합니다. 이렇게 설계한 이유는:

- **에디터 없이 검증** — 시뮬레이션을 유니티 에디터가 아니라 코드만으로 돌려 확인할 수 있습니다.
  `dotnet` 하나로 수백 판을 초 단위에 돌립니다.
- **AI 협업** — AI가 MCP 같은 별도 브리지 없이 CLI만으로 전투를 실행·검증할 수 있어,
  "수치 변경 → 수백 판 실측 → 판단"의 루프를 AI와 사람이 같은 도구로 공유합니다.
- **재현성** — 리포트에 기록된 모든 시드는 언제나 다시 돌려볼 수 있고, 게임 런타임(Mono)과
  CLI(.NET)의 통계적 등가성을 테스트(`BalanceLabCrossCheckTests`)가 상시 감시합니다.

## BalanceLab — 밸런싱 파이프라인

```bash
dotnet run --project Tools/BalanceLab -- Tools/BalanceLab/scenarios/all.json
```

14개 시나리오(장군 유/무·미러·롤 상성)를 목표 승률 밴드와 대조해 PASS/FAIL을 판정합니다.
`Tools/BalanceLab/viewer/index.html`을 **더블클릭**하면 서버 없이 결과 대시보드가 열립니다 —
승률 매트릭스, 시드별 리플레이(탑다운 2D 재생), 수치 슬라이더 → `overrides.json` 내보내기까지
한 화면에서 돕니다. 상세: [Tools/BalanceLab/README.md](Tools/BalanceLab/README.md)

## 테스트

Unity Test Runner(EditMode)에서 실행합니다.

- `NHN.Simulation.Tests` — 전투 시뮬 헤드리스 테스트 39종. 결정론·전투 규칙·CLI 교차 검증.
- `OutGame.Tests.EditMode` / `OutGame.Tests.PlayMode` — 아웃게임 로직·흐름 테스트.

## 문서

| 문서 | 내용 |
|---|---|
| [게임 소개 및 설명 문서](게임%20소개%20및%20설명%20문서.md) | 게임 방법·조작·APK 설치 안내 (심사용 정본) |
| [AI 활용 기술 문서](AI%20활용%20기술%20문서.md) | AI를 활용한 개발 과정 정리 |
| [Assets/Docs/nan2026_design_doc.md](Assets/Docs/nan2026_design_doc.md) | 게임 기획 정본 |
| [Assets/Docs/ai_usage_log.md](Assets/Docs/ai_usage_log.md) | AI 활용 작업 로그 (시행착오 포함 원본 기록) |
| [Tools/BalanceLab/README.md](Tools/BalanceLab/README.md) | 밸런싱 파이프라인 사용법 |
| [Tools/BalanceLab/밸런스 뷰어 설계.md](Tools/BalanceLab/밸런스%20뷰어%20설계.md) | 밸런스 뷰어 설계 문서 |
