# 상태 콘텐츠 JSON화와 카드 저작 표면 축소 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-08-02
- 상태: `archived` — 완료. 2026-08-03 master 머지. 헤드리스 446/446, Unity EditMode 520/520
  519/520 — 남은 1건은 progress.md 참고)
- 권위 문서: [`specs/2026-07-30-card-mutation-and-runtime-content-design.md`](../specs/2026-07-30-card-mutation-and-runtime-content-design.md)
- 선행 계획: [`2026-07-31-card-content-json-loading.md`](2026-07-31-card-content-json-loading.md) (완료)
- 관련 계획: [`../../plans/2026-07-30-status-rule-and-debuffs.md`](../../plans/2026-07-30-status-rule-and-debuffs.md) (`active`)
- 브랜치: `claude/card-mutation-runtime-content-a65c58`

**Goal:** 상태 이상의 규칙을 JSON 콘텐츠로 옮기고, 카드가 상태에 대해 적는 것을 숫자 하나(`count`)로
줄인다. 쓰이지 않는 레거시 카드 10장과 상태 1종을 제거한다.

**Architecture:** 상태가 자기 세기와 수명 종류를 소유하고, 카드는 "몇"만 기여한다. `count`의 뜻은
상태의 `lifetime`이 결정한다 — `Permanent`·`ThisTurn`이면 세기, `Turns`·`UntilConsumed`면 지속.
상태 규칙은 전투당 하나이며(전역), 캐릭터별 규칙은 없다. behavior 클래스는 코드에 남고 키로
등록되며(규칙 9), 데이터로 나가는 것은 파라미터뿐이다.

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), NUnit, Newtonsoft.Json,
`FateWeaver.Core`(UnityEngine 미참조)

## Global Constraints

- 헤드리스 테스트 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- 착수 시점 기준선: **432 tests, 0 failed** / Unity EditMode **506, 0 failed** / 카드 JSON **36장**
- `FateWeaver.Core`에서 `UnityEngine`을 참조하지 않는다 (규칙 6)
- 결정론: 무작위는 시드 RNG 경유, 반복 순서는 사전 구현·파일 시스템 순서에 의존하지 않는다 (규칙 7)
- 튜닝 수치를 계산식에 박지 않는다 (규칙 8)
- 새 상태·효과 = 클래스 1개 + 명시적 카탈로그 등록. 리플렉션 자동 등록 금지 (규칙 9)
- 카드·상태 설명을 하드코딩하지 않는다. 설명 레지스트리가 생성한다 (규칙 10)
- 새 Unity 에셋은 1:1 `.meta`와 함께 커밋한다 (규칙 16)
- 워킹 트리를 깨끗이 남긴다 (규칙 18)
- 문서 색인을 같은 커밋에서 갱신한다 (규칙 20)
- C# 9 한계: `record struct` 금지, 기본 인터페이스 구현 금지, 파일 범위 네임스페이스 금지
- Unity 배치에서 `-runTests`와 `-quit`를 **함께 쓰지 않는다** (테스트 없이 exit 0이 된다)

## 확정된 결정

### 무엇이 어디에 사는가

```
상태 JSON  ← 세기(배율·성장치·순서 변화량) + 수명 종류
카드 JSON  ← count 하나. 그 뜻은 상태가 정한다
```

| 상태 | 키 | `lifetime` | 상태가 가진 세기 | 카드의 `count` |
|---|---|---|---|---|
| 독 | `poison` | `Permanent` | `growthPerTurn: 1` | 독 수치 |
| 방어 | `block` | `ThisTurn` | — | 흡수량 |
| 취약 | `vulnerable` | `Turns` | `multiplierPercent: 150` | 지속 턴 |
| 약화 | `weak` | `Turns` | `multiplierPercent: 75` | 지속 턴 |
| 손상 | `damaged` | `Turns` | `multiplierPercent: 75` | 지속 턴 |
| 둔화 | `slow` | `Turns` | `executionOrderDelta: 2` | 지속 턴 |
| 가속 | `haste` | `Turns` | `executionOrderDelta: -2` | 지속 턴 |
| 전염 | `contagion` | `Turns` | — | 지속 턴 |
| 독 잠복 | `poison_dormant` | `ThisTurn` | — | 없음 |
| 독 안정 | `poison_stasis` | `ThisTurn` | — | 없음 |
| 보상 무효 | `reward_nullified` | `UntilConsumed` | — | 소진 횟수 |

`count`의 해석은 `lifetime`에서 **파생**한다. 별도 필드를 두지 않는다.

- `Permanent` / `ThisTurn` → `count`는 `Magnitude`
- `Turns` / `UntilConsumed` → `count`는 `Lifetime.Count`

### 유지·제거

| 대상 | 처분 | 근거 |
|---|---|---|
| `vulnerable`·`weak`·`damaged` | **유지** | `status-rule-and-debuffs` 계획이 `active`이고 Task 1~4가 구현돼 있다. 카드가 없는 건 저작 단계까지 안 갔기 때문 |
| `slow`·`haste` | **유지** | 유일한 카드가 폐기 대상이라 카드 0장이 되지만 실행 순서 조작은 이 게임의 핵심 축이다 |
| `reward_nullified` | **유지** | `TurnResolver.ResolveTier`의 판정 경로에 엮여 있다 |
| `stun` | **제거** | 어떤 코드도 걸지 않는다 |
| `ThisTurn` 수명 종류 | **유지** | `Turns`+1과 동작은 같지만 상태 파일에서 "이번 턴만"이 더 명확하다 |
| 레거시 카드 10장 | **제거** | 배틀씬·`StarterDeck.asset`·`StarterPool.asset` 어디에도 없다 |

### 모딩 경계

모드는 **등록된 상태의 파라미터만** 조정한다. 새 상태 키는 behavior 코드 로딩을 뜻하므로 허용하지
않는다 — 카드 효과 키에 대해 설계 §4.8이 내린 결정과 같다.

## 파일 구조

| 경로 | 책임 |
|---|---|
| `Assets/Core/Authoring/Statuses/StatusSpec.cs` | 상태 저작 스펙 기반 클래스 (`Key`, `Lifetime`) |
| `Assets/Core/Authoring/Statuses/Specs/*.cs` | 파라미터를 갖는 상태의 스펙 3종 |
| `Assets/Core/Authoring/Statuses/StatusSpecCatalog.cs` | 명시적 등록 목록 (규칙 9) |
| `Assets/Core/Authoring/Statuses/StatusContentLoader.cs` | 상태 소스 → `StatusContentCatalog` 또는 오류 목록 |
| `Assets/Core/Authoring/Statuses/StatusContentCatalog.cs` | `StatusRuleSet` + 수명 종류 조회 |
| `Assets/Core/Authoring/Statuses/StatusContentDefaults.cs` | 상태 기본값의 **단일 출처**. 내보내기와 헤드리스 폴백이 둘 다 여기서 읽는다 |
| `Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs` | 다형 컨버터. 판별자는 `key` |
| `Assets/StreamingAssets/Content/Statuses/*.json` | 상태 11개 |
| `Assets/Core/Tests/EditMode/StatusContentTests.cs` | 왕복·로더·검증 테스트 |

---

## Task 1: 레거시 카드 10장을 폐기한다

`StarterDeckSpecs`가 가진 10개 팩터리(`Slash`·`Guard`·`QuickCut`·`Counter`·`Cover`·`PullForward`·
`PushBack`·`SwapPositions`·`SlowHex`·`QuickenSelf`)는 `Build()`가 고르지 않고, 배틀씬도
`StarterDeck.asset`도 `StarterPool.asset`도 참조하지 않는다. 이들을 참조하는 것은 테스트 5곳과 SO
에셋 8개뿐이다.

**Files:**
- Modify: `Assets/Core/Authoring/StarterDeckSpecs.cs` (10개 팩터리와 `AllAuthored()` 삭제)
- Modify: `Assets/Unity/Editor/CardContentExporter.cs` (`AllAuthored()` → `Build()`)
- Modify: `Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs` (같은 변경)
- Modify: `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs`
- Modify: `Assets/Core/Tests/EditMode/SlowHasteStatusTests.cs`
- Delete: `Assets/StreamingAssets/Content/Cards/{slash,guard,quick_cut,counter_stance,cover,pull_forward,push_back,swap_positions,slow_hex,quicken_self}.json` (+ `.meta`)
- Delete: `Assets/Unity/CardSO/Player/{slash,guard,quick_cut,counter_stance,cover,pull_forward,push_back}.asset` (+ `.meta`)

**Interfaces:**
- Produces: `StarterDeckSpecs.Build()` 만 남는다 (10장, 전부 `StarterPoolSpecs` 위임)

- [ ] **Step 1: 기준선을 기록한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 432`

- [ ] **Step 2: 규칙을 검사하는 테스트를 인라인 픽스처로 옮긴다**

`StarterDeckSpecEquivalenceTests`의 세 테스트는 규칙을 검사하고 폐기 대상 카드를 픽스처로만 쓴다.
카드에서 떼어내면 "어떤 카드가 출시되는지"에 의존하지 않게 되어 오히려 견고해진다.

`Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs`에 픽스처 헬퍼를 더한다.

```csharp
        private static CardSpec Fixture(string id, int cost, int order, params EffectSpec[] effects)
            => new CardSpec
            {
                Id = id,
                Name = id,
                Side = Side.Player,
                Category = CardCategory.Execution,
                EnergyCost = cost,
                BaseExecutionOrder = order,
                Effects = effects
            };
```

세 테스트의 카드 출처를 바꾼다. 값은 원래 카드가 갖고 있던 것을 그대로 옮긴다 — 폐기 전
`StarterDeckSpecs.cs`에서 각 팩터리의 `EnergyCost`·`BaseExecutionOrder`·`Value`·`Condition`·
`Selector`를 **전부** 읽어 그대로 채운다. 이 픽스처가 그 카드가 무엇이었는지의 유일한 기록이
되므로, 테스트 결과에 영향이 없는 필드도 흘리지 않는다. 단언은 하나도 바꾸지 않는다.

```csharp
        [Test]
        public void Quick_cut_pulled_first_deals_eight()
        {
            var session = new DeckCombatSession(
                new[] { Def(QuickCutFixture()), Def(PullForwardFixture()) }, 30,
                new[] { new Enemy("goblin", 100) }, Goblin(5, 3), 3, 5, 1);
            session.PlayExecutionCard(HandIndex(session, "quick_cut"));
            session.PlayInterventionCard(HandIndex(session, "pull_forward"), ZoneIndex(session, "quick_cut"));
            Assert.AreEqual(8, DamageOf(session.ResolveTurn(), "quick_cut"));
        }
```

`Counter_spec_uses_previous_executed_enemy_attack_condition`은 **카드의 저작 자체**를 검사하므로
카드와 함께 삭제한다. 남길 규칙이 없다 — 조건 자체는
`PreviousExecutedCardConditionTests`가 이미 직접 검사한다.

`SlowHasteStatusTests.cs:134`의 `StarterDeckSpecs.SlowHex()`도 같은 방식으로 인라인 픽스처로
바꾼다.

- [ ] **Step 3: 픽스처 이관만으로 테스트가 통과하는지 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 431` (Counter 저작 검사 1개 삭제)

여기서 실패하면 픽스처가 원래 카드와 다른 것이다. 폐기 **전에** 잡아야 한다.

- [ ] **Step 4: 팩터리와 `AllAuthored()`를 삭제한다**

`Assets/Core/Authoring/StarterDeckSpecs.cs`에서 10개 `public static CardSpec` 팩터리와
`AllAuthored()`를 지운다. `Build()`와 그것이 부르는 `StarterPoolSpecs` 위임은 그대로 둔다.

- [ ] **Step 5: 내보내기 원본을 되돌린다**

`Assets/Unity/Editor/CardContentExporter.cs`와
`Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs`에서
`StarterDeckSpecs.AllAuthored()` → `StarterDeckSpecs.Build()`.

`CardContentEquivalenceJsonTests`의 `EveryAuthoredCardFactoryIsRepresentedInTheContent`는
**남긴다.** 리플렉션으로 팩터리를 훑으므로 앞으로도 누락을 잡는다.

- [ ] **Step 6: JSON과 SO를 지운다**

```bash
cd Assets/StreamingAssets/Content/Cards
git rm slash.json slash.json.meta guard.json guard.json.meta \
       quick_cut.json quick_cut.json.meta counter_stance.json counter_stance.json.meta \
       cover.json cover.json.meta pull_forward.json pull_forward.json.meta \
       push_back.json push_back.json.meta swap_positions.json swap_positions.json.meta \
       slow_hex.json slow_hex.json.meta quicken_self.json quicken_self.json.meta
cd -
git rm Assets/Unity/CardSO/Player/{slash,guard,quick_cut,counter_stance,cover,pull_forward,push_back}.asset
git rm Assets/Unity/CardSO/Player/{slash,guard,quick_cut,counter_stance,cover,pull_forward,push_back}.asset.meta
```

`swap_positions`·`slow_hex`·`quicken_self`의 SO는 없을 수 있다. `ls`로 확인하고 있는 것만 지운다.

- [ ] **Step 7: 어디서도 참조가 끊기지 않았는지 확인한다**

```bash
/usr/bin/grep -rn "slash\|quick_cut\|counter_stance\|pull_forward\|push_back\|swap_positions\|slow_hex\|quicken_self" Assets --include='*.cs' --include='*.asset' | /usr/bin/grep -v "Content/Cards"
```
Expected: 출력 없음

- [ ] **Step 8: 전체 테스트**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 431`, 카드 JSON **26장**

- [ ] **Step 9: Unity 배치로 확인한다**

```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode -testResults /private/tmp/fw-s1.xml \
  -logFile /private/tmp/fw-s1.log
```
Expected: 실패 0건. SO를 지웠으므로 `CardPoolAssetTests`·`StarterPoolSeederTests`가 특히 중요하다.

- [ ] **Step 10: 커밋**

```bash
git status --short
git add -A Assets docs
git commit -m "refactor: 쓰이지 않는 레거시 카드 10장을 폐기한다

StarterDeckSpecs.Build()가 고르지 않고 배틀씬·StarterDeck.asset·
StarterPool.asset 어디에도 없던 10장이다. 이들을 픽스처로 쓰던 규칙
테스트는 인라인 픽스처로 옮겨 출시 카드에 의존하지 않게 했다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: 상태 콘텐츠 스키마와 로더

카드와 같은 `ContentJson`을 쓴다. 판별자는 상태 키다.

**Files:**
- Create: `Assets/Core/Authoring/Statuses/StatusSpec.cs`
- Create: `Assets/Core/Authoring/Statuses/Specs/{MultiplierStatusSpec,PoisonStatusSpec,ExecutionOrderStatusSpec}.cs`
- Create: `Assets/Core/Authoring/Statuses/StatusSpecCatalog.cs`
- Create: `Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs`
- Modify: `Assets/Core/Authoring/Json/ContentJson.cs` (컨버터 등록)
- Create: `Assets/Core/Tests/EditMode/StatusContentTests.cs`

**Interfaces:**
- Consumes: `ContentJson.Write/Read` (계획 1), `StatusKey`, `StatusLifetimeKind`, `StatusRule`
- Produces:
  - `abstract class StatusSpec` — `StatusKeyRef Key`, `StatusLifetimeKind Lifetime`,
    `virtual StatusRule ToRule()`, `virtual IEnumerable<string> Validate(AuthoringContext)`
  - `StatusSpecCatalog.All()` → `IReadOnlyList<StatusSpecInfo>` (`Key`, `SpecType`, `Create`)

파라미터가 없는 상태(방어·전염·독 잠복·독 안정·보상 무효)는 기반 클래스를 그대로 쓴다. 파라미터가
있는 상태만 서브클래스를 갖는다 — `lifetimeCount`가 그랬듯 **쓰이지 않는 칸을 만들지 않기 위해서**다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

Create `Assets/Core/Tests/EditMode/StatusContentTests.cs`:

```csharp
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Status;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class StatusContentTests
    {
        [Test]
        public void RoundTripsEveryRegisteredStatusSpecKind()
        {
            foreach (var info in StatusSpecCatalog.All())
            {
                var original = info.Create();
                var restored = ContentJson.Read<StatusSpec>(ContentJson.Write(original));

                Assert.AreEqual(info.SpecType, restored.GetType(), info.Key.Id);
            }
        }

        [Test]
        public void RoundTripsPoisonGrowth()
        {
            var original = new PoisonStatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Poison),
                Lifetime = StatusLifetimeKind.Permanent,
                GrowthPerTurn = 2
            };

            var restored = (PoisonStatusSpec)ContentJson.Read<StatusSpec>(ContentJson.Write(original));

            Assert.AreEqual("poison", restored.Key.Id);
            Assert.AreEqual(StatusLifetimeKind.Permanent, restored.Lifetime);
            Assert.AreEqual(2, restored.GrowthPerTurn);
        }

        [Test]
        public void MultiplierSpecBecomesAStatusRule()
        {
            var spec = new MultiplierStatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Vulnerable),
                Lifetime = StatusLifetimeKind.Turns,
                MultiplierPercent = 150
            };

            Assert.AreEqual(150, spec.ToRule().MultiplierPercent);
            Assert.AreEqual(15, spec.ToRule().Apply(10));
        }

        [Test]
        public void EveryCatalogEntryHasADistinctKey()
        {
            CollectionAssert.AllItemsAreUnique(
                StatusSpecCatalog.All().Select(info => info.Key.Id).ToList());
        }

        [Test]
        public void RejectsAnUnknownStatusKeyByName()
        {
            var ex = Assert.Throws<Newtonsoft.Json.JsonSerializationException>(
                () => ContentJson.Read<StatusSpec>("{ \"key\": \"psion\" }"));

            StringAssert.Contains("psion", ex.Message);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter StatusContentTests`
Expected: 컴파일 실패 — `namespace 'Statuses' does not exist`

- [ ] **Step 3: 기반 스펙을 만든다**

Create `Assets/Core/Authoring/Statuses/StatusSpec.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>저작된 상태 하나. 파라미터가 없는 상태(방어·전염·독 잠복·독 안정·보상 무효)는 이
    /// 클래스를 그대로 쓴다 — 쓰이지 않는 칸을 만들지 않기 위해 파라미터가 있는 상태만 서브클래스를
    /// 갖는다. behavior 클래스는 코드에 남고 키로 등록된다(규칙 9).</summary>
    [Serializable]
    public class StatusSpec
    {
        public StatusKeyRef Key;

        /// <summary>이 상태의 수명 종류. 카드가 적는 count의 뜻을 여기서 정한다 —
        /// Permanent·ThisTurn이면 세기, Turns·UntilConsumed면 지속.</summary>
        public StatusLifetimeKind Lifetime;

        [JsonIgnore]
        public bool CountIsDuration
            => Lifetime == StatusLifetimeKind.Turns
                || Lifetime == StatusLifetimeKind.UntilConsumed;

        public virtual StatusRule ToRule() => new StatusRule();

        public virtual IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Key.IsEmpty)
            {
                yield return "status spec requires a key.";
            }
            else if (!context.HasStatus(Key.ToKey()))
            {
                yield return "no runtime behavior for status key '" + Key.Id + "'.";
            }
        }
    }
}
```

- [ ] **Step 4: 파라미터를 갖는 세 스펙을 만든다**

Create `Assets/Core/Authoring/Statuses/Specs/MultiplierStatusSpec.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>피해·획득량에 정수 퍼센트 배율을 거는 상태 (취약·약화·손상).</summary>
    [Serializable]
    public sealed class MultiplierStatusSpec : StatusSpec
    {
        public int MultiplierPercent = StatusRule.NeutralPercent;

        public override StatusRule ToRule()
            => new StatusRule { MultiplierPercent = MultiplierPercent };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            foreach (var error in base.Validate(context)) yield return error;

            if (MultiplierPercent < 0)
            {
                yield return "multiplierPercent must not be negative.";
            }
        }
    }
}
```

Create `Assets/Core/Authoring/Statuses/Specs/PoisonStatusSpec.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>턴 종료마다 발동하고 스스로 자라는 상태 (독).</summary>
    [Serializable]
    public sealed class PoisonStatusSpec : StatusSpec
    {
        public int GrowthPerTurn;

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            foreach (var error in base.Validate(context)) yield return error;

            if (GrowthPerTurn < 0)
            {
                yield return "growthPerTurn must not be negative.";
            }
        }
    }
}
```

Create `Assets/Core/Authoring/Statuses/Specs/ExecutionOrderStatusSpec.cs`:

```csharp
using System;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>보유자의 카드 실행 순서를 옮기는 상태 (둔화 +, 가속 −).
    /// 세기는 상태가 소유하고 카드는 지속 턴만 준다.</summary>
    [Serializable]
    public sealed class ExecutionOrderStatusSpec : StatusSpec
    {
        public int ExecutionOrderDelta;
    }
}
```

- [ ] **Step 5: 명시적 카탈로그를 만든다**

Create `Assets/Core/Authoring/Statuses/StatusSpecCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    public sealed class StatusSpecInfo
    {
        public StatusSpecInfo(StatusKey key, Type specType, Func<StatusSpec> create)
        {
            Key = key;
            SpecType = specType;
            Create = create;
        }

        public StatusKey Key { get; }
        public Type SpecType { get; }
        public Func<StatusSpec> Create { get; }
    }

    /// <summary>저작 가능한 상태의 명시적 목록. 어느 상태가 어떤 스펙 모양을 갖는지의 단일 출처이며
    /// JSON 판별자 표도 여기서 만든다 — 리플렉션 스캔 없음(규칙 9). 모드는 여기 등록된 상태의
    /// 파라미터만 조정할 수 있고 새 키는 추가할 수 없다.</summary>
    public static class StatusSpecCatalog
    {
        public static IReadOnlyList<StatusSpecInfo> All() => new[]
        {
            Simple(StatusKeys.Block),
            Simple(StatusKeys.Contagion),
            Simple(StatusKeys.PoisonDormant),
            Simple(StatusKeys.PoisonStasis),
            Simple(StatusKeys.RewardNullified),
            Parameterised(StatusKeys.Poison, () => new PoisonStatusSpec()),
            Parameterised(StatusKeys.Vulnerable, () => new MultiplierStatusSpec()),
            Parameterised(StatusKeys.Weak, () => new MultiplierStatusSpec()),
            Parameterised(StatusKeys.Damaged, () => new MultiplierStatusSpec()),
            Parameterised(StatusKeys.Slow, () => new ExecutionOrderStatusSpec()),
            Parameterised(StatusKeys.Haste, () => new ExecutionOrderStatusSpec())
        };

        private static StatusSpecInfo Simple(StatusKey key)
            => Parameterised(key, () => new StatusSpec());

        /// <summary>팩터리가 만든 스펙에 **반드시 Key를 채운다.** Key는 EffectSpec.Key와 달리
        /// [JsonIgnore]가 아닌 실제 필드라, 비워두면 DefaultValueHandling.Ignore가 쓰기에서
        /// 지워버리고 되읽을 때 "key 없음" 예외가 난다.</summary>
        private static StatusSpecInfo Parameterised(StatusKey key, Func<StatusSpec> create)
            => new StatusSpecInfo(key, create().GetType(), () =>
            {
                var spec = create();
                spec.Key = StatusKeyRef.Of(key);
                return spec;
            });
    }
}
```

`MultiplierStatusSpec`을 세 상태가 공유하므로 **판별자는 타입이 아니라 키**다. 쓰기 쪽은
`spec.Key.Id`를 그대로 쓰고, 읽기 쪽은 키로 팩터리를 찾는다.

- [ ] **Step 6: 다형 컨버터를 만든다**

Create `Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Statuses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>StatusSpec의 다형 (역)직렬화. 판별자는 상태 키 자체다 — EffectSpec이 EffectKey.Id를
    /// 쓰는 것과 같은 형태이며, 여러 상태가 같은 스펙 타입을 공유하므로 타입이 아니라 키로 가른다.</summary>
    public sealed class StatusSpecJsonConverter : JsonConverter<StatusSpec>
    {
        public const string KeyProperty = "key";

        private static readonly Dictionary<string, Func<StatusSpec>> FactoryByKey = BuildFactories();

        public override StatusSpec ReadJson(
            JsonReader reader, Type objectType, StatusSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var key = (string)entry[KeyProperty];
            if (string.IsNullOrEmpty(key))
            {
                throw new JsonSerializationException(
                    "Status entry requires a '" + KeyProperty + "' property.");
            }

            if (!FactoryByKey.TryGetValue(key, out var create))
            {
                throw new JsonSerializationException("Unknown status key '" + key + "'.");
            }

            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Plain.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, StatusSpec value, JsonSerializer serializer)
            => JObject.FromObject(value, ContentJson.Plain).WriteTo(writer);

        private static Dictionary<string, Func<StatusSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<StatusSpec>>();
            foreach (var info in StatusSpecCatalog.All())
            {
                if (table.ContainsKey(info.Key.Id))
                {
                    throw new InvalidOperationException(
                        "Duplicate status key '" + info.Key.Id + "' in StatusSpecCatalog.");
                }

                table.Add(info.Key.Id, info.Create);
            }

            return table;
        }
    }
}
```

`key`는 스펙의 실제 필드이므로 `Remove` 하지 않는다 — `Populate`이 그대로 채운다.

- [ ] **Step 7: 컨버터를 등록한다**

`Assets/Core/Authoring/Json/ContentJson.cs`의 `Build`에서 `includePolymorphic` 블록에 한 줄 추가:

```csharp
                settings.Converters.Add(new StatusSpecJsonConverter());
```

- [ ] **Step 8: 테스트 통과를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter StatusContentTests`
Expected: `Passed: 5, Failed: 0`

- [ ] **Step 9: 전체 테스트와 커밋**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 436`

```bash
git add Assets/Core/Authoring Assets/Core/Tests/EditMode/StatusContentTests.cs
git commit -m "feat: 상태 저작 스펙과 다형 JSON 컨버터를 더한다

파라미터가 있는 상태만 서브클래스를 갖는다. 판별자는 상태 키 자체이고
표는 명시적 카탈로그에서 만든다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: 상태 콘텐츠 로더와 파일

**Files:**
- Create: `Assets/Core/Authoring/Statuses/StatusContentCatalog.cs`
- Create: `Assets/Core/Authoring/Statuses/StatusContentLoader.cs`
- Create: `Assets/StreamingAssets/Content/Statuses/*.json` (11개, 익스포터로 생성)
- Modify: `Assets/Unity/Editor/CardContentExporter.cs` (상태 내보내기 추가)
- Modify: `Assets/Core/Tests/EditMode/StatusContentTests.cs`

**Interfaces:**
- Consumes: `CardContentSource`, `CardContentFiles.ReadDirectory` (계획 1 — 그대로 재사용),
  `AuthoringContext.Default()`
- Produces:
  - `StatusContentCatalog` — `Rules` (`StatusRuleSet`), `LifetimeOf(StatusKey)` →
    `StatusLifetimeKind`, `CountIsDuration(StatusKey)` → `bool`, `Keys` (정렬됨)
  - `StatusContentLoader.Load(IEnumerable<CardContentSource>, AuthoringContext)` →
    `StatusContentLoadResult` (`Succeeded`, `Catalog`, `Errors`)
  - `CardContentFiles.StatusesFolderName` = `"Statuses"`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`StatusContentTests.cs`에 더한다.

```csharp
        private static StatusContentLoadResult Load(params CardContentSource[] sources)
            => StatusContentLoader.Load(sources, AuthoringContext.Default());

        [Test]
        public void LoadsAStatusIntoTheCatalog()
        {
            var result = Load(new CardContentSource(
                "poison.json",
                "{ \"key\": \"poison\", \"lifetime\": \"Permanent\", \"growthPerTurn\": 1 }"));

            Assert.IsTrue(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.AreEqual(
                StatusLifetimeKind.Permanent, result.Catalog.LifetimeOf(StatusKeys.Poison));
            Assert.IsFalse(result.Catalog.CountIsDuration(StatusKeys.Poison));
        }

        [Test]
        public void ExposesMultipliersAsCombatRules()
        {
            var result = Load(new CardContentSource(
                "vulnerable.json",
                "{ \"key\": \"vulnerable\", \"lifetime\": \"Turns\", \"multiplierPercent\": 150 }"));

            Assert.IsTrue(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.AreEqual(15, result.Catalog.Rules.For(StatusKeys.Vulnerable).Apply(10));
            Assert.IsTrue(result.Catalog.CountIsDuration(StatusKeys.Vulnerable));
        }

        [Test]
        public void ReportsADuplicateStatusAcrossFiles()
        {
            const string Block = "{ \"key\": \"block\", \"lifetime\": \"ThisTurn\" }";
            var result = Load(
                new CardContentSource("a.json", Block),
                new CardContentSource("b.json", Block));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("block", result.Errors[0]);
            StringAssert.Contains("b.json", result.Errors[0]);
        }

        [Test]
        public void ReportsAStatusThatHasNoRegisteredBehavior()
        {
            var result = Load(new CardContentSource(
                "ghost.json", "{ \"key\": \"stun\", \"lifetime\": \"ThisTurn\" }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("stun", result.Errors[0]);
        }

        [Test]
        public void RequiresEveryRegisteredStatusToBeAuthored()
        {
            var result = Load(new CardContentSource(
                "block.json", "{ \"key\": \"block\", \"lifetime\": \"ThisTurn\" }"));

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("poison")));
        }
```

마지막 테스트가 중요하다 — 상태 하나라도 저작이 빠지면 그 상태를 거는 카드가 조용히 잘못
동작하므로, 로더는 **등록된 전 상태가 저작됐는지**까지 확인한다.

그리고 내보낸 파일과 헤드리스 폴백이 같은 출처임을 잠근다.

```csharp
        [Test]
        public void DefaultsCoverEveryRegisteredStatus()
        {
            var catalog = StatusContentDefaults.Catalog();

            foreach (var key in AuthoringContext.Default().RegisteredStatusKeys)
            {
                Assert.DoesNotThrow(
                    () => catalog.LifetimeOf(key), "상태 '" + key.Id + "'의 기본값이 없다.");
            }
        }
```

`ReportsAStatusThatHasNoRegisteredBehavior`는 Task 5에서 `stun`을 지운 뒤에야 통과한다. 그때까지는
`[Ignore("Task 5에서 stun 제거 후 활성화")]`를 붙여둔다.

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter StatusContentTests`
Expected: 컴파일 실패 — `StatusContentLoader does not exist`

- [ ] **Step 3: 카탈로그를 만든다**

Create `Assets/Core/Authoring/Statuses/StatusContentCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>부팅 시 한 번 만들어지는 상태 규칙 모음. 전투당 하나이며 캐릭터별 규칙은 없다.</summary>
    public sealed class StatusContentCatalog
    {
        private readonly Dictionary<StatusKey, StatusSpec> _specs;
        private readonly List<string> _keys;

        public StatusContentCatalog(Dictionary<StatusKey, StatusSpec> specs)
        {
            _specs = specs;
            Rules = new StatusRuleSet();
            _keys = new List<string>();
            foreach (var pair in specs)
            {
                Rules.Set(pair.Key, pair.Value.ToRule());
                _keys.Add(pair.Key.Id);
            }

            _keys.Sort(StringComparer.Ordinal);
        }

        public StatusRuleSet Rules { get; }

        /// <summary>정렬된 키 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Keys => _keys;

        public StatusLifetimeKind LifetimeOf(StatusKey key) => Spec(key).Lifetime;

        public bool CountIsDuration(StatusKey key) => Spec(key).CountIsDuration;

        public int ExecutionOrderDeltaOf(StatusKey key)
            => Spec(key) is ExecutionOrderStatusSpec spec ? spec.ExecutionOrderDelta : 0;

        public int GrowthPerTurnOf(StatusKey key)
            => Spec(key) is PoisonStatusSpec spec ? spec.GrowthPerTurn : 0;

        private StatusSpec Spec(StatusKey key)
        {
            if (!_specs.TryGetValue(key, out var spec))
            {
                throw new KeyNotFoundException("No authored status content for '" + key.Id + "'.");
            }

            return spec;
        }
    }
}
```

- [ ] **Step 4: 로더를 만든다**

Create `Assets/Core/Authoring/Statuses/StatusContentLoader.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Status;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Statuses
{
    public sealed class StatusContentLoadResult
    {
        private StatusContentLoadResult(StatusContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public StatusContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static StatusContentLoadResult Ok(StatusContentCatalog catalog)
            => new StatusContentLoadResult(catalog, new string[0]);

        public static StatusContentLoadResult Failed(IReadOnlyList<string> errors)
            => new StatusContentLoadResult(null, errors);
    }

    /// <summary>상태 소스를 파싱·검증해 카탈로그로 만든다. 카드 로더와 같은 형태이며 파일을 직접
    /// 읽지 않는다. 등록된 상태가 하나라도 저작되지 않으면 거부한다 — 빠진 상태를 거는 카드가
    /// 조용히 잘못 동작하는 것을 막는다.</summary>
    public static class StatusContentLoader
    {
        public static StatusContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            AuthoringContext context)
        {
            var errors = new List<string>();
            var specs = new Dictionary<StatusKey, StatusSpec>();
            var origin = new Dictionary<StatusKey, string>();

            foreach (var source in sources)
            {
                StatusSpec spec;
                try
                {
                    spec = ContentJson.Read<StatusSpec>(source.Json);
                }
                catch (JsonException ex)
                {
                    errors.Add(source.Name + ": " + ex.Message);
                    continue;
                }

                var key = spec.Key.ToKey();
                if (origin.TryGetValue(key, out var first))
                {
                    errors.Add(
                        source.Name + ": duplicate status '" + key.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                foreach (var error in spec.Validate(context))
                {
                    errors.Add(source.Name + ": " + error);
                }

                origin.Add(key, source.Name);
                specs.Add(key, spec);
            }

            foreach (var key in context.RegisteredStatusKeys)
            {
                if (!specs.ContainsKey(key))
                {
                    errors.Add("Status '" + key.Id + "' is registered but has no authored content.");
                }
            }

            return errors.Count > 0
                ? StatusContentLoadResult.Failed(errors)
                : StatusContentLoadResult.Ok(new StatusContentCatalog(specs));
        }
    }
}
```

- [ ] **Step 5: 저작 기본값을 한 곳에 두고 익스포터가 그것을 읽는다**

**원본은 하나여야 한다.** 익스포터가 값을 직접 나열하고 코어가 폴백으로 같은 값을 또 갖고 있으면
둘이 어긋날 수 있고, 그건 이 작업이 없애려는 바로 그 문제다. 따라서 기본값을 코어에 한 번만 두고
익스포터는 **읽기만** 한다. 카드가 `StarterPoolSpecs` → JSON으로 나가는 것과 같은 형태이며,
후속 계획이 C# 쪽을 지울 때도 같은 경로를 밟는다.

Create `Assets/Core/Authoring/Statuses/StatusContentDefaults.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>저작된 상태의 기본값. 이 게임의 상태 규칙이 실제로 사는 곳이며, 내보내기와 헤드리스
    /// 폴백이 **둘 다 여기서 읽는다** — 값이 두 곳에 있으면 어긋날 수 있기 때문이다.
    /// 후속 계획에서 JSON이 진실의 원천이 되면 이 클래스는 제거된다.</summary>
    public static class StatusContentDefaults
    {
        public static IReadOnlyList<StatusSpec> Specs() => new[]
        {
            Simple(StatusKeys.Block, StatusLifetimeKind.ThisTurn),
            Simple(StatusKeys.Contagion, StatusLifetimeKind.Turns),
            Simple(StatusKeys.PoisonDormant, StatusLifetimeKind.ThisTurn),
            Simple(StatusKeys.PoisonStasis, StatusLifetimeKind.ThisTurn),
            Simple(StatusKeys.RewardNullified, StatusLifetimeKind.UntilConsumed),
            new PoisonStatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Poison),
                Lifetime = StatusLifetimeKind.Permanent,
                GrowthPerTurn = 1
            },
            Multiplier(StatusKeys.Vulnerable, StatusRuleCatalog.VulnerableIncomingPercent),
            Multiplier(StatusKeys.Weak, StatusRuleCatalog.WeakOutgoingPercent),
            Multiplier(StatusKeys.Damaged, StatusRuleCatalog.DamagedBlockGainPercent),
            Order(StatusKeys.Slow, 2),
            Order(StatusKeys.Haste, -2)
        };

        /// <summary>파일 없이 도는 헤드리스 테스트와 하니스가 쓰는 카탈로그.
        /// 내보낸 JSON과 같은 Specs()에서 만들어지므로 둘이 어긋날 수 없다.</summary>
        public static StatusContentCatalog Catalog()
        {
            var specs = new Dictionary<StatusKey, StatusSpec>();
            foreach (var spec in Specs())
            {
                specs.Add(spec.Key.ToKey(), spec);
            }

            return new StatusContentCatalog(specs);
        }

        private static StatusSpec Simple(StatusKey key, StatusLifetimeKind lifetime)
            => new StatusSpec { Key = StatusKeyRef.Of(key), Lifetime = lifetime };

        private static StatusSpec Multiplier(StatusKey key, int percent)
            => new MultiplierStatusSpec
            {
                Key = StatusKeyRef.Of(key),
                Lifetime = StatusLifetimeKind.Turns,
                MultiplierPercent = percent
            };

        private static StatusSpec Order(StatusKey key, int delta)
            => new ExecutionOrderStatusSpec
            {
                Key = StatusKeyRef.Of(key),
                Lifetime = StatusLifetimeKind.Turns,
                ExecutionOrderDelta = delta
            };
```

그리고 `Assets/Unity/Editor/CardContentExporter.cs`는 **읽기만** 한다 — 값을 다시 적지 않는다.

```csharp
        private const string StatusOutputDirectory = "Assets/StreamingAssets/Content/Statuses";

        private static void ExportStatuses()
        {
            Directory.CreateDirectory(StatusOutputDirectory);
            foreach (var spec in StatusContentDefaults.Specs())
            {
                File.WriteAllText(
                    Path.Combine(StatusOutputDirectory, spec.Key.Id + ".json"),
                    ContentJson.Write(spec) + "\n");
            }
        }
```

`ExportAll()`에서 `ExportStatuses()`를 부른다.

내보낸 JSON과 헤드리스 폴백이 **같은 `Specs()`에서 나오므로** 어긋날 수 없다. 이 동등성을 잠그는
테스트를 Task 3 Step 1에 함께 넣는다.

둔화·가속의 `2`/`-2`는 폐기된 `slow_hex`·`quicken_self`가 갖고 있던 값(3)이 아니라 **새로 정하는
기본값**이다. 카드가 0장이라 회귀시킬 대상이 없으므로 이 계획이 정한다.

- [ ] **Step 6: 익스포터를 배치로 실행한다**

```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod FateWeaver.Unity.Editor.CardContentExporter.ExportAll \
  -logFile /private/tmp/fw-s3-export.log
```
Expected: 카드 26장 + 상태 11개

- [ ] **Step 7: 생성물을 눈으로 확인한다**

Run: `cat Assets/StreamingAssets/Content/Statuses/poison.json Assets/StreamingAssets/Content/Statuses/block.json`
Expected: `poison.json`에 `growthPerTurn: 1`이 있고, `block.json`에는 `key`·`lifetime`뿐

- [ ] **Step 8: 테스트와 커밋**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 441` (`ReportsAStatusThatHasNoRegisteredBehavior`는 Ignore 상태)

```bash
git status --short
git add Assets/Core/Authoring Assets/Unity/Editor Assets/StreamingAssets Assets/Core/Tests
git commit -m "feat: 상태 규칙을 JSON 콘텐츠로 내보내고 읽는다

배율·성장치·실행 순서 변화량이 코드 상수에서 저작 데이터로 나온다.
등록된 상태가 하나라도 저작되지 않으면 로드를 거부한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: 코어가 상태 카탈로그에서 수명과 세기를 읽는다

여기서 런타임 동작이 바뀐다. 카드가 주는 것은 `count` 하나가 되고, 수명 종류와 세기는 상태에서 온다.

**Files:**
- Modify: `Assets/Core/Combat/CombatState.cs` (`StatusContent` 보유)
- Modify: `Assets/Core/Effects/ApplyStatusPayload.cs` (`StatusLifetime` 제거 — 숫자는 담지 않는다)
- Modify: `Assets/Core/Effects/ApplyStatusHandler.cs`
- Modify: `Assets/Core/Cards/CardDefinition.cs` (`EffectData.ApplyStatus` 헬퍼)
- Modify: `Assets/Core/Simulation/StarterDeck.cs` (9개 테스트 픽스처 팩터리)
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionContracts.cs` (`DescriptionContext`가 카탈로그를 든다)
- Modify: `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs` (`ApplyStatusDescriptionHandler`)
- Modify: `Assets/Core/Status/{SlowBehavior,HasteBehavior}.cs` (세기를 규칙에서)
- Modify: `Assets/Core/Status/PoisonBehavior.cs` (성장치를 규칙에서)
- Modify: `Assets/Core/Effects/TriggerStatusHandler.cs` (억제 마커를 behavior가 안다)
- Modify: `Assets/Core/Status/IStatusBehavior.cs` (`SuppressThisTurn` 훅)

**Interfaces:**
- Consumes: `StatusContentCatalog` (Task 3)
- Produces:
  - `CombatState.StatusContent` (`StatusContentCatalog`)
  - `ApplyStatusPayload(StatusKey Key, StatusApplyTarget Target)` — **숫자를 담지 않는다.**
    수치는 이미 `EffectData.EffectValue`에 있고, 조건부 성공값 덮어쓰기도 그쪽이 처리한다
    (`Cover`의 조건부 방어 7이 그 경로다). 페이로드에 또 넣으면 같은 숫자가 두 벌이 되어
    어긋날 수 있다 — 이 계획이 없애려는 바로 그 결함이다.
  - `DescriptionContext.StatusContent` (`StatusContentCatalog`)
  - `StatusBehavior.SuppressThisTurn(StatusBag holderBag)` — 기본 구현은 no-op,
    `PoisonBehavior`가 `poison_dormant`를 건다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/StatusContentTests.cs`에 더한다.

```csharp
        [Test]
        public void CardCountBecomesMagnitudeForAPermanentStatus()
        {
            var state = CombatFixture.WithStatusContent();
            var card = CombatFixture.ApplyStatusCard("poison", count: 3);

            CombatFixture.Resolve(state, card);

            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void CardCountBecomesDurationForATurnsStatus()
        {
            var state = CombatFixture.WithStatusContent();
            var card = CombatFixture.ApplyStatusCard("slow", count: 3);

            CombatFixture.Resolve(state, card);
            var instance = state.Enemies[0].Statuses.Get(StatusKeys.Slow);

            Assert.AreEqual(3, instance.Count);
            Assert.AreEqual(StatusLifetimeKind.Turns, instance.Kind);
        }

        [Test]
        public void SlowStrengthComesFromTheStatusNotTheCard()
        {
            var state = CombatFixture.WithStatusContent();
            CombatFixture.Resolve(state, CombatFixture.ApplyStatusCard("slow", count: 2));

            Assert.AreEqual(
                2, state.StatusContent.ExecutionOrderDeltaOf(StatusKeys.Slow));
        }
```

`CombatFixture`는 이 테스트 파일 안의 private static 헬퍼로 만든다 — 상태 콘텐츠를 실은
`CombatState`와 `apply_status` 효과 하나짜리 카드를 조립하고 한 턴 해결하는 세 메서드다.

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter StatusContentTests`
Expected: 컴파일 실패 — `CombatState does not contain StatusContent`

- [ ] **Step 3: `CombatState`가 상태 콘텐츠를 든다**

`StatusRules` 프로퍼티를 카탈로그 경유로 바꾼다. 기존 `StatusRules` 사용처가 그대로 돌도록
프로퍼티는 남기고 카탈로그에서 위임한다.

```csharp
        /// <summary>이 전투의 상태 저작 콘텐츠. 규칙(배율)과 수명 종류의 단일 출처다.</summary>
        public Authoring.Statuses.StatusContentCatalog StatusContent { get; set; }
            = StatusContentDefaults.Catalog();

        public Status.StatusRuleSet StatusRules => StatusContent.Rules;
```

`StatusContentDefaults.Catalog()`(Task 3에서 만든다)는 파일 없이 도는 헤드리스 테스트와 하니스의
폴백이다. 내보낸 JSON과 **같은 `Specs()`에서 만들어지므로** 값이 어긋날 수 없다. Unity 런타임은
로더가 파일에서 만든 카탈로그를 주입한다.

`Assets/Core/Simulation/StarterDeck.cs`는 폐기된 레거시 카드와 같은 id를 갖지만 `CardSpec`이 아니라
`CardDefinition`을 직접 만드는 **테스트 픽스처 9개**다(테스트 22곳이 쓴다). 출시 경로가 없으므로
Task 1이 남겨두었지만, `ApplyStatusPayload` 시그니처가 바뀌면 함께 고쳐야 한다. 이들을 없애는 것은
22곳 이관이 필요한 별도 정리 작업이다.

- [ ] **Step 4: 페이로드에서 수명을 빼고 `Count`를 넣는다**

```csharp
public sealed record ApplyStatusPayload(StatusKey Key, int Count, StatusApplyTarget Target);
```

`ApplyStatusHandler`가 카탈로그를 보고 조립한다.

```csharp
            var content = ctx.State.StatusContent;
            var kind = content.LifetimeOf(payload.Key);
            var countIsDuration = content.CountIsDuration(payload.Key);
            // 숫자는 ctx.EffectValue 하나뿐이다 — 조건부 성공값이 이미 반영된 값이다.
            var lifetime = StatusLifetime.Of(kind, countIsDuration ? ctx.EffectValue : 0);
            var magnitude = countIsDuration ? 0 : ctx.EffectValue;
```

`StatusLifetime`에 `public static StatusLifetime Of(StatusLifetimeKind kind, int count)`를 더한다
(생성자가 private이므로 필요하다).

- [ ] **Step 5: 둔화·가속·독의 세기를 규칙에서 읽는다**

```csharp
    public sealed class SlowBehavior : StatusBehavior
    {
        public override int ModifyExecutionOrder(int executionOrder, StatusContext ctx)
            => executionOrder + ctx.Content.ExecutionOrderDeltaOf(Key);
    }
```

`StatusContext`에 `StatusContentCatalog Content`를 더하고, `Rules`는 그대로 둔다(취약·약화·손상이
쓴다). `PoisonBehavior`의 `_growthPerTurn` 필드를 제거하고 `ctx.Content.GrowthPerTurnOf(Key)`로
바꾼다 — `StatusTickContext`에도 `Content`를 더한다. `CombatRegistries.Statuses()`의
`new PoisonBehavior(growthPerTurn: 1)`은 `new PoisonBehavior()`가 된다.

- [ ] **Step 5b: 설명 레이어가 카탈로그를 읽는다**

`ApplyStatusDescriptionHandler`는 지금 `payload.Lifetime.Kind`로 "(N턴)" 접미사를 만든다. 페이로드가
수명을 잃으면 컴파일되지 않고, 숫자가 턴인지 세기인지는 이제 상태만 안다. 규칙 10(설명은 데이터에서
생성)을 지키려면 컴포저가 카탈로그에 닿아야 한다.

`DescriptionContext` 생성자에 `StatusContentCatalog statusContent`를 더하고 프로퍼티로 노출한다.
설명 레이어가 이미 `StatusDescriptionRegistry`로 상태 메타데이터에 의존하고 있으므로 같은 성격의
확장이다.

`ApplyStatusDescriptionHandler`는 `CountIsDuration`으로 갈린다. 새로 지어낼 문구는 없다.

| `CountIsDuration` | 수명 종류 | 문구 |
|---|---|---|
| false | `Permanent`·`ThisTurn` | `"{상태} {N}"` — 예: `방어 4`, `독 1` (오늘과 같다) |
| true | `Turns` | `"{상태} ({N}턴)"` — 예: `취약 (2턴)` |
| true | `UntilConsumed` | `"{상태} ({N}회)"` |

상태 자신의 세기(`executionOrderDelta`·`multiplierPercent`)는 **카드 텍스트에 나오지 않는다.** 취약이
이미 그렇다 — 배율은 상태의 성질이지 카드의 성질이 아니라서 카드 본문이 ×150%를 되풀이하지 않는다.
둔화도 같아진다.

`LifetimeSuffix(StatusLifetime)`은 종류와 수를 받는 메서드로 바꾼다.

`Korean_slow_status_shows_turn_suffix`는 **지우지 말고 기대값을 다시 쓴다.** 이 테스트는 둔화가
세기 3과 지속 2를 동시에 갖던 옛 구조를 못박고 있으므로, 새 규칙에 맞는 문자열로 바꾸되 접미사가
여전히 렌더링되는지 지키는 역할은 남긴다.

- [ ] **Step 6: 억제 마커를 behavior가 소유한다**

`Assets/Core/Status/IStatusBehavior.cs`의 `StatusBehavior`에 훅을 더한다.

```csharp
        /// <summary>이번 턴 이 상태의 발동을 막는다. trigger_status가 즉시 발동시킨 뒤 호출하며,
        /// 어떤 마커를 쓰는지는 상태 자신만 안다 — 카드가 알 필요가 없다.</summary>
        public virtual void SuppressThisTurn(StatusBag holderBag) { }
```

`PoisonBehavior`가 구현한다.

```csharp
        public override void SuppressThisTurn(StatusBag holderBag)
            => holderBag.Add(StatusKeys.PoisonDormant, StatusLifetime.ThisTurn);
```

`TriggerStatusHandler`에서 `enemy.Statuses.Add(payload.SuppressMarkerKey, ...)`를
`behavior.SuppressThisTurn(enemy.Statuses)`로 바꾸고, `TriggerStatusPayload`에서
`SuppressMarkerKey`를 제거한다. `ValidateData`의 해당 검사도 지운다.

- [ ] **Step 7: 테스트와 커밋**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 444`

기존 상태 테스트가 깨지면 대부분 `ApplyStatusPayload` 생성자 시그니처 때문이다. 값의 의미가 바뀌지
않았는지(`count`가 세기인지 지속인지) 확인하며 고친다.

```bash
git add Assets/Core
git commit -m "refactor: 상태의 수명과 세기를 저작 콘텐츠에서 읽는다

카드가 주는 것은 count 하나이고, 그 뜻은 상태의 lifetime이 정한다.
trigger_status의 억제 마커도 상태 자신이 알게 해 카드에서 사라진다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: 카드 저작 표면을 줄이고 `stun`을 제거한다

**Files:**
- Modify: `Assets/Core/Authoring/Specs/ApplyStatusSpec.cs`
- Modify: `Assets/Core/Authoring/Specs/TriggerStatusSpec.cs`
- Modify: `Assets/Core/Authoring/StarterPoolSpecs.cs` (26장의 저작 값)
- Delete: `Assets/Core/Status/StunBehavior.cs` (+ `.meta`)
- Modify: `Assets/Core/Registries/CombatRegistries.cs`, `KoreanDescriptionCatalog.cs`, `StatusKeys`
- Modify: `Assets/StreamingAssets/Content/Cards/*.json` (익스포터 재실행)

- [ ] **Step 1: `ApplyStatusSpec`을 줄인다**

`Value`·`Lifetime`·`LifetimeCount`를 지우고 `Count` 하나만 남긴다.

```csharp
    [Serializable]
    public sealed class ApplyStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;

        /// <summary>이 카드가 거는 양. 뜻은 상태가 정한다 — 수명이 Permanent·ThisTurn이면 세기,
        /// Turns·UntilConsumed면 지속.</summary>
        public int Count;

        public StatusApplyTarget Target;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.ApplyStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Count)
            {
                Payload = new ApplyStatusPayload(Status.ToKey(), Count, Target)
            }) with { TargetSelector = ToSelector(Selector) };
    }
```

`TriggerStatusSpec`에서 `SuppressMarker` 필드와 그 검증을 지운다.

- [ ] **Step 2: 26장의 저작 값을 옮긴다**

`StarterPoolSpecs.cs`에서 `Value = N, Lifetime = X, LifetimeCount = M` 조합을 `Count = ?`로 바꾼다.
현재 값에서 기계적으로 결정된다.

| 현재 | 새 `Count` |
|---|---|
| `block`, `Value = 4`, `ThisTurn` | `4` (세기) |
| `poison`, `Value = 1`, `Permanent` | `1` (세기) |
| `poison_stasis`, `Value = 0`, `ThisTurn` | `0` |
| `contagion`, `Value = 0`, `Turns`, `LifetimeCount = 2` | `2` (지속) |

- [ ] **Step 3: `stun`을 제거한다**

`StunBehavior.cs`(+`.meta`) 삭제, `CombatRegistries.Statuses()`의 등록 한 줄 삭제,
`KoreanDescriptionCatalog`의 `statuses.Register(StatusKeys.Stun, "기절")` 삭제,
`StatusKeys.Stun` 삭제. Task 3의 `ReportsAStatusThatHasNoRegisteredBehavior`에서 `[Ignore]`를 뗀다.

- [ ] **Step 4: 익스포터를 다시 돌린다**

```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod FateWeaver.Unity.Editor.CardContentExporter.ExportAll \
  -logFile /private/tmp/fw-s5-export.log
```

- [ ] **Step 5: 생성물을 확인한다**

Run: `cat Assets/StreamingAssets/Content/Cards/stable_culture.json Assets/StreamingAssets/Content/Cards/early_onset.json`

Expected: `apply_status`에 `lifetime`·`value`가 없고 `count`만 있다. `trigger_status`에
`suppressMarker`가 없다.

```json
{
  "kind": "apply_status", "status": "poison", "count": 2,
  "target": "TargetEnemy", "selector": "BackMost"
}
```

- [ ] **Step 6: 전체 검증**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 444`

```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode -testResults /private/tmp/fw-s5.xml \
  -logFile /private/tmp/fw-s5.log
```
Expected: 실패 0건

- [ ] **Step 7: 커밋과 문서 갱신**

```bash
git status --short
git add -A Assets docs
git commit -m "refactor: 카드가 상태에 적는 것을 count 하나로 줄인다

세기와 수명 종류는 상태가 소유한다. 쓰이지 않는 stun을 제거한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

이 계획의 상태 줄과 `docs/superpowers/README.md`의 행을 함께 갱신한다(규칙 20).

---

## 완료 조건

- 헤드리스 445 tests 통과, 실패 0 / Unity EditMode 실패 0
- 카드 JSON **26장**, 상태 JSON **11개**
- 카드의 `apply_status`에 `lifetime`·`value`가 없고 `count`만 있다
- `trigger_status`에 `suppressMarker`가 없다
- 등록된 상태가 하나라도 저작되지 않으면 로드가 거부된다
- 워킹 트리가 깨끗하다 (규칙 18)

## 전체 브랜치 리뷰가 잡은 것

**카드 에셋 마이그레이션을 빠뜨렸다 — 게임을 깨뜨릴 뻔했다.**

`ApplyStatusSpec`이 `Value`·`Lifetime`·`LifetimeCount`를 잃었는데 17개 `CardSO/*.asset`의
`[SerializeReference]` YAML은 그대로였다. Unity가 사라진 필드를 버리므로 모든 에셋 카드가
`Count = 0`으로 읽혔다.

이걸 "게임 경로는 JSON이라 무해하다"고 판단해 스냅샷 가드 테스트를 지웠는데, **그 전제가
틀렸다.** `BattleScreenController.cs:78`이 `member.Deck.ToSpecs()`로 **에셋에서** 카드를 만들고
(`DeckPlaytestController.cs:73`도 같다), `CardContentLoader`는 아직 테스트에서만 불린다. 즉
Play 모드에서 `early_guard`·`quick_cover`가 방어 0을, 독 카드들이 독 0을 걸 상태였다.

에셋 YAML을 기계적으로 옮기고(`Count = (Lifetime ∈ {Turns, UntilConsumed}) ? LifetimeCount : Value`)
가드 테스트를 되살렸다. C# 저작(`StarterPoolSpecs`)과 대조해 20개 항목 전부 일치를 확인했다.

**교훈:** 이 저장소에서 `[SerializeReference]` 필드를 바꾸면 **코드와 에셋 YAML을 같은 커밋에서
함께 옮겨야 한다.** 계획 1의 Task 1이 어셈블리 이름으로 같은 함정을 밟았고, 이번엔 필드 이름으로
밟았다. 헤드리스 테스트는 둘 다 잡지 못한다 — Unity EditMode만 잡는다.

## 후속으로 넘기는 것

- **설명 카탈로그가 전투와 다른 `StatusContentCatalog` 인스턴스를 읽는다.**
  `KoreanDescriptionCatalog.Default`는 프로세스 전역 싱글턴이고 그 `DescriptionContext.StatusContent`가
  `StatusContentDefaults.Catalog()`로 고정돼 있다(`KoreanDescriptionCatalog.cs:11,66`). 반면
  `CombatState.StatusContent`는 로더가 만든 카탈로그를 받도록 설계된 세터다. 지금은 값이 같아
  드러나지 않지만, 후속 계획이 `StatusContentLoader`를 부팅에 배선하는 순간 **카드 텍스트는 코드
  기본값으로, 규칙은 파일로** 갈린다. `CreateDefault()`에 카탈로그 오버로드를 주고 Unity 레이어가
  세션마다 만들게 해야 한다.

## 후속

이 계획이 끝나면 계획 2(콘텐츠 원본 전환·코드 생성 제거)가 이어진다. 스키마 정정을 계획 2보다
앞에 둔 이유는, 계획 2가 끝나면 JSON이 진실의 원천이 되어 스키마 변경이 파일 마이그레이션이 되기
때문이다.
