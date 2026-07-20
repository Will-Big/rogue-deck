# CardType Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `CardType` from every runtime/authoring schema and derive damage-card behavior from the presence of `EffectKeys.Damage`, including composite cards.

**Architecture:** `CardDefinition.HasEffect(EffectKey)` is the only effect-presence query. Core conditions accept an `EffectKey`, while the closed Unity authoring enum exposes explicit Damage-card condition names; execution/intervention routing remains solely on `CardCategory`.

**Tech Stack:** Unity 6000.5.2f1, C# 9, NUnit 3, .NET 6 headless test harness, Unity EditMode batch runner.

## Global Constraints

- `FateWeaver.Core` must not reference `UnityEngine`.
- All product randomness continues to flow through `CombatState`; this refactor must not change RNG consumption or timeline order.
- Do not add external packages, reflection auto-registration, raw-string effect comparisons, card tags, or a capability registry.
- Definition-based Damage-card conditions inspect `EffectData.Key`; they do not inspect `CardResolved.DamageDealt`.
- Conditions remain a closed combinator model; only effect handlers/statuses/intervention actions use open registries.
- Preserve existing content except for deleting `CardType` and renaming attack-derived condition/bonus identifiers to Damage-card terminology.
- Run Unity only in `-batchmode` with `-projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck`; write logs/results under `/private/tmp`.
- Do not run Unity GUI or manual Play validation in this worktree. The user performs Play validation after an approved merge to the main checkout.
- Use `apply_patch` for source and asset edits. Preserve unrelated user changes and stage only paths from the current task.

---

## File map

### Core behavior

- `Assets/Core/Cards/CardDefinition.cs`: immutable card data and the new `HasEffect(EffectKey)` query.
- `Assets/Core/Conditions/Condition.cs`: side-only and effect-aware condition records.
- `Assets/Core/Conditions/ConditionEvaluator.cs`: evaluation of definition-based effect conditions.
- `Assets/Core/Effects/EffectKey.cs`: rename the next-Damage-card bonus key.
- `Assets/Core/Effects/GrantNextPlayerDamageCardBonusHandler.cs`: select the next player card with a Damage effect.

### Authoring and descriptions

- `Assets/Core/Simulation/Authoring/EffectSpec.cs`: rename Damage-card `ConditionKind` members and mappings.
- `Assets/Core/Simulation/Authoring/Specs/GrantNextDamageCardBonusSpec.cs`: renamed authoring spec and key.
- `Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs`: describe definition-based cards as `피해 카드`.
- `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs`: next Damage-card bonus copy.
- `Assets/Core/Simulation/CombatRegistries.cs`: runtime handler registration.
- `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`: description handler registration.

### Schema and content

- `Assets/Core/Cards/CardType.cs` and `.meta`: remove the enum asset.
- `Assets/Core/Simulation/Authoring/CardSpec.cs`: remove `Type`.
- `Assets/Core/Simulation/ScenarioDefinition.cs`: remove `ZoneCardSpec.Type`.
- `Assets/Unity/CardAsset.cs`: remove serialized `Type`.
- `Assets/Unity/Editor/CardCodeGenerator.cs`: stop copying/emitting `Type`.
- `Assets/Core/Simulation/Generated/GeneratedCards.cs`: regenerate without `Type` and with renamed conditions.
- `Assets/Unity/CardSO/**/*.asset`: remove obsolete serialized `Type` lines.

### Regression tests

- `Assets/Core/Tests/EditMode/CardDefinitionDataTests.cs`: effect-presence contract and schema absence.
- `Assets/Core/Tests/EditMode/ConditionEvaluatorTests.cs`: Damage, Damage+Block, Block-only condition behavior.
- `Assets/Core/Tests/EditMode/ConditionalEffectResolutionTests.cs`: next Damage-card bonus behavior.
- `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs`: golden signatures with only the type column removed and condition names updated.
- `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`, `GeneratedCardsTests.cs`, `DescriptionComposerTests.cs`: authoring/description rename coverage.
- Existing core and Unity tests containing constructor `CardType` arguments: remove only that argument and adapt helpers to express intent through effects.

---

### Task 1: Add the type-safe card effect query

**Files:**
- Modify: `Assets/Core/Tests/EditMode/CardDefinitionDataTests.cs`
- Modify: `Assets/Core/Cards/CardDefinition.cs`

**Interfaces:**
- Produces: `bool CardDefinition.HasEffect(EffectKey key)`.
- Contract: exact top-level key membership; conditional effects count; an empty key throws `ArgumentException`.

- [ ] **Step 1: Write failing query tests**

Add these tests while keeping the existing constructor shape for this task:

```csharp
[TestCase(true, true)]
[TestCase(true, false)]
[TestCase(false, true)]
public void HasEffect_derives_damage_capability_from_effect_composition(
    bool hasDamage,
    bool hasBlock)
{
    var effects = new List<EffectData>();
    if (hasDamage) effects.Add(new EffectData(EffectKeys.Damage, 3));
    if (hasBlock)
        effects.Add(EffectData.ApplyStatus(
            StatusKeys.Block,
            StatusLifetime.ThisTurn,
            StatusApplyTarget.Self,
            2));

    var card = new CardDefinition(
        "test", "test", Side.Player, CardType.Skill, 5, effects);

    Assert.AreEqual(hasDamage, card.HasEffect(EffectKeys.Damage));
}

[Test]
public void HasEffect_rejects_an_empty_key()
{
    var card = new CardDefinition(
        "test", "test", Side.Player, CardType.Skill, 5,
        Array.Empty<EffectData>());

    Assert.Throws<ArgumentException>(() => card.HasEffect(default));
}
```

Add `System.Collections.Generic`, `FateWeaver.Core.Status`, and the existing `FateWeaver.Core.Effects` import as required.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~CardDefinitionDataTests
```

Expected: compilation fails because `CardDefinition` has no `HasEffect` method.

- [ ] **Step 3: Implement the minimal query**

Add to the `CardDefinition` record body:

```csharp
public bool HasEffect(EffectKey key)
{
    if (string.IsNullOrEmpty(key.Id))
        throw new System.ArgumentException("Effect key must not be empty.", nameof(key));

    foreach (var effect in Effects)
    {
        if (effect.Key == key)
            return true;
    }

    return false;
}
```

- [ ] **Step 4: Verify GREEN**

Run the focused command from Step 2. Expected: all `CardDefinitionDataTests` pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Core/Cards/CardDefinition.cs Assets/Core/Tests/EditMode/CardDefinitionDataTests.cs
git commit -m "refactor(core): derive card effects through typed query"
```

---

### Task 2: Replace attack-type conditions with effect-aware conditions

**Files:**
- Modify: `Assets/Core/Conditions/Condition.cs`
- Modify: `Assets/Core/Conditions/ConditionEvaluator.cs`
- Modify: `Assets/Core/Tests/EditMode/ConditionEvaluatorTests.cs`
- Modify: `Assets/Core/Tests/EditMode/PreviousExecutedCardConditionTests.cs`
- Modify: `Assets/Core/Tests/EditMode/CounterStanceTests.cs`
- Modify: `Assets/Core/Tests/EditMode/PartyDescriptionTests.cs`
- Modify: `Assets/Core/Tests/EditMode/DescriptionComposerTests.cs`
- Modify: `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`
- Modify: `Assets/Core/Tests/EditMode/GeneratedCardsTests.cs`
- Modify: `Assets/Core/Simulation/Authoring/EffectSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs`
- Modify: `Assets/Core/Simulation/StarterDeck.cs`
- Modify: `Assets/Core/Simulation/SampleScenarios.cs`
- Modify: `Assets/Core/Simulation/SampleMultiTurnScenarios.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs`

**Interfaces:**
- Consumes: `CardDefinition.HasEffect(EffectKey)`.
- Produces: `AdjacentCardHasEffect`, `PreviousExecutedCardHasEffect`, `BeforeNextEnemyDamageCard`.
- Preserves: side-only `AdjacentCardIs(Direction, Side)` and `PreviousExecutedCardIs(Side)`.

- [ ] **Step 1: Write failing condition behavior tests**

Change the `ConditionEvaluatorTests.Card` helper to accept effects while temporarily supplying any existing `CardType` value:

```csharp
private static ExecutionCardInstance Card(
    string id,
    Side side,
    int executionOrder,
    params EffectData[] effects)
{
    var def = new CardDefinition(
        id, id, side, CardType.Skill, executionOrder, effects);
    return new ExecutionCardInstance(def);
}

private static EffectData Block()
    => EffectData.ApplyStatus(
        StatusKeys.Block,
        StatusLifetime.ThisTurn,
        StatusApplyTarget.Self,
        2);
```

Add three focused tests:

```csharp
[Test]
public void AdjacentCardHasEffect_matches_damage_in_a_composite_card_only()
{
    var state = new CombatState();
    var subject = Card("subject", Side.Player, 1, Block());
    var hybrid = Card("hybrid", Side.Enemy, 2,
        new EffectData(EffectKeys.Damage, 3), Block());
    state.Zone.Add(subject);
    state.Zone.Add(hybrid);
    var ctx = ResolutionContext.From(state);

    Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(
        new AdjacentCardHasEffect(
            AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage),
        subject,
        ctx));
}

[Test]
public void PreviousExecutedCardHasEffect_rejects_a_block_only_card()
{
    var state = new CombatState();
    var blockOnly = Card("block", Side.Player, 1, Block());
    var subject = Card("subject", Side.Player, 2,
        new EffectData(EffectKeys.Damage, 1));
    state.Zone.Add(blockOnly);
    state.Zone.Add(subject);
    var ctx = ResolutionContext.From(state);
    ctx.MarkExecuted(blockOnly);

    Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(
        new PreviousExecutedCardHasEffect(Side.Player, EffectKeys.Damage),
        subject,
        ctx));
}

[Test]
public void BeforeNextEnemyDamageCard_ignores_an_earlier_block_only_enemy_card()
{
    var state = new CombatState();
    var blockOnly = Card("block", Side.Enemy, 1, Block());
    var subject = Card("subject", Side.Player, 2,
        new EffectData(EffectKeys.Damage, 1));
    state.Zone.Add(blockOnly);
    state.Zone.Add(subject);
    var ctx = ResolutionContext.From(state);

    Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(
        new BeforeNextEnemyDamageCard(), subject, ctx));
}
```

- [ ] **Step 2: Run the focused condition tests and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~ConditionEvaluatorTests
```

Expected: compilation fails because the three new condition record types do not exist.

- [ ] **Step 3: Define the new condition records**

Replace the nullable-type records in `Condition.cs` with:

```csharp
public sealed record BeforeNextEnemyDamageCard : Condition;

public sealed record AdjacentCardIs(
    AdjacentDirection Direction,
    Side Side) : Condition;

public sealed record AdjacentCardHasEffect(
    AdjacentDirection Direction,
    Side Side,
    EffectKey EffectKey) : Condition;

public sealed record PreviousExecutedCardIs(Side Side) : Condition;

public sealed record PreviousExecutedCardHasEffect(
    Side Side,
    EffectKey EffectKey) : Condition;
```

Import `FateWeaver.Core.Effects` and remove the obsolete `BeforeNextEnemyAttack` record.

- [ ] **Step 4: Implement evaluator branches**

Keep side-only branches free of effect checks. Add effect-aware branches that call `HasEffect`:

```csharp
if (condition is AdjacentCardHasEffect adjacentEffect)
{
    var offset = adjacentEffect.Direction == AdjacentDirection.Previous ? -1 : 1;
    var neighbor = ctx.CardAt(index + offset);
    return neighbor != null
        && neighbor.Def.Side == adjacentEffect.Side
        && neighbor.Def.HasEffect(adjacentEffect.EffectKey)
            ? ConditionTier.Success
            : ConditionTier.Basic;
}

if (condition is BeforeNextEnemyDamageCard)
{
    for (var i = 0; i < index; i++)
    {
        var earlier = ctx.Order[i];
        if (earlier.Def.Side == Side.Enemy
            && earlier.Def.HasEffect(EffectKeys.Damage))
            return ConditionTier.Basic;
    }

    return ConditionTier.Success;
}

if (condition is PreviousExecutedCardHasEffect previousEffect)
{
    var last = ctx.LastExecutedCard;
    return last != null
        && last.Def.Side == previousEffect.Side
        && last.Def.HasEffect(previousEffect.EffectKey)
            ? ConditionTier.Success
            : ConditionTier.Basic;
}
```

- [ ] **Step 5: Migrate authoring names and core call sites**

Rename enum members without changing their declaration order:

```csharp
public enum ConditionKind
{
    None,
    FirstToTrigger,
    WithinNth,
    BeforeNextEnemyDamageCard,
    PrevExecutedIsPlayerDamageCard,
    NextIsEnemyDamageCard,
    PrevExecutedIsEnemyDamageCard,
    NoPrecedingPlayerCard,
    NoFollowingEnemyCard
}
```

Map those members to `BeforeNextEnemyDamageCard`, `PreviousExecutedCardHasEffect(..., EffectKeys.Damage)`, and
`AdjacentCardHasEffect(..., EffectKeys.Damage)`. Convert side-only uses to the two-argument/one-argument records.

Update Korean grammar with these exact stems:

```csharp
case BeforeNextEnemyDamageCard _:
    return "다음 적 피해 카드 전";
case AdjacentCardHasEffect a:
    return AdjacentEffectStem(a);
case PreviousExecutedCardHasEffect p:
    return PreviousExecutedEffectStem(p);

private static string EffectCardName(EffectKey key)
    => key == EffectKeys.Damage ? "피해 카드" : key + " 효과 카드";
```

The adjacent/previous helpers combine `SideName(...)` with `EffectCardName(...)`; the side-only helpers continue to say `플레이어 카드` or `적 카드`.

- [ ] **Step 6: Update condition tests and golden condition strings**

Replace attack-specific assertions with Damage-effect assertions. In golden signatures, use record strings such as:

```text
PreviousExecutedCardHasEffect { Side = Enemy, EffectKey = damage }
AdjacentCardHasEffect { Direction = Next, Side = Enemy, EffectKey = damage }
```

Do not remove the `CardType` column from golden signatures yet; Task 4 owns that baseline transition.

- [ ] **Step 7: Verify GREEN and the full headless suite**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~ConditionEvaluatorTests
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
```

Expected: focused tests pass; full suite has zero failures.

- [ ] **Step 8: Commit**

```bash
git add Assets/Core/Conditions Assets/Core/Simulation Assets/Core/Tests/EditMode
git commit -m "refactor(core): derive damage card conditions from effects"
```

---

### Task 3: Rename and retarget the next Damage-card bonus

**Files:**
- Delete: `Assets/Core/Effects/GrantNextPlayerAttackDamageBonusHandler.cs`
- Delete: `Assets/Core/Effects/GrantNextPlayerAttackDamageBonusHandler.cs.meta`
- Create: `Assets/Core/Effects/GrantNextPlayerDamageCardBonusHandler.cs`
- Create: `Assets/Core/Effects/GrantNextPlayerDamageCardBonusHandler.cs.meta` (preserve the deleted file's GUID/content)
- Delete: `Assets/Core/Simulation/Authoring/Specs/GrantNextAttackBonusSpec.cs`
- Delete: `Assets/Core/Simulation/Authoring/Specs/GrantNextAttackBonusSpec.cs.meta`
- Create: `Assets/Core/Simulation/Authoring/Specs/GrantNextDamageCardBonusSpec.cs`
- Create: `Assets/Core/Simulation/Authoring/Specs/GrantNextDamageCardBonusSpec.cs.meta` (preserve the deleted file's GUID/content)
- Modify: `Assets/Core/Effects/EffectKey.cs`
- Modify: `Assets/Core/Simulation/CombatRegistries.cs`
- Modify: `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Modify: `Assets/Core/Simulation/Authoring/EffectSpecCatalog.cs`
- Modify: `Assets/Core/Simulation/SampleScenarios.cs`
- Modify: `Assets/Core/Simulation/SampleMultiTurnScenarios.cs`
- Modify: `Assets/Core/Tests/EditMode/ConditionalEffectResolutionTests.cs`
- Modify: `Assets/Core/Tests/EditMode/DescriptionComposerTests.cs`

**Interfaces:**
- Produces: `EffectKeys.GrantNextPlayerDamageCardBonus` with id `grant_next_player_damage_card_bonus`.
- Produces: `GrantNextPlayerDamageCardBonusHandler` and `GrantNextDamageCardBonusSpec`.

- [ ] **Step 1: Write a failing composite-card selection test**

In `ConditionalEffectResolutionTests`, create a turn containing:

1. a player card with `GrantNextPlayerDamageCardBonus` value 6;
2. a player Block-only card;
3. a player card with `Damage(1)` plus `ApplyStatus(Block, TargetEnemy)`.

Register `DamageHandler`, `ApplyStatusHandler`, and `GrantNextPlayerDamageCardBonusHandler`; add one enemy so both Damage and TargetEnemy status application resolve. Assert the Block-only card deals 0 and the composite card's `CardResolved.DamageDealt` is 7.

Use this core effect setup:

```csharp
var block = EffectData.ApplyStatus(
    StatusKeys.Block,
    StatusLifetime.ThisTurn,
    StatusApplyTarget.TargetEnemy,
    2);
var hybridEffects = new[]
{
    new EffectData(EffectKeys.Damage, 1),
    block
};
```

- [ ] **Step 2: Verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~ConditionalEffectResolutionTests
```

Expected: compilation fails because the renamed key and handler do not exist.

- [ ] **Step 3: Implement the renamed key and handler**

Add the key:

```csharp
public static readonly EffectKey GrantNextPlayerDamageCardBonus =
    new EffectKey("grant_next_player_damage_card_bonus");
```

The handler's selection condition must be exactly:

```csharp
if (card.Def.Side == Side.Player
    && card.Def.HasEffect(EffectKeys.Damage))
```

Remove the old key, class, spec, and registrations rather than leaving compatibility aliases.

- [ ] **Step 4: Rename authoring and description components**

`GrantNextDamageCardBonusSpec` returns the new key and emits:

```csharp
"new GrantNextDamageCardBonusSpec { Value = " + Value + ", "
    + ConditionLiteral() + " }"
```

Rename the description handler to `GrantNextPlayerDamageCardBonusDescriptionHandler` and return:

```csharp
context.TargetPrefix(effect) + "다음 플레이어 피해 카드가 주는 피해 +" + effectValue
```

- [ ] **Step 5: Verify GREEN and absence of the old identifier**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~ConditionalEffectResolutionTests
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
rg -n 'GrantNext(Player)?Attack|grant_next_player_attack_damage_bonus' Assets --glob '*.cs' --glob '*.asset'
```

Expected: tests pass; `rg` returns no matches.

- [ ] **Step 6: Commit**

```bash
git add Assets/Core/Effects Assets/Core/Simulation Assets/Core/Tests/EditMode
git commit -m "refactor(core): target next damage card by effect"
```

---

### Task 4: Remove CardType from pure C# schemas and fixtures

**Files:**
- Delete: `Assets/Core/Cards/CardType.cs`
- Delete: `Assets/Core/Cards/CardType.cs.meta`
- Modify: `Assets/Core/Cards/CardDefinition.cs`
- Modify: `Assets/Core/Simulation/Authoring/CardSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/CardSpecMapper.cs`
- Modify: `Assets/Core/Simulation/ScenarioDefinition.cs`
- Modify: all `Assets/Core/Simulation/**/*.cs` files returned by `rg -l '\bCardType\b' Assets/Core/Simulation --glob '*.cs'`
- Modify: all `Assets/Core/Tests/EditMode/**/*.cs` files returned by `rg -l '\bCardType\b' Assets/Core/Tests/EditMode --glob '*.cs'`
- Modify: `Assets/Tests/UnityEditMode/BattleScreenUnitIdentityTests.cs`
- Modify: `Assets/Tests/UnityEditMode/CardPresentationTests.cs`

**Interfaces:**
- Changes `CardDefinition` positional constructor to `(string Id, string Name, Side Side, int BaseExecutionOrder, IReadOnlyList<EffectData> Effects)`.
- Changes `ZoneCardSpec` constructor to `(string id, string name, Side side, int executionOrder, IReadOnlyList<EffectData> effects)`.
- Removes `CardSpec.Type`.

- [ ] **Step 1: Add a failing architecture test**

Add to `CardDefinitionDataTests` before deleting production types:

```csharp
[Test]
public void CardType_is_absent_from_core_and_authoring_schemas()
{
    var assembly = typeof(CardDefinition).Assembly;
    Assert.IsNull(assembly.GetType("FateWeaver.Core.Cards.CardType"));
    Assert.IsNull(typeof(CardDefinition).GetProperty("Type"));
    Assert.IsNull(typeof(CardSpec).GetField("Type"));
    Assert.IsNull(typeof(ZoneCardSpec).GetProperty("Type"));
}
```

Import `FateWeaver.Simulation` and `FateWeaver.Simulation.Authoring`.

- [ ] **Step 2: Verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~CardType_is_absent
```

Expected: the assertion finding `FateWeaver.Core.Cards.CardType` fails.

- [ ] **Step 3: Remove the schema fields and mapper plumbing**

Use these exact shapes:

```csharp
public sealed record CardDefinition(
    string Id,
    string Name,
    Side Side,
    int BaseExecutionOrder,
    IReadOnlyList<EffectData> Effects)
```

```csharp
public sealed class CardSpec
{
    public string Id;
    public string Name;
    public Side Side;
    public CardCategory Category;
    public int EnergyCost;
    public int BaseExecutionOrder;
    public EffectSpec[] Effects;
    public InterventionKeyRef Intervention;
    public int InterventionEffectValue;
}
```

For both branches of `CardSpecMapper`, construct `CardDefinition` without `spec.Type`.

- [ ] **Step 4: Mechanically migrate product constructors and simulation copies**

For every `CardDefinition` and `ZoneCardSpec` call, remove only the positional `CardType.*` argument. For every `CardSpec` initializer, remove only `Type = CardType.*`. Remove `card.Type` forwarding in `ScenarioRunner` and `PlaytestSession`.

Examples:

```csharp
new CardDefinition(id, name, side, executionOrder, effects)
new ZoneCardSpec(id, name, side, executionOrder, effects)
new CardSpec { Id = id, Name = name, Side = side, Category = category }
```

Delete helper parameters such as `CardType type` when their only use was forwarding to a constructor. Where a test used Attack/Skill/Defense to distinguish behavior, express that distinction with `Damage`, `ApplyStatus(Block)`, or no Damage effect.

- [ ] **Step 5: Update golden signatures by deleting only the type column**

Change:

```csharp
d.Id, d.Name, d.Side, d.Type, d.Category, d.EnergyCost
```

to:

```csharp
d.Id, d.Name, d.Side, d.Category, d.EnergyCost
```

Each golden line changes from `...;Player;Attack;Execution;...` (or Skill/Defense) to `...;Player;Execution;...`. Keep all other fields and the Task 2 condition record names intact.

- [ ] **Step 6: Delete CardType and verify compilation/GREEN**

Delete `CardType.cs` and its `.meta`, then run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~CardType_is_absent
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
rg -n '\bCardType\b|\.Type\b' Assets/Core Assets/Tests --glob '*.cs'
```

Expected: architecture test passes; full suite has zero failures; `rg` returns no matches.

- [ ] **Step 7: Commit**

```bash
git add Assets/Core Assets/Tests/UnityEditMode
git commit -m "refactor(core): remove CardType schema"
```

---

### Task 5: Remove Unity CardAsset Type and regenerate authored output

**Files:**
- Modify: `Assets/Unity/CardAsset.cs`
- Modify: `Assets/Unity/Editor/CardCodeGenerator.cs`
- Modify: `Assets/Core/Simulation/Generated/GeneratedCards.cs`
- Modify: the 18 CardAsset files under `Assets/Unity/CardSO/Enemies`, `Player`, and `Validation` containing an exact `  Type:` line.
- Modify: `Assets/Tests/UnityEditMode/CardPresentationTests.cs`
- Modify: `Assets/Tests/UnityEditMode/BattleScreenUnitIdentityTests.cs`

**Interfaces:**
- `CardAsset.ToSpec()` no longer emits `Type`.
- `CardCodeGenerator.EmitSpec` no longer emits `Type = CardType.*`.

- [ ] **Step 1: Add a failing Unity schema assertion**

Add to `CardPresentationTests`:

```csharp
[Test]
public void CardAsset_has_no_serialized_card_type_field()
{
    Assert.IsNull(typeof(CardAsset).GetField("Type"));
}
```

Run the focused Unity test without `-quit`:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardPresentationTests \
  -testResults /private/tmp/p0b2-cardasset-red.xml \
  -logFile /private/tmp/p0b2-cardasset-red.log
```

Expected: the new assertion fails because `CardAsset.Type` exists.

- [ ] **Step 2: Remove Unity schema and generator plumbing**

Delete `public CardType Type;`, `Type = Type` from `ToSpec`, both `card.Type = ...` copies, and this emitter line:

```csharp
sb.Append("Type = CardType.").Append(s.Type).Append(", ");
```

Update Unity tests' `CardDefinition` calls to the Task 4 signature.

- [ ] **Step 3: Remove stale YAML fields**

Remove exactly the `  Type: <integer>` line from each result of:

```bash
rg -l '^  Type:' Assets/Unity/CardSO --glob '*.asset'
```

Do not alter `m_Type`, `m_RendererType`, or any field outside CardSO assets.

- [ ] **Step 4: Regenerate GeneratedCards.cs twice**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck \
  -executeMethod FateWeaver.Unity.Editor.CardCodeGenerator.Generate \
  -logFile /private/tmp/p0b2-generate-first.log
```

Inspect the log for `Generated Assets/Core/Simulation/Generated/GeneratedCards.cs`, then record the diff. Run the same command with `/private/tmp/p0b2-generate-second.log`; compare `git diff -- Assets/Core/Simulation/Generated/GeneratedCards.cs` before and after the second run. Expected: the second run adds no diff.

- [ ] **Step 5: Verify focused Unity GREEN and schema search**

Repeat the Step 1 Unity command with result/log names `p0b2-cardasset-green.*`.

Run:

```bash
rg -n '\bCardType\b|^  Type:' Assets --glob '*.cs' --glob '*.asset'
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
git status --short
```

Expected: no CardType/CardSO Type matches; headless suite has zero failures; status contains only expected P0-B2 paths and Unity-generated `Logs`/temporary artifacts are absent.

- [ ] **Step 6: Commit**

```bash
git add Assets/Unity/CardAsset.cs Assets/Unity/Editor/CardCodeGenerator.cs Assets/Unity/CardSO Assets/Core/Simulation/Generated/GeneratedCards.cs Assets/Tests/UnityEditMode
git commit -m "refactor(unity): remove CardType authoring field"
```

---

### Task 6: Full verification and implementation record

**Files:**
- Modify: `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md`
- Create: `docs/superpowers/plans/2026-07-19-p0b2-implementation-record.md`

**Interfaces:**
- Produces: auditable P0-B2 completion evidence without marking post-merge Play verification complete prematurely.

- [ ] **Step 1: Run the full headless suite fresh**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
```

Expected: zero failed/skipped tests. Record the exact passed count in the implementation record.

- [ ] **Step 2: Run the complete Unity EditMode suite fresh**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/p0b2-editmode.xml \
  -logFile /private/tmp/p0b2-editmode.log
```

Expected: result XML reports zero failures and zero skipped tests. Record the exact passed count.

- [ ] **Step 3: Run structural and generated-output checks**

```bash
rg -n '\bCardType\b|GrantNext(Player)?Attack|grant_next_player_attack_damage_bonus' Assets --glob '*.cs' --glob '*.asset'
rg -n '^  Type:' Assets/Unity/CardSO --glob '*.asset'
git diff --check
git status --short --branch
```

Expected: both searches return no matches; diff check is clean; status contains only the two documentation files for this task after prior implementation commits.

- [ ] **Step 4: Update backlog and write the implementation record**

Set P0-B2 status to `구현 완료, 머지 후 사용자 Play 검증 대기` unless the user has already completed Play verification. The record must include:

- design and implementation-plan links;
- commit list;
- RED/GREEN evidence for Tasks 1–5;
- exact headless and Unity EditMode totals;
- generated-code second-run no-diff evidence;
- no-match structural search evidence;
- remaining manual Play verification and master-merge approval gates.

- [ ] **Step 5: Commit documentation**

```bash
git add docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md docs/superpowers/plans/2026-07-19-p0b2-implementation-record.md
git commit -m "docs: record P0-B2 CardType removal"
```

- [ ] **Step 6: Final clean-tree verification**

```bash
git status --short --branch
git log --oneline --decorate -8
```

Expected: clean `refactor/p0-b2-card-type-removal` worktree. Do not merge to `master`; request user approval after reporting verification evidence.
