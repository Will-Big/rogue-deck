# 런 원 사이클 뼈대 — 계획 인덱스 (병렬 작업 안내)

스펙: [2026-07-20-run-cycle-skeleton-design.md](../specs/2026-07-20-run-cycle-skeleton-design.md)

원 사이클 구현은 서로 얽히지 않는 기능 단위로 5개 계획 파일로 나뉜다. 각 계획은 전용 워크트리·브랜치에서 독립 실행할 수 있도록 파일 소유권이 겹치지 않게 설계되었다.

## 계획 목록과 의존 관계

| # | 계획 | 브랜치 | 워크트리 | 선행 조건 |
|---|---|---|---|---|
| 1 | [run-core-foundation](2026-07-20-run-core-foundation.md) — RunState·RunDefinition·노드 레지스트리 | `feat/run-core` | `../rogue-deck-run-core` | 없음 (최우선) |
| 2 | [run-combat-node](2026-07-20-run-combat-node.md) — 전투 노드 핸들러, HP 이월, 인카운터 | `feat/run-combat-node` | `../rogue-deck-run-combat-node` | 1 머지 후 |
| 3 | [run-recruit-heal-node](2026-07-20-run-recruit-heal-node.md) — 고용·회복 노드 핸들러 | `feat/run-recruit-heal-node` | `../rogue-deck-run-recruit-heal` | 1 머지 후 |
| 4 | [run-combat-reward](2026-07-20-run-combat-reward.md) — 보상 롤·적용 + 보상 패널 컴포넌트 | `feat/run-reward` | `../rogue-deck-run-reward` | 1 머지 후 |
| 5 | [run-unity-flow](2026-07-20-run-unity-flow.md) — 부팅 등록, SO 파이프라인, 화면 흐름, 씬·콘텐츠, 통합 테스트 | `feat/run-unity-flow` | `../rogue-deck-run-unity-flow` | 2·3·4 모두 머지 후 |

```
1 (foundation)
├── 2 (전투 노드)      ┐
├── 3 (고용·회복 노드)  ├── 5 (Unity 흐름·통합)
└── 4 (전투 보상)      ┘
```

- **2·3·4는 완전 병렬** — 서로 파일이 겹치지 않는다 (2만 `PartyMemberLoadout`·`DeckCombatSession`을 수정, 3·4는 신규 파일만). 머지 순서도 상호 무관.
- 핸들러들을 실제 등록하는 `RunRegistries`와 원 사이클 통합 테스트는 의도적으로 5에 있다 — 2·3·4가 등록 지점을 공유 수정하며 충돌하는 것을 피하기 위함.
- 보상 UI는 사용자 결정대로 **전투 씬에서 전투 완료 후** 뜨는 패널(4에서 컴포넌트, 5에서 씬 배선)이다.

## 공통 규칙 요약

- 머지는 매번 사용자 승인 후 (AGENTS.md 규칙 19). 머지 전 전체 헤드리스 테스트 통과 확인:
  `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
- 메인 체크아웃에서 브랜치 전환 금지 (규칙 15) — 반드시 위 표의 전용 워크트리 사용.
- `Assets/` 아래 새 파일은 Unity `-batchmode` 실행으로 `.meta`를 생성해 함께 커밋 (규칙 16·17).
- Unity Play/GUI 검증은 워크트리에서 하지 않는다 — 5번 계획 말미의 사용자 검증 체크리스트로 인계.
