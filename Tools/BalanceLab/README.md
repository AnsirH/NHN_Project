# BalanceLab — 밸런싱 파이프라인 (작업 1: CLI 러너)

## 사용법

```
cd Tools/BalanceLab
dotnet run -- scenarios/<시나리오>.json
```

- 시드 0..N-1 고정 실행 → `results/<시나리오명>.json` 생성 (판별 기록 포함 — 모든 시드는 언제나 재현 가능)
- 밸런스 수치의 단일 출처 = Unity `.asset` 파일 (CLI가 직접 파싱, fail-fast). 튜닝 = .asset 수정 후 재실행,
  Unity는 refresh만으로 게임에 반영된다. 시나리오 JSON에는 스탯을 넣지 않는다.
- guid→경로 인덱스는 캐싱 없이 매 실행 재구축된다.

## 정본 분리 원칙 (교차 검증 계약)

| 역할 | 정본 |
|---|---|
| 밸런싱 통계 (승률·분포·리포트) | **CLI (.NET)** |
| 시드 리플레이 (눈 검증, 작업 4) | **에디터 자체 시뮬 (Mono)** |

두 런타임은 JIT float 정밀도 차이(Mono가 확장 정밀도 중간값을 허용 — ECMA CIL 표준 동작)로
**비트 단위 일치가 불가**함이 실측으로 확인됐다 (FMA·하드웨어 인트린식 비활성화 실험으로 소거 —
상세 경위는 Assets/Docs/ai_usage_log.md). 따라서 교차 검증(`BalanceLabCrossCheckTests`)은
결정론이 아니라 **두 정본의 통계적 등가성**을 감시한다:

- **승자 불일치 = 즉시 FAIL (무관용)**
- 종료 틱 **상대 오차 ±20% 허용** (절대 틱이 아닌 이유: 드리프트는 나비효과라 전투 길이에 비례해 커진다 —
  실측 최대 15.3%, 일제 사격 시나리오. 여유를 둬 ±20%로 확정)
- 검증 시드 **100개** — "100시드 승자 뒤집힘 0건"이 이 계약의 신뢰 근거
- 작업 4(시드 리플레이) 완료 기준도 동일 계약: 승자 일치 + 종료 틱 상대 ±20%
- 작업 2에서 미러 매치업(45~55% 밴드) 시나리오가 생기면 교차 검증 대상에 추가한다 —
  박빙 판이야말로 승자 안정성의 진짜 스트레스 테스트다

### 실측 (2026-07-24, 시드 100개 × 2종)

| 시나리오 | 승자 뒤집힘 | 최대 틱 드리프트 | 판정 |
|---|---|---|---|
| warrior_general_vs_plain (방진, 삼각함수 미사용) | 0건 | 18틱 / 352틱 = 5.1% | PASS |
| archer_general_volley_cross (일제 사격, Sin/Cos 경로) | 0건 | 50틱 / 327틱 = 15.3% | PASS |

완전한 크로스 런타임 결정론(고정소수점 수학 전환)은 **잼 이후 과제**로 보류한다.

## 교차 검증 실행 절차

1. CLI로 두 시나리오 실행 (results/ 생성):
   `dotnet run -- scenarios/warrior_general_vs_plain.json`
   `dotnet run -- scenarios/archer_general_volley_cross.json`
2. Unity EditMode 테스트 실행 → `BalanceLabCrossCheckTests`가 시드 0..99를 에디터 인프로세스로
   재실행해 CLI 기록과 비교한다. (결과 파일이 없으면 Inconclusive)
