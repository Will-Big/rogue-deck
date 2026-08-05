# 덱·풀·캐릭터 콘텐츠 스키마 구현 계획 (카드 콘텐츠 계획 3a)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-08-03
- 상태: `완료` (2026-08-03 구현·머지)
- 권위 문서: [`specs/2026-07-30-card-mutation-and-runtime-content-design.md`](../../specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.5
- 선행 계획: [`archive/plans/2026-08-03-status-registration-consolidation.md`](./2026-08-03-status-registration-consolidation.md)
- 후속 계획: 3b(런타임 전환) → 3c(상태 원본 확정) · 3d(C# 카드 스펙 제거). 아래 [로드맵](#계획-3의-분할-로드맵) 참고
- 브랜치: `feat/deck-pool-character-content`

**Goal:** 시작 덱 10장·시작 풀 22장·파티 멤버 둘의 목록을 JSON 콘텐츠로 만든다. 지금 이 목록은
순수 C# 상수와 Unity SO 두 곳에 흩어져 있다.

**Architecture:** 카드·상태 콘텐츠가 이미 쓰는 형태(`Spec` → `Loader` → `Catalog`)를 그대로 복제한다.
새 패턴을 발명하지 않는다(규칙 13). 덱·풀·캐릭터는 **카드 id 목록**만 담는다 — 카드 규칙의 원본은
`Content/Cards/*.json` 하나다.

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), NUnit, Newtonsoft.Json,
`FateWeaver.Core`(UnityEngine 미참조)

## Global Constraints

- 헤드리스 테스트 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- 착수 시점 기준선: **헤드리스 447/447**, Unity EditMode **521/521**, 카드 JSON **26**, 상태 JSON **11**
- 이 계획은 **순수 코어 작업이다.** Unity 런타임·씬·프리팹·`.asset`을 건드리지 않는다 — 그 전환은 3b가 한다
- `FateWeaver.Core`에서 `UnityEngine`을 참조하지 않는다 (규칙 6)
- 결정론: 반복 순서가 사전 구현·파일 시스템 순서에 의존하지 않는다 (규칙 7)
- 튜닝 수치를 계산식에 박지 않는다 (규칙 8)
- 콘텐츠 경로는 루트 상수 하나만 두고 나머지는 폴더 스캔이다 (규칙 2·3, 설계 §4.5)
- 워킹 트리를 깨끗이 남긴다 (규칙 18)
- 문서 색인을 같은 커밋에서 갱신한다 (규칙 20)
- C# 9 한계: `record struct` 금지, 기본 인터페이스 구현 금지, 파일 범위 네임스페이스 금지
- Unity 배치에서 `-runTests`와 `-quit`를 **함께 쓰지 않는다**

## 목표 상태

| | 지금 | 이후 |
|---|---|---|
| 시작 덱 10장 목록 | `StarterDeckSpecs.cs` + `StarterDeck.asset` | **`Content/Decks/starter.json`** |
| 시작 풀 22장 목록 | `StarterPoolSpecs.Build()` + `StarterPool.asset` | **`Content/Pools/starter.json`** |
| 파티 멤버 둘 | `PartyPrototypeRoster.cs` + `CharacterSO/*.asset` | **`Content/Characters/*.json`** |
| 목록 검증 | 없음 (SO와 C#이 어긋나도 조용하다) | **로더가 거부** — 없는 카드 id, 중복 id, 없는 덱 참조 |

이 계획이 끝나도 **런타임은 여전히 SO를 읽는다.** JSON은 커밋되어 테스트가 잠그는 상태로 대기하고,
3b가 소비자를 옮긴다. 계획 1·2가 카드·상태에 대해 밟은 것과 같은 순서다.

## 스키마

```json
// Assets/StreamingAssets/Content/Decks/starter.json
{
  "id": "starter",
  "cards": ["probing_strike", "delayed_strike", "quick_cover", "..."]
}

// Assets/StreamingAssets/Content/Pools/starter.json
{
  "id": "starter",
  "cards": ["vanguard_slash", "parry_strike", "..."]
}

// Assets/StreamingAssets/Content/Characters/member_a.json
{
  "id": "member_a",
  "displayName": "파티원 A",
  "deck": "starter"
}
```

**덱은 중복 id를 허용하고 풀은 허용하지 않는다.** 덱은 같은 카드를 여러 장 담을 수 있고(현재
`DeckAsset.Entry.Count`가 하는 일), 풀은 후보 집합이라 중복이 저작 실수다(`CardPoolAsset.Validate`가
이미 그렇게 판정한다).

**캐릭터의 색은 JSON에 넣지 않는다.** 색 틴트가 이 게임의 아트이므로 표현 데이터이고, 설계 §4.5의
"Unity는 표현만 담당"에 따라 `CharacterAsset`(id → Color)에 남는다. 3b가 `CharacterAsset`을 그
매핑으로 축소한다.

**등급·태그는 이 계획에서 다루지 않는다.** 카드에 딸린 데이터이므로 `CardSpec`으로 가야 하고,
그 이동은 `CardPoolAsset.Validate`를 `AuthoringValidator`로 옮기는 3b의 일이다.

## 구현 결과 (2026-08-03)

헤드리스 **447 → 487**(추가 40), Unity 무변경. 계획대로 순수 코어 작업이었다.
`.asset`·씬·프리팹·프로젝트 설정을 건드리지 않았다.

계획과 달라진 곳 넷:

1. **`ContentExportWriter`가 캐릭터 목록을 인자로 받는다.** 원본인 `PartyPrototypeRoster`가
   `FateWeaver.Simulation` 어셈블리에 있어 `FateWeaver.Core`에서 닿지 않는다(asmdef 경계).
   호출자가 넘기도록 `WriteAll(rootDirectory, characters)`로 만들고, 로스터를 저작 형태로
   비추는 `PartyPrototypeCharacterSpecs`를 `Assets/Core/Simulation/`에 뒀다. 3d가 로스터와
   함께 지운다.
2. **덱·풀 id 상수가 `ContentExportWriter`에 있다** (`StarterDeckId`·`PartyPrototypeDeckId`·
   `StarterPoolId`). 3d가 라이터를 지울 때 이 상수들의 거처를 정해야 한다 — JSON 파일 이름으로만
   남으면 잠금 테스트가 문자열을 다시 박게 된다.
3. **로더가 넷이 되면서 공용 조각 둘을 뽑았다.** 필수 키 확인은 `ContentKeys.FirstMissing`,
   Newtonsoft 예외 문장은 `ContentJsonError.Describe`. `CardContentLoader`의 사본을 지우고
   같은 것을 쓰게 했다.
4. **`.meta`를 손으로 만들었다** (규칙 17: 이 워크트리는 에디터를 열지 않는다). `Assets` 전체에
   guid 중복이 없음을 확인했다. `DeckPoolCharacterContentTests`가 `.json`마다 `.meta`가 있는지
   잠근다.

**SO 대조 결과(Task 4 Step 3): 불일치 없음.** `StarterDeck.asset` 10장·`StarterPool.asset` 22장을
guid → `CardAsset.Id`로 풀어 JSON과 비교했고, id와 순서가 정확히 같았다. 덱의 `Count`는 전부 1이다.
3b는 어느 쪽이 옳은지 판정할 필요가 없다.

**아직 아무도 새 JSON을 읽지 않는다.** 런타임은 여전히 SO를 읽는다 — 3b가 소비자를 옮긴다.

---

## Task 1: 세 스펙 타입과 JSON 왕복을 만든다

**Files:**
- Create: `Assets/Core/Authoring/Decks/DeckSpec.cs` (+ `.meta`)
- Create: `Assets/Core/Authoring/Decks/PoolSpec.cs` (+ `.meta`)
- Create: `Assets/Core/Authoring/Characters/CharacterSpec.cs` (+ `.meta`)
- Create: `Assets/Core/Tests/EditMode/DeckPoolCharacterSpecJsonTests.cs` (+ `.meta`)

**Interfaces:**
- Produces: `DeckSpec`·`PoolSpec`·`CharacterSpec` — `ContentJson`으로 왕복 가능한 평평한 저작 타입

세 타입 모두 다형이 아니므로 `EffectSpec`·`StatusSpec` 같은 컨버터가 필요 없다. `ContentJson.Settings`를
그대로 쓴다.

```csharp
namespace FateWeaver.Core.Authoring.Decks
{
    /// <summary>저작된 덱 하나. 카드 규칙은 담지 않고 Content/Cards의 id를 가리키기만 한다
    /// (설계 §4.5: 카드 규칙의 유일한 원본은 카드 JSON이다). 같은 id가 여러 번 올 수 있다 —
    /// 덱은 장수를 갖는다.</summary>
    public sealed class DeckSpec
    {
        public string Id;
        public string[] Cards;
    }
}
```

`PoolSpec`은 같은 모양이되 중복을 허용하지 않는다는 점만 로더에서 갈린다. **두 타입을 하나로
합치지 않는다** — 검증 규칙이 다르고, 합치면 "중복 허용" 플래그라는 쓰이지 않는 칸이 생긴다.

- [x] **Step 1: 기준선을 기록한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 447`

- [x] **Step 2: 왕복 테스트를 먼저 쓴다 (RED)**

세 타입 각각에 대해 `ContentJson.Write` → `ContentJson.Read` 왕복이 원본과 같은지 단언한다.
**빈 목록·빈 문자열이 살아남는지도 함께 단언한다** — `DefaultValueHandling.Ignore`가 기본값을
지우기 때문이다(README의 함정 2). 열거형이 없어 계획 2가 밟은 0번 값 함정은 재현되지 않지만,
빈 배열은 여전히 사라진다.

Expected: 컴파일 실패 (타입 없음)

- [x] **Step 3: 세 타입을 만든다 (GREEN)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`, `Passed`가 447 + 이번에 추가한 테스트 수

- [x] **Step 4: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: 덱·풀·캐릭터 저작 스펙 타입을 추가한다

카드 규칙은 담지 않고 Content/Cards의 id를 가리킨다. 로더는 다음 커밋.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: 세 로더와 카탈로그를 만든다

**Files:**
- Create: `Assets/Core/Authoring/Decks/DeckContentLoader.cs`, `DeckContentCatalog.cs` (+ `.meta`)
- Create: `Assets/Core/Authoring/Decks/PoolContentLoader.cs`, `PoolContentCatalog.cs` (+ `.meta`)
- Create: `Assets/Core/Authoring/Characters/CharacterContentLoader.cs`, `CharacterContentCatalog.cs` (+ `.meta`)
- Create: `Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs` (+ `.meta`)
- Modify: `Assets/Core/Authoring/CardContentFiles.cs` (폴더 이름 상수 3개 추가)

**Interfaces:**
- Consumes: `CardContentSource`(파일 I/O 분리), `CardContentCatalog`(카드 id 존재 확인)
- Produces: `DeckContentCatalog`·`PoolContentCatalog`·`CharacterContentCatalog`

`CardContentLoader`와 같은 형태다 — 파일을 직접 읽지 않고 `CardContentSource` 목록을 받으며,
실패하면 카탈로그를 내주지 않고 **모든 이유를 모아** 보고한다.

카드 로더와 다른 점 하나: 이 로더들은 **`CardContentCatalog`를 인자로 받는다.** 존재하지 않는 카드
id를 가리키는 덱을 거부하려면 카드가 먼저 로드되어 있어야 한다. 즉 부팅 순서가
`카드 → 덱·풀 → 캐릭터`로 정해진다(캐릭터는 덱 카탈로그를 받는다). 이 순서를 3b의
`ContentBootstrap`이 그대로 따른다.

**거부해야 하는 것:**

| 대상 | 조건 | 메시지 예 |
|---|---|---|
| 덱·풀·캐릭터 | 파일 안의 `id`가 비었다 | `starter.json: required key 'id' must be a non-empty string.` |
| 덱·풀·캐릭터 | 같은 id가 두 파일에 있다 | `b.json: duplicate deck id 'starter' (already defined in a.json).` |
| 덱·풀 | 없는 카드 id를 가리킨다 | `starter.json: unknown card id 'ghost_card'.` |
| 풀 | 같은 카드 id가 두 번 온다 | `starter.json: duplicate card id 'hasten' in pool.` |
| 캐릭터 | 없는 덱 id를 가리킨다 | `member_a.json: unknown deck id 'ghost_deck'.` |
| 캐릭터 | `displayName`이 비었다 | `member_a.json: requires a displayName.` |

- [x] **Step 1: 거부 경로 테스트를 먼저 쓴다 (RED)**

위 표의 여섯 줄 각각에 테스트를 하나씩 둔다. 성공 경로 테스트도 함께 둔다 — 카탈로그의 `Ids`가
정렬되어 있는지(규칙 7) 확인한다.

Expected: 컴파일 실패

- [x] **Step 2: 로더·카탈로그를 만든다 (GREEN)**

`CardContentLoader`·`CardContentCatalog`를 그대로 본뜬다. 카탈로그는 정렬된 `Ids`를 노출하고
`Get(id)`는 없으면 `KeyNotFoundException`을 던진다.

`CardContentFiles`에 `DecksFolderName`·`PoolsFolderName`·`CharactersFolderName` 상수를 더한다.
`ReadDirectory`는 이미 범용이라 그대로 쓴다.

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`, 새 테스트만큼 증가

- [x] **Step 3: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: 덱·풀·캐릭터 콘텐츠 로더와 카탈로그를 추가한다

카드 로더와 같은 형태다. 없는 카드 id·없는 덱 참조·중복 id를 로드 시점에
거부하며, 실패하면 모든 이유를 모아 보고한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: 익스포터를 순수 라이터와 Unity 껍데기로 가른다

**Files:**
- Create: `Assets/Core/Authoring/Json/ContentExportWriter.cs` (+ `.meta`)
- Modify: `Assets/Unity/Editor/CardContentExporter.cs`
- Create: `Assets/Core/Tests/EditMode/ContentExportWriterTests.cs` (+ `.meta`)

**Interfaces:**
- Produces: `ContentExportWriter.WriteAll(rootDirectory)` — Unity 없이 도는 내보내기

지금 내보내기는 `[MenuItem]`이라 Unity 에디터를 열어야 한다. 그런데 **원본 목록이 전부 순수
C#이다** — 시작 덱 10장은 `StarterDeckSpecs`, 풀 22장은 `StarterPoolSpecs`, 멤버 둘은
`PartyPrototypeRoster`. `AssetDatabase.Refresh()` 한 줄만 Unity가 필요하다.

파일 쓰기를 코어의 `ContentExportWriter`로 내리고 `CardContentExporter`는 그것을 부른 뒤
`Refresh()`만 하는 껍데기로 만든다. 그러면 **전용 워크트리에서 헤드리스로 내보낼 수 있어**
규칙 17의 Unity GUI 제약을 피한다. 3d가 라이터와 껍데기를 함께 지운다.

`ContentExportWriter`는 카드·상태에 더해 덱·풀·캐릭터도 쓴다:

| 산출 | 원본 |
|---|---|
| `Content/Cards/*.json` | `StarterPoolSpecs` + `StarterDeckSpecs` + `PartyPrototypeDeckSpecs` (id 중복 제거) |
| `Content/Statuses/*.json` | `StatusContentDefaults.Specs()` |
| `Content/Decks/starter.json` | `StarterDeckSpecs.Build()`의 id 순서 |
| `Content/Decks/party_prototype.json` | `PartyPrototypeDeckSpecs.Build()`의 id 순서 |
| `Content/Pools/starter.json` | `StarterPoolSpecs.Build()`의 id 순서 |
| `Content/Characters/member_a.json` · `member_b.json` | `PartyPrototypeRoster` |

`PartyPrototypeRoster`의 멤버 B는 `PartyPrototypeDeck`(검증용 덱)을 쓰므로 덱이 둘 나온다.

- [x] **Step 1: 라이터 테스트를 먼저 쓴다 (RED)**

임시 디렉터리에 `WriteAll`을 돌리고, 쓰인 파일 수와 덱 JSON의 카드 id 순서를 단언한다.
**리포지토리의 `Assets/StreamingAssets`에 쓰지 않는다** — 테스트가 커밋된 콘텐츠를 덮어쓰면 안 된다.

Expected: 컴파일 실패

- [x] **Step 2: 라이터를 만들고 익스포터를 껍데기로 줄인다 (GREEN)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`

- [x] **Step 3: 리포지토리에 쓰는 실행 경로를 만든다**

라이터를 실제 콘텐츠에 대고 돌릴 방법이 필요하다. 헤드리스 테스트 프로젝트에 `[Explicit]` 테스트를
하나 둔다 — 명시적으로 지목할 때만 돌고 일반 실행에서는 건너뛴다. 저장소 루트는
`CardContentEquivalenceJsonTests`가 쓰는 방식(`Assets` 폴더가 나올 때까지 상위로 올라간다)을 그대로
쓴다. 3d가 라이터와 함께 이 테스트도 지운다.

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~ContentExportWriterTests.Export_to_repository"
```

- [x] **Step 4: 기존 카드·상태 JSON이 바뀌지 않았는지 확인한다**

위 명령을 한 번 돌린 뒤 `git diff`가 **비어야 한다** — 이번 커밋은 이동일 뿐 값을 바꾸지 않는다.
`Content/Decks`·`Pools`·`Characters`만 새로 생긴다.

```bash
git status --short Assets/StreamingAssets/Content
```
`Cards/`·`Statuses/` 아래에 수정된 파일이 하나도 없어야 한다.

- [x] **Step 5: 커밋한다** (JSON 산출물은 Task 4에서 함께 커밋한다)

```bash
git status --short
git add -A Assets/Core Assets/Unity/Editor
git commit -m "refactor: 콘텐츠 내보내기를 코어의 순수 라이터로 내린다

원본 목록이 전부 순수 C#이라 Unity 에디터 없이 내보낼 수 있다. Unity 쪽은
AssetDatabase.Refresh만 하는 껍데기로 남는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: 1회 내보내고 공인 목록과 대조해 잠근다

**Files:**
- Create: `Assets/StreamingAssets/Content/Decks/*.json` (+ `.meta`)
- Create: `Assets/StreamingAssets/Content/Pools/starter.json` (+ `.meta`)
- Create: `Assets/StreamingAssets/Content/Characters/*.json` (+ `.meta`)
- Create: `Assets/Core/Tests/EditMode/DeckPoolCharacterContentTests.cs` (+ `.meta`)

**Interfaces:**
- Produces: 커밋된 덱·풀·캐릭터 JSON. 3b가 이것을 읽는다

- [x] **Step 1: 내보낸다**

Task 3 Step 3의 `[Explicit]` 테스트를 1회 실행한다. `.meta`는 Unity가 만들지 못하므로
(이 워크트리는 에디터를 열지 않는다) 기존 `Cards/*.json.meta`와 같은 형태로 손으로 만든다 —
`guid`는 파일마다 유일해야 한다(규칙 16: 새 Unity 에셋은 1:1 `.meta`와 함께 커밋한다).

- [x] **Step 2: 공인 목록과 대조하는 잠금 테스트를 쓴다**

리포지토리의 JSON을 로더로 읽어 다음을 단언한다. `CardContentEquivalenceJsonTests`가 저장소
루트를 찾는 방식을 그대로 쓴다.

| 단언 | 근거 |
|---|---|
| `Decks/starter.json`의 카드 10장이 `StarterDeckSpecs.Build()`의 id 순서와 같다 | [무작위 10장 시작 덱 설계](../../specs/2026-07-30-random-starter-deck-design.md)의 공인 추첨 결과 |
| `Pools/starter.json`의 카드 22장이 `StarterPoolSpecs.Build()`의 id 순서와 같다 | [시작 카드 풀 SO 저작](../specs/2026-07-29-starter-pool-so-authoring-design.md) |
| 캐릭터 둘의 id·표시명이 `PartyPrototypeRoster`의 상수와 같다 | 현재 파티 구성 |
| 세 카탈로그가 카드 카탈로그와 함께 오류 없이 로드된다 | 통합 확인 |

이 테스트는 3d가 C# 스펙을 지울 때 **골든 문자열 배열로 바뀐다** — 그때 비교 대상이 사라지기
때문이다. 지금은 두 원본이 공존하므로 교차 대조가 더 강하다.

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`

- [x] **Step 3: SO와도 어긋나지 않는지 확인한다**

`StarterDeck.asset`·`StarterPool.asset`은 아직 살아 있는 원본이다(3b가 지운다). 이 둘과 JSON이
어긋나면 3b에서 조용히 카드가 바뀐다. `.asset` YAML을 눈으로 확인하거나 Unity EditMode 테스트로
확인한다.

```bash
/usr/bin/grep -c "guid:" Assets/Unity/CardSO/Player/StarterDeck.asset
```

Unity EditMode 검증이 필요하면 배치로 돌린다 (`-runTests`와 `-quit`를 함께 쓰지 않는다).
불일치를 발견하면 **고치지 말고 기록한다** — 어느 쪽이 옳은지는 3b가 판정한다.

- [x] **Step 4: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: 덱·풀·캐릭터 콘텐츠 JSON을 내보내 커밋한다

시작 덱 10장·풀 22장·멤버 둘. 잠금 테스트가 공인 목록과 대조한다.
소비자 전환은 계획 3b가 한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: 문서 색인을 갱신한다

**Files:**
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/plans/2026-08-03-deck-pool-character-content.md` (이 문서, 상태를 `완료`로)

- [x] **Step 1: 이 계획을 완료로 표시하고 보관으로 옮긴다**

`docs/superpowers/archive/plans/`로 옮기고 머리말의 상태를 `완료`로 고친다 (규칙 20).

- [x] **Step 2: README를 갱신한다**

세 곳을 고친다.

1. `활성 계획과 로드맵` 표에서 이 계획의 줄을 지운다 (보관으로 갔으므로)
2. 카드 콘텐츠 흐름 표에서 3a를 **완료·머지**로, 3b를 **다음**으로 바꾸고 링크를 보관 경로로 돌린다
3. `현재 수치` 절에 덱·풀·캐릭터 JSON 개수를 더한다

- [x] **Step 3: 최종 검증하고 커밋한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`

```bash
git status --short
git commit -am "docs: 계획 3a를 완료로 보관하고 3b~3d 로드맵을 색인에 적는다

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## 계획 3의 분할 로드맵

설계 §4.5의 "콘텐츠 원본 전환"은 한 계획으로 담기에 크다. 각각 독립 실행 가능하고, 끝난 시점의
트리가 일관되도록 넷으로 나눈다.

| | 계획 | 범위 | 선행 |
|---|---|---|---|
| **3a** | 이 문서 | 덱·풀·캐릭터 스키마·로더·JSON 산출. 순수 코어 | 없음 |
| **3b** | 런타임 전환 | `ContentBootstrap` 신설, `BattleScreenController`·`DeckPlaytestController`를 JSON으로. `CardAsset`→아트 매핑, `CharacterAsset`→색 매핑, `DeckAsset`·`CardPoolAsset` 제거, `CardCodeGenerator` 제거. 등급·태그를 `CardSpec`으로, `CardPoolAsset.Validate`를 `AuthoringValidator`로 | 3a |
| **3c** | 상태 원본 확정 | 판별자를 `StatusRegistry`로, `StatusContentDefaults` 제거, `CombatState`의 코드 기본값 제거, `KoreanDescriptionCatalog.Default` 전역 제거 → 주입 | 3b |
| **3d** | C# 카드 스펙 제거 | `GeneratedCards.cs`·`StarterPoolSpecs`·`StarterDeckSpecs`·`PartyPrototypeDeckSpecs`·`StarterDeck.Build()`·`PartyPrototypeDeck`·`ContentExportWriter`·`CardContentExporter` 제거. 테스트를 JSON 카탈로그로 전환 | 3b |

3c와 3d는 서로 독립이라 순서를 바꿔도 된다. 3b가 병목이고, **`.asset` YAML 이관을 동반하는 것은
3b뿐이다**(README의 함정 1).

### 3b가 먼저 풀어야 할 것 — 상태 JSON은 아직 스스로를 해석하지 못한다

[`StatusSpecJsonConverter.cs:49`](../../../../Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs)가
판별자 표를 `StatusContentDefaults.Specs()`에서 만든다. 즉 `poison.json`을 `PoisonStatusSpec`으로
읽으려면 **코드 기본값 목록이 있어야 한다.** 이걸 떼기 전까지 "JSON이 유일 원본"은 성립하지 않는다.

3c의 결정: **판별자를 `StatusRegistry`로 옮긴다.** `IStatusBehavior`가 `NewSpec()`을 선언하고
컨버터가 `AuthoringContext`를 주입받는다. 규칙 9와 계획 2.5의 등록 지점 통합에 맞고, JSON에 코드
타입명이 새지 않으며, 모드가 새 상태 키를 만들 수 없으므로(설계 §4.8) `"kind"` 판별자를 JSON에
추가하는 것보다 정보 중복이 적다. `ContentJson.Read<T>`가 정적이라 컨텍스트를 넘기는 형태로
바뀌어야 한다.

### 카드의 세 형태와 세이브 (계획 4의 전제)

계획 3 전체가 향하는 곳이다. 설계 §4.3·§4.4가 이미 정했고, 이 계획의 덱 JSON이 그 그림에서 어디에
있는지 적어 둔다.

| 층 | 무엇 | 수명 | 세이브 |
|---|---|---|---|
| 콘텐츠 (JSON) | `id → CardDefinition` 사전. 부팅 1회, 불변 | 프로세스 | 안 함 |
| 런 상태 | `OwnedCard[]` — 정의 참조 + 영구 변형 | 런 | **함** (`{ defId, ownerId, permanent[] }`) |
| 전투 상태 | 더미·손패가 그 `OwnedCard`들을 가리킨다. 전투 한정 변형은 `OwnedCard.Combat` | 전투 | 안 함 |

`Content/Decks/starter.json`은 **새 런의 초기 목록**이다. 런이 시작되면 그 목록으로 `OwnedCard`가
만들어지고, 그 뒤로 이 JSON은 다시 읽히지 않는다. 세이브는 `OwnedCard`(정의 id + 영구 변형)를 담는다.

전투용 복제본은 설계 §4.3이 명시적으로 기각했다 — 전투 중 발생한 **영구** 강화가 복제본과 함께
사라지고 런으로 되돌릴 경로가 없기 때문이다. 같은 객체에 목록 둘을 두면 영구 강화가 그 전투에 즉시
적용되면서 런에도 남고, 되돌리기가 `Combat` 목록 버리기 한 줄이 된다.

## 열린 항목

- **전투 도중 세이브.** 설계 §3.2가 명시적으로 제외했다. "게임 종료 시점에 항상 세이브"가 전투
  중단까지 포함해야 한다면 전투 상태(더미·손패·존·적 HP·상태 스택)도 직렬화해야 하고, 그건 설계
  §3.2를 고치는 일이다. 계획 3·4 범위 밖이다.
- **덱 id의 소유자.** 지금 캐릭터가 덱 id를 가리키지만, 런이 시작되면 덱은 캐릭터가 아니라 런 상태에
  속한다. 런 사이클 재설계(`needs-redesign`)가 이 경계를 정한다.
- **모드가 같은 id의 덱·풀을 제공할 때의 처리.** 설계 §5의 카드 id 충돌 항목과 같은 결정을 따른다.
