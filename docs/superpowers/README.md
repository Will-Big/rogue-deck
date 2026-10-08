# Fate Weaver 설계·계획 문서 색인

- 개정일: 2026-10-08
- 역할: 현재 기준 문서와 진행 중인 계획을 찾는 출발점

새 작업을 시작할 때는 이 색인에서 해당 도메인의 기준 문서를 먼저 찾는다.

`.archive/`는 **폐기된 이력이며 읽지 않는다.** 이유와 보관 절차는
[문서 수명주기](../agents/doc-lifecycle.md)에 있다.

## 문서 상태

| 상태 | 의미 |
|---|---|
| `current` | 현재 규칙 또는 구현 구조를 설명하는 기준 문서 |
| `active` | 아직 끝나지 않았고 현재 기준으로 실행 가능한 계획 |
| `needs-redesign` | 필요한 영역이지만 기존 문서를 그대로 실행할 수 없음 |
| `archived` | 완료·대체되어 `.archive/`로 옮긴 기록. 이 색인에는 남기지 않는다 |

현행 문서끼리 충돌하면 날짜가 아니라 이 색인의 `적용 범위`와 문서가 명시한 대체 관계를 따른다.
현재 결정을 바꾸는 새 문서는 기존 기준 문서와 이 색인을 같은 커밋에서 함께 갱신해야 한다.

## 현재 기준 문서

### 핵심 아키텍처

| 문서 | 상태 | 적용 범위 | 다음 사용 시점 |
|---|---|---|---|
| [전투 코어 설계](specs/2026-06-18-fate-weaver-core-design.md) | `current` | 순수 C# 코어 경계, 결정론, 이벤트 출력 | 새 규칙·효과·상태·시뮬레이션 구현 |
| [카드 설명 레지스트리](specs/2026-07-16-description-registry-design.md) | `current` | 효과·상태·개입 설명 핸들러 확장 | 새 카드 능력의 자동 설명 추가 |
| [열린 카드 저작 구조](specs/2026-07-19-open-card-authoring-design.md) | `current` | 다형 효과 스펙의 매핑·검증·확장 구조(JSON `kind` 판별자와 컨버터) | 새 효과·상태·개입 저작 타입 추가 |
| [대상 선택 메타데이터](specs/2026-07-28-p0c-targeting-metadata-design.md) | `current` | 카드 플레이 전 입력 대상 요구의 선언·질의·검증. 실행 효과의 자동 대상 선택은 전투 실행 계약이 우선한다 | 새 대상형 개입 액션·대상 종류 추가 |

### 전투 실행 재설계

| 문서 | 상태 | 적용 범위 | 다음 사용 시점 |
|---|---|---|---|
| [전투 실행·반응·콘텐츠 계약](specs/2026-09-18-combat-execution-contract-design.md) — [HTML 검토](specs/2026-09-18-combat-execution-contract-design.html) | `current` | 실행 카드·효과별 위치(카드 축 + 효과 진영)·직접 반응·피해 속성·소비 보상·카드 종료 승패·공통 만료 시점의 기준 문서. 2026-09-18 구현 완료·master 머지(`9dbbdca`). 구현 계획은 보관됨. 다중 적 정책 제외 | 전투 실행·반응·만료 규칙 변경 |

### 전투와 파티 규칙

| 문서 | 상태 | 적용 범위 | 다음 사용 시점 |
|---|---|---|---|
| [덱 기반 코어 루프](specs/2026-06-22-deck-loop-design.md) | `current` | 덱·손패·행동 턴의 구조. 상태 만료와 실행 시점은 전투 실행 계약이 우선한다 | 전투 흐름 또는 드로우 경제 변경 |
| [파티 기반 전투](specs/2026-07-15-party-foundation-design.md) | `current` | 파티, 개별 HP, 대형, 카드 소유. 치명타 버티기는 전투 실행 계약이 제거했다 | 캐릭터 영입·사망·대형 변경 |
| [전투 템포 개선](specs/2026-09-20-combat-pacing-design.md) — 개요는 [HTML](specs/2026-09-20-combat-pacing-design.html) | `current` | 전투 길이와 단조로움을 콘텐츠로 잡은 결정과 수치: 파티원 B의 직접 피해 덱, 방어 3 대 고블린 공격 5의 상쇄 해소, 고블린의 `shuffle_bag` 정책과 빈 턴 제거, 짝 전투용 약체 `goblin_runt`. 2026-09-20 구현(단독 5·4·5턴, 짝 5·5·6턴), 화면 확인 완료(2026-10-06). Unity 배치 EditMode 회귀만 남았다(대기열). 구현 계획은 보관됨 | 전투 길이·적 수치 조정, 새 적·편성 저작 |
| [전투 노드 한 사이클](specs/2026-09-15-combat-node-cycle-design.md) — 개요는 [HTML](specs/2026-09-15-combat-node-cycle-design.html) | `current` | 전투 한 판의 시작~끝(승패·보상 선택·덱 반영·다음 전투), 노드 시드와 목적별 스트림 파생, 적·편성·캐릭터 스탯·전투 규칙 JSON. 1단계 흐름 → 2단계 구성 저작. 1단계 구현 완료(2026-09-15). 2단계 구현 완료(2026-09-17) | 전투 결과·보상 구현, 시드 동작 추가, 적·편성 저작 |

### 카드풀과 콘텐츠

| 문서 | 상태 | 적용 범위 | 다음 사용 시점 |
|---|---|---|---|
| [무작위 10장 시작 덱 구성](specs/2026-07-30-random-starter-deck-design.md) | `current` | 22장 풀에서 역할별 2/2/2/4를 한 번 추첨해 고정하는 시작 덱 | 시작 덱 10장 추첨·에셋 교체·검증 |
| [캐릭터 및 카드풀 설계 규칙](specs/2026-07-20-character-card-pools-design.md) | `current` | 카드 소유권, 카드풀, 독 아키타입, 유산 | 캐릭터·카드·독 카드풀 디자인 |
| [카드 변형과 런타임 콘텐츠 로딩](specs/2026-07-30-card-mutation-and-runtime-content-design.md) | `current` | OwnedCard의 영구·전투 변형 2목록과 Effective 카드, 코드 생성의 JSON 런타임 로딩 대체, UGC 경계 | 카드 강화·변경 구현, 모딩 지원 착수 |

카드 디자인을 새 세션에서 이어갈 때는
[캐릭터 및 카드풀 설계 규칙](specs/2026-07-20-character-card-pools-design.md)부터 읽는다.
기본 캐릭터 다섯 명의 설계는 진행 중이며, 검수된 상태·효과 후보와 그동안 정한 원칙은 참고 메모
[상태·효과 후보](<../../Tools/card-idea-notebook/상태·효과 후보.md>)에 모아 둔다(2026-10-08 갱신). 이 메모는 기준 문서가 아니다.

### UX와 표현

| 문서 | 상태 | 적용 범위 | 다음 사용 시점 |
|---|---|---|---|
| [손패 레이아웃 조절](../agents/hand-layout.md) | `current` | 손패 원호·장수별 펼침·크기와 독립된 위치 이동·음수 여백을 지원하는 하단 정렬·DOTween 전환의 인스펙터 사용법과 책임 경계 | 손패 배치 튜닝·유지보수 |
| [전투 화면 시각 설계](specs/2026-07-10-battle-scene-visual-design.md) | `current` | 전투 화면의 상위 구도와 표현 방향 | 전투 화면 구조·연출 변경 |
| [전투 화면 컴포넌트 분해](specs/2026-08-04-battle-screen-decomposition-design.md) | `current` | 전투 화면 Unity 컴포넌트의 경계와 책임 분배 | 전투 화면에 컴포넌트·표현 추가, 캐릭터 아트 도입 |
| [턴 연출 재생 구조](specs/2026-08-30-turn-playback-design.md) — 개요는 [HTML](specs/2026-08-30-turn-playback-design.html) | `current` | 이벤트 타임라인을 연출 단위로 나누어 순서대로 재생하는 구조, 배속·건너뛰기, 이벤트별 연출 처리 등록 | 턴 실행 결과에 연출을 추가하거나 재생 동작을 바꿀 때 |
| [적 카드 묶음](specs/2026-08-31-enemy-card-bundle-design.md) — 개요는 [HTML](specs/2026-08-31-enemy-card-bundle-design.html) | `active` | 적이 매 턴 어떤 카드를 존에 올리는지: 선택 단위가 묶음이고 기본 선택자(무작위·비복원·순서)가 고른다. 1단계 구현 완료, 조건 각본은 2단계 | 몬스터 행동 재작업, 보스 패턴 설계, 적 조건 추가 |
| [위치 대상과 카드 텍스트](specs/2026-07-27-position-targeting-card-text-design.md) | `current` | 다섯 위치 범위와 자신, 대상 칸과 진영별 본문 표기. 대상을 고르는 시점은 전투 실행 계약이 대체했다 | 카드 대상·설명·프레임 설계 |
| [프리미티브 카드 프레임과 구조화 설명](specs/2026-07-31-primitive-card-frame-design.md) | `current` | 실행·개입 카드 폼팩터, 대상 glyph, 진영별 구조화 설명, 반응형 핸드 | 카드 프레임·대상·설명 표현 변경 |
| [카드 상태 그리드와 호버 툴팁](specs/2026-08-03-card-status-grid-tooltip-design.md) | `current` | 카드에 직접 붙은 상태의 4열 그리드, 표시 데이터 경계, 호버 설명 | 카드 상태 아이콘·툴팁 구현·변경 |
| [카드 저작 노트북 JSON 전환](specs/2026-08-05-card-authoring-json-notebook-design.md) | `current` | 저작 원본을 Markdown에서 콘텐츠 JSON으로, 구조화 효과 편집기, 저장소 직접 읽기·쓰기, 풀 편성, 생성 스키마. **계획 A~D 완료** | 카드 저작 도구 구현·변경 |

### 문서 관리

| 문서 | 상태 | 적용 범위 | 다음 사용 시점 |
|---|---|---|---|
| [문서 정리와 중앙 색인 설계](specs/2026-07-24-document-index-cleanup-design.md) | `current` | 문서 상태, 보관·삭제 기준, 색인 수명주기 | 스펙·계획 추가·완료·대체 |

### 작업 환경과 도구

| 문서 | 상태 | 적용 범위 | 다음 사용 시점 |
|---|---|---|---|
| [graphify 카드 그래프 통합](specs/2026-08-28-graphify-card-graph-design.md) | `current` | 카드·상태 JSON의 그래프 편입, 재생성 단일 명령, 카드·아키텍처 시각화 | 그래프 도구 확장(새 그래프 가지치기 규칙, 새 뷰) |

검증과 실행 명령은 문서가 아니라 [`AGENTS.md`](../../AGENTS.md)의 「명령」 절을 기준으로 삼는다 —
`Tools/verify.sh` 하나가 규칙 검사·헤드리스·노트북을 돌린다. `.githooks/pre-commit`은 커밋에 든 경로에 따라
그 일부만 돌리고, CI(`.github/workflows/verify.yml`)는 master push·PR·수동 실행에서 전체를 돌린다. 상황별 절차는
[`docs/agents/`](../agents/)에 있다: [그래프 조회·재생성·비용](../agents/graphify-usage.md),
[EditMode 배치 실행과 라이선싱 장애](../agents/unity-batch-runs.md),
[설계·계획 문서 골격](../agents/design-doc-format.md)(규칙 28·29의 상세 — **새 문서를 쓰기 전에
읽는다**). 이 문서들은 **작업 절차**이며 도구에 매이지 않도록 `.claude/` 바깥에 둔다.
손패 레이아웃 조절처럼 작업의 직접 진입점인 문서는 위 표에도 연결한다.

## 진행 중인 계획과 로드맵

| 문서 | 상태 | 범위 |
|---|---|---|
| [카드·상태 변경과 count 합산 구조](plans/2026-10-07-card-status-mutation-roadmap.md) — [HTML 설계도](plans/2026-10-07-card-status-mutation-roadmap.html) | `active` | 수정된 구조 후보 검토. 키당 인스턴스 하나·Count 합산, 중앙 정도와 개별 현재 수치의 책임 분리, Count와 무관한 규칙 호버 설명. 부여 기록·부여자 추적·N회 실행은 철회. 범용은 파티 전체·적 제외, UI 배치는 미결정. 단일 Count 이행 → 중앙 규칙·표시 → 카드 변경 → 이름 정리 |
| [확장성·하드코딩 후속 리팩터링 백로그](plans/2026-07-16-architecture-refactor-backlog.md) | `active` | 남은 범위: P1-B 프리팹화, P1-C 튜닝 데이터화, P2 표현 경계, §12~§14 점검 추가 항목, §15 유지 기간별 상태 수치 분리. 항목별 현황은 그 문서 §0 표 |
| [후속 작업 상세](plans/2026-10-06-follow-up-queue.md) | `active` | 아래 「후속 작업 대기열」 표의 항목별 본문·근거·결정 기록(F1~F15). 다른 문서에 상세가 있는 항목은 표가 직접 가리킨다 |
| [카드 상태 그리드와 툴팁 구현](plans/2026-08-03-card-status-grid-tooltip.md) | `active` | Task 1–2의 JSON 독립 UI·프리팹은 완료. Task 3–5의 표시 데이터 변환·공유 호버 툴팁 연결은 **선행 작업 없이 재개 가능** — 재개 조건과 순서는 계획 맨 위 갱신(2026-10-06)에 있다 |

## 남은 작업 흐름: 카드 콘텐츠

[카드 변형과 런타임 콘텐츠 로딩 설계](specs/2026-07-30-card-mutation-and-runtime-content-design.md)의
JSON 런타임 로딩은 구현이 끝났다(계획 3a~3d·3.5, 2026-08-06). **남은 것은 계획 4 `CardMutation`
하나이며 개별 구현 계획은 아직 쓰지 않았다.** 기존 선행 작업은 끝났고, [카드·상태 변경 로드맵](plans/2026-10-07-card-status-mutation-roadmap.md)에 설계 결정과 실행 순서를 정리했다. 상태 수치까지의 적용 범위는 로드맵 단계 A와 [백로그 §15](plans/2026-07-16-architecture-refactor-backlog.md)를 함께 보고 결정한다.

콘텐츠 원본과 저작 규칙은 [content-authoring.md](../agents/content-authoring.md), 검증 명령은
[commands.md](../agents/commands.md)를 기준으로 삼는다. 테스트·카드 개수 같은 수치는 여기 적지 않는다 —
`Tools/verify.sh`와 저장소가 답한다.

## 후속 작업 대기열

남은 일의 색인이다. 본문·근거·결정 기록은 [후속 작업 상세](plans/2026-10-06-follow-up-queue.md)에 번호별로 있다. 끝난 항목은 이 표와
상세 문서에서 함께 지운다(번호는 결번으로 남는다).

| # | 항목 | 분류 | 상세 |
|---|---|---|---|
| F1 | 적 개체별 수치 차이 — 전투 진입 시 HP·카드 피해를 개체마다 다르게 확정 | 요구사항 | [상세 F1](plans/2026-10-06-follow-up-queue.md) |
| F2 | 스테이지 맵 생성 시 노드 내용 사전 확정 | 요구사항 | [상세 F2](plans/2026-10-06-follow-up-queue.md) |
| F3 | 보상용 캐릭터 풀의 임시 데이터 교체 — 기본 캐릭터 다섯 명 설계 중(중심 컨셉 "HP를 잃는 것", 혈주·광전사 풀 검수 중). 연계 작업 F15 | 진행 중 | [상세 F3](plans/2026-10-06-follow-up-queue.md), [후보 메모](<../../Tools/card-idea-notebook/상태·효과 후보.md>) |
| F4 | 카드 상태 UI에 실제 전투 데이터와 툴팁 연결 | 바로 가능 | [구현 계획 맨 위 갱신](plans/2026-08-03-card-status-grid-tooltip.md) |
| F5 | 전투 템포 개선의 Unity 배치 EditMode 회귀 — 에이전트가 돌린다 | 바로 가능 | [전투 템포 개선](specs/2026-09-20-combat-pacing-design.md) 검증 절차 4 |
| F6 | 턴 연출 재생: 방어 수치 등 효과가 실행 즉시 보이지 않는다 | 바로 가능 | [상세 F6](plans/2026-10-06-follow-up-queue.md) |
| F7 | 디버프 3종(약화·취약·손상)의 화면 표시 확인 | 눈으로 확인 | [상세 F7](plans/2026-10-06-follow-up-queue.md) |
| F8 | 턴 연출 재생: 실행 중인 카드의 아웃라인 색과 두께 | 눈으로 확인 | [상세 F8](plans/2026-10-06-follow-up-queue.md) |
| F9 | 계획 3.5의 남은 세 항목 — 복수 개입, `lock` 카드 콘텐츠, 컨트롤러 입력 처리 | 결정 필요 | [상세 F9](plans/2026-10-06-follow-up-queue.md) |
| F10 | 유지 기간별 상태 수치 분리와 저작 데이터 조회 방식 | 결정 필요 | [백로그 §15](plans/2026-07-16-architecture-refactor-backlog.md) (§13.1 이름 정리 포함) |
| F11 | 치명타 버티기 재도입 — 새 설계부터 | 결정 필요 | [상세 F11](plans/2026-10-06-follow-up-queue.md) |
| F12 | HP 회복 수단 — 후순위로 미룸 | 결정 필요 | [상세 F12](plans/2026-10-06-follow-up-queue.md) |
| F13 | 조건별 평가 함수를 레지스트리에 등록하는 구조로 전환 | 조건부 | [백로그 §10](plans/2026-07-16-architecture-refactor-backlog.md) |
| F14 | 턴 연출 재생 구조에 추가할 개별 연출 | 조건부 | [상세 F14](plans/2026-10-06-follow-up-queue.md) |
| F15 | 키워드·상태·카드 아이디어를 빠르게 검증하는 환경 — F3 연계. 구조 수정 단위를 "이음매"로 묶고, 봇과 대리 지표로 재미·밸런스·어울림을 가린다 | 요구사항 | [상세 F15](plans/2026-10-06-follow-up-queue.md), [카드·상태 변경 로드맵](plans/2026-10-07-card-status-mutation-roadmap.md)과 범위가 겹친다 |

분류: `요구사항`은 설계부터 필요한 것, `바로 가능`은 선행 작업 없이 시작할 수 있는 것, `눈으로 확인`은 사용자가 화면으로
판단할 것(규칙 17), `결정 필요`는 착수 전에 정할 것이 있는 것, `조건부`는 정해진 조건이 충족되면 시작하는 것이다.

## 재설계가 필요한 영역

| 영역 | 상태 | 이유와 재개 기준 |
|---|---|---|
| 런 한 사이클 | `needs-redesign` | 과거 설계가 `재화 없음`, `사망 카드 인계 없음`, 이전 보상 모델을 전제한다. 재개 시 현재 카드풀 문서의 유산·소유권 규칙을 기준으로 새 스펙을 작성한다. **전투 노드 한 판(결과·보상·덱 반영)은 [전투 노드 한 사이클](specs/2026-09-15-combat-node-cycle-design.md)이 맡는다** — 남은 재설계 범위는 맵(노드 목록은 `Map/` 범주)·세이브·영입·유산이다. |
| 카드 유효 수치 색상 피드백 | `needs-redesign` | 카드 변형과 런·전투 상태 중앙관리 작업이 원본값·유효값의 표현 계약을 확정한 뒤 피해·방어·비용 등 변경된 텍스트 span만 색으로 표시한다. 상태 아이콘은 사용하지 않는다. |

## 문서 수명주기

작성 형식은 [design-doc-format.md](../agents/design-doc-format.md)(규칙 28·29), 상태·색인 갱신·보관 절차는 [doc-lifecycle.md](../agents/doc-lifecycle.md)(규칙 20)를 따른다.
