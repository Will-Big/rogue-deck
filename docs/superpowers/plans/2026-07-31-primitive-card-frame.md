# Primitive Card Frame and Structured Description Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 실행·개입 카드가 서로 다른 프리미티브 프레임을 사용하고, 순수 C#이 대상 의미·설명 줄·심볼 평문을 구조화하며, 손패가 4:3부터 21:9까지 간격과 전체 스케일만으로 대응하게 한다.

**Architecture:** 코어의 닫힌 위치 범위와 카드 실행 시작 시점 대상 스냅샷이 실제 효과 대상을 소유하고, Simulation 설명 레지스트리는 같은 의미를 `CardDescriptionLayout`으로 합성한다. Unity 경계의 `CardPresentation`은 구조화 결과를 그대로 전달하며, `CardPrefabCatalog`가 카테고리별 전체 카드 프리팹과 재사용 심볼·설명 줄 프리팹을 제공한다. 모든 전체 카드 소비처는 카탈로그만 참조하고, `HandFanView`는 프리팹 내부 좌표 대신 간격과 공통 스케일만 계산한다.

**Tech Stack:** Unity 6000.5.2f1, C# 9, .NET 6/net5.0 headless harness, NUnit 3, uGUI, TextMeshPro, ScriptableObject/YAML prefabs, Unity EditMode batch tests

## Global Constraints

- 작업 위치는 `/Users/ish/Git/rogue-deck-card-frame-design`, 브랜치는 `refactor/card-frame-design`이다. 새 워크트리·브랜치를 만들거나 메인 체크아웃의 브랜치를 전환하지 않는다.
- 권위 설계는 `docs/superpowers/specs/2026-07-31-primitive-card-frame-design.md`와 선행 문서 `docs/superpowers/specs/2026-07-27-position-targeting-card-text-design.md`다.
- `FateWeaver.Core`와 `FateWeaver.Simulation`은 `UnityEngine`을 참조하지 않는다. C# 9 제약 때문에 `record struct` 대신 명시적 `readonly struct`와 `IEquatable<T>`를 사용한다.
- 무작위 대상 선택을 제거한다. 규칙 코드에 새 `System.Random`, `DateTime`, `Guid.NewGuid()`를 도입하지 않는다.
- `TargetSelectorRef`의 기존 직렬화 값 `2`(`SecondFromFront`)와 `4`(`Random`)를 새 범위에 재사용하지 않는다. 정의되지 않은 값으로 남겨 부팅 검증이 오래된 에셋을 확실히 거부하게 한다.
- 새 효과의 대상 의미는 중앙 switch가 아니라 효과 핸들러 계약과 레지스트리를 통해 확장한다.
- 런타임 `new GameObject`, `GameObject.Find`, `FindObjectOfType`, 태그·레이어 이름 비교, `Resources.Load` 호출, 카드 ID·파일 경로 기반 프리팹 선택을 추가하지 않는다.
- Unity 인스펙터 참조는 `[SerializeField] private`으로 유지하고, 전체 카드·대상 심볼·설명 줄은 프리팹으로 저장한다.
- 카드 아트, 카드 수치, 효과 순서, 개입 대상 규칙, `RailCardView` 레이아웃은 변경하지 않는다.
- 카드 내부 좌표는 프리팹이 소유한다. 반응형 코드는 손패 간격과 `Content` 루트의 균일 스케일만 계산하며 `LateUpdate()`에서 좌표를 덮어쓰지 않는다.
- 헤드리스 명령은 `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`다.
- Unity 자동 검증은 지정 워크트리를 `-projectPath`로 사용하고 결과·로그를 `/private/tmp`에 쓴다. GUI Play·씬/프리팹 수동 저작은 사용자가 별도로 요청하기 전에는 수행하지 않는다.
- 새 `Assets/` 파일은 Unity가 생성한 1:1 `.meta`와 함께 커밋한다. 각 task는 RED → 최소 GREEN → 관련 회귀 → 제한 스테이징 → 커밋 순서를 지킨다.

---

## File Map

### Core target schema and runtime

- Create `Assets/Core/Cards/CardTarget.cs`: `CardTargetFaction`, `CardTargetRange`, 값 동등성을 갖는 `CardTargetKey`.
- Modify `Assets/Core/Cards/TargetSelector.cs`: `FrontOne`, `FrontTwo`, `BackOne`, `BackTwo`, `All` 닫힌 집합.
- Create `Assets/Core/Combat/CardTargetSnapshot.cs`: 카드 실행 시작 시 진영별 대상 객체를 한 번 확정하고 효과 사이에 공유.
- Modify `Assets/Core/Effects/IEffectHandler.cs`: 각 핸들러가 `TargetFor(CardDefinition, EffectData)`를 선언하고 `EffectContext`가 스냅샷을 전달.
- Modify `Assets/Core/Combat/TurnResolver.cs`: 효과 실행 전에 대상 의미를 수집·검증하고 단일 스냅샷 생성.
- Modify `Assets/Core/Combat/EnemyTargeting.cs`, `Assets/Core/Combat/PartyTargeting.cs`: 앞/뒤 1·2·전체 집합 선택과 중복 없는 순서 보존.
- Modify `Assets/Core/Effects/DamageHandler.cs`, `ApplyStatusHandler.cs`, `ConsumeStatusHandler.cs`, `TriggerStatusHandler.cs`, `MoveFormationHandler.cs`, `GrantNextTurnFateHandler.cs`, `GrantNextPlayerDamageCardBonusHandler.cs`, `NullifyNextPlayerConditionRewardHandler.cs`: 대상 의미 선언 및 스냅샷 소비.

### Authoring and structured descriptions

- Modify `Assets/Core/Simulation/Authoring/EffectSpec.cs`, `Specs/DamageSpec.cs`, `Specs/ApplyStatusSpec.cs`, `Specs/ConsumeStatusSpec.cs`, `Specs/TriggerStatusSpec.cs`: 명시적 `TargetSelectorRef` 숫자, 안전한 매핑, 정의되지 않은 값 검증.
- Modify `Assets/Core/Simulation/Authoring/StarterPoolSpecs.cs` and `Generated/GeneratedCards.cs`: 새 선택자 이름으로 원본과 산출물 동기화.
- Create `Assets/Core/Simulation/Descriptions/CardDescriptionLayout.cs`: `EffectDescriptionFragment`, `CardDescriptionLine`, `CardDescriptionLayout`.
- Modify `DescriptionContracts.cs`, `DescriptionComposer.cs`, `BuiltInEffectDescriptionHandlers.cs`, `KoreanDescriptionGrammar.cs`, `KoreanDescriptionCatalog.cs`: 구조화 조각·줄·심볼 평문 계약.
- Modify `DescriptionCatalogValidator.cs`: 카드 ID가 포함된 범위 충돌, 지원하지 않는 직접 선택, 빈 조각 검증.

### Unity presentation and assets

- Modify `Assets/Unity/CardPresentation.cs`: `CardDescriptionLayout` 전달.
- Create `Assets/Unity/CardPrefabCatalog.cs` and `Assets/Unity/CardPrefabCatalog.asset`: 실행·개입·대상 심볼·설명 줄 프리팹 참조와 카테고리 조회.
- Create `Assets/Unity/TargetGlyphView.cs`, `Assets/Unity/DescriptionLineView.cs` and matching prefabs.
- Move `Assets/Unity/Prefabs/CardView.prefab` to `ExecutionCardView.prefab` while preserving its `.meta` GUID.
- Create `Assets/Unity/Prefabs/InterventionCardView.prefab`: 대상 패널·실행 순서 없는 별도 레이아웃.
- Modify `CardView.cs`, `HandFanView.cs`, `PileView.cs`, `ExecutionRailView.cs`, `DeckPlaytestController.cs`, `Editor/BattleSceneBuilder.cs`.
- Create `Assets/Core/Simulation/Presentation/ResponsiveHandLayout.cs`: 순수 간격·스케일 계산.
- Delete seven `Assets/Unity/Resources/Cards/Frame/fw_*_poster_v2.png` files and matching `.meta` only after the reference audit is empty.

### Tests

- Create `Assets/Core/Tests/EditMode/CardTargetSnapshotTests.cs`, `StructuredCardDescriptionTests.cs`, `ResponsiveHandLayoutTests.cs`.
- Modify targeting, mapper, authoring, description, content-equivalence, starter-pool description tests under `Assets/Core/Tests/EditMode/`.
- Create `Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs`, `CardFramePrefabTests.cs`, `CardFrameResponsiveLayoutTests.cs`, `CardFrameRenderCapture.cs`.
- Modify `CardPresentationTests.cs`, `HandFanHoverTests.cs`, `ExecutionRailInputTests.cs`, `CardCodeGeneratorTests.cs`, and affected controller tests.

---

### Task 1: Close the target schema and preserve serialized invalid values

**Files:**
- Create: `Assets/Core/Cards/CardTarget.cs`
- Modify: `Assets/Core/Cards/TargetSelector.cs`
- Modify: `Assets/Core/Simulation/Authoring/EffectSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/Specs/DamageSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/Specs/ApplyStatusSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/Specs/ConsumeStatusSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/Specs/TriggerStatusSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/StarterPoolSpecs.cs`
- Modify: `Assets/Core/Simulation/Generated/GeneratedCards.cs`
- Modify: `Assets/Core/Status/ContagionBehavior.cs`
- Test: `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`
- Test: `Assets/Core/Tests/EditMode/AuthoringValidationTests.cs`
- Test: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs`

**Interfaces:**
- Produces: `CardTargetKey(CardTargetFaction faction, CardTargetRange range)` with structural equality.
- Produces: `TargetSelector.FrontOne|FrontTwo|BackOne|BackTwo|All`.
- Produces: `TargetSelectorRef.None=0, FrontOne=1, BackOne=3, All=5, FrontTwo=6, BackTwo=7`.
- Preserves: existing YAML values `0`, `1`, `3`, `5`; raw values `2`, `4` remain invalid.

- [ ] **Step 1: Write RED schema and authoring tests**

```csharp
[Test]
public void Target_selector_schema_contains_only_approved_ranges()
{
    CollectionAssert.AreEqual(
        new[] { "FrontOne", "FrontTwo", "BackOne", "BackTwo", "All" },
        Enum.GetNames(typeof(TargetSelector)));
}

[Test]
public void Removed_serialized_selector_values_are_not_reused()
{
    Assert.IsFalse(Enum.IsDefined(typeof(TargetSelectorRef), 2));
    Assert.IsFalse(Enum.IsDefined(typeof(TargetSelectorRef), 4));
}

[TestCase(2)]
[TestCase(4)]
public void Undefined_authored_selector_reports_the_card_id(int rawValue)
{
    var spec = new CardSpec
    {
        Id = "legacy_selector",
        Category = CardCategory.Execution,
        Effects = new EffectSpec[]
        {
            new DamageSpec { Value = 1, Selector = (TargetSelectorRef)rawValue }
        }
    };

    var errors = AuthoringValidator.Validate(
        new[] { spec }, AuthoringContext.Default());

    Assert.That(errors, Has.Some.Contains("Card 'legacy_selector'"));
    Assert.That(errors, Has.Some.Contains("unsupported target selector value " + rawValue));
}
```

- [ ] **Step 2: Run focused tests and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~CardSpecMapperTests|FullyQualifiedName~AuthoringValidationTests"
```

Expected: legacy enum names still exist and invalid selector validation is absent.

- [ ] **Step 3: Add C# 9 target types and explicit authoring values**

```csharp
public enum CardTargetFaction { Ally, Enemy }
public enum CardTargetRange { Self, FrontOne, FrontTwo, BackOne, BackTwo, All }

public readonly struct CardTargetKey : IEquatable<CardTargetKey>
{
    public CardTargetFaction Faction { get; }
    public CardTargetRange Range { get; }

    public CardTargetKey(CardTargetFaction faction, CardTargetRange range)
    {
        Faction = faction;
        Range = range;
    }

    public bool Equals(CardTargetKey other)
        => Faction == other.Faction && Range == other.Range;
    public override bool Equals(object obj)
        => obj is CardTargetKey other && Equals(other);
    public override int GetHashCode() => ((int)Faction * 397) ^ (int)Range;
    public override string ToString() => Faction + "/" + Range;
}
```

Use the exact explicit values:

```csharp
public enum TargetSelectorRef
{
    None = 0,
    FrontOne = 1,
    BackOne = 3,
    All = 5,
    FrontTwo = 6,
    BackTwo = 7
}
```

`ToSelector` throws `ArgumentOutOfRangeException` for undefined non-zero values. Add a protected `ValidateSelector` iterator and call it from all four selector-bearing specs so `AuthoringValidator` reports the card ID before mapping/code generation.

- [ ] **Step 4: Rename source-authored selectors and golden strings**

Replace `FrontMost` with `FrontOne` and `BackMost` with `BackOne` in source, generated output, and tests. Remove tests dedicated to `SecondFromFront` and random target determinism; replace mapper coverage with `FrontTwo` and `BackTwo`. Do not change `CardSO` YAML `Selector:` numbers: production assets currently use only `0`, `1`, `3`, `5`.

- [ ] **Step 5: Run focused and full headless tests**

Run Step 2, then:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo
```

Expected: all pass; `rg 'SecondFromFront|TargetSelector\.Random|TargetSelectorRef\.Random' Assets/Core Assets/Unity` has no production match.

- [ ] **Step 6: Commit**

```bash
git add Assets/Core/Cards Assets/Core/Simulation/Authoring \
  Assets/Core/Simulation/Generated/GeneratedCards.cs \
  Assets/Core/Status/ContagionBehavior.cs Assets/Core/Tests/EditMode
git commit -m "refactor(core): close positional target schema"
```

---

### Task 2: Resolve one target snapshot per executing card

**Files:**
- Create: `Assets/Core/Combat/CardTargetSnapshot.cs`
- Modify: `Assets/Core/Combat/EnemyTargeting.cs`
- Modify: `Assets/Core/Combat/PartyTargeting.cs`
- Modify: `Assets/Core/Effects/IEffectHandler.cs`
- Modify: `Assets/Core/Effects/DamageHandler.cs`
- Modify: `Assets/Core/Effects/ApplyStatusHandler.cs`
- Modify: `Assets/Core/Effects/ConsumeStatusHandler.cs`
- Modify: `Assets/Core/Effects/TriggerStatusHandler.cs`
- Modify: `Assets/Core/Effects/MoveFormationHandler.cs`
- Modify: `Assets/Core/Effects/GrantNextTurnFateHandler.cs`
- Modify: `Assets/Core/Effects/GrantNextPlayerDamageCardBonusHandler.cs`
- Modify: `Assets/Core/Effects/NullifyNextPlayerConditionRewardHandler.cs`
- Modify: `Assets/Core/Combat/TurnResolver.cs`
- Test: `Assets/Core/Tests/EditMode/CardTargetSnapshotTests.cs`
- Test: `Assets/Core/Tests/EditMode/PartyTargetingTests.cs`
- Test: `Assets/Core/Tests/EditMode/EnemyTargetingTests.cs`
- Test: `Assets/Core/Tests/EditMode/FormationTargetingIntegrationTests.cs`

**Interfaces:**
- Produces: `IEffectHandler.TargetFor(CardDefinition card, EffectData effect) -> CardTargetKey?`.
- Produces: `CardTargetSnapshot.Capture(CombatState, ExecutionCardInstance, IEnumerable<CardTargetKey>)`.
- Produces: typed `PartyTargets(CardTargetKey)` and `EnemyTargets(CardTargetKey)` lists whose object identities remain fixed for the card.

- [ ] **Step 1: Write RED range and snapshot tests**

```csharp
[Test]
public void Front_two_returns_up_to_two_distinct_living_members_in_order()
{
    var state = PartyState(deadFront: true, livingIds: new[] { "b", "c", "d" });
    CollectionAssert.AreEqual(
        new[] { "b", "c" },
        PartyTargeting.SelectRange(state, TargetSelector.FrontTwo)
            .Select(member => member.Id));
}

[Test]
public void Back_two_returns_up_to_two_distinct_living_enemies_in_formation_order()
{
    var state = EnemyState("a", "b", "c");
    CollectionAssert.AreEqual(
        new[] { "b", "c" },
        EnemyTargeting.SelectRange(state, TargetSelector.BackTwo)
            .Select(enemy => enemy.Id));
}

[Test]
public void Later_effect_does_not_promote_a_new_target_after_snapshot_target_dies()
{
    var state = ThreeEnemyState(hp: 2);
    state.Zone.Add(PlayerCard(
        Damage(2, TargetSelector.FrontTwo),
        ApplyPoison(1, TargetSelector.FrontTwo)));

    new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses())
        .Resolve(state, 0);

    Assert.AreEqual(2, state.Enemies[2].Hp);
    Assert.IsFalse(state.Enemies[2].Statuses.Has(StatusKeys.Poison));
}
```

Also assert one-member `FrontTwo`, `BackTwo`, and `All` return exactly one object with no duplication.

- [ ] **Step 2: Run focused tests and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~CardTargetSnapshotTests|FullyQualifiedName~PartyTargetingTests|FullyQualifiedName~EnemyTargetingTests"
```

Expected: `SelectRange`, `TargetFor`, and `CardTargetSnapshot` do not exist.

- [ ] **Step 3: Implement ordered range selection and snapshot capture**

`SelectRange` returns a fresh ordered list, skips dead units, takes at most the requested count, and never calls RNG. Use this mapping in both formation helpers:

```csharp
private static int TakeCount(TargetSelector selector, int livingCount)
{
    switch (selector)
    {
        case TargetSelector.FrontOne:
        case TargetSelector.BackOne: return Math.Min(1, livingCount);
        case TargetSelector.FrontTwo:
        case TargetSelector.BackTwo: return Math.Min(2, livingCount);
        case TargetSelector.All: return livingCount;
        default: throw new ArgumentOutOfRangeException(nameof(selector));
    }
}
```

`CardTargetSnapshot` stores returned object references keyed by `CardTargetKey`; `Self` resolves the living owner once from `ExecutionCardInstance.OwnerId` and fails capture with `NoValidTarget` when missing or ambiguous.

- [ ] **Step 4: Make runtime handlers declare and consume target meaning**

Use this mapping exactly:

| Handler | Target key |
|---|---|
| `DamageHandler` | player card → `Enemy/<selector or FrontOne>`; enemy card → `Ally/<selector or FrontOne>` |
| `ApplyStatusHandler` `Self` | card side → `Ally/Self` or `Enemy/Self` |
| `ApplyStatusHandler` `TargetEnemy` | `Enemy/<selector or FrontOne>` |
| `ApplyStatusHandler` `PartyBySelector` | `Ally/<selector or FrontOne>` |
| `ApplyStatusHandler` `AllPartyMembers` | `Ally/All` |
| `ApplyStatusHandler` `PartyMember` | `null`; retain explicit-target legacy runtime behavior, but Task 3 rejects authored use in structured catalogs |
| `ConsumeStatusHandler`, `TriggerStatusHandler` | `Enemy/<selector or FrontOne>` |
| `MoveFormationHandler` | card side → `Ally/Self` or `Enemy/Self` |
| grant/nullify/fate handlers | `null` |

Snapshot-backed handlers iterate the captured list, skip a captured object that is dead when reached, and do not select a replacement. Set `EffectContext.TargetId` only when exactly one living target was affected; preserve null for multi-target events.

- [ ] **Step 5: Capture once in `TurnResolver.ResolveCard`**

```csharp
var handlers = card.Def.Effects
    .Select(effect => _effects.Resolve(effect.Key))
    .ToArray();
var targetKeys = card.Def.Effects
    .Select((effect, index) => handlers[index].TargetFor(card.Def, effect))
    .Where(key => key.HasValue)
    .Select(key => key.Value)
    .ToArray();
var targets = CardTargetSnapshot.Capture(state, card, targetKeys);
```

Reject conflicting ranges for the same faction before effects. Pass one snapshot into every `EffectContext`. Keep condition evaluation, death sweep, cancellation, and event order unchanged.

- [ ] **Step 6: Run focused and full headless suites**

Run Step 2 and the full headless command. Existing single-target and `All` behavior must stay green; new range/snapshot tests pass; targeting consumes no RNG.

- [ ] **Step 7: Commit**

```bash
git add Assets/Core/Cards Assets/Core/Combat Assets/Core/Effects \
  Assets/Core/Tests/EditMode
git commit -m "refactor(core): snapshot positional card targets"
```

---

### Task 3: Compose structured layouts and symbol-only plain text

**Files:**
- Create: `Assets/Core/Simulation/Descriptions/CardDescriptionLayout.cs`
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionContracts.cs`
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs`
- Modify: `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionCatalogValidator.cs`
- Test: `Assets/Core/Tests/EditMode/StructuredCardDescriptionTests.cs`
- Test: existing description/registry/catalog test fixtures

**Interfaces:**
- Produces: `DescriptionComposer.Compose(CardDefinition, KoreanDescriptionCatalog) -> CardDescriptionLayout`.
- Preserves: `DescriptionComposer.Describe` as a wrapper returning `DescriptionComposer.Compose(card, catalog).PlainText`.
- Changes: `IEffectDescriptionHandler.Describe -> EffectDescriptionFragment`.
- Produces: consecutive-equal line grouping, target-entry-only deduplication, stable `Ally` then `Enemy` order.

- [ ] **Step 1: Write RED structured-layout tests**

```csharp
[Test]
public void Toxic_reclaim_separates_enemy_and_ally_self_lines()
{
    var definition = GeneratedCards.StarterPool()
        .Select(CardSpecMapper.ToDefinition)
        .Single(card => card.Id == "toxic_reclaim");
    var layout = DescriptionComposer.Compose(
        definition, KoreanDescriptionCatalog.Default);

    CollectionAssert.AreEqual(
        new[]
        {
            new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
            new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne)
        },
        layout.TargetEntries);
    Assert.AreEqual("독 최대 1 소비. 독 1.", layout.Lines[0].Text);
    Assert.AreEqual("소비했다면 방어 4.", layout.Lines[1].Text);
    Assert.AreEqual(
        "[◆] 독 최대 1 소비. 독 1.\n[◇◎] 소비했다면 방어 4.",
        layout.PlainText);
}

[Test]
public void Repeated_nonconsecutive_target_keeps_three_lines()
{
    var layout = DescriptionComposer.Compose(
        Execution("repeat", DamageEnemy(3), BlockSelf(2), DamageEnemy(3)), Korean);
    Assert.AreEqual(3, layout.Lines.Count);
    Assert.AreEqual(2, layout.TargetEntries.Count);
}

[Test]
public void Conflicting_ranges_include_card_id_and_both_ranges()
{
    var ex = Assert.Throws<InvalidOperationException>(() =>
        DescriptionComposer.Compose(
            Execution("conflict",
                DamageEnemy(3, TargetSelector.FrontOne),
                PoisonEnemy(1, TargetSelector.BackOne)), Korean));
    StringAssert.Contains("conflict", ex.Message);
    StringAssert.Contains("FrontOne", ex.Message);
    StringAssert.Contains("BackOne", ex.Message);
}
```

- [ ] **Step 2: Run description tests and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~StructuredCardDescriptionTests|FullyQualifiedName~DescriptionComposerTests|FullyQualifiedName~PartyDescriptionTests"
```

Expected: `Compose` and structured types do not exist.

- [ ] **Step 3: Implement immutable C# 9 DTOs**

Use constructors plus read-only properties; copy lists to arrays. Both target properties use `CardTargetKey?`.

```csharp
public sealed class EffectDescriptionFragment
{
    public CardTargetKey? Target { get; }
    public string Text { get; }
    public EffectDescriptionFragment(CardTargetKey? target, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Description text is required.", nameof(text));
        Target = target;
        Text = text;
    }
}

public sealed class CardDescriptionLine
{
    public CardTargetKey? Target { get; }
    public string Text { get; }
    public CardDescriptionLine(CardTargetKey? target, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Description line text is required.", nameof(text));
        Target = target;
        Text = text;
    }
}

public sealed class CardDescriptionLayout
{
    public IReadOnlyList<CardTargetKey> TargetEntries { get; }
    public IReadOnlyList<CardDescriptionLine> Lines { get; }
    public string PlainText { get; }
    public CardDescriptionLayout(
        IReadOnlyList<CardTargetKey> targetEntries,
        IReadOnlyList<CardDescriptionLine> lines,
        string plainText)
    {
        if (targetEntries == null) throw new ArgumentNullException(nameof(targetEntries));
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        TargetEntries = targetEntries.ToArray();
        Lines = lines.ToArray();
        PlainText = plainText ?? throw new ArgumentNullException(nameof(plainText));
    }
}
```

Reject null/whitespace fragments with card ID and effect key.

- [ ] **Step 4: Change handler and grammar contracts**

Create `DescriptionContext` per card with `CardId`, `CardSide`, condition/lifetime/status vocabulary, `Range(TargetSelector?)`, and `SelfTarget()`. Remove target prose and status-target prefixes from `IDescriptionGrammar`.

`KoreanDescriptionGrammar.Symbol` returns:

```text
Ally/Self      ◇◎
Enemy/Self     ◎◆
Ally/non-Self  ◇
Enemy/non-Self ◆
```

No-target lines have no bracket prefix. Do not emit `적`, `아군`, `자신`, `가장 앞`, or `가장 뒤` as target prose.

- [ ] **Step 5: Implement grouping and target-entry validation**

Render effects in order. Append base and conditional-success sentences to the current line only when the nullable target equals the preceding target; otherwise start a line. `SkipOnBasic` omits only the base sentence. Build `TargetEntries` from unique keys and sort `Ally`, then `Enemy`; never deduplicate sentences.

Intervention cards return one no-target line and no target entries. Zero-effect execution cards return empty lines/plain text. Before construction, group target entries by faction and throw when a faction has multiple ranges. `DescriptionCatalogValidator` invokes `Compose` so boot validation shares the rule.

- [ ] **Step 6: Migrate built-in description handlers**

Return target-free Korean text and the Task 2 target key. `ApplyStatusDescriptionHandler` maps `AllPartyMembers` to `Ally/All`; `PartyMember` throws an `InvalidOperationException` containing card ID because direct unit selection is outside the approved frame schema. Runtime support remains only for legacy selection tests. `MoveFormationDescriptionHandler` returns `Self` plus `대형 전방으로 N칸 이동`, `대형 후방으로 N칸 이동`, or `대형 위치 유지` so owner/self prose is not duplicated.

```csharp
return new EffectDescriptionFragment(
    context.EnemyRange(effect.TargetSelector),
    "피해 " + effectValue);

return new EffectDescriptionFragment(
    context.SelfTarget(),
    context.Statuses.Resolve(payload.Key) + " " + effectValue + suffix);

return new EffectDescriptionFragment(
    null,
    "다음 사용 턴에 운명력 " + effectValue + " 획득");
```

- [ ] **Step 7: Update exact goldens and run full suite**

Update `DescriptionComposerTests`, `PartyDescriptionTests`, and `StarterPoolDescriptionTests` to bracketed symbols/newlines. Add a loop composing every default/generated card twice and asserting byte-identical `PlainText`, entries, and lines. Run Step 2 and the full suite; description registry locality tests must remain green.

- [ ] **Step 8: Commit**

```bash
git add Assets/Core/Simulation/Descriptions Assets/Core/Tests/EditMode
git commit -m "refactor(sim): compose structured card descriptions"
```

---

### Task 4: Carry the layout through `CardPresentation`

**Files:**
- Modify: `Assets/Unity/CardPresentation.cs`
- Test: `Assets/Tests/UnityEditMode/CardPresentationTests.cs`

**Interfaces:**
- Produces: `CardPresentation.DescriptionLayout`.
- Preserves: read-only `Description` returning `DescriptionLayout.PlainText`.
- Preserves: `WithExecutionOrder` changes only order.

- [ ] **Step 1: Write RED tests**

```csharp
[Test]
public void Toxic_reclaim_presentation_keeps_structured_targets_and_lines()
{
    var definition = GeneratedCards.StarterPool()
        .Select(CardSpecMapper.ToDefinition)
        .Single(card => card.Id == "toxic_reclaim");
    var presentation = CardPresentation.FromDefinition(definition);

    Assert.AreEqual(2, presentation.DescriptionLayout.TargetEntries.Count);
    Assert.AreEqual(2, presentation.DescriptionLayout.Lines.Count);
    Assert.AreEqual(presentation.DescriptionLayout.PlainText, presentation.Description);
}

[Test]
public void Intervention_presentation_has_no_unit_target_entries()
{
    var presentation = CardPresentation.FromDefinition(StarterDeck.PullForward());
    Assert.AreEqual(CardCategory.Intervention, presentation.Category);
    Assert.AreEqual(0, presentation.DescriptionLayout.TargetEntries.Count);
}
```

- [ ] **Step 2: Run focused Unity test and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardPresentationTests \
  -testResults /private/tmp/card-presentation-red.xml \
  -logFile /private/tmp/card-presentation-red.log
```

Expected: `DescriptionLayout` is absent.

- [ ] **Step 3: Add the property and preserve it across copies**

The constructor accepts non-null `CardDescriptionLayout descriptionLayout`; `From` and `FromDefinition` call `DescriptionComposer.Compose`; `WithExecutionOrder` passes the same layout instance and all existing metadata unchanged.

- [ ] **Step 4: Run focused test and commit**

Re-run Step 2. Expected: all `CardPresentationTests` pass.

```bash
git add Assets/Unity/CardPresentation.cs Assets/Tests/UnityEditMode/CardPresentationTests.cs
git commit -m "refactor(unity): carry structured card descriptions"
```

---

### Task 5: Add prefab catalog and reusable primitive subviews

**Files:**
- Create: `Assets/Unity/CardPrefabCatalog.cs`
- Create: `Assets/Unity/TargetGlyphView.cs`
- Create: `Assets/Unity/DescriptionLineView.cs`
- Create: matching prefabs under `Assets/Unity/Prefabs/`
- Create: `Assets/Unity/CardPrefabCatalog.asset`
- Test: `Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs`
- Test: `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs`

**Interfaces:**
- Produces: `CardPrefabCatalog.Resolve(CardCategory)` and `Create(CardPresentation, RectTransform)`.
- Produces: `TargetGlyphView.Bind(CardTargetKey?)`.
- Produces: `DescriptionLineView.Bind(CardDescriptionLine)`.

- [ ] **Step 1: Write RED catalog and subview tests**

```csharp
[TestCase(CardCategory.Execution, "ExecutionCardView")]
[TestCase(CardCategory.Intervention, "InterventionCardView")]
public void Catalog_resolves_the_category_prefab(CardCategory category, string expectedName)
{
    var catalog = AssetDatabase.LoadAssetAtPath<CardPrefabCatalog>(CatalogPath);
    Assert.AreEqual(expectedName, catalog.Resolve(category).name);
}

[Test]
public void Target_glyph_prefab_uses_images_and_no_text()
{
    var prefab = AssetDatabase.LoadAssetAtPath<TargetGlyphView>(TargetGlyphPath);
    Assert.IsEmpty(prefab.GetComponentsInChildren<TMP_Text>(true));
    Assert.IsNotEmpty(prefab.GetComponentsInChildren<Image>(true));
}

[Test]
public void Description_line_hides_glyph_for_no_target_line()
{
    var line = InstantiateDescriptionLine();
    line.Bind(new CardDescriptionLine(null, "카드 1장 뽑기."));
    Assert.IsFalse(Field<TargetGlyphView>(line, "_glyph").gameObject.activeSelf);
}
```

- [ ] **Step 2: Run focused EditMode tests and verify RED**

Run filter `CardPrefabCatalogTests,CardFramePrefabTests`, results `/private/tmp/card-primitives-red.xml`, log `/private/tmp/card-primitives-red.log`. Expected: missing types/assets.

- [ ] **Step 3: Implement the catalog**

Define four `[SerializeField] private` references: execution card, intervention card, target glyph, description line. `Resolve` rejects undefined categories; `Validate` reports each missing reference and prefab/category mismatch. `Create` resolves by `CardPresentation.Category`, instantiates under parent, calls `CardView.Configure(this)`, and returns the view.

- [ ] **Step 4: Author reusable primitive prefabs**

Use this fixed `TargetGlyphView.prefab` hierarchy:

```text
TargetGlyphView
├─ AllyDirection
├─ Rail
│  ├─ Segment0
│  ├─ Segment1
│  ├─ Segment2
│  ├─ Segment3
│  └─ Segment4
├─ Diamond0
├─ Diamond1
├─ SelfOuter
├─ SelfInner
├─ EnemyDirection
└─ EmptySlash
```

All nodes are uGUI `Image` primitives with serialized references. `Bind` toggles/mirrors them by faction/range; fill/outline and direction distinguish factions without color alone. A null key activates the circle-plus-slash empty glyph.

`DescriptionLineView.prefab` contains a fixed-width glyph slot and wrapping `TMP_Text`, embeds one `TargetGlyphView`, and hides the glyph slot for null-target lines.

- [ ] **Step 5: Create the catalog asset**

Create one `CardPrefabCatalog.asset`, assign both subview prefabs, and leave the two full-card slots for Task 6. Do not commit until Tasks 5–6 are green together.

---

### Task 6: Author separate execution and intervention full-card prefabs

**Files:**
- Move: `Assets/Unity/Prefabs/CardView.prefab` → `Assets/Unity/Prefabs/ExecutionCardView.prefab`
- Move: matching `.meta` with it
- Create: `Assets/Unity/Prefabs/InterventionCardView.prefab`
- Modify: `Assets/Unity/CardView.cs`
- Modify: `Assets/Unity/CardPrefabCatalog.asset`
- Test: `CardFramePrefabTests.cs`, `HandFanHoverTests.cs`, `CardSelectionControllerTests.cs`

**Interfaces:**
- `CardView.Configure(CardPrefabCatalog)` supplies line/glyph prefabs.
- `CardView.Bind` clears generated children, binds entries/lines, and never recalculates authored coordinates.
- Full-card prefabs expose a serialized `PrefabCategory` marker.

- [ ] **Step 1: Extend RED prefab-contract tests**

```csharp
[Test]
public void Execution_frame_has_symbol_target_panel_and_protruding_badges()
{
    var view = LoadExecution();
    Assert.IsNotNull(Child(view, "SymbolOnlyTargetPanel"));
    Assert.IsNotNull(Child(view, "ExecutionOrderBadge"));
    Assert.IsEmpty(Child(view, "SymbolOnlyTargetPanel")
        .GetComponentsInChildren<TMP_Text>(true));
    AssertBadgeOutsideFrame(view, "CostBadge");
    AssertBadgeOutsideFrame(view, "ExecutionOrderBadge");
}

[Test]
public void Intervention_frame_omits_target_and_order_and_expands_description()
{
    var view = LoadIntervention();
    Assert.IsNull(ChildOrNull(view, "SymbolOnlyTargetPanel"));
    Assert.IsNull(ChildOrNull(view, "ExecutionOrderBadge"));
    Assert.Greater(
        Child(view, "ExpandedDescriptionPanel").rect.height,
        Child(LoadExecution(), "DescriptionPanel").rect.height);
}
```

Also assert badges are under `OverlayLayer`, no badge ancestor has `Mask`/`RectMask2D`, cost diameter is `68`, and order diamond bounding size is `50`.

- [ ] **Step 2: Move the existing prefab and preserve GUID**

Move prefab and `.meta` together. `git diff --summary -- Assets/Unity/Prefabs` must show rename detection.

- [ ] **Step 3: Replace poster-backed layers with primitive hierarchy**

Author spec §11.1/§11.2 hierarchies. Keep art, owner chip, selection outline, back face, status icons. Place `68×68` cost upper-left and `50×50` rotated-square order badge on the right/lower band; offsets live only in prefab `RectTransform`s.

In `CardView.cs`:

- remove `BaseWidth`, `BaseHeight`, `MinCardHeight`, `LateUpdate`, `ApplyResponsiveLayout`, and all `Layout*` helpers;
- replace `_descriptionText` with `_descriptionContent` and generated `DescriptionLineView` children;
- add `_targetContent`, `_targetPanel`, `_executionOrderBadge`, and prefab category marker;
- throw on bound category mismatch;
- when an execution layout has zero `TargetEntries`, create exactly one `TargetGlyphView` bound to `null` so the panel shows the primitive `∅`; intervention prefabs never create a target glyph;
- retain selection, owner/status, art/back-face, and button behavior.

- [ ] **Step 4: Complete catalog references**

Assign all four prefabs to `CardPrefabCatalog.asset` by GUID-backed references. No runtime path lookup.

- [ ] **Step 5: Run focused tests**

Run filters `CardPrefabCatalogTests,CardFramePrefabTests,HandFanHoverTests,CardSelectionControllerTests`, results `/private/tmp/card-frames-green.xml`, log `/private/tmp/card-frames-green.log`. Expected: all pass and all new `.meta` files exist.

- [ ] **Step 6: Commit Tasks 5–6 together**

```bash
git add Assets/Unity/CardPrefabCatalog.cs Assets/Unity/CardPrefabCatalog.cs.meta \
  Assets/Unity/CardPrefabCatalog.asset Assets/Unity/CardPrefabCatalog.asset.meta \
  Assets/Unity/TargetGlyphView.cs Assets/Unity/TargetGlyphView.cs.meta \
  Assets/Unity/DescriptionLineView.cs Assets/Unity/DescriptionLineView.cs.meta \
  Assets/Unity/CardView.cs Assets/Unity/Prefabs \
  Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs \
  Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs.meta \
  Assets/Tests/UnityEditMode/CardFramePrefabTests.cs \
  Assets/Tests/UnityEditMode/CardFramePrefabTests.cs.meta \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs \
  Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs
git commit -m "feat(unity): add primitive card frame prefabs"
```

---

### Task 7: Switch every full-card consumer to the catalog

**Files:**
- Modify: `HandFanView.cs`, `PileView.cs`, `ExecutionRailView.cs`, `DeckPlaytestController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/Scenes/FateWeaverBattle.unity`
- Test: `HandFanHoverTests.cs`, `ExecutionRailInputTests.cs`, `CardPrefabCatalogTests.cs`

**Interfaces:**
- `HandFanView.EditorBuild(CardPrefabCatalog catalog, RectTransform content)`.
- `PileView.Create(RectTransform parent, RectTransform popupLayer, string title, CardPrefabCatalog catalog, Vector2 buttonSize)`.
- `ExecutionRailView.EditorBuild(CardPrefabCatalog catalog, RailCardView railPrefab, RectTransform previewLayer)`.
- Placement flight and rail hover preview preserve `CardPresentation.Category`.

- [ ] **Step 1: Write RED mixed-category consumer tests**

```csharp
[Test]
public void Mixed_hand_uses_distinct_category_prefabs()
{
    var hand = BuildHand(root,
        new[] { ExecutionPresentation(), InterventionPresentation() });
    var views = root.GetComponentsInChildren<CardView>();
    Assert.AreEqual(CardCategory.Execution, views[0].PrefabCategory);
    Assert.AreEqual(CardCategory.Intervention, views[1].PrefabCategory);
}

[Test]
public void Placement_flight_preserves_source_card_category()
{
    var intervention = InterventionPresentation();
    var hand = BuildHand(root, new[] { intervention });
    Assert.IsTrue(hand.TryPreparePlacementFlight(
        0, intervention, overlay, out var flight));
    Assert.AreEqual(CardCategory.Intervention, flight.Card.PrefabCategory);
}
```

In `ExecutionRailInputTests`, hover an intervention presentation and assert the full preview uses `InterventionCardView`, while the rail item remains `RailCardView`.

- [ ] **Step 2: Run focused tests and verify RED**

Run filters `HandFanHoverTests,ExecutionRailInputTests,CardPrefabCatalogTests`. Expected: consumers still accept a single `CardView` prefab.

- [ ] **Step 3: Replace single-prefab fields and instantiation**

Every full-card consumer stores `[SerializeField] private CardPrefabCatalog _cardPrefabs` and calls `_cardPrefabs.Create(presentation, parent)`. `ExecutionRailView` keeps `RailCardView _cardPrefab`; only full preview changes. Placement flight creates from the passed presentation category, disables interaction/raycasting, and preserves source transform.

`DeckPlaytestController` uses the catalog for hand and full-size zone rows without changing session/input behavior.

At session startup, both battle controllers call `_cardPrefabs.ValidateOrThrow()`. They also pass the complete player definitions plus `GoblinDeck.AllCards()` or the selected enemy deck to `DescriptionCatalogValidator.ValidateDefault` before constructing views, so missing prefab references, category mismatches, unsupported direct-target descriptions, and conflicting per-faction ranges fail before a card enters the visible catalog.

- [ ] **Step 4: Rebuild scene references**

In editor-only `BattleSceneBuilder`, require exactly one `t:CardPrefabCatalog` result and pass it to hand, rail, and piles. This is editor validation, not runtime lookup.

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -executeMethod FateWeaver.Unity.Editor.BattleSceneBuilder.Build \
  -logFile /private/tmp/card-catalog-scene-build.log -quit
```

- [ ] **Step 5: Run focused tests, inspect scene diff, and commit**

```bash
git add Assets/Unity/HandFanView.cs Assets/Unity/PileView.cs \
  Assets/Unity/ExecutionRailView.cs Assets/Unity/DeckPlaytestController.cs \
  Assets/Unity/Editor/BattleSceneBuilder.cs Assets/Scenes/FateWeaverBattle.unity \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs \
  Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs \
  Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs
git commit -m "refactor(unity): select full cards through catalog"
```

---

### Task 8: Make the overlapping hand responsive

**Files:**
- Create: `Assets/Core/Simulation/Presentation/ResponsiveHandLayout.cs`
- Modify: `Assets/Core/Simulation/Presentation/HandFanLayout.cs`
- Modify: `Assets/Unity/HandFanView.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/Scenes/FateWeaverBattle.unity`
- Test: `Assets/Core/Tests/EditMode/ResponsiveHandLayoutTests.cs`
- Test: `Assets/Tests/UnityEditMode/CardFrameResponsiveLayoutTests.cs`
- Test: `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`
- Create: `Assets/Tests/UnityEditMode/CardFrameRenderCapture.cs`

**Interfaces:**
- Produces: `ResponsiveHandLayout.Calculate(float availableWidth, float availableHeight, int cardCount, ResponsiveHandSettings settings) -> ResponsiveHandMetrics` containing `Spacing` and `Scale` only.
- `HandFanView` recalculates on `SetCards` and `OnRectTransformDimensionsChange`, never `LateUpdate`.

- [ ] **Step 1: Write RED pure calculation tests**

Use named settings: card width `170`, card height `238`, base spacing `150`, minimum spacing `72`, badge overflow from authored badge bounds, safe margins, minimum scale.

```csharp
[Test]
public void Wide_five_card_hand_keeps_baseline_spacing_and_scale()
{
    var result = ResponsiveHandLayout.Calculate(900f, 260f, 5, Settings());
    Assert.AreEqual(150f, result.Spacing, 0.01f);
    Assert.AreEqual(1f, result.Scale, 0.001f);
}

[Test]
public void Narrow_hand_reduces_spacing_before_scaling()
{
    var result = ResponsiveHandLayout.Calculate(650f, 260f, 5, Settings());
    Assert.That(result.Spacing, Is.InRange(72f, 149.99f));
    Assert.AreEqual(1f, result.Scale, 0.001f);
}

[Test]
public void Too_small_hand_uses_minimum_spacing_then_uniform_scale()
{
    var result = ResponsiveHandLayout.Calculate(420f, 190f, 5, Settings());
    Assert.AreEqual(72f, result.Spacing, 0.01f);
    Assert.That(result.Scale, Is.LessThan(1f));
    Assert.That(result.Scale, Is.GreaterThanOrEqualTo(Settings().MinimumScale));
}
```

- [ ] **Step 2: Run headless test and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter FullyQualifiedName~ResponsiveHandLayoutTests
```

Expected: missing layout types.

- [ ] **Step 3: Implement pure formula**

For 0/1 card, avoid division by zero and use baseline spacing. Otherwise:

```csharp
var widthForCards = Math.Max(0f,
    availableWidth - settings.HorizontalSafeMargins);
var rawSpacing = (widthForCards - settings.CardWidth - settings.BadgeOverflow)
    / (cardCount - 1);
var spacing = Clamp(rawSpacing,
    settings.MinimumSpacing, settings.BaseSpacing);
var widthAtMinimum = settings.CardWidth + settings.BadgeOverflow
    + settings.MinimumSpacing * (cardCount - 1);
var scale = Min(1f,
    widthForCards / widthAtMinimum,
    (availableHeight - settings.VerticalSafeMargins)
        / settings.RequiredFanHeight);
scale = Math.Max(settings.MinimumScale, scale);
```

Return only spacing/scale; continue using deterministic `HandFanLayout.PoseFor`.

- [ ] **Step 4: Apply metrics to one content root**

Store serialized tuning fields and `_content` on `HandFanView`. Instantiate under `_content`, set authored base size once, compute poses with returned spacing, and set `_content.localScale` uniformly. Do not resize badge/text children. Hover raises the card to highest sibling and restores its original sibling.

- [ ] **Step 5: Add Unity geometry tests**

For logical root sizes `960×720`, `1280×800`, `1280×720`, `1680×720`, instantiate 1–5 mixed-category cards and assert:

- full bounds including badge overflow stay inside safe margins;
- X/Y scale is equal and within `[minimumScale, 1]`;
- spacing stays within `[minimumSpacing, baseSpacing]` before scale;
- adjacent cards do not fully cover cost/order badges;
- hovered card is last sibling;
- dimension changes trigger one recomputation without a frame loop.

Create `CardFrameRenderCapture` as an explicit EditMode fixture with seven test cases. Each case constructs a camera-backed test canvas, binds the catalog, sets the logical size, renders to `RenderTexture`, and writes `ImageConversion.EncodeToPNG(texture)` to `/private/tmp/primitive-card-frame-captures/<case>.png`. Cases are `execution-1280x720`, `intervention-1280x720`, `toxic-reclaim-1280x720`, `mixed-five-960x720`, `mixed-five-1280x800`, `mixed-five-1280x720`, and `mixed-five-1680x720`. The fixture creates no repository files.

- [ ] **Step 6: Run headless and Unity tests**

Run Step 2, then Unity filters `CardFrameResponsiveLayoutTests,HandFanHoverTests`, results `/private/tmp/card-responsive-green.xml`, log `/private/tmp/card-responsive-green.log`.

- [ ] **Step 7: Rebuild scene and commit**

Run Task 7 scene builder command, review only tuning/content-root changes, then:

```bash
git add Assets/Core/Simulation/Presentation/ResponsiveHandLayout.cs \
  Assets/Core/Simulation/Presentation/ResponsiveHandLayout.cs.meta \
  Assets/Core/Tests/EditMode/ResponsiveHandLayoutTests.cs \
  Assets/Core/Tests/EditMode/ResponsiveHandLayoutTests.cs.meta \
  Assets/Unity/HandFanView.cs Assets/Unity/Editor/BattleSceneBuilder.cs \
  Assets/Scenes/FateWeaverBattle.unity \
  Assets/Tests/UnityEditMode/CardFrameResponsiveLayoutTests.cs \
  Assets/Tests/UnityEditMode/CardFrameResponsiveLayoutTests.cs.meta \
  Assets/Tests/UnityEditMode/CardFrameRenderCapture.cs \
  Assets/Tests/UnityEditMode/CardFrameRenderCapture.cs.meta \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs
git commit -m "feat(ui): scale overlapping hand responsively"
```

---

### Task 9: Validate authored assets and retire poster frames

**Files:**
- Modify: `Assets/Unity/Editor/CardCodeGenerator.cs`
- Modify: `Assets/Tests/UnityEditMode/CardCodeGeneratorTests.cs`
- Modify: `Assets/Unity/PLAYTEST.md`
- Delete: seven `Assets/Unity/Resources/Cards/Frame/fw_*_poster_v2.png` and matching `.meta`

**Interfaces:**
- Produces: editor validation errors containing card ID, asset path, raw invalid selector value.
- Preserves: generated card equivalence after selector rename.

- [ ] **Step 1: Write RED serialized-asset validation tests**

Create an in-memory `CardAsset` with `DamageSpec.Selector = (TargetSelectorRef)2`, validate it against path `Assets/Validation/legacy_card.asset`, and assert one error contains `legacy_card`, that path, and `2`. Also scan every real `CardAsset` from `AssetDatabase.FindAssets("t:CardAsset")` and assert none reports values `2` or `4`.

- [ ] **Step 2: Run `CardCodeGeneratorTests` and verify RED**

Expected: missing path-aware validation helper.

- [ ] **Step 3: Add path-aware preflight**

Before conversion or source emission, validate every loaded asset. Reuse `AuthoringValidator` and prefix each error with `Card '<id>' at '<path>':`. Abort generation; never coerce undefined values.

- [ ] **Step 4: Audit poster GUID references**

```bash
for meta in Assets/Unity/Resources/Cards/Frame/fw_*_poster_v2.png.meta; do
  guid=$(sed -n 's/^guid: //p' "$meta")
  rg -n "$guid" Assets --glob '!*.meta' || true
done
```

Expected after Task 6: no matches. If a GUID remains, replace that serialized sprite with primitives and repeat. Only then delete each PNG and matching `.meta`.

- [ ] **Step 5: Document manual visual checks**

Update `PLAYTEST.md` with: mixed execution/intervention hand; `독성 환원` two target groups; no-target execution `∅`; intervention without target/order; 1–5 cards at 4:3/16:10/16:9/21:9; hovered-card top ordering; flight category preservation; unchanged `RailCardView`.

- [ ] **Step 6: Run tests and commit cleanup**

```bash
git add Assets/Unity/Editor/CardCodeGenerator.cs \
  Assets/Tests/UnityEditMode/CardCodeGeneratorTests.cs \
  Assets/Unity/PLAYTEST.md Assets/Unity/Resources/Cards/Frame
git commit -m "chore(ui): retire poster card frame assets"
```

---

### Task 10: Run full verification and archive the completed plan

**Files:**
- Move after successful implementation: this plan to `docs/superpowers/archive/plans/2026-07-31-primitive-card-frame.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`

- [ ] **Step 1: Run full headless suite**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo
```

Expected: all pass.

- [ ] **Step 2: Run full Unity EditMode suite**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/primitive-card-frame-editmode.xml \
  -logFile /private/tmp/primitive-card-frame-editmode.log
```

Expected: XML `result="Passed"`; no compile errors, missing scripts/references, or import failures.

- [ ] **Step 3: Run structural and dirty-tree audits**

```bash
rg -n 'SecondFromFront|TargetSelector\.Random|TargetSelectorRef\.Random' Assets/Core Assets/Unity
rg -n 'Resources\.Load|GameObject\.Find|FindObjectOfType' \
  Assets/Unity/CardView.cs Assets/Unity/CardPrefabCatalog.cs \
  Assets/Unity/HandFanView.cs Assets/Unity/PileView.cs Assets/Unity/ExecutionRailView.cs
git diff --check
git status --short
```

Expected: searches empty, diff check clean, only intended files listed.

- [ ] **Step 4: Record automated render captures**

Run `FateWeaver.Tests.UnityEditMode.CardFrameRenderCapture` to render execution, intervention, toxic-reclaim, and mixed five-card hands at four logical sizes into `/private/tmp/primitive-card-frame-captures/`. Capture output remains untracked.

- [ ] **Step 5: Archive plan and update indexes**

After all implementation and verification pass, move this plan to `archive/plans/`, remove its active row from `docs/superpowers/README.md`, add the archived entry to `docs/superpowers/archive/README.md`, and keep the design spec current.

- [ ] **Step 6: Commit completion record**

```bash
git add docs/superpowers/README.md docs/superpowers/archive/README.md \
  docs/superpowers/archive/plans/2026-07-31-primitive-card-frame.md
git commit -m "docs: archive primitive card frame implementation"
```

- [ ] **Step 7: Confirm final branch state**

```bash
git status --short
git log --oneline --decorate -10
```

Expected: clean worktree on `refactor/card-frame-design`. Do not merge to `master` without explicit user approval.
