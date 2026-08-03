# Fate Weaver 설계·계획 문서 색인

- 개정일: 2026-08-03
- 역할: 현재 권위 문서와 활성 계획의 단일 진입점

새 작업을 시작할 때는 이 색인에서 해당 도메인의 권위 문서를 먼저 찾는다. `archive/`의 문서는 과거
설계·구현 근거이며 현재 규칙으로 직접 사용하지 않는다.

## 문서 상태

| 상태 | 의미 |
|---|---|
| `current` | 현재 규칙 또는 구현 구조를 설명하는 권위 문서 |
| `active` | 아직 끝나지 않았고 현재 기준으로 실행 가능한 계획 |
| `needs-redesign` | 필요한 영역이지만 기존 문서를 그대로 실행할 수 없음 |
| `archived` | 완료되었거나 현재 기준에서 대체된 역사 기록 |

현행 문서끼리 충돌하면 날짜가 아니라 이 색인의 `권위 범위`와 문서가 명시한 대체 관계를 따른다.
현재 결정을 바꾸는 새 문서는 기존 권위 문서와 이 색인을 같은 커밋에서 함께 갱신해야 한다.

## 현재 권위 문서

### 핵심 아키텍처

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [전투 코어 설계](specs/2026-06-18-fate-weaver-core-design.md) | `current` | 순수 C# 코어 경계, 결정론, 이벤트 출력 | 새 규칙·효과·상태·시뮬레이션 구현 |
| [카드 설명 레지스트리](specs/2026-07-16-description-registry-design.md) | `current` | 효과·상태·개입 설명 핸들러 확장 | 새 카드 능력의 자동 설명 추가 |
| [열린 카드 저작 구조](specs/2026-07-19-open-card-authoring-design.md) | `current` | ScriptableObject 효과 저작과 코어 변환 | 새 효과·상태·개입 저작 타입 추가 |
| [시작 카드 풀 SO 저작](specs/2026-07-29-starter-pool-so-authoring-design.md) | `current` | 22장 CardAsset·CardPoolAsset, Unity 메타데이터, 제거 가능한 헤드리스 export | 시작 카드 풀 에셋·시더·생성 경로 구현 |
| [대상 선택 메타데이터](specs/2026-07-28-p0c-targeting-metadata-design.md) | `current` | 대상 요구의 선언·질의·검증 경로 | 새 대상형 개입 액션·대상 종류 추가 |

### 전투와 파티 규칙

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [덱 기반 코어 루프](specs/2026-06-22-deck-loop-design.md) | `current` | 덱·손패·행동 턴과 상태 타이밍 | 전투 흐름 또는 드로우 경제 변경 |
| [파티 기반 전투](specs/2026-07-15-party-foundation-design.md) | `current` | 파티, 개별 HP, 대형, 전투 중 사망 | 캐릭터 영입·사망·대형 변경 |

### 카드풀과 콘텐츠

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [무작위 10장 시작 덱 구성](specs/2026-07-30-random-starter-deck-design.md) | `current` | 22장 풀에서 역할별 2/2/2/4를 한 번 추첨해 고정하는 시작 덱 | 시작 덱 10장 추첨·에셋 교체·검증 |
| [캐릭터 및 카드풀 설계 규칙](specs/2026-07-20-character-card-pools-design.md) | `current` | 카드 소유권, 카드풀, 독 아키타입, 유산 | 캐릭터·카드·독 카드풀 디자인 |
| [간수 적 설계](specs/2026-06-27-warden-lock-enemy-design.md) | `current` | 잠금 입문 적의 카드·행동 패턴 | 간수 조정 또는 잠금 적 확장 |
| [카드 변형과 런타임 콘텐츠 로딩](specs/2026-07-30-card-mutation-and-runtime-content-design.md) | `current` | OwnedCard의 영구·전투 변형 2목록과 Effective 카드, 코드 생성의 JSON 런타임 로딩 대체, UGC 경계 | 카드 강화·변경 구현, 모딩 지원 착수 |

카드 디자인을 새 세션에서 이어갈 때는
[캐릭터 및 카드풀 설계 규칙](specs/2026-07-20-character-card-pools-design.md)부터 읽는다.

### UX와 표현

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [전투 화면 시각 설계](specs/2026-07-10-battle-scene-visual-design.md) | `current` | 전투 화면의 상위 구도와 표현 방향 | 전투 화면 구조·연출 변경 |
| [위치 대상과 카드 텍스트](specs/2026-07-27-position-targeting-card-text-design.md) | `current` | 다섯 위치 범위와 자신, 실행 시 대상 고정, 대상 칸과 진영별 본문 | 카드 대상·설명·프레임 설계 |
| [카드 아이디어 노트](specs/2026-07-27-card-idea-notebook-design.md) | `current` | 외부 카드 초안 즉시 보존, 진영·등급, 다중 선택·편집, 개별·전체 저장, Markdown 입출력 | 카드 아이디어 도구 구현·변경 |

### 문서 관리

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [문서 정리와 중앙 색인 설계](specs/2026-07-24-document-index-cleanup-design.md) | `current` | 문서 상태, 보관·삭제 기준, 색인 수명주기 | 스펙·계획 추가·완료·대체 |

## 활성 계획과 로드맵

| 문서 | 상태 | 범위 |
|---|---|---|
| [확장성·하드코딩 후속 리팩터링 백로그](plans/2026-07-16-architecture-refactor-backlog.md) | `active` | P1 단일 원본·프리팹·튜닝, P2 표현 경계, §12 2026-07-25 점검 추가 항목, §13 2026-07-30 상태 이상 논의 추가 항목 |
| [상태 규칙 파라미터화와 3종 디버프](plans/2026-07-30-status-rule-and-debuffs.md) | `active` | 방어 흡수 층 분리, 상태 배율의 런타임 조절, 약화·취약·손상 |
| [전투 상호작용 로그](plans/2026-07-31-combat-interaction-log.md) | `active` | 피해 계산 단계별 내역, 상태 부여·만료 이벤트, 한국어 타임라인 포매터, 개발용 Console 덤프 |
| [상태 콘텐츠 JSON화와 카드 저작 표면 축소](plans/2026-08-02-status-content-and-authoring-surface.md) | `active` | 상태가 세기·수명 종류를 소유하고 카드는 count 하나만 준다, 레거시 카드 10장·stun 폐기 (카드 변형 설계의 계획 1.5/4). Task 1~5 구현 완료, 최종 브랜치 리뷰 대기 |
| [상태 등록 지점 통합](plans/2026-08-03-status-registration-consolidation.md) | `active` | 상태 추가 시 손대는 곳 7→4, 수치·이름 변경을 JSON 한 줄로 (카드 변형 설계의 계획 2.5) |

## 진행 중인 작업 흐름: 카드 콘텐츠 (2026-08-03 인계)

[카드 변형과 런타임 콘텐츠 로딩 설계](specs/2026-07-30-card-mutation-and-runtime-content-design.md)를
여러 계획으로 나눠 구현하는 중이다. 새 세션은 이 절을 먼저 읽고 다음 계획 문서로 들어간다.

| | 계획 | 상태 |
|---|---|---|
| 1 | [카드 콘텐츠 JSON 직렬화·로딩](archive/plans/2026-07-31-card-content-json-loading.md) | **완료·머지** |
| 2 | [상태 콘텐츠 JSON화와 카드 저작 표면 축소](archive/plans/2026-08-02-status-content-and-authoring-surface.md) | **완료·머지** |
| 2.5 | [상태 등록 지점 통합](plans/2026-08-03-status-registration-consolidation.md) | **다음** |
| 3 | 콘텐츠 원본 전환 (계획 문서 미작성) | 대기 |
| 3.5 | 개입 액션 다형화·카드 스펙 분리 (미작성) | 대기 |
| 4 | 카드 변형 `CardMutation` (미작성) | 대기 |

계획 3은 소비자를 JSON으로 돌리고 `CardCodeGenerator`·`GeneratedCards.cs`·`CardAsset`의 규칙 필드를
제거한다. 계획 3.5는 개입 액션을 `EffectSpec`처럼 다형화하고 `CardSpec`을 실행/개입으로 쪼갠다
(핸들러가 읽는 파라미터가 액션마다 달라, 지금은 `lock` 카드가 안 쓰는 칸 넷을 들고 있다).

### 새 세션이 먼저 알아야 할 함정 셋

1. **`[SerializeReference]`를 건드리면 `.asset` YAML도 같은 커밋에서 옮긴다.** Unity는 어셈블리
   한정 타입명과 필드명을 YAML에 박아두고, 없는 멤버는 조용히 버린다. 이 흐름에서 두 번 밟았다 —
   계획 1은 어셈블리 이동으로 27개 카드 에셋의 `Effects`를 `null`로, 계획 2는 필드 제거로 17개
   에셋을 `Count = 0`으로 만들 뻔했다. **헤드리스 테스트는 둘 다 못 잡는다. Unity EditMode만 잡는다.**
2. **`DefaultValueHandling.Ignore`가 열거형 0번 값을 지운다.** `Side.Player`·`CardCategory.Execution`·
   `StatusLifetimeKind.Permanent`가 전부 0이라 JSON에서 사라졌다. 생략이 위험한 필드에는
   `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]`를 붙인다.
3. **런타임은 아직 JSON을 읽지 않는다.** 배틀 씬은 `BattleScreenController.cs`의
   `member.Deck.ToSpecs()`로 **CardSO 에셋에서** 카드를 만든다. `CardContentLoader`·
   `StatusContentLoader`는 테스트에서만 불린다. 계획 3이 이 전환을 한다 — 그전까지 SO는 살아 있는
   원본이다.

### 넘어온 부채

- **설명 카탈로그가 전투와 다른 `StatusContentCatalog` 인스턴스를 읽는다.**
  `KoreanDescriptionCatalog.Default`가 전역 싱글턴이고 그 `StatusContent`가
  `StatusContentDefaults.Catalog()`로 고정돼 있다. 계획 3이 로더를 부팅에 배선하면 카드 텍스트는
  코드 기본값을, 규칙은 파일을 보게 되어 갈린다. **계획 2.5의 Task 3이 이걸 함께 고친다.**
- **`CardSO`의 규칙 필드가 검증 없이 남아 있다.** SO→코드생성 일치를 지키던 스냅샷 테스트는
  복원했지만, 설계 §4.5대로 계획 3이 SO의 규칙 필드를 지우면 이 축 전체가 사라진다.

### 현재 수치 (계획 2 머지 시점)

헤드리스 **446/446**, Unity EditMode **520/520**, 카드 JSON **26**, 상태 JSON **11**.
헤드리스 명령은 `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`.

## 재설계가 필요한 영역

| 영역 | 상태 | 이유와 재개 기준 |
|---|---|---|
| 런 원 사이클 | `needs-redesign` | 과거 설계가 `재화 없음`, `사망 카드 인계 없음`, 이전 보상 모델을 전제한다. 재개 시 현재 카드풀 문서의 유산·소유권 규칙을 기준으로 새 스펙을 작성한다. |

과거 런 설계와 계획은 [보관 문서 색인](archive/README.md)에서 참고할 수 있다.

## 문서 수명주기

1. 새 스펙·계획을 추가할 때 이 색인을 같은 커밋에서 갱신한다.
2. 구현이 끝난 세부 계획과 구현 기록은 `archive/plans/`로 옮긴다.
3. 대체된 설계는 구현의 역사적 근거가 있으면 `archive/specs/`로 옮긴다.
4. 승인되지 않은 WIP와 유효한 내용이 완전히 흡수된 문서는 삭제한다.
5. 현행 `specs/`와 `plans/`에는 `current` 또는 `active` 문서만 둔다.
6. 보관 문서는 현재 규칙의 권위가 아니며, 현재 문서가 명시적으로 연결할 때만 참고한다.

## 보관소

완료된 설계·계획·구현 기록은 [보관 문서 색인](archive/README.md)에 분리되어 있다.
