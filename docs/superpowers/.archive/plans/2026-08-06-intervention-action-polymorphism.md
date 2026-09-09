# 개입 액션 다형화·카드 스펙 분리 구현 계획 (계획 3.5)

- 작성일: 2026-08-06
- 상태: `완료`
- 범위: 개입 액션 파라미터를 저작·런타임 두 층에서 액션별로 분리, `CardSpec`을 실행·개입 두 타입으로
  분리, 개입 카드 JSON 4장 마이그레이션
- 선행: 계획 3b·3c·3d (완료·머지)
- 관련 설계: [카드 변형과 런타임 콘텐츠 로딩](../specs/2026-07-30-card-mutation-and-runtime-content-design.md),
  [열린 카드 저작 구조](../specs/2026-07-19-open-card-authoring-design.md)

## 설계 개요 (사람 검수용)

이 절만 읽고 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은 읽지
않아도 된다.

**무엇을 만드나** — 개입 액션의 파라미터를 저작·런타임 두 층 모두에서 액션별로 분리한다. 지금은
두 층 다 파라미터 칸 셋을 모든 액션이 공유해서 `lock`은 셋 다 안 쓴다. 저작은 카드 타입 분리와
개입 스펙 다형화로, 런타임은 이미 효과가 쓰고 있는 페이로드 패턴으로 푼다.

**구조**

| 객체 | 책임 (한 줄) | 이 객체가 모르는 것 |
|---|---|---|
| `CardSpec` (추상, 수정) | 카드 종류와 무관한 공통 저작 필드를 담는다 | 실행·개입 각각의 고유 필드 |
| `ExecutionCardSpec` / `InterventionCardSpec` | 자기 종류의 고유 저작 필드만 담는다 | 다른 종류의 필드, 판별 방법 |
| `CardSpecJsonConverter` | 카드 분류를 판별자로 카드 스펙을 다형 역직렬화한다 | 필드의 의미, 검증 규칙 |
| `InterventionSpec` (추상) + 구체 3종 | 개입 액션 하나의 저작 파라미터를 소유하고 런타임 페이로드로 옮긴다 | 자기를 담은 카드, 전투 상태 |
| `InterventionSpecCatalog` | 판별자 문자열에서 어느 개입 스펙 타입으로 읽을지 알려준다 | 파라미터의 내용, 런타임 핸들러 |
| `IInterventionPayload` + 구체 페이로드 | 액션 하나를 실행하는 데 필요한 값을 담는다 | 어디서 저작됐는지, 누가 읽는지 |
| `InterventionActionData` (수정) | 모든 개입이 공유하는 키·비용과 페이로드를 운반한다 | 페이로드 안의 내용 |

**의존 방향** — `카드 JSON → CardSpec(구체) → InterventionSpec(구체) → IInterventionPayload → 핸들러`
`InterventionActionData`는 키·비용·페이로드를 나르는 봉투이며 액션별 지식을 갖지 않는다.

**확장 축**
- *갈아끼울 수 있는 것* — 개입 액션 종류. 스펙 1개 + 페이로드 1개 + 핸들러 1개 + 명부 한 줄이면
  끝나고 중앙 switch가 없다. 노트북 폼은 명부에서 파생되므로 노트북 소스를 건드리지 않는다.
- *한번 정하면 고정되는 것* — 카드 한 장이 개입 액션을 **하나만** 갖는다. 카드 분류가 실행·개입
  둘뿐이다. 개입에는 효과와 달리 조건 시스템이 없다.

**대안과 기각 이유**
1. *저작만 다형화하고 런타임은 그대로 두기* — 기각. 저작 표면은 깨끗해지지만 런타임 봉투의 빈 칸이
   그대로 남아 같은 부채를 두 번 갚게 된다.
2. *`InterventionActionData`를 상속으로 다형화* — 기각. `InterventionPlay`·`InterventionPlayResolver`·
   `DeckCombatSession`·`ScenarioDefinition`·`PlaytestSession` 다섯의 시그니처가 전부 바뀐다. 페이로드
   방식은 그 다섯을 무변경으로 두고 핸들러만 고친다.

**이 선택으로 나중에 어려워지는 것**
- 페이로드는 빈 마커 인터페이스라 컴파일러가 "이 액션엔 이 페이로드" 짝을 검사하지 않는다. 잘못
  배선된 개입은 캐스팅 실패로 런타임에 드러난다. 효과 쪽이 이미 지고 있는 부채를 같은 모양으로 하나
  더 지는 것이다.
- 카드 스펙이 타입으로 갈리므로 실행·개입 **양쪽 성격을 가진 카드**는 이 구조를 다시 열어야 한다.
- 개입 카드 JSON 4장과 시나리오 생성 6곳이 함께 움직여야 해서, 태스크 중간에 멈추면 트리가 깨진다.
  태스크 경계를 그에 맞춰 잡았고 각 태스크 끝은 항상 초록이다.
- 이미 머지된 [노트북 JSON 코어 계획](2026-08-05-notebook-json-core.md)이 평평한 개입 4필드를
  전제한다. 그 계획의 스키마·모델·테스트 절이 이 작업으로 무효가 된다 (Task 5에서 인계 메모를 남긴다).

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 개입 액션의 파라미터를 저작·런타임 양쪽에서 액션별로 분리해, 액션이 늘어도 공용 클래스가
자라지 않게 한다.

**Architecture:** 효과 쪽이 이미 쓰는 두 패턴을 개입에 그대로 복제한다 — 저작은
`EffectSpec`/`EffectSpecCatalog`/`EffectSpecJsonConverter` 삼각형, 런타임은 `IEffectPayload`.
카드 스펙은 `category`를 판별자로 하는 다형 역직렬화로 실행·개입 두 타입으로 갈린다. 런타임 봉투
`InterventionActionData`는 키·비용만 공개해 중간 운반자 다섯을 무변경으로 남긴다.

**Tech Stack:** C# (netstandard, `FateWeaver.Core`), Newtonsoft.Json, NUnit,
`dotnet test` (헤드리스) + Unity EditMode.

## Global Constraints

- **헤드리스 테스트 명령** — `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`.
  `-p:TargetFramework=net5.0`은 생략 불가다(로컬 SDK가 .NET 5뿐).
- **기준선** — 착수 시점 헤드리스 511/511 통과. Unity EditMode 659 total / 652 passed / 0 failed / 7 skipped.
  각 태스크 종료 시 이 수치 이상이어야 하고, 줄었다면 이유를 커밋 메시지에 적는다.
- **`FateWeaver.Core`는 UnityEngine을 참조하지 않는다** (규칙 6). 이 계획이 만드는 파일은 전부 코어다.
- **중앙 switch를 키우지 않는다** (규칙 9). 단 하나의 예외가 Task 1이 만드는 `CardSpecMapper.ToPayload`이며,
  **Task 4가 반드시 제거한다.** Task 4 없이 멈추면 규칙 위반 상태로 남는다.
- **페이로드는 불변 `record`로 만든다** (규칙 7의 결정론). 저작 스펙은 가변 public 필드다 — 두 세계의
  차이이며 의도된 것이다.
- **튜닝 수치를 하드코딩하지 않는다** (규칙 8). 이 계획은 값을 옮기기만 하고 새 상수를 만들지 않는다.
- **커밋 메시지는 한국어** (규칙 27). 형식 `타입(범위): 한국어 제목`, 제목은 "…한다"로 끝낸다.
- **새 `.cs` 파일에는 `.meta`가 필요하다.** Unity EditMode를 한 번 돌리면 생성되므로, 커밋 전
  `git status`로 확인해 `.cs`와 `.meta`를 1:1로 함께 스테이징한다 (규칙 16·17).
- **작업 위치** — 전용 워크트리에서만 작업한다 (규칙 15). 메인 체크아웃의 브랜치를 바꾸지 않는다.

---

### Task 1: 런타임 페이로드 전환

런타임 봉투에서 액션별 필드 셋을 걷어내고 페이로드로 옮긴다. JSON은 건드리지 않으므로 이 태스크가
끝나도 저장소의 카드 파일은 그대로다.

**Files:**
- Create: `Assets/Core/Intervention/IInterventionPayload.cs`
- Create: `Assets/Core/Intervention/ChangeExecutionOrderPayload.cs`
- Create: `Assets/Core/Intervention/SwapExecutionOrderPayload.cs`
- Modify: `Assets/Core/Intervention/InterventionActionData.cs`
- Modify: `Assets/Core/Intervention/ChangeExecutionOrderHandler.cs`
- Modify: `Assets/Core/Intervention/SwapExecutionOrderHandler.cs`
- Modify: `Assets/Core/Simulation/Descriptions/BuiltInInterventionDescriptionHandlers.cs`
- Modify: `Assets/Core/Authoring/CardSpecMapper.cs`
- Modify: `Assets/Core/Simulation/SampleScenarios.cs:63,106,175`
- Modify: `Assets/Core/Simulation/SampleMultiTurnScenarios.cs:112,218,245`
- Test: `Assets/Core/Tests/EditMode/InterventionActionTests.cs`
- Test: `Assets/Core/Tests/EditMode/InterventionPlayResolverTests.cs`
- Test: `Assets/Core/Tests/EditMode/InterventionConstraintTests.cs`

**Interfaces:**
- Produces: `IInterventionPayload` (마커 인터페이스);
  `ChangeExecutionOrderPayload(int Delta, Side? TargetSide)`;
  `SwapExecutionOrderPayload(Side? TargetSide, bool RequireAdjacent)`;
  `InterventionActionData(InterventionActionKey key, int interventionCost)` 및
  `InterventionActionData(InterventionActionKey key, int interventionCost, IInterventionPayload payload)`;
  `InterventionActionData.Payload` (타입 `IInterventionPayload`, 파라미터 없는 액션은 `null`).
- Consumes: 없음 (첫 태스크).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/InterventionActionTests.cs`의 `ChangeExecutionOrder_spends_cost_and_changes_target_executionOrder`
바로 위에 다음 두 테스트를 추가한다. 기존 `Card`·`EffectRegistry` 헬퍼를 그대로 쓴다.

```csharp
        [Test]
        public void ChangeExecutionOrder_reads_delta_from_payload()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var card = Card("quick_cut", Side.Player, 4, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(
                InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1,
                new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null));
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            new ChangeExecutionOrderHandler().Apply(ctx);

            Assert.AreEqual(2, card.ExecutionOrder);
            Assert.AreEqual(2, state.FateEnergy);
        }

        [Test]
        public void Lock_needs_no_payload()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var card = Card("quick_cut", Side.Player, 4, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(InterventionActionKeys.Lock, interventionCost: 1);
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            new LockHandler().Apply(ctx);

            Assert.IsTrue(card.IsLocked);
            Assert.IsNull(action.Payload);
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 컴파일 실패. `ChangeExecutionOrderPayload`, `IInterventionPayload`를 찾을 수 없고
`InterventionActionData`에 2인자 생성자가 없다는 오류.

- [ ] **Step 3: 페이로드 타입 셋을 만든다**

`Assets/Core/Intervention/IInterventionPayload.cs`:

```csharp
namespace FateWeaver.Core.Intervention
{
    /// <summary>액션 종류별 파라미터 블록. 이것이 있어서 InterventionActionData는 액션이 늘어도
    /// 필드가 자라지 않는다(AGENTS.md 규칙 9). 효과 쪽 IEffectPayload와 같은 형태이며, 같은
    /// 이유로 비어 있다 — 공통으로 꺼낼 것이 없다.</summary>
    public interface IInterventionPayload
    {
    }
}
```

`Assets/Core/Intervention/ChangeExecutionOrderPayload.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Intervention
{
    /// <summary>실행 순서 변경의 파라미터. Delta는 대상의 ExecutionOrder에 더할 값이고,
    /// TargetSide가 null이 아니면 그 진영의 레일 카드만 대상이 된다(재촉=Player, 유예=Enemy).</summary>
    public sealed record ChangeExecutionOrderPayload(int Delta, Side? TargetSide) : IInterventionPayload;
}
```

`Assets/Core/Intervention/SwapExecutionOrderPayload.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Intervention
{
    /// <summary>실행 순서 교환의 파라미터. RequireAdjacent가 true면 두 대상이 해결 순서상 서로
    /// 인접해야 한다(엇갈림).</summary>
    public sealed record SwapExecutionOrderPayload(Side? TargetSide, bool RequireAdjacent) : IInterventionPayload;
}
```

- [ ] **Step 4: 봉투를 갈아끼운다**

`Assets/Core/Intervention/InterventionActionData.cs` 전문을 다음으로 교체한다.

```csharp
namespace FateWeaver.Core.Intervention
{
    /// <summary>개입 한 건의 런타임 데이터. 모든 액션이 공유하는 것(핸들러를 찾을 키, 차감할 비용)만
    /// 직접 들고, 액션별 파라미터는 Payload에 실어 나른다. 카드에서 핸들러까지 이 봉투를 넘기는
    /// InterventionPlay·InterventionPlayResolver·DeckCombatSession·ScenarioDefinition·
    /// PlaytestSession은 Payload를 열지 않는다 — 여는 것은 자기가 무슨 액션인지 아는 핸들러뿐이다.</summary>
    public sealed class InterventionActionData
    {
        public InterventionActionKey Key { get; }
        public int InterventionCost { get; }

        /// <summary>액션별 파라미터. 파라미터가 없는 액션(lock)은 null이다.</summary>
        public IInterventionPayload Payload { get; }

        public InterventionActionData(InterventionActionKey key, int interventionCost)
            : this(key, interventionCost, null)
        {
        }

        public InterventionActionData(
            InterventionActionKey key,
            int interventionCost,
            IInterventionPayload payload)
        {
            Key = key;
            InterventionCost = interventionCost;
            Payload = payload;
        }
    }
}
```

- [ ] **Step 5: 핸들러 둘을 페이로드에서 읽게 한다**

`LockHandler`는 셋 중 아무것도 읽지 않으므로 **수정하지 않는다.**

`Assets/Core/Intervention/ChangeExecutionOrderHandler.cs` 전문:

```csharp
namespace FateWeaver.Core.Intervention
{
    public sealed class ChangeExecutionOrderHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;

        public TargetingRequirement Targeting => TargetingRequirement.RailCards(1);

        public bool CanApply(InterventionPlayContext ctx)
        {
            var payload = PayloadOf(ctx);
            return payload != null
                && ctx.Target != null
                && !ctx.Target.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost
                && (payload.TargetSide == null || ctx.Target.Def.Side == payload.TargetSide);
        }

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.InterventionCost;
            ctx.FateEnergySpent = ctx.Intervention.InterventionCost;
            ctx.Target.ExecutionOrder += PayloadOf(ctx).Delta;
        }

        /// <summary>봉투가 이 핸들러의 것이고 페이로드 타입까지 맞을 때만 값을 준다. 잘못 배선된
        /// 개입은 예외가 아니라 CanApply 실패로 떨어진다 — 기존 방어 순서를 그대로 유지한다.</summary>
        private ChangeExecutionOrderPayload PayloadOf(InterventionPlayContext ctx)
            => ctx != null && ctx.State != null && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                    ? ctx.Intervention.Payload as ChangeExecutionOrderPayload
                    : null;
    }
}
```

`Assets/Core/Intervention/SwapExecutionOrderHandler.cs` 전문:

```csharp
namespace FateWeaver.Core.Intervention
{
    public sealed class SwapExecutionOrderHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public TargetingRequirement Targeting => TargetingRequirement.RailCards(2);

        public bool CanApply(InterventionPlayContext ctx)
        {
            var payload = PayloadOf(ctx);
            return payload != null
                && ctx.Target != null
                && ctx.SecondaryTarget != null
                && !ctx.Target.IsLocked
                && !ctx.SecondaryTarget.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost
                && (payload.TargetSide == null
                    || (ctx.Target.Def.Side == payload.TargetSide
                        && ctx.SecondaryTarget.Def.Side == payload.TargetSide))
                && AreAdjacentIfRequired(ctx, payload);
        }

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.InterventionCost;
            ctx.FateEnergySpent = ctx.Intervention.InterventionCost;

            var executionOrder = ctx.Target.ExecutionOrder;
            ctx.Target.ExecutionOrder = ctx.SecondaryTarget.ExecutionOrder;
            ctx.SecondaryTarget.ExecutionOrder = executionOrder;
        }

        private SwapExecutionOrderPayload PayloadOf(InterventionPlayContext ctx)
            => ctx != null && ctx.State != null && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                    ? ctx.Intervention.Payload as SwapExecutionOrderPayload
                    : null;

        private static bool AreAdjacentIfRequired(
            InterventionPlayContext ctx, SwapExecutionOrderPayload payload)
        {
            if (!payload.RequireAdjacent)
            {
                return true;
            }

            var order = ctx.State.Zone.ResolutionOrder();
            var first = IndexOf(order, ctx.Target);
            var second = IndexOf(order, ctx.SecondaryTarget);
            return first >= 0 && second >= 0 && (first - second == 1 || second - first == 1);
        }

        private static int IndexOf(
            System.Collections.Generic.IReadOnlyList<Combat.ExecutionCardInstance> order,
            Combat.ExecutionCardInstance card)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (ReferenceEquals(order[i], card))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
```

- [ ] **Step 6: 설명 핸들러를 페이로드에서 읽게 한다**

`Assets/Core/Simulation/Descriptions/BuiltInInterventionDescriptionHandlers.cs`의
`ChangeExecutionOrderDescriptionHandler.Describe`만 교체한다. 나머지 둘은 파라미터를 읽지 않으므로 그대로 둔다.

```csharp
        public string Describe(InterventionActionData action, DescriptionContext context)
        {
            var delta = (action.Payload as ChangeExecutionOrderPayload)?.Delta ?? 0;
            return "한 카드의 실행 순서 " + (delta >= 0 ? "+" + delta : delta.ToString());
        }
```

- [ ] **Step 7: 매퍼에 임시 다리를 놓는다**

`Assets/Core/Authoring/CardSpecMapper.cs`의 개입 분기에서 봉투 생성을 바꾸고 헬퍼를 더한다.
**이 헬퍼는 Task 4가 지운다** — 규칙 9가 금지하는 중앙 switch이므로 남겨두면 안 된다.

```csharp
                    InterventionAction = new InterventionActionData(
                        spec.Intervention.ToKey(), spec.EnergyCost, ToPayload(spec))
```

같은 클래스에 다음을 추가한다.

```csharp
        /// <summary>계획 3.5 Task 1의 임시 다리. 저작이 아직 평평해서 키를 보고 페이로드를 만든다.
        /// Task 4가 InterventionSpec.ToPayload()로 옮기며 이 메서드를 제거한다 — 그때까지만 존재하는
        /// 규칙 9 예외다.</summary>
        private static IInterventionPayload ToPayload(CardSpec spec)
        {
            var key = spec.Intervention.ToKey();
            if (key == InterventionActionKeys.ChangeExecutionOrder)
            {
                return new ChangeExecutionOrderPayload(
                    spec.InterventionEffectValue, ToTargetSide(spec.InterventionTargetSide));
            }

            if (key == InterventionActionKeys.SwapExecutionOrder)
            {
                return new SwapExecutionOrderPayload(
                    ToTargetSide(spec.InterventionTargetSide), spec.InterventionRequireAdjacent);
            }

            return null;
        }
```

- [ ] **Step 8: 시나리오 생성 6곳을 고친다**

옛 인자를 새 페이로드로 바꾼다. 좌변이 파일에 있는 정확한 문자열이다.

| 파일:행 | 옛 인자 | 새 인자 |
|---|---|---|
| `SampleScenarios.cs:63` | `InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, effectValue: 0` | `InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false)` |
| `SampleScenarios.cs:106` | 같음 | 같음 |
| `SampleScenarios.cs:175-176` | `InterventionActionKeys.ChangeExecutionOrder,\n                            interventionCost: 1, effectValue: 2` | `InterventionActionKeys.ChangeExecutionOrder,\n                            interventionCost: 1, new ChangeExecutionOrderPayload(Delta: 2, TargetSide: null)` |
| `SampleMultiTurnScenarios.cs:112` | `InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: 3` | `InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: 3, TargetSide: null)` |
| `SampleMultiTurnScenarios.cs:218` | `InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2` | `InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null)` |
| `SampleMultiTurnScenarios.cs:245` | 같음 | 같음 |

- [ ] **Step 9: 기존 테스트 생성 지점을 고친다**

같은 방식으로 치환한다.

| 파일:행 | 새 인자 |
|---|---|
| `InterventionActionTests.cs:35,50,85,182` | `InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null)` |
| `InterventionActionTests.cs:106,142,200` | `InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false)` |
| `InterventionActionTests.cs:166` | `InterventionActionKeys.Lock, interventionCost: 1` |
| `InterventionPlayResolverTests.cs:33,53,54,96` | `InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null)` |
| `InterventionPlayResolverTests.cs:34` | `InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: 1, TargetSide: null)` |
| `InterventionPlayResolverTests.cs:76,98` | `InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false)` |
| `InterventionPlayResolverTests.cs:97` | `InterventionActionKeys.Lock, interventionCost: 1` |
| `InterventionConstraintTests.cs:80` | `InterventionActionKeys.SwapExecutionOrder, 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false)` |

`InterventionConstraintTests.cs:34-36`을 다음으로 바꾼다.

```csharp
            var action = new InterventionActionData(
                InterventionActionKeys.ChangeExecutionOrder, 1,
                new ChangeExecutionOrderPayload(Delta: -1, TargetSide: Side.Player));
```

`InterventionConstraintTests.cs:56-58`을 다음으로 바꾼다.

```csharp
            var action = new InterventionActionData(
                InterventionActionKeys.SwapExecutionOrder, 1,
                new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: true));
```

- [ ] **Step 10: 테스트가 통과하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 513/513 통과 (기준선 511 + 이번에 추가한 2개). 실패가 있으면 다음 태스크로 넘어가지 않는다.

- [ ] **Step 11: 커밋한다**

```bash
git add Assets/Core/Intervention Assets/Core/Authoring/CardSpecMapper.cs Assets/Core/Simulation Assets/Core/Tests/EditMode
```

```bash
git commit -m "refactor(core): 개입 런타임 파라미터를 액션별 페이로드로 옮긴다"
```

---

### Task 2: 저작 개입 스펙과 명부

저작 쪽 다형 구조를 만든다. 이 태스크가 만드는 것은 아직 아무도 쓰지 않는다 — 순수 추가이며 JSON도
로더도 건드리지 않는다. Task 4가 배선한다.

**Files:**
- Create: `Assets/Core/Authoring/InterventionSpec.cs`
- Create: `Assets/Core/Authoring/Specs/ChangeExecutionOrderSpec.cs`
- Create: `Assets/Core/Authoring/Specs/SwapExecutionOrderSpec.cs`
- Create: `Assets/Core/Authoring/Specs/LockSpec.cs`
- Create: `Assets/Core/Authoring/InterventionSpecCatalog.cs`
- Test: `Assets/Core/Tests/EditMode/InterventionSpecCatalogTests.cs` (신규)

**Interfaces:**
- Consumes: Task 1의 `IInterventionPayload`, `ChangeExecutionOrderPayload`, `SwapExecutionOrderPayload`.
- Produces: `abstract class InterventionSpec`
  — `abstract InterventionActionKey Key { get; }`,
  `abstract IInterventionPayload ToPayload()`,
  `virtual IEnumerable<string> Validate(AuthoringContext context)`;
  `ChangeExecutionOrderSpec { public int Delta; public InterventionTargetSideRef TargetSide; }`;
  `SwapExecutionOrderSpec { public InterventionTargetSideRef TargetSide; public bool RequireAdjacent; }`;
  `LockSpec {}` (필드 없음);
  `InterventionSpecInfo(string DisplayName, Type SpecType, Func<InterventionSpec> Create)`;
  `InterventionSpecCatalog.All()` → `IReadOnlyList<InterventionSpecInfo>`.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/InterventionSpecCatalogTests.cs`를 새로 만든다.

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    /// <summary>저작 명부와 런타임 레지스트리가 같은 키 집합을 덮는지, 각 스펙이 자기 페이로드를
    /// 만드는지 잠근다. 둘이 어긋나면 저작은 되는데 실행 핸들러가 없는 카드가 생긴다.</summary>
    public class InterventionSpecCatalogTests
    {
        [Test]
        public void Every_authored_spec_has_a_registered_runtime_handler()
        {
            var context = AuthoringContext.Default();

            foreach (var info in InterventionSpecCatalog.All())
            {
                Assert.IsTrue(
                    context.HasIntervention(info.Create().Key),
                    "저작 명부의 '" + info.DisplayName + "'에 런타임 핸들러가 없다.");
            }
        }

        [Test]
        public void Catalog_has_no_duplicate_keys()
        {
            var ids = InterventionSpecCatalog.All().Select(i => i.Create().Key.Id).ToList();

            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void Change_execution_order_spec_builds_its_payload()
        {
            var spec = new ChangeExecutionOrderSpec
            {
                Delta = -2,
                TargetSide = InterventionTargetSideRef.Player
            };

            var payload = (ChangeExecutionOrderPayload)spec.ToPayload();

            Assert.AreEqual(-2, payload.Delta);
            Assert.AreEqual(Side.Player, payload.TargetSide);
        }

        [Test]
        public void Swap_execution_order_spec_builds_its_payload()
        {
            var spec = new SwapExecutionOrderSpec { RequireAdjacent = true };

            var payload = (SwapExecutionOrderPayload)spec.ToPayload();

            Assert.IsTrue(payload.RequireAdjacent);
            Assert.IsNull(payload.TargetSide);
        }

        [Test]
        public void Lock_spec_has_no_payload()
        {
            Assert.IsNull(new LockSpec().ToPayload());
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 컴파일 실패. `InterventionSpecCatalog`·`ChangeExecutionOrderSpec` 등을 찾을 수 없다.

- [ ] **Step 3: 추상 스펙을 만든다**

`Assets/Core/Authoring/InterventionSpec.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>저작된 개입 액션 하나. 각 구체 스펙이 자기 파라미터(실타입)와 런타임 페이로드로의
    /// 변환, 검증을 소유한다 — 액션을 더해도 중앙 enum/switch가 자라지 않는다(AGENTS.md 규칙 9).
    /// InterventionSpecCatalog에 명시적으로 등록한다. EffectSpec과 같은 형태다.</summary>
    [Serializable]
    public abstract class InterventionSpec
    {
        [JsonIgnore]
        public abstract InterventionActionKey Key { get; }

        /// <summary>런타임 파라미터로 옮긴다. 파라미터가 없는 액션은 null을 돌려준다.</summary>
        public abstract IInterventionPayload ToPayload();

        public virtual IEnumerable<string> Validate(AuthoringContext context)
        {
            yield break;
        }

        /// <summary>저작 열거형을 코어의 진영으로 옮긴다. Any는 "제한 없음"이라 null이다.</summary>
        protected static Side? ToTargetSide(InterventionTargetSideRef side)
        {
            switch (side)
            {
                case InterventionTargetSideRef.Player: return Side.Player;
                case InterventionTargetSideRef.Enemy: return Side.Enemy;
                default: return null;
            }
        }
    }
}
```

- [ ] **Step 4: 구체 스펙 셋을 만든다**

`Assets/Core/Authoring/Specs/ChangeExecutionOrderSpec.cs`:

```csharp
using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 하나의 실행 순서를 Delta만큼 옮긴다. TargetSide로 진영을 제한한다
    /// (재촉=Player, 유예=Enemy, Any=제한 없음).</summary>
    [Serializable]
    public sealed class ChangeExecutionOrderSpec : InterventionSpec
    {
        public int Delta;
        public InterventionTargetSideRef TargetSide;

        public override InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new ChangeExecutionOrderPayload(Delta, ToTargetSide(TargetSide));
    }
}
```

`Assets/Core/Authoring/Specs/SwapExecutionOrderSpec.cs`:

```csharp
using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 두 장의 실행 순서를 맞바꾼다. RequireAdjacent가 true면 둘이 해결
    /// 순서상 인접해야 한다(엇갈림).</summary>
    [Serializable]
    public sealed class SwapExecutionOrderSpec : InterventionSpec
    {
        public InterventionTargetSideRef TargetSide;
        public bool RequireAdjacent;

        public override InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new SwapExecutionOrderPayload(ToTargetSide(TargetSide), RequireAdjacent);
    }
}
```

`Assets/Core/Authoring/Specs/LockSpec.cs`:

```csharp
using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드를 고정해 이후 개입을 거부하게 한다. 파라미터가 없으므로 페이로드도
    /// 없다 — 이 계획 이전에는 쓰지 않는 칸 셋을 들고 있었다.</summary>
    [Serializable]
    public sealed class LockSpec : InterventionSpec
    {
        public override InterventionActionKey Key => InterventionActionKeys.Lock;

        public override IInterventionPayload ToPayload() => null;
    }
}
```

- [ ] **Step 5: 명부를 만든다**

`Assets/Core/Authoring/InterventionSpecCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring
{
    /// <summary>저작 가능한 개입 스펙 하나의 메타데이터: 표시 이름, 구체 타입, 무인자 팩토리.</summary>
    public sealed class InterventionSpecInfo
    {
        public InterventionSpecInfo(string displayName, Type specType, Func<InterventionSpec> create)
        {
            DisplayName = displayName;
            SpecType = specType;
            Create = create;
        }

        public string DisplayName { get; }
        public Type SpecType { get; }
        public Func<InterventionSpec> Create { get; }
    }

    /// <summary>저작 가능한 개입 스펙의 명시적 목록. JSON 컨버터가 판별자 표를 만들 때와 노트북
    /// 저작 스키마 생성기가 폼을 만들 때 읽는다 — 스펙/핸들러 클래스를 쓰는 것 외에 필요한 유일한
    /// 등록 절차다(AGENTS.md 규칙 9). EffectSpecCatalog와 같은 형태다.
    /// 런타임에서 "실행 가능한 것"을 답하는 InterventionActionRegistry와 짝이며, 둘이 어긋나면
    /// AuthoringValidator가 부팅에서 잡는다.</summary>
    public static class InterventionSpecCatalog
    {
        public static IReadOnlyList<InterventionSpecInfo> All() => new[]
        {
            new InterventionSpecInfo("실행 순서 변경", typeof(ChangeExecutionOrderSpec), () => new ChangeExecutionOrderSpec()),
            new InterventionSpecInfo("실행 순서 교환", typeof(SwapExecutionOrderSpec), () => new SwapExecutionOrderSpec()),
            new InterventionSpecInfo("고정", typeof(LockSpec), () => new LockSpec())
        };
    }
}
```

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 518/518 통과 (Task 1의 513 + 이번 5개).

- [ ] **Step 7: 커밋한다**

```bash
git add Assets/Core/Authoring Assets/Core/Tests/EditMode/InterventionSpecCatalogTests.cs
```

```bash
git commit -m "feat(core): 개입 저작 스펙과 명부를 추가한다"
```

---

### Task 3: 카드 스펙 타입 분리

`CardSpec`을 추상 기반과 두 파생으로 쪼개고 `category`를 판별자로 다형 역직렬화한다. **개입 필드는
아직 평평하게 둔다** — JSON이 바뀌지 않으므로 이 태스크의 diff에 콘텐츠 파일이 없다.

**Files:**
- Modify: `Assets/Core/Authoring/CardSpec.cs`
- Create: `Assets/Core/Authoring/Json/CardSpecJsonConverter.cs`
- Modify: `Assets/Core/Authoring/Json/ContentJson.cs`
- Modify: `Assets/Core/Authoring/CardSpecMapper.cs`
- Modify: `Assets/Core/Authoring/AuthoringValidator.cs`
- Test: `Assets/Core/Tests/EditMode/CardContentJsonTests.cs`
- Test: `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`
- Test: `Assets/Core/Tests/EditMode/AuthoringValidationTests.cs`
- Test: `Assets/Core/Tests/EditMode/CardSpecGradeTagTests.cs`
- Test: `Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs`
- Test: `Assets/Core/Tests/EditMode/SlowHasteStatusTests.cs`

**Interfaces:**
- Consumes: 없음 (Task 1·2와 독립).
- Produces: `abstract class CardSpec` (필드 `Id`·`Name`·`Side`·`Category`·`EnergyCost`·`Grade`·`Tags`);
  `sealed class ExecutionCardSpec : CardSpec` (`BaseExecutionOrder`·`Effects`);
  `sealed class InterventionCardSpec : CardSpec`
  (`Intervention`·`InterventionEffectValue`·`InterventionTargetSide`·`InterventionRequireAdjacent`);
  `CardSpecJsonConverter` (ContentJson에 등록됨).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/CardContentJsonTests.cs` 맨 아래 클래스 안에 다음을 추가한다. 첫 번째가
타입 분리를, 두 번째가 **키 순서 보존**을 잠근다 — 기반 클래스 필드가 앞으로 나오면서 `grade`·`tags`가
`effects` 앞으로 튀어나가는 것이 이 태스크의 가장 큰 함정이다.

```csharp
        [Test]
        public void Category_picks_the_concrete_card_spec_type()
        {
            var execution = ContentJson.Read<CardSpec>(
                "{\"id\":\"a\",\"name\":\"a\",\"side\":\"Player\",\"category\":\"Execution\"}");
            var intervention = ContentJson.Read<CardSpec>(
                "{\"id\":\"b\",\"name\":\"b\",\"side\":\"Player\",\"category\":\"Intervention\"}");

            Assert.IsInstanceOf<ExecutionCardSpec>(execution);
            Assert.IsInstanceOf<InterventionCardSpec>(intervention);
        }

        [Test]
        public void Execution_card_rejects_intervention_keys()
        {
            Assert.Throws<JsonSerializationException>(() => ContentJson.Read<CardSpec>(
                "{\"id\":\"a\",\"name\":\"a\",\"side\":\"Player\",\"category\":\"Execution\","
                + "\"intervention\":\"lock\"}"));
        }

        [Test]
        public void Repository_cards_round_trip_byte_identically()
        {
            var directory = System.IO.Path.Combine(TestContent.Root(), "Cards");

            foreach (var path in System.IO.Directory.GetFiles(directory, "*.json"))
            {
                var original = System.IO.File.ReadAllText(path);
                var rewritten = ContentJson.Write(ContentJson.Read<CardSpec>(original));

                Assert.AreEqual(
                    Normalize(original), Normalize(rewritten),
                    System.IO.Path.GetFileName(path) + "의 왕복이 원본과 다르다.");
            }
        }

        /// <summary>줄바꿈과 파일 끝 공백만 맞춘다. 키 순서·들여쓰기·값은 그대로 비교한다 —
        /// 그것이 이 테스트가 잠그려는 것이기 때문이다.</summary>
        private static string Normalize(string json)
            => json.Replace("\r\n", "\n").TrimEnd();
```

파일 맨 위 `using`에 `Newtonsoft.Json;`이 없으면 추가한다.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 컴파일 실패. `ExecutionCardSpec`·`InterventionCardSpec`을 찾을 수 없다.

- [ ] **Step 3: 카드 스펙을 쪼갠다**

`Assets/Core/Authoring/CardSpec.cs` 전문을 교체한다. **`Grade`·`Tags`의 `Order`가 핵심이다** —
Newtonsoft는 기반 클래스 멤버를 파생보다 먼저 쓰므로, 이것이 없으면 `grade`·`tags`가
`baseExecutionOrder`·`effects` 앞으로 나와 기존 카드 26장의 diff가 전부 뒤집힌다.

```csharp
using FateWeaver.Core.Cards;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>카드 종류와 무관한 공통 저작 필드. 실행·개입 각각의 고유 필드는 파생 클래스가
    /// 가지므로, 개입 카드가 실행 순서를·실행 카드가 개입 키를 드는 오저작을 타입이 막는다
    /// (ContentJson의 MissingMemberHandling.Error가 부팅에서 거부한다).</summary>
    public abstract class CardSpec
    {
        public string Id;
        public string Name;

        // Player/Execution은 각 enum의 0번째(기본) 값이라 DefaultValueHandling.Ignore가 지운다.
        // CardContentLoader가 "side"·"category" 키의 존재 자체로 무결성을 검증하므로
        // (생략 시 조용히 Player/Execution이 되는 사고 방지), 여기서는 항상 써야 한다.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public Side Side;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public CardCategory Category;

        public int EnergyCost;

        /// <summary>카드 풀 후보 구성용 등급. None은 등급 개념이 없는 카드(fixture 등)의 정상
        /// 상태이므로 Side·Category와 달리 Include 처방을 쓰지 않는다 — 생략이 곧 None이라
        /// 정보 손실이 없다.
        /// Order는 키 순서를 위한 것이다: Newtonsoft가 기반 클래스 멤버를 파생보다 먼저 쓰므로,
        /// 이것이 없으면 등급·태그가 파생 필드 앞으로 나와 기존 카드 JSON 26장이 전부 재정렬된다.</summary>
        [JsonProperty(Order = 100)]
        public CardGrade Grade;

        /// <summary>저작 분류 태그. 풀 소속 카드는 하나 이상 가져야 한다(PoolContentLoader).</summary>
        [JsonProperty(Order = 101)]
        public string[] Tags;
    }

    /// <summary>실행 카드의 저작 데이터. 레일에 올라 효과를 순서대로 발동한다.</summary>
    public sealed class ExecutionCardSpec : CardSpec
    {
        public int BaseExecutionOrder;
        public EffectSpec[] Effects;
    }

    /// <summary>개입 카드의 저작 데이터. 레일 위 카드를 조작하며 효과 목록을 갖지 않는다.</summary>
    public sealed class InterventionCardSpec : CardSpec
    {
        public InterventionKeyRef Intervention;
        public int InterventionEffectValue;
        public InterventionTargetSideRef InterventionTargetSide;
        public bool InterventionRequireAdjacent;
    }

    /// <summary>개입 대상 진영 제한. Any=제한 없음, Player=재촉류, Enemy=유예류.</summary>
    public enum InterventionTargetSideRef { Any, Player, Enemy }
}
```

- [ ] **Step 4: 카드 컨버터를 만든다**

`Assets/Core/Authoring/Json/CardSpecJsonConverter.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>CardSpec의 다형 (역)직렬화. 판별자는 카드 분류이며 스펙의 실제 필드이기도 하므로,
    /// EffectSpecJsonConverter와 달리 읽기 전에 떼어내거나 쓰기 후에 되붙일 필요가 없다.
    /// CardContentLoader의 RequiredKeys가 "category"를 필수로 강제하므로 판별자는 항상 존재한다.</summary>
    public sealed class CardSpecJsonConverter : JsonConverter<CardSpec>
    {
        public const string CategoryProperty = "category";

        private static readonly Dictionary<string, Func<CardSpec>> FactoryByCategory =
            new Dictionary<string, Func<CardSpec>>(StringComparer.Ordinal)
            {
                { CardCategory.Execution.ToString(), () => new ExecutionCardSpec() },
                { CardCategory.Intervention.ToString(), () => new InterventionCardSpec() }
            };

        public override CardSpec ReadJson(
            JsonReader reader, Type objectType, CardSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var category = (string)entry[CategoryProperty];
            if (string.IsNullOrEmpty(category))
            {
                throw new JsonSerializationException(
                    "Card entry requires a '" + CategoryProperty + "' property.");
            }

            if (!FactoryByCategory.TryGetValue(category, out var create))
            {
                throw new JsonSerializationException("Unknown card category '" + category + "'.");
            }

            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Nested.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, CardSpec value, JsonSerializer serializer)
            => JObject.FromObject(value, ContentJson.Nested).WriteTo(writer);
    }
}
```

**`Plain`이 아니라 `Nested`인 이유** — 카드는 다른 다형 타입(`EffectSpec[]`, 나중에
`InterventionSpec`)을 품는 유일한 스펙이다. `Plain`으로 populate하면 효과 배열이 추상 타입을
만들지 못해 터지고, 반대로 전체 `Settings`를 쓰면 `JObject.FromObject`가 이 컨버터를 다시 불러
무한 재귀에 빠진다. `Nested`는 **자기만 뺀** 설정이라 둘 다 피한다.

- [ ] **Step 5: 컨버터를 등록하고 `Nested` 설정을 만든다**

`Assets/Core/Authoring/Json/ContentJson.cs`에서 `Plain` 아래에 `Nested`를 추가하고 `Build`의
시그니처와 다형 블록을 바꾼다.

```csharp
        /// <summary>카드 컨버터만 뺀 설정. CardSpecJsonConverter가 중첩된 EffectSpec을 다형으로
        /// 다루면서도 자기 자신을 재귀 호출하지 않기 위해 쓴다. 외부에서 직접 쓰지 않는다.</summary>
        internal static JsonSerializer Nested { get; } =
            JsonSerializer.Create(Build(includePolymorphic: true, includeCardSpec: false));
```

```csharp
        private static JsonSerializerSettings Build(
            bool includePolymorphic, bool includeCardSpec = true)
        {
```

```csharp
            if (includePolymorphic)
            {
                if (includeCardSpec)
                {
                    settings.Converters.Add(new CardSpecJsonConverter());
                }

                settings.Converters.Add(new EffectSpecJsonConverter());
                settings.Converters.Add(new StatusSpecJsonConverter());
            }
```

`Plain`의 기존 정의(`Build(includePolymorphic: false)`)는 그대로 둔다 — `includeCardSpec`의 기본값이
`true`지만 다형 블록 자체를 건너뛰므로 영향이 없다.

- [ ] **Step 6: 매퍼와 검증기를 타입으로 분기시킨다**

`Assets/Core/Authoring/CardSpecMapper.cs`의 `ToDefinition` 전문을 교체한다. `ToPayload`와
`ToTargetSide` 헬퍼는 그대로 두되 인자 타입만 `InterventionCardSpec`으로 바꾼다.

```csharp
        public static CardDefinition ToDefinition(CardSpec spec)
        {
            if (spec is InterventionCardSpec intervention)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = spec.EnergyCost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        intervention.Intervention.ToKey(), spec.EnergyCost, ToPayload(intervention))
                };
            }

            var execution = (ExecutionCardSpec)spec;
            var effects = (execution.Effects ?? Array.Empty<EffectSpec>())
                .Select(e => e.ToEffectData())
                .ToArray();
            return new CardDefinition(
                spec.Id, spec.Name, spec.Side, execution.BaseExecutionOrder, effects)
            {
                EnergyCost = spec.EnergyCost,
                Category = CardCategory.Execution
            };
        }
```

`Assets/Core/Authoring/AuthoringValidator.cs`에서 `if (spec.Category == CardCategory.Intervention)`
블록을 다음으로 바꾼다. 이후 효과 순회는 `execution.Effects`를 쓰도록 고친다.

```csharp
                if (spec is InterventionCardSpec intervention)
                {
                    if (intervention.Intervention.IsEmpty)
                    {
                        errors.Add("Card '" + spec.Id + "': intervention card requires an action key.");
                    }
                    else if (!context.HasIntervention(intervention.Intervention.ToKey()))
                    {
                        errors.Add("Card '" + spec.Id + "': unknown intervention key '"
                            + intervention.Intervention.Id + "'.");
                    }

                    continue;
                }

                var execution = (ExecutionCardSpec)spec;
                foreach (var effect in execution.Effects ?? System.Array.Empty<EffectSpec>())
```

- [ ] **Step 7: 테스트 픽스처를 구체 타입으로 바꾼다**

`new CardSpec` 생성 지점을 전부 구체 타입으로 치환한다. `Category = CardCategory.Execution`인 것은
`ExecutionCardSpec`, `Intervention`인 것은 `InterventionCardSpec`이다.

| 파일:행 | 바꿀 것 |
|---|---|
| `AuthoringValidationTests.cs:11` | `new CardSpec` → `new ExecutionCardSpec` |
| `AuthoringValidationTests.cs:61` | `new CardSpec` → `new ExecutionCardSpec` |
| `AuthoringValidationTests.cs:82` | `new CardSpec {` → `new InterventionCardSpec {` |
| `CardSpecGradeTagTests.cs:12` | `new CardSpec` → `new ExecutionCardSpec`, 반환 타입도 `ExecutionCardSpec` |
| `CardSpecMapperTests.cs:32,50,67` | `new CardSpec` → `new ExecutionCardSpec` |
| `CardSpecMapperTests.cs:91` | `new CardSpec` → `new InterventionCardSpec` |
| `CardContentJsonTests.cs:17,34,100,135` | `new CardSpec` → `new ExecutionCardSpec` |
| `CardContentJsonTests.cs:159` | `new CardSpec` → `new ExecutionCardSpec` |
| `DeckPoolCharacterLoaderTests.cs:24,27` | `Dictionary<string, CardSpec>`은 그대로, `new CardSpec` → `new ExecutionCardSpec` |
| `SlowHasteStatusTests.cs:148` | `new CardSpec` → `new ExecutionCardSpec`, 반환 타입도 `ExecutionCardSpec` |

`CardSpecGradeTagTests.cs:24,37,49`의 `ContentJson.Read<CardSpec>`은 그대로 둔다 — 추상 타입으로
읽어 구체가 나오는 것이 이번 변경의 요점이다.

- [ ] **Step 8: 테스트가 통과하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 521/521 통과 (Task 2의 518 + 이번 3개).
`Repository_cards_round_trip_byte_identically`가 실패하면 Step 3의 `Order` 지정을 먼저 의심한다 —
실패 메시지가 어느 파일에서 어긋났는지 알려준다.

- [ ] **Step 9: 저장소 파일이 안 변했는지 확인한다**

```bash
git status --short Assets/StreamingAssets/Content
```

Expected: 출력 없음. 이 태스크는 콘텐츠를 건드리지 않는다.

- [ ] **Step 10: 커밋한다**

```bash
git add Assets/Core/Authoring Assets/Core/Tests/EditMode
```

```bash
git commit -m "refactor(core): 카드 저작 스펙을 실행·개입 두 타입으로 나눈다"
```

---

### Task 4: 개입 저작을 중첩 스펙으로 바꾸고 카드 JSON을 옮긴다

`InterventionCardSpec`의 평평한 네 필드를 `InterventionSpec` 하나로 접고, 카드 JSON 4장을 새 모양으로
옮긴다. Task 1이 놓은 임시 다리를 여기서 제거한다.

**Files:**
- Create: `Assets/Core/Authoring/Json/InterventionSpecJsonConverter.cs`
- Modify: `Assets/Core/Authoring/CardSpec.cs`
- Modify: `Assets/Core/Authoring/Json/ContentJson.cs`
- Modify: `Assets/Core/Authoring/CardSpecMapper.cs`
- Modify: `Assets/Core/Authoring/AuthoringValidator.cs`
- Modify: `Assets/StreamingAssets/Content/Cards/breather.json`
- Modify: `Assets/StreamingAssets/Content/Cards/hasten.json`
- Modify: `Assets/StreamingAssets/Content/Cards/delay.json`
- Modify: `Assets/StreamingAssets/Content/Cards/crossover.json`
- Test: `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`
- Test: `Assets/Core/Tests/EditMode/AuthoringValidationTests.cs`
- Test: `Assets/Core/Tests/EditMode/CardContentJsonTests.cs`

**Interfaces:**
- Consumes: Task 2의 `InterventionSpec`·`InterventionSpecCatalog`, Task 3의 `InterventionCardSpec`.
- Produces: `InterventionCardSpec.Intervention` (타입 `InterventionSpec`, 이전의 `InterventionKeyRef` 대체);
  `InterventionSpecJsonConverter` (판별자 `"kind"`).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/CardContentJsonTests.cs`에 추가한다.

```csharp
        [Test]
        public void Intervention_kind_picks_the_concrete_intervention_spec()
        {
            var spec = (InterventionCardSpec)ContentJson.Read<CardSpec>(
                "{\"id\":\"d\",\"name\":\"d\",\"side\":\"Player\",\"category\":\"Intervention\","
                + "\"energyCost\":1,\"intervention\":{\"kind\":\"change_execution_order\","
                + "\"delta\":1,\"targetSide\":\"Enemy\"}}");

            var change = (ChangeExecutionOrderSpec)spec.Intervention;
            Assert.AreEqual(1, change.Delta);
            Assert.AreEqual(InterventionTargetSideRef.Enemy, change.TargetSide);
        }

        [Test]
        public void Swap_spec_rejects_a_parameter_it_does_not_own()
        {
            Assert.Throws<JsonSerializationException>(() => ContentJson.Read<CardSpec>(
                "{\"id\":\"c\",\"name\":\"c\",\"side\":\"Player\",\"category\":\"Intervention\","
                + "\"energyCost\":1,\"intervention\":{\"kind\":\"swap_execution_order\","
                + "\"delta\":1}}"));
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 컴파일 실패 또는 캐스팅 실패 — `InterventionCardSpec.Intervention`이 아직 `InterventionKeyRef`다.

- [ ] **Step 3: 개입 스펙 컨버터를 만든다**

`Assets/Core/Authoring/Json/InterventionSpecJsonConverter.cs`:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>InterventionSpec의 다형 (역)직렬화. 판별자는 스펙이 이미 갖고 있는
    /// InterventionActionKey.Id이고 타입 표는 InterventionSpecCatalog에서 만든다 — 리플렉션 스캔
    /// 없음(AGENTS.md 규칙 9). EffectSpecJsonConverter와 같은 형태다.</summary>
    public sealed class InterventionSpecJsonConverter : JsonConverter<InterventionSpec>
    {
        public const string KindProperty = "kind";

        private static readonly Dictionary<string, Func<InterventionSpec>> FactoryByKind = BuildFactories();
        private static readonly Dictionary<Type, string> KindByType = BuildKinds();

        public override InterventionSpec ReadJson(
            JsonReader reader, Type objectType, InterventionSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var kind = (string)entry[KindProperty];
            if (string.IsNullOrEmpty(kind))
            {
                throw new JsonSerializationException(
                    "Intervention entry requires a '" + KindProperty + "' property.");
            }

            if (!FactoryByKind.TryGetValue(kind, out var create))
            {
                throw new JsonSerializationException("Unknown intervention kind '" + kind + "'.");
            }

            entry.Remove(KindProperty);
            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Plain.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, InterventionSpec value, JsonSerializer serializer)
        {
            if (!KindByType.TryGetValue(value.GetType(), out var kind))
            {
                throw new JsonSerializationException(
                    "Intervention spec type '" + value.GetType().Name
                    + "' is not registered in InterventionSpecCatalog.");
            }

            var entry = JObject.FromObject(value, ContentJson.Plain);
            entry.AddFirst(new JProperty(KindProperty, kind));
            entry.WriteTo(writer);
        }

        private static Dictionary<string, Func<InterventionSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<InterventionSpec>>();
            foreach (var info in InterventionSpecCatalog.All())
            {
                var kind = info.Create().Key.Id;
                if (table.ContainsKey(kind))
                {
                    throw new InvalidOperationException(
                        "Duplicate intervention spec kind '" + kind + "' in InterventionSpecCatalog.");
                }

                table.Add(kind, info.Create);
            }

            return table;
        }

        private static Dictionary<Type, string> BuildKinds()
        {
            var table = new Dictionary<Type, string>();
            foreach (var info in InterventionSpecCatalog.All())
            {
                table[info.SpecType] = info.Create().Key.Id;
            }

            return table;
        }
    }
}
```

- [ ] **Step 4: 컨버터를 등록한다**

`ContentJson.Build`의 다형 블록에서 **`includeCardSpec` 분기 바깥**에 한 줄을 더한다. 조건 밖에
두어야 `Settings`와 `Nested` 양쪽에 들어가고, 그래야 카드 컨버터가 중첩된 개입 스펙을 다형으로
읽고 쓸 수 있다.

```csharp
            if (includePolymorphic)
            {
                if (includeCardSpec)
                {
                    settings.Converters.Add(new CardSpecJsonConverter());
                }

                settings.Converters.Add(new EffectSpecJsonConverter());
                settings.Converters.Add(new StatusSpecJsonConverter());
                settings.Converters.Add(new InterventionSpecJsonConverter());
            }
```

- [ ] **Step 5: 개입 카드 스펙을 중첩으로 접는다**

`Assets/Core/Authoring/CardSpec.cs`의 `InterventionCardSpec`을 다음으로 교체한다.

```csharp
    /// <summary>개입 카드의 저작 데이터. 액션별 파라미터는 InterventionSpec이 소유하므로 이 클래스는
    /// 액션이 늘어도 자라지 않는다 — 계획 3.5 이전에는 lock 카드가 쓰지 않는 칸 셋을 들고 있었다.</summary>
    public sealed class InterventionCardSpec : CardSpec
    {
        public InterventionSpec Intervention;
    }
```

`InterventionTargetSideRef` 열거형은 그대로 둔다 — 구체 스펙들이 계속 쓴다.

- [ ] **Step 6: 매퍼에서 임시 다리를 제거한다**

`Assets/Core/Authoring/CardSpecMapper.cs`에서 **`ToPayload`와 `ToTargetSide` 두 헬퍼를 통째로
삭제하고** 개입 분기를 다음으로 바꾼다. `FateWeaver.Core.Intervention` using이 안 쓰이면 함께 지운다.

```csharp
            if (spec is InterventionCardSpec intervention)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = spec.EnergyCost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        intervention.Intervention.Key,
                        spec.EnergyCost,
                        intervention.Intervention.ToPayload())
                };
            }
```

- [ ] **Step 7: 검증기를 새 모양에 맞춘다**

`AuthoringValidator.cs`의 개입 블록을 다음으로 바꾼다. 키가 스펙 자신에게서 나오므로 "빈 키"는
"스펙 없음"이 되고, 스펙 자신의 `Validate`도 여기서 돌린다.

```csharp
                if (spec is InterventionCardSpec intervention)
                {
                    if (intervention.Intervention == null)
                    {
                        errors.Add("Card '" + spec.Id + "': intervention card requires an action key.");
                        continue;
                    }

                    if (!context.HasIntervention(intervention.Intervention.Key))
                    {
                        errors.Add("Card '" + spec.Id + "': unknown intervention key '"
                            + intervention.Intervention.Key.Id + "'.");
                        continue;
                    }

                    foreach (var error in intervention.Intervention.Validate(context))
                    {
                        errors.Add("Card '" + spec.Id + "': " + error);
                    }

                    continue;
                }
```

- [ ] **Step 8: 카드 JSON 4장을 옮긴다**

`Assets/StreamingAssets/Content/Cards/breather.json`:

```json
{
  "id": "breather",
  "name": "숨 고르기",
  "side": "Player",
  "category": "Intervention",
  "energyCost": 1,
  "intervention": {
    "kind": "change_execution_order",
    "delta": 1,
    "targetSide": "Player"
  },
  "grade": "Common",
  "tags": [
    "시작",
    "실행력"
  ]
}
```

`Assets/StreamingAssets/Content/Cards/hasten.json`:

```json
{
  "id": "hasten",
  "name": "재촉",
  "side": "Player",
  "category": "Intervention",
  "energyCost": 1,
  "intervention": {
    "kind": "change_execution_order",
    "delta": -1,
    "targetSide": "Player"
  },
  "grade": "Common",
  "tags": [
    "시작",
    "실행력"
  ]
}
```

`Assets/StreamingAssets/Content/Cards/delay.json`:

```json
{
  "id": "delay",
  "name": "유예",
  "side": "Player",
  "category": "Intervention",
  "energyCost": 1,
  "intervention": {
    "kind": "change_execution_order",
    "delta": 1,
    "targetSide": "Enemy"
  },
  "grade": "Common",
  "tags": [
    "시작",
    "실행력"
  ]
}
```

`crossover.json`:

```json
{
  "id": "crossover",
  "name": "엇갈림",
  "side": "Player",
  "category": "Intervention",
  "energyCost": 1,
  "intervention": {
    "kind": "swap_execution_order",
    "requireAdjacent": true
  },
  "grade": "Common",
  "tags": [
    "시작",
    "실행력"
  ]
}
```

`crossover`에 `targetSide`가 없는 것은 값이 `Any`(열거형 0번)라 `DefaultValueHandling.Ignore`가
지우기 때문이다. `Repository_cards_round_trip_byte_identically`가 이 생략까지 검사한다.

- [ ] **Step 9: 테스트 픽스처를 새 모양으로 바꾼다**

`CardSpecMapperTests.cs:91-97`의 개입 카드 생성을 다음으로 바꾼다.

```csharp
            var def = CardSpecMapper.ToDefinition(new InterventionCardSpec
            {
                Id = "pull_forward", Name = "앞당김", Side = Side.Player,
                Category = CardCategory.Intervention, EnergyCost = 1,
                Intervention = new ChangeExecutionOrderSpec { Delta = -2 }
            });
```

`AuthoringValidationTests.cs:82-85`의 미등록 키 테스트는 **삭제하고** 다음으로 대체한다. 스펙 타입이
명부에서 나오므로 "등록되지 않은 키"를 저작으로 만들 수 없고, 이제 그 자리를 컨버터가 막는다.

```csharp
        [Test]
        public void Unknown_intervention_kind_is_rejected_while_reading()
        {
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(
                () => ContentJson.Read<CardSpec>(
                    "{\"id\":\"t\",\"name\":\"t\",\"side\":\"Player\",\"category\":\"Intervention\","
                    + "\"intervention\":{\"kind\":\"no_such_action\"}}"));
        }

        [Test]
        public void Intervention_card_without_an_action_fails()
        {
            var errors = AuthoringValidator.Validate(
                new[] { new InterventionCardSpec {
                    Id = "t", Name = "t", Side = Side.Player,
                    Category = CardCategory.Intervention, EnergyCost = 1 } },
                AuthoringContext.Default());

            Assert.IsTrue(errors.Any(e => e.Contains("requires an action key")));
        }
```

파일 맨 위 `using`에 `FateWeaver.Core.Authoring.Json;`을 추가한다.

- [ ] **Step 10: 테스트가 통과하는지 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 524/524 통과. `Repository_cards_round_trip_byte_identically`가 카드 26장을 모두 통과해야 한다.

- [ ] **Step 11: 임시 다리가 사라졌는지 확인한다**

```bash
/usr/bin/grep -n "ToPayload\|ToTargetSide" Assets/Core/Authoring/CardSpecMapper.cs
```

Expected: 출력 없음. 남아 있으면 Step 6이 덜 끝난 것이며, 규칙 9 위반 상태로 커밋하면 안 된다.

- [ ] **Step 12: 커밋한다**

```bash
git add Assets/Core/Authoring Assets/Core/Tests/EditMode Assets/StreamingAssets/Content/Cards
```

```bash
git commit -m "refactor(core): 개입 저작을 액션별 스펙으로 바꾸고 카드 넷을 옮긴다"
```

---

### Task 5: Unity 회귀 확인과 문서 갱신

**Files:**
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/plans/2026-08-05-notebook-json-core.md`
- Modify: `Assets/Core/Authoring/InterventionKeyRef.cs` (주석만)

- [ ] **Step 1: Unity EditMode 회귀를 확인한다**

코어만 바뀌었지만 `[SerializeReference]`를 쓰는 에셋이 저작 타입을 참조할 수 있으므로 반드시 돌린다
(규칙 17, 그리고 색인의 "함정 1"). 이 워크트리 경로로 실행하고 로그는 `/private/tmp`에 남긴다.

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /private/tmp/3-5-editmode.xml -logFile /private/tmp/3-5-editmode.log
```

`-quit`를 함께 쓰지 않는다 — 테스트 없이 exit 0이 된다.
Expected: 기준선(659 total / 652 passed / 0 failed / 7 skipped) 대비 failed 0. 이번 계획이 추가한
테스트만큼 total이 는다. Unity가 새 `.cs`의 `.meta`를 생성하므로 실행 후 `git status`로 확인한다.

- [ ] **Step 2: 남은 `.meta`를 스테이징한다**

```bash
git status --short Assets
```

`.cs`와 1:1로 대응하는 `.meta`만 추가한다. 폰트 아틀라스 같은 실행 부산물은 스테이징하지 않는다.

- [ ] **Step 3: 낡은 주석을 고친다**

`Assets/Core/Authoring/InterventionKeyRef.cs`의 doc 주석은 "uniform {key, value} params today;
promote to polymorphic specs (like EffectSpec) only when an action needs unique parameters"라고
적혀 있다. 이번 계획이 그 승격을 끝냈으므로 다음으로 바꾼다.

```csharp
    /// <summary>Serializable reference to an open-set intervention action key. 카드 저작에서는
    /// 계획 3.5가 InterventionSpec 다형화로 대체했다 — 지금은 키만 필요한 자리에서 쓴다.</summary>
```

- [ ] **Step 4: 노트북 계획에 인계 메모를 남긴다**

`docs/superpowers/plans/2026-08-05-notebook-json-core.md` 머리말 바로 아래에 다음 절을 넣는다.

```markdown
> **2026-08-06 계획 3.5로 무효가 된 부분** — 이 계획은 개입을 평평한 네 필드
> (`intervention`·`interventionEffectValue`·`interventionTargetSide`·`interventionRequireAdjacent`)로
> 전제한다. 계획 3.5가 그것을 `{"kind": …, …}` 중첩 스펙으로 바꿨으므로 다음이 갱신되어야 한다:
> 스키마의 `cardFields`와 `BuildInterventions`, 노트북 카드 모델의 개입 네 필드, 개입 카드 테스트.
> 스키마의 `interventions`는 이제 `InterventionSpecCatalog`에서 `{kind, label, fields[]}`로 뽑으며,
> 그러면 노트북 개입 폼이 효과 행 렌더러를 그대로 재사용할 수 있다.
```

- [ ] **Step 5: 색인을 갱신한다**

`docs/superpowers/README.md`에서 세 곳을 고친다. 계획을 추가할 때 색인 행 둘은 이미 만들어 뒀으므로
여기서는 상태만 옮긴다.

카드 콘텐츠 흐름 표의 3.5 행 상태를 `계획 작성` → `**완료**`로 바꾼다.

```markdown
| 3.5 | [개입 액션 다형화·카드 스펙 분리](plans/2026-08-06-intervention-action-polymorphism.md) | **완료** |
```

`## 활성 계획과 로드맵` 표에서 이 계획의 행을 **삭제한다** — 끝난 계획은 활성 목록에 두지 않는다
(규칙 20). 계획 문서 머리말의 상태도 `active` → `완료`로 바꾸고, 규칙 20에 따라 문서를
`docs/superpowers/archive/plans/`로 옮긴 뒤 위 링크 둘의 경로를 `archive/plans/`로 고친다.

계획 3.5를 설명하는 문단("계획 3.5는 개입 액션을 …")을 결과 서술로 바꾼다.

```markdown
계획 3.5는 개입 액션을 `EffectSpec`처럼 다형화하고 `CardSpec`을 실행/개입으로 쪼갰다. 저작은
`InterventionSpec` + `InterventionSpecCatalog` + 컨버터, 런타임은 `IInterventionPayload`이며
(효과의 `IEffectPayload`와 같은 형태), `lock` 카드가 들고 있던 빈 칸 넷이 사라졌다. 카드 한 장은
개입 액션을 하나만 갖는다 — 복수 개입은 대상 묶기 규칙과 비용 귀속 설계가 딸려 오므로 범위 밖이다.
```

- [ ] **Step 6: 헤드리스를 마지막으로 한 번 더 돌린다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: 524/524 통과.

- [ ] **Step 7: 커밋한다**

```bash
git add docs Assets/Core/Authoring/InterventionKeyRef.cs
```

```bash
git commit -m "docs: 개입 다형화 계획을 색인에 반영하고 노트북 계획에 인계를 남긴다"
```

---

## 검수 기준

1. 저장소의 카드 26장을 읽어 그대로 내보내면 바이트가 같다
   (`Repository_cards_round_trip_byte_identically`).
2. 실행 카드에 개입 키를 손으로 넣으면 부팅이 거부한다. 개입 카드에 `effects`를 넣어도 같다.
3. `swap_execution_order`에 `delta`를 적으면 부팅이 거부한다 — 지금은 조용히 무시된다.
4. `CardSpecMapper`에 개입 종류를 보는 분기가 없다.
5. `lock` 액션의 저작 스펙과 런타임 페이로드에 필드가 하나도 없다.
6. 헤드리스 524/524, Unity EditMode failed 0.

## 범위 밖

| 제외 | 이유 |
|---|---|
| 카드 한 장에 개입 액션 여럿 | 대상 묶기 규칙(카드당 `TargetingRequirement` 하나)과 비용 귀속(핸들러마다 차감) 재설계가 딸려 온다. 이번 작업이 그 문을 더 닫지는 않는다 — 액션이 자기 페이로드를 갖게 되는 것이 리스트화의 전제 조건이다 |
| `lock` 개입 카드 JSON 신설 | 액션은 등록됐지만 대응 카드가 없다. 카드 디자인 판단이 필요하다 |
| 적 카드의 JSON 전환 | `GoblinDeck`·`WardenDeck`은 여전히 순수 C#이고, 옮기려면 적 정책·행동 패턴 설계가 필요하다 |
| 노트북 구현 갱신 | 별도 세션이 진행 중이다. Task 5가 인계 메모만 남긴다 |
