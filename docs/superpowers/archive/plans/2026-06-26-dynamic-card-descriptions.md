# Dynamic Card Descriptions Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hardcoded `PlaytestKoreanText.CardDescription(id)` switch with an effect-composed description system that builds each card's text from its `EffectData`/`InterventionAction`, substituting numbers from the data so descriptions auto-follow balance tuning.

**Architecture:** A pure-C# `DescriptionComposer` (in `Simulation`) walks a `CardDefinition`'s effects (or its `InterventionAction` for intervention cards) and asks an `IDescriptionVocabulary` for each localized fragment, assembling sentences with a fixed structure (base sentence, then an optional `condition이면 success` sentence per effect, joined by spaces). One Korean implementation, `KoreanDescriptionVocabulary`, owns ALL Korean wording and grammar (including composite-condition joins) so the composer stays a pure dispatcher. The Unity `CardPresentation` calls the composer instead of the switch.

**Tech Stack:** C# 9 (Unity 6 constraint — no `record struct`, no file-scoped namespaces), NUnit, headless `dotnet test` proxy for the pure layers.

---

## Background / Reference (read before starting)

Spec: [`docs/superpowers/specs/2026-06-26-card-descriptions-design.md`](../specs/2026-06-26-card-descriptions-design.md).

Core types the composer reads (already exist — do NOT modify):
- `CardDefinition` (`Assets/Core/Cards/CardDefinition.cs`): `Id`, `Name`, `Category` ( `Execution`|`Intervention` ), `IReadOnlyList<EffectData> Effects`, `InterventionActionData InterventionAction`.
- `EffectData` (same file): `EffectKey Key`, `int EffectValue`, `Condition Condition`, `int? SuccessEffectValue`, `StatusKey? StatusKey`, `StatusLifetime? StatusLifetime`, `StatusApplyTarget StatusTarget`. For `ApplyStatus`, the magnitude rides on `EffectValue`.
- `EffectKeys` (`Core/Effects/EffectKey.cs`): `Damage`, `NullifyNextPlayerConditionReward`, `GrantNextPlayerAttackDamageBonus`, `ApplyStatus`.
- `Condition` records (`Core/Conditions/Condition.cs`): `FirstToTrigger`, `WithinNth(int N)`, `BeforeNextEnemyAttack`, `AdjacentCardIs(AdjacentDirection Direction, Side Side, CardType? Type)`, `SameTarget`, `NoPrecedingCardOfSide(Side Side)`, `AllOf(IReadOnlyList<Condition> Conditions)`.
- `StatusKeys` (`Core/Status/StatusKey.cs`): `Stun`, `Vulnerable`, `RewardNullified`, `Block`, `Slow`, `Haste`.
- `StatusLifetime` (`Core/Status/StatusLifetime.cs`): `Kind` (`Permanent`|`ThisTurn`|`Turns`|`UntilConsumed`), `Count`.
- `StatusApplyTarget` (`Core/Effects/ApplyStatusHandler.cs`): `Self`, `TargetEnemy`.
- `InterventionActionData` (`Core/Intervention/InterventionActionData.cs`): `InterventionActionKey Key`, `int InterventionCost`, `int EffectValue`. `InterventionActionKeys`: `ChangeExecutionOrder`, `SwapExecutionOrder`, `Lock`.

The active decks the output must satisfy:
- `StarterDeck` (`Assets/Core/Simulation/StarterDeck.cs`): slash `Damage(4)`; guard `ApplyStatus(Block, ThisTurn, Self, 4)`; quick_cut `Damage(2, FirstToTrigger→8)`; counter_stance `Damage(4, AdjacentCardIs(Previous,Enemy,Attack)→9)`; cover `ApplyStatus(Block, ThisTurn, Self, 2, AdjacentCardIs(Next,Enemy,Attack)→7)`; pull_forward `Intervention(ChangeExecutionOrder, -2)`; swap_positions `Intervention(SwapExecutionOrder, 0)`.
- `GoblinDeck` (`Assets/Core/Simulation/GoblinDeck.cs`): goblin_jab `Damage(4)`; crude_guard `ApplyStatus(Block, ThisTurn, Self, 3)`; sly_jab `Damage(3, NoPrecedingCardOfSide(Player)→6)`.

**Scope = coverage by KIND, not by card.** The vocab must handle every `EffectKey`, `Condition` record, `StatusKey`, and `InterventionActionKey` above, so any card composed from them renders. Curated flavor-only ids with no effects (e.g. scenario-only `preemptive_thrust`) are out of scope and render to empty string.

**Test runner:** the pure layers compile into `Tests/Headless/FateWeaver.Tests.Headless.csproj` via globs over `Core/`, `Simulation/`, and `Tests/EditMode/`. New files under those folders are picked up automatically. Run from repo root:
`dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj`
Filter to this feature: append `--filter "FullyQualifiedName~Description"`.

When Unity re-imports the new files it generates `.meta` siblings — commit those too.

---

## File Structure

- **Create** `Assets/Core/Simulation/Descriptions/IDescriptionVocabulary.cs` — the localization seam (one interface).
- **Create** `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs` — pure assembly logic; depends only on Core types + the interface.
- **Create** `Assets/Core/Simulation/Descriptions/KoreanDescriptionVocabulary.cs` — the single Korean implementation, owns all wording/grammar, exposes a stateless `Instance` singleton.
- **Create** `Assets/Core/Tests/EditMode/DescriptionComposerTests.cs` — composer logic isolated via a fake vocab + a few real Korean integration assertions (headless).
- **Modify** `Assets/Unity/CardPresentation.cs` — call `DescriptionComposer.Describe(def, KoreanDescriptionVocabulary.Instance)` instead of `PlaytestKoreanText.CardDescription(def.Id)`.
- **Modify** `Assets/Unity/PlaytestKoreanText.cs` — delete the `CardDescription` method (now dead).
- **Modify** `Assets/Tests/UnityEditMode/CardDescriptionTests.cs` — drop the `CardDescription` assertions (logic moved to headless); keep only the `CardName` assertions, renamed.

All `Descriptions/*.cs` are in namespace `FateWeaver.Simulation.Descriptions` and reference only `FateWeaver.Core.*` (no UnityEngine) so they stay headless.

---

## Task 1: Description vocabulary interface

**Files:**
- Create: `Assets/Core/Simulation/Descriptions/IDescriptionVocabulary.cs`

This is a pure interface (no behavior to test on its own); it's exercised by the composer tests in Task 2 via a fake implementation, and by the Korean impl in Task 3. The composer asks the vocab for each *fragment* (no trailing period — the composer adds sentence punctuation). All condition rendering, including `AllOf`, lives behind a single `Condition` method so Korean grammar stays in the implementation.

- [ ] **Step 1: Create the interface**

```csharp
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Supplies localized text fragments for the <see cref="DescriptionComposer"/>.
    /// Fragments carry NO trailing punctuation; the composer assembles sentences. One implementation
    /// per language owns all wording and grammar (including composite-condition joins).</summary>
    public interface IDescriptionVocabulary
    {
        /// <summary>e.g. "피해 4".</summary>
        string Damage(int amount);

        /// <summary>e.g. "방어 4", "적 둔화 3 (2턴)". <paramref name="magnitude"/> is the status strength
        /// (rides on EffectData.EffectValue); <paramref name="lifetime"/> drives any duration suffix.</summary>
        string Status(StatusKey key, StatusApplyTarget target, int magnitude, StatusLifetime lifetime);

        /// <summary>e.g. "다음 플레이어 조건 보상을 무효화".</summary>
        string NullifyNextReward();

        /// <summary>e.g. "다음 플레이어 공격 피해 +6".</summary>
        string GrantNextAttackBonus(int amount);

        /// <summary>A full conditional clause ending in the appropriate Korean conditional ending
        /// (e.g. "첫 발동이면", "바로 뒤가 적 공격이면"). Handles AllOf internally.</summary>
        string Condition(Condition condition);

        /// <summary>A complete intervention-card sentence fragment, e.g. "한 카드의 실행 순서 -2",
        /// "두 카드의 실행 순서을 교환", "한 카드를 고정".</summary>
        string Intervention(InterventionActionData intervention);
    }
}
```

- [ ] **Step 2: Verify it compiles (headless)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~Description"`
Expected: build SUCCEEDS, 0 tests matched/run (no tests yet). A compile error here means a using/namespace typo — fix before continuing.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Core/Simulation/Descriptions/IDescriptionVocabulary.cs"
git commit -m "feat(descriptions): add IDescriptionVocabulary seam"
```

---

## Task 2: DescriptionComposer (logic isolated with a fake vocab)

**Files:**
- Create: `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs`
- Test: `Assets/Core/Tests/EditMode/DescriptionComposerTests.cs`

The composer owns sentence STRUCTURE only; the fake vocab returns marker strings so tests assert assembly (base/condition/success ordering, separators, intervention vs effect dispatch) without depending on Korean wording.

- [ ] **Step 1: Write the failing tests with a fake vocab**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests.EditMode
{
    public class DescriptionComposerTests
    {
        // Fake vocab: marker strings so we assert STRUCTURE, not Korean wording.
        private sealed class FakeVocabulary : IDescriptionVocabulary
        {
            public string Damage(int amount) => "DMG" + amount;
            public string Status(StatusKey key, StatusApplyTarget target, int magnitude, StatusLifetime lifetime)
                => "STATUS:" + key.Id + ":" + target + ":" + magnitude + ":" + lifetime.Kind;
            public string NullifyNextReward() => "NULLIFY";
            public string GrantNextAttackBonus(int amount) => "GRANT" + amount;
            public string Condition(Condition condition) => "COND[" + condition.GetType().Name + "]";
            public string Intervention(InterventionActionData intervention) => "INTERVENTION:" + intervention.Key.Id + ":" + intervention.EffectValue;
        }

        private static readonly IDescriptionVocabulary Vocab = new FakeVocabulary();

        private static CardDefinition Execution(string id, params EffectData[] effects)
            => new CardDefinition(id, id, Side.Player, CardType.Attack, 5, effects)
               { Category = CardCategory.Execution };

        [Test]
        public void Single_damage_effect_is_one_sentence()
        {
            var card = Execution("slash", new EffectData(EffectKeys.Damage, 4));
            Assert.AreEqual("DMG4.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Conditional_effect_appends_condition_then_success_sentence()
        {
            var card = Execution("quick_cut",
                EffectData.Conditional(EffectKeys.Damage, 2, new FirstToTrigger(), 8));
            Assert.AreEqual("DMG2. COND[FirstToTrigger] DMG8.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Multiple_effects_join_with_a_space()
        {
            var card = Execution("wrist_cut",
                new EffectData(EffectKeys.Damage, 3),
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0));
            Assert.AreEqual("DMG3. NULLIFY.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Apply_status_uses_amount_as_magnitude()
        {
            var card = Execution("guard",
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 4));
            Assert.AreEqual("STATUS:block:Self:4:ThisTurn.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Conditional_status_reuses_success_amount_for_the_success_fragment()
        {
            var card = Execution("cover",
                new EffectData(EffectKeys.ApplyStatus, 2)
                {
                    StatusKey = StatusKeys.Block,
                    StatusLifetime = StatusLifetime.ThisTurn,
                    StatusTarget = StatusApplyTarget.Self,
                    Condition = new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack),
                    SuccessEffectValue = 7
                });
            Assert.AreEqual(
                "STATUS:block:Self:2:ThisTurn. COND[AdjacentCardIs] STATUS:block:Self:7:ThisTurn.",
                DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Grant_next_attack_bonus_renders_its_amount()
        {
            var card = Execution("mark", new EffectData(EffectKeys.GrantNextPlayerAttackDamageBonus, 6));
            Assert.AreEqual("GRANT6.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Intervention_card_renders_the_intervention_action_and_ignores_effects()
        {
            var card = new CardDefinition("pull_forward", "pull", Side.Player, CardType.Skill, 0,
                new EffectData[0])
            {
                Category = CardCategory.Intervention,
                InterventionAction = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, 1, -2)
            };
            Assert.AreEqual("INTERVENTION:change_execution_order:-2.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Card_with_no_effects_or_intervention_renders_empty()
        {
            var card = Execution("flavor_only");
            Assert.AreEqual(string.Empty, DescriptionComposer.Describe(card, Vocab));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~DescriptionComposerTests"`
Expected: FAIL — build error "DescriptionComposer does not exist in the current context".

- [ ] **Step 3: Write the composer**

```csharp
using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Builds a card's description from its effects (or intervention action), substituting numbers from
    /// the data. Pure: all wording comes from the supplied <see cref="IDescriptionVocabulary"/>.
    /// Structure per effect: "{base}." optionally followed by " {condition} {success}." Effects join
    /// with a single space. Intervention cards render their intervention action instead of effects.</summary>
    public static class DescriptionComposer
    {
        public static string Describe(CardDefinition def, IDescriptionVocabulary vocab)
        {
            if (def.Category == CardCategory.Intervention && def.InterventionAction != null)
                return vocab.Intervention(def.InterventionAction) + ".";

            if (def.Effects == null || def.Effects.Count == 0)
                return string.Empty;

            var sentences = new List<string>(def.Effects.Count);
            foreach (var effect in def.Effects)
                sentences.Add(RenderEffect(effect, vocab));

            return string.Join(" ", sentences);
        }

        private static string RenderEffect(EffectData effect, IDescriptionVocabulary vocab)
        {
            var sb = new StringBuilder();
            sb.Append(Fragment(effect, effect.EffectValue, vocab)).Append('.');

            if (effect.Condition != null && effect.SuccessEffectValue.HasValue)
            {
                sb.Append(' ')
                  .Append(vocab.Condition(effect.Condition))
                  .Append(' ')
                  .Append(Fragment(effect, effect.SuccessEffectValue.Value, vocab))
                  .Append('.');
            }

            return sb.ToString();
        }

        // One effect's fragment for a given amount (base or success). No trailing punctuation.
        private static string Fragment(EffectData effect, int amount, IDescriptionVocabulary vocab)
        {
            if (effect.Key == EffectKeys.Damage)
                return vocab.Damage(amount);

            if (effect.Key == EffectKeys.ApplyStatus)
                return vocab.Status(
                    effect.StatusKey.Value,
                    effect.StatusTarget,
                    amount,
                    effect.StatusLifetime ?? Core.Status.StatusLifetime.ThisTurn);

            if (effect.Key == EffectKeys.NullifyNextPlayerConditionReward)
                return vocab.NullifyNextReward();

            if (effect.Key == EffectKeys.GrantNextPlayerAttackDamageBonus)
                return vocab.GrantNextAttackBonus(amount);

            return string.Empty;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~DescriptionComposerTests"`
Expected: PASS, 8 tests.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Core/Simulation/Descriptions/DescriptionComposer.cs" "Assets/Core/Tests/EditMode/DescriptionComposerTests.cs"
git commit -m "feat(descriptions): add DescriptionComposer with structure tests"
```

---

## Task 3: KoreanDescriptionVocabulary (real wording)

**Files:**
- Create: `Assets/Core/Simulation/Descriptions/KoreanDescriptionVocabulary.cs`
- Test: append to `Assets/Core/Tests/EditMode/DescriptionComposerTests.cs`

The Korean impl owns every word. It mirrors the existing wording in `PlaytestKoreanText` where sensible. Note: conditional cards now show the *actual success value* (e.g. "방어 7"), not the old "+5" delta — this is the intended behavior from the spec (numbers follow the data).

Status-key Korean names mirror `PlaytestKoreanText.StatusName`: Block=방어, Slow=둔화, Haste=가속, Stun=기절, Vulnerable=취약, RewardNullified="조건 보상 무효".

- [ ] **Step 1: Write the failing Korean integration tests**

Append these methods inside the existing `DescriptionComposerTests` class (before the closing brace):

```csharp
        // --- Korean vocabulary integration (real output) ---------------------

        private static readonly IDescriptionVocabulary Kr = KoreanDescriptionVocabulary.Instance;

        [Test]
        public void Korean_slash() =>
            Assert.AreEqual("피해 4.",
                DescriptionComposer.Describe(StarterDeck.Slash(), Kr));

        [Test]
        public void Korean_guard() =>
            Assert.AreEqual("방어 4.",
                DescriptionComposer.Describe(StarterDeck.Guard(), Kr));

        [Test]
        public void Korean_quick_cut() =>
            Assert.AreEqual("피해 2. 첫 발동이면 피해 8.",
                DescriptionComposer.Describe(StarterDeck.QuickCut(), Kr));

        [Test]
        public void Korean_counter_stance() =>
            Assert.AreEqual("피해 4. 바로 앞이 적 공격이면 피해 9.",
                DescriptionComposer.Describe(StarterDeck.Counter(), Kr));

        [Test]
        public void Korean_cover() =>
            Assert.AreEqual("방어 2. 바로 뒤가 적 공격이면 방어 7.",
                DescriptionComposer.Describe(StarterDeck.Cover(), Kr));

        [Test]
        public void Korean_pull_forward() =>
            Assert.AreEqual("한 카드의 실행 순서 -2.",
                DescriptionComposer.Describe(StarterDeck.PullForward(), Kr));

        [Test]
        public void Korean_swap_positions() =>
            Assert.AreEqual("두 카드의 실행 순서을 교환.",
                DescriptionComposer.Describe(StarterDeck.SwapPositions(), Kr));

        [Test]
        public void Korean_goblin_jab() =>
            Assert.AreEqual("피해 4.",
                DescriptionComposer.Describe(GoblinDeck.Thrust(), Kr));

        [Test]
        public void Korean_crude_guard() =>
            Assert.AreEqual("방어 3.",
                DescriptionComposer.Describe(GoblinDeck.CrudeGuard(), Kr));

        [Test]
        public void Korean_sly_jab() =>
            Assert.AreEqual("피해 3. 앞에 플레이어 카드가 없으면 피해 6.",
                DescriptionComposer.Describe(GoblinDeck.SlyJab(), Kr));

        [Test]
        public void Korean_number_token_follows_data()
        {
            // Re-tuning the amount changes the description automatically (the whole point).
            var tuned = new CardDefinition("slash", "베기", Side.Player, CardType.Attack, 4,
                new[] { new EffectData(EffectKeys.Damage, 99) }) { Category = CardCategory.Execution };
            Assert.AreEqual("피해 99.", DescriptionComposer.Describe(tuned, Kr));
        }

        [Test]
        public void Korean_slow_status_shows_turn_suffix()
        {
            var card = new CardDefinition("slow_hex", "둔화 저주", Side.Player, CardType.Skill, 5,
                new[]
                {
                    EffectData.ApplyStatus(StatusKeys.Slow, StatusLifetime.Turns(2),
                        StatusApplyTarget.TargetEnemy, 3)
                }) { Category = CardCategory.Execution };
            Assert.AreEqual("적 둔화 3 (2턴).", DescriptionComposer.Describe(card, Kr));
        }

        [Test]
        public void Korean_allof_condition_joins_naturally()
        {
            // A single conditional effect (base 1, 6 on success when prev is a player card AND within the 3rd slot).
            var card = new CardDefinition("chain", "연쇄 베기", Side.Player, CardType.Attack, 5,
                new[]
                {
                    EffectData.Conditional(
                        EffectKeys.Damage, 1,
                        new AllOf(new Condition[]
                        {
                            new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, null),
                            new WithinNth(3)
                        }),
                        6)
                }) { Category = CardCategory.Execution };
            Assert.AreEqual("피해 1. 바로 앞이 플레이어 카드이고 3번째 안이면 피해 6.",
                DescriptionComposer.Describe(card, Kr));
        }
```

Note: `DescriptionComposerTests.cs` already has `using FateWeaver.Simulation;` indirectly? It does not — add `using FateWeaver.Simulation;` to the file's using block (for `StarterDeck`/`GoblinDeck`). The `using` additions: ensure these are present at the top of the file: `using FateWeaver.Simulation;`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~DescriptionComposerTests"`
Expected: FAIL — build error "KoreanDescriptionVocabulary does not exist".

- [ ] **Step 3: Write the Korean vocabulary**

```csharp
using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>The Korean description vocabulary. Stateless; use <see cref="Instance"/>.
    /// Owns all Korean wording and grammar (including AllOf joins via condition stems).</summary>
    public sealed class KoreanDescriptionVocabulary : IDescriptionVocabulary
    {
        public static readonly KoreanDescriptionVocabulary Instance = new KoreanDescriptionVocabulary();

        public string Damage(int amount) => "피해 " + amount;

        public string Status(StatusKey key, StatusApplyTarget target, int magnitude, StatusLifetime lifetime)
        {
            var sb = new StringBuilder();
            if (target == StatusApplyTarget.TargetEnemy)
                sb.Append("적 ");
            sb.Append(StatusName(key)).Append(' ').Append(magnitude);

            var suffix = LifetimeSuffix(lifetime);
            if (suffix != null)
                sb.Append(' ').Append(suffix);

            return sb.ToString();
        }

        public string NullifyNextReward() => "다음 플레이어 조건 보상을 무효화";

        public string GrantNextAttackBonus(int amount) => "다음 플레이어 공격 피해 +" + amount;

        public string Condition(Condition condition)
        {
            switch (condition)
            {
                // Verb-stem predicate: takes "...없으면", not "...없으" + "이면".
                case NoPrecedingCardOfSide n:
                    return "앞에 " + SideName(n.Side) + " 카드가 없으면";
                case AllOf all:
                    return JoinAll(all.Conditions) + "이면";
                default:
                    return ConditionStem(condition) + "이면";
            }
        }

        public string Intervention(InterventionActionData intervention)
        {
            if (intervention.Key == InterventionActionKeys.ChangeExecutionOrder)
                return "한 카드의 실행 순서 " + (intervention.EffectValue >= 0 ? "+" + intervention.EffectValue : intervention.EffectValue.ToString());
            if (intervention.Key == InterventionActionKeys.SwapExecutionOrder)
                return "두 카드의 실행 순서을 교환";
            if (intervention.Key == InterventionActionKeys.Lock)
                return "한 카드를 고정";
            return string.Empty;
        }

        // --- helpers ---------------------------------------------------------

        // A condition stem WITHOUT the trailing "이면" so AllOf can join with "이고".
        private static string ConditionStem(Condition condition)
        {
            switch (condition)
            {
                case FirstToTrigger _:
                    return "첫 발동";
                case WithinNth w:
                    return w.N + "번째 안";
                case BeforeNextEnemyAttack _:
                    return "다음 적 공격 전";
                case SameTarget _:
                    return "같은 대상";
                case AdjacentCardIs a:
                    return AdjacentStem(a);
                case AllOf all:
                    return JoinAll(all.Conditions);
                default:
                    return string.Empty;
            }
        }

        // "바로 앞이 적 공격" / "바로 뒤가 적 공격" / "바로 앞이 플레이어 카드"
        private static string AdjacentStem(AdjacentCardIs a)
        {
            var position = a.Direction == AdjacentDirection.Previous ? "바로 앞이 " : "바로 뒤가 ";
            var subject = a.Type.HasValue
                ? SideName(a.Side) + " " + CardTypeName(a.Type.Value)
                : SideName(a.Side) + " 카드";
            return position + subject;
        }

        // Join child stems with "이고 ", e.g. "바로 앞이 플레이어 카드이고 3번째 안".
        private static string JoinAll(IReadOnlyList<Condition> children)
        {
            var stems = new string[children.Count];
            for (int i = 0; i < children.Count; i++)
                stems[i] = ConditionStem(children[i]);
            return string.Join("이고 ", stems);
        }

        private static string LifetimeSuffix(StatusLifetime lifetime)
        {
            switch (lifetime.Kind)
            {
                case StatusLifetimeKind.Turns:
                    return "(" + lifetime.Count + "턴)";
                case StatusLifetimeKind.UntilConsumed:
                    return "(" + lifetime.Count + "회)";
                default:
                    return null; // Permanent / ThisTurn: no suffix
            }
        }

        private static string SideName(Side side) => side == Side.Player ? "플레이어" : "적";

        private static string CardTypeName(CardType type)
        {
            switch (type)
            {
                case CardType.Attack: return "공격";
                case CardType.Defense: return "방어";
                default: return "스킬";
            }
        }

        private static string StatusName(StatusKey key)
        {
            if (key == StatusKeys.Block) return "방어";
            if (key == StatusKeys.Slow) return "둔화";
            if (key == StatusKeys.Haste) return "가속";
            if (key == StatusKeys.Stun) return "기절";
            if (key == StatusKeys.Vulnerable) return "취약";
            if (key == StatusKeys.RewardNullified) return "조건 보상 무효";
            return key.ToString();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~DescriptionComposerTests"`
Expected: PASS (8 structure + 13 Korean = 21 tests).

- [ ] **Step 5: Run the full headless suite (no regressions)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj`
Expected: PASS, all prior tests still green.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Core/Simulation/Descriptions/KoreanDescriptionVocabulary.cs" "Assets/Core/Tests/EditMode/DescriptionComposerTests.cs"
git commit -m "feat(descriptions): Korean vocabulary + integration tests"
```

---

## Task 4: Wire CardPresentation to the composer; remove the dead switch

**Files:**
- Modify: `Assets/Unity/CardPresentation.cs` (lines 45 and 59 — the two `PlaytestKoreanText.CardDescription(def.Id)` calls)
- Modify: `Assets/Unity/PlaytestKoreanText.cs` (delete `CardDescription`, lines 46-73)
- Modify: `Assets/Tests/UnityEditMode/CardDescriptionTests.cs` (drop CardDescription assertions)

This layer is Unity (UnityEngine) — NOT headless. Verification is the user running Play after the edits (report nothing as "verified" beyond compile reasoning; ask the user to confirm in-editor). The `DescriptionComposer`/`KoreanDescriptionVocabulary` live in `FateWeaver.Simulation`, which `FateWeaver.Unity` already references (it uses `GoblinDeck`, `PlaytestKoreanText` imports `FateWeaver.Simulation`), so no asmdef change is needed.

- [ ] **Step 1: Add the Descriptions using to CardPresentation**

In `Assets/Unity/CardPresentation.cs`, add to the using block (after `using FateWeaver.Core.Combat;`):

```csharp
using FateWeaver.Simulation.Descriptions;
```

- [ ] **Step 2: Replace the two description calls**

In `CardPresentation.From` (line ~45) replace:

```csharp
                PlaytestKoreanText.CardDescription(def.Id),
```

with:

```csharp
                DescriptionComposer.Describe(def, KoreanDescriptionVocabulary.Instance),
```

In `CardPresentation.FromDefinition` (line ~59) replace the identical line with the same replacement. Both call sites now read the description from the card's effect data.

- [ ] **Step 3: Delete the dead CardDescription method**

In `Assets/Unity/PlaytestKoreanText.cs`, delete the entire `CardDescription` method (the block from `public static string CardDescription(string id)` through its closing brace, lines 46-73). Leave `CardName`, `StatusName`, and the rest intact. Remove now-unused usings only if the compiler flags them (it should not — others remain used).

- [ ] **Step 4: Trim the obsolete UnityEditMode test**

The description coverage now lives in the headless `DescriptionComposerTests`. Replace the contents of `Assets/Tests/UnityEditMode/CardDescriptionTests.cs` with only the still-valid `CardName` assertions:

```csharp
using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardDescriptionTests
    {
        [Test]
        public void Cards_have_curated_names()
        {
            Assert.AreEqual("베기", PlaytestKoreanText.CardName("slash", "fallback"));
            Assert.AreEqual("찌르기", PlaytestKoreanText.CardName("goblin_jab", "fallback"));
            Assert.AreEqual("조잡한 방어", PlaytestKoreanText.CardName("crude_guard", "fallback"));
            Assert.AreEqual("약삭빠른 찌르기", PlaytestKoreanText.CardName("sly_jab", "fallback"));
        }

        [Test]
        public void Suffixed_ids_match_by_prefix()
        {
            Assert.AreEqual("찰나의 베기", PlaytestKoreanText.CardName("quick_cut_t1", "fallback"));
        }
    }
}
```

- [ ] **Step 5: Confirm the headless suite still builds/passes**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj`
Expected: PASS. (This does NOT compile the Unity layer or the UnityEditMode tests — those are verified in-editor.)

- [ ] **Step 6: Ask the user to verify in Unity**

The Unity layer has no headless path. Ask the user to: open the project, let it compile (watch the Console for errors in `CardPresentation`/`PlaytestKoreanText`), enter Play on the DeckPlaytest scene, and confirm each card shows its description (e.g. slash "피해 4.", cover "방어 2. 바로 뒤가 적 공격이면 방어 7."). Report any console errors back before committing if the user surfaces them.

- [ ] **Step 7: Commit (after user confirms compile/Play)**

```bash
git add "Assets/Unity/CardPresentation.cs" "Assets/Unity/PlaytestKoreanText.cs" "Assets/Tests/UnityEditMode/CardDescriptionTests.cs"
git commit -m "feat(descriptions): drive CardPresentation from the composer, drop hardcoded switch"
```

---

## Self-Review

**1. Spec coverage:**
- Effect-composed descriptions, numbers from data → Tasks 2-3 (`Korean_number_token_follows_data`). ✓
- `DescriptionComposer` pure + headless → Task 2. ✓
- `IDescriptionVocabulary` + `KoreanDescriptionVocabulary` in Simulation → Tasks 1, 3. ✓
- Token mapping (`{dmg}`=EffectValue, `{dmg_success}`=SuccessEffectValue, `{mag}`=ApplyStatus EffectValue, `{turns}`=lifetime Count, `{amt}`=intervention EffectValue, target text) → `Damage`/`Status`/`Intervention` + `Fragment` reuse of SuccessEffectValue. ✓
- Call site swap in `CardPresentation`, switch removed → Task 4. ✓
- Coverage of all current effects/conditions/statuses/intervention → vocab handles every `EffectKey`, `Condition` record, `StatusKey`, `InterventionActionKey`. ✓
- Open question #1 (condition on ApplyStatus, cover) → resolved: success fragment re-renders the same effect kind; tested by `Conditional_status_reuses_success_amount` + `Korean_cover`. ✓
- Open question #2 (multi-effect separator) → resolved: per-sentence `.`, joined by single space; tested by `Multiple_effects_join_with_a_space`. ✓
- Rewrite existing `CardDescriptionTests` as headless assembly tests → Task 3 replaces description coverage headless; Task 4 trims the Unity file to name-only. ✓
- Non-goals (real localization, modifier colors, ownership status layer, keyword tooltips, card *names*) → untouched. ✓

**2. Placeholder scan:** No TBD/TODO/"add error handling"/"similar to". Every code step shows complete code. ✓

**3. Type consistency:** `DescriptionComposer.Describe(CardDefinition, IDescriptionVocabulary)` used identically in Tasks 2, 3, 4. Vocab method names (`Damage`, `Status`, `NullifyNextReward`, `GrantNextAttackBonus`, `Condition`, `Intervention`) consistent between interface (Task 1), fake (Task 2), Korean impl (Task 3). `KoreanDescriptionVocabulary.Instance` defined Task 3, used Tasks 3-4. `EffectData.SuccessEffectValue` is `int?` → guarded with `HasValue`/`.Value`. `EffectData.StatusKey` is `StatusKey?` → `.Value` in `Fragment` only on the ApplyStatus branch (safe — only ApplyStatus sets it). ✓
