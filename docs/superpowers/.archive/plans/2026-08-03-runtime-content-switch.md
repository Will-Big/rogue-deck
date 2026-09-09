# 런타임 콘텐츠 전환 구현 계획 (카드 콘텐츠 계획 3b)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-08-03
- 상태: `완료` (2026-08-03 구현)
- 권위 문서: [`specs/2026-07-30-card-mutation-and-runtime-content-design.md`](../../specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.5
- 선행 계획: [`archive/plans/2026-08-03-deck-pool-character-content.md`](./2026-08-03-deck-pool-character-content.md) (3a)
- 후속 계획: 3c(상태 원본 확정) · 3d(C# 카드 스펙 제거)
- 브랜치: `feat/runtime-content-switch`

**Goal:** 런타임이 JSON을 읽게 만들고, 카드 규칙의 원본을 `Content/Cards/*.json` 하나로 줄인다.
3a가 만든 덱·풀·캐릭터 JSON의 첫 소비자가 생긴다.

**Architecture:** 코어에 `ContentBootstrap`을 두어 4개 카탈로그를 `카드 → 덱·풀 → 캐릭터` 순서로
만들고, Unity는 `Application.streamingAssetsPath`만 넘긴다. 소비자 둘(`BattleScreenController`·
`DeckPlaytestController`)이 SO 대신 카탈로그를 읽는다. 그 뒤에야 `CardAsset`·`DeckAsset`·
`CardPoolAsset`과 코드 생성 경로를 지운다 — **위험한 삭제를 맨 뒤로 몬다.**

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), NUnit, Newtonsoft.Json,
`FateWeaver.Core`(UnityEngine 미참조), Unity 6000.5.2f1 EditMode

## Global Constraints

- 헤드리스 테스트 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- Unity 배치 명령 (`-runTests`와 `-quit`를 **함께 쓰지 않는다**):
  ```
  /Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode \
    -projectPath <워크트리> -runTests -testPlatform EditMode \
    -testResults /private/tmp/<이름>.xml -logFile /private/tmp/<이름>.log
  ```
- 착수 시점 기준선: **헤드리스 487/487**, Unity EditMode **561/561**(skipped 1 = `[Explicit]`),
  카드 JSON **26**, 상태 JSON **11**, 덱 JSON **2**, 풀 JSON **1**, 캐릭터 JSON **2**
- `FateWeaver.Core`에서 `UnityEngine`을 참조하지 않는다 (규칙 6)
- 결정론: 반복 순서가 사전 구현·파일 시스템 순서에 의존하지 않는다 (규칙 7)
- 튜닝 수치를 계산식에 박지 않는다 (규칙 8)
- 콘텐츠 경로는 루트 상수 하나만 두고 나머지는 폴더 스캔이다 (규칙 2·3)
- 런타임 문자열 탐색 금지 (규칙 3) — 카탈로그 조회는 부팅 1회이며 `GameObject.Find` 부류가 아니다
- 워킹 트리를 깨끗이 남긴다 (규칙 18)
- 문서 색인을 같은 커밋에서 갱신한다 (규칙 20)
- C# 9 한계: `record struct` 금지, 기본 인터페이스 구현 금지, 파일 범위 네임스페이스 금지

## 범위 밖 — 적 카드는 아직 JSON이 아니다

고블린·간수 카드는 `GoblinDeck`·`WardenDeck`의 순수 C#에서 나오고 `Content/Cards`에 없다.
이 계획이 "JSON이 유일 원본"으로 만드는 것은 **플레이어 카드**다. 적 콘텐츠 JSON화는 적 정책·행동
패턴 설계를 함께 건드려야 하므로 별도 단계로 미룬다. `CardArtCatalog`가 적 카드 아트를 id로
가리키는 것은 이 경계와 무관하다 — 아트는 표현이라 원래 Unity 쪽이다.

## 목표 상태

| | 지금 | 이후 |
|---|---|---|
| 전투 씬의 카드 | `member.Deck.ToSpecs()` — CardSO에서 | **JSON 카탈로그에서** |
| 카드 규칙의 원본 | C# 스펙 · `GeneratedCards` · CardSO · JSON (넷) | **JSON 하나** |
| 등급·태그 | `CardAsset._grade`·`_tags` (`.asset` YAML) | **카드 JSON** |
| 풀의 등급·태그 검증 | `CardPoolAsset.Validate` | **`PoolContentLoader`** |
| 카드 아트 | `CardAsset.Art` 32개 중 3개만 실제 사용 | **`CardArtCatalog` (항목 3개)** |
| 코드 생성 | `CardCodeGenerator` → `GeneratedCards.cs` | **없음** |

## 조사로 확정된 사실 (착수 전에 읽는다)

이 계획의 형태를 정한 실측이다. 다시 조사하지 않아도 된다 (2026-08-03 측정).

1. **`CardAsset` 32개 중 `Art`가 연결된 것은 3개뿐이고 전부 적 카드다** —
   `goblin_jab`, `crude_guard`, `sly_jab`. 플레이어 카드 아트는 0개다(색상 틴트 아트 방향).
2. **그 3개는 `_enemyArtCards`(별도 인스펙터 필드)로 들어온다.** `BuildArtLookup()`이
   `member.Deck.Entries`를 훑는 부분은 `AddArt`의 `card.Art != null` 가드에 전부 걸려
   **아무것도 넣지 않는다.** 즉 `DeckAsset` 제거가 아트 경로를 끊지 않는다.
3. **`GeneratedCards`에 런타임 소비자가 없다.** 읽는 곳은 `GeneratedCardsTests`,
   `CardContentEquivalenceTests`, `StarterDeckAssetCompositionTests`, 그리고 생성기 자신뿐이다.
4. **`EffectSpec.ToLiteral()`의 유일한 호출자는 `CardCodeGenerator.cs:693`이다**
   (`NewEffectLocalityTests`의 테스트 픽스처가 override 하나를 갖는다).
5. **등급·태그의 원본은 `.asset` YAML뿐이다.** 플레이어 카드 22개가 전부 `_grade: 1`(Common)이고
   태그가 채워져 있는데, JSON의 원본인 `StarterPoolSpecs`에는 두 필드가 **없다.** 그래서
   Task 3의 병합이 `CardAsset` 삭제(Task 7)보다 반드시 앞에 온다.
6. **`CardPoolAsset.Validate`의 등급·태그 규칙은 풀 소속 카드에만 걸린다.** `fixture_*` 카드도
   카드 JSON에 있으나 등급이 없으므로, 이 규칙을 `AuthoringValidator`(전역)로 올리면 그것들이
   거부된다. 규칙은 `PoolContentLoader`로 간다.
7. **`BattleSceneBuilder`(355줄)가 `CharacterAsset`·`CardAsset`을 경로로 로드한다.** 삭제 대상이
   아니고 적 아트 참조부(201줄) 한 곳만 고친다.

## 구현 결과 (2026-08-03)

헤드리스 **487 → 499**, Unity EditMode **561 → 557**(순감은 SO·코드 생성 테스트를 지운 결과다).
카드 규칙의 원본이 `Content/Cards/*.json` 하나가 됐다.

**계획이 틀렸던 곳 여섯.** 다음 계획을 쓸 때 같은 실수를 피하려고 적어 둔다.

1. **등급·태그 병합 대상이 22가 아니라 26이었다.** `fixture_*` 카드에도 `CardAsset`이 있다는 걸
   조사에서 놓쳤다. 병합 자체는 옳았고(풀 22장은 등급+태그, fixture 4장은 빈 태그 배열) diff는
   순수 추가였다 — 삭제된 8줄이 전부 같은 줄에 콤마만 붙어 다시 나타났다.
2. **`CardAssetAuthoringTests`가 반대 방향 불변식을 잠그고 있었다.**
   `Assert.IsNull(typeof(CardSpec).GetField("Grade"))` — "등급·태그는 Unity 전용"이라는 주장이다.
   3b가 의도적으로 뒤집는 것이라 테스트를 뒤집었다. Files 목록에 없던 파일이다.
3. **`ContentExportWriter`가 파괴적 도구가 됐다.** 등급·태그를 JSON에 넣은 순간, C# 스펙에서
   카드를 다시 쓰는 `Export_to_repository`는 그 값을 지우는 명령이 된다. 카드 내보내기 경로를
   없애고 `WriteAllDoesNotTouchCards` 회귀 테스트를 뒀다. **원본을 옮길 때는 옛 생성 경로가
   파괴적으로 변하지 않는지 반드시 확인한다.**
4. **`_enemyArtCards`는 이미 덱과 무관했다.** `BuildArtLookup`이 덱을 훑는 코드는 `Art != null`
   가드에 전부 걸려 아무것도 넣지 않았다(플레이어 카드는 아트가 없다). `DeckAsset` 제거가 아트
   경로를 끊는다는 계획의 전제가 틀렸고, 덕분에 씬 작업이 한 단계 줄었다.
5. **씬 배선을 사람이 할 필요가 없었다.** `BattleSceneBuilder`가 씬을 자동 생성하므로 거기서
   `_cardArt`를 배선하면 메뉴 한 번으로 끝난다. 하드코딩된 카드 경로 셋도 카탈로그 경로 하나로 줄었다.
6. **`GeneratedCards`·`ToLiteral` 제거를 3d에서 앞당겼다.** 런타임 소비자가 없었고 `CardAsset`이
   죽으면 생성기도 함께 죽는다. `ToLiteral` 제거로 `EffectSpec` 서브클래스 8개가 메서드 하나씩
   잃었고 전용 헬퍼 `ConditionLiteral`·`Quote`도 죽었다.

**범위를 벗어나 함께 처리한 것** (사용자 요청):

- **안 쓰는 플레이테스트 씬 둘과 `DeckPlaytestController`(334줄) 제거.** 그 두 씬이 컨트롤러의
  유일한 소비자였다. 빌드 설정이 없어진 씬 둘을 등록하고 정작 전투 씬은 빠뜨리고 있어 바로잡았다.

**아직 검증되지 않은 축 하나: Play 모드.** 배치 EditMode는 `Application.streamingAssetsPath`로
JSON을 읽는 실제 경로를 밟지 못한다. 씬 재생성(`Fate Weaver ▸ Build Battle Scene`) 후 Play로
확인해야 한다.

## 씬 저작 경계

**이 계획에서 사람이 Unity GUI로 해야 하는 일은 둘뿐이다** (규칙 17: 워크트리는 에디터를 열지
않는다). 나머지는 전부 코드이며 배치 EditMode로 검증한다.

1. `BattleScreenController`의 `_enemyArtCards` 필드를 `_cardArt`(CardArtCatalog)로 교체 — Task 6
2. `CharacterAsset` 에셋 둘에서 `Deck` 참조 끊기 — Task 7

**`_party`는 `CharacterAsset[]` 그대로 둔다.** 축소된 `CharacterAsset`(id + Color)은 설계 §4.5의
"Unity는 표현만 담당"에 정확히 맞고, 문자열 id 배열로 바꾸면 인스펙터가 오타를 잡아줄 수단이
사라진다. 그리고 party 목록의 소유자는 런 사이클 재설계(`needs-redesign`)가 정할 일이라 지금
확정하면 두 번 바꾼다 — 3a의 열린 항목이 이미 그렇게 적었다.

---

## Task 1: `CardGrade`를 코어로 옮기고 `CardSpec`에 등급·태그를 더한다

**Files:**
- Create: `Assets/Core/Cards/CardGrade.cs` (+ `.meta`)
- Delete: `Assets/Unity/CardGrade.cs` (+ `.meta`)
- Modify: `Assets/Core/Authoring/CardSpec.cs`
- Modify: `Assets/Unity/CardAsset.cs` (using 추가)
- Modify: `Assets/Unity/CardPoolAsset.cs` (using 추가)
- Create: `Assets/Core/Tests/EditMode/CardSpecGradeTagTests.cs` (+ `.meta`)

**Interfaces:**
- Produces: `FateWeaver.Core.Cards.CardGrade` — `None`·`Common`·`Advanced`·`Rare`·`Other`
- Produces: `CardSpec.Grade`(CardGrade) · `CardSpec.Tags`(string[])

`CardGrade`는 지금 `Assets/Unity/CardGrade.cs`의 `FateWeaver.Unity` 네임스페이스에 있다.
UnityEngine을 참조하지 않는 평범한 enum이므로 파일을 옮기고 네임스페이스만 바꾸면 된다.
`CardAsset`·`CardPoolAsset`이 같은 어셈블리 안에서 이름만으로 쓰고 있었으므로 `using`이 필요해진다.

`Grade`는 `CardGrade.None`이 0번 값이라 `DefaultValueHandling.Ignore`가 지운다. `Side`·`Category`가
같은 함정을 밟았으므로(`CardSpec.cs:16`) **같은 처방을 쓰지 않는다** — 등급 없음은 `fixture_*`
카드의 정상 상태이고, 생략이 곧 `None`이라 정보 손실이 없다. 반면 `Tags`는 빈 배열이 그대로
살아남는다(3a에서 확인).

- [x] **Step 1: 기준선을 기록한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 487`

- [x] **Step 2: 왕복 테스트를 먼저 쓴다 (RED)**

Create `Assets/Core/Tests/EditMode/CardSpecGradeTagTests.cs`:

```csharp
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>등급·태그가 CardSpec에 실려 JSON을 왕복하는지 잠근다. 등급은 0번 값(None)이
    /// 생략되지만 그것이 정상 상태다 — fixture 카드는 등급을 갖지 않는다.</summary>
    public class CardSpecGradeTagTests
    {
        private static CardSpec Base() => new CardSpec
        {
            Id = "sample", Name = "표본", Side = Side.Player, Category = CardCategory.Execution
        };

        [Test]
        public void GradeAndTagsSurviveTheRoundTrip()
        {
            var spec = Base();
            spec.Grade = CardGrade.Common;
            spec.Tags = new[] { "시작", "실행력" };

            var read = ContentJson.Read<CardSpec>(ContentJson.Write(spec));

            Assert.AreEqual(CardGrade.Common, read.Grade);
            CollectionAssert.AreEqual(spec.Tags, read.Tags);
        }

        [Test]
        public void MissingGradeReadsBackAsNone()
        {
            var spec = Base();
            spec.Tags = new string[0];

            var json = ContentJson.Write(spec);
            var read = ContentJson.Read<CardSpec>(json);

            Assert.AreEqual(CardGrade.None, read.Grade);
            StringAssert.DoesNotContain("\"grade\"", json, "None은 생략되어야 한다.");
        }

        [Test]
        public void AnEmptyTagListSurvives()
        {
            var spec = Base();
            spec.Tags = new string[0];

            var read = ContentJson.Read<CardSpec>(ContentJson.Write(spec));

            Assert.IsNotNull(read.Tags, "빈 배열이 직렬화에서 사라졌다.");
            Assert.AreEqual(0, read.Tags.Length);
        }
    }
}
```

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: 컴파일 실패 — `CardGrade` 가 `FateWeaver.Core.Cards`에 없고 `CardSpec.Grade`도 없다

- [x] **Step 3: enum을 옮기고 필드를 더한다 (GREEN)**

Create `Assets/Core/Cards/CardGrade.cs`:

```csharp
namespace FateWeaver.Core.Cards
{
    /// <summary>카드 등급. 카드 풀의 후보 구성에만 쓰이며 전투 규칙에는 관여하지 않는다.
    /// None은 등급 개념이 없는 카드(검증용 fixture 등)의 정상 상태다.</summary>
    public enum CardGrade
    {
        None,
        Common,
        Advanced,
        Rare,
        Other
    }
}
```

Delete `Assets/Unity/CardGrade.cs` 와 `Assets/Unity/CardGrade.cs.meta`.

`Assets/Core/Authoring/CardSpec.cs`의 필드 목록 끝(`InterventionRequireAdjacent` 다음)에 추가:

```csharp
        /// <summary>카드 풀 후보 구성용 등급. None은 등급 개념이 없는 카드의 정상 상태이므로
        /// Side·Category와 달리 Include 처방을 쓰지 않는다.</summary>
        public CardGrade Grade;

        /// <summary>저작 분류 태그. 풀 소속 카드는 하나 이상 가져야 한다(PoolContentLoader).</summary>
        public string[] Tags;
```

`Assets/Unity/CardAsset.cs`와 `Assets/Unity/CardPoolAsset.cs` 맨 위에 `using FateWeaver.Core.Cards;`를
더한다 (`CardAsset.cs`에는 이미 있으므로 `CardPoolAsset.cs`만 필요할 수 있다 — 컴파일 오류를 보고 판단한다).

`CardAsset.ToSpec()`에 두 줄을 더한다:

```csharp
            InterventionRequireAdjacent = _interventionRequireAdjacent,
            Grade = _grade,
            Tags = _tags ?? System.Array.Empty<string>()
```

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`, `Passed: 490`

- [x] **Step 4: Unity 배치로 컴파일과 회귀를 확인한다**

Run:
```
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath $(pwd) -runTests -testPlatform EditMode \
  -testResults /private/tmp/3b-task1.xml -logFile /private/tmp/3b-task1.log
```
Expected: XML 루트의 `failed="0"`. `CardGrade` 이동이 `.asset` YAML을 깨지 않았는지 확인한다 —
`_grade`는 `[SerializeField] private CardGrade`이고 enum은 int로 직렬화되므로 네임스페이스 변경이
YAML에 영향을 주지 않는다. **`git status`로 `.asset`이 하나도 수정되지 않았음을 확인한다.**

- [x] **Step 5: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: 등급·태그를 CardSpec으로 올린다

CardGrade를 코어로 옮기고 CardSpec에 Grade·Tags를 더한다. 카드 JSON에
값을 채우는 것은 다음 커밋.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: 풀의 등급·태그 규칙을 `PoolContentLoader`로 옮긴다

**Files:**
- Modify: `Assets/Core/Authoring/Decks/PoolContentLoader.cs`
- Modify: `Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs`

**Interfaces:**
- Consumes: `CardContentCatalog`(Task 1이 등급·태그를 실어 보낸 `CardDefinition`은 아니다 — 아래 참고)
- Produces: 풀 로드 시점의 등급·태그 거부

**`CardDefinition`에는 등급·태그를 싣지 않는다.** 전투 규칙이 쓰지 않는 값이고, 코어의 출력은
이벤트 타임라인뿐이라는 규칙 11과도 맞지 않는다. 대신 `CardContentLoader`가 만든 카탈로그가
스펙을 함께 들고 있어야 풀 로더가 검사할 수 있다 — `CardContentCatalog`에 `Specs` 사전을 더한다.

`CardPoolAsset.Validate`가 지금 하는 검사 중 **풀 로더로 옮길 것**:

| 규칙 | 메시지 |
|---|---|
| 풀 소속 카드는 등급을 가져야 한다 | `starter.json: card 'hasten' must have a grade.` |
| 풀 소속 카드는 태그를 하나 이상 가져야 한다 | `starter.json: card 'hasten' must have at least one tag.` |
| 태그에 빈 문자열이 없어야 한다 | `starter.json: card 'hasten' has an empty tag at index 1.` |
| 태그가 중복되지 않아야 한다 | `starter.json: card 'hasten' has duplicate tag '시작'.` |

`CardPoolAsset.Validate`의 나머지(빈 풀 id, null 카드, 빈 카드 id, 카드 중복)는 3a의
`PoolContentLoader`가 **이미 거부한다.**

- [x] **Step 1: 거부 경로 테스트를 먼저 쓴다 (RED)**

`Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs`의 `Cards` 헬퍼를 등급·태그를 받도록
바꾸고(기본값은 유효한 값), 위 표의 네 줄 각각에 테스트를 하나씩 더한다. 기존 헬퍼:

```csharp
        private static CardContentCatalog Cards(params string[] ids)
            => Cards(CardGrade.Common, new[] { "시작" }, ids);

        private static CardContentCatalog Cards(
            CardGrade grade, string[] tags, params string[] ids)
        {
            var specs = new List<CardSpec>();
            foreach (var id in ids)
            {
                specs.Add(new CardSpec
                {
                    Id = id, Name = id, Side = Side.Player,
                    Category = CardCategory.Execution, Grade = grade, Tags = tags
                });
            }

            var cards = new Dictionary<string, CardDefinition>();
            var byId = new Dictionary<string, CardSpec>();
            foreach (var spec in specs)
            {
                cards.Add(spec.Id, CardSpecMapper.ToDefinition(spec));
                byId.Add(spec.Id, spec);
            }

            return new CardContentCatalog(cards, byId);
        }
```

새 테스트 넷:

```csharp
        [Test]
        public void PoolLoaderRejectsACardWithoutAGrade()
        {
            var result = PoolContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }") },
                Cards(CardGrade.None, new[] { "시작" }, "hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors, "starter.json: card 'hasten' must have a grade.");
        }

        [Test]
        public void PoolLoaderRejectsACardWithoutTags()
        {
            var result = PoolContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }") },
                Cards(CardGrade.Common, new string[0], "hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors, "starter.json: card 'hasten' must have at least one tag.");
        }

        [Test]
        public void PoolLoaderRejectsAnEmptyTag()
        {
            var result = PoolContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }") },
                Cards(CardGrade.Common, new[] { "시작", "" }, "hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors, "starter.json: card 'hasten' has an empty tag at index 1.");
        }

        [Test]
        public void PoolLoaderRejectsADuplicateTag()
        {
            var result = PoolContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }") },
                Cards(CardGrade.Common, new[] { "시작", "시작" }, "hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors, "starter.json: card 'hasten' has duplicate tag '시작'.");
        }
```

Expected: 컴파일 실패 — `CardContentCatalog`에 2인자 생성자와 `Specs`가 없다

- [x] **Step 2: 카탈로그에 스펙을 싣고 풀 로더에 규칙을 더한다 (GREEN)**

`Assets/Core/Authoring/CardContentCatalog.cs`에 스펙 사전을 더한다:

```csharp
        private readonly Dictionary<string, CardSpec> _specs;

        public CardContentCatalog(
            Dictionary<string, CardDefinition> cards,
            Dictionary<string, CardSpec> specs)
        {
            _cards = cards;
            _specs = specs;
            _ids = new List<string>(cards.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>저작 스펙. 전투 규칙이 쓰지 않는 값(등급·태그)을 CardDefinition에 싣지 않기
        /// 위해 따로 둔다 — 코어의 출력은 이벤트 타임라인뿐이다(규칙 11).</summary>
        public IReadOnlyDictionary<string, CardSpec> Specs => _specs;
```

`CardContentLoader.Load`의 마지막 부분에서 두 사전을 함께 만든다:

```csharp
            var cards = new Dictionary<string, CardDefinition>();
            var specsById = new Dictionary<string, CardSpec>();
            foreach (var spec in specs)
            {
                cards.Add(spec.Id, CardSpecMapper.ToDefinition(spec));
                specsById.Add(spec.Id, spec);
            }

            return CardContentLoadResult.Ok(new CardContentCatalog(cards, specsById));
```

`PoolContentLoader`의 카드 검사 루프(중복 검사 뒤)에 등급·태그 검사를 더한다:

```csharp
                    if (!ValidateGradeAndTags(source.Name, cardId, cards, errors))
                    {
                        rejected = true;
                    }
```

그리고 클래스에 헬퍼를 더한다:

```csharp
        /// <summary>풀 소속 카드에만 걸리는 규칙이다. fixture 카드처럼 풀에 들지 않는 카드는
        /// 등급·태그가 없어도 정상이므로 AuthoringValidator(전역)로 올리지 않는다.</summary>
        private static bool ValidateGradeAndTags(
            string sourceName,
            string cardId,
            CardContentCatalog cards,
            List<string> errors)
        {
            var spec = cards.Specs[cardId];
            var ok = true;

            if (spec.Grade == CardGrade.None)
            {
                errors.Add(sourceName + ": card '" + cardId + "' must have a grade.");
                ok = false;
            }

            var tags = spec.Tags ?? new string[0];
            if (tags.Length == 0)
            {
                errors.Add(
                    sourceName + ": card '" + cardId + "' must have at least one tag.");
                return false;
            }

            var seenTags = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < tags.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(tags[i]))
                {
                    errors.Add(
                        sourceName + ": card '" + cardId + "' has an empty tag at index "
                        + i + ".");
                    ok = false;
                    continue;
                }

                if (!seenTags.Add(tags[i]))
                {
                    errors.Add(
                        sourceName + ": card '" + cardId + "' has duplicate tag '"
                        + tags[i] + "'.");
                    ok = false;
                }
            }

            return ok;
        }
```

`using FateWeaver.Core.Cards;`가 `PoolContentLoader.cs`에 필요하다.

**이 시점에 `Pools/starter.json` 잠금 테스트가 실패한다** — 카드 JSON에 아직 등급·태그가 없기
때문이다. 그것이 Task 3의 RED다. Task 2의 커밋에서는
`DeckPoolCharacterContentTests`·`ContentExportWriterTests`의 풀 관련 단언이 깨지므로,
**Task 2와 Task 3을 하나의 작업 흐름으로 보고 Task 3까지 간 뒤 커밋한다.**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: 새 테스트 4개는 통과, 풀 로드를 거치는 기존 테스트가 실패 (Task 3에서 해소)

---

## Task 3: 카드 JSON에 등급·태그를 병합한다

**Files:**
- Create: `Assets/Tests/UnityEditMode/CardGradeTagMigrationTests.cs` (+ `.meta`)
- Modify: `Assets/StreamingAssets/Content/Cards/*.json` (22개 — 플레이어 카드)

**Interfaces:**
- Produces: 등급·태그가 채워진 카드 JSON. Task 2의 풀 로더가 이것을 통과시킨다

**손으로 전사하지 않는다.** 플레이어 카드 22장 × 태그 2~4개를 옮겨 적으면 조용히 틀린다.
`CardAsset`이 아직 살아 있는 지금 Unity에서 읽어 병합한다. 이 테스트는 Task 7이 `CardAsset`과
함께 지운다.

- [x] **Step 1: 마이그레이션 테스트를 쓴다**

Create `Assets/Tests/UnityEditMode/CardGradeTagMigrationTests.cs`. `Assets/Tests/UnityEditMode/`에
두는 이유는 `AssetDatabase`가 필요해서다(헤드리스 프로젝트는 이 폴더를 컴파일하지 않는다).

```csharp
using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>CardAsset의 등급·태그를 카드 JSON에 1회 병합한다. 등급·태그의 원본이 .asset
    /// YAML뿐이라(계획 3b 조사 5) 손 전사를 피하려고 둔다. 계획 3b Task 7이 CardAsset과 함께
    /// 이 테스트를 지운다.</summary>
    public class CardGradeTagMigrationTests
    {
        private const string CardsDirectory = "Assets/StreamingAssets/Content/Cards";

        [Test]
        [Explicit]
        public void Merge_grade_and_tags_into_card_json()
        {
            var merged = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CardAsset)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CardAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || string.IsNullOrEmpty(asset.Id))
                {
                    continue;
                }

                var path = Path.Combine(CardsDirectory, asset.Id + ".json");
                if (!File.Exists(path))
                {
                    continue; // 적 카드는 아직 JSON이 아니다 (범위 밖)
                }

                var spec = ContentJson.Read<CardSpec>(File.ReadAllText(path));
                spec.Grade = asset.Grade;
                spec.Tags = asset.Tags.ToArray();
                File.WriteAllText(path, ContentJson.Write(spec) + "\n");
                merged++;
            }

            TestContext.WriteLine("Merged grade/tags into " + merged + " card JSON files.");
            Assert.AreEqual(22, merged, "플레이어 카드 22장에 병합되어야 한다.");
        }

        [Test]
        public void EveryPooledCardJsonHasAGradeAndTags()
        {
            var pool = ContentJson.Read<FateWeaver.Core.Authoring.Decks.PoolSpec>(
                File.ReadAllText("Assets/StreamingAssets/Content/Pools/starter.json"));

            foreach (var cardId in pool.Cards)
            {
                var spec = ContentJson.Read<CardSpec>(
                    File.ReadAllText(Path.Combine(CardsDirectory, cardId + ".json")));

                Assert.AreNotEqual(
                    FateWeaver.Core.Cards.CardGrade.None, spec.Grade,
                    cardId + "에 등급이 없다.");
                Assert.IsNotNull(spec.Tags, cardId + "에 태그가 없다.");
                Assert.Greater(spec.Tags.Length, 0, cardId + "에 태그가 없다.");
            }
        }

        [Test]
        public void CardJsonGradeAndTagsMatchTheAuthoredAsset()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CardAsset)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CardAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || string.IsNullOrEmpty(asset.Id))
                {
                    continue;
                }

                var path = Path.Combine(CardsDirectory, asset.Id + ".json");
                if (!File.Exists(path))
                {
                    continue;
                }

                var spec = ContentJson.Read<CardSpec>(File.ReadAllText(path));
                Assert.AreEqual(asset.Grade, spec.Grade, asset.Id + "의 등급이 어긋난다.");
                CollectionAssert.AreEqual(
                    asset.Tags.ToArray(), spec.Tags ?? new string[0],
                    asset.Id + "의 태그가 어긋난다.");
            }
        }
    }
}
```

- [x] **Step 2: 병합을 1회 실행한다**

Run:
```
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath $(pwd) -runTests -testPlatform EditMode \
  -testFilter "FateWeaver.Tests.UnityEditMode.CardGradeTagMigrationTests.Merge_grade_and_tags_into_card_json" \
  -testResults /private/tmp/3b-merge.xml -logFile /private/tmp/3b-merge.log
```
Expected: XML의 `passed="1"`. `git diff --stat Assets/StreamingAssets/Content/Cards`가 **22개 파일
수정**을 보여야 한다.

- [x] **Step 3: 병합 결과를 눈으로 확인한다**

```bash
git diff Assets/StreamingAssets/Content/Cards/hasten.json
```
`"grade": "Common"`과 `"tags": [...]`가 더해졌고 **다른 키는 하나도 바뀌지 않아야 한다.**
`fixture_*`·`goblin_*` JSON은 수정되지 않아야 한다.

- [x] **Step 4: 헤드리스와 Unity를 모두 돌린다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0` — Task 2에서 깨졌던 풀 잠금 테스트가 되살아난다

Run: Unity 배치 EditMode 전체 (Global Constraints의 명령)
Expected: `failed="0"`, skipped는 `[Explicit]` 둘(3a의 내보내기 + 이번 병합)

- [x] **Step 5: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: 등급·태그를 카드 JSON으로 옮기고 풀 로더가 검증한다

CardPoolAsset.Validate의 등급·태그 규칙이 PoolContentLoader로 간다. 이 규칙은
풀 소속 카드에만 걸리므로 AuthoringValidator(전역)로 올리지 않는다 — fixture
카드는 등급이 없는 것이 정상이다.

값은 CardAsset에서 1회 병합했다. 손 전사가 아니다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: `ContentBootstrap`을 만든다

**Files:**
- Create: `Assets/Core/Authoring/ContentBootstrap.cs` (+ `.meta`)
- Create: `Assets/Core/Authoring/GameContent.cs` (+ `.meta`)
- Create: `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `CardContentFiles.ReadDirectory`, 로더 넷
- Produces: `ContentBootstrap.Load(string contentRoot)` → `ContentBootstrapResult`
- Produces: `GameContent` — 카탈로그 넷을 묶은 불변 번들

부팅 순서는 3a가 이미 정했다: **카드 → 덱·풀 → 캐릭터.** 덱·풀 로더가 카드 카탈로그를,
캐릭터 로더가 덱 카탈로그를 인자로 받기 때문이다. `ContentBootstrap`은 그 순서를 코드로 굳히고,
어느 단계가 실패하든 **모든 이유를 모아** 보고한다.

상태 카탈로그는 이 계획에서 묶지 않는다 — `StatusSpecJsonConverter`가 아직 `StatusContentDefaults`에
의존하므로(3c가 뗀다) `GameContent`에 넣으면 "JSON이 원본"이라는 거짓 신호를 준다.

- [x] **Step 1: 부팅 테스트를 먼저 쓴다 (RED)**

Create `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs`:

```csharp
using System.IO;
using FateWeaver.Core.Authoring;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>부팅이 카드 → 덱·풀 → 캐릭터 순서를 지키고, 실패하면 카탈로그를 내주지 않는지
    /// 잠근다. 리포지토리의 실제 콘텐츠를 읽는다.</summary>
    public class ContentBootstrapTests
    {
        private static string ContentRoot()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return Path.Combine(directory, "Assets", "StreamingAssets", "Content");
        }

        [Test]
        public void BootstrapLoadsEveryCatalog()
        {
            var result = ContentBootstrap.Load(ContentRoot());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            Assert.AreEqual(26, result.Content.Cards.Ids.Count);
            Assert.AreEqual(2, result.Content.Decks.Ids.Count);
            Assert.AreEqual(1, result.Content.Pools.Ids.Count);
            Assert.AreEqual(2, result.Content.Characters.Ids.Count);
        }

        [Test]
        public void BootstrapResolvesACharacterToItsCards()
        {
            var content = ContentBootstrap.Load(ContentRoot()).Content;

            var memberA = content.Characters.Get("member_a");
            var deck = content.Decks.Get(memberA.Deck);

            Assert.AreEqual(10, deck.Count);
            foreach (var cardId in deck)
            {
                Assert.IsTrue(content.Cards.Cards.ContainsKey(cardId), cardId + "가 없다.");
            }
        }

        [Test]
        public void BootstrapFailsWhenTheRootIsMissing()
        {
            var result = ContentBootstrap.Load(
                Path.Combine(Path.GetTempPath(), "fate-weaver-no-such-content"));

            Assert.IsFalse(result.Succeeded);
            Assert.Greater(result.Errors.Count, 0);
        }
    }
}
```

Expected: 컴파일 실패 — `ContentBootstrap`이 없다

- [x] **Step 2: 번들과 부팅을 만든다 (GREEN)**

Create `Assets/Core/Authoring/GameContent.cs`:

```csharp
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 1회로 만들어져 상주하는 콘텐츠 번들. 상태 카탈로그는 아직 묶지 않는다 —
    /// StatusSpecJsonConverter가 StatusContentDefaults에 의존하므로(계획 3c가 뗀다) 여기 넣으면
    /// "JSON이 원본"이라는 거짓 신호가 된다.</summary>
    public sealed class GameContent
    {
        public GameContent(
            CardContentCatalog cards,
            DeckContentCatalog decks,
            PoolContentCatalog pools,
            CharacterContentCatalog characters)
        {
            Cards = cards;
            Decks = decks;
            Pools = pools;
            Characters = characters;
        }

        public CardContentCatalog Cards { get; }
        public DeckContentCatalog Decks { get; }
        public PoolContentCatalog Pools { get; }
        public CharacterContentCatalog Characters { get; }
    }
}
```

Create `Assets/Core/Authoring/ContentBootstrap.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 1회의 결과.</summary>
    public sealed class ContentBootstrapResult
    {
        private ContentBootstrapResult(GameContent content, IReadOnlyList<string> errors)
        {
            Content = content;
            Errors = errors;
        }

        public bool Succeeded => Content != null;
        public GameContent Content { get; }
        public IReadOnlyList<string> Errors { get; }

        public static ContentBootstrapResult Ok(GameContent content)
            => new ContentBootstrapResult(content, new string[0]);

        public static ContentBootstrapResult Failed(IReadOnlyList<string> errors)
            => new ContentBootstrapResult(null, errors);
    }

    /// <summary>콘텐츠 루트 하나를 받아 카탈로그 넷을 만든다. 순서는 카드 → 덱·풀 → 캐릭터로
    /// 고정이다 — 덱·풀 로더가 카드 카탈로그를, 캐릭터 로더가 덱 카탈로그를 필요로 한다.
    /// 파일 I/O는 CardContentFiles가 맡으므로 Unity 없이 돈다.</summary>
    public static class ContentBootstrap
    {
        public static ContentBootstrapResult Load(string contentRoot)
        {
            var errors = new List<string>();

            var cards = CardContentLoader.Load(
                Read(contentRoot, CardContentFiles.CardsFolderName, errors),
                AuthoringContext.Default());
            if (!cards.Succeeded)
            {
                errors.AddRange(cards.Errors);
                return ContentBootstrapResult.Failed(errors);
            }

            var decks = DeckContentLoader.Load(
                Read(contentRoot, CardContentFiles.DecksFolderName, errors), cards.Catalog);
            var pools = PoolContentLoader.Load(
                Read(contentRoot, CardContentFiles.PoolsFolderName, errors), cards.Catalog);

            if (!decks.Succeeded) errors.AddRange(decks.Errors);
            if (!pools.Succeeded) errors.AddRange(pools.Errors);
            if (errors.Count > 0)
            {
                return ContentBootstrapResult.Failed(errors);
            }

            var characters = CharacterContentLoader.Load(
                Read(contentRoot, CardContentFiles.CharactersFolderName, errors),
                decks.Catalog);
            if (!characters.Succeeded)
            {
                errors.AddRange(characters.Errors);
            }

            if (errors.Count > 0)
            {
                return ContentBootstrapResult.Failed(errors);
            }

            return ContentBootstrapResult.Ok(new GameContent(
                cards.Catalog, decks.Catalog, pools.Catalog, characters.Catalog));
        }

        /// <summary>폴더가 없으면 던지지 않고 이유로 바꾼다 — 부팅은 모든 이유를 모아 보고한다.</summary>
        private static IReadOnlyList<CardContentSource> Read(
            string contentRoot, string folderName, List<string> errors)
        {
            var directory = Path.Combine(contentRoot, folderName);
            if (!Directory.Exists(directory))
            {
                errors.Add("Content directory not found: " + directory);
                return new CardContentSource[0];
            }

            return CardContentFiles.ReadDirectory(directory);
        }
    }
}
```

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`, 새 테스트 3개 증가

- [x] **Step 3: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: ContentBootstrap이 카탈로그 넷을 부팅 순서대로 만든다

카드 → 덱·풀 → 캐릭터. 어느 단계가 실패하든 모든 이유를 모아 보고한다.
소비자 전환은 다음 커밋.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: 소비자 둘을 카탈로그로 전환한다

**Files:**
- Create: `Assets/Unity/UnityContentRoot.cs` (+ `.meta`)
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/DeckPlaytestController.cs`
- Create: `Assets/Core/Tests/EditMode/ContentDrivenLoadoutTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `ContentBootstrap.Load`, `GameContent`
- Produces: `UnityContentRoot.Path` — `Application.streamingAssetsPath` 아래의 콘텐츠 루트

`BattleScreenController`가 지금 하는 일:

```csharp
            var loadouts = _party.Select(member => new PartyMemberLoadout(
                member.Id, member.DisplayName, tuning.DefaultMemberMaxHp,
                member.Deck.ToSpecs().Select(CardSpecMapper.ToDefinition).ToList())).ToList();
```

이후:

```csharp
            var loadouts = _party.Select(member => LoadoutFor(content, member.Id, tuning)).ToList();
```

**표시명도 JSON에서 온다.** `CharacterAsset.DisplayName`은 Task 7에서 사라진다 — 표시명은 표현이
아니라 콘텐츠이고, 3a가 이미 `Content/Characters/*.json`에 넣었다. `CharacterAsset`에 남는 것은
id와 Color뿐이다.

- [x] **Step 1: 로드아웃 조립 테스트를 먼저 쓴다 (RED)**

로드아웃 조립은 순수 로직이므로 코어에 두고 헤드리스로 잠근다. Create
`Assets/Core/Tests/EditMode/ContentDrivenLoadoutTests.cs`:

```csharp
using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>콘텐츠에서 조립한 파티 로드아웃이 공인 목록과 같은지 잠근다. 계획 3b가
    /// BattleScreenController를 이 경로로 옮긴다.</summary>
    public class ContentDrivenLoadoutTests
    {
        private static GameContent Content()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            var result = ContentBootstrap.Load(
                Path.Combine(directory, "Assets", "StreamingAssets", "Content"));
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Content;
        }

        [Test]
        public void LoadoutForBuildsTheAuthoredDeck()
        {
            var content = Content();

            var loadout = ContentLoadouts.For(content, "member_a", maxHp: 30);

            Assert.AreEqual("member_a", loadout.Id);
            Assert.AreEqual("파티원 A", loadout.Name);
            Assert.AreEqual(30, loadout.MaxHp);
            CollectionAssert.AreEqual(
                content.Decks.Get("starter").ToArray(),
                loadout.Cards.Select(card => card.Id).ToArray());
        }

        [Test]
        public void LoadoutSharesOneDefinitionPerCardId()
        {
            var content = Content();

            var loadout = ContentLoadouts.For(content, "member_b", maxHp: 30);
            var attacks = loadout.Cards.Where(card => card.Id == "fixture_attack").ToArray();

            Assert.AreEqual(2, attacks.Length, "party_prototype 덱은 fixture_attack을 둘 갖는다.");
            Assert.AreSame(
                attacks[0], attacks[1],
                "같은 카드 id는 정의 객체 하나를 참조해야 한다(설계 §4.5).");
        }
    }
}
```

Expected: 컴파일 실패 — `ContentLoadouts`가 없다

- [x] **Step 2: 로드아웃 조립기를 만든다 (GREEN)**

Create **`Assets/Core/Simulation/ContentLoadouts.cs`** (+ `.meta`). 코어가 아니라 시뮬레이션
어셈블리에 두는 이유는 `PartyMemberLoadout`이 `FateWeaver.Simulation`에 있고 `FateWeaver.Core`가
그것을 참조하지 않기 때문이다 (3a에서 확인한 asmdef 경계).

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>캐릭터 id 하나를 콘텐츠에서 파티 로드아웃으로 편다. 같은 카드 id는 카탈로그의
    /// 정의 객체 하나를 참조한다 — 소유 카드가 정의를 복제하지 않는다(설계 §4.5).</summary>
    public static class ContentLoadouts
    {
        public static PartyMemberLoadout For(GameContent content, string characterId, int maxHp)
        {
            var character = content.Characters.Get(characterId);
            var cards = new List<CardDefinition>();
            foreach (var cardId in content.Decks.Get(character.Deck))
            {
                cards.Add(content.Cards.Get(cardId));
            }

            return new PartyMemberLoadout(character.Id, character.DisplayName, maxHp, cards);
        }
    }
}
```

`PartyMemberLoadout`의 두 번째 인자는 `Name`이다(`DisplayName`이 아니다) — 생성자 인자명은
`name`이고 프로퍼티도 `Name`이다. 위 호출은 `CharacterContent.DisplayName`을 그 자리에 넘긴다.

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`, 새 테스트 2개 증가

- [x] **Step 3: Unity 쪽 콘텐츠 루트를 만든다**

Create `Assets/Unity/UnityContentRoot.cs`:

```csharp
using System.IO;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>StreamingAssets 아래 콘텐츠 루트. 경로 상수는 여기 하나뿐이고 나머지는 폴더
    /// 스캔이다(규칙 2·3, 설계 §4.5).</summary>
    public static class UnityContentRoot
    {
        private const string FolderName = "Content";

        public static string Path => System.IO.Path.Combine(
            Application.streamingAssetsPath, FolderName);
    }
}
```

- [x] **Step 4: `BattleScreenController`를 전환한다**

`_party` 필드 타입은 그대로 둔다. `StartSession()`을 고친다:

```csharp
        private GameContent _content;

        private void StartSession()
        {
            _selection.CancelSelection();
            if (_unitPrefab == null || _party == null || _party.Length == 0
                || _party.Any(member => member == null))
            {
                SetMessage("파티 CharacterAsset 또는 UnitView 프리팹이 연결되지 않았습니다.");
                return;
            }

            if (_content == null)
            {
                var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
                if (!loaded.Succeeded)
                {
                    SetMessage("콘텐츠 로드 실패:\n" + string.Join("\n", loaded.Errors));
                    Debug.LogError(string.Join("\n", loaded.Errors));
                    return;
                }

                _content = loaded.Content;
            }

            var tuning = PartyPrototypeRoster.Tuning;
            var loadouts = _party
                .Select(member => ContentLoadouts.For(
                    _content, member.Id, tuning.DefaultMemberMaxHp))
                .ToList();
            var enemies = new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) };
            _session = new DeckCombatSession(
                loadouts, enemies, GoblinDeck.Policy(), tuning,
                partyCards: null, fateEnergyPerTurn: FateEnergyPerTurn, seed: Seed);

            BuildArtLookup();
            SpawnUnits();
            BindPiles();
            SetMessage("전투 시작.");
            RefreshAll();
        }
```

`BuildArtLookup()`에서 덱을 훑는 루프를 지운다 (조사 2: 아무것도 넣지 않는다):

```csharp
        private void BuildArtLookup()
        {
            _artById.Clear();
            foreach (var card in _enemyArtCards) AddArt(card);
        }
```

- [x] **Step 5: `DeckPlaytestController`를 전환한다**

`[SerializeField] private DeckAsset _deck;`를 지우고 덱 id 문자열로 바꾼다:

```csharp
        [SerializeField] private string _deckId = "starter";
```

`StartSession()`의 첫 줄을 고친다:

```csharp
        private void StartSession()
        {
            var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
            if (!loaded.Succeeded)
            {
                SetMessage("콘텐츠 로드 실패:\n" + string.Join("\n", loaded.Errors));
                Debug.LogError(string.Join("\n", loaded.Errors));
                return;
            }

            var content = loaded.Content;
            var deckDefs = content.Decks.Get(_deckId)
                .Select(content.Cards.Get).ToList();
            var enemies = new[] { new Enemy(EnemyId(), EnemyStartingHp()) };
            _session = new DeckCombatSession(
                deckDefs, PlayerHp, enemies, EnemyPolicy(), FateEnergyPerTurn, HandSize, Seed);
            BuildArtLookup();
            ClearArmed();
            SetMessage("전투 시작.");
            RefreshAll();
        }
```

`StarterDeckSpecs` 폴백과 그 메시지를 지운다 — 폴백이 있으면 콘텐츠가 깨져도 조용히 돌아
"JSON이 원본"이 거짓말이 된다. `_deck != null` 분기를 쓰던 `BuildArtLookup()`도 `_enemyArtCards`
계열만 남기도록 같이 정리한다.

- [x] **Step 6: Unity 배치로 검증한다**

Run: Unity 배치 EditMode 전체
Expected: `failed="0"`. `.asset`이 수정되지 않았음을 `git status`로 확인한다.

- [x] **Step 7: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: 전투 씬과 플레이테스트가 JSON 콘텐츠를 읽는다

BattleScreenController·DeckPlaytestController가 SO 대신 ContentBootstrap의
카탈로그를 읽는다. 코드 시작덱 폴백을 지웠다 — 폴백이 있으면 콘텐츠가 깨져도
조용히 돌아 JSON이 원본이라는 말이 거짓이 된다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: `CardArtCatalog`을 만들고 적 아트를 옮긴다

**Files:**
- Create: `Assets/Unity/CardArtCatalog.cs` (+ `.meta`)
- Create: `Assets/Unity/CardSO/CardArt.asset` (+ `.meta`) — **사람이 인스펙터에서 만든다**
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/DeckPlaytestController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs:201` 부근

**Interfaces:**
- Produces: `CardArtCatalog.ArtFor(string id)` → `Sprite`

설계 §4.5의 "카드 아트는 id → Sprite 매핑 SO로 남는다"를 그대로 만든다. 항목은 셋뿐이다
(`goblin_jab`, `crude_guard`, `sly_jab`).

- [x] **Step 1: 카탈로그 타입을 만든다**

Create `Assets/Unity/CardArtCatalog.cs`:

```csharp
using System;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>id → Sprite 매핑. 카드 규칙은 JSON이 갖고 Unity는 표현만 담당한다(설계 §4.5).
    /// 플레이어 카드는 색상 틴트만 쓰므로 여기 항목은 아트가 실제로 있는 카드뿐이다.</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Card Art Catalog", fileName = "CardArt")]
    public sealed class CardArtCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Id;
            public Sprite Art;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public Sprite ArtFor(string id)
        {
            foreach (var entry in _entries)
            {
                if (entry.Id == id)
                {
                    return entry.Art;
                }
            }

            return null;
        }
    }
}
```

- [x] **Step 2: 사람이 에셋을 만들고 배선한다 (Unity GUI)**

**이 단계는 워크트리가 아니라 메인 체크아웃에서 사람이 한다** (규칙 17).

1. `Assets/Unity/CardSO/`에 `Create > Fate Weaver > Card Art Catalog`로 `CardArt.asset` 생성
2. 항목 셋을 넣는다 — id와 스프라이트는 기존 에셋에서 그대로 옮긴다:
   - `goblin_jab` ← `Assets/Unity/CardSO/Enemies/Goblin/goblin_jab.asset`의 `Art`
   - `crude_guard` ← `goblin_crude_guard.asset`의 `Art`
   - `sly_jab` ← `goblin_sly_jab.asset`의 `Art`
3. 전투 씬에서 `BattleScreenController`의 `_enemyArtCards` 자리에 `_cardArt`로 이 에셋을 연결
4. 플레이테스트 씬에서도 같은 필드를 연결

- [x] **Step 3: 컨트롤러 둘의 아트 경로를 교체한다**

`BattleScreenController`:

```csharp
        [SerializeField] private CardArtCatalog _cardArt;

        // _artById 사전과 BuildArtLookup·AddArt를 지운다.
        private Sprite ArtFor(string id) => _cardArt != null ? _cardArt.ArtFor(id) : null;
```

`StartSession()`에서 `BuildArtLookup();` 호출을 지운다. `DeckPlaytestController`도 같은 형태로
바꾼다.

`BattleSceneBuilder.cs`의 `EnemyArtCardPaths` 로드부(201줄 부근)를 `CardArtCatalog` 하나를
로드하는 것으로 바꾼다.

- [x] **Step 4: Unity 배치로 검증한다**

Run: Unity 배치 EditMode 전체
Expected: `failed="0"`

- [x] **Step 5: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "feat: 카드 아트를 id→Sprite 카탈로그로 분리한다

CardAsset 32개 중 아트가 있는 것은 적 카드 3개뿐이다. 규칙에서 아트를 떼어
Unity가 표현만 담당하게 한다(설계 §4.5).

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: SO와 코드 생성 경로를 제거한다

**Files:**
- Delete: `Assets/Unity/CardAsset.cs`, `DeckAsset.cs`, `CardPoolAsset.cs` (+ `.meta`)
- Delete: `Assets/Unity/Editor/CardCodeGenerator.cs` (+ `.meta`)
- Delete: `Assets/Core/Simulation/Generated/GeneratedCards.cs` (+ `.meta`)
- Delete: `Assets/Unity/CardSO/**/*.asset` (CardAsset 32 · DeckAsset 2 · CardPoolAsset 1, + `.meta`)
- Delete: `Assets/Tests/UnityEditMode/CardPoolAssetTests.cs`, `CardAssetAuthoringTests.cs`,
  `CardCodeGeneratorTests.cs`, `StarterPoolSeederTests.cs`, `StarterDeckAssetCompositionTests.cs`,
  `CardGradeTagMigrationTests.cs` (+ `.meta`)
- Delete: `Assets/Core/Tests/EditMode/GeneratedCardsTests.cs` (+ `.meta`)
- Modify: `Assets/Core/Authoring/EffectSpec.cs` (`ToLiteral()` 제거)
- Modify: `Assets/Core/Tests/EditMode/NewEffectLocalityTests.cs` (`ToLiteral` override 제거)
- Modify: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs` (`GeneratedCards` 축 제거)
- Modify: `Assets/Unity/CharacterAsset.cs` (`_displayName`·`_deck` 제거)
- Modify: `Assets/Unity/CardPresentation.cs`, `PlaytestCardArt.cs` (CardAsset 의존 제거)
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`

**Interfaces:**
- Produces: 카드 규칙의 원본이 `Content/Cards/*.json` 하나가 된 트리

`GeneratedCards`·`ToLiteral()` 제거는 색인이 3d로 배정했으나, `CardAsset`이 여기서 죽으면
`CardCodeGenerator`가 함께 죽고 그 산출물을 3d까지 남길 이유가 없다 (조사 3·4: 런타임 소비자 없음).
**색인을 Task 8에서 그렇게 고친다.**

`CharacterAsset`은 삭제하지 않고 축소한다 — `_party` 배선을 유지하기로 했다(씬 저작 경계).

- [x] **Step 1: 제거 대상이 정말 안 쓰이는지 다시 확인한다**

```bash
/usr/bin/grep -rn "GeneratedCards\.\|ToLiteral\|CardPoolAsset\|DeckAsset" Assets --include='*.cs' \
  | /usr/bin/grep -v "Assets/Core/Simulation/Generated/"
```
남은 참조가 위 Files 목록에 없는 파일에서 나오면 **멈추고 기록한다.**

- [x] **Step 2: 코드부터 지운다**

Files 목록의 `.cs`와 `.meta`를 지우고 Modify 대상을 고친다. `CharacterAsset`은 이렇게 남는다:

```csharp
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>캐릭터의 표현 데이터. id와 색뿐이다 — 표시명과 덱은 콘텐츠이므로
    /// Content/Characters/*.json이 갖는다(설계 §4.5).</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Character")]
    public sealed class CharacterAsset : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private Color _color;

        public string Id => _id;
        public Color Color => _color;
    }
}
```

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`. 테스트 수가 줄어든다(`GeneratedCardsTests` 제거분).

- [x] **Step 3: 에셋을 지운다**

```bash
git rm -r Assets/Unity/CardSO/Player Assets/Unity/CardSO/Enemies
git rm Assets/Unity/CardSO/StarterDeck.asset Assets/Unity/CardSO/StarterDeck.asset.meta
```
경로는 실제 트리에 맞춰 확인한 뒤 지운다. **적 아트 스프라이트 파일(.png)은 지우지 않는다** —
`CardArtCatalog`가 참조한다.

- [x] **Step 4: 사람이 `CharacterAsset` 에셋 둘을 정리한다 (Unity GUI)**

메인 체크아웃에서 `member_a`·`member_b`의 `CharacterAsset`을 열어 `Deck` 참조가 사라졌는지
확인한다. `_displayName`·`_deck`은 필드가 없어졌으므로 Unity가 조용히 버린다 — **이것이 README
함정 1의 정상 경로이며, 두 값 모두 JSON에 이미 있으므로 손실이 없다.**

- [x] **Step 5: Unity 배치로 최종 검증한다**

Run: Unity 배치 EditMode 전체
Expected: `failed="0"`. 지운 테스트만큼 총계가 줄어든다.

`git status`로 **의도하지 않은 `.asset` 수정이 없는지** 확인한다.

- [x] **Step 6: 커밋한다**

```bash
git status --short
git add -A Assets
git commit -m "refactor: 카드 SO와 코드 생성 경로를 제거한다

카드 규칙의 원본이 Content/Cards/*.json 하나가 된다. CardAsset이 죽으면서
CardCodeGenerator·GeneratedCards·EffectSpec.ToLiteral도 함께 죽는다 —
런타임 소비자가 없었다.

CharacterAsset은 id와 색만 남는다. 표시명과 덱은 콘텐츠라 JSON이 갖는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: 문서 색인을 갱신한다

**Files:**
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`
- Move: `docs/superpowers/plans/2026-08-03-runtime-content-switch.md` → `archive/plans/`

- [x] **Step 1: 이 계획을 완료로 표시하고 보관으로 옮긴다**

머리말의 상태를 `완료`로 고치고 `구현 결과` 절을 더한다 (3a의 형식을 따른다). 상대 링크의
깊이를 한 단계 늘린다.

- [x] **Step 2: README를 갱신한다**

1. `활성 계획과 로드맵` 표에서 이 계획 줄을 지운다
2. 카드 콘텐츠 흐름 표에서 3b를 **완료·머지**로, 3c를 **다음**으로 바꾼다
3. **3d의 범위에서 `GeneratedCards.cs`와 `ToLiteral` 제거를 뺀다** — 3b가 했다
4. `함정 셋`의 3번(런타임이 JSON을 읽지 않는다)을 고쳐 쓴다 — 이제 읽는다. 남은 원본 이중성은
   C# 스펙(3d가 지운다)과 적 카드(범위 밖)뿐이다
5. `넘어온 부채`의 `CardSO의 규칙 필드` 항목을 해결로 표시한다
6. `현재 수치`를 갱신한다

- [x] **Step 3: 최종 검증하고 커밋한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`

Run: Unity 배치 EditMode 전체
Expected: `failed="0"`

```bash
git status --short
git commit -am "docs: 계획 3b를 완료로 보관하고 3d 범위를 줄인다

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

## 열린 항목

- **적 카드 JSON화.** 이 계획의 범위 밖이다. 적 정책·행동 패턴 설계를 함께 건드려야 한다.
- **덱 id의 소유자.** 지금 캐릭터가 덱 id를 가리키지만, 런이 시작되면 덱은 캐릭터가 아니라 런
  상태에 속한다. 런 사이클 재설계(`needs-redesign`)가 이 경계를 정한다.
- **`CardArtCatalog`의 항목이 셋뿐이다.** 아트가 늘어나면 폴더 스캔으로 바꿀지 판단한다. 지금
  인스펙터 목록으로 두는 것은 규칙 3(런타임 문자열 탐색 금지)에 맞고 항목이 적어서다.
- **`ContentExportWriter`의 덱·풀 id 상수.** 3d가 라이터를 지울 때 상수의 거처를 정해야 한다.
