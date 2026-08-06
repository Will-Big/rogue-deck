# Fate Weaver 설계·계획 문서 색인

- 개정일: 2026-08-05
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
| [전투 화면 컴포넌트 분해](specs/2026-08-04-battle-screen-decomposition-design.md) | `current` | 전투 화면 Unity 컴포넌트의 경계와 책임 분배 | 전투 화면에 컴포넌트·표현 추가, 캐릭터 아트 도입 |
| [위치 대상과 카드 텍스트](specs/2026-07-27-position-targeting-card-text-design.md) | `current` | 다섯 위치 범위와 자신, 실행 시 대상 고정, 대상 칸과 진영별 본문 | 카드 대상·설명·프레임 설계 |
| [프리미티브 카드 프레임과 구조화 설명](specs/2026-07-31-primitive-card-frame-design.md) | `current` | 실행·개입 카드 폼팩터, 대상 glyph, 진영별 구조화 설명, 반응형 핸드 | 카드 프레임·대상·설명 표현 변경 |
| [카드 상태 그리드와 호버 툴팁](specs/2026-08-03-card-status-grid-tooltip-design.md) | `current` | 카드에 직접 붙은 상태의 4열 그리드, 표시 데이터 경계, 호버 설명 | 카드 상태 아이콘·툴팁 구현·변경 |
| [카드 아이디어 노트](specs/2026-07-27-card-idea-notebook-design.md) | `superseded` | Markdown 저작 시절의 노트북. 아래 JSON 전환 설계가 대체한다 — 구현 완료 시 `archive/`로 옮긴다 | 참조 전용 |
| [카드 저작 노트북 JSON 전환](specs/2026-08-05-card-authoring-json-notebook-design.md) | `current` | 저작 원본을 Markdown에서 콘텐츠 JSON으로, 구조화 효과 편집기, 저장소 직접 읽기·쓰기, 풀 편성, 생성 스키마 | 카드 저작 도구 구현·변경 |

### 문서 관리

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [문서 정리와 중앙 색인 설계](specs/2026-07-24-document-index-cleanup-design.md) | `current` | 문서 상태, 보관·삭제 기준, 색인 수명주기 | 스펙·계획 추가·완료·대체 |

## 활성 계획과 로드맵

| 문서 | 상태 | 범위 |
|---|---|---|
| [확장성·하드코딩 후속 리팩터링 백로그](plans/2026-07-16-architecture-refactor-backlog.md) | `active` | P1 단일 원본·프리팹·튜닝, P2 표현 경계, §12 2026-07-25 점검 추가 항목, §13 2026-07-30 상태 이상 논의 추가 항목 |
| [전투 상호작용 로그](plans/2026-07-31-combat-interaction-log.md) | `active` | 피해 계산 단계별 내역, 상태 부여·만료 이벤트, 한국어 타임라인 포매터, 개발용 Console 덤프 |
| [프리미티브 카드 프레임 구현](plans/2026-07-31-primitive-card-frame.md) | `active` | 실행·개입 프리팹, 구조화 설명, 대상 glyph, 반응형 핸드와 카드 상태 UI |
| [카드 프레임 다음 세션 인계](plans/2026-08-04-card-frame-session-handoff.md) | `active` | 실행 순서 뱃지 검증, 얕은 호 위의 미세 카드 높낮이 설계·구현, 최종 검증과 프레임 계획 보관 |
| [카드 상태 그리드와 툴팁 구현](plans/2026-08-03-card-status-grid-tooltip.md) | `active` | Task 1–2의 JSON 독립 UI·프리팹은 완료. Task 3–5의 JSON 표시 투영·공유 호버 툴팁 배선은 후속 작업 대기열의 재개 조건까지 보류 |

## 진행 중인 작업 흐름: 카드 콘텐츠 (2026-08-03 인계)

[카드 변형과 런타임 콘텐츠 로딩 설계](specs/2026-07-30-card-mutation-and-runtime-content-design.md)를
여러 계획으로 나눠 구현하는 중이다. 새 세션은 이 절을 먼저 읽고 다음 계획 문서로 들어간다.

| | 계획 | 상태 |
|---|---|---|
| 1 | [카드 콘텐츠 JSON 직렬화·로딩](archive/plans/2026-07-31-card-content-json-loading.md) | **완료·머지** |
| 2 | [상태 콘텐츠 JSON화와 카드 저작 표면 축소](archive/plans/2026-08-02-status-content-and-authoring-surface.md) | **완료·머지** |
| 2.5 | [상태 등록 지점 통합](archive/plans/2026-08-03-status-registration-consolidation.md) | **완료·머지** |
| 3a | [덱·풀·캐릭터 콘텐츠 스키마](archive/plans/2026-08-03-deck-pool-character-content.md) | **완료·머지** |
| 3b | [런타임 콘텐츠 전환](archive/plans/2026-08-03-runtime-content-switch.md) | **완료** |
| 3c | [상태 원본 확정](archive/plans/2026-08-04-status-content-single-source.md) | **완료** |
| 3d | [C# 카드 스펙 제거](archive/plans/2026-08-05-card-spec-removal.md) | **완료** |
| 3.5 | 개입 액션 다형화·카드 스펙 분리 (미작성) | 대기 |
| 4 | 카드 변형 `CardMutation` (미작성) | 대기 |

설계 §4.5의 "콘텐츠 원본 전환"은 한 계획으로 담기에 커서 넷으로 나눴다. 각각 독립 실행 가능하고,
끝난 시점의 트리가 일관된다. 3c와 3d는 서로 독립이라 순서를 바꿔도 된다.

| | 범위 | 선행 |
|---|---|---|
| 3a | ~~덱·풀·캐릭터 스키마·로더·JSON 산출. **순수 코어**, Unity 무변경~~ **완료** | 없음 |
| 3b | ~~`ContentBootstrap` 신설, 소비자를 JSON으로, SO·코드 생성 제거, 등급·태그를 `CardSpec`으로~~ **완료** | 3a |
| 3c | ~~상태 스펙 판별자를 `StatusRegistry`로, `StatusContentDefaults` 제거, `CombatState`의 코드 기본값 제거, `KoreanDescriptionCatalog.Default` 전역 제거 → 주입~~ **완료** | 3b |
| 3d | ~~`StarterPoolSpecs`·`StarterDeckSpecs`·`PartyPrototypeDeckSpecs`·`StarterDeck.Build()`·`PartyPrototypeDeck`·`ContentExportWriter`·`PartyPrototypeCharacterSpecs` 제거. 테스트를 JSON 카탈로그로 전환. (`GeneratedCards`·`ToLiteral`은 3b가 이미 지웠다)~~ **완료** | 3b |

계획 3.5는 개입 액션을 `EffectSpec`처럼 다형화하고 `CardSpec`을 실행/개입으로 쪼갠다
(핸들러가 읽는 파라미터가 액션마다 달라, 지금은 `lock` 카드가 안 쓰는 칸 넷을 들고 있다).

**3d가 3b·3c에서 물려받는 것:** 런타임이 JSON을 읽는다. `ContentBootstrap.Load(콘텐츠루트)`가
**상태** → 카드 → 덱·풀 → 캐릭터 순서로 카탈로그 다섯을 만들어 `GameContent`로 돌려주고,
`BattleScreenController`가 그것을 부팅 1회로 상주시킨다. Unity 쪽 경로 상수는 `UnityContentRoot.Path`
하나뿐이다. 상태가 가장 먼저인 이유는 카드 검증이 "등록된 상태에는 저작이 있다"를 전제하기 때문이다.

**상태 규칙의 원본은 이제 `Content/Statuses/*.json` 하나다** (계획 3c). `StatusSpecJsonConverter`는
판별자 표를 `CombatRegistries.Statuses()`에서 만들고(각 행동이 `NewSpec()`으로 자기 스펙 타입을
답한다), `CombatState`는 카탈로그를 **생성자에서 요구**하며, `KoreanDescriptionCatalog`의 전역
`Default`와 무인자 `CreateDefault()`는 사라졌다 — 설명 카탈로그는 부팅 콘텐츠로 만들어 주입된다.
카드·상태를 코드에서 JSON으로 내보내는 경로는 아예 없다 — 저작은 JSON에서 시작해 JSON으로 끝나며,
그 경로를 지키던 `ContentExportWriter`와 가드 테스트(`WriteAllDoesNotTouchCards`·
`WriteAllDoesNotTouchStatuses`)는 계획 3d(커밋 `ec12b47`)가 함께 지웠다.

**테스트가 콘텐츠를 읽는 진입점은 둘이다:** 코어는 `TestContent.Statuses()`, Unity EditMode는
`UnityTestContent.Statuses()`. **둘 다 호출마다 카탈로그를 새로 만든다** — `StatusContentCatalog.Rules`가
가변이고 그것을 바꿔 보는 테스트가 있어(`StatusTests`의 배율 조절), 인스턴스를 공유하면 한 테스트의
변경이 뒤 테스트로 샌다. 캐시하고 싶어지면 이 사실을 먼저 떠올릴 것.

### 새 세션이 먼저 알아야 할 함정 셋

1. **`[SerializeReference]`를 건드리면 `.asset` YAML도 같은 커밋에서 옮긴다.** Unity는 어셈블리
   한정 타입명과 필드명을 YAML에 박아두고, 없는 멤버는 조용히 버린다. 이 흐름에서 두 번 밟았다 —
   계획 1은 어셈블리 이동으로 27개 카드 에셋의 `Effects`를 `null`로, 계획 2는 필드 제거로 17개
   에셋을 `Count = 0`으로 만들 뻔했다. **헤드리스 테스트는 둘 다 못 잡는다. Unity EditMode만 잡는다.**
2. **`DefaultValueHandling.Ignore`가 열거형 0번 값을 지운다.** `Side.Player`·`CardCategory.Execution`·
   `StatusLifetimeKind.Permanent`가 전부 0이라 JSON에서 사라졌다. 생략이 위험한 필드에는
   `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]`를 붙인다.
3. **카드와 상태 규칙의 원본은 이제 `Content/Cards/*.json`·`Content/Statuses/*.json`뿐이다**
   (계획 3b·3c). 배틀 씬이 `ContentBootstrap`으로 읽고, `CardAsset`·`DeckAsset`·`CardPoolAsset`과
   코드 생성 경로, `StatusContentDefaults`는 사라졌다.
   **남은 이중성 하나:** (a) ~~`StarterPoolSpecs`·`StarterDeckSpecs`·`PartyPrototypeDeckSpecs`가
   골든 테스트 축으로 살아 있다~~ **계획 3d가 지웠다** — 테스트는 이제 `CardFixtures`·
   `UnityCardFixtures` 합성 픽스처와 `TestContent`·`UnityTestContent` JSON 카탈로그, 둘로만 카드를
   얻는다. (b) **적 카드는 아직 JSON이 아니다** — `GoblinDeck`·`WardenDeck`의 순수 C#에서 나오며,
   옮기려면 적 정책·행동 패턴 설계가 딸려 온다(아직 계획 없음).
   그리고 ~~`ContentExportWriter`는 카드도 상태도 쓰지 않는다 — 저작이 JSON에만 있어 다시 쓰면
   지워지기 때문이다(`WriteAllDoesNotTouchCards`·`WriteAllDoesNotTouchStatuses`가 막는다)~~
   **계획 3d가 지웠다** — 코드에서 JSON으로 내보내는 경로 자체가 없다. 저작은 JSON에서 시작해
   JSON으로 끝난다.

### 넘어온 부채

- ~~설명 카탈로그가 전투와 다른 `StatusContentCatalog` 인스턴스를 읽는다.~~ **계획 2.5가 오버로드를,
  계획 3c가 배선을 끝냈다.** `BattleScreenController`가 `KoreanDescriptionCatalog.CreateDefault(_content.Statuses)`로
  만들어 `BattlePresenter`에 주입하므로 카드 텍스트와 전투 규칙이 같은 콘텐츠를 본다. 인자 없는
  `CreateDefault()`와 전역 `Default` 싱글턴은 제거됐다.
- ~~**`CardSO`의 규칙 필드가 검증 없이 남아 있다.**~~ **계획 3b가 해결했다.** `CardAsset` 자체가
  사라졌다. `CardArtCatalog`(id → Sprite, 항목 3개)만 남고 규칙은 전부 JSON이다.
- ~~**`BattleScreenController`에 책임이 몰려 있다.**~~ **[전투 화면 컴포넌트 분해 계획](archive/plans/2026-08-04-battle-screen-decomposition.md)이
  해결했다** (2026-08-04). 467줄 → 347줄, `[SerializeField]` 18개 → 8개. 표현 변환은
  `BattlePresenter`, 유닛은 `BattleUnitsView`, 파일 셋은 `BattlePilesView`, HUD는 `BattleHudView`가
  가져갔고 씬은 `BattleSceneBuilder`가 재생성했다. **남은 후속:** 입력 핸들러 다섯이 아직
  컨트롤러에 있다 — 설계 §4.1대로 P2(코어 이벤트 확충) 이후로 미룬다.
- ~~**상태 JSON이 코드 기본값 없이는 파싱되지 않는다.**~~ **계획 3c가 해결했다.**
  `StatusSpecJsonConverter`가 판별자 표를 `CombatRegistries.Statuses()`에서 만든다 — 각 행동이
  `NewSpec()`으로 자기 스펙 타입을 답하므로 코드에 값 목록이 남지 않는다.

### 현재 수치 (계획 3d 완료 시점, 2026-08-05 실측)

헤드리스 **511/511**, Unity EditMode **659 total / 652 passed / 0 failed / 7 skipped**,
카드 JSON **26**(플레이어 22 + fixture 4, 전부 등급·태그 보유), 상태 JSON **11**,
덱 JSON **2**, 풀 JSON **1**, 캐릭터 JSON **2**. Unity 씬은 `FateWeaverBattle`·`SampleScene` 둘.
헤드리스 명령은 `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`.
계획 3d가 대조·중복 테스트를 지우고 규칙 테스트를 합성 픽스처로 옮기면서 총계가 계획 3c 시점
(헤드리스 533, Unity 682)보다 줄었다 — 실패가 늘어난 것이 아니라 테스트 자체가 정리된 결과다.

## 후속 작업 대기열

- [ ] **카드 상태 UI의 JSON 런타임 연계** — 현재 완료된 범위는 JSON과 독립적인
  `CardStatusDisplayContent`·`ICardStatusDisplaySource` 경계, 4열 하향 그리드, 상태 아이콘·툴팁
  컴포넌트와 프리팹이다. 상태 원본 확정은 계획 3c가 끝냈고(`master` 머지는 사용자 승인 대기),
  카드별 부착 상태 키 계약까지 `master`에 반영된 뒤
  [카드 상태 그리드와 툴팁 구현 계획](plans/2026-08-03-card-status-grid-tooltip.md)의 Task 3–5를 재개한다.
  JSON의 상태 키·표시 이름·설명·아이콘 키를 표시 투영에 연결하고, 손패·실행 레일·더미 팝업이 하나의
  공유 툴팁을 쓰도록 배선한다. 카드에는 카드에 직접 붙은 상태만 표시하며, SO나 C# 문자열 임시
  fallback은 추가하지 않는다.

- [ ] **디버프 3종의 Unity 표시 확인** — 약화·취약·손상은 코어에 구현되어 있고
  [보관된 계획](archive/plans/2026-07-30-status-rule-and-debuffs.md)이 헤드리스로 검증했다. 남은 것은
  전투 화면에서 세 상태가 유닛에 옳게 표시되는지 **눈으로** 보는 것뿐이다(규칙 17: 시각 확인은
  사용자 몫). 표시가 어긋나면 그때 별도 작업으로 잡는다.
- [ ] **`StatusLifetime` count 의미 단일화** — 상태마다 `count`가 "남은 턴"인지 "세기"인지 다르고,
  지금은 상태 콘텐츠의 수명 종류가 그것을 정한다. 보관된 상태 규칙 계획이 "영향 범위가 넓어 별도
  계획으로 분리한다"고 명시하고 미뤄둔 항목이다. `StatusBag`·`ApplyStatusPayload`·`ApplyStatusSpec`·
  카드 JSON·설명 문법의 `LifetimeSuffix`에 걸친다. 착수하려면 먼저 스펙이 필요하다.

## 재설계가 필요한 영역

| 영역 | 상태 | 이유와 재개 기준 |
|---|---|---|
| 런 원 사이클 | `needs-redesign` | 과거 설계가 `재화 없음`, `사망 카드 인계 없음`, 이전 보상 모델을 전제한다. 재개 시 현재 카드풀 문서의 유산·소유권 규칙을 기준으로 새 스펙을 작성한다. |
| 카드 유효 수치 색상 피드백 | `needs-redesign` | 카드 변형과 런·전투 상태 중앙관리 작업이 원본값·유효값의 표현 계약을 확정한 뒤 피해·방어·비용 등 변경된 텍스트 span만 색으로 표시한다. 상태 아이콘은 사용하지 않는다. |

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
