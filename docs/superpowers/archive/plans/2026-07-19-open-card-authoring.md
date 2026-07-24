# P0-B 열린 카드 저작 구조 Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 새 효과 저작 타입을 중앙 enum/switch 수정 없이 추가할 수 있도록, 저작 층을 다형 spec 클래스로 전환하고 코어 `EffectData`의 ApplyStatus 전용 필드를 payload로 이관한다.

**Architecture:** 효과별 `[Serializable]` spec 클래스가 자기 파라미터·`ToEffectData()` 매핑·`Validate()` 검증·codegen 리터럴을 소유한다. 코어 `EffectData`는 공용 필드 + `IEffectPayload` 슬롯만 가진다. 조건(`ConditionKind`)은 닫힌 조합형으로 유지한다(백로그 §10). 스펙: `docs/superpowers/specs/2026-07-19-open-card-authoring-design.md`.

**Tech Stack:** C# 9 (Unity 6 호환), NUnit, Unity `[SerializeReference]` + PropertyDrawer (에디터 전용).

## Global Constraints

- 테스트 실행: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo` (로컬 SDK가 .NET 5뿐)
- `FateWeaver.Core`는 UnityEngine 참조 금지. LangVersion 9 (record struct, file-scoped namespace 사용 금지)
- 리플렉션 자동 등록 금지 — spec 타입 목록은 명시적 카탈로그로 등록
- 조건(`ConditionKind`)·`TargetSelectorRef`·`StatusLifetimeKind`·`StatusApplyTarget`은 닫힌 enum 유지
- Unity 레이어 파일은 헤드리스로 컴파일되지 않음 — Unity 쪽 변경은 컴파일/Play 확인을 사용자 검증 항목으로 기록
- 커밋 메시지 끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

---

### Task 1: 콘텐츠 등가 오라클 테스트 (구조 변경 전 안전망)

손코딩 코어 덱(`StarterDeck`, `PartyPrototypeDeck`)을 오라클로 삼아, spec 경로(`CardSpecMapper`)가 만든 `CardDefinition`이 오라클과 의미상 동일함을 서명 문자열로 비교한다. 이 테스트는 Task 2·3의 마이그레이션을 통과해도 계속 성립해야 한다 (golden 파일 없이 양쪽 경로가 서로를 검증).

**Files:**
- Create: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs`

**Interfaces:**
- Produces: `CardContentEquivalenceTests.Sig(CardDefinition)` — Task 2에서 payload 형태로 갱신됨 (아래 Task 2 Step 4에 갱신 코드 있음)

- [ ] **Step 1: 테스트 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Generated;

namespace FateWeaver.Tests
{
    /// <summary>Authoring-path equivalence oracle (백로그 §11: 동작 등가성 테스트 선확보).
    /// The hand-coded core decks are the oracle; the spec/mapper path must produce semantically
    /// identical CardDefinitions. Signatures are shape-agnostic so the payload migration keeps passing.</summary>
    public class CardContentEquivalenceTests
    {
        internal static string Sig(CardDefinition d) => string.Join(";",
            d.Id, d.Name, d.Side, d.Type, d.Category, d.EnergyCost, d.BaseExecutionOrder,
            d.InterventionAction == null
                ? "-"
                : d.InterventionAction.Key + ":" + d.InterventionAction.InterventionCost
                    + ":" + d.InterventionAction.EffectValue,
            string.Join("|", d.Effects.Select(EffectSig)));

        private static string EffectSig(EffectData e) => string.Join(",",
            e.Key, e.EffectValue,
            e.Condition == null ? "-" : e.Condition.ToString(),
            e.SuccessEffectValue?.ToString() ?? "-",
            e.TargetSelector?.ToString() ?? "-",
            StatusSig(e));

        // Task 2에서 payload 기반으로 교체된다 (형태만 바뀌고 출력 문자열은 동일해야 한다).
        private static string StatusSig(EffectData e)
            => !e.StatusKey.HasValue
                ? "-"
                : e.StatusKey.Value + "/" + e.StatusLifetime.Value.Kind + ":" + e.StatusLifetime.Value.Count
                    + "/" + e.StatusTarget;

        private static List<string> Sigs(IEnumerable<CardDefinition> defs)
            => defs.Select(Sig).OrderBy(s => s).ToList();

        [Test]
        public void Starter_specs_match_handcoded_starter_deck()
            => CollectionAssert.AreEqual(
                Sigs(StarterDeck.Build()),
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Generated_cards_match_starter_specs()
            => CollectionAssert.AreEqual(
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)),
                Sigs(GeneratedCards.StarterDeck().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Party_prototype_specs_match_handcoded_deck()
            => CollectionAssert.AreEqual(
                Sigs(PartyPrototypeDeck.Build()),
                Sigs(PartyPrototypeDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));
    }
}
```

주의: `PartyPrototypeDeck.Build()`의 실제 시그니처를 먼저 확인하라 (`Assets/Core/Simulation/PartyPrototypeDeck.cs`). 반환이 `IReadOnlyList<CardDefinition>`이 아니면 그에 맞춰 호출부만 조정한다. `GeneratedCards.StarterDeck()`과 `StarterDeckSpecs.Build()`는 카드 순서가 다를 수 있으므로 서명 정렬 비교를 사용한다.

- [ ] **Step 2: 테스트 실행 — 통과 확인 (현재 코드가 이미 등가여야 함)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~CardContentEquivalenceTests`
Expected: PASS 3/3. 실패하면 **구조 변경을 시작하지 말고** 서명 함수가 실제 콘텐츠 차이를 드러낸 것인지 확인해 사용자에게 보고한다 (예: 생성 파일이 stale).

- [ ] **Step 3: Commit**

```bash
git add Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs
git commit -m "test(core): add authoring-path content equivalence oracle (P0-B prep)"
```

---

### Task 2: 코어 EffectData payload 이관

**Files:**
- Create: `Assets/Core/Effects/IEffectPayload.cs`
- Create: `Assets/Core/Effects/ApplyStatusPayload.cs`
- Create: `Assets/Core/Effects/IEffectDataValidator.cs`
- Modify: `Assets/Core/Cards/CardDefinition.cs` (EffectData)
- Modify: `Assets/Core/Effects/ApplyStatusHandler.cs`
- Modify: `Assets/Core/Combat/PartyTargetRules.cs:26`
- Modify: `Assets/Core/Simulation/StarterDeck.cs:67-72` (Cover 카드)
- Modify: `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs` (ApplyStatusDescriptionHandler)
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionCatalogValidator.cs:75-79`
- Modify: `Assets/Core/Simulation/Authoring/CardSpecMapper.cs:41-51` (임시 — Task 3에서 삭제됨)
- Modify: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs` (StatusSig)
- Modify: status 필드를 읽는 기존 테스트 (Step 5의 grep으로 전수 확인)

**Interfaces:**
- Produces: `IEffectPayload` (마커), `ApplyStatusPayload(StatusKey Key, StatusLifetime Lifetime, StatusApplyTarget Target)`, `EffectData.Payload { get; init; }`, `IEffectDataValidator.ValidateData(EffectData) : IEnumerable<string>`
- `EffectData.ApplyStatus(statusKey, lifetime, target, magnitude)` 팩토리는 시그니처 유지 (내부에서 payload 구성) — 코드 덱 5곳(StarterDeck:44, GoblinDeck:29, WardenDeck:35·42, PartyPrototypeDeck, SampleMultiTurnScenarios:146)은 수정 불필요

- [ ] **Step 1: 새 코어 타입 3개 작성**

`Assets/Core/Effects/IEffectPayload.cs`:
```csharp
namespace FateWeaver.Core.Effects
{
    /// <summary>Per-effect-kind parameter block carried by EffectData. Each effect key that needs
    /// parameters beyond the shared scalar declares its own payload record; the common model never
    /// grows per-effect fields (AGENTS.md rule 9).</summary>
    public interface IEffectPayload
    {
    }
}
```

`Assets/Core/Effects/ApplyStatusPayload.cs`:
```csharp
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Parameters for the apply_status effect. Magnitude rides on EffectData.EffectValue.</summary>
    public sealed record ApplyStatusPayload(
        StatusKey Key,
        StatusLifetime Lifetime,
        StatusApplyTarget Target) : IEffectPayload;
}
```

`Assets/Core/Effects/IEffectDataValidator.cs`:
```csharp
using System.Collections.Generic;

namespace FateWeaver.Core.Effects
{
    /// <summary>Optional handler capability: validates its own EffectData (payload type, required
    /// values) during content validation. Content walks resolve the handler and delegate, so a new
    /// effect's validation lives in its handler class — no central switch.</summary>
    public interface IEffectDataValidator
    {
        IEnumerable<string> ValidateData(EffectData effect);
    }
}
```

- [ ] **Step 2: EffectData 개편** (`Assets/Core/Cards/CardDefinition.cs`)

`EffectData` 레코드에서 `StatusKey`/`StatusLifetime`/`StatusTarget` init 필드 3개를 제거하고 아래로 교체:

```csharp
    /// <summary>One effect entry on a card: which handler + its scalar effect value (M1).</summary>
    public sealed record EffectData(EffectKey Key, int EffectValue)
    {
        public Condition Condition { get; init; }
        public int? SuccessEffectValue { get; init; }

        /// <summary>Effect-kind-specific parameters (null when the scalar is enough).</summary>
        public IEffectPayload Payload { get; init; }

        // Position selector for enemy attacks against the player party formation. Null means
        // FrontMost (pre-party content has no selector, so this keeps single-enemy-attack compat).
        public TargetSelector? TargetSelector { get; init; }

        public static EffectData Conditional(
            EffectKey key,
            int effectValue,
            Condition condition,
            int successEffectValue)
            => new EffectData(key, effectValue)
            {
                Condition = condition,
                SuccessEffectValue = successEffectValue
            };

        public static EffectData ApplyStatus(
            StatusKey statusKey,
            StatusLifetime lifetime,
            StatusApplyTarget target,
            int magnitude = 0)
            => new EffectData(EffectKeys.ApplyStatus, magnitude)
            {
                Payload = new ApplyStatusPayload(statusKey, lifetime, target)
            };
    }
```

- [ ] **Step 3: payload 독자 갱신**

`ApplyStatusHandler.Apply` 도입부(33행 부근)를 payload 기반으로 교체하고, 클래스 선언에 `IEffectDataValidator`를 추가한다. 각 `Apply*` private 메서드의 `EffectData effect` 파라미터는 `ApplyStatusPayload payload`로 바꾸고 `effect.StatusKey.Value` → `payload.Key`, `effect.StatusLifetime.Value` → `payload.Lifetime` 치환:

```csharp
    public sealed class ApplyStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.ApplyStatus;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (!(ctx.Effect?.Payload is ApplyStatusPayload payload))
            {
                return;
            }

            switch (payload.Target)
            {
                case StatusApplyTarget.Self:
                    ApplySelf(ctx, payload);
                    break;
                case StatusApplyTarget.TargetEnemy:
                    ApplyTargetEnemy(ctx, payload);
                    break;
                case StatusApplyTarget.PartyMember:
                    ApplyPartyMember(ctx, payload);
                    break;
                case StatusApplyTarget.AllPartyMembers:
                    ApplyAllPartyMembers(ctx, payload);
                    break;
            }
        }

        public System.Collections.Generic.IEnumerable<string> ValidateData(EffectData effect)
        {
            if (!(effect.Payload is ApplyStatusPayload payload))
            {
                yield return "apply_status effect requires an ApplyStatusPayload.";
                yield break;
            }

            if (string.IsNullOrEmpty(payload.Key.Id))
            {
                yield return "apply_status payload requires a status key.";
            }
        }
```

`PartyTargetRules.RequiresExplicitAllyTarget`(26행):
```csharp
                if (effect.Payload is ApplyStatusPayload payload
                    && payload.Target == StatusApplyTarget.PartyMember)
```
(파일 상단에 `using FateWeaver.Core.Effects;`는 이미 있음.)

`StarterDeck.cs` Cover 카드(67행 부근)의 객체 초기화식을 팩토리 + `with`로 교체 (주변의 Condition/SuccessEffectValue 초기화는 보존):
```csharp
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 2)
                    with
                    {
                        Condition = new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack),
                        SuccessEffectValue = 7
                    }
```
(실제 파일의 기존 Condition 값을 그대로 옮긴다 — 위는 현재 값 기준.)

`ApplyStatusDescriptionHandler.Describe`:
```csharp
        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is ApplyStatusPayload payload))
                throw new ArgumentException(
                    "Apply-status description requires an ApplyStatusPayload.",
                    nameof(effect));

            var suffix = context.LifetimeSuffix(payload.Lifetime);
            return context.TargetPrefix(effect)
                + context.StatusTargetPrefix(payload.Target)
                + context.Statuses.Resolve(payload.Key)
                + " " + effectValue
                + (string.IsNullOrEmpty(suffix) ? string.Empty : " " + suffix);
        }
```

`DescriptionCatalogValidator.Validate`의 효과 루프(72행 이후)를 payload + 핸들러 검증 위임으로 교체:
```csharp
                foreach (var effect in card.Effects)
                {
                    if (effect == null)
                        throw new ArgumentException(
                            "Card effects cannot contain null entries.",
                            nameof(cards));

                    var handler = effects.Resolve(effect.Key);
                    descriptions.Effects.Resolve(effect.Key);

                    if (handler is IEffectDataValidator validator)
                    {
                        foreach (var error in validator.ValidateData(effect))
                            throw new ArgumentException(
                                "Card '" + card.Id + "': " + error, nameof(cards));
                    }

                    if (effect.Payload is ApplyStatusPayload statusPayload)
                    {
                        statuses.Resolve(statusPayload.Key);
                        descriptions.Statuses.Resolve(statusPayload.Key);
                    }
                }
```

`CardSpecMapper.ToEffectData`의 ApplyStatus 분기(41-51행, Task 3에서 통째로 사라질 임시 갱신):
```csharp
            if (e.Kind == EffectKind.ApplyStatus)
            {
                effect = new EffectData(key, e.EffectValue)
                {
                    Payload = new ApplyStatusPayload(
                        ToStatusKey(e.Status),
                        ToLifetime(e.Lifetime, e.LifetimeCount),
                        e.Target),
                    Condition = hasCondition ? ToCondition(e) : null,
                    SuccessEffectValue = hasCondition ? e.SuccessEffectValue : (int?)null
                };
            }
```

- [ ] **Step 4: 등가 테스트 서명 함수 갱신** (`CardContentEquivalenceTests.StatusSig` — 출력 문자열 형식은 동일하게 유지)

```csharp
        private static string StatusSig(EffectData e)
            => !(e.Payload is Core.Effects.ApplyStatusPayload p)
                ? "-"
                : p.Key + "/" + p.Lifetime.Kind + ":" + p.Lifetime.Count + "/" + p.Target;
```
(네임스페이스는 파일의 using에 맞춰 조정. `using FateWeaver.Core.Effects;` 추가가 깔끔하다.)

- [ ] **Step 5: 남은 독자 전수 확인 및 테스트 갱신**

Run: `grep -rn "\.StatusKey\|\.StatusLifetime\b\|StatusTarget =" Assets/Core --include="*.cs" | grep -v Payload | grep -v "StatusApplyTarget"`

남는 곳은 전부 테스트여야 한다 (예: `WardenDeckTests.cs`의 `braceEffect.StatusKey.Value`, `CardSpecMapperTests.cs`의 `e.StatusKey.Value`). 패턴:
- `effect.StatusKey.Value` → `((ApplyStatusPayload)effect.Payload).Key`
- `effect.StatusTarget` → `((ApplyStatusPayload)effect.Payload).Target`
- `Assert.IsTrue(e.StatusKey.HasValue)` → `Assert.IsInstanceOf<ApplyStatusPayload>(e.Payload)`

- [ ] **Step 6: 전체 테스트 실행**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: PASS 전체 (Task 1의 등가 테스트 3개 포함, 총 개수는 기존과 동일).

- [ ] **Step 7: Commit**

```bash
git add -A Assets/Core
git commit -m "refactor(core): move apply-status fields into EffectData payload (P0-B)"
```

---

### Task 3: 다형 EffectSpec 저작 모델 전환

한 커밋으로 전환한다 (구/신 모델은 같은 타입명이라 공존 불가). 끝난 뒤 Task 1 등가 테스트가 그대로 통과해야 한다.

**Files:**
- Rewrite: `Assets/Core/Simulation/Authoring/EffectSpec.cs` (베이스 클래스 + ConditionSpec + 닫힌 enum 유지, `EffectKind`/`StatusKindRef`/`InterventionKind` 삭제)
- Create: `Assets/Core/Simulation/Authoring/StatusKeyRef.cs`, `InterventionKeyRef.cs`
- Create: `Assets/Core/Simulation/Authoring/Specs/DamageSpec.cs`, `ApplyStatusSpec.cs`, `GrantNextAttackBonusSpec.cs`, `NullifyNextRewardSpec.cs`, `MoveFormationSpec.cs`
- Create: `Assets/Core/Simulation/Authoring/EffectSpecCatalog.cs`
- Modify: `Assets/Core/Simulation/Authoring/CardSpec.cs` (Intervention 타입), `CardSpecMapper.cs` (효과 switch 삭제), `StarterDeckSpecs.cs`, `PartyPrototypeDeckSpecs.cs`
- Rewrite: `Assets/Core/Simulation/Generated/GeneratedCards.cs` (손 이관 — 다음 Unity 재생성 때 동일 결과 확인)
- Modify: `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`, `StarterDeckSpecEquivalenceTests.cs:51-56`, `PartyPrototypeDataTests.cs` (spec 리터럴)

**Interfaces:**
- Produces (Task 4·5·6이 사용):
  - `abstract class EffectSpec { ConditionSpec Condition; abstract EffectKey Key { get; } abstract EffectData ToEffectData(); abstract string ToLiteral(); virtual IEnumerable<string> Validate(AuthoringContext); }`
  - `struct ConditionSpec { ConditionKind Kind; int N; int SuccessEffectValue; Condition ToCondition(); }`
  - `struct StatusKeyRef { string Id; StatusKey ToKey(); bool IsEmpty; static StatusKeyRef Of(StatusKey); }`
  - `struct InterventionKeyRef { string Id; InterventionActionKey ToKey(); bool IsEmpty; static InterventionKeyRef Of(InterventionActionKey); }`
  - `static class EffectSpecCatalog { static IReadOnlyList<EffectSpecInfo> All(); }`, `sealed class EffectSpecInfo { string DisplayName; Type SpecType; Func<EffectSpec> Create; }`
- `AuthoringContext`는 Task 4에서 정의된다. Task 3 시점에는 `Validate(AuthoringContext)`가 컴파일되도록 Task 4의 `AuthoringContext.cs`를 **빈 껍데기 없이 Task 4 Step 1 코드 그대로** 먼저 생성해도 된다 — 순서 단순화를 위해 Task 3에서 함께 만든다.

- [ ] **Step 1: 베이스 타입 작성** — `EffectSpec.cs` 전체 교체

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    public enum TargetSelectorRef { None, FrontMost, SecondFromFront, BackMost, Random }

    public enum ConditionKind { None, FirstToTrigger, WithinNth, BeforeNextEnemyAttack, PrevExecutedIsPlayerAttack, NextIsEnemyAttack, PrevExecutedIsEnemyAttack, NoPrecedingPlayerCard, NoFollowingEnemyCard }

    /// <summary>Closed condition combinator (백로그 §10): the kind enum + central switch stay by design.</summary>
    [Serializable]
    public struct ConditionSpec
    {
        public ConditionKind Kind;
        public int N;
        public int SuccessEffectValue;

        public Condition ToCondition()
        {
            switch (Kind)
            {
                case ConditionKind.FirstToTrigger: return new FirstToTrigger();
                case ConditionKind.WithinNth: return new WithinNth(N);
                case ConditionKind.BeforeNextEnemyAttack: return new BeforeNextEnemyAttack();
                case ConditionKind.PrevExecutedIsPlayerAttack:
                    return new PreviousExecutedCardIs(Side.Player, CardType.Attack);
                case ConditionKind.PrevExecutedIsEnemyAttack:
                    return new PreviousExecutedCardIs(Side.Enemy, CardType.Attack);
                case ConditionKind.NextIsEnemyAttack:
                    return new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack);
                case ConditionKind.NoPrecedingPlayerCard:
                    return new NoPrecedingCardOfSide(Side.Player);
                case ConditionKind.NoFollowingEnemyCard:
                    return new NoFollowingCardOfSide(Side.Enemy);
                default: return null;
            }
        }
    }

    /// <summary>One authored effect. Each concrete spec owns its parameters (real types), its mapping
    /// to core EffectData, its validation, and its codegen literal — adding a new effect touches no
    /// central enum/switch (AGENTS.md rule 9). Registered explicitly in EffectSpecCatalog.</summary>
    [Serializable]
    public abstract class EffectSpec
    {
        public ConditionSpec Condition;

        public abstract EffectKey Key { get; }
        public abstract EffectData ToEffectData();

        /// <summary>C# literal for codegen (SO → GeneratedCards.cs). Lives here so a new effect's
        /// authoring+export stay in one class.</summary>
        public abstract string ToLiteral();

        public virtual IEnumerable<string> Validate(AuthoringContext context)
        {
            yield break;
        }

        protected EffectData ApplyCondition(EffectData effect)
            => Condition.Kind == ConditionKind.None
                ? effect
                : effect with
                {
                    Condition = Condition.ToCondition(),
                    SuccessEffectValue = Condition.SuccessEffectValue
                };

        protected string ConditionLiteral()
            => "Condition = new ConditionSpec { Kind = ConditionKind." + Condition.Kind
                + ", N = " + Condition.N
                + ", SuccessEffectValue = " + Condition.SuccessEffectValue + " }";

        protected static TargetSelector? ToSelector(TargetSelectorRef selector)
        {
            switch (selector)
            {
                case TargetSelectorRef.FrontMost: return TargetSelector.FrontMost;
                case TargetSelectorRef.SecondFromFront: return TargetSelector.SecondFromFront;
                case TargetSelectorRef.BackMost: return TargetSelector.BackMost;
                case TargetSelectorRef.Random: return TargetSelector.Random;
                default: return null;
            }
        }

        protected static string Quote(string value)
            => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
```

`StatusKeyRef.cs`:
```csharp
using System;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Serializable reference to an open-set status key. Validated (registry membership)
    /// at editor/boot time instead of being a closed enum.</summary>
    [Serializable]
    public struct StatusKeyRef
    {
        public string Id;

        public bool IsEmpty => string.IsNullOrEmpty(Id);
        public StatusKey ToKey() => new StatusKey(Id);
        public static StatusKeyRef Of(StatusKey key) => new StatusKeyRef { Id = key.Id };
    }
}
```

`InterventionKeyRef.cs`:
```csharp
using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Serializable reference to an open-set intervention action key. Uniform {key, value}
    /// params today; promote to polymorphic specs (like EffectSpec) only when an action needs
    /// unique parameters (설계 문서 §4.1).</summary>
    [Serializable]
    public struct InterventionKeyRef
    {
        public string Id;

        public bool IsEmpty => string.IsNullOrEmpty(Id);
        public InterventionActionKey ToKey() => new InterventionActionKey(Id);
        public static InterventionKeyRef Of(InterventionActionKey key) => new InterventionKeyRef { Id = key.Id };
    }
}
```
(`InterventionActionKey`가 `Id` 프로퍼티/생성자 형태가 `EffectKey`와 같은지 `Assets/Core/Intervention/InterventionActionKey.cs`에서 확인 후 맞춘다.)

- [ ] **Step 2: 서브클래스 5개 작성** (`Assets/Core/Simulation/Authoring/Specs/`)

`DamageSpec.cs`:
```csharp
using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    [Serializable]
    public sealed class DamageSpec : EffectSpec
    {
        public int Value;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.Damage;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Value)) with { TargetSelector = ToSelector(Selector) };

        public override string ToLiteral()
            => "new DamageSpec { Value = " + Value
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";
    }
}
```

`ApplyStatusSpec.cs`:
```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    [Serializable]
    public sealed class ApplyStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public int Value;
        public StatusLifetimeKind Lifetime;
        public int LifetimeCount;
        public StatusApplyTarget Target;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.ApplyStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Value)
            {
                Payload = new ApplyStatusPayload(Status.ToKey(), ToLifetime(), Target)
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty)
            {
                yield return "apply_status spec requires a status key.";
            }
            else if (!context.HasStatus(Status.ToKey()))
            {
                yield return "Unknown status key '" + Status.Id + "'.";
            }
        }

        public override string ToLiteral()
            => "new ApplyStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", Value = " + Value
                + ", Lifetime = StatusLifetimeKind." + Lifetime
                + ", LifetimeCount = " + LifetimeCount
                + ", Target = StatusApplyTarget." + Target
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";

        private StatusLifetime ToLifetime()
        {
            switch (Lifetime)
            {
                case StatusLifetimeKind.Permanent: return StatusLifetime.Permanent;
                case StatusLifetimeKind.Turns: return StatusLifetime.Turns(LifetimeCount);
                case StatusLifetimeKind.UntilConsumed: return StatusLifetime.UntilConsumed(LifetimeCount);
                default: return StatusLifetime.ThisTurn;
            }
        }
    }
}
```

`GrantNextAttackBonusSpec.cs` / `NullifyNextRewardSpec.cs` / `MoveFormationSpec.cs` (동형, 각각 자기 키):
```csharp
using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    [Serializable]
    public sealed class GrantNextAttackBonusSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.GrantNextPlayerAttackDamageBonus;

        public override EffectData ToEffectData() => ApplyCondition(new EffectData(Key, Value));

        public override string ToLiteral()
            => "new GrantNextAttackBonusSpec { Value = " + Value + ", " + ConditionLiteral() + " }";
    }
}
```
`NullifyNextRewardSpec`: 파라미터 없음 (`Value` 없이 `new EffectData(Key, 0)`), Key는 `EffectKeys.NullifyNextPlayerConditionReward`.
`MoveFormationSpec`: `public int Value;` (이동 거리, 음수=전방), Key는 `EffectKeys.MoveFormation`, Damage와 동형 (Selector 없음).

`EffectSpecCatalog.cs` (명시적 등록 — 리플렉션 금지):
```csharp
using System;
using System.Collections.Generic;

namespace FateWeaver.Simulation.Authoring
{
    public sealed class EffectSpecInfo
    {
        public EffectSpecInfo(string displayName, Type specType, Func<EffectSpec> create)
        {
            DisplayName = displayName;
            SpecType = specType;
            Create = create;
        }

        public string DisplayName { get; }
        public Type SpecType { get; }
        public Func<EffectSpec> Create { get; }
    }

    /// <summary>Explicit list of authorable effect specs. The Unity drawer's dropdown and the boot
    /// validation cross-check both read this — registering here is the one step besides writing the
    /// spec/handler classes (AGENTS.md rule 9: 핸들러 1개 + 키 등록).</summary>
    public static class EffectSpecCatalog
    {
        public static IReadOnlyList<EffectSpecInfo> All() => new[]
        {
            new EffectSpecInfo("피해", typeof(DamageSpec), () => new DamageSpec()),
            new EffectSpecInfo("상태 부여", typeof(ApplyStatusSpec), () => new ApplyStatusSpec()),
            new EffectSpecInfo("다음 공격 강화", typeof(GrantNextAttackBonusSpec), () => new GrantNextAttackBonusSpec()),
            new EffectSpecInfo("다음 보상 무효화", typeof(NullifyNextRewardSpec), () => new NullifyNextRewardSpec()),
            new EffectSpecInfo("대형 이동", typeof(MoveFormationSpec), () => new MoveFormationSpec())
        };
    }
}
```

그리고 Task 4 Step 1의 `AuthoringContext.cs`를 지금 함께 생성한다 (컴파일 의존).

- [ ] **Step 3: CardSpec / CardSpecMapper 갱신**

`CardSpec.cs`: `public InterventionKind Intervention;` → `public InterventionKeyRef Intervention;` (나머지 동일).

`CardSpecMapper.cs` 전체 교체:
```csharp
using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Card-level assembly only. Effect mapping lives on each EffectSpec subclass
    /// (no central effect switch — AGENTS.md rule 9).</summary>
    public static class CardSpecMapper
    {
        public static CardDefinition ToDefinition(CardSpec spec)
        {
            if (spec.Category == CardCategory.Intervention)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, spec.Type, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = spec.EnergyCost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        spec.Intervention.ToKey(), spec.EnergyCost, spec.InterventionEffectValue)
                };
            }

            var effects = (spec.Effects ?? Array.Empty<EffectSpec>())
                .Select(e => e.ToEffectData())
                .ToArray();
            return new CardDefinition(spec.Id, spec.Name, spec.Side, spec.Type, spec.BaseExecutionOrder, effects)
            {
                EnergyCost = spec.EnergyCost,
                Category = CardCategory.Execution
            };
        }
    }
}
```
(`EffectData` using은 `FateWeaver.Core.Cards`에 있음 — 기존 파일의 using을 따른다. `ToEffectData` 정적 메서드는 삭제되므로 사용처는 Step 5에서 함께 수정.)

- [ ] **Step 4: 손저작 spec 2파일 + GeneratedCards 이관**

`StarterDeckSpecs.cs` — 효과 리터럴만 교체 (카드 필드는 동일). 대표 예 (전체 파일에 같은 패턴 적용):
```csharp
        public static CardSpec Slash() => new CardSpec
        {
            Id = "slash", Name = "베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[] { new DamageSpec { Value = 4 } }
        };

        public static CardSpec Guard() => new CardSpec
        {
            Id = "guard", Name = "막기", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[] { new ApplyStatusSpec {
                Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self } }
        };

        public static CardSpec QuickCut() => new CardSpec
        {
            Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[] { new DamageSpec { Value = 2,
                Condition = new ConditionSpec { Kind = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 } } }
        };

        public static CardSpec PullForward() => new CardSpec
        {
            Id = "pull_forward", Name = "앞당김", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = -1
        };
```
나머지 카드(Counter=PrevExecutedIsEnemyAttack/9, Cover=NextIsEnemyAttack/7 + Block ThisTurn Self, PushBack=+1, SwapPositions=Swap, SlowHex=Slow Turns 2 TargetEnemy, QuickenSelf=Haste Turns 2 Self)도 현재 파일의 값 그대로 같은 패턴으로 옮긴다. `using FateWeaver.Core.Intervention;` 추가.

`PartyPrototypeDeckSpecs.cs` — 같은 패턴: Attack→`DamageSpec{Value=AttackDamage}`, SelectedBlock/AllBlock→`ApplyStatusSpec{Status=StatusKeyRef.Of(StatusKeys.Block), Value=BlockMagnitude, Lifetime=ThisTurn, Target=Self/AllPartyMembers}`, MoveForward→`MoveFormationSpec{Value=MoveForwardDistance}`. `ExecutionSpec` 헬퍼 파라미터 타입은 `EffectSpec`으로.

`GeneratedCards.cs` — 헤더 주석·구조 유지, 리터럴만 새 형식으로 손 이관 (현재 파일의 값 그대로; slash/guard 2장씩, quick_cut, counter_stance, cover, pull_forward, swap_positions, push_back). 예:
```csharp
            new CardSpec { Id = "slash", Name = "베기", Side = Side.Player, Type = CardType.Attack, Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4, InterventionEffectValue = 0, Effects = new EffectSpec[] { new DamageSpec { Value = 4, Selector = TargetSelectorRef.None, Condition = new ConditionSpec { Kind = ConditionKind.None, N = 0, SuccessEffectValue = 0 } } } },
```
개입 카드는 `Intervention = new InterventionKeyRef { Id = "..." }` — Id 문자열은 `InterventionActionKeys` 상수의 실제 값 확인 후 기입 (또는 `InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder)` 사용 — 생성기 출력과 텍스트가 달라도 되며, 의미 등가는 Task 1 테스트가 보증).

- [ ] **Step 5: 테스트 갱신**

`grep -rln "EffectKind\|StatusKindRef\|InterventionKind\|CardSpecMapper.ToEffectData" Assets/Core/Tests` 로 대상 확인 후:
- `CardSpecMapperTests.cs`: spec 리터럴을 새 형식으로 교체. `CardSpecMapper.ToEffectData(x)` 호출은 `x.ToEffectData()`로. 상태 단언은 payload 캐스트로 (Task 2 Step 5 패턴).
- `StarterDeckSpecEquivalenceTests.cs` 51-56행: `counter.Effects.Single().Condition` → `counter.Effects.Single().Condition.Kind` 비교 (`ConditionKind.PrevExecutedIsEnemyAttack`).
- `PartyPrototypeDataTests.cs`: spec 리터럴 교체 (있는 경우).

- [ ] **Step 6: 전체 테스트 — 등가 오라클 포함 통과**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: PASS 전체. 특히 `CardContentEquivalenceTests` 3개가 그대로 통과해야 손 이관이 등가라는 뜻이다.

- [ ] **Step 7: 잔존 enum 검색 (Unity 층 제외)**

Run: `grep -rn "EffectKind\|StatusKindRef\|InterventionKind" Assets/Core --include="*.cs"`
Expected: 결과 없음.

- [ ] **Step 8: Commit**

```bash
git add -A Assets/Core
git commit -m "refactor(authoring): polymorphic effect specs replace closed enums (P0-B)"
```

---

### Task 4: 저작 검증층 (AuthoringContext + Validator + 실패 테스트)

**Files:**
- Create: `Assets/Core/Simulation/Authoring/AuthoringContext.cs` (Task 3에서 이미 생성했다면 확인만)
- Create: `Assets/Core/Simulation/Authoring/AuthoringValidator.cs`
- Create: `Assets/Core/Tests/EditMode/AuthoringValidationTests.cs`

**Interfaces:**
- Produces: `AuthoringContext { bool HasStatus(StatusKey); bool HasEffect(EffectKey); bool HasIntervention(InterventionActionKey); static AuthoringContext Default(); }`,
  `AuthoringValidator.Validate(IEnumerable<CardSpec>, AuthoringContext) : IReadOnlyList<string>` (빈 목록 = 통과)
- Consumes: Task 3의 `EffectSpec.Validate`, `EffectSpecCatalog.All()`; `CombatRegistries`는 internal이므로 `AuthoringContext.Default()`도 Simulation 어셈블리 내부에서 구성

- [ ] **Step 1: AuthoringContext 작성**

```csharp
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Registry lookups for authoring-time validation (editor and boot use the same checks).</summary>
    public sealed class AuthoringContext
    {
        private readonly EffectRegistry _effects;
        private readonly StatusRegistry _statuses;
        private readonly InterventionActionRegistry _interventions;

        public AuthoringContext(
            EffectRegistry effects,
            StatusRegistry statuses,
            InterventionActionRegistry interventions)
        {
            _effects = effects;
            _statuses = statuses;
            _interventions = interventions;
        }

        public static AuthoringContext Default()
            => new AuthoringContext(
                CombatRegistries.Effects(),
                CombatRegistries.Statuses(),
                CombatRegistries.InterventionActions());

        public bool HasStatus(StatusKey key) => _statuses.TryResolve(key, out _);
        public bool HasEffect(EffectKey key) => _effects.Contains(key);
        public bool HasIntervention(InterventionActionKey key) => _interventions.Contains(key);
    }
}
```
`EffectRegistry`/`InterventionActionRegistry`에 `Contains`가 없으면 추가한다 (딕셔너리 `ContainsKey` 위임 한 줄 — `EffectDescriptionRegistry.Contains`와 동형). `AuthoringContext`는 `FateWeaver.Simulation` 네임스페이스의 internal `CombatRegistries`를 쓰므로 `using FateWeaver.Simulation;`이 필요하면 추가.

- [ ] **Step 2: 실패 테스트 먼저 작성** (`AuthoringValidationTests.cs`)

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class AuthoringValidationTests
    {
        private static CardSpec Execution(params EffectSpec[] effects) => new CardSpec
        {
            Id = "t", Name = "t", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = effects
        };

        [Test]
        public void Valid_starter_content_passes()
            => Assert.IsEmpty(AuthoringValidator.Validate(
                StarterDeckSpecs.Build(), AuthoringContext.Default()));

        [Test]
        public void Unknown_status_key_fails()
        {
            var errors = AuthoringValidator.Validate(
                new[] { Execution(new ApplyStatusSpec {
                    Status = new StatusKeyRef { Id = "no_such_status" }, Value = 1,
                    Lifetime = StatusLifetimeKind.ThisTurn }) },
                AuthoringContext.Default());
            Assert.IsTrue(errors.Any(e => e.Contains("no_such_status")));
        }

        [Test]
        public void Empty_status_key_fails()
        {
            var errors = AuthoringValidator.Validate(
                new[] { Execution(new ApplyStatusSpec { Value = 1, Lifetime = StatusLifetimeKind.ThisTurn }) },
                AuthoringContext.Default());
            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Unknown_intervention_key_fails()
        {
            var errors = AuthoringValidator.Validate(
                new[] { new CardSpec {
                    Id = "t", Name = "t", Side = Side.Player, Type = CardType.Skill,
                    Category = CardCategory.Intervention, EnergyCost = 1,
                    Intervention = new InterventionKeyRef { Id = "no_such_action" } } },
                AuthoringContext.Default());
            Assert.IsTrue(errors.Any(e => e.Contains("no_such_action")));
        }

        [Test]
        public void Catalog_specs_all_have_registered_runtime_handlers()
        {
            var context = AuthoringContext.Default();
            foreach (var info in EffectSpecCatalog.All())
            {
                var spec = info.Create();
                Assert.IsTrue(context.HasEffect(spec.Key),
                    info.SpecType.Name + " has no runtime handler for key " + spec.Key);
            }
        }
    }
}
```

- [ ] **Step 3: 실행 — AuthoringValidator 미구현으로 컴파일 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~AuthoringValidationTests`
Expected: FAIL (CS0103: AuthoringValidator 없음)

- [ ] **Step 4: AuthoringValidator 구현**

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Walks authored card specs and collects every validation error (returns them instead of
    /// throwing so the editor can show all problems at once; boot/tests assert the list is empty).</summary>
    public static class AuthoringValidator
    {
        public static IReadOnlyList<string> Validate(
            IEnumerable<CardSpec> specs,
            AuthoringContext context)
        {
            var errors = new List<string>();
            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    errors.Add("Card spec list contains a null entry.");
                    continue;
                }

                if (string.IsNullOrEmpty(spec.Id))
                {
                    errors.Add("Card spec requires an id.");
                }

                if (spec.Category == CardCategory.Intervention)
                {
                    if (spec.Intervention.IsEmpty)
                    {
                        errors.Add("Card '" + spec.Id + "': intervention card requires an action key.");
                    }
                    else if (!context.HasIntervention(spec.Intervention.ToKey()))
                    {
                        errors.Add("Card '" + spec.Id + "': unknown intervention key '" + spec.Intervention.Id + "'.");
                    }

                    continue;
                }

                foreach (var effect in spec.Effects ?? System.Array.Empty<EffectSpec>())
                {
                    if (effect == null)
                    {
                        errors.Add("Card '" + spec.Id + "': effects contain a null entry.");
                        continue;
                    }

                    if (!context.HasEffect(effect.Key))
                    {
                        errors.Add("Card '" + spec.Id + "': no runtime handler for effect key '" + effect.Key + "'.");
                    }

                    foreach (var error in effect.Validate(context))
                    {
                        errors.Add("Card '" + spec.Id + "': " + error);
                    }
                }
            }

            return errors;
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인 후 전체 실행**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: PASS 전체.

- [ ] **Step 6: Commit**

```bash
git add -A Assets/Core
git commit -m "feat(authoring): authoring-time validation walk for card specs (P0-B)"
```

---

### Task 5: 샘플 신규 효과 데모 (완료 기준 증명)

테스트 파일 하나에 Heal 효과의 전체 패키지(핸들러·spec·설명 핸들러)를 정의하고, 중앙 파일 수정 없이 실행·저작·설명·검증 경로가 전부 동작함을 보인다. 이 커밋의 diff에 `Assets/Core/Simulation`·`Assets/Core/Effects` 변경이 없어야 한다는 것 자체가 증명이다.

**Files:**
- Create: `Assets/Core/Tests/EditMode/NewEffectLocalityTests.cs`

**Interfaces:**
- Consumes: `IEffectHandler`, `IEffectDescriptionHandler`, `EffectSpec`/`EffectSpecCatalog`(로컬 확장), `TurnResolver`, `AuthoringContext`(로컬 레지스트리로 구성)

- [ ] **Step 1: 테스트 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests
{
    /// <summary>P0-B 완료 기준: a brand-new effect ships as one package (handler + spec + description
    /// + registration) without touching any central enum/mapper. Everything Heal lives in this file.</summary>
    public class NewEffectLocalityTests
    {
        private static readonly EffectKey HealKey = new EffectKey("heal");

        private sealed class HealHandler : IEffectHandler
        {
            public EffectKey Key => HealKey;

            public void Apply(EffectContext ctx)
            {
                var member = PartyTargeting.LivingById(ctx.State, ctx.Card.OwnerId);
                if (member == null)
                {
                    ctx.Cancel(CardCancellationReason.NoValidTarget);
                    return;
                }

                member.Hp += ctx.EffectValue;
            }
        }

        private sealed class HealSpec : EffectSpec
        {
            public int Value;

            public override EffectKey Key => HealKey;

            public override EffectData ToEffectData() => ApplyCondition(new EffectData(Key, Value));

            public override string ToLiteral()
                => "new HealSpec { Value = " + Value + ", " + ConditionLiteral() + " }";
        }

        private sealed class HealDescriptionHandler : IEffectDescriptionHandler
        {
            public EffectKey Key => HealKey;

            public string Describe(EffectData effect, int effectValue, DescriptionContext context)
                => "치유 " + effectValue;
        }

        [Test]
        public void Heal_spec_maps_and_validates_without_central_changes()
        {
            var spec = new HealSpec { Value = 3 };
            var effect = spec.ToEffectData();
            Assert.AreEqual(HealKey, effect.Key);
            Assert.AreEqual(3, effect.EffectValue);
            Assert.IsEmpty(spec.Validate(AuthoringContext.Default()).ToList());
        }

        [Test]
        public void Extended_catalog_registers_heal_like_any_other_spec()
        {
            var extended = EffectSpecCatalog.All()
                .Concat(new[] { new EffectSpecInfo("치유", typeof(HealSpec), () => new HealSpec()) })
                .ToList();
            Assert.IsTrue(extended.Any(i => i.SpecType == typeof(HealSpec)));
        }

        [Test]
        public void Heal_description_resolves_from_extended_registry()
        {
            var registry = new EffectDescriptionRegistry();
            registry.Register(new HealDescriptionHandler());
            Assert.AreEqual("치유 5",
                registry.Resolve(HealKey).Describe(new EffectData(HealKey, 5), 5, null));
        }
    }
}
```

실행 경로 테스트는 기존 코어 테스트가 `TurnResolver`를 어떻게 구동하는지 확인 후 같은 방식으로 1개 추가한다 (`InterventionActionTests.cs`나 `SlowHasteStatusTests.cs`의 CombatState/TurnResolver 구성 패턴을 그대로 따르되, `EffectRegistry`에 `new HealHandler()`를 추가 등록하고 Heal 카드 1장이 아군 HP를 회복시키는지 단언). 패턴을 찾은 뒤 작성한다 — 임의 발명 금지 (AGENTS.md 규칙 13).

- [ ] **Step 2: 실행 및 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~NewEffectLocalityTests`
Expected: PASS

- [ ] **Step 3: diff 확인 — 중앙 파일 무변경 증명**

Run: `git status --short Assets/Core/Simulation Assets/Core/Effects Assets/Core/Cards`
Expected: 결과 없음 (테스트 파일만 신규).

- [ ] **Step 4: Commit**

```bash
git add Assets/Core/Tests/EditMode/NewEffectLocalityTests.cs
git commit -m "test(authoring): prove new-effect locality with sample heal package (P0-B)"
```

---

### Task 6: Unity 층 — [SerializeReference] + 드로어 + 코드젠

헤드리스로 컴파일되지 않으므로 이 태스크의 검증은 (1) 코드 리뷰 수준의 self-check, (2) 사용자 Unity 확인이다.

**Files:**
- Modify: `Assets/Unity/CardAsset.cs`
- Create: `Assets/Unity/Editor/EffectSpecDrawer.cs`
- Modify: `Assets/Unity/Editor/CardCodeGenerator.cs` (Apply·EmitSpec·EmitEffect·ApplyDefinition)

**Interfaces:**
- Consumes: `EffectSpec`(추상), `EffectSpecCatalog.All()`, `InterventionKeyRef`, `EffectSpec.ToLiteral()`, `AuthoringValidator`/`AuthoringContext`

- [ ] **Step 1: CardAsset 갱신**

```csharp
        [SerializeReference] public EffectSpec[] Effects = Array.Empty<EffectSpec>();
        public InterventionKeyRef Intervention;
```
(기존 `[FormerlySerializedAs("Fate")] public InterventionKind Intervention;` 교체. enum→struct는 자동 마이그레이션되지 않는다 — 시드 재실행으로 해소, Step 4 참고. `ToSpec()`은 그대로 컴파일된다.)

- [ ] **Step 2: 드로어 작성** (`Assets/Unity/Editor/EffectSpecDrawer.cs`)

```csharp
using System.Linq;
using FateWeaver.Simulation.Authoring;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>Dropdown-driven [SerializeReference] picker for EffectSpec. Candidates come from the
    /// explicit EffectSpecCatalog (no reflection scan — AGENTS.md rule 14 준수).</summary>
    [CustomPropertyDrawer(typeof(EffectSpec), useForChildren: true)]
    public sealed class EffectSpecDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var infos = EffectSpecCatalog.All();
            var current = property.managedReferenceValue as EffectSpec;
            var currentIndex = current == null
                ? -1
                : infos.ToList().FindIndex(i => i.SpecType == current.GetType());

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var names = new[] { "(효과 선택)" }.Concat(infos.Select(i => i.DisplayName)).ToArray();
            var picked = EditorGUI.Popup(line, label.text, currentIndex + 1, names) - 1;
            if (picked != currentIndex && picked >= 0)
            {
                property.managedReferenceValue = infos[picked].Create();
            }

            if (property.managedReferenceValue != null)
            {
                var body = new Rect(
                    position.x,
                    position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    position.height - EditorGUIUtility.singleLineHeight);
                EditorGUI.indentLevel++;
                foreach (var child in ChildProperties(property))
                {
                    var h = EditorGUI.GetPropertyHeight(child, includeChildren: true);
                    EditorGUI.PropertyField(new Rect(body.x, body.y, body.width, h), child, includeChildren: true);
                    body.y += h + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.indentLevel--;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue != null)
            {
                foreach (var child in ChildProperties(property))
                {
                    height += EditorGUI.GetPropertyHeight(child, includeChildren: true)
                        + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }

        private static System.Collections.Generic.IEnumerable<SerializedProperty> ChildProperties(
            SerializedProperty property)
        {
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            if (!iterator.NextVisible(enterChildren: true)) yield break;
            while (!SerializedProperty.EqualContents(iterator, end))
            {
                yield return iterator.Copy();
                if (!iterator.NextVisible(enterChildren: false)) yield break;
            }
        }
    }
}
```

- [ ] **Step 3: CardCodeGenerator 갱신**

- `Apply(...)`: `card.Intervention = spec.Intervention;` (타입만 바뀜 — 코드 동일), `card.Effects = spec.Effects ?? System.Array.Empty<EffectSpec>();` 유지.
- `ApplyDefinition(...)`: `card.Intervention = default;` 추가 (enum이 아니므로).
- `EmitSpec`: `sb.Append("Intervention = InterventionKind.")...` 줄을 다음으로 교체:
```csharp
            if (!s.Intervention.IsEmpty)
            {
                sb.Append("Intervention = new InterventionKeyRef { Id = ").Append(Quote(s.Intervention.Id)).Append(" }, ");
            }
```
- `EmitEffect(EffectSpec e)` 본문 전체를 `return e.ToLiteral();` 로 교체.
- `Generate()`에 저작 검증 게이트 추가 (spec 잘못 저장 시 생성 차단):
```csharp
            var specs = deck.ToSpecs();
            var errors = AuthoringValidator.Validate(specs, AuthoringContext.Default());
            if (errors.Count > 0)
            {
                Debug.LogError("Card validation failed:\n" + string.Join("\n", errors));
                return;
            }

            File.WriteAllText(GeneratedPath, Emit(specs), new UTF8Encoding(false));
```

주의: `AuthoringContext.Default()`가 internal `CombatRegistries`를 쓰므로 Unity 어셈블리에서 접근 가능한지 확인 — `CombatRegistries`가 `internal`이면 `AuthoringContext.Default()` 자체는 public이라 문제없다.

- [ ] **Step 4: 사용자 검증 항목 기록 (실행은 사용자)**

헤드리스 전체 테스트가 통과하면 커밋하고, 아래를 사용자에게 요청한다:
1. Unity 에디터에서 컴파일 에러 없음 확인
2. `Fate Weaver/Seed Starter Card Assets` → `Seed Enemy Card Assets` → `Seed Party Prototype Assets` → `Generate Cards from SO` 순서로 재실행
3. `git diff Assets/Core/Simulation/Generated/GeneratedCards.cs` 가 의미 없는 차이(텍스트 순서/형식)만 보이는지 확인 — 헤드리스에서 `CardContentEquivalenceTests`가 재검증
4. CardAsset 인스펙터에서 효과 드롭다운 동작 확인, Play로 전투 화면 정상 구동 확인

- [ ] **Step 5: Commit**

```bash
git add Assets/Unity
git commit -m "feat(unity): SerializeReference effect authoring with catalog drawer (P0-B)"
```

---

### Task 7: 마무리 — 문서·백로그·최종 검증

**Files:**
- Modify: `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md` (§4 상태)
- Create: `docs/superpowers/plans/2026-07-19-p0b-implementation-record.md`

- [ ] **Step 1: 최종 검증**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
grep -rn "EffectKind\|StatusKindRef\|InterventionKind" Assets --include="*.cs"
```
Expected: 테스트 전체 PASS, grep 결과 없음 (Unity 층 포함).

- [ ] **Step 2: 백로그 §4에 상태 줄 추가** (P0-A와 같은 형식)

```markdown
- 상태: **완료 (YYYY-MM-DD)** — 구현 기록: [`2026-07-19-p0b-implementation-record.md`](2026-07-19-p0b-implementation-record.md)
```

- [ ] **Step 3: 구현 기록 문서 작성** — P0-A 기록(`2026-07-18-p0a-rng-unification.md`) 형식을 따라 설계 결정 요약, 변경 파일, 완료 조건 체크리스트(스펙 §7), 사용자 Play 검증 대기 항목을 기록.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/plans
git commit -m "docs(plan): record P0-B completion status"
```
