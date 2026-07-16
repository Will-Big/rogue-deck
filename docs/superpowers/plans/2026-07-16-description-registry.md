# Card Description Registry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Replace central card-description key branches with fail-fast effect, intervention, and status description registries, then add the missing Korean formation-movement description.

**Architecture:** Pure C# description handlers live in FateWeaver.Simulation.Descriptions, separate from Core execution handlers. DescriptionComposer only selects a registered handler and assembles conditional sentences; KoreanDescriptionCatalog is the composition root shared by headless tests and Unity presentation. Default-content validation checks execution and description registrations before combat content is used.

**Tech Stack:** Unity 6 C# 9, FateWeaver.Core/FateWeaver.Simulation, NUnit, headless .NET 5 test proxy, Unity EditMode tests.

## Global Constraints

- FateWeaver.Core must not reference UnityEngine; descriptions remain in the no-engine Simulation assembly.
- Generate descriptions from EffectData and InterventionActionData; do not use CardAsset.Description.
- New effect/status/intervention descriptions use typed-key registration; add no central semantic key branch.
- Korean only; no localization package, table, reflection registration, or external dependency.
- Use Unity 6 C# 9 syntax; no record struct or file-scoped namespace.
- Red-green-refactor for every change. Run the full headless suite after every task.
- Preserve unrelated Assets/.DS_Store and never stage it.
- Out of scope: authoring redesign, target metadata, RNG, SO source migration, and prefab migration.

---

## File Map

New pure files:

- Assets/Core/Simulation/Descriptions/DescriptionContracts.cs
- Assets/Core/Simulation/Descriptions/EffectDescriptionRegistry.cs
- Assets/Core/Simulation/Descriptions/InterventionDescriptionRegistry.cs
- Assets/Core/Simulation/Descriptions/StatusDescriptionRegistry.cs
- Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs
- Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs
- Assets/Core/Simulation/Descriptions/BuiltInInterventionDescriptionHandlers.cs
- Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs
- Assets/Core/Simulation/Descriptions/DescriptionCatalogValidator.cs
- Assets/Core/Tests/EditMode/DescriptionRegistryTests.cs
- Assets/Core/Tests/EditMode/DescriptionCatalogValidatorTests.cs

Modified integration files:

- Assets/Core/Simulation/Descriptions/DescriptionComposer.cs
- Assets/Core/Tests/EditMode/DescriptionComposerTests.cs
- Assets/Core/Tests/EditMode/PartyDescriptionTests.cs
- Assets/Unity/CardPresentation.cs
- Assets/Unity/PlaytestKoreanText.cs
- Assets/Tests/UnityEditMode/CardPresentationTests.cs
- Assets/Tests/UnityEditMode/PlaytestKoreanTextTests.cs

Removed after migration:

- Assets/Core/Simulation/Descriptions/IDescriptionVocabulary.cs and its .meta
- Assets/Core/Simulation/Descriptions/KoreanDescriptionVocabulary.cs and its .meta

Unity-generated .meta files for new scripts must be staged after Unity imports them. Never copy an existing GUID.

---

### Task 1: Typed description contracts and registries

**Files:**
- Create: Assets/Core/Simulation/Descriptions/DescriptionContracts.cs
- Create: Assets/Core/Simulation/Descriptions/EffectDescriptionRegistry.cs
- Create: Assets/Core/Simulation/Descriptions/InterventionDescriptionRegistry.cs
- Create: Assets/Core/Simulation/Descriptions/StatusDescriptionRegistry.cs
- Test: Assets/Core/Tests/EditMode/DescriptionRegistryTests.cs

**Interfaces:**
- Produces: IEffectDescriptionHandler.Describe(EffectData, int, DescriptionContext).
- Produces: IInterventionDescriptionHandler.DisplayName and Describe(InterventionActionData, DescriptionContext).
- Produces: IDescriptionGrammar for closed target/condition/status/lifetime grammar.
- Produces: Register, Resolve, Contains on all three registries.

- [ ] **Step 1: Write the failing registry tests**

Create DescriptionRegistryTests.cs with these behaviors:

~~~csharp
private sealed class FakeEffectHandler : IEffectDescriptionHandler
{
    public EffectKey Key { get; }
    public FakeEffectHandler(EffectKey key) => Key = key;
    public string Describe(EffectData effect, int value, DescriptionContext context)
        => "effect:" + value;
}

private sealed class FakeInterventionHandler : IInterventionDescriptionHandler
{
    public InterventionActionKey Key { get; }
    public string DisplayName => "fake action";
    public FakeInterventionHandler(InterventionActionKey key) => Key = key;
    public string Describe(InterventionActionData action, DescriptionContext context)
        => "action:" + action.EffectValue;
}

[Test]
public void Effect_registry_is_typed_and_fail_fast()
{
    var key = new EffectKey("test_effect");
    var handler = new FakeEffectHandler(key);
    var registry = new EffectDescriptionRegistry();
    registry.Register(handler);

    Assert.AreSame(handler, registry.Resolve(key));
    Assert.IsTrue(registry.Contains(key));
    Assert.Throws<ArgumentNullException>(() => registry.Register(null));
    Assert.Throws<ArgumentException>(() =>
        registry.Register(new FakeEffectHandler(new EffectKey(null))));
    Assert.Throws<ArgumentException>(() => registry.Register(new FakeEffectHandler(key)));
    Assert.Throws<KeyNotFoundException>(() => registry.Resolve(new EffectKey("missing")));
}

[Test]
public void Intervention_and_status_registries_are_fail_fast()
{
    var actionKey = new InterventionActionKey("test_action");
    var actions = new InterventionDescriptionRegistry();
    actions.Register(new FakeInterventionHandler(actionKey));

    Assert.AreEqual("fake action", actions.Resolve(actionKey).DisplayName);
    Assert.Throws<ArgumentNullException>(() => actions.Register(null));
    Assert.Throws<ArgumentException>(() => actions.Register(
        new FakeInterventionHandler(new InterventionActionKey(null))));
    Assert.Throws<ArgumentException>(() =>
        actions.Register(new FakeInterventionHandler(actionKey)));
    Assert.Throws<KeyNotFoundException>(() =>
        actions.Resolve(new InterventionActionKey("missing")));

    var statusKey = new StatusKey("test_status");
    var statuses = new StatusDescriptionRegistry();
    statuses.Register(statusKey, "시험 상태");

    Assert.AreEqual("시험 상태", statuses.Resolve(statusKey));
    Assert.Throws<ArgumentException>(() => statuses.Register(statusKey, "중복"));
    Assert.Throws<ArgumentException>(() =>
        statuses.Register(new StatusKey("blank"), ""));
    Assert.Throws<KeyNotFoundException>(() =>
        statuses.Resolve(new StatusKey("missing")));
}
~~~

Use namespace FateWeaver.Tests.EditMode and import System, System.Collections.Generic, NUnit, Core Cards/Effects/Intervention/Status, and Simulation.Descriptions.

- [ ] **Step 2: Verify RED**

Run:

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~DescriptionRegistryTests
~~~

Expected: build failure because the handler interfaces, context, and registries do not exist.

- [ ] **Step 3: Add the public contracts**

Create DescriptionContracts.cs:

~~~csharp
public interface IEffectDescriptionHandler
{
    EffectKey Key { get; }
    string Describe(EffectData effect, int effectValue, DescriptionContext context);
}

public interface IInterventionDescriptionHandler
{
    InterventionActionKey Key { get; }
    string DisplayName { get; }
    string Describe(InterventionActionData action, DescriptionContext context);
}

public interface IDescriptionGrammar
{
    string Target(TargetSelector selector);
    string Condition(Condition condition);
    string StatusTargetPrefix(StatusApplyTarget target);
    string LifetimeSuffix(StatusLifetime lifetime);
}

public sealed class DescriptionContext
{
    private readonly IDescriptionGrammar _grammar;

    public DescriptionContext(
        IDescriptionGrammar grammar,
        StatusDescriptionRegistry statuses)
    {
        _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
    }

    public StatusDescriptionRegistry Statuses { get; }

    public string TargetPrefix(EffectData effect)
        => effect.TargetSelector.HasValue
            ? _grammar.Target(effect.TargetSelector.Value) + " "
            : string.Empty;

    public string Condition(Condition condition) => _grammar.Condition(condition);
    public string StatusTargetPrefix(StatusApplyTarget target)
        => _grammar.StatusTargetPrefix(target);
    public string LifetimeSuffix(StatusLifetime lifetime)
        => _grammar.LifetimeSuffix(lifetime);
}
~~~

Wrap in FateWeaver.Simulation.Descriptions and add exact Core/System imports.

- [ ] **Step 4: Implement the registries**

EffectDescriptionRegistry uses Dictionary<EffectKey, IEffectDescriptionHandler> and these exact methods:

~~~csharp
public void Register(IEffectDescriptionHandler handler)
{
    if (handler == null) throw new ArgumentNullException(nameof(handler));
    if (string.IsNullOrWhiteSpace(handler.Key.Id))
        throw new ArgumentException("Effect description key is required.", nameof(handler));
    if (_handlers.ContainsKey(handler.Key))
        throw new ArgumentException(
            "Duplicate effect description key '" + handler.Key + "'.", nameof(handler));
    _handlers.Add(handler.Key, handler);
}

public bool Contains(EffectKey key) => _handlers.ContainsKey(key);

public IEffectDescriptionHandler Resolve(EffectKey key)
    => _handlers.TryGetValue(key, out var handler)
        ? handler
        : throw new KeyNotFoundException(
            "No effect description handler registered for '" + key + "'.");
~~~

InterventionDescriptionRegistry uses Dictionary<InterventionActionKey, IInterventionDescriptionHandler> and the same three methods with these exact typed substitutions:

~~~csharp
public void Register(IInterventionDescriptionHandler handler)
{
    if (handler == null) throw new ArgumentNullException(nameof(handler));
    if (string.IsNullOrWhiteSpace(handler.Key.Id))
        throw new ArgumentException("Intervention description key is required.", nameof(handler));
    if (_handlers.ContainsKey(handler.Key))
        throw new ArgumentException(
            "Duplicate intervention description key '" + handler.Key + "'.", nameof(handler));
    _handlers.Add(handler.Key, handler);
}

public bool Contains(InterventionActionKey key) => _handlers.ContainsKey(key);

public IInterventionDescriptionHandler Resolve(InterventionActionKey key)
    => _handlers.TryGetValue(key, out var handler)
        ? handler
        : throw new KeyNotFoundException(
            "No intervention description handler registered for '" + key + "'.");
~~~

StatusDescriptionRegistry uses Dictionary<StatusKey, string> and this exact API:

~~~csharp
public void Register(StatusKey key, string displayName)
{
    if (string.IsNullOrWhiteSpace(key.Id))
        throw new ArgumentException("Status key is required.", nameof(key));
    if (string.IsNullOrWhiteSpace(displayName))
        throw new ArgumentException("Status display name is required.", nameof(displayName));
    if (_names.ContainsKey(key))
        throw new ArgumentException(
            "Duplicate status description key '" + key + "'.", nameof(key));
    _names.Add(key, displayName);
}

public bool Contains(StatusKey key) => _names.ContainsKey(key);

public string Resolve(StatusKey key)
    => _names.TryGetValue(key, out var displayName)
        ? displayName
        : throw new KeyNotFoundException(
            "No status description registered for '" + key + "'.");
~~~

- [ ] **Step 5: Verify GREEN and full regression**

Run:

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~DescriptionRegistryTests
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
~~~

Expected: registry tests pass; full suite has zero failures.

- [ ] **Step 6: Commit**

~~~bash
git add Assets/Core/Simulation/Descriptions/DescriptionContracts.cs Assets/Core/Simulation/Descriptions/EffectDescriptionRegistry.cs Assets/Core/Simulation/Descriptions/InterventionDescriptionRegistry.cs Assets/Core/Simulation/Descriptions/StatusDescriptionRegistry.cs Assets/Core/Tests/EditMode/DescriptionRegistryTests.cs
git commit -m "feat(descriptions): add typed description registries"
~~~

---

### Task 2: Korean handlers, catalog, and Composer migration

**Files:**
- Create: Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs
- Create: Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs
- Create: Assets/Core/Simulation/Descriptions/BuiltInInterventionDescriptionHandlers.cs
- Create: Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs
- Modify: Assets/Core/Simulation/Descriptions/DescriptionComposer.cs
- Modify: Assets/Core/Tests/EditMode/DescriptionComposerTests.cs
- Modify: Assets/Core/Tests/EditMode/PartyDescriptionTests.cs

**Interfaces:**
- Consumes: Task 1 contracts and registries.
- Produces: KoreanDescriptionCatalog.Default and CreateDefault().
- Produces: DescriptionComposer.Describe(CardDefinition, KoreanDescriptionCatalog).
- Produces: one registered handler for every current effect/intervention key.

- [ ] **Step 1: Write failing formation and unknown-key tests**

Migrate tests to a shared catalog:

~~~csharp
private static readonly KoreanDescriptionCatalog Korean =
    KoreanDescriptionCatalog.CreateDefault();
~~~

Delete FakeVocabulary and the old Vocab/Kr fields. Replace every call argument with Korean. Change the former marker-only expectations to these real registered outputs while preserving the later Korean integration expectations unchanged:

- DMG4. becomes 피해 4.
- TARGET[BackMost] DMG4. becomes 가장 뒤의 대상에게 피해 4.
- DMG2. COND[FirstToTrigger] DMG8. becomes 피해 2. 첫 발동이면 피해 8.
- DMG3. NULLIFY. becomes 피해 3. 다음 플레이어 조건 보상을 무효화.
- STATUS:block:Self:4:ThisTurn. becomes 방어 4.
- STATUS:block:Self:2:ThisTurn. COND[AdjacentCardIs] STATUS:block:Self:7:ThisTurn. becomes 방어 2. 바로 뒤가 적 공격이면 방어 7.
- GRANT6. becomes 다음 플레이어 공격 피해 +6.
- INTERVENTION:change_execution_order:-2. becomes 한 카드의 실행 순서 -2.

Add:

~~~csharp
[TestCase(-2, "소유자를 대형 전방으로 2칸 이동.")]
[TestCase(2, "소유자를 대형 후방으로 2칸 이동.")]
[TestCase(0, "소유자의 대형 위치를 유지.")]
public void Korean_formation_movement_uses_signed_direction(
    int distance,
    string expected)
{
    var card = Execution(
        "move",
        new EffectData(EffectKeys.MoveFormation, distance));

    Assert.AreEqual(expected, DescriptionComposer.Describe(card, Korean));
}

[Test]
public void Unknown_effect_fails_instead_of_rendering_an_empty_sentence()
{
    var card = Execution(
        "unknown",
        new EffectData(new EffectKey("unknown_effect"), 1));

    Assert.Throws<KeyNotFoundException>(() =>
        DescriptionComposer.Describe(card, Korean));
}
~~~

Add a nested empty handler and a Composer guard test. This also fixes the public catalog constructor signature used by later tests:

~~~csharp
private sealed class EmptyEffectDescriptionHandler : IEffectDescriptionHandler
{
    public EffectKey Key => EffectKeys.Damage;
    public string Describe(EffectData effect, int value, DescriptionContext context)
        => string.Empty;
}

[Test]
public void Empty_handler_fragment_fails_fast()
{
    var effects = new EffectDescriptionRegistry();
    effects.Register(new EmptyEffectDescriptionHandler());
    var catalog = new KoreanDescriptionCatalog(
        effects,
        new InterventionDescriptionRegistry(),
        new StatusDescriptionRegistry(),
        new KoreanDescriptionGrammar());

    Assert.Throws<InvalidOperationException>(() =>
        DescriptionComposer.Describe(
            Execution("empty", new EffectData(EffectKeys.Damage, 1)),
            catalog));
}
~~~

Add the malformed status-data guard test:

~~~csharp
[Test]
public void Apply_status_requires_its_status_key_and_lifetime()
{
    var card = Execution(
        "invalid_status",
        new EffectData(EffectKeys.ApplyStatus, 1));

    Assert.Throws<ArgumentException>(() =>
        DescriptionComposer.Describe(card, Korean));
}
~~~

- [ ] **Step 2: Verify RED**

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~DescriptionComposerTests|FullyQualifiedName~PartyDescriptionTests"
~~~

Expected: build failure because KoreanDescriptionCatalog and the new Composer signature do not exist.

- [ ] **Step 3: Extract KoreanDescriptionGrammar**

Move Target, Condition, ConditionStem, AdjacentStem, PreviousExecutedStem, JoinAll, SideName, and CardTypeName from KoreanDescriptionVocabulary without changing text.

Add these exact shared fragments:

~~~csharp
public string StatusTargetPrefix(StatusApplyTarget target)
{
    switch (target)
    {
        case StatusApplyTarget.TargetEnemy: return "적 ";
        case StatusApplyTarget.PartyMember: return "선택한 아군에게 ";
        case StatusApplyTarget.AllPartyMembers: return "모든 아군에게 ";
        default: return string.Empty;
    }
}

public string LifetimeSuffix(StatusLifetime lifetime)
{
    switch (lifetime.Kind)
    {
        case StatusLifetimeKind.Turns:
            return "(" + lifetime.Count + "턴)";
        case StatusLifetimeKind.UntilConsumed:
            return "(" + lifetime.Count + "회)";
        default:
            return string.Empty;
    }
}
~~~

- [ ] **Step 4: Implement all effect handlers**

BuiltInEffectDescriptionHandlers.cs contains five sealed handlers:

- DamageDescriptionHandler: target prefix + 피해 + value.
- ApplyStatusDescriptionHandler: validate StatusKey and StatusLifetime, then target prefix + status-target prefix + registered status name + magnitude + optional lifetime suffix.
- NullifyNextPlayerConditionRewardDescriptionHandler: target prefix + 다음 플레이어 조건 보상을 무효화.
- GrantNextPlayerAttackDamageBonusDescriptionHandler: target prefix + 다음 플레이어 공격 피해 +N.
- MoveFormationDescriptionHandler: exact implementation below.

~~~csharp
public sealed class MoveFormationDescriptionHandler : IEffectDescriptionHandler
{
    public EffectKey Key => EffectKeys.MoveFormation;

    public string Describe(
        EffectData effect,
        int effectValue,
        DescriptionContext context)
    {
        if (effectValue == 0) return "소유자의 대형 위치를 유지";

        var distance = effectValue < 0 ? -(long)effectValue : effectValue;
        var direction = effectValue < 0 ? "전방" : "후방";
        return "소유자를 대형 " + direction + "으로 "
            + distance + "칸 이동";
    }
}
~~~

ApplyStatusDescriptionHandler must throw ArgumentException when either nullable field is absent. It must resolve the status name through context.Statuses, never through a key branch.

- [ ] **Step 5: Implement intervention handlers and catalog**

BuiltInInterventionDescriptionHandlers.cs provides:

- ChangeExecutionOrder: DisplayName 실행 순서 변경; description 한 카드의 실행 순서 followed by signed N.
- SwapExecutionOrder: DisplayName 실행 순서 교환; description 두 카드의 실행 순서를 교환.
- Lock: DisplayName 고정; description 한 카드를 고정.

KoreanDescriptionCatalog exposes get-only Effects, Interventions, Statuses, and Context. Its public constructor is:

~~~csharp
public KoreanDescriptionCatalog(
    EffectDescriptionRegistry effects,
    InterventionDescriptionRegistry interventions,
    StatusDescriptionRegistry statuses,
    IDescriptionGrammar grammar)
{
    Effects = effects ?? throw new ArgumentNullException(nameof(effects));
    Interventions = interventions
        ?? throw new ArgumentNullException(nameof(interventions));
    Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
    Context = new DescriptionContext(grammar, statuses);
}
~~~

CreateDefault explicitly registers:

~~~csharp
statuses.Register(StatusKeys.Block, "방어");
statuses.Register(StatusKeys.Slow, "둔화");
statuses.Register(StatusKeys.Haste, "가속");
statuses.Register(StatusKeys.Stun, "기절");
statuses.Register(StatusKeys.Vulnerable, "취약");
statuses.Register(StatusKeys.RewardNullified, "조건 보상 무효");

effects.Register(new DamageDescriptionHandler());
effects.Register(new ApplyStatusDescriptionHandler());
effects.Register(new NullifyNextPlayerConditionRewardDescriptionHandler());
effects.Register(new GrantNextPlayerAttackDamageBonusDescriptionHandler());
effects.Register(new MoveFormationDescriptionHandler());

interventions.Register(new ChangeExecutionOrderDescriptionHandler());
interventions.Register(new SwapExecutionOrderDescriptionHandler());
interventions.Register(new LockDescriptionHandler());
~~~

Expose:

~~~csharp
public static readonly KoreanDescriptionCatalog Default = CreateDefault();
~~~

- [ ] **Step 6: Replace Composer semantic branches**

DescriptionComposer.Describe takes CardDefinition and KoreanDescriptionCatalog, rejects null arguments, and:

1. Resolves intervention handler for intervention cards.
2. Resolves one effect handler for each effect.
3. Uses that same handler for base and conditional-success values.
4. Uses catalog.Context.Condition for the condition clause.
5. Preserves existing punctuation and multi-effect spacing.
6. Returns string.Empty only for a valid execution card with zero effects.

Guard handler output:

~~~csharp
private static string Fragment(
    IEffectDescriptionHandler handler,
    EffectData effect,
    int amount,
    DescriptionContext context)
{
    var fragment = handler.Describe(effect, amount, context);
    if (string.IsNullOrWhiteSpace(fragment))
        throw new InvalidOperationException(
            "Effect description handler returned an empty fragment for '"
            + effect.Key + "'.");
    return fragment;
}
~~~

DescriptionComposer must contain no EffectKeys or InterventionActionKeys reference.

- [ ] **Step 7: Verify GREEN and full regression**

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~DescriptionComposerTests|FullyQualifiedName~PartyDescriptionTests|FullyQualifiedName~DescriptionRegistryTests"
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
~~~

Expected: formation, existing Korean description, and full regression tests all pass.

- [ ] **Step 8: Commit**

~~~bash
git add Assets/Core/Simulation/Descriptions Assets/Core/Tests/EditMode/DescriptionComposerTests.cs Assets/Core/Tests/EditMode/PartyDescriptionTests.cs
git commit -m "refactor(descriptions): dispatch card text through handlers"
~~~

---

### Task 3: Default-content preflight validation

**Files:**
- Create: Assets/Core/Simulation/Descriptions/DescriptionCatalogValidator.cs
- Test: Assets/Core/Tests/EditMode/DescriptionCatalogValidatorTests.cs

**Interfaces:**
- Consumes: runtime registries and KoreanDescriptionCatalog.
- Produces: ValidateDefault(IEnumerable<CardDefinition>, KoreanDescriptionCatalog).
- Produces: public Validate overload with all registries injected.

- [ ] **Step 1: Write failing validation tests**

~~~csharp
private static IReadOnlyList<CardDefinition> DefaultCards()
    => StarterDeck.Build()
        .Concat(GoblinDeck.AllCards())
        .Concat(WardenDeck.Deck())
        .Concat(PartyPrototypeDeck.Build())
        .ToArray();

[Test]
public void Every_default_card_has_runtime_and_description_registrations()
{
    Assert.DoesNotThrow(() =>
        DescriptionCatalogValidator.ValidateDefault(
            DefaultCards(),
            KoreanDescriptionCatalog.CreateDefault()));
}

[Test]
public void Unknown_effect_key_fails_preflight()
{
    var card = new CardDefinition(
        "unknown",
        "unknown",
        Side.Player,
        CardType.Skill,
        5,
        new[] { new EffectData(new EffectKey("unknown_effect"), 1) })
        { Category = CardCategory.Execution };

    Assert.Throws<KeyNotFoundException>(() =>
        DescriptionCatalogValidator.ValidateDefault(
            new[] { card },
            KoreanDescriptionCatalog.CreateDefault()));
}

[Test]
public void Unknown_status_key_fails_preflight()
{
    var card = new CardDefinition(
        "unknown_status",
        "unknown",
        Side.Player,
        CardType.Skill,
        5,
        new[]
        {
            EffectData.ApplyStatus(
                new StatusKey("unknown_status"),
                StatusLifetime.ThisTurn,
                StatusApplyTarget.Self,
                1)
        }) { Category = CardCategory.Execution };

    Assert.Throws<KeyNotFoundException>(() =>
        DescriptionCatalogValidator.ValidateDefault(
            new[] { card },
            KoreanDescriptionCatalog.CreateDefault()));
}
~~~

Add invalid category/data combination coverage:

~~~csharp
[Test]
public void Intervention_category_requires_an_action()
{
    var card = new CardDefinition(
        "invalid_intervention",
        "invalid",
        Side.Player,
        CardType.Skill,
        0,
        new EffectData[0])
        { Category = CardCategory.Intervention };

    Assert.Throws<ArgumentException>(() =>
        DescriptionCatalogValidator.ValidateDefault(
            new[] { card },
            KoreanDescriptionCatalog.CreateDefault()));
}
~~~

Use System.Collections.Generic, System.Linq, NUnit, Core Cards/Effects/Status, Simulation, and Descriptions imports.

- [ ] **Step 2: Verify RED**

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~DescriptionCatalogValidatorTests
~~~

Expected: build failure because DescriptionCatalogValidator does not exist.

- [ ] **Step 3: Implement validation without effect-key branches**

ValidateDefault creates runtime registries through CombatRegistries and delegates to this public overload:

~~~csharp
public static void Validate(
    IEnumerable<CardDefinition> cards,
    KoreanDescriptionCatalog descriptions,
    EffectRegistry effects,
    StatusRegistry statuses,
    InterventionActionRegistry interventions)
~~~

For every card, Validate must:

1. Reject null cards with ArgumentException.
2. For intervention category: require InterventionAction, resolve runtime and description action handlers, and compose once.
3. For execution category: reject InterventionAction, require non-null Effects, and resolve runtime and description effect handlers.
4. Whenever effect.StatusKey.HasValue, resolve both runtime StatusRegistry and description StatusDescriptionRegistry. Do not compare EffectKeys.ApplyStatus.
5. Call DescriptionComposer.Describe so handler-specific required data and empty output fail.
6. Preserve KeyNotFoundException from any missing registry.

- [ ] **Step 4: Verify GREEN and full regression**

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~DescriptionCatalogValidatorTests
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
~~~

Expected: default content passes and malformed content is rejected by Assert.Throws.

- [ ] **Step 5: Commit**

~~~bash
git add Assets/Core/Simulation/Descriptions/DescriptionCatalogValidator.cs Assets/Core/Tests/EditMode/DescriptionCatalogValidatorTests.cs
git commit -m "feat(descriptions): validate default description coverage"
~~~

---

### Task 4: Unity integration and legacy vocabulary removal

**Files:**
- Modify: Assets/Unity/CardPresentation.cs
- Modify: Assets/Unity/PlaytestKoreanText.cs
- Modify: Assets/Tests/UnityEditMode/CardPresentationTests.cs
- Modify: Assets/Tests/UnityEditMode/PlaytestKoreanTextTests.cs
- Delete: IDescriptionVocabulary.cs, KoreanDescriptionVocabulary.cs, and both .meta files

**Interfaces:**
- Consumes: KoreanDescriptionCatalog.Default.
- Produces: Unity description text and registered status/action display names.
- Removes: all legacy vocabulary references and fallback behavior.

- [ ] **Step 1: Add Unity integration assertions**

Add to CardPresentationTests:

~~~csharp
[Test]
public void Formation_card_uses_the_registered_dynamic_description()
{
    var presentation = CardPresentation.FromDefinition(
        PartyPrototypeDeck.MoveForward(),
        id => null);

    Assert.AreEqual(
        "소유자를 대형 전방으로 1칸 이동.",
        presentation.Description);
}
~~~

Add to PlaytestKoreanTextTests:

~~~csharp
[Test]
public void Registered_labels_share_the_description_catalog()
{
    Assert.AreEqual("방어", PlaytestKoreanText.StatusName(StatusKeys.Block));
    Assert.AreEqual(
        "실행 순서 변경",
        PlaytestKoreanText.InterventionActionName(
            InterventionActionKeys.ChangeExecutionOrder));
    Assert.Throws<KeyNotFoundException>(() =>
        PlaytestKoreanText.StatusName(new StatusKey("unknown_status")));
}
~~~

Add FateWeaver.Simulation import to CardPresentationTests and System.Collections.Generic import to PlaytestKoreanTextTests.

- [ ] **Step 2: Record the Unity RED state**

~~~bash
rg -n "KoreanDescriptionVocabulary|StatusKeys\\.|InterventionActionKeys\\." Assets/Unity/CardPresentation.cs Assets/Unity/PlaytestKoreanText.cs
~~~

Expected: old vocabulary calls and direct status/action key branches are found.

- [ ] **Step 3: Migrate Unity consumers**

Both CardPresentation factories call:

~~~csharp
DescriptionComposer.Describe(def, KoreanDescriptionCatalog.Default)
~~~

PlaytestKoreanText uses:

~~~csharp
public static string StatusName(StatusKey key)
    => KoreanDescriptionCatalog.Default.Statuses.Resolve(key);

public static string InterventionActionName(InterventionActionKey key)
    => KoreanDescriptionCatalog.Default.Interventions.Resolve(key).DisplayName;
~~~

Add FateWeaver.Simulation.Descriptions import. Preserve all unrelated scenario/card/enemy wording.

- [ ] **Step 4: Delete the legacy vocabulary**

Delete IDescriptionVocabulary.cs, KoreanDescriptionVocabulary.cs, and both .meta files only after rg confirms no callers. Do not add compatibility overloads or key-string fallbacks.

- [ ] **Step 5: Run headless and structural gates**

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
test -z "$(rg -l 'IDescriptionVocabulary|KoreanDescriptionVocabulary' Assets --glob '*.cs' || true)"
test -z "$(rg -l 'effect\\.Key == EffectKeys|intervention\\.Key == InterventionActionKeys' Assets/Core/Simulation/Descriptions --glob '*.cs' || true)"
test -z "$(rg -l 'key == StatusKeys|key == InterventionActionKeys' Assets/Unity/PlaytestKoreanText.cs || true)"
~~~

Expected: zero headless failures and no paths from the three forbidden searches.

- [ ] **Step 6: Run Unity verification**

In Unity 6:

1. Import scripts and stage their new .meta files.
2. Run CardPresentationTests and PlaytestKoreanTextTests in Unity EditMode.
3. Play the party battle scene.
4. Confirm [검증] 대형 이동 shows 소유자를 대형 전방으로 1칸 이동.
5. Confirm existing damage, defense, status, condition, and intervention text is unchanged.
6. Confirm Console has no missing-script, duplicate-registration, or missing-description exception.

Expected: selected EditMode tests pass and the checklist matches.

- [ ] **Step 7: Commit**

~~~bash
git add Assets/Core/Simulation/Descriptions Assets/Unity/CardPresentation.cs Assets/Unity/PlaytestKoreanText.cs Assets/Tests/UnityEditMode/CardPresentationTests.cs Assets/Tests/UnityEditMode/PlaytestKoreanTextTests.cs
git commit -m "refactor(unity): use registered card descriptions"
~~~

---

### Task 5: Final verification and truthful design status

**Files:**
- Modify: docs/superpowers/specs/2026-07-16-description-registry-design.md

**Interfaces:**
- Consumes: Tasks 1-4.
- Produces: verification evidence and final spec status.

- [ ] **Step 1: Run fresh final verification**

~~~bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
git diff --check
test -z "$(rg -l 'IDescriptionVocabulary|KoreanDescriptionVocabulary' Assets --glob '*.cs' || true)"
test -z "$(rg -l 'effect\\.Key == EffectKeys|intervention\\.Key == InterventionActionKeys' Assets/Core/Simulation/Descriptions --glob '*.cs' || true)"
rg -n "MoveFormationDescriptionHandler|소유자를 대형 전방으로" Assets/Core/Simulation/Descriptions Assets/Core/Tests/EditMode Assets/Tests/UnityEditMode
~~~

Expected: zero test failures, silent diff/forbidden searches, and matches for the movement handler plus headless and Unity assertions.

- [ ] **Step 2: Check all eight spec goals**

Confirm direct code/test evidence for:

- typed effect handler registration
- typed intervention handler registration
- status display registration
- no Composer semantic key branch
- no code for a new card composed only from existing effects
- EffectData-driven text, not CardAsset.Description
- missing and duplicate registration failure
- pure headless coverage

If any item lacks evidence, return to its owning task; do not weaken the spec.

- [ ] **Step 3: Update spec status**

After successful Unity verification:

~~~markdown
- 상태: 구현 완료 — 헤드리스 및 Unity 검증 완료
~~~

If Unity verification is still pending:

~~~markdown
- 상태: 구현 완료 — 헤드리스 검증 완료, Unity 검증 대기
~~~

- [ ] **Step 4: Commit status evidence**

~~~bash
git add docs/superpowers/specs/2026-07-16-description-registry-design.md
git commit -m "docs: record description registry verification"
~~~
