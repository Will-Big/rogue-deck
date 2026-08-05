# 계획 3c — 상태 원본 확정 구현 계획

> **에이전트 작업자에게:** 필수 서브 스킬 — `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans`로 태스크 단위로 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

- 작성일: 2026-08-04
- 완료일: 2026-08-04
- 상태: `archived`
- 완료 시점 실측: 헤드리스 **533/533**, Unity EditMode **682 total / 674 passed / 0 failed / 8 skipped**
- 상위 설계: [카드 변형과 런타임 콘텐츠 로딩](../../specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.5
- 선행: 계획 3b [런타임 콘텐츠 전환](2026-08-03-runtime-content-switch.md) **완료**
- 독립: 계획 3d(C# 카드 스펙 제거)와 순서를 바꿔도 된다

**목표:** 상태 규칙의 유일한 원본을 `Assets/StreamingAssets/Content/Statuses/*.json`으로 확정하고,
같은 값을 들고 있는 코드 기본값(`StatusContentDefaults`)과 그것을 퍼뜨리는 전역 싱글턴
(`CombatState`의 기본 카탈로그, `KoreanDescriptionCatalog.Default`)을 제거한다.

**접근:** 코드는 **모양**(어떤 상태가 존재하고 어떤 스펙 타입을 쓰는가)만 갖고, JSON은 **값**
(표시명·수명·배율·성장치)만 갖는다. 판별자 표를 `StatusContentDefaults.Specs()`에서 행동 레지스트리
(`CombatRegistries.Statuses()`)로 옮기면 JSON이 스스로를 해석하게 되고, 그 시점부터 코드 기본값은
지울 수 있다. 카탈로그는 부팅 1회로 만들어 `GameContent.Statuses`로 상주하고, 필요한 곳에 **주입**된다.

**기술 스택:** C# (netstandard2.1), Newtonsoft.Json, NUnit, Unity 6000.5.2f1

## 전역 제약

- **규칙 6:** `FateWeaver.Core`는 UnityEngine을 참조하지 않는다. 이 계획에서 만드는 코어 타입은
  전부 순수 C#이다.
- **규칙 7 (결정론):** 새 코드에서 `System.Random`·`DateTime`·`Guid.NewGuid()`를 쓰지 않는다.
  카탈로그 순회는 `StatusContentCatalog.Keys`(정렬된 목록)를 쓴다.
- **규칙 8:** 튜닝 수치를 코드에 박지 않는다. 이 계획의 존재 이유이기도 하다 — 독 성장 1,
  취약 150, 약화 75, 손상 75, 둔화 +2, 가속 −2는 **JSON에만** 남는다.
- **규칙 9:** 중앙 switch를 키우지 않는다. 스펙 타입 선택은 각 행동 클래스가 스스로 답한다.
- **규칙 12:** 새 규칙 로직에는 헤드리스 테스트가 붙는다.
- **규칙 20:** 마지막 태스크에서 `docs/superpowers/README.md` 색인을 같은 커밋으로 갱신한다.
- **`.meta` 파일:** 새로 만든 `.cs`에는 Unity가 `.meta`를 생성한다. Unity 배치 실행 뒤
  `git status`로 확인해 같은 커밋에 포함한다.

## 검증 명령

**헤드리스** (모든 태스크 끝에서 실행):

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

특정 테스트만:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~StatusContentTests
```

**Unity EditMode** (태스크 6·7 끝에서 최소 1회. `-quit`를 붙이면 테스트 없이 exit 0이 되므로
절대 붙이지 않는다):

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-status-content -runTests -testPlatform EditMode -testResults /private/tmp/status-3c.xml -logFile /private/tmp/status-3c.log
```

결과는 XML 루트의 `result=` / `total=` / `passed=` / `failed=` 속성으로 확인한다.

**시작 시점 기준선:** 헤드리스 **530/530**(2026-08-04, 카드 프레임 작업이 master에 머지된 뒤 실측).
Unity EditMode 기준선은 착수 세션이 첫 배치 실행에서 측정해 여기 적는다 — 카드 프레임 머지로
프리팹·테스트가 늘어 이전 수치(562/563)는 더 이상 유효하지 않다.

## 파일 구조

| 파일 | 이 계획에서의 책임 |
|---|---|
| `Assets/Core/Status/IStatusBehavior.cs` | 행동이 자기 스펙 타입을 답하는 `NewSpec()` 추가 |
| `Assets/Core/Status/{Poison,Slow,Haste,Vulnerable,Weak,Damaged}Behavior.cs` | `NewSpec()` 재정의 |
| `Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs` | 판별자 표를 레지스트리에서 생성 |
| `Assets/Core/Authoring/ContentBootstrap.cs` | 상태를 가장 먼저 읽는다 + `LoadStatuses` 공개 |
| `Assets/Core/Authoring/GameContent.cs` | `Statuses` 추가 |
| `Assets/Core/Authoring/Specs/ApplyStatusSpec.cs` | 중복 가드 제거 |
| `Assets/Core/Combat/CombatState.cs` | 카탈로그를 생성자에서 요구, 읽기 전용화 |
| `Assets/Core/Simulation/*.cs` | 세션·하니스 5종이 카탈로그를 주입받는다 |
| `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs` | 전역 `Default`·무인자 `CreateDefault()` 제거 |
| `Assets/Unity/{CardPresentation,BattlePresenter,BattleScreenController,BattleUnitsView,UnitView,PlaytestKoreanText}.cs` | 설명·상태 이름을 주입으로 |
| `Assets/Unity/Editor/StatusKeyDropdownOptions.cs` | 드롭다운 라벨을 JSON에서 |
| `Assets/Core/Tests/EditMode/TestContent.cs` | **신설** — 코어 테스트의 콘텐츠 진입점 |
| `Assets/Tests/UnityEditMode/UnityTestContent.cs` | **신설** — Unity 테스트의 콘텐츠 진입점 |
| `Assets/Core/Authoring/Statuses/StatusContentDefaults.cs` | **삭제** |

---

### Task 1: 판별자 표를 행동 레지스트리에서 만든다

JSON이 `StatusContentDefaults` 없이 스스로를 해석하게 만드는 태스크다. 이게 되기 전까지 나머지
제거는 불가능하다.

**Files:**
- Modify: `Assets/Core/Status/IStatusBehavior.cs`
- Modify: `Assets/Core/Status/PoisonBehavior.cs`, `SlowBehavior.cs`, `HasteBehavior.cs`,
  `VulnerableBehavior.cs`, `WeakBehavior.cs`, `DamagedBehavior.cs`
- Modify: `Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs`
- Test: `Assets/Core/Tests/EditMode/StatusContentTests.cs`

**Interfaces:**
- Produces: `IStatusBehavior.NewSpec() → StatusSpec` (기본 구현은 `StatusBehavior`가 제공),
  `StatusSpecJsonConverter.BuildFactories(StatusRegistry) → Dictionary<string, Func<StatusSpec>>`
  (`internal`)

- [x] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/StatusContentTests.cs`에 추가한다 (파일 상단에
`using FateWeaver.Core;`, `using FateWeaver.Core.Status;`가 없으면 함께 추가):

```csharp
[Test]
public void SpecTypeComesFromTheBehaviorRegistry()
{
    var behaviors = CombatRegistries.Statuses();

    Assert.AreEqual(
        typeof(PoisonStatusSpec), behaviors.Resolve(StatusKeys.Poison).NewSpec().GetType());
    Assert.AreEqual(
        typeof(ExecutionOrderStatusSpec), behaviors.Resolve(StatusKeys.Slow).NewSpec().GetType());
    Assert.AreEqual(
        typeof(ExecutionOrderStatusSpec), behaviors.Resolve(StatusKeys.Haste).NewSpec().GetType());
    Assert.AreEqual(
        typeof(MultiplierStatusSpec), behaviors.Resolve(StatusKeys.Vulnerable).NewSpec().GetType());
    Assert.AreEqual(
        typeof(MultiplierStatusSpec), behaviors.Resolve(StatusKeys.Weak).NewSpec().GetType());
    Assert.AreEqual(
        typeof(MultiplierStatusSpec), behaviors.Resolve(StatusKeys.Damaged).NewSpec().GetType());
    Assert.AreEqual(
        typeof(StatusSpec), behaviors.Resolve(StatusKeys.Block).NewSpec().GetType());
}
```

- [x] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~StatusContentTests
```

예상: 컴파일 실패 — `IStatusBehavior`에 `NewSpec` 정의가 없다.

- [x] **Step 3: 행동이 자기 스펙 타입을 답하게 한다**

`Assets/Core/Status/IStatusBehavior.cs`의 `IStatusBehavior` 인터페이스에 추가한다:

```csharp
        /// <summary>이 상태를 저작할 때 쓰는 스펙 타입의 빈 인스턴스. JSON 판별자가 이걸로
        /// "poison → PoisonStatusSpec"을 안다 — 스펙 **모양**은 코드가, **값**은 JSON이 갖는다.
        /// 리플렉션 대신 각 행동이 스스로 답한다 (규칙 9).</summary>
        Authoring.Statuses.StatusSpec NewSpec();
```

같은 파일의 `StatusBehavior` 추상 클래스에 기본 구현을 추가한다:

```csharp
        public virtual Authoring.Statuses.StatusSpec NewSpec()
            => new Authoring.Statuses.StatusSpec();
```

- [x] **Step 4: 파라미터가 있는 여섯 상태가 재정의한다**

`PoisonBehavior.cs`의 클래스 안에 추가한다 (파일 상단에
`using FateWeaver.Core.Authoring.Statuses;` 추가):

```csharp
        public override StatusSpec NewSpec() => new PoisonStatusSpec();
```

`SlowBehavior.cs`와 `HasteBehavior.cs`에 각각 (같은 using 추가):

```csharp
        public override StatusSpec NewSpec() => new ExecutionOrderStatusSpec();
```

`VulnerableBehavior.cs`, `WeakBehavior.cs`, `DamagedBehavior.cs`에 각각 (같은 using 추가):

```csharp
        public override StatusSpec NewSpec() => new MultiplierStatusSpec();
```

나머지 다섯(`Block`, `Contagion`, `PoisonDormant`, `PoisonStasis`, `RewardSuppression`)은
파라미터가 없으므로 재정의하지 않는다 — 기본 구현이 `StatusSpec`을 준다.

- [x] **Step 5: 컨버터가 레지스트리에서 표를 만든다**

`Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs`의 `using`에 `FateWeaver.Core;`와
`FateWeaver.Core.Status;`를 추가하고, `FactoryByKey` 필드와 `BuildFactories`를 통째로 바꾼다:

```csharp
        private static readonly Dictionary<string, Func<StatusSpec>> FactoryByKey =
            BuildFactories(CombatRegistries.Statuses());

        /// <summary>판별자의 원본은 행동 레지스트리다. 등록된 상태만 저작될 수 있고, 스펙 타입은
        /// 행동이 답한다 — 코드에 값 목록을 두지 않고도 다형 역직렬화가 성립한다.</summary>
        internal static Dictionary<string, Func<StatusSpec>> BuildFactories(StatusRegistry behaviors)
        {
            var table = new Dictionary<string, Func<StatusSpec>>();
            foreach (var key in behaviors.RegisteredKeys)
            {
                var behavior = behaviors.Resolve(key);
                var keyRef = StatusKeyRef.Of(key);
                table.Add(key.Id, () =>
                {
                    var created = behavior.NewSpec();
                    created.Key = keyRef;
                    return created;
                });
            }

            return table;
        }
```

`StatusContentDefaults`를 쓰던 중복 키 검사는 지운다 — 레지스트리가 이미 키로 색인된
사전이라 중복이 성립하지 않는다. `using FateWeaver.Core.Authoring.Statuses;`는 `StatusSpec`
때문에 남는다.

- [x] **Step 6: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0 (기준선 530 + 이 태스크의 새 테스트 1개). `StatusContentDefaults`는 아직 살아 있지만 컨버터는 더 이상 그것을 읽지 않는다.

- [x] **Step 7: 커밋**

```bash
git add Assets/Core/Status Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs Assets/Core/Tests/EditMode/StatusContentTests.cs && git commit -m "refactor: 상태 스펙 판별자를 행동 레지스트리에서 만든다"
```

---

### Task 2: 부팅이 상태를 가장 먼저 읽는다

**Files:**
- Modify: `Assets/Core/Authoring/ContentBootstrap.cs`
- Modify: `Assets/Core/Authoring/GameContent.cs`
- Modify: `Assets/Core/Authoring/Specs/ApplyStatusSpec.cs`
- Test: `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs`

**Interfaces:**
- Consumes: Task 1의 자립한 `StatusSpecJsonConverter`
- Produces: `ContentBootstrap.LoadStatuses(string contentRoot) → StatusContentLoadResult`,
  `GameContent.Statuses → StatusContentCatalog`

- [x] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/ContentBootstrapTests.cs`에 추가한다 (상단에
`using FateWeaver.Core.Status;` 추가):

```csharp
[Test]
public void BootstrapLoadsTheStatusCatalog()
{
    var content = ContentBootstrap.Load(ContentRoot()).Content;

    Assert.AreEqual(11, content.Statuses.Keys.Count);
    Assert.AreEqual("독", content.Statuses.DisplayNameOf(StatusKeys.Poison));
    Assert.AreEqual(1, content.Statuses.GrowthPerTurnOf(StatusKeys.Poison));
    Assert.AreEqual(2, content.Statuses.ExecutionOrderDeltaOf(StatusKeys.Slow));
}

[Test]
public void BootstrapReportsMissingStatusesBeforeReadingCards()
{
    var result = ContentBootstrap.Load(
        Path.Combine(Path.GetTempPath(), "fate-weaver-no-such-content"));

    Assert.IsFalse(result.Succeeded);
    StringAssert.Contains("Statuses", string.Join("\n", result.Errors));
}
```

- [x] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~ContentBootstrapTests
```

예상: 컴파일 실패 — `GameContent`에 `Statuses`가 없다.

- [x] **Step 3: `GameContent`가 상태를 갖는다**

`Assets/Core/Authoring/GameContent.cs`를 다음으로 바꾼다 (문서 주석의 "아직 묶지 않는다" 설명은
사라진다 — 계획 3c가 그 이유를 없앴다):

```csharp
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Statuses;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 1회로 만들어져 상주하는 콘텐츠 번들. 상태 규칙의 유일한 원본은
    /// Content/Statuses/*.json이며 여기 실려 전투·설명 양쪽에 같은 인스턴스로 주입된다.</summary>
    public sealed class GameContent
    {
        public GameContent(
            StatusContentCatalog statuses,
            CardContentCatalog cards,
            DeckContentCatalog decks,
            PoolContentCatalog pools,
            CharacterContentCatalog characters)
        {
            Statuses = statuses;
            Cards = cards;
            Decks = decks;
            Pools = pools;
            Characters = characters;
        }

        public StatusContentCatalog Statuses { get; }
        public CardContentCatalog Cards { get; }
        public DeckContentCatalog Decks { get; }
        public PoolContentCatalog Pools { get; }
        public CharacterContentCatalog Characters { get; }
    }
}
```

- [x] **Step 4: 부팅 순서를 상태 → 카드 → 덱·풀 → 캐릭터로 바꾼다**

`Assets/Core/Authoring/ContentBootstrap.cs`의 `using`에
`FateWeaver.Core.Authoring.Statuses;`를 추가하고, `Load`의 맨 앞에 상태 단계를 넣는다:

```csharp
        public static ContentBootstrapResult Load(string contentRoot)
        {
            var errors = new List<string>();

            // 상태가 가장 먼저다. 카드 검증이 "등록된 상태에는 저작된 콘텐츠가 있다"를 전제하므로
            // (ApplyStatusSpec), 그 전제를 세우는 단계가 앞서야 한다.
            var statuses = LoadStatuses(contentRoot);
            if (!statuses.Succeeded)
            {
                return ContentBootstrapResult.Failed(statuses.Errors);
            }

            var cards = CardContentLoader.Load(
                Read(contentRoot, CardContentFiles.CardsFolderName, errors),
                AuthoringContext.Default());
```

그리고 마지막 반환을 바꾼다:

```csharp
            return ContentBootstrapResult.Ok(new GameContent(
                statuses.Catalog, cards.Catalog, decks.Catalog, pools.Catalog, characters.Catalog));
```

같은 파일에 공개 헬퍼를 추가한다 — 테스트와 에디터 드롭다운이 상태만 따로 읽을 때 쓴다:

```csharp
        /// <summary>상태 카탈로그만 읽는다. 부팅의 첫 단계이자, 카탈로그 하나만 필요한 곳
        /// (에디터 드롭다운·테스트)의 단일 진입점이다.</summary>
        public static StatusContentLoadResult LoadStatuses(string contentRoot)
        {
            var errors = new List<string>();
            var sources = Read(contentRoot, CardContentFiles.StatusesFolderName, errors);
            return errors.Count > 0
                ? StatusContentLoadResult.Failed(errors)
                : StatusContentLoader.Load(sources, AuthoringContext.Default());
        }
```

- [x] **Step 5: 카드 쪽 중복 가드를 없앤다**

`Assets/Core/Authoring/Specs/ApplyStatusSpec.cs`의 `Validate`에서 마지막 `else if` 분기를 지운다.
`using FateWeaver.Core.Authoring.Statuses;`도 함께 지운다. 결과:

```csharp
        /// <summary>저작 콘텐츠 존재 여부는 검사하지 않는다. StatusContentLoader가 "등록된 모든
        /// 상태에 저작이 있다"를 요구하고 부팅이 상태를 카드보다 먼저 읽으므로, 여기 도달한
        /// 시점에는 HasStatus가 곧 저작 존재다 — 가드를 두면 같은 불변식을 두 곳에서 지키게 된다.</summary>
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
```

- [x] **Step 6: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 전부 통과. `ApplyStatusSpec`의 "has no authored content" 문구를 기대하던 테스트가 있으면
그 케이스를 지운다 — 다음으로 확인한다:

```bash
/usr/bin/grep -rn "has no authored content" --include='*.cs' Assets
```

- [x] **Step 7: 커밋**

```bash
git add Assets/Core/Authoring Assets/Core/Tests/EditMode/ContentBootstrapTests.cs && git commit -m "feat: 부팅이 상태 카탈로그를 가장 먼저 읽는다"
```

---

### Task 3: 테스트가 저장소 JSON에서 상태를 읽는다

코드 기본값을 지우기 전에, 그것을 대신할 **테스트용 진입점**을 세운다. 어셈블리가 둘이라
헬퍼도 둘이다 — `FateWeaver.Tests.UnityEditMode`는 `FateWeaver.Tests.EditMode`를 참조하지 않는다.

**Files:**
- Create: `Assets/Core/Tests/EditMode/TestContent.cs`
- Create: `Assets/Tests/UnityEditMode/UnityTestContent.cs`
- Modify: `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs`,
  `DeckPoolCharacterContentTests.cs`, `CardContentEquivalenceJsonTests.cs`,
  `ContentDrivenLoadoutTests.cs`, `ContentExportWriterTests.cs` — 각자 복사해 둔
  `ContentRoot()` 걷기 로직을 `TestContent.Root()`로 대체

**Interfaces:**
- Consumes: Task 2의 `ContentBootstrap.LoadStatuses`
- Produces: `TestContent.Root() → string`, `TestContent.Statuses() → StatusContentCatalog`,
  `UnityTestContent.Statuses() → StatusContentCatalog`

- [x] **Step 1: 코어 테스트 헬퍼를 만든다**

`Assets/Core/Tests/EditMode/TestContent.cs`:

```csharp
using System.IO;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Statuses;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>테스트가 저장소의 커밋된 콘텐츠를 읽는 단 하나의 진입점. 상태 규칙의 원본이
    /// JSON뿐이므로 테스트도 거기서 읽는다 — 코드 기본값을 되살리지 않는다.</summary>
    public static class TestContent
    {
        private static StatusContentCatalog _statuses;

        /// <summary>Assets 폴더가 보일 때까지 올라가 콘텐츠 루트를 찾는다. 테스트 실행 디렉터리는
        /// 헤드리스(bin/...)와 Unity(Library/...)가 다르므로 경로를 박지 않는다.</summary>
        public static string Root()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return Path.Combine(directory, "Assets", "StreamingAssets", "Content");
        }

        /// <summary>파일에서 만든 상태 카탈로그. 한 번 읽어 재사용한다.</summary>
        public static StatusContentCatalog Statuses()
        {
            if (_statuses == null)
            {
                var result = ContentBootstrap.LoadStatuses(Root());
                Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
                _statuses = result.Catalog;
            }

            return _statuses;
        }
    }
}
```

- [x] **Step 2: Unity 테스트 헬퍼를 만든다**

`Assets/Tests/UnityEditMode/UnityTestContent.cs`:

```csharp
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Unity;
using NUnit.Framework;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>Unity EditMode 테스트의 콘텐츠 진입점. 코어 테스트 어셈블리를 참조하지 않으므로
    /// 루트는 프로덕션의 UnityContentRoot에서 받는다 — 경로 상수를 새로 만들지 않는다.</summary>
    public static class UnityTestContent
    {
        private static StatusContentCatalog _statuses;

        public static StatusContentCatalog Statuses()
        {
            if (_statuses == null)
            {
                var result = ContentBootstrap.LoadStatuses(UnityContentRoot.Path);
                Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
                _statuses = result.Catalog;
            }

            return _statuses;
        }
    }
}
```

- [x] **Step 3: 중복된 `ContentRoot()`를 걷어낸다**

다음 다섯 파일에서 각자의 `private static string ContentRoot()`(또는 같은 걷기 로직)를 지우고
호출부를 `TestContent.Root()`로 바꾼다:

```bash
/usr/bin/grep -rn "Directory.Exists(Path.Combine(directory" --include='*.cs' Assets/Core/Tests/EditMode
```

`ContentExportWriterTests`의 `[Explicit]` 내보내기 테스트도 같은 헬퍼를 쓴다.

- [x] **Step 4: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0, 총계는 기준선 530 그대로 (동작 변경 없음, 경로 조회 경로만 하나로 모였다).

- [x] **Step 5: 커밋**

```bash
git add Assets/Core/Tests/EditMode Assets/Tests/UnityEditMode && git commit -m "test: 콘텐츠 루트 조회를 TestContent 하나로 모은다"
```

---

### Task 4: 세션과 하니스가 상태 카탈로그를 주입받는다

`CombatState`의 기본값을 지우기 **전에** 모든 생성 경로에 카탈로그가 흐르게 만든다. 이 태스크가
끝난 시점에도 기본값은 살아 있으므로 트리는 계속 초록이다.

**Files:**
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`, `ScenarioRunner.cs`,
  `MultiTurnRunner.cs`, `PlaytestSession.cs`, `MultiTurnPlaytestSession.cs`,
  `ScenarioCliReport.cs`
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: 호출부 테스트 (`new DeckCombatSession(` 24곳, 하니스 26곳)

**Interfaces:**
- Consumes: `TestContent.Statuses()`, `UnityTestContent.Statuses()`, `GameContent.Statuses`
- Produces: 아래 시그니처들. **첫 파라미터**로 넣는다 — 컴파일러가 모든 호출부를 잡게 하려는
  의도이며, 선택 파라미터 뒤에 붙일 수 없기 때문이기도 하다.

```csharp
public DeckCombatSession(
    StatusContentCatalog statusContent,
    IReadOnlyList<OwnedCard> deckCards, int playerHp, IReadOnlyList<Enemy> enemies,
    IEnemyTurnPolicy enemyPolicy, int fateEnergyPerTurn = 3, int handSize = 5, int seed = 0)

public DeckCombatSession(
    StatusContentCatalog statusContent,
    IReadOnlyList<PartyMemberLoadout> party, IReadOnlyList<Enemy> enemies,
    IEnemyTurnPolicy enemyPolicy, PartyTuning tuning,
    IReadOnlyList<CardDefinition> partyCards = null, int fateEnergyPerTurn = 3, int seed = 0)

public ScenarioRunner(StatusContentCatalog statusContent)
public MultiTurnRunner(StatusContentCatalog statusContent)
public PlaytestSession(ScenarioDefinition scenario, StatusContentCatalog statusContent)
public MultiTurnPlaytestSession(MultiTurnScenario scenario, StatusContentCatalog statusContent)
public static string ScenarioCliReport.Build(string scenarioId, StatusContentCatalog statusContent)
```

- [x] **Step 1: `DeckCombatSession`이 카탈로그를 받는다**

두 공개 생성자와 비공개 생성자에 `StatusContentCatalog statusContent`를 첫 파라미터로 추가하고
(`using FateWeaver.Core.Authoring.Statuses;` 추가), 비공개 생성자의 상태 생성을 바꾼다:

```csharp
            _state = new CombatState
            {
                StatusContent = statusContent
                    ?? throw new ArgumentNullException(nameof(statusContent)),
                FateEnergyPerTurn = fateEnergyPerTurn,
                RngSeed = seed
            };
```

두 공개 생성자의 `: this(...)` 전달 목록 맨 앞에도 `statusContent`를 넣는다.

- [x] **Step 2: 하니스 넷이 카탈로그를 받는다**

`ScenarioRunner`·`MultiTurnRunner`는 생성자와 `private readonly StatusContentCatalog _statusContent;`
필드를 추가하고, `new CombatState { ... }`에 `StatusContent = _statusContent,`를 넣는다.
`ScenarioRunner.BuildState`는 지금 `static`이므로 인스턴스 메서드로 바꾼다.

`PlaytestSession`·`MultiTurnPlaytestSession`은 생성자에 파라미터를 추가하고 같은 방식으로 넣는다.

`ScenarioCliReport.Build`는 카탈로그를 받아 두 러너에 그대로 넘긴다:

```csharp
        public static string Build(string scenarioId, StatusContentCatalog statusContent)
        {
            if (SampleMultiTurnScenarios.TryFind(scenarioId, out var multiTurnScenario))
            {
                var comparison = new MultiTurnRunner(statusContent).Compare(multiTurnScenario);
                return MultiTurnComparisonReport.ToMarkdown(comparison);
            }

            var singleTurnComparison = new ScenarioRunner(statusContent).Compare(
                SampleScenarios.Find(scenarioId));
            return ScenarioComparisonReport.ToMarkdown(singleTurnComparison);
        }
```

- [x] **Step 3: Unity 런타임이 부팅 카탈로그를 넘긴다**

`Assets/Unity/BattleScreenController.cs`의 `StartSession`에서 세션 생성을 바꾼다:

```csharp
            _session = new DeckCombatSession(
                _content.Statuses,
                loadouts,
                enemies,
                GoblinDeck.Policy(),
                tuning,
                partyCards: null,
                fateEnergyPerTurn: FateEnergyPerTurn,
                seed: Seed);
```

- [x] **Step 4: 테스트 호출부를 기계적으로 고친다**

저장소 루트에서 실행한다 (macOS `sed`이므로 `-i ''`):

```bash
/usr/bin/grep -rl "new DeckCombatSession(" Assets/Core/Tests/EditMode | xargs sed -i '' 's/new DeckCombatSession(/new DeckCombatSession(TestContent.Statuses(), /g'
```

```bash
/usr/bin/grep -rl "new DeckCombatSession(" Assets/Tests/UnityEditMode | xargs sed -i '' 's/new DeckCombatSession(/new DeckCombatSession(UnityTestContent.Statuses(), /g'
```

```bash
/usr/bin/grep -rl "new ScenarioRunner()\|new MultiTurnRunner()" Assets/Core/Tests/EditMode | xargs sed -i '' -e 's/new ScenarioRunner()/new ScenarioRunner(TestContent.Statuses())/g' -e 's/new MultiTurnRunner()/new MultiTurnRunner(TestContent.Statuses())/g'
```

`PlaytestSession`·`MultiTurnPlaytestSession`·`ScenarioCliReport.Build`는 호출부가 적으므로
컴파일 오류를 보고 손으로 고친다. 줄바꿈된 `new DeckCombatSession(` 호출에서 인자가 첫 줄에
붙어 어색해지면 포맷만 정리한다 — 동작은 그대로다.

- [x] **Step 5: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0. 이 시점에도 `CombatState`의 기본값은 살아 있고, 주입된 카탈로그가 그것을
덮어쓴다 — 값이 같으므로 동작은 변하지 않는다.

- [x] **Step 6: 커밋**

```bash
git add Assets && git commit -m "refactor: 세션과 하니스가 상태 카탈로그를 주입받는다"
```

---

### Task 5: `CombatState`의 코드 기본값을 없앤다

**Files:**
- Modify: `Assets/Core/Combat/CombatState.cs`
- Modify: `new CombatState`를 쓰는 나머지 전부 (2026-08-04 실측 127곳 / 32파일)

**Interfaces:**
- Produces: `CombatState(StatusContentCatalog statusContent)` — 유일한 생성자.
  `StatusContent`는 읽기 전용 프로퍼티가 된다.

- [x] **Step 1: 생성자를 필수로 만든다**

`Assets/Core/Combat/CombatState.cs`에서 `StatusContent` 프로퍼티를 바꾸고 생성자를 추가한다:

```csharp
        /// <summary>이 전투의 상태 저작 콘텐츠. 규칙(배율)과 수명 종류의 단일 출처이며 원본은
        /// Content/Statuses/*.json이다. 전투 단위로 존재하므로 전투 중 변경이 런으로 새지 않는다 —
        /// 런 지속 변경(유물 등)은 전투 시작 전에 카탈로그를 만들어 넘기는 방식으로 반영한다.</summary>
        public Authoring.Statuses.StatusContentCatalog StatusContent { get; }

        /// <summary>상태 콘텐츠 없이는 전투가 성립하지 않는다 — 규칙 수치가 전부 거기 있다.
        /// 기본값을 두면 코드가 JSON과 같은 값을 두 벌 갖게 되므로 생성자에서 요구한다.</summary>
        public CombatState(Authoring.Statuses.StatusContentCatalog statusContent)
        {
            StatusContent = statusContent
                ?? throw new ArgumentNullException(nameof(statusContent));
        }
```

- [x] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 컴파일 실패 다수 — `new CombatState()`에 인자가 없다.

- [x] **Step 3: 하니스·세션을 고친다**

Task 4에서 이미 필드로 들고 있으므로 생성만 바꾼다. 다섯 파일에서
`new CombatState { StatusContent = X, ... }` → `new CombatState(X) { ... }`,
`new CombatState()` → `new CombatState(X)` (X는 각 클래스가 주입받은 카탈로그).

- [x] **Step 4: 테스트 호출부를 기계적으로 고친다**

```bash
/usr/bin/grep -rl "new CombatState" Assets/Core/Tests/EditMode | xargs sed -i '' -e 's/new CombatState()/new CombatState(TestContent.Statuses())/g' -e 's/new CombatState {/new CombatState(TestContent.Statuses()) {/g'
```

```bash
/usr/bin/grep -rl "new CombatState" Assets/Tests/UnityEditMode | xargs sed -i '' -e 's/new CombatState()/new CombatState(UnityTestContent.Statuses())/g' -e 's/new CombatState {/new CombatState(UnityTestContent.Statuses()) {/g'
```

남은 형태(`new CombatState\n{`처럼 줄바꿈된 것)는 컴파일 오류로 드러나므로 손으로 고친다.
다음으로 잔여를 확인한다:

```bash
/usr/bin/grep -rn "new CombatState" --include='*.cs' Assets | /usr/bin/grep -v "TestContent.Statuses()\|_statusContent\|statusContent"
```

- [x] **Step 5: 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 실패 0. 이제 전투 규칙 수치는 JSON에서만 온다.

- [x] **Step 6: 커밋**

```bash
git add Assets && git commit -m "refactor: CombatState가 상태 콘텐츠를 생성자에서 요구한다"
```

---

### Task 6: 설명 카탈로그의 전역 싱글턴을 없앤다

`KoreanDescriptionCatalog.Default`는 정적 초기화 시점에 `StatusContentDefaults`를 읽는 마지막
경로다. 이걸 지워야 코드 기본값을 삭제할 수 있다.

**Files:**
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Modify: `Assets/Unity/CardPresentation.cs`, `BattlePresenter.cs`, `BattleScreenController.cs`,
  `BattleUnitsView.cs`, `UnitView.cs`, `PlaytestKoreanText.cs`
- Modify: `Assets/Unity/Editor/StatusKeyDropdownOptions.cs`
- Modify: `Assets/Tests/UnityEditMode/CardPresentationTests.cs`, `PlaytestKoreanTextTests.cs`
- Modify: `Assets/Core/Tests/EditMode/` 중 `KoreanDescriptionCatalog.CreateDefault()`를 쓰는 6파일

**Interfaces:**
- Consumes: `GameContent.Statuses`, `UnityTestContent.Statuses()`, `TestContent.Statuses()`
- Produces:
  - `CardPresentation.From(ExecutionCardInstance card, KoreanDescriptionCatalog korean, Func<string, Sprite> art = null, string ownerDisplayName = null, Color ownerColor = default, bool isPartyOwned = false)`
  - `CardPresentation.FromDefinition(CardDefinition def, KoreanDescriptionCatalog korean, ...)` (같은 꼬리 파라미터)
  - `BattlePresenter.Initialize(Func<string, string> ownerName, KoreanDescriptionCatalog korean)`
  - `BattleUnitsView.Spawn(CombatState state, Func<string, Color> colorFor, Func<string, string> enemyNameFor, Func<StatusKey, string> statusNameFor)`
  - `UnitView.SetStatuses(IReadOnlyList<StatusInstance> statuses, Func<StatusKey, string> nameFor)`

- [x] **Step 1: 전역과 무인자 오버로드를 지운다**

`KoreanDescriptionCatalog.cs`에서 다음 둘을 삭제한다:

```csharp
        public static readonly KoreanDescriptionCatalog Default = CreateDefault();
```

```csharp
        /// <summary>코드 기본값 카탈로그를 쓰는 편의 오버로드.</summary>
        public static KoreanDescriptionCatalog CreateDefault()
            => CreateDefault(StatusContentDefaults.Catalog());
```

`using FateWeaver.Core.Authoring.Statuses;`는 파라미터 타입 때문에 남는다.

- [x] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 컴파일 실패 — 코어 테스트 6파일이 무인자 `CreateDefault()`를 부른다.

- [x] **Step 3: 코어 테스트를 카탈로그 주입으로 바꾼다**

```bash
/usr/bin/grep -rl "KoreanDescriptionCatalog.CreateDefault()" Assets/Core/Tests/EditMode | xargs sed -i '' 's/KoreanDescriptionCatalog.CreateDefault()/KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses())/g'
```

정규화된 이름으로 부르는 곳(`FateWeaver.Simulation.Descriptions.KoreanDescriptionCatalog.CreateDefault()`)도
있으므로 다음으로 잔여를 확인하고 손으로 고친다:

```bash
/usr/bin/grep -rn "CreateDefault()" --include='*.cs' Assets
```

- [x] **Step 4: 카드 표현이 카탈로그를 받는다**

`Assets/Unity/CardPresentation.cs`의 두 팩토리에 `KoreanDescriptionCatalog korean`을 두 번째
파라미터로 추가하고, 본문의 `KoreanDescriptionCatalog.Default`를 `korean`으로 바꾼다:

```csharp
        public static CardPresentation From(
            ExecutionCardInstance card,
            KoreanDescriptionCatalog korean,
            Func<string, Sprite> art = null,
            string ownerDisplayName = null,
            Color ownerColor = default,
            bool isPartyOwned = false)
        {
            var def = card.Def;
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                card.ExecutionOrder,
                def.EnergyCost,
                def.Side,
                DescriptionComposer.Compose(def, korean),
                ResolveArt(def.Id, art),
                card.IsLocked,
                StatusIconsFor(card),
                def.Category,
                ownerDisplayName,
                ownerColor,
                isPartyOwned);
        }
```

`FromDefinition`도 같은 자리에 `KoreanDescriptionCatalog korean`을 넣고
`DescriptionComposer.Compose(def, korean)`으로 바꾼다.

(합성 진입점은 카드 프레임 작업이 `Describe`에서 `Compose`로 바꿨다. 착수 시점에
`DescriptionComposer`의 현재 메서드 이름을 확인한다.)

- [x] **Step 5: `BattlePresenter`가 카탈로그를 들고 넘긴다**

`Assets/Unity/BattlePresenter.cs`에 필드와 초기화를 추가한다 (`using FateWeaver.Simulation.Descriptions;` 추가):

```csharp
        private KoreanDescriptionCatalog _korean;

        /// <summary>세션의 파티에서 표시명을 읽는 델리게이트와, 부팅 콘텐츠로 만든 설명 카탈로그를
        /// 주입한다. 카드 본문의 상태 이름이 전투 규칙과 같은 JSON에서 오게 하는 지점이다.</summary>
        public void Initialize(Func<string, string> ownerName, KoreanDescriptionCatalog korean)
        {
            _ownerName = ownerName;
            _korean = korean;
        }
```

`For` 둘의 호출을 바꾼다:

```csharp
            return CardPresentation.FromDefinition(card.Def, _korean, ArtFor, name, color, isPartyOwned);
```

```csharp
            return CardPresentation.From(card, _korean, ArtFor, name, color, isPartyOwned);
```

- [x] **Step 6: 유닛의 상태 이름을 주입으로 바꾼다**

`Assets/Unity/PlaytestKoreanText.cs`에서 두 메서드를 삭제한다:

```csharp
        public static string StatusName(StatusKey key)
            => KoreanDescriptionCatalog.Default.Statuses.Resolve(key);

        public static string InterventionActionName(InterventionActionKey key)
            => KoreanDescriptionCatalog.Default.Interventions.Resolve(key).DisplayName;
```

`InterventionActionName`은 프로덕션 호출자가 없다(2026-08-04 실측: 자기 테스트뿐). 되살릴 필요가
생기면 카탈로그를 받는 형태로 새로 만든다. `Assets/Tests/UnityEditMode/PlaytestKoreanTextTests.cs`에서
이 메서드를 쓰는 케이스를 지운다. 쓰이지 않게 된 `using`도 정리한다.

`Assets/Unity/UnitView.cs`의 `SetStatuses`가 이름 조회를 받는다:

```csharp
        public void SetStatuses(
            IReadOnlyList<StatusInstance> statuses, Func<StatusKey, string> nameFor)
        {
            if (_statusText == null)
            {
                return;
            }

            var parts = new List<string>();
            if (statuses != null)
            {
                foreach (var status in statuses)
                {
                    int value = status.Magnitude > 0 ? status.Magnitude : status.Count;
                    var name = nameFor(status.Key);
                    parts.Add(value > 0 ? name + "(" + value + ")" : name);
                }
            }

            _statusText.text = string.Join(" · ", parts);
        }
```

(`using System;`이 없으면 추가한다.)

`Assets/Unity/BattleUnitsView.cs`가 그 조회를 소유한다 — `UnitView`의 유일한 호출자이므로
(설계 §4.6) 여기서 멈춘다. 필드와 `Spawn` 파라미터를 추가하고 `Refresh`에서 쓴다:

```csharp
        private Func<StatusKey, string> _statusNameFor;

        public void Spawn(
            CombatState state,
            Func<string, Color> colorFor,
            Func<string, string> enemyNameFor,
            Func<StatusKey, string> statusNameFor)
        {
            _statusNameFor = statusNameFor;
```

`Refresh`의 두 `view.SetStatuses(...)` 호출을 바꾼다:

```csharp
                    view.SetStatuses(member.Statuses.All, _statusNameFor);
```

```csharp
                    view.SetStatuses(enemy.Statuses.All, _statusNameFor);
```

(`using FateWeaver.Core.Status;` 추가.)

- [x] **Step 7: 컨트롤러가 부팅 콘텐츠로 배선한다**

`Assets/Unity/BattleScreenController.cs`의 `StartSession`에서 세션 생성 뒤 배선을 바꾼다
(`using FateWeaver.Simulation.Descriptions;` 추가):

```csharp
            var korean = KoreanDescriptionCatalog.CreateDefault(_content.Statuses);
            _presenter.Initialize(OwnerNameOf, korean);
            _units.Spawn(
                _session.State,
                _presenter.OwnerColor,
                id => PlaytestKoreanText.EnemyName(id, id),
                key => _content.Statuses.DisplayNameOf(key));
```

- [x] **Step 8: 에디터 드롭다운이 JSON을 읽는다**

`Assets/Unity/Editor/StatusKeyDropdownOptions.cs`의 `CreateDefault`를 바꾼다
(`using FateWeaver.Unity;` 추가):

```csharp
        /// <summary>라벨의 원본도 JSON이다. 상태만 필요하므로 부팅 전체가 아니라
        /// ContentBootstrap.LoadStatuses를 쓴다.</summary>
        public static StatusKeyDropdownModel CreateDefault(string currentId)
        {
            var authoring = AuthoringContext.Default();
            var statuses = ContentBootstrap.LoadStatuses(UnityContentRoot.Path);
            return Create(
                currentId,
                authoring.RegisteredStatusKeys,
                KoreanDescriptionCatalog.CreateDefault(statuses.Catalog).Statuses);
        }
```

로드 실패 시 `statuses.Catalog`가 `null`이라 `CreateDefault`가 던진다 — 에디터에서 콘텐츠가
깨졌다는 사실이 드러나야 하므로 삼키지 않는다.

- [x] **Step 9: Unity 테스트를 고친다**

`Assets/Tests/UnityEditMode/CardPresentationTests.cs`의 여덟 호출에 카탈로그를 넣는다.
카탈로그는 **둘째** 인자이므로 일괄 치환이 아니라 손으로 고친다 — 첫 인자가 카드다.
파일 안에 공용 필드를 둔다 (`using FateWeaver.Simulation.Descriptions;` 추가):

```csharp
        private static readonly KoreanDescriptionCatalog Korean =
            KoreanDescriptionCatalog.CreateDefault(UnityTestContent.Statuses());
```

호출부는 다음 형태가 된다:

```csharp
            var presentation = CardPresentation.From(instance, Korean);
```

```csharp
            var presentation = CardPresentation.FromDefinition(EnemyCard(), Korean);
```

- [x] **Step 10: 헤드리스와 Unity EditMode를 모두 돌린다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-status-content -runTests -testPlatform EditMode -testResults /private/tmp/status-3c-task6.xml -logFile /private/tmp/status-3c-task6.log
```

예상: 헤드리스 실패 0, Unity EditMode는 삭제한 `InterventionActionName` 케이스 수만큼 총계가
줄고 failed=0. 실행 뒤 `git status`로 폰트 아틀라스 같은 런타임 부산물이 섞이지 않았는지 확인한다.

- [x] **Step 11: 커밋**

```bash
git add Assets && git commit -m "refactor: 설명 카탈로그를 전역 대신 주입으로 받는다"
```

---

### Task 7: `StatusContentDefaults`를 지우고 색인을 갱신한다

**Files:**
- Delete: `Assets/Core/Authoring/Statuses/StatusContentDefaults.cs` (+ `.cs.meta`)
- Modify: `Assets/Core/Authoring/Json/ContentExportWriter.cs`
- Modify: `Assets/Core/Tests/EditMode/StatusContentTests.cs`, `SlowHasteStatusTests.cs`,
  `StatusTests.cs`, `DescriptionComposerTests.cs`, `ContentExportWriterTests.cs`
- Modify: `docs/superpowers/README.md`, `docs/superpowers/plans/2026-08-04-status-content-single-source.md`

- [x] **Step 1: 남은 참조를 테스트 헬퍼로 바꾼다**

```bash
/usr/bin/grep -rn "StatusContentDefaults" --include='*.cs' Assets
```

- `SlowHasteStatusTests.cs:18`, `StatusTests.cs:204,211`, `DescriptionComposerTests.cs:195`,
  `StatusContentTests.cs:68` → `TestContent.Statuses()`
- `StatusContentTests.cs:19,62` → `TestContent.Statuses().Keys`로 다시 쓴다. `Specs()`를 순회하던
  검사는 카탈로그 질의로 바꾼다 (예: 모든 키에 표시명이 있다).
- `ContentExportWriterTests.cs:56,87` → 상태를 더 이상 내보내지 않으므로 해당 단언을 지우고,
  아래 Step 2의 새 단언으로 대체한다.

- [x] **Step 2: 내보내기에서 상태를 뺀다**

`Assets/Core/Authoring/Json/ContentExportWriter.cs`의 `WriteAll`에서 상태 루프를 지운다:

```csharp
            foreach (var spec in StatusContentDefaults.Specs())
            {
                written.Add(Write(
                    rootDirectory, CardContentFiles.StatusesFolderName, spec.Key.Id, spec));
            }
```

`using FateWeaver.Core.Authoring.Statuses;`도 지운다. 클래스 문서 주석에서 상태를 "C# 스펙이
여전히 온전한 원본"이라고 적은 문장을 고친다 — 이제 카드와 상태 **둘 다** JSON이 원본이고,
남은 것은 덱·풀·캐릭터의 id 목록뿐이다(계획 3d가 지운다).

`ContentExportWriterTests`에 카드와 같은 형태의 회귀 잠금을 추가한다:

```csharp
[Test]
public void WriteAllDoesNotTouchStatuses()
{
    var directory = NewTempDirectory();

    ContentExportWriter.WriteAll(directory, Characters());

    Assert.IsFalse(
        Directory.Exists(Path.Combine(directory, CardContentFiles.StatusesFolderName)),
        "상태의 원본은 JSON이다 — 다시 쓰면 저작이 지워진다.");
}
```

(`NewTempDirectory()`·`Characters()`는 기존 테스트의 헬퍼 이름에 맞춘다. 없으면 기존 테스트가
쓰는 방식 그대로 임시 디렉터리와 캐릭터 목록을 만든다.)

- [x] **Step 3: 파일을 지운다**

```bash
git rm Assets/Core/Authoring/Statuses/StatusContentDefaults.cs Assets/Core/Authoring/Statuses/StatusContentDefaults.cs.meta
```

- [x] **Step 4: 잔여가 없음을 확인한다**

```bash
/usr/bin/grep -rn "StatusContentDefaults" --include='*.cs' Assets
```

예상: 출력 없음.

- [x] **Step 5: 헤드리스와 Unity EditMode를 모두 돌린다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-status-content -runTests -testPlatform EditMode -testResults /private/tmp/status-3c-final.xml -logFile /private/tmp/status-3c-final.log
```

예상: 둘 다 failed=0. 실제 총계를 다음 단계에서 문서에 적는다.

- [x] **Step 6: 문서를 갱신한다 (규칙 20)**

`docs/superpowers/README.md`에서:
- "진행 중인 작업 흐름" 표의 3c 행을 **완료**로 바꾸고 이 계획 문서를
  `archive/plans/`로 옮긴 경로로 링크한다.
- "새 세션이 먼저 알아야 할 함정 셋"의 3번과 "넘어온 부채"의 마지막 항목("상태 JSON이 코드
  기본값 없이는 파싱되지 않는다")을 해결됨으로 고친다.
- "현재 수치"의 테스트 총계를 Step 5의 실측으로 갱신한다.
- "활성 계획과 로드맵" 표에서 이 계획 행을 지운다.

이 계획 문서를 `docs/superpowers/archive/plans/`로 옮기고 머리말의 상태를 `archived`로 바꾼다.
`docs/superpowers/archive/README.md`에도 한 줄 추가한다.

- [x] **Step 7: 커밋**

```bash
git add -A && git commit -m "refactor: 상태 규칙의 코드 기본값을 지우고 JSON을 유일 원본으로 확정한다"
```

---

## 완료 기준

1. `/usr/bin/grep -rn "StatusContentDefaults" --include='*.cs' Assets`가 아무것도 찾지 못한다.
2. `KoreanDescriptionCatalog`에 전역 `Default`도 무인자 `CreateDefault()`도 없다.
3. `new CombatState(...)`가 상태 카탈로그 없이는 컴파일되지 않는다.
4. 헤드리스와 Unity EditMode가 모두 failed=0.
5. `Assets/StreamingAssets/Content/Statuses/*.json` 11개가 상태 규칙의 유일한 원본이다.

## 이 계획이 열어주는 것

- **계획 3d (C# 카드 스펙 제거)** — 독립이지만, `ContentExportWriter`가 상태를 잃으면서
  덱·풀·캐릭터 세 항목만 남아 삭제 범위가 좁아진다.
- **[상태 규칙 파라미터화와 3종 디버프](2026-07-30-status-rule-and-debuffs.md)** — 새 상태를
  JSON 파일 하나 + 행동 클래스 하나로 추가하게 된다. 코드에 값을 두 벌 적지 않는다.
- **런 지속 변경(유물)** — `CombatState`가 카탈로그를 생성자에서 받으므로, 전투 시작 전에
  수정한 카탈로그를 넘기는 것만으로 런 단위 규칙 변경이 성립한다.

## 범위 밖

- **적 카드의 JSON 전환.** `GoblinDeck`·`WardenDeck`은 여전히 순수 C#이다. 적 정책·행동 패턴
  설계가 딸려 오므로 별도 계획이다.
- **`StatusRule`의 확장.** 방어 흡수 층 분리와 배율의 런타임 조절은
  [상태 규칙 파라미터화 계획](2026-07-30-status-rule-and-debuffs.md)의 몫이다.
- **`ContentExportWriter` 삭제.** 계획 3d가 한다.
