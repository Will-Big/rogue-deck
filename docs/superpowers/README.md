# Fate Weaver 설계·계획 문서 색인

- 개정일: 2026-08-28
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
| [카드 변형과 런타임 콘텐츠 로딩](specs/2026-07-30-card-mutation-and-runtime-content-design.md) | `current` | OwnedCard의 영구·전투 변형 2목록과 Effective 카드, 코드 생성의 JSON 런타임 로딩 대체, UGC 경계 | 카드 강화·변경 구현, 모딩 지원 착수 |

카드 디자인을 새 세션에서 이어갈 때는
[캐릭터 및 카드풀 설계 규칙](specs/2026-07-20-character-card-pools-design.md)부터 읽는다.

### UX와 표현

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [전투 화면 시각 설계](specs/2026-07-10-battle-scene-visual-design.md) | `current` | 전투 화면의 상위 구도와 표현 방향 | 전투 화면 구조·연출 변경 |
| [전투 화면 컴포넌트 분해](specs/2026-08-04-battle-screen-decomposition-design.md) | `current` | 전투 화면 Unity 컴포넌트의 경계와 책임 분배 | 전투 화면에 컴포넌트·표현 추가, 캐릭터 아트 도입 |
| [턴 재생 계층](specs/2026-08-30-turn-playback-design.md) — 개요는 [HTML](specs/2026-08-30-turn-playback-design.html) | `active` | 이벤트 타임라인의 비트 분할·시간축 재생·배속·스킵, 이벤트별 연출자 레지스트리 | 턴 해석 결과에 연출을 붙이거나 재생 동작을 바꿀 때 |
| [위치 대상과 카드 텍스트](specs/2026-07-27-position-targeting-card-text-design.md) | `current` | 다섯 위치 범위와 자신, 실행 시 대상 고정, 대상 칸과 진영별 본문 | 카드 대상·설명·프레임 설계 |
| [프리미티브 카드 프레임과 구조화 설명](specs/2026-07-31-primitive-card-frame-design.md) | `current` | 실행·개입 카드 폼팩터, 대상 glyph, 진영별 구조화 설명, 반응형 핸드 | 카드 프레임·대상·설명 표현 변경 |
| [카드 상태 그리드와 호버 툴팁](specs/2026-08-03-card-status-grid-tooltip-design.md) | `current` | 카드에 직접 붙은 상태의 4열 그리드, 표시 데이터 경계, 호버 설명 | 카드 상태 아이콘·툴팁 구현·변경 |
| [카드 아이디어 노트](archive/specs/2026-07-27-card-idea-notebook-design.md) | `archived` | Markdown 저작 시절의 노트북. 아래 JSON 전환 설계가 대체했다 — 구현 완료로 보관 | 참조 전용 |
| [카드 저작 노트북 JSON 전환](specs/2026-08-05-card-authoring-json-notebook-design.md) | `current` | 저작 원본을 Markdown에서 콘텐츠 JSON으로, 구조화 효과 편집기, 저장소 직접 읽기·쓰기, 풀 편성, 생성 스키마. **계획 A~D 완료** | 카드 저작 도구 구현·변경 |

### 문서 관리

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [문서 정리와 중앙 색인 설계](specs/2026-07-24-document-index-cleanup-design.md) | `current` | 문서 상태, 보관·삭제 기준, 색인 수명주기 | 스펙·계획 추가·완료·대체 |

### 작업 환경과 도구

| 문서 | 상태 | 권위 범위 | 다음 사용 시점 |
|---|---|---|---|
| [graphify 카드 그래프 통합](specs/2026-08-28-graphify-card-graph-design.md) | `current` | 카드·상태 JSON의 그래프 편입, 재생성 단일 명령, 카드·아키텍처 시각화 | 그래프 도구 확장(새 프루닝 규칙, 새 뷰) |

## 활성 계획과 로드맵

| 문서 | 상태 | 범위 |
|---|---|---|
| [확장성·하드코딩 후속 리팩터링 백로그](plans/2026-07-16-architecture-refactor-backlog.md) | `active` | P1 단일 원본·프리팹·튜닝, P2 표현 경계, §12 2026-07-25 점검 추가 항목, §13 2026-07-30 상태 이상 논의 추가 항목 |
| [전투 상호작용 로그](archive/plans/2026-07-31-combat-interaction-log.md) | **완료·머지·보관 (2026-08-28)** | 피해 계산 단계별 내역, 상태 부여·만료 이벤트, 한국어 타임라인 포매터, 개발용 Console 덤프 |
| [전투 타임라인 이벤트 확장](archive/plans/2026-08-28-combat-timeline-event-expansion.md) | **완료·머지·보관 (2026-08-29)** | 캐릭터별 HP 변화, 운명력 적립, 상태 소비, 카드 귀속 버프, 대형 이동, 취소 카드 부분 피해의 이벤트화와 이벤트당 Console 로그 |
| [프리미티브 카드 프레임 구현](plans/2026-07-31-primitive-card-frame.md) | `active` | 실행·개입 프리팹, 구조화 설명, 대상 glyph, 반응형 핸드와 카드 상태 UI |
| [카드 프레임 다음 세션 인계](plans/2026-08-04-card-frame-session-handoff.md) | `active` | 실행 순서 뱃지 검증, 얕은 호 위의 미세 카드 높낮이 설계·구현, 최종 검증과 프레임 계획 보관 |
| [카드 상태 그리드와 툴팁 구현](plans/2026-08-03-card-status-grid-tooltip.md) | `active` | Task 1–2의 JSON 독립 UI·프리팹은 완료. Task 3–5의 표시 투영·공유 호버 툴팁 배선은 **선행 없이 재개 가능**(2026-08-28 정정 — 후속 작업 대기열 참고) |

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
| 3.5 | [개입 액션 다형화·카드 스펙 분리](archive/plans/2026-08-06-intervention-action-polymorphism.md) | **완료** |
| 4 | 카드 변형 `CardMutation` (미작성) | 대기 |

설계 §4.5의 "콘텐츠 원본 전환"은 한 계획으로 담기에 커서 넷으로 나눴다. 각각 독립 실행 가능하고,
끝난 시점의 트리가 일관된다. 3c와 3d는 서로 독립이라 순서를 바꿔도 된다.

| | 범위 | 선행 |
|---|---|---|
| 3a | ~~덱·풀·캐릭터 스키마·로더·JSON 산출. **순수 코어**, Unity 무변경~~ **완료** | 없음 |
| 3b | ~~`ContentBootstrap` 신설, 소비자를 JSON으로, SO·코드 생성 제거, 등급·태그를 `CardSpec`으로~~ **완료** | 3a |
| 3c | ~~상태 스펙 판별자를 `StatusRegistry`로, `StatusContentDefaults` 제거, `CombatState`의 코드 기본값 제거, `KoreanDescriptionCatalog.Default` 전역 제거 → 주입~~ **완료** | 3b |
| 3d | ~~`StarterPoolSpecs`·`StarterDeckSpecs`·`PartyPrototypeDeckSpecs`·`StarterDeck.Build()`·`PartyPrototypeDeck`·`ContentExportWriter`·`PartyPrototypeCharacterSpecs` 제거. 테스트를 JSON 카탈로그로 전환. (`GeneratedCards`·`ToLiteral`은 3b가 이미 지웠다)~~ **완료** | 3b |

계획 3.5는 개입 액션을 `EffectSpec`처럼 다형화하고 `CardSpec`을 실행/개입으로 쪼갰다. 저작은
`InterventionSpec` + `InterventionSpecCatalog` + 컨버터, 런타임은 `IInterventionPayload`이며
(효과의 `IEffectPayload`와 같은 형태), `lock` 카드가 들고 있던 빈 칸 넷이 사라졌다. 카드 한 장은
개입 액션을 하나만 갖는다 — 복수 개입은 대상 묶기 규칙과 비용 귀속 설계가 딸려 오므로 범위 밖이다.

**계획 4의 선행은 2026-08-06 기준 전부 풀렸다.** 3a~3d와 3.5가 끝나 콘텐츠 원본이 JSON 하나로
확정됐으므로, 이 흐름에서 남은 것은 계획 4를 쓰는 일뿐이다.

**3d가 3b·3c에서 물려받는 것:** 런타임이 JSON을 읽는다. `ContentBootstrap.Load(콘텐츠루트)`가
**상태** → 카드 → 덱·풀 → 캐릭터 순서로 카탈로그 다섯을 만들어 `GameContent`로 돌려주고,
`BattleScreenController`가 그것을 `_content`에 담아 상주시킨다 — 부팅 시가 아니라 **전투 화면
진입 시** 첫 `StartSession()`에서 만들어지고, `static`이 아니므로 그 컨트롤러와 수명을 같이한다
(2026-08-28 정정). Unity 쪽 경로 상수는 `UnityContentRoot.Path`
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
   얻는다. (b) **적 카드는 아직 JSON이 아니다** — `GoblinDeck`의 순수 C#에서 나오며,
   옮기려면 적 정책·행동 패턴 설계가 딸려 온다(아직 계획 없음). `WardenDeck`은 2026-08-31에
   삭제했다 — 몬스터를 묶음 기반으로 재작업할 예정이다.
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

### 현재 수치 (계획 D 완료 시점, 2026-08-28 실측)

헤드리스 **526/526**(변동 없음 — 계획 D는 C# 코드를 건드리지 않았다), 노트북 **150/150**
(계획 C 완료 시점 168 → 150. 계획 D가 Markdown 저작 경로 테스트 약 55개를 지우고 저장소 경로
테스트를 더한 결과다. 전체 리뷰 이후 최종 수정으로 148 → 150이 됐다), Unity EditMode **672 total / 665 passed / 0 failed / 7 skipped**
(EditMode는 계획 3.5 시점 수치이며 계획 A 이후 재측정하지 않았다 — 신규 테스트는 `Tests/Headless`가
포함하는 EditMode 폴더에 있으므로 Unity 쪽도 1 늘어날 것이다).
카드 JSON **26**(실행 22 + 개입 4 중 fixture 4, 플레이어 카드는 전부 등급·태그 보유), 상태 JSON **11**,
덱 JSON **2**, 풀 JSON **1**, 캐릭터 JSON **2**. 프로젝트 씬은 `FateWeaverBattle`·`SampleScene` 둘
(`Settings/Scenes/URP2DSceneTemplate`은 URP 템플릿 자산이며 프로젝트 씬이 아니다).

계획 D는 `Tools/card-idea-notebook/index.html`을 Markdown 저작 경로 제거로 3,206줄까지 줄였고
(`<script>` 블록도 셋에서 `data-card-idea-core`·`data-repo-ui` 둘로 줄었다), `index.test.mjs`는
1,955줄이다. `시작 카드 풀.md`는 지워졌고 `적 타입 A.md`는 참고 메모로 남았다. 실행 계획은
[노트북 저장소 반영](archive/plans/2026-08-11-notebook-repo-write.md)에 있다.

검증 명령 둘:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

노트북 명령에 **글로브가 필요하다.** `node --test Tools/card-idea-notebook/`처럼 디렉터리를 주면
Node 24가 그것을 모듈 경로로 해석해 `MODULE_NOT_FOUND`로 죽는다(2026-08-07 실측, Node v24.13.1).

계획 3d가 대조·중복 테스트를 지우고 규칙 테스트를 합성 픽스처로 옮기면서 총계가 계획 3c 시점
(헤드리스 533, Unity 682)보다 줄었고, 계획 3.5가 다형화 검증과 **저장소 카드 26장의 왕복 바이트
동일성 테스트**를 더하며 다시 늘었다(헤드리스 511 → 525). 그 왕복 테스트는 카드 JSON의 키 순서와
생략 규칙을 통째로 잠그므로, C# 저작 타입의 필드를 재배치하면 여기서 먼저 실패한다.

계획 A는 헤드리스에 `AuthoringSchemaExportTests` 하나(525 → 526)를, 노트북에 44개(58 → 102)를
더했다. 노트북 쪽의 축은 **저장소 카드 26장·풀 1개의 왕복 바이트 동일성**이며, C# 왕복 테스트와
같은 것을 브라우저 쪽에서 잠근다 — 둘이 어긋나면 노트북이 저작하지 않은 카드까지 diff에 띄운다.
계획 C는 노트북에 42개를 더했다(126 → 168). 미반영 보관소·uid·편집 명령이 전부 코어의 순수
함수이고, 폼과 이벤트는 마크업 존재 검사만 자동화된다.
계획 B는 노트북에 24개를 더했다(102 → 126). 집계·검색·요약 규칙은 코어의 순수 함수라 단위
테스트가 덮고, 브라우저 API와 DOM은 마크업 존재 검사만 자동화된다.

## 후속 작업 대기열

- [x] **적이 죽어도 그 적의 남은 카드가 실행된다 — 2026-08-30 수정 완료.** 파티원만 `OwnerDied`로
  취소되던 스윕에 적 사망을 대칭으로 넣었다. `TurnResolver.CollectNewlyDeadOwnerIds`가
  `PartyMemberDied`와 `EnemyDied`에서 모두 소유자 id를 뽑고, 그 id로
  `MarkOwnerDiedForFutureCards`가 실행 순서상 뒤쪽 카드를 취소한다
  (`Assets/Core/Combat/TurnResolver.cs:144`·`270`). 헤드리스 557 → 562, 실패 0.

  선행 확인이던 소유자 귀속은 **정책 API를 바꾸지 않고 귀속을 정직하게** 만드는 쪽으로 정했다.
  `IEnemyTurnPolicy.CardsForTurn`은 `IReadOnlyList<CardDefinition>`만 돌려주어 어느 적의 카드인지
  말할 수단이 아예 없으므로(`Assets/Core/Simulation/Enemies/IEnemyTurnPolicy.cs:16`), 올바른 귀속은
  인터페이스 재설계다. 대신 `DeckCombatSession`이 **적이 정확히 하나일 때만** 소유자를 확정하고
  둘 이상이면 비워 둔다(`Assets/Core/Simulation/DeckCombatSession.cs:387`) — `CardActor`가 이미
  문서화한 규약과 같다(`Assets/Core/Combat/CardActor.cs:3`). 이러면 다중 적에서 남의 카드가
  잘못 취소되는 일은 생기지 않고, 그 경우 죽은 적의 카드는 여전히 실행된다. 다중 적을 실제로
  도입할 때 정책 API를 소유자 인지형으로 재설계하면서 함께 닫는다.

- [ ] **카드 상태 UI의 JSON 런타임 연계 — 막혀 있지 않다. 배선만 남았다.** 완료된 범위는 JSON과
  독립적인 `CardStatusDisplayContent`·`ICardStatusDisplaySource` 경계, 4열 하향 그리드, 상태
  아이콘·툴팁 컴포넌트와 프리팹이다. 상태 원본 확정(계획 3c)은 `master`에 들어갔다.

  ~~막고 있는 것은 "카드별 부착 상태 키 계약"이며, 그것이 아직 존재하지 않는다~~
  **2026-08-28 정정: 그 계약은 설계할 필요가 없다. 이미 코드에 있다.**
  2026-08-06 조사가 "`Content/Cards/*.json` 26장 어디에도 부착 상태를 적는 키가 없다"를 확인하고
  카드 JSON에 새 키를 설계해야 한다고 결론지었는데, **찾을 곳이 틀렸다.** 카드에 붙는 상태는
  저작 데이터가 아니라 **런타임 인스턴스 상태**다.

  - `ExecutionCardInstance.Statuses`(`StatusBag`)가 이미 있다 — `PartyMember`·`Enemy`와 같은 형태다.
  - `StatusScope`가 `Entity`와 `CardInstance`를 가른다. `RewardSuppressionBehavior`(보상 무효)가
    실제로 `CardInstance`로 등록되어 있고 `TurnResolver`가 `card.Statuses`에서 꺼내 쓴다.

  따라서 카드 JSON에 새 키를 넣으면 안 된다 — 넣으면 런타임 실상과 저작본이 어긋난다.
  [카드 상태 그리드와 툴팁 구현 계획](plans/2026-08-03-card-status-grid-tooltip.md)의 Task 3–5는
  **선행 없이 바로 재개할 수 있다.**

  (재개하면: `ExecutionCardInstance.Statuses`의 키 목록 → `ICardStatusDisplaySource`로 표시 콘텐츠
  해석 → `CardStatusPresentation` 조립 → 아이콘·툴팁 배선. 표시 콘텐츠의 출처는 상태 JSON
  (`displayName` 등)과 `CardArtCatalog`(아이콘 Sprite)이며, `ICardStatusDisplaySource` 구현체가
  아직 하나도 없으므로 그것을 만드는 것이 첫 작업이다. 카드에는 카드에 직접 붙은 상태만 표시하며,
  SO나 C# 문자열 임시 fallback은 추가하지 않는다.)

- [ ] **디버프 3종의 Unity 표시 확인** — 약화·취약·손상은 코어에 구현되어 있고
  [보관된 계획](archive/plans/2026-07-30-status-rule-and-debuffs.md)이 헤드리스로 검증했다. 남은 것은
  전투 화면에서 세 상태가 유닛에 옳게 표시되는지 **눈으로** 보는 것뿐이다(규칙 17: 시각 확인은
  사용자 몫). 표시가 어긋나면 그때 별도 작업으로 잡는다.
- [ ] **계획 3.5가 남긴 콘텐츠·구조 항목 셋** — 전부 코드는 준비돼 있고 결정만 남았다.
  (a) **복수 개입** — 카드 한 장에 개입 액션 여럿. `InterventionPlayResolver`는 이미 리스트를 받지만,
  `CardDefinition.InterventionAction`이 단수이고 `DeckCombatSession`이 카드당 `TargetingRequirement`
  하나만 뽑으며 비용이 액션마다 차감되므로, 대상 묶기 규칙과 비용 귀속을 함께 설계해야 한다.
  (b) **`lock` 개입 카드와 `SwapExecutionOrderSpec.TargetSide`를 쓰는 카드가 없다** — 액션은 등록돼
  있고 테스트도 덮지만 대응 콘텐츠가 없다. 카드 디자인 결정이다.
  (c) **`BattleScreenController`의 입력 핸들러 다섯** — 전투 화면 분해가 남긴 후속이며 설계 §4.1대로
  백로그 P2(코어 이벤트 확충) 이후로 미룬다.

- [ ] **상태 수치를 담을 수명별 층이 없고, 저작값 읽는 통로가 중앙에 쌓인다.** 2026-08-28 조사
  결과이며 문제가 둘이다. 둘 다 같은 작업에서 풀리므로 따로 착수하면 두 번 뜯게 된다.

  **문제 1 — 수명이 맞는 그릇이 없다.**
  변경에는 살아야 하는 기간이 있다("이번 전투 동안 취약 200%", "이번 런 동안 독 성장량 +2").
  변경을 그 기간만큼 사는 객체에 담으면 지우는 코드 없이 알아서 사라진다. 그런데 지금 상태 수치가
  담기는 곳은 기간이 맞지 않는다.

  | 변경이 살아야 할 기간 | 맞는 그릇 | 현재 |
  |---|---|---|
  | 이 전투 | `CombatState` | 객체는 전투마다 새로 만들어지는데 **자기 수치 표가 없다** — `StatusRules`가 카탈로그 것을 가리키는 위임일 뿐이다 |
  | 이 런 | `RunState` | 객체는 있는데 **전투와 연결돼 있지 않다** (참조하는 파일이 자기 자신과 자기 테스트뿐) |
  | 안 변함 | `GameContent` | 있다. 지금은 전부 여기 쓴다 |

  `GameContent`(및 `StatusContentCatalog`)는 **콘텐츠 JSON을 읽어 만든 런타임 객체**다. 부팅 시가
  아니라 전투 화면 진입 시 `BattleScreenController.Start()`의 첫 `StartSession()`에서 만들어지고,
  `_content`는 `static`이 아니라 그 컨트롤러의 인스턴스 필드다. 다만 `StartSession`이 HUD 재시작
  버튼에도 배선돼 있고 그때 `_content`를 재사용하므로 **전투를 다시 시작해도 같은 인스턴스**다.
  그래서 `StatusContentCatalog.Rules`에 쓴 값은 전투 경계를 넘어 남는다.

  **가변인 것 자체는 결함이 아니다** — 유물 같은 효과가 수치를 바꾸게 하려는 의도다. 결함은 그
  변경이 **지워질 시점이 없다**는 것이다. 아직 피해가 없는 이유는 프로덕션에서 `Rules.Set`을 부르는
  코드가 하나도 없어서다. 테스트는 이미 이 누수를 밟아 `TestContent.Statuses()`가 호출마다 카탈로그를
  새로 만드는 것으로 우회한다. 프로덕션 소비자는 `TurnResolver`의 `state.StatusRules` 한 곳뿐이다.

  **전투용 사본을 뜨는 방식은 안 된다** — [카드 변형 설계](specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.3이
  이미 기각했다(전투 중 발생한 런 지속 변경이 사본과 함께 사라진다). 그 설계가 카드에 대해 정해 둔
  `Source + Permanent(런) + Combat(전투) → Effective`가 이 문제의 해법이며, **카드에만 문서화돼 있고
  구현은 없다**(`CardMutation` 타입이 저장소에 없고 `OwnedCard`는 `Def`+`OwnerId`뿐). 상태 수치·덱·
  캐릭터는 문서조차 없다.

  **문제 2 — 저작값 읽는 통로가 상태마다 다르고 중앙에 쌓인다.**
  상태의 런타임 행동(`StatusBehavior` 11개)과 저작 스펙(`StatusSpec` 서브클래스 3개)은 규칙 9대로
  갈려 있어, 상태를 추가해도 클래스 하나와 등록 한 줄이면 된다. 그런데 그 저작값을 **읽는** 쪽은
  `StatusContentCatalog`에 상태별 메서드로 쌓인다.

  | 통로 | 담는 것 | 해당 상태 | 가변? |
  |---|---|---|---|
  | `Rules` (`StatusRuleSet`) | `multiplierPercent` | 취약 150 · 약화 75 · 손상 75 | 가변 |
  | `GrowthPerTurnOf` | `growthPerTurn` | 독 | 읽기 전용 |
  | `ExecutionOrderDeltaOf` | `executionOrderDelta` | 가속 · 감속 | 읽기 전용 |
  | 없음 | — | 방어 · 전염 · 독 잠복 · 독 안정 · 보상 무효 | — |

  `is PoisonStatusSpec spec ? spec.GrowthPerTurn : 0`은 중앙 switch를 손으로 편 것이고, 파라미터를
  가진 상태를 추가할 때마다 메서드가 하나 는다. 그리고 셋 중 하나만 가변이라 "유물이 독 성장량 +1"은
  손댈 통로 자체가 없다.

  원인은 방향이다. 행동은 `NewSpec()`으로 자기 스펙 타입을 아는데, 읽을 때 카탈로그를 거치느라 그
  지식이 버려지고 카탈로그가 대신 알게 됐다. **카탈로그가 `StatusKey → StatusSpec`만 돌려주고 해석은
  각 행동이 하면** 중앙이 자라지 않고, 수명별 층을 얹을 자리도 한 곳으로 모인다.

  착수하려면 먼저 결정해야 할 것 둘:
  1. §4.3의 `Source + Permanent(런) + Combat(전투) → Effective` 모델을 상태 수치·덱·캐릭터에도
     **그대로 적용**할 것인가, 데이터 종류마다 다른 모양이 필요한가.
  2. 계획 4의 범위를 카드에 한정할 것인가, 층 전반으로 넓힐 것인가.

- [ ] **`StatusLifetime` count 의미 단일화** — 상태마다 `count`가 "남은 턴"인지 "세기"인지 다르고,
  지금은 상태 콘텐츠의 수명 종류가 그것을 정한다. 보관된 상태 규칙 계획이 "영향 범위가 넓어 별도
  계획으로 분리한다"고 명시하고 미뤄둔 항목이다. `StatusBag`·`ApplyStatusPayload`·`ApplyStatusSpec`·
  카드 JSON·설명 문법의 `LifetimeSuffix`에 걸친다. 착수하려면 먼저 스펙이 필요하다.

  **2026-08-28 논의로 전제가 좁아졌다.** 사용자가 밝힌 설계 규칙은 이렇다 — 상태이상 **임시
  객체**(캐릭터 또는 카드에 붙는다)는 `count`만 관리하고, **정보 객체**(중앙 공유)가 `magnitude`를
  관리한다. `count`는 남은 턴·충전·스택처럼 전투 중 변하는 수치이고 상태마다 해석이 다르며,
  `magnitude`는 임시 객체들이 참조하는 대체로 고정된 공유값이다. 둘 다 카드·유물이 바꿀 수 있다.

  그 규칙에 비추면 **저작 경계는 고칠 게 없다.** 카드가 적는 `count` 하나는 임시 객체의 초기
  `count`이므로 필드가 하나인 것이 맞고, `magnitude`는 카드가 아니라 상태 JSON에 있어야 하는데
  이미 그렇다(`poison.json`의 `growthPerTurn`). 앞서 "카드 JSON의 `count`를 `turns`와 `magnitude`로
  쪼개자"는 방향이 나왔으나 이 규칙과 어긋나므로 채택하지 않는다.

  남는 것은 **런타임 이름이 규칙과 뒤집혀 있다**는 점이다. `StatusInstance`가 `Count`와 `Magnitude`를
  둘 다 들고 있고, 독의 경우 전투 중 변하는 스택이 `Instance.Magnitude`(규칙상 `count`여야 한다),
  고정 공유값인 성장량이 `Catalog.GrowthPerTurnOf`(규칙상 `magnitude`)다. 위치는 맞는데 이름이
  서로 바뀌어 있고, 정보 객체 쪽에는 일반화된 `magnitude` 슬롯 없이 상태별 전용 접근자만 있다.
  이 정리는 위의 **수명별 층** 항목과 같은 작업에 속한다 — 따로 하면 두 번 뜯는다.

## 재설계가 필요한 영역

| 영역 | 상태 | 이유와 재개 기준 |
|---|---|---|
| 런 원 사이클 | `needs-redesign` | 과거 설계가 `재화 없음`, `사망 카드 인계 없음`, 이전 보상 모델을 전제한다. 재개 시 현재 카드풀 문서의 유산·소유권 규칙을 기준으로 새 스펙을 작성한다. |
| 카드 유효 수치 색상 피드백 | `needs-redesign` | 카드 변형과 런·전투 상태 중앙관리 작업이 원본값·유효값의 표현 계약을 확정한 뒤 피해·방어·비용 등 변경된 텍스트 span만 색으로 표시한다. 상태 아이콘은 사용하지 않는다. |

과거 런 설계와 계획은 [보관 문서 색인](archive/README.md)에서 참고할 수 있다.

## 문서 수명주기

1. 새 스펙·계획은 **`<이름>.html`(사람 검수용) + `<이름>.md`의 `## 상세`(세션 인계용)** 두 파일로
   쓴다(2026-08-30 결정). HTML은 인라인 SVG 다이어그램으로 구조·대안·실행 예시를 보이고, 사람은
   그것만 보고 승인할 수 있어야 한다. 에이전트는 HTML을 방향 확인에만 쓰고 실행 근거는 `.md`의
   상세에서 얻는다. 골격과 제약은 `AGENTS.md` 규칙 28·29에 있다. 기존 문서는 소급해 고치지 않고,
   수정할 일이 생겼을 때 이 골격으로 맞춘다.
2. 새 스펙·계획을 추가할 때 이 색인을 같은 커밋에서 갱신한다.
3. 구현이 끝난 세부 계획과 구현 기록은 `archive/plans/`로 옮긴다.
4. 대체된 설계는 구현의 역사적 근거가 있으면 `archive/specs/`로 옮긴다.
5. 승인되지 않은 WIP와 유효한 내용이 완전히 흡수된 문서는 삭제한다.
6. 현행 `specs/`와 `plans/`에는 `current` 또는 `active` 문서만 둔다.
7. 보관 문서는 현재 규칙의 권위가 아니며, 현재 문서가 명시적으로 연결할 때만 참고한다.

## 보관소

완료된 설계·계획·구현 기록은 [보관 문서 색인](archive/README.md)에 분리되어 있다.
