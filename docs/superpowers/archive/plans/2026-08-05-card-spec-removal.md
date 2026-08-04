# 계획 3d — C# 카드 스펙 제거 구현 계획

> **에이전트 작업자에게:** 필수 서브 스킬 — `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans`로 태스크 단위로 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

- 작성일: 2026-08-05
- 상태: `archived`
- 완료일: 2026-08-05 — 헤드리스 513/513, Unity EditMode 661 total / 654 passed / 0 failed / 7 skipped
- 상위 설계: [카드 변형과 런타임 콘텐츠 로딩](../../specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.5
- 선행: 계획 3b [런타임 콘텐츠 전환](2026-08-03-runtime-content-switch.md) **완료**,
  계획 3c [상태 원본 확정](2026-08-04-status-content-single-source.md) **완료**
- 후속: P1-B 프리팹화 → 에셋 폴더 재정리 (순서는
  [백로그 §7](../../plans/2026-07-16-architecture-refactor-backlog.md)에 기록)

**목표:** 카드 규칙을 담은 C# 코드를 전부 제거해 `Content/*/*.json`을 유일 원본으로 확정한다.
저작 스펙 3종, 카드 팩토리 2종, 내보내기 경로 3종이 사라진다.

**접근:** 지우기 전에 **대체 경로부터 세운다.** 테스트가 C# 카드에 의존하는 방식이 둘로 갈리므로
각각 다르게 처리한다 — 규칙 단위 테스트는 **의도가 드러나는 합성 픽스처**로, 콘텐츠 계약 테스트는
**JSON 카탈로그 조회**로 옮긴다. 두 원본이 같은지 대조하던 동등성 테스트는 원본이 하나가 되는
순간 존재 이유가 없어지므로 **삭제한다.**

**기술 스택:** C# (netstandard2.1), NUnit, Unity 6000.5.2f1

## 전역 제약

- **규칙 6:** `FateWeaver.Core`는 UnityEngine을 참조하지 않는다.
- **규칙 7 (결정론):** 새 코드에서 `System.Random`·`DateTime`·`Guid.NewGuid()`를 쓰지 않는다.
- **규칙 8:** 픽스처의 수치는 호출부에서 명명 인자로 넘긴다. 픽스처 안에 매직 넘버를 두지 않는다.
- **규칙 12:** 트리는 매 태스크 끝에서 초록이어야 한다. 지우기 전에 대체가 먼저다.
- **규칙 20:** 마지막 태스크에서 `docs/superpowers/README.md` 색인을 같은 커밋으로 갱신한다.
- **`.meta` 파일:** 새 `.cs`에는 Unity가 `.meta`를 생성한다. Unity 배치 실행 뒤 `git status`로
  확인해 같은 커밋에 포함한다. 파일 삭제 시 `.cs.meta`도 함께 지운다.
- **어셈블리 경계:** `FateWeaver.Tests.UnityEditMode`는 `FateWeaver.Tests.EditMode`를 **참조하지
  않는다**(2026-08-05 asmdef 실측). 코어 테스트용 픽스처를 Unity 테스트에서 쓸 수 없다.
- **적 카드는 범위 밖:** `GoblinDeck`·`WardenDeck`은 순수 C#으로 남는다. 적 정책·행동 패턴 설계가
  딸려 오므로 별도 계획이다.

## 검증 명령

**헤드리스** (모든 태스크 끝에서):

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

**Unity EditMode** (태스크 3·8 끝에서 최소 1회. `-quit`를 붙이면 테스트 없이 exit 0이 되므로
절대 붙이지 않는다):

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-card-spec-removal -runTests -testPlatform EditMode -testResults /private/tmp/plan-3d.xml -logFile /private/tmp/plan-3d.log
```

결과는 XML 루트의 `result=` / `total=` / `passed=` / `failed=` 속성으로 확인한다.

**시작 시점 기준선 (2026-08-05 실측):** 헤드리스 **533/533**,
Unity EditMode **682 total / 674 passed / 0 failed / 8 skipped**.

## 제거 대상과 근거

| 대상 | 성격 | 왜 지우나 |
|---|---|---|
| `StarterPoolSpecs` | 22장 카드를 C#으로 저작 | `Content/Cards/*.json`과 같은 카드를 두 벌 갖는다 |
| `StarterDeckSpecs` | 위에서 10장 선택 | `Content/Decks/starter.json`과 중복 |
| `PartyPrototypeDeckSpecs` | fixture 4종 저작 | `Content/Cards/fixture_*.json`과 중복 |
| `StarterDeck` | 카드 팩토리 + `Build()` | `Build()`는 스펙 경유(중복), 개별 팩토리는 **JSON에 없는 레거시 카드** |
| `PartyPrototypeDeck` | fixture 카드 팩토리 | `fixture_*` 4종이 JSON에 있다 |
| `PartyPrototypeRoster.Build()` | 로드아웃 조립 | `ContentLoadouts.For(content, id, hp)`가 대체한다 |
| `PartyPrototypeCharacterSpecs` | 캐릭터 저작 | `Content/Characters/*.json`과 중복 |
| `ContentExportWriter` | C# → JSON 내보내기 | 원본이 JSON이면 내보낼 것이 없다 |
| `CardContentExporter` | 위의 Unity 메뉴 껍데기 | 라이터와 함께 |

**남는 것:** `PartyPrototypeRoster`의 `Tuning`·`MemberAId`·`MemberAName`·`MemberBId`·`MemberBName`
(`BattleScreenController`가 `Tuning`을 쓴다), `CardSpec`·`CardSpecMapper`(JSON 로더가 쓴다),
`GoblinDeck`·`WardenDeck`(적 카드, 범위 밖).

## 파일 구조

| 파일 | 이 계획에서의 책임 |
|---|---|
| `Assets/Core/Tests/EditMode/CardFixtures.cs` | **신설** — 코어 규칙 테스트용 합성 카드. 이름이 카드 정체성이 아니라 **효과 모양**이다 |
| `Assets/Core/Tests/EditMode/TestContent.cs` | `Content()`·`Cards()`·`StarterDeckCards()` 추가 |
| `Assets/Tests/UnityEditMode/UnityTestContent.cs` | `Cards()` 추가 |
| `Assets/Tests/UnityEditMode/UnityCardFixtures.cs` | **신설** — Unity 테스트용. 어셈블리 경계 때문에 코어 픽스처를 못 쓴다 |
| `Assets/Core/Simulation/StarterDeck.cs` | **삭제** |
| `Assets/Core/Simulation/PartyPrototypeDeck.cs` | **삭제** |
| `Assets/Core/Simulation/PartyPrototypeCharacterSpecs.cs` | **삭제** |
| `Assets/Core/Simulation/PartyPrototypeRoster.cs` | `Build()`만 제거, 상수와 `Tuning`은 유지 |
| `Assets/Core/Authoring/StarterPoolSpecs.cs` | **삭제** |
| `Assets/Core/Authoring/StarterDeckSpecs.cs` | **삭제** |
| `Assets/Core/Authoring/PartyPrototypeDeckSpecs.cs` | **삭제** |
| `Assets/Core/Authoring/Json/ContentExportWriter.cs` | **삭제** |
| `Assets/Unity/Editor/CardContentExporter.cs` | **삭제** |
| `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs` | **삭제** — 원본이 하나면 대조할 상대가 없다 |
| `Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs` | **삭제** — 같은 이유 |
| `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs` | **삭제** — 규칙 3건은 Task 5에서 살려 옮긴다 |
| `Assets/Core/Tests/EditMode/ContentExportWriterTests.cs` | **삭제** |
| `Assets/Core/Tests/EditMode/StarterDeckTests.cs` | JSON 계약 테스트로 재작성 |
| `Assets/Core/Tests/EditMode/DeckPoolCharacterContentTests.cs` | 골든 문자열 배열로 재작성 |
| `Assets/Core/Tests/EditMode/StarterPoolSpecsTests.cs` | 규칙 테스트를 JSON 카드로 |
| `Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs` | 계약 테스트를 JSON으로, 중복 삭제 |

---

### Task 1: 코어 테스트 픽스처와 콘텐츠 진입점을 세운다

지우기 전에 대체를 먼저 만든다. 이 태스크가 끝나도 `StarterDeck`은 살아 있으므로 트리는 초록이다.

**Files:**
- Create: `Assets/Core/Tests/EditMode/CardFixtures.cs`
- Modify: `Assets/Core/Tests/EditMode/TestContent.cs`
- Test: `Assets/Core/Tests/EditMode/CardFixturesTests.cs` (신설)

**Interfaces:**
- Produces:
  - `CardFixtures.Damage(string id, int damage, int executionOrder = 5, int cost = 1) → CardDefinition`
  - `CardFixtures.Block(string id, int magnitude, int executionOrder = 5, int cost = 1) → CardDefinition`
  - `CardFixtures.DamageOnFirstTrigger(string id, int baseDamage, int whenFirst, int executionOrder = 5, int cost = 1) → CardDefinition`
  - `CardFixtures.DamageAfterEnemyDamage(string id, int baseDamage, int whenAfter, int executionOrder = 5, int cost = 1) → CardDefinition`
  - `CardFixtures.BlockBeforeEnemyDamage(string id, int baseMagnitude, int whenBefore, int executionOrder = 5, int cost = 1) → CardDefinition`
  - `CardFixtures.ChangeExecutionOrder(string id, int delta, int cost = 1) → CardDefinition`
  - `CardFixtures.SwapExecutionOrder(string id, int cost = 1) → CardDefinition`
  - `CardFixtures.EnemyAttack(string id, int executionOrder, int damage) → CardDefinition`
  - `TestContent.Content() → GameContent`, `TestContent.Cards() → CardContentCatalog`,
    `TestContent.StarterDeckCards() → IReadOnlyList<CardDefinition>`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/CardFixturesTests.cs`를 만든다:

```csharp
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>픽스처가 의도한 모양을 만드는지 잠근다. 픽스처는 콘텐츠가 아니라 테스트 입력이며,
    /// 이름이 카드 정체성이 아니라 효과 모양인 것이 요점이다.</summary>
    public class CardFixturesTests
    {
        [Test]
        public void DamageFixtureCarriesItsDamageAndCost()
        {
            var card = CardFixtures.Damage("fx", damage: 4, executionOrder: 3, cost: 2);

            Assert.AreEqual("fx", card.Id);
            Assert.AreEqual(3, card.BaseExecutionOrder);
            Assert.AreEqual(2, card.EnergyCost);
            Assert.AreEqual(CardCategory.Execution, card.Category);
            Assert.AreEqual(EffectKeys.Damage, card.Effects.Single().Key);
            Assert.AreEqual(4, card.Effects.Single().EffectValue);
        }

        [Test]
        public void ConditionalFixtureCarriesBothValues()
        {
            var card = CardFixtures.DamageOnFirstTrigger("fx", baseDamage: 2, whenFirst: 8);

            var effect = card.Effects.Single();
            Assert.AreEqual(2, effect.EffectValue);
            Assert.AreEqual(8, effect.SuccessEffectValue);
            Assert.IsNotNull(effect.Condition);
        }

        [Test]
        public void InterventionFixtureHasNoEffectsAndCarriesItsAction()
        {
            var card = CardFixtures.ChangeExecutionOrder("fx", delta: -1);

            Assert.AreEqual(CardCategory.Intervention, card.Category);
            Assert.AreEqual(0, card.Effects.Count);
            Assert.AreEqual(-1, card.InterventionAction.EffectValue);
        }

        [Test]
        public void StarterDeckCardsComeFromTheRepositoryJson()
        {
            var deck = TestContent.StarterDeckCards();

            Assert.AreEqual(10, deck.Count);
            CollectionAssert.Contains(deck.Select(card => card.Id).ToList(), "probing_strike");
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~CardFixturesTests
```

예상: 컴파일 실패 — `CardFixtures`와 `TestContent.StarterDeckCards`가 없다.

- [ ] **Step 3: 픽스처를 만든다**

`Assets/Core/Tests/EditMode/CardFixtures.cs`:

```csharp
using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>규칙 단위 테스트가 쓰는 합성 카드. **콘텐츠가 아니다** — 여기 있는 카드는
    /// Content/Cards/*.json에 없고, 있어서도 안 된다.
    ///
    /// 메서드 이름이 카드 정체성(`Slash`)이 아니라 **효과 모양**(`Damage`)인 것이 요점이다.
    /// 테스트가 왜 그 카드를 쓰는지가 호출부에서 보이고, 밸런스 조정이 규칙 테스트를 깨지 않는다.
    /// 실제 카드의 동작을 검증하려면 픽스처가 아니라 TestContent.Cards()를 쓴다.</summary>
    public static class CardFixtures
    {
        /// <summary>플레이어 카드의 기본 실행 순서. 적(6)보다 앞이라 "적보다 먼저 해결"이 기본이다.</summary>
        public const int DefaultExecutionOrder = 5;

        public static CardDefinition Damage(
            string id, int damage, int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(id, executionOrder, cost, new EffectData(EffectKeys.Damage, damage));

        public static CardDefinition Block(
            string id, int magnitude, int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, magnitude));

        public static CardDefinition DamageOnFirstTrigger(
            string id, int baseDamage, int whenFirst,
            int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.Conditional(
                    EffectKeys.Damage, baseDamage, new FirstToTrigger(), whenFirst));

        public static CardDefinition DamageAfterEnemyDamage(
            string id, int baseDamage, int whenAfter,
            int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.Conditional(
                    EffectKeys.Damage, baseDamage,
                    new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage), whenAfter));

        public static CardDefinition BlockBeforeEnemyDamage(
            string id, int baseMagnitude, int whenBefore,
            int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, baseMagnitude)
                    with
                    {
                        Condition = new AdjacentCardHasEffect(
                            AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage),
                        SuccessEffectValue = whenBefore
                    });

        public static CardDefinition ChangeExecutionOrder(string id, int delta, int cost = 1)
            => Intervention(
                id, cost,
                new InterventionActionData(
                    InterventionActionKeys.ChangeExecutionOrder,
                    interventionCost: cost, effectValue: delta));

        public static CardDefinition SwapExecutionOrder(string id, int cost = 1)
            => Intervention(
                id, cost,
                new InterventionActionData(
                    InterventionActionKeys.SwapExecutionOrder,
                    interventionCost: cost, effectValue: 0));

        /// <summary>적 의도 카드. 적 카드는 아직 JSON이 아니므로(별도 계획) 픽스처가 필요하다.</summary>
        public static CardDefinition EnemyAttack(string id, int executionOrder, int damage)
            => new CardDefinition(
                id, id, Side.Enemy, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) })
                { EnergyCost = 0, Category = CardCategory.Execution };

        private static CardDefinition Execution(
            string id, int executionOrder, int cost, EffectData effect)
            => new CardDefinition(id, id, Side.Player, executionOrder, new[] { effect })
                { EnergyCost = cost, Category = CardCategory.Execution };

        private static CardDefinition Intervention(
            string id, int cost, InterventionActionData action)
            => new CardDefinition(id, id, Side.Player, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = cost,
                    Category = CardCategory.Intervention,
                    InterventionAction = action
                };
    }
}
```

- [ ] **Step 4: `TestContent`에 콘텐츠 진입점을 추가한다**

`Assets/Core/Tests/EditMode/TestContent.cs`의 `Statuses()` 아래에 추가한다
(파일 상단 `using`에 `System.Collections.Generic;`과 `FateWeaver.Core.Cards;` 추가):

```csharp
        /// <summary>저장소 JSON 전체를 읽은 콘텐츠 번들. 상태 카탈로그와 같은 이유로 호출마다
        /// 새로 만든다 — 카탈로그의 Rules가 가변이다.</summary>
        public static GameContent Content()
        {
            var result = ContentBootstrap.Load(Root());
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Content;
        }

        public static CardContentCatalog Cards() => Content().Cards;

        /// <summary>Decks/starter.json이 지정한 10장을 정의 객체로 편다. 예전 StarterDeck.Build()의
        /// 대체이며, 원본이 JSON이라는 점만 다르다.</summary>
        public static IReadOnlyList<CardDefinition> StarterDeckCards()
        {
            var content = Content();
            var cards = new List<CardDefinition>();
            foreach (var id in content.Decks.Get("starter"))
            {
                cards.Add(content.Cards.Get(id));
            }

            return cards;
        }
```

- [ ] **Step 5: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0. 총계는 기준선 533 + 새 테스트 4 = **537**.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Tests/EditMode && git commit -m "test: 효과 모양으로 이름 붙인 카드 픽스처를 만든다"
```

---

### Task 2: 코어 테스트를 픽스처와 JSON으로 옮긴다

`StarterDeck`·`PartyPrototypeDeck` 호출을 코어 테스트에서 0으로 만든다. 아직 두 클래스는 살아 있다.

**Files:**
- Modify: `Assets/Core/Tests/EditMode/` 아래 `StarterDeck.`·`PartyPrototypeDeck.`을 부르는 전 파일

**Interfaces:**
- Consumes: Task 1의 `CardFixtures`, `TestContent.StarterDeckCards()`

- [ ] **Step 1: 전환 대상을 나열한다**

```bash
/usr/bin/grep -rln "StarterDeck\.\|PartyPrototypeDeck\." --include='*.cs' Assets/Core/Tests/EditMode
```

- [ ] **Step 2: 설명 계열을 옮긴다**

`DescriptionComposerTests.cs`의 카드 인자를 픽스처로 바꾼다. **단언 문자열은 그대로 둔다** —
같은 수치로 만들므로 결과가 같아야 한다. 예:

```csharp
// 전
Assert.AreEqual("[◆] 피해 4.",
    DescriptionComposer.Describe(StarterDeck.Slash(), Korean));
Assert.AreEqual("[◆] 피해 2. 첫 발동이면 피해 8.",
    DescriptionComposer.Describe(StarterDeck.QuickCut(), Korean));
Assert.AreEqual("[◆] 피해 4. 직전에 실행한 카드가 적 피해 카드이면 피해 9.",
    DescriptionComposer.Describe(StarterDeck.Counter(), Korean));
Assert.AreEqual("[◆] 방어 2. 바로 뒤가 적 피해 카드이면 방어 7.",
    DescriptionComposer.Describe(StarterDeck.Cover(), Korean));

// 후
Assert.AreEqual("[◆] 피해 4.",
    DescriptionComposer.Describe(CardFixtures.Damage("fx_damage", damage: 4), Korean));
Assert.AreEqual("[◆] 피해 2. 첫 발동이면 피해 8.",
    DescriptionComposer.Describe(
        CardFixtures.DamageOnFirstTrigger("fx_first", baseDamage: 2, whenFirst: 8), Korean));
Assert.AreEqual("[◆] 피해 4. 직전에 실행한 카드가 적 피해 카드이면 피해 9.",
    DescriptionComposer.Describe(
        CardFixtures.DamageAfterEnemyDamage("fx_after", baseDamage: 4, whenAfter: 9), Korean));
Assert.AreEqual("[◆] 방어 2. 바로 뒤가 적 피해 카드이면 방어 7.",
    DescriptionComposer.Describe(
        CardFixtures.BlockBeforeEnemyDamage("fx_before", baseMagnitude: 2, whenBefore: 7), Korean));
```

`DescriptionCatalogValidatorTests.cs`·`StructuredCardDescriptionTests.cs`도 같은 방식이다.
`StarterDeck.Build()`·`PartyPrototypeDeck.Build()`로 카드 목록을 훑던 곳은
`TestContent.StarterDeckCards()`와 `TestContent.Cards().Cards.Values`로 바꾼다.

- [ ] **Step 3: 세션·규칙 계열을 옮긴다**

`DeckCombatSessionTests.cs`는 카드 id로 손패를 찾으므로 픽스처 id를 그대로 유지한다:

```csharp
// 전 — 왜 Counter인지 주석이 설명하고 있었다
// deck of two counters (cost 2 each); energy 3 -> only one is affordable.
var session = NewSession(new[] { StarterDeck.Counter(), StarterDeck.Counter() }, Goblin(4, 3));
Assert.IsTrue(session.PlayExecutionCard(HandIndex(session, "counter_stance")));

// 후 — 비용 2가 호출부에 보이므로 주석이 필요 없다
var costTwo = CardFixtures.Damage("cost_two", damage: 4, cost: 2);
var session = NewSession(new[] { costTwo, costTwo }, Goblin(4, 3));
Assert.IsTrue(session.PlayExecutionCard(HandIndex(session, "cost_two")));
```

같은 파일의 나머지도 대응시킨다.

| 전 | 후 |
|---|---|
| `StarterDeck.Slash()` | `CardFixtures.Damage("slash_fx", damage: 4, executionOrder: 4)` |
| `StarterDeck.Guard()` | `CardFixtures.Block("guard_fx", magnitude: 4)` |
| `StarterDeck.QuickCut()` | `CardFixtures.DamageOnFirstTrigger("quick_fx", baseDamage: 2, whenFirst: 8)` |
| `StarterDeck.Counter()` | `CardFixtures.DamageAfterEnemyDamage("counter_fx", baseDamage: 4, whenAfter: 9, executionOrder: 7, cost: 2)` |
| `StarterDeck.Cover()` | `CardFixtures.BlockBeforeEnemyDamage("cover_fx", baseMagnitude: 2, whenBefore: 7)` |
| `StarterDeck.PullForward()` | `CardFixtures.ChangeExecutionOrder("pull_fx", delta: -1)` |
| `StarterDeck.PushBack()` | `CardFixtures.ChangeExecutionOrder("push_fx", delta: 1)` |
| `StarterDeck.SwapPositions()` | `CardFixtures.SwapExecutionOrder("swap_fx")` |
| `StarterDeck.EnemyAttack(id, name, order, dmg)` | `CardFixtures.EnemyAttack(id, order, dmg)` |
| `StarterDeck.DefaultExecutionOrder` | `CardFixtures.DefaultExecutionOrder` |
| `StarterDeck.Build()` | `TestContent.StarterDeckCards()` |
| `PartyPrototypeDeck.Attack()` | `TestContent.Cards().Get("fixture_attack")` |
| `PartyPrototypeDeck.SelectedBlock()` | `TestContent.Cards().Get("fixture_selected_block")` |
| `PartyPrototypeDeck.AllBlock()` | `TestContent.Cards().Get("fixture_all_block")` |
| `PartyPrototypeDeck.MoveForward()` | `TestContent.Cards().Get("fixture_move_forward")` |
| `PartyPrototypeDeck.Build()` | `TestContent.Cards()`에서 위 넷을 조립 |

`fixture_*` 넷은 **JSON에 실재하므로** 픽스처가 아니라 카탈로그에서 읽는다. 그 카드들의 동작을
검증하는 것이 테스트의 목적이기 때문이다.

- [ ] **Step 4: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0. 단언값을 바꾸지 않았으므로 총계도 537 그대로다. 값이 어긋나면 픽스처 인자가
원래 카드와 다른 것이므로 **단언이 아니라 인자를 고친다.**

- [ ] **Step 5: 잔여를 확인한다**

```bash
/usr/bin/grep -rn "StarterDeck\.\|PartyPrototypeDeck\." --include='*.cs' Assets/Core/Tests/EditMode
```

예상: 동등성 테스트 4파일(`CardContentEquivalenceTests`, `CardContentEquivalenceJsonTests`,
`StarterDeckSpecEquivalenceTests`, `StarterDeckTests`)과 `PartyPrototypeDataTests`만 남는다 —
Task 5가 이들을 처리한다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Tests/EditMode && git commit -m "test: 코어 테스트를 카드 픽스처와 JSON 카탈로그로 옮긴다"
```

---

### Task 3: Unity EditMode 테스트를 옮긴다

어셈블리 경계 때문에 코어 픽스처를 쓸 수 없으므로 Unity 쪽 진입점을 따로 만든다.

**Files:**
- Create: `Assets/Tests/UnityEditMode/UnityCardFixtures.cs`
- Modify: `Assets/Tests/UnityEditMode/UnityTestContent.cs`
- Modify: `Assets/Tests/UnityEditMode/CardPresentationTests.cs`

**Interfaces:**
- Produces: `UnityTestContent.Cards() → CardContentCatalog`,
  `UnityCardFixtures.ChangeExecutionOrder(string id, int delta, int cost = 1) → CardDefinition`

- [ ] **Step 1: Unity 콘텐츠 진입점을 넓힌다**

`Assets/Tests/UnityEditMode/UnityTestContent.cs`에 추가한다
(`using FateWeaver.Core.Authoring;`은 이미 있다):

```csharp
        /// <summary>저장소 JSON 전체. 상태와 같은 이유로 호출마다 새로 만든다.</summary>
        public static GameContent Content()
        {
            var result = ContentBootstrap.Load(UnityContentRoot.Path);
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Content;
        }

        public static CardContentCatalog Cards() => Content().Cards;
```

- [ ] **Step 2: Unity 픽스처를 만든다**

개입 카드는 JSON에 대응물이 없으므로(`pull_forward`는 제거된 레거시 카드다) 픽스처가 필요하다.
`Assets/Tests/UnityEditMode/UnityCardFixtures.cs`:

```csharp
using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>Unity EditMode 테스트용 합성 카드. 코어의 CardFixtures와 같은 역할이지만,
    /// FateWeaver.Tests.UnityEditMode가 FateWeaver.Tests.EditMode를 참조하지 않아 따로 둔다
    /// (asmdef 경계). 필요한 모양만 담는다 — 전부 옮기지 않는다.</summary>
    public static class UnityCardFixtures
    {
        public static CardDefinition ChangeExecutionOrder(string id, int delta, int cost = 1)
            => new CardDefinition(id, id, Side.Player, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = cost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        InterventionActionKeys.ChangeExecutionOrder,
                        interventionCost: cost, effectValue: delta)
                };
    }
}
```

- [ ] **Step 3: `CardPresentationTests`의 세 곳을 바꾼다**

```csharp
// 전 (117): PartyPrototypeDeck.MoveForward()
// 후 — fixture_move_forward는 JSON에 실재한다
var presentation = CardPresentation.FromDefinition(
    UnityTestContent.Cards().Get("fixture_move_forward"),
    Korean,
    id => null);

// 전 (129): CardSpecMapper.ToDefinition(StarterPoolSpecs.ToxicReclaim())
// 후 — toxic_reclaim도 JSON에 실재한다
var definition = UnityTestContent.Cards().Get("toxic_reclaim");

// 전 (140): CardPresentation.FromDefinition(StarterDeck.PullForward(), Korean)
// 후 — 개입 카드는 JSON에 없으므로 픽스처
var presentation = CardPresentation.FromDefinition(
    UnityCardFixtures.ChangeExecutionOrder("pull_fx", delta: -1), Korean);
```

`using FateWeaver.Core.Authoring;`·`using FateWeaver.Simulation;`이 쓰이지 않게 되면 정리한다.

- [ ] **Step 4: 헤드리스와 Unity EditMode를 모두 돌린다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-card-spec-removal -runTests -testPlatform EditMode -testResults /private/tmp/plan-3d-task3.xml -logFile /private/tmp/plan-3d-task3.log
```

예상: 둘 다 failed=0. 실행 뒤 `git status`로 폰트 아틀라스 같은 런타임 부산물이 섞이지 않았는지
확인한다(2026-08-03 `KoreanTMP.asset` 사례).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Tests/UnityEditMode && git commit -m "test: Unity 테스트를 JSON 카드와 전용 픽스처로 옮긴다"
```

---

### Task 4: 동등성 테스트를 정리한다

**원본이 하나가 되면 "두 원본이 같다"는 검증은 의미가 없다.** 대조 테스트는 삭제하고, 그 안에
섞여 있던 **규칙 검증 3건은 살려 옮긴다.**

**Files:**
- Delete: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs` (+ `.meta`)
- Delete: `Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs` (+ `.meta`)
- Rename: `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs` →
  `ConditionalCardRuleTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: Task 1의 `CardFixtures`

- [ ] **Step 1: 순수 대조 테스트 둘을 지운다**

`CardContentEquivalenceTests`(6개)와 `CardContentEquivalenceJsonTests`(4개)는 전부
"C# 스펙과 JSON이 같은 정의를 만든다"를 검증한다. C# 스펙이 사라지면 대조할 상대가 없다.

```bash
git rm Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs.meta Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs.meta
```

- [ ] **Step 2: 규칙 테스트 셋을 살려 이름을 바로잡는다**

`StarterDeckSpecEquivalenceTests`의 세 테스트는 이름과 달리 **대조 테스트가 아니다.** 파일 안에
직접 정의한 합성 스펙(`QuickCutFixture()`·`CounterFixture()`·`CoverFixture()`)으로 조건부 효과
셋을 세션 전체를 통해 검증한다. 값어치가 있으므로 살린다.

```bash
git mv Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs Assets/Core/Tests/EditMode/ConditionalCardRuleTests.cs
git mv Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs.meta Assets/Core/Tests/EditMode/ConditionalCardRuleTests.cs.meta
```

파일 안에서 다음을 바꾼다.

클래스 이름과 문서 주석:

```csharp
    /// <summary>조건부 효과 셋이 세션 전체(배치 → 개입 → 해결)를 통과해 옳게 동작하는지 잠근다.
    /// 입력은 합성 픽스처다 — 특정 카드의 밸런스가 아니라 조건 판정 규칙이 검증 대상이다.</summary>
    public class ConditionalCardRuleTests
```

파일 안의 `QuickCutFixture()`·`CounterFixture()`·`CoverFixture()`·`PullForwardFixture()`
스펙 정의와 `Def(...)` 헬퍼, `SelectedIds` 배열을 **모두 지우고** 픽스처 호출로 바꾼다:

```csharp
        [Test]
        public void Conditional_damage_on_first_trigger_uses_the_boosted_value()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[]
                {
                    CardFixtures.DamageOnFirstTrigger("quick_cut", baseDamage: 2, whenFirst: 8),
                    CardFixtures.ChangeExecutionOrder("pull_forward", delta: -1)
                },
                30, new[] { new Enemy("goblin", 100) }, Goblin(5, 3), 3, 5, 1);

            session.PlayExecutionCard(HandIndex(session, "quick_cut"));
            session.PlayInterventionCard(
                HandIndex(session, "pull_forward"), ZoneIndex(session, "quick_cut"));

            Assert.AreEqual(8, DamageOf(session.ResolveTurn(), "quick_cut"));
        }

        [Test]
        public void Conditional_damage_after_an_enemy_damage_card_uses_the_boosted_value()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[]
                {
                    CardFixtures.DamageAfterEnemyDamage(
                        "counter_stance", baseDamage: 4, whenAfter: 9, executionOrder: 7, cost: 2)
                },
                30, new[] { new Enemy("goblin", 100) }, Goblin(6, 4), 3, 5, 1);

            session.PlayExecutionCard(HandIndex(session, "counter_stance"));

            Assert.AreEqual(9, DamageOf(session.ResolveTurn(), "counter_stance"));
        }

        [Test]
        public void Conditional_block_before_an_enemy_damage_card_absorbs_the_hit()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[]
                {
                    CardFixtures.BlockBeforeEnemyDamage("cover", baseMagnitude: 2, whenBefore: 7)
                },
                30, new[] { new Enemy("goblin", 100) }, Goblin(6, 3), 3, 5, 1);

            session.PlayExecutionCard(HandIndex(session, "cover"));
            int hp = session.State.Party[0].Hp;
            session.ResolveTurn();

            Assert.AreEqual(hp, session.State.Party[0].Hp);
        }
```

`Spec_deck_has_same_composition`은 **삭제한다** — 덱 구성 골든은 Task 5가
`DeckPoolCharacterContentTests`에서 JSON 기준으로 맡는다.

파일의 `Goblin(...)` 헬퍼가 쓰는 `StarterDeck.EnemyAttack`을 `CardFixtures.EnemyAttack`으로
바꾼다(Task 2의 대응표와 같다). 쓰이지 않게 된 `using`(`FateWeaver.Core.Authoring` 등)을 정리한다.

- [ ] **Step 3: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0. 총계는 537 − 10(지운 대조 테스트) − 1(`Spec_deck_has_same_composition`) = **526**.

- [ ] **Step 4: 커밋**

```bash
git add -A Assets/Core/Tests/EditMode && git commit -m "test: 대조 테스트를 지우고 조건부 규칙 테스트를 픽스처로 살린다"
```

---

### Task 5: 콘텐츠 계약 테스트를 JSON 골든으로 다시 쓴다

"덱이 10장이다", "풀이 22장이다" 같은 계약은 여전히 유효하다. 대조 상대를 C# 스펙에서
**골든 문자열 배열**로 바꾼다.

**Files:**
- Modify: `Assets/Core/Tests/EditMode/DeckPoolCharacterContentTests.cs`
- Modify: `Assets/Core/Tests/EditMode/StarterDeckTests.cs`
- Modify: `Assets/Core/Tests/EditMode/StarterPoolSpecsTests.cs`
- Modify: `Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs`

- [ ] **Step 1: 덱·풀·캐릭터 계약을 골든으로 바꾼다**

`DeckPoolCharacterContentTests.cs`에서 C# 스펙과 대조하던 셋을 배열 리터럴과 대조하도록 바꾼다.
값은 `Assets/StreamingAssets/Content/Decks/starter.json`에서 그대로 옮겨 적는다:

```csharp
        /// <summary>추첨으로 고정된 10장. 순서까지 계약이다 — 무작위 시작 덱 설계 §3이
        /// 역할 순서로 고정한다고 정했다.</summary>
        private static readonly string[] StarterDeckGolden =
        {
            "probing_strike", "delayed_strike", "quick_cover", "early_guard", "breather",
            "hasten", "toxic_reclaim", "early_onset", "spore_veil", "last_drop"
        };

        [Test]
        public void StarterDeckJsonMatchesTheGoldenTenCards()
        {
            CollectionAssert.AreEqual(StarterDeckGolden, TestContent.Content().Decks.Get("starter"));
        }
```

풀 22장과 캐릭터 둘도 같은 형태로 쓴다. 풀의 골든은 `Content/Pools/starter.json`에서,
캐릭터는 `Content/Characters/*.json`에서 옮긴다.

`EveryContentFileHasAUnityMetaSibling`·`EveryCatalogLoadsTogetherWithoutErrors`는 C# 스펙을
쓰지 않으므로 **그대로 둔다.**

- [ ] **Step 2: `StarterDeckTests`를 다시 쓴다**

```csharp
[Test]
public void StarterDeckHasTenDistinctCards()
{
    var deck = TestContent.Content().Decks.Get("starter");

    Assert.AreEqual(10, deck.Count);
    CollectionAssert.AllItemsAreUnique(deck);
}

[Test]
public void EveryInterventionCardCostMatchesItsActionCost()
{
    foreach (var card in TestContent.StarterDeckCards())
    {
        if (card.Category != CardCategory.Intervention)
        {
            continue;
        }

        Assert.AreEqual(
            card.EnergyCost, card.InterventionAction.InterventionCost, card.Id);
    }
}
```

- [ ] **Step 3: `StarterPoolSpecsTests`를 JSON 카드로 옮긴다**

이 파일의 다섯 테스트가 쓰는 카드는 **전부 JSON에 실재한다**(`riposte`·`quick_cover`·`crossover`·
`hasten`·`delay`·`breather`). 실제 카드의 동작을 보는 것이 목적이므로 픽스처가 아니라 카탈로그에서
읽는다. 치환은 기계적이다:

```csharp
// 전
CardSpecMapper.ToDefinition(StarterPoolSpecs.Riposte())
CardSpecMapper.ToDefinition(StarterPoolSpecs.QuickCover())
CardSpecMapper.ToDefinition(StarterPoolSpecs.Crossover())
CardSpecMapper.ToDefinition(StarterPoolSpecs.Hasten())
CardSpecMapper.ToDefinition(StarterPoolSpecs.Delay())
CardSpecMapper.ToDefinition(StarterPoolSpecs.Breather())

// 후
TestContent.Cards().Get("riposte")
TestContent.Cards().Get("quick_cover")
TestContent.Cards().Get("crossover")
TestContent.Cards().Get("hasten")
TestContent.Cards().Get("delay")
TestContent.Cards().Get("breather")
```

카탈로그 조회가 매번 파일을 읽으므로 클래스 상단에 한 번 받아둔다:

```csharp
        private static readonly CardContentCatalog Pool = TestContent.Cards();
```

`All_pool_specs_validate_against_default_registries`는 **삭제한다** — 저작 검증은
`ContentBootstrap.Load`가 부팅에서 하고, 그 성공은 같은 파일의
`EveryCatalogLoadsTogetherWithoutErrors`(`DeckPoolCharacterContentTests`)가 이미 잠근다.

파일 이름이 더 이상 내용과 맞지 않으므로 바꾼다:

```bash
git mv Assets/Core/Tests/EditMode/StarterPoolSpecsTests.cs Assets/Core/Tests/EditMode/StarterPoolCardRuleTests.cs
git mv Assets/Core/Tests/EditMode/StarterPoolSpecsTests.cs.meta Assets/Core/Tests/EditMode/StarterPoolCardRuleTests.cs.meta
```

클래스 이름도 `StarterPoolCardRuleTests`로 바꾼다.

- [ ] **Step 4: `PartyPrototypeDataTests`를 정리한다**

`Hand_coded_and_authored_specs_map_to_equal_definitions`는 동등성 테스트이므로 **삭제한다.**
`Roster_assigns_distinct_character_owners`는 `PartyPrototypeRoster.Build()` 대신
`ContentLoadouts.For`를 쓰도록 바꾼다:

```csharp
[Test]
public void ContentAssignsDistinctCharacterOwners()
{
    var content = TestContent.Content();
    var tuning = PartyPrototypeRoster.Tuning;

    var memberA = ContentLoadouts.For(content, "member_a", tuning.DefaultMemberMaxHp);
    var memberB = ContentLoadouts.For(content, "member_b", tuning.DefaultMemberMaxHp);

    Assert.AreNotEqual(memberA.Id, memberB.Id);
    Assert.AreEqual("파티원 A", memberA.Name);
    Assert.AreEqual("파티원 B", memberB.Name);
}
```

나머지 계약 테스트(`Prototype_deck_contains_only_validation_prefixed_cards` 등)는 카드 목록을
`TestContent.Content().Decks.Get("party_prototype")`에서 얻도록 바꾼다.

- [ ] **Step 5: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

- [ ] **Step 6: 잔여를 확인한다**

```bash
/usr/bin/grep -rn "StarterPoolSpecs\|StarterDeckSpecs\|PartyPrototypeDeckSpecs\|StarterDeck\.\|PartyPrototypeDeck\." --include='*.cs' Assets/Core/Tests Assets/Tests
```

예상: 출력 없음. 테스트가 C# 카드 정의에서 완전히 분리됐다.

- [ ] **Step 7: 커밋**

```bash
git add -A Assets/Core/Tests/EditMode && git commit -m "test: 콘텐츠 계약을 JSON 골든 대조로 다시 쓴다"
```

---

### Task 6: 카드 팩토리와 로스터 조립을 제거한다

**Files:**
- Delete: `Assets/Core/Simulation/StarterDeck.cs` (+ `.meta`)
- Delete: `Assets/Core/Simulation/PartyPrototypeDeck.cs` (+ `.meta`)
- Modify: `Assets/Core/Simulation/PartyPrototypeRoster.cs`

- [ ] **Step 1: 로스터에서 `Build()`를 뺀다**

`PartyPrototypeRoster.cs`를 다음으로 바꾼다. 상수와 `Tuning`은 `BattleScreenController`가 쓰므로
남긴다:

```csharp
namespace FateWeaver.Simulation
{
    /// <summary>파티 프로토타입의 id·표시명·튜닝. 로드아웃 조립은 콘텐츠가 한다 —
    /// ContentLoadouts.For(content, id, maxHp)가 Characters/Decks/Cards JSON을 편다.</summary>
    public static class PartyPrototypeRoster
    {
        public const string MemberAId = "member_a";
        public const string MemberAName = "파티원 A";
        public const string MemberBId = "member_b";
        public const string MemberBName = "파티원 B";

        public static PartyTuning Tuning => PartyTuning.Prototype;
    }
}
```

`using System.Collections.Generic;`이 쓰이지 않으므로 지운다.

- [ ] **Step 2: 두 파일을 지운다**

```bash
git rm Assets/Core/Simulation/StarterDeck.cs Assets/Core/Simulation/StarterDeck.cs.meta Assets/Core/Simulation/PartyPrototypeDeck.cs Assets/Core/Simulation/PartyPrototypeDeck.cs.meta
```

- [ ] **Step 3: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0. 컴파일 오류가 나면 Task 2·5가 놓친 호출부이므로 그것을 먼저 고친다.

- [ ] **Step 4: 커밋**

```bash
git add -A Assets/Core/Simulation && git commit -m "refactor: 카드 팩토리와 로스터 조립을 제거한다"
```

---

### Task 7: 저작 스펙과 내보내기 경로를 제거한다

**Files:**
- Delete: `Assets/Core/Authoring/StarterPoolSpecs.cs`, `StarterDeckSpecs.cs`,
  `PartyPrototypeDeckSpecs.cs` (+ 각 `.meta`)
- Delete: `Assets/Core/Authoring/Json/ContentExportWriter.cs` (+ `.meta`)
- Delete: `Assets/Core/Simulation/PartyPrototypeCharacterSpecs.cs` (+ `.meta`)
- Delete: `Assets/Unity/Editor/CardContentExporter.cs` (+ `.meta`)
- Delete: `Assets/Core/Tests/EditMode/ContentExportWriterTests.cs` (+ `.meta`)

- [ ] **Step 1: 전부 지운다**

```bash
git rm Assets/Core/Authoring/StarterPoolSpecs.cs Assets/Core/Authoring/StarterPoolSpecs.cs.meta Assets/Core/Authoring/StarterDeckSpecs.cs Assets/Core/Authoring/StarterDeckSpecs.cs.meta Assets/Core/Authoring/PartyPrototypeDeckSpecs.cs Assets/Core/Authoring/PartyPrototypeDeckSpecs.cs.meta Assets/Core/Authoring/Json/ContentExportWriter.cs Assets/Core/Authoring/Json/ContentExportWriter.cs.meta Assets/Core/Simulation/PartyPrototypeCharacterSpecs.cs Assets/Core/Simulation/PartyPrototypeCharacterSpecs.cs.meta Assets/Unity/Editor/CardContentExporter.cs Assets/Unity/Editor/CardContentExporter.cs.meta Assets/Core/Tests/EditMode/ContentExportWriterTests.cs Assets/Core/Tests/EditMode/ContentExportWriterTests.cs.meta
```

- [ ] **Step 2: 잔여가 없음을 확인한다**

```bash
/usr/bin/grep -rn "StarterPoolSpecs\|StarterDeckSpecs\|PartyPrototypeDeckSpecs\|PartyPrototypeCharacterSpecs\|ContentExportWriter\|CardContentExporter\|StarterDeck\b\|PartyPrototypeDeck\b" --include='*.cs' Assets
```

예상: 출력 없음.

- [ ] **Step 3: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

- [ ] **Step 4: 커밋**

```bash
git add -A Assets && git commit -m "refactor: C# 카드 저작 스펙과 내보내기 경로를 제거한다"
```

---

### Task 8: 검증하고 문서를 갱신한다

**Files:**
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/specs/2026-07-30-card-mutation-and-runtime-content-design.md`
- Modify: `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md`
- Move: 이 문서를 `docs/superpowers/archive/plans/`로
- Modify: `docs/superpowers/archive/README.md`

- [ ] **Step 1: 최종 검증**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-card-spec-removal -runTests -testPlatform EditMode -testResults /private/tmp/plan-3d-final.xml -logFile /private/tmp/plan-3d-final.log
```

예상: 둘 다 failed=0. 실행 뒤 `git status`로 Unity 부산물이 섞이지 않았는지 확인한다.

- [ ] **Step 2: Unity 메뉴가 사라진 것을 확인한다**

`Fate Weaver/Export Card Content to JSON` 메뉴가 없어야 한다. 로그에 `CardContentExporter`
관련 컴파일 경고가 없는지 `/private/tmp/plan-3d-final.log`에서 확인한다.

- [ ] **Step 3: 문서를 갱신한다 (규칙 20)**

`docs/superpowers/README.md`에서:
- "진행 중인 작업 흐름" 표의 3d 행을 **완료**로 바꾸고 `archive/plans/` 경로로 링크한다.
- 같은 절 아래 범위 표의 3d 행에 취소선과 **완료**를 넣는다.
- "새 세션이 먼저 알아야 할 함정 셋" 3번의 "남은 이중성 둘" 중 (a)를 해결됨으로 고친다.
  (b) 적 카드는 그대로 남는다.
- "현재 수치"의 테스트 총계를 Step 1의 실측으로 갱신한다.
- "활성 계획과 로드맵" 표에서 이 계획 행을 지운다.

`specs/2026-07-30-card-mutation-and-runtime-content-design.md` §4.5의 인용 블록에서
"**3d에 남은 것**" 문단을 완료로 고친다. 계획 3.5와 4가 다음이라고 적는다.

`plans/2026-07-16-architecture-refactor-backlog.md` §0 표의 §6 P1-A 행에 3d 완료를 반영한다.

이 계획 문서를 `docs/superpowers/archive/plans/`로 옮기고 머리말 상태를 `archived`로 바꾼다.
`docs/superpowers/archive/README.md`의 "카드 콘텐츠 JSON 로딩" 절에 한 줄 추가한다.

- [ ] **Step 4: 커밋**

```bash
git add -A && git commit -m "refactor: 카드 규칙의 C# 원본을 지우고 JSON을 유일 원본으로 확정한다"
```

---

## 완료 기준

1. 다음 grep이 아무것도 찾지 못한다:
   ```bash
   /usr/bin/grep -rn "StarterPoolSpecs\|StarterDeckSpecs\|PartyPrototypeDeckSpecs\|PartyPrototypeCharacterSpecs\|ContentExportWriter\|CardContentExporter" --include='*.cs' Assets
   ```
2. `StarterDeck`·`PartyPrototypeDeck` 타입이 존재하지 않는다.
3. `PartyPrototypeRoster`에 `Build()`가 없고 `Tuning`과 id·표시명 상수만 남는다.
4. 테스트가 카드를 얻는 경로는 둘뿐이다 — 합성 픽스처(`CardFixtures`·`UnityCardFixtures`)와
   JSON 카탈로그(`TestContent`·`UnityTestContent`).
5. 헤드리스와 Unity EditMode가 모두 failed=0.
6. `Assets/StreamingAssets/Content/`가 카드·상태·덱·풀·캐릭터의 유일한 원본이다.

## 이 계획이 열어주는 것

- **계획 3.5 (개입 액션 다형화·카드 스펙 분리)** — `CardSpec`이 실행/개입으로 쪼개진다.
  지금은 `lock` 카드가 안 쓰는 칸 넷을 들고 있다.
- **계획 4 (`CardMutation`)** — 카드 변형의 기반. `OwnedCard`가 영구·전투 변형 2목록을 갖는다.
- **적 카드 JSON 전환** — 남은 마지막 C# 카드 정의(`GoblinDeck`·`WardenDeck`)다. 적 정책·행동
  패턴 설계가 선행되어야 한다.

## 범위 밖

- **적 카드의 JSON 전환.** 위 이유로 별도 계획이다.
- **`CardSpec`·`CardSpecMapper` 제거.** JSON 로더가 쓰는 현행 경로이며 계획 3.5의 몫이다.
- **에셋 폴더 재정리와 P1-B 프리팹화.** 3d 이후 순서로 합의되어 있다
  ([백로그 §7](../../plans/2026-07-16-architecture-refactor-backlog.md)).
