# 카드 콘텐츠 JSON 직렬화·로딩 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-07-31
- 상태: `active` — 미착수
- 권위 문서: [`specs/2026-07-30-card-mutation-and-runtime-content-design.md`](../specs/2026-07-30-card-mutation-and-runtime-content-design.md)
- 브랜치: `claude/card-mutation-runtime-content-a65c58`

**Goal:** 카드 저작 데이터를 JSON으로 직렬화·역직렬화하고, 디렉터리에서 읽어 `CardDefinition`
사전으로 만드는 로딩 경로를 세운다. 기존 C# 스펙과 결과가 동일함을 헤드리스로 증명한다.

**Architecture:** `EffectSpec`의 다형 직렬화를 Newtonsoft `JsonConverter`로 처리하되, 타입 판별자는
각 스펙이 이미 갖고 있는 `EffectKey.Id`를 쓰고 타입 표는 기존 `EffectSpecCatalog`에서 만든다(리플렉션
스캔 없음). 로더는 파일 I/O를 받지 않고 `(이름, 본문)` 쌍을 받아 순수하게 동작하므로, 헤드리스
테스트가 임시 파일 없이 검증할 수 있고 코어의 UnityEngine 미참조가 유지된다.

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), NUnit, Newtonsoft.Json,
`FateWeaver.Core`(UnityEngine 미참조)

## Global Constraints

- 헤드리스 테스트 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- 착수 시점 기준선: **409 tests, 0 failed**
- `FateWeaver.Core`에서 `UnityEngine`을 참조하지 않는다 (AGENTS.md 규칙 6). Newtonsoft.Json은 순수
  관리 어셈블리이므로 이 제약을 깨지 않는다
- 새 효과·변형 종류는 클래스 1개 + 명시적 카탈로그 등록. 리플렉션 자동 등록 금지 (규칙 9)
- 카드 설명을 하드코딩하지 않는다. 설명은 `DescriptionComposer`가 EffectData에서 생성한다 (규칙 10)
- C# 9 한계: `record struct` 금지, 기본 인터페이스 구현 금지, 파일 범위 네임스페이스 금지
- 이 워크트리에서 Unity GUI Editor를 열지 않는다 (규칙 17). Unity EditMode 검증이 필요한 항목은
  각 Task에 `-batchmode` 명령으로 명시한다
- 새 Unity 에셋에는 1:1 대응하는 `.meta` 파일을 함께 커밋한다

## 이 계획의 경계

**포함:** 직렬화, 로더, 익스포터, 동등성 증명. 기존 C# 콘텐츠 경로는 **살아 있는 채로 둔다.**

**제외 (후속 계획):**

| 계획 | 내용 |
|---|---|
| 계획 2 — 콘텐츠 원본 전환 | 소비자를 JSON으로 전환하고 `StarterDeckSpecs`·`StarterPoolSpecs`·`PartyPrototypeDeckSpecs`·`GeneratedCards.cs`·`CardCodeGenerator.cs` 제거. 이 파일들의 카드별 팩터리 메서드를 참조하는 테스트 약 10개를 id 조회로 전환한다 |
| 계획 3 — 카드 변형 | `CardMutation` 5종, `CardMutationPipeline`, `OwnedCard`의 `Source`/`Permanent`/`Combat`/`Effective`, `ExecutionCardInstance(OwnedCard)`, `RunMember.Cards` 전환, 강화 효과·개입 핸들러 |
| 계획 4 — 저작 도구 | 효과 스키마 내보내기와 `Tools/card-idea-notebook`의 구조화 효과 편집기·JSON 내보내기 |

계획 2를 이 계획에서 분리한 이유는, 전환 대상 테스트가 `StarterPoolSpecs.VenomThrust()` 같은 카드별
팩터리 메서드를 직접 부르고 있어 전환 자체가 독립적인 검토 단위이기 때문이다. 이 계획이 끝나면 JSON
경로가 완성되고 기존 경로와 동등함이 증명되므로, 계획 2는 순수한 전환 작업이 된다.

## 파일 구조

| 경로 | 책임 |
|---|---|
| `Assets/Core/Authoring/` (이동) | `CardSpec`, `EffectSpec` 계열, `EffectSpecCatalog`, `AuthoringContext`, `AuthoringValidator`, `CardSpecMapper`, 키 참조 구조체 |
| `Assets/Core/Registries/CombatRegistries.cs` (이동) | 기본 효과·상태·개입 레지스트리. 코어 핸들러만 등록하므로 코어에 속한다 |
| `Assets/Core/Authoring/Json/ContentJson.cs` | 직렬화 설정 단일 지점(`Settings`, `Plain`) |
| `Assets/Core/Authoring/Json/EffectSpecJsonConverter.cs` | `EffectSpec` 다형 컨버터. 판별자 = `EffectKey.Id` |
| `Assets/Core/Authoring/Json/KeyRefJsonConverters.cs` | `StatusKeyRef`·`InterventionKeyRef`를 평범한 문자열로 |
| `Assets/Core/Authoring/CardContentSource.cs` | `(Name, Json)` 쌍. 로더의 입력 단위 |
| `Assets/Core/Authoring/CardContentLoader.cs` | 소스 목록 → 검증 → `CardContentCatalog` 또는 오류 목록 |
| `Assets/Core/Authoring/CardContentCatalog.cs` | `id → CardDefinition` 사전 |
| `Assets/Core/Authoring/CardContentFiles.cs` | 디렉터리에서 `*.json`을 읽어 소스 목록으로. 파일 I/O를 로더 밖에 격리 |
| `Assets/Unity/Editor/CardContentExporter.cs` | 기존 C# 스펙 → `Assets/StreamingAssets/Content/Cards/*.json` 1회 변환 |
| `Assets/Core/Tests/EditMode/CardContentJsonTests.cs` | 왕복 테스트 |
| `Assets/Core/Tests/EditMode/CardContentLoaderTests.cs` | 로더·검증·오류 보고 테스트 |
| `Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs` | JSON 로드 결과 == 기존 C# 스펙 |

---

## Task 1: 저작 기반을 코어로 옮긴다

`OwnedCard`(`FateWeaver.Core`)가 변형 목록에 `EffectSpec`(`FateWeaver.Simulation`)을 담아야 하는데
참조 방향이 반대다(스펙 §4.4). 계획 3이 그 위에 서므로 지금 옮긴다. 이 Task는 순수 이동이며, 기존
409개 테스트가 그대로 통과하는 것이 검증이다.

**Files:**
- Move: `Assets/Core/Simulation/Authoring/` → `Assets/Core/Authoring/` (19개 `.cs` + 각 `.meta`)
- Move: `Assets/Core/Simulation/CombatRegistries.cs` → `Assets/Core/Registries/CombatRegistries.cs`
- Modify: `Assets/Core/Registries/CombatRegistries.cs` (namespace, `internal` → `public`)
- Modify: 네임스페이스를 참조하는 47개 파일 (sed 일괄)

**Interfaces:**
- Produces: 네임스페이스 `FateWeaver.Core.Authoring` (`CardSpec`, `EffectSpec`, `EffectSpecCatalog`,
  `EffectSpecInfo`, `AuthoringContext`, `AuthoringValidator`, `CardSpecMapper`, `StatusKeyRef`,
  `InterventionKeyRef`, `ConditionSpec`, `TargetSelectorRef`, `ConditionKind`,
  `InterventionTargetSideRef`, 8종 `*Spec`)
- Produces: `public static class FateWeaver.Core.CombatRegistries` — `Effects()`, `Statuses()`,
  `InterventionActions()`

- [ ] **Step 1: 기준선을 기록한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Passed! - Failed: 0, Passed: 409`

- [ ] **Step 2: 폴더와 파일을 옮긴다**

```bash
mkdir -p Assets/Core/Registries
git mv Assets/Core/Simulation/Authoring Assets/Core/Authoring
git mv Assets/Core/Simulation/Authoring.meta Assets/Core/Authoring.meta
git mv Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Registries/CombatRegistries.cs
git mv Assets/Core/Simulation/CombatRegistries.cs.meta Assets/Core/Registries/CombatRegistries.cs.meta
```

`Assets/Core/Registries.meta`는 아직 없다. Step 7의 Unity 배치 실행이 생성하므로 Step 8에서 함께
커밋한다.

- [ ] **Step 3: 네임스페이스를 일괄 치환한다**

```bash
grep -rl "FateWeaver\.Simulation\.Authoring" Assets --include='*.cs' \
  | xargs sed -i '' 's/FateWeaver\.Simulation\.Authoring/FateWeaver.Core.Authoring/g'
```

- [ ] **Step 4: `CombatRegistries`의 네임스페이스와 가시성을 고친다**

`Assets/Core/Registries/CombatRegistries.cs`의 두 줄만 바꾼다.

```csharp
namespace FateWeaver.Core
{
    /// <summary>Single source of truth for the default effect / status / fate-action registries used by
    /// the runners and the playtest session — so a new handler is registered everywhere at once.</summary>
    public static class CombatRegistries
```

- [ ] **Step 5: `CombatRegistries` 소비자에 using을 더한다**

소비자는 전부 `FateWeaver.Simulation` 안에 있어 이제 명시적 using이 필요하다.

```bash
for f in Assets/Core/Simulation/DeckCombatSession.cs \
         Assets/Core/Simulation/PlaytestSession.cs \
         Assets/Core/Simulation/MultiTurnPlaytestSession.cs \
         Assets/Core/Simulation/MultiTurnRunner.cs \
         Assets/Core/Simulation/ScenarioRunner.cs \
         Assets/Core/Simulation/Descriptions/DescriptionCatalogValidator.cs \
         Assets/Core/Authoring/AuthoringContext.cs; do
  grep -q "^using FateWeaver.Core;" "$f" || sed -i '' '1i\
using FateWeaver.Core;
' "$f"
done
```

- [ ] **Step 6: 헤드리스 테스트로 이동이 무해했음을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Passed! - Failed: 0, Passed: 409` — 기준선과 정확히 같은 수

수가 다르면 sed가 파일을 놓친 것이다. `grep -rn "FateWeaver.Simulation.Authoring" Assets --include='*.cs'`로
남은 참조를 찾는다.

- [ ] **Step 6b: 카드 에셋의 직렬화 참조를 함께 옮긴다**

Unity의 `[SerializeReference]`는 어셈블리 한정 타입명을 `.asset` YAML에 박아둔다. 코드만 옮기면
27개 카드 에셋의 `Effects` 배열이 조용히 `null`이 된다. 헤드리스 테스트는 이걸 못 잡는다.

```bash
/usr/bin/grep -rl "ns: FateWeaver.Simulation.Authoring, asm: FateWeaver.Simulation" Assets --include='*.asset' \
  | xargs sed -i '' 's/ns: FateWeaver\.Simulation\.Authoring, asm: FateWeaver\.Simulation/ns: FateWeaver.Core.Authoring, asm: FateWeaver.Core/g'
```

Unity 표준 해법인 `[MovedFromAttribute]`는 쓸 수 없다 — `UnityEngine` 타입이라 코어의
`noEngineReferences`를 깬다(규칙 6). 직렬화 데이터를 직접 옮기는 것이 이 프로젝트에서의 올바른
해법이다.

`.asset`의 다른 `[SerializeReference]` 항목은 전부 Unity 렌더 파이프라인 어셈블리 소유이므로
`git diff`에 FateWeaver 줄만 나와야 한다. 확인:

```bash
/usr/bin/grep -rl "FateWeaver.Simulation" Assets --include='*.asset' | wc -l   # 0이어야 한다
```

- [ ] **Step 7: Unity 컴파일을 배치로 확인한다**

Run:
```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode -testResults /private/tmp/fw-task1.xml \
  -logFile /private/tmp/fw-task1.log
```
Expected: 종료 코드 0, `/private/tmp/fw-task1.xml`에 실패 0건 (483/483)

Step 6b를 건너뛰면 `StarterDeckAssetCompositionTests.Generated_snapshot_is_byte_for_byte_current_with_the_assets`가
`NullReferenceException`으로 실패한다.

`-quit`를 함께 쓰면 테스트 없이 exit 0이 나므로 절대 붙이지 않는다.

- [ ] **Step 8: 커밋**

Step 7의 Unity 실행이 만든 `Assets/Core/Registries.meta`가 `git status`에 보여야 한다. Unity가
남긴 그 밖의 변경(`Library/`, `ProjectSettings/`)은 스테이징하지 않는다.

```bash
git status --short
git add Assets/Core/Authoring Assets/Core/Authoring.meta \
        Assets/Core/Registries Assets/Core/Registries.meta \
        Assets/Core/Simulation Assets/Core/Tests Assets/Unity
git commit -m "refactor: 저작 스펙 기반을 FateWeaver.Core로 옮긴다

OwnedCard(Core)가 변형 목록에 EffectSpec(Simulation)을 담아야 하는데 참조
방향이 반대였다. CombatRegistries는 코어 핸들러만 등록하므로 함께 옮긴다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: Newtonsoft 의존성과 직렬화 설정

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Tests/Headless/FateWeaver.Tests.Headless.csproj`
- Create: `Assets/Core/Authoring/Json/ContentJson.cs`
- Create: `Assets/Core/Tests/EditMode/CardContentJsonTests.cs`

**Interfaces:**
- Produces: `FateWeaver.Core.Authoring.Json.ContentJson.Settings` (`JsonSerializerSettings`),
  `ContentJson.Plain` (`JsonSerializer`, 다형 컨버터 없음 — 컨버터 내부에서만 쓴다),
  `ContentJson.Write(object)` → `string`, `ContentJson.Read<T>(string)` → `T`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

Create `Assets/Core/Tests/EditMode/CardContentJsonTests.cs`:

```csharp
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CardContentJsonTests
    {
        [Test]
        public void WritesEnumsAsNamesAndCamelCaseKeys()
        {
            var json = ContentJson.Write(new CardSpec
            {
                Id = "slash",
                Name = "베기",
                Side = Side.Enemy,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 4
            });

            StringAssert.Contains("\"id\": \"slash\"", json);
            StringAssert.Contains("\"side\": \"Enemy\"", json);
        }

        [Test]
        public void OmitsDefaultValuedMembers()
        {
            var json = ContentJson.Write(new CardSpec { Id = "x", Name = "x" });

            StringAssert.DoesNotContain("interventionEffectValue", json);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentJsonTests`
Expected: 컴파일 실패 — `The type or namespace name 'Json' does not exist`

- [ ] **Step 3: 헤드리스 프로젝트에 패키지를 더한다**

`Tests/Headless/FateWeaver.Tests.Headless.csproj`의 첫 `ItemGroup`에 한 줄:

```xml
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

같은 파일의 `Compile` 항목은 `Assets/Core/**/*.cs`가 `Assets/Core/Simulation/**`만 제외하므로
`Assets/Core/Authoring/`과 `Assets/Core/Registries/`는 이미 포함된다. 수정할 필요 없다.

- [ ] **Step 4: Unity 패키지를 더한다**

`Packages/manifest.json`의 `dependencies`에 한 줄(알파벳 순서상 `com.unity.multiplayer.center` 다음):

```json
    "com.unity.nuget.newtonsoft-json": "3.2.1",
```

- [ ] **Step 5: 직렬화 설정을 만든다**

Create `Assets/Core/Authoring/Json/ContentJson.cs`:

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>카드 콘텐츠와 세이브가 공유하는 단 하나의 직렬화 설정. 열거형은 이름으로, 키는
    /// camelCase로, 기본값인 멤버는 생략한다(생략된 값은 읽을 때 기본값으로 복원되므로 왕복이
    /// 안전하고, 파일이 사람 눈에 읽힌다 — 설계 §4.5의 diff 목표).</summary>
    public static class ContentJson
    {
        public static JsonSerializerSettings Settings => Build(includePolymorphic: true);

        /// <summary>다형 컨버터가 빠진 설정. EffectSpecJsonConverter가 자기 자신을 재귀 호출하지
        /// 않고 대상 객체의 평범한 필드만 쓰기 위해 쓴다. 외부에서 직접 쓰지 않는다.</summary>
        internal static JsonSerializer Plain { get; } =
            JsonSerializer.Create(Build(includePolymorphic: false));

        public static string Write(object value)
            => JsonConvert.SerializeObject(value, Settings);

        public static T Read<T>(string json)
            => JsonConvert.DeserializeObject<T>(json, Settings);

        private static JsonSerializerSettings Build(bool includePolymorphic)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DefaultValueHandling = DefaultValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error
            };
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new StatusKeyRefJsonConverter());
            settings.Converters.Add(new InterventionKeyRefJsonConverter());
            if (includePolymorphic)
            {
                settings.Converters.Add(new EffectSpecJsonConverter());
            }

            return settings;
        }
    }
}
```

`MissingMemberHandling.Error`는 모드 저작자의 오타(`"vale": 5`)를 침묵으로 흘리지 않고 줄 위치와
함께 예외로 만든다 — Task 5의 오류 보고가 이 동작에 의존한다.

- [ ] **Step 6: 키 참조 컨버터를 만든다**

Create `Assets/Core/Authoring/Json/KeyRefJsonConverters.cs`:

```csharp
using System;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>StatusKeyRef를 {"id":"block"}이 아니라 "block"으로 쓴다. 저작자가 보는 파일에서
    /// 상태 참조가 한 겹 덜 중첩된다.</summary>
    public sealed class StatusKeyRefJsonConverter : JsonConverter<StatusKeyRef>
    {
        public override StatusKeyRef ReadJson(
            JsonReader reader, Type objectType, StatusKeyRef existingValue,
            bool hasExistingValue, JsonSerializer serializer)
            => new StatusKeyRef { Id = (string)reader.Value };

        public override void WriteJson(JsonWriter writer, StatusKeyRef value, JsonSerializer serializer)
            => writer.WriteValue(value.Id);
    }

    /// <summary>InterventionKeyRef도 같은 이유로 평범한 문자열로 쓴다.</summary>
    public sealed class InterventionKeyRefJsonConverter : JsonConverter<InterventionKeyRef>
    {
        public override InterventionKeyRef ReadJson(
            JsonReader reader, Type objectType, InterventionKeyRef existingValue,
            bool hasExistingValue, JsonSerializer serializer)
            => new InterventionKeyRef { Id = (string)reader.Value };

        public override void WriteJson(
            JsonWriter writer, InterventionKeyRef value, JsonSerializer serializer)
            => writer.WriteValue(value.Id);
    }
}
```

- [ ] **Step 7: 다형 컨버터의 빈 껍데기를 만든다**

Task 3에서 채운다. `ContentJson`이 컴파일되려면 타입이 존재해야 한다.

Create `Assets/Core/Authoring/Json/EffectSpecJsonConverter.cs`:

```csharp
using System;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Json
{
    public sealed class EffectSpecJsonConverter : JsonConverter<EffectSpec>
    {
        public override EffectSpec ReadJson(
            JsonReader reader, Type objectType, EffectSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
            => throw new NotImplementedException();

        public override void WriteJson(JsonWriter writer, EffectSpec value, JsonSerializer serializer)
            => throw new NotImplementedException();
    }
}
```

- [ ] **Step 8: 테스트 통과를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentJsonTests`
Expected: `Passed: 2, Failed: 0`

- [ ] **Step 9: 전체 테스트로 회귀가 없음을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 411`

- [ ] **Step 10: 커밋**

```bash
git add Packages/manifest.json Tests/Headless/FateWeaver.Tests.Headless.csproj \
        Assets/Core/Authoring/Json Assets/Core/Tests/EditMode/CardContentJsonTests.cs
git commit -m "feat: 카드 콘텐츠 JSON 직렬화 설정을 더한다

JsonUtility는 다형성을 지원하지 않아 EffectSpec[]의 서브타입이 소실된다.
Newtonsoft는 순수 관리 어셈블리라 코어의 noEngineReferences를 깨지 않는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: EffectSpec 다형 컨버터

판별자는 각 스펙이 이미 갖고 있는 `EffectKey.Id`를 쓴다(`damage`, `apply_status`, …). 8종 모두 키가
서로 다르므로 새 등록 필드가 필요 없고, 표는 `EffectSpecCatalog`에서 만든다(규칙 9).

**Files:**
- Modify: `Assets/Core/Authoring/Json/EffectSpecJsonConverter.cs`
- Modify: `Assets/Core/Tests/EditMode/CardContentJsonTests.cs`

**Interfaces:**
- Consumes: `ContentJson.Plain` (Task 2), `EffectSpecCatalog.All()` → `IReadOnlyList<EffectSpecInfo>`
  (`DisplayName`, `SpecType`, `Create`)
- Produces: `EffectSpecJsonConverter.KindProperty` = `"kind"`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`CardContentJsonTests.cs`에 다음 세 테스트를 더한다. 파일 상단 using에 `System.Linq`,
`FateWeaver.Core.Effects`, `FateWeaver.Core.Status`를 추가한다.

```csharp
        [Test]
        public void RoundTripsEveryRegisteredEffectSpecKind()
        {
            foreach (var info in EffectSpecCatalog.All())
            {
                var original = info.Create();
                var json = ContentJson.Write(original);
                var restored = ContentJson.Read<EffectSpec>(json);

                Assert.AreEqual(info.SpecType, restored.GetType(), info.DisplayName);
                Assert.AreEqual(original.Key, restored.Key, info.DisplayName);
            }
        }

        [Test]
        public void RoundTripsSpecParametersAndCondition()
        {
            var original = new ApplyStatusSpec
            {
                Status = new StatusKeyRef { Id = "poison" },
                Value = 3,
                Lifetime = StatusLifetimeKind.Turns,
                LifetimeCount = 2,
                Target = StatusApplyTarget.TargetEnemy,
                Selector = TargetSelectorRef.BackMost,
                Condition = new ConditionSpec
                {
                    Kind = ConditionKind.WithinNth, N = 2, SuccessEffectValue = 5, SkipOnBasic = true
                }
            };

            var restored = (ApplyStatusSpec)ContentJson.Read<EffectSpec>(ContentJson.Write(original));

            Assert.AreEqual("poison", restored.Status.Id);
            Assert.AreEqual(3, restored.Value);
            Assert.AreEqual(StatusLifetimeKind.Turns, restored.Lifetime);
            Assert.AreEqual(2, restored.LifetimeCount);
            Assert.AreEqual(StatusApplyTarget.TargetEnemy, restored.Target);
            Assert.AreEqual(TargetSelectorRef.BackMost, restored.Selector);
            Assert.AreEqual(ConditionKind.WithinNth, restored.Condition.Kind);
            Assert.AreEqual(2, restored.Condition.N);
            Assert.AreEqual(5, restored.Condition.SuccessEffectValue);
            Assert.IsTrue(restored.Condition.SkipOnBasic);
        }

        [Test]
        public void RejectsUnknownEffectKindByName()
        {
            var ex = Assert.Throws<JsonSerializationException>(
                () => ContentJson.Read<EffectSpec>("{ \"kind\": \"dmage\", \"value\": 5 }"));

            StringAssert.Contains("dmage", ex.Message);
        }

        [Test]
        public void EveryCatalogEntryHasADistinctKind()
        {
            var kinds = EffectSpecCatalog.All().Select(info => info.Create().Key.Id).ToList();

            CollectionAssert.AllItemsAreUnique(kinds);
        }
```

`Newtonsoft.Json` using도 추가한다.

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentJsonTests`
Expected: FAIL — `System.NotImplementedException`

- [ ] **Step 3: 컨버터를 구현한다**

Replace `Assets/Core/Authoring/Json/EffectSpecJsonConverter.cs`:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>EffectSpec의 다형 (역)직렬화. 판별자는 스펙이 이미 갖고 있는 EffectKey.Id이고
    /// 타입 표는 EffectSpecCatalog에서 만든다 — 리플렉션 스캔 없음(AGENTS.md 규칙 9).</summary>
    public sealed class EffectSpecJsonConverter : JsonConverter<EffectSpec>
    {
        public const string KindProperty = "kind";

        private static readonly Dictionary<string, Func<EffectSpec>> FactoryByKind = BuildFactories();
        private static readonly Dictionary<Type, string> KindByType = BuildKinds();

        public override EffectSpec ReadJson(
            JsonReader reader, Type objectType, EffectSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var kind = (string)entry[KindProperty];
            if (string.IsNullOrEmpty(kind))
            {
                throw new JsonSerializationException(
                    "Effect entry requires a '" + KindProperty + "' property.");
            }

            if (!FactoryByKind.TryGetValue(kind, out var create))
            {
                throw new JsonSerializationException("Unknown effect kind '" + kind + "'.");
            }

            entry.Remove(KindProperty);
            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Plain.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, EffectSpec value, JsonSerializer serializer)
        {
            if (!KindByType.TryGetValue(value.GetType(), out var kind))
            {
                throw new JsonSerializationException(
                    "Effect spec type '" + value.GetType().Name
                    + "' is not registered in EffectSpecCatalog.");
            }

            var entry = JObject.FromObject(value, ContentJson.Plain);
            entry.AddFirst(new JProperty(KindProperty, kind));
            entry.WriteTo(writer);
        }

        private static Dictionary<string, Func<EffectSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<EffectSpec>>();
            foreach (var info in EffectSpecCatalog.All())
            {
                var kind = info.Create().Key.Id;
                if (table.ContainsKey(kind))
                {
                    throw new InvalidOperationException(
                        "Duplicate effect spec kind '" + kind + "' in EffectSpecCatalog.");
                }

                table.Add(kind, info.Create);
            }

            return table;
        }

        private static Dictionary<Type, string> BuildKinds()
        {
            var table = new Dictionary<Type, string>();
            foreach (var info in EffectSpecCatalog.All())
            {
                table.Add(info.SpecType, info.Create().Key.Id);
            }

            return table;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentJsonTests`
Expected: `Passed: 6, Failed: 0`

- [ ] **Step 5: 전체 테스트**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 415`

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Authoring/Json Assets/Core/Tests/EditMode/CardContentJsonTests.cs
git commit -m "feat: EffectSpec 다형 JSON 컨버터를 더한다

판별자는 스펙이 이미 갖고 있는 EffectKey.Id를 쓰고 타입 표는
EffectSpecCatalog에서 만든다. 새 등록 필드도 리플렉션 스캔도 없다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: CardSpec 왕복

**Files:**
- Modify: `Assets/Core/Tests/EditMode/CardContentJsonTests.cs`

구현 코드는 없다. Task 2·3의 설정이 `CardSpec`에도 그대로 통하는지를 확인하고, 통하지 않는 부분만
고치는 Task다. 예상되는 문제는 `CardSpec.Effects`가 `EffectSpec[]`(배열)이라 컨버터가 원소마다
호출되는지 여부다.

**Interfaces:**
- Consumes: `ContentJson.Write` / `ContentJson.Read<CardSpec>` (Task 2),
  `EffectSpecJsonConverter` (Task 3), `CardSpecMapper.ToDefinition(CardSpec)` → `CardDefinition`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`CardContentJsonTests.cs`에 더한다.

```csharp
        [Test]
        public void RoundTripsAnExecutionCardWithMultipleEffects()
        {
            var original = new CardSpec
            {
                Id = "probing_strike",
                Name = "견제타",
                Side = Side.Player,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 4,
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Value = 4, Selector = TargetSelectorRef.FrontMost },
                    new ApplyStatusSpec
                    {
                        Status = new StatusKeyRef { Id = "block" },
                        Value = 1,
                        Lifetime = StatusLifetimeKind.ThisTurn,
                        Target = StatusApplyTarget.Self
                    }
                }
            };

            var restored = ContentJson.Read<CardSpec>(ContentJson.Write(original));

            Assert.AreEqual("probing_strike", restored.Id);
            Assert.AreEqual("견제타", restored.Name);
            Assert.AreEqual(4, restored.BaseExecutionOrder);
            Assert.AreEqual(2, restored.Effects.Length);
            Assert.IsInstanceOf<DamageSpec>(restored.Effects[0]);
            Assert.AreEqual(4, ((DamageSpec)restored.Effects[0]).Value);
            Assert.IsInstanceOf<ApplyStatusSpec>(restored.Effects[1]);
            Assert.AreEqual("block", ((ApplyStatusSpec)restored.Effects[1]).Status.Id);
        }

        [Test]
        public void RoundTripsAnInterventionCardIncludingTargetRestrictions()
        {
            var original = new CardSpec
            {
                Id = "hasten",
                Name = "재촉",
                Side = Side.Player,
                Category = CardCategory.Intervention,
                EnergyCost = 1,
                Intervention = new InterventionKeyRef { Id = "change_execution_order" },
                InterventionEffectValue = -2,
                InterventionTargetSide = InterventionTargetSideRef.Player,
                InterventionRequireAdjacent = true
            };

            var restored = ContentJson.Read<CardSpec>(ContentJson.Write(original));

            Assert.AreEqual("change_execution_order", restored.Intervention.Id);
            Assert.AreEqual(-2, restored.InterventionEffectValue);
            Assert.AreEqual(InterventionTargetSideRef.Player, restored.InterventionTargetSide);
            Assert.IsTrue(restored.InterventionRequireAdjacent);
        }

        [Test]
        public void RoundTrippedCardProducesAnIdenticalDefinition()
        {
            var original = new CardSpec
            {
                Id = "delayed_strike",
                Name = "늦춘 일격",
                Side = Side.Player,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 5,
                Effects = new EffectSpec[] { new DamageSpec { Value = 5 } }
            };

            var before = CardSpecMapper.ToDefinition(original);
            var after = CardSpecMapper.ToDefinition(
                ContentJson.Read<CardSpec>(ContentJson.Write(original)));

            Assert.AreEqual(before.Id, after.Id);
            Assert.AreEqual(before.Name, after.Name);
            Assert.AreEqual(before.BaseExecutionOrder, after.BaseExecutionOrder);
            Assert.AreEqual(before.EnergyCost, after.EnergyCost);
            Assert.AreEqual(before.Category, after.Category);
            Assert.AreEqual(before.Effects.Count, after.Effects.Count);
            Assert.AreEqual(before.Effects[0].Key, after.Effects[0].Key);
            Assert.AreEqual(before.Effects[0].EffectValue, after.Effects[0].EffectValue);
        }
```

- [ ] **Step 2: 실행해서 무엇이 깨지는지 본다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentJsonTests`

세 테스트가 그대로 통과하면 Step 3을 건너뛰고 Step 4로 간다. 통과하지 못하면 실패 메시지가 원인을
가리킨다. 가장 있을 법한 두 가지:

- `Effects`가 `null`로 복원된다 → `DefaultValueHandling.Ignore`가 빈 배열을 지운 것이다.
  `CardSpecMapper`가 이미 `spec.Effects ?? Array.Empty<EffectSpec>()`로 방어하므로 정의 생성에는
  문제가 없다. 테스트에서 배열이 비지 않은 카드를 쓰고 있으므로 이 경로는 타지 않아야 한다.
- `MissingMemberHandling.Error`가 `kind`에 걸린다 → 컨버터가 `entry.Remove(KindProperty)`를
  `Populate` 전에 하고 있는지 확인한다.

- [ ] **Step 3: 필요할 때만 고친다**

Step 2에서 실패한 원인만 고친다. 실패가 없었다면 이 단계는 건너뛴다.

- [ ] **Step 4: 전체 테스트**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 418`

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Tests/EditMode/CardContentJsonTests.cs Assets/Core/Authoring/Json
git commit -m "test: CardSpec의 JSON 왕복을 고정한다

실행 카드의 다중 효과, 개입 카드의 대상 진영·인접 제한, 왕복 후 생성한
CardDefinition의 동일성을 잠근다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: 콘텐츠 로더와 오류 보고

로더는 파일을 읽지 않는다. `(이름, 본문)` 쌍을 받아 순수하게 동작하므로 헤드리스 테스트가 임시
파일 없이 검증한다. 실제 디렉터리 읽기는 `CardContentFiles`가 따로 맡는다.

**Files:**
- Create: `Assets/Core/Authoring/CardContentSource.cs`
- Create: `Assets/Core/Authoring/CardContentCatalog.cs`
- Create: `Assets/Core/Authoring/CardContentLoader.cs`
- Create: `Assets/Core/Authoring/CardContentFiles.cs`
- Create: `Assets/Core/Tests/EditMode/CardContentLoaderTests.cs`

**Interfaces:**
- Consumes: `ContentJson.Read<CardSpec>` (Task 2), `AuthoringValidator.Validate(IEnumerable<CardSpec>,
  AuthoringContext)` → `IReadOnlyList<string>`, `AuthoringContext.Default()`,
  `CardSpecMapper.ToDefinition(CardSpec)`
- Produces:
  - `CardContentSource` — `Name` (string), `Json` (string), 생성자 `(string name, string json)`
  - `CardContentCatalog` — `Cards` (`IReadOnlyDictionary<string, CardDefinition>`),
    `Get(string id)` → `CardDefinition`, `Ids` (`IReadOnlyList<string>`, 정렬됨)
  - `CardContentLoadResult` — `Succeeded` (bool), `Catalog` (`CardContentCatalog`, 실패 시 null),
    `Errors` (`IReadOnlyList<string>`)
  - `CardContentLoader.Load(IEnumerable<CardContentSource>, AuthoringContext)` → `CardContentLoadResult`
  - `CardContentFiles.ReadDirectory(string path)` → `IReadOnlyList<CardContentSource>`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

Create `Assets/Core/Tests/EditMode/CardContentLoaderTests.cs`:

```csharp
using System.Linq;
using FateWeaver.Core.Authoring;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CardContentLoaderTests
    {
        private const string Slash =
            "{ \"id\": \"slash\", \"name\": \"베기\", \"side\": \"Player\","
            + " \"category\": \"Execution\", \"energyCost\": 1, \"baseExecutionOrder\": 4,"
            + " \"effects\": [ { \"kind\": \"damage\", \"value\": 5 } ] }";

        private static CardContentLoadResult Load(params CardContentSource[] sources)
            => CardContentLoader.Load(sources, AuthoringContext.Default());

        [Test]
        public void LoadsACardIntoTheCatalog()
        {
            var result = Load(new CardContentSource("slash.json", Slash));

            Assert.IsTrue(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.AreEqual("베기", result.Catalog.Get("slash").Name);
            Assert.AreEqual(5, result.Catalog.Get("slash").Effects[0].EffectValue);
        }

        [Test]
        public void ReportsMalformedJsonWithFileNameAndLineNumber()
        {
            // 필수 키는 모두 있고 중괄호만 닫히지 않았다. 필수 키 검사를 통과해 파서까지
            // 도달해야 파싱 오류 경로를 실제로 검증한다. Newtonsoft 13은 트레일링 콤마를
            // 허용하므로 그것으로는 파싱이 실패하지 않는다.
            var result = Load(new CardContentSource(
                "broken.json",
                "{ \"id\": \"x\", \"name\": \"x\", \"side\": \"Player\", \"category\": \"Execution\""));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Catalog);
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains("broken.json", result.Errors[0]);
            StringAssert.Contains("line 1", result.Errors[0]);
        }

        [Test]
        public void ReportsAnUnknownEffectKindInsteadOfSkippingIt()
        {
            var result = Load(new CardContentSource(
                "typo.json",
                "{ \"id\": \"x\", \"name\": \"x\", \"side\": \"Player\","
                + " \"category\": \"Execution\","
                + " \"effects\": [ { \"kind\": \"dmage\", \"value\": 5 } ] }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("typo.json", result.Errors[0]);
            StringAssert.Contains("dmage", result.Errors[0]);
        }

        [Test]
        public void ReportsAMissingRequiredKeyRatherThanDefaultingIt()
        {
            var result = Load(new CardContentSource(
                "nosides.json", "{ \"id\": \"x\", \"name\": \"x\", \"category\": \"Execution\" }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("side", result.Errors[0]);
        }

        [Test]
        public void ReportsADuplicateIdAcrossFiles()
        {
            var result = Load(
                new CardContentSource("a.json", Slash),
                new CardContentSource("b.json", Slash));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("slash", result.Errors[0]);
            StringAssert.Contains("b.json", result.Errors[0]);
        }

        [Test]
        public void ReportsAuthoringValidationFailures()
        {
            var result = Load(new CardContentSource(
                "badstatus.json",
                "{ \"id\": \"x\", \"name\": \"x\", \"side\": \"Player\","
                + " \"category\": \"Execution\", \"effects\": ["
                + " { \"kind\": \"apply_status\", \"status\": \"no_such_status\", \"value\": 1 } ] }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("no_such_status", result.Errors[0]);
        }

        [Test]
        public void ReportsEveryFailingFileAtOnce()
        {
            var result = Load(
                new CardContentSource(
                    "one.json",
                    "{ \"id\": \"a\", \"name\": \"x\", \"side\": \"Player\", \"category\": \"Execution\""),
                new CardContentSource(
                    "two.json",
                    "{ \"id\": \"b\", \"name\": \"x\", \"side\": \"Player\", \"category\": \"Execution\""));

            Assert.AreEqual(2, result.Errors.Count);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("one.json")));
            Assert.IsTrue(result.Errors.Any(e => e.Contains("two.json")));
        }

        [Test]
        public void ExposesIdsInSortedOrderForDeterminism()
        {
            var result = Load(
                new CardContentSource("z.json", Slash.Replace("slash", "zeta")),
                new CardContentSource("a.json", Slash.Replace("slash", "alpha")));

            CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, result.Catalog.Ids);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentLoaderTests`
Expected: 컴파일 실패 — `The name 'CardContentLoader' does not exist`

- [ ] **Step 3: 입력·출력 타입을 만든다**

Create `Assets/Core/Authoring/CardContentSource.cs`:

```csharp
namespace FateWeaver.Core.Authoring
{
    /// <summary>로더의 입력 한 단위. Name은 오류 메시지에만 쓰이며 보통 파일 이름이다.
    /// 로더가 파일을 직접 읽지 않으므로 헤드리스 테스트가 임시 파일 없이 검증할 수 있다.</summary>
    public sealed class CardContentSource
    {
        public CardContentSource(string name, string json)
        {
            Name = name;
            Json = json;
        }

        public string Name { get; }
        public string Json { get; }
    }
}
```

Create `Assets/Core/Authoring/CardContentCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 시 한 번 만들어져 상주하는 id → CardDefinition 사전. 같은 카드를 여러 장
    /// 소유해도 정의 객체는 하나이고, 소유 카드는 이것을 참조한다(설계 §4.5).</summary>
    public sealed class CardContentCatalog
    {
        private readonly Dictionary<string, CardDefinition> _cards;
        private readonly List<string> _ids;

        public CardContentCatalog(Dictionary<string, CardDefinition> cards)
        {
            _cards = cards;
            _ids = new List<string>(cards.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, CardDefinition> Cards => _cards;

        /// <summary>정렬된 id 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Ids => _ids;

        public CardDefinition Get(string id)
        {
            if (!_cards.TryGetValue(id, out var card))
            {
                throw new KeyNotFoundException("No card content with id '" + id + "'.");
            }

            return card;
        }
    }
}
```

- [ ] **Step 4: 로더를 구현한다**

Create `Assets/Core/Authoring/CardContentLoader.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>로드 한 번의 결과. 실패하면 카탈로그를 내주지 않고 모든 이유를 모아 보고한다
    /// (설계 §4.5: 실패한 모드 콘텐츠는 로드를 거부하며 이유를 보고한다).</summary>
    public sealed class CardContentLoadResult
    {
        private CardContentLoadResult(CardContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public CardContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static CardContentLoadResult Ok(CardContentCatalog catalog)
            => new CardContentLoadResult(catalog, new string[0]);

        public static CardContentLoadResult Failed(IReadOnlyList<string> errors)
            => new CardContentLoadResult(null, errors);
    }

    /// <summary>콘텐츠 소스 목록을 파싱·검증해 카탈로그로 만든다. 파일 I/O는 CardContentFiles가
    /// 맡으므로 이 클래스는 순수하고 헤드리스 테스트가 그대로 돌린다.</summary>
    public static class CardContentLoader
    {
        /// <summary>생략되면 조용히 기본값이 들어가서는 안 되는 키. side가 빠진 카드가 말없이
        /// 플레이어 카드가 되는 사고를 막는다.</summary>
        private static readonly string[] RequiredKeys = { "id", "name", "side", "category" };

        public static CardContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            AuthoringContext context)
        {
            var errors = new List<string>();
            var specs = new List<CardSpec>();
            var origin = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var missing = FirstMissingKey(source.Json);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                CardSpec spec;
                try
                {
                    spec = ContentJson.Read<CardSpec>(source.Json);
                }
                catch (JsonException ex)
                {
                    errors.Add(source.Name + ": " + Describe(ex));
                    continue;
                }

                if (origin.TryGetValue(spec.Id, out var first))
                {
                    errors.Add(
                        source.Name + ": duplicate card id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                origin.Add(spec.Id, source.Name);
                specs.Add(spec);
            }

            foreach (var error in AuthoringValidator.Validate(specs, context))
            {
                errors.Add(error);
            }

            if (errors.Count > 0)
            {
                return CardContentLoadResult.Failed(errors);
            }

            var cards = new Dictionary<string, CardDefinition>();
            foreach (var spec in specs)
            {
                cards.Add(spec.Id, CardSpecMapper.ToDefinition(spec));
            }

            return CardContentLoadResult.Ok(new CardContentCatalog(cards));
        }

        /// <summary>필수 키 중 처음으로 빠진 것. 없으면 null.</summary>
        private static string FirstMissingKey(string json)
        {
            foreach (var key in RequiredKeys)
            {
                if (json.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal) < 0)
                {
                    return key;
                }
            }

            return null;
        }

        /// <summary>Newtonsoft의 예외에서 줄·열을 꺼내 저작자가 고칠 수 있는 문장으로 만든다.</summary>
        private static string Describe(JsonException exception)
        {
            if (exception is JsonReaderException reader)
            {
                return exception.Message + " (line " + reader.LineNumber
                    + ", position " + reader.LinePosition + ")";
            }

            if (exception is JsonSerializationException serialization
                && serialization.LineNumber > 0)
            {
                return exception.Message + " (line " + serialization.LineNumber
                    + ", position " + serialization.LinePosition + ")";
            }

            return exception.Message;
        }
    }
}
```

`goto nextSource`는 필수 키 검사에서 바깥 루프의 다음 원소로 건너뛰기 위한 것이다. C# 9에서
`foreach` 안의 `foreach`를 벗어나는 다른 방법(플래그 변수)보다 짧고 의도가 드러난다.

- [ ] **Step 5: 테스트 통과를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentLoaderTests`
Expected: `Passed: 8, Failed: 0`

`ReportsMalformedJsonWithFileNameAndLineNumber`가 `line 1`을 못 찾으면 실제 예외 타입과 메시지를
확인해 `Describe`를 맞춘다. **단언을 약화시키지 않는다** — 저작자에게 위치를 알려주는 것이 이
테스트의 존재 이유다.

- [ ] **Step 6: 디렉터리 읽기를 더한다**

Create `Assets/Core/Authoring/CardContentFiles.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace FateWeaver.Core.Authoring
{
    /// <summary>콘텐츠 디렉터리에서 *.json을 읽어 로더의 입력으로 바꾼다. 파일 I/O를 로더 밖에
    /// 격리해 로더가 순수하게 남는다. 개별 카드를 경로 문자열로 찾지 않고 디렉터리를 훑는다
    /// (AGENTS.md 규칙 2·3).</summary>
    public static class CardContentFiles
    {
        public const string CardsFolderName = "Cards";

        public static IReadOnlyList<CardContentSource> ReadDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(
                    "Card content directory not found: " + directory);
            }

            var paths = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);

            var sources = new List<CardContentSource>(paths.Length);
            foreach (var path in paths)
            {
                sources.Add(new CardContentSource(Path.GetFileName(path), File.ReadAllText(path)));
            }

            return sources;
        }
    }
}
```

`Array.Sort`로 읽기 순서를 고정하는 것은 결정론 때문이다. 파일 시스템 열거 순서는 플랫폼마다 달라
중복 id 오류 메시지가 "어느 파일이 먼저였는지"를 다르게 보고할 수 있다.

- [ ] **Step 7: 전체 테스트**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 426`

- [ ] **Step 8: 커밋**

```bash
git add Assets/Core/Authoring Assets/Core/Tests/EditMode/CardContentLoaderTests.cs
git commit -m "feat: 카드 콘텐츠 JSON 로더를 더한다

로더는 파일이 아니라 (이름, 본문) 쌍을 받아 순수하게 동작한다. 파싱 실패·
미등록 키·중복 id·검증 위반을 모아 파일 이름과 줄 위치로 보고하고, 하나라도
실패하면 카탈로그를 내주지 않는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: 익스포터와 동등성 증명

기존 C# 스펙을 JSON으로 1회 변환하고, 변환 결과를 로더로 읽었을 때 원본과 같은 `CardDefinition`이
나오는지 확인한다. 이 동등성 테스트가 계획 2의 전환을 안전하게 만드는 안전망이다.

**Files:**
- Create: `Assets/Unity/Editor/CardContentExporter.cs`
- Create: `Assets/StreamingAssets/Content/Cards/*.json` (생성물)
- Create: `Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs`

**Interfaces:**
- Consumes: `ContentJson.Write` (Task 2), `CardContentFiles.ReadDirectory` /
  `CardContentLoader.Load` / `CardContentCatalog` (Task 5),
  `StarterPoolSpecs.Build()` / `StarterDeckSpecs.Build()` / `PartyPrototypeDeckSpecs.Build()` →
  `IReadOnlyList<CardSpec>`
- Produces: 메뉴 `Fate Weaver/Export Card Content to JSON`,
  `CardContentExporter.ExportAll()` (에디터 전용)

- [ ] **Step 1: 실패하는 동등성 테스트를 쓴다**

Create `Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>내보낸 JSON이 손으로 쓴 C# 스펙과 같은 카드를 만드는지 잠근다. 계획 2가 소비자를
    /// JSON으로 옮길 때 이 테스트가 안전망이 된다.</summary>
    public class CardContentEquivalenceJsonTests
    {
        private static string ContentDirectory()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return Path.Combine(directory, "Assets", "StreamingAssets", "Content", "Cards");
        }

        private static CardContentCatalog Catalog()
        {
            var result = CardContentLoader.Load(
                CardContentFiles.ReadDirectory(ContentDirectory()), AuthoringContext.Default());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        private static string Signature(CardDefinition def)
            => def.Id + "|" + def.Name + "|" + def.Side + "|" + def.Category
                + "|" + def.EnergyCost + "|" + def.BaseExecutionOrder
                + "|" + (def.InterventionAction == null
                    ? "-"
                    : def.InterventionAction.Key + ":" + def.InterventionAction.EffectValue
                        + ":" + def.InterventionAction.TargetSide
                        + ":" + def.InterventionAction.RequireAdjacentTargets)
                + "|" + string.Join(",", def.Effects.Select(e =>
                    e.Key + ":" + e.EffectValue + ":" + e.TargetSelector
                        + ":" + (e.Condition == null ? "-" : e.Condition.GetType().Name)
                        + ":" + e.SuccessEffectValue + ":" + e.SkipOnBasic));

        private static IEnumerable<CardSpec> AuthoredSpecs()
            => StarterPoolSpecs.Build()
                .Concat(StarterDeckSpecs.Build())
                .Concat(PartyPrototypeDeckSpecs.Build())
                .GroupBy(spec => spec.Id)
                .Select(group => group.First());

        [Test]
        public void ExportedJsonContainsEveryAuthoredCard()
        {
            var catalog = Catalog();

            foreach (var spec in AuthoredSpecs())
            {
                Assert.IsTrue(
                    catalog.Cards.ContainsKey(spec.Id),
                    "내보낸 콘텐츠에 '" + spec.Id + "'가 없다.");
            }
        }

        [Test]
        public void ExportedJsonProducesIdenticalDefinitions()
        {
            var catalog = Catalog();

            foreach (var spec in AuthoredSpecs())
            {
                Assert.AreEqual(
                    Signature(CardSpecMapper.ToDefinition(spec)),
                    Signature(catalog.Get(spec.Id)),
                    "카드 '" + spec.Id + "'가 달라졌다.");
            }
        }

        [Test]
        public void ExportedJsonHasOneFilePerCard()
        {
            var files = Directory.GetFiles(ContentDirectory(), "*.json");

            Assert.AreEqual(AuthoredSpecs().Count(), files.Length);
        }
    }
}
```

`StarterPoolSpecs`·`StarterDeckSpecs`·`PartyPrototypeDeckSpecs`는 Task 1에서 함께 옮겨졌으므로
`FateWeaver.Core.Authoring`에 있다. 별도 using이 필요 없다.

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentEquivalenceJsonTests`
Expected: FAIL — `Card content directory not found`

- [ ] **Step 3: 익스포터를 만든다**

Create `Assets/Unity/Editor/CardContentExporter.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>손으로 쓴 C# 카드 스펙을 StreamingAssets의 JSON으로 1회 변환한다. 변환이 끝나고
    /// 계획 2가 소비자를 JSON으로 옮기면 이 익스포터와 C# 스펙은 함께 제거된다.</summary>
    public static class CardContentExporter
    {
        private const string OutputDirectory = "Assets/StreamingAssets/Content/Cards";

        [MenuItem("Fate Weaver/Export Card Content to JSON")]
        public static void ExportAll()
        {
            Directory.CreateDirectory(OutputDirectory);

            var written = 0;
            foreach (var spec in DistinctById(AuthoredSpecs()))
            {
                var path = Path.Combine(OutputDirectory, spec.Id + ".json");
                File.WriteAllText(path, ContentJson.Write(spec) + "\n");
                written++;
            }

            AssetDatabase.Refresh();
            Debug.Log("Exported " + written + " cards to " + OutputDirectory);
        }

        private static IEnumerable<CardSpec> AuthoredSpecs()
            => StarterPoolSpecs.Build()
                .Concat(StarterDeckSpecs.Build())
                .Concat(PartyPrototypeDeckSpecs.Build());

        private static IEnumerable<CardSpec> DistinctById(IEnumerable<CardSpec> specs)
            => specs.GroupBy(spec => spec.Id).Select(group => group.First());
    }
}
```

- [ ] **Step 4: 익스포터를 배치로 실행한다**

Run:
```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod FateWeaver.Unity.Editor.CardContentExporter.ExportAll \
  -logFile /private/tmp/fw-export.log
```
Expected: 종료 코드 0, 로그에 `Exported N cards`

`-executeMethod`는 `-runTests`가 아니므로 `-quit`를 함께 써도 안전하다.

- [ ] **Step 5: 생성물을 눈으로 확인한다**

Run: `ls Assets/StreamingAssets/Content/Cards | head; cat Assets/StreamingAssets/Content/Cards/slash.json`
Expected: 카드당 파일 하나. `slash.json`이 `"id"`, `"name"`, `"side"`, `"category"`, `"effects"`를
포함하고 조건이 없는 효과에는 `"condition"` 키가 없다.

- [ ] **Step 6: 동등성 테스트 통과를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter CardContentEquivalenceJsonTests`
Expected: `Passed: 3, Failed: 0`

실패하면 서명의 어느 항목이 다른지가 메시지에 카드 id와 함께 나온다. 가장 있을 법한 원인은
`DefaultValueHandling.Ignore`가 의미 있는 0을 지운 경우다. 해당 필드에 `[JsonProperty(
DefaultValueHandling = DefaultValueHandling.Include)]`를 붙여 그 필드만 예외로 둔다.

- [ ] **Step 7: 전체 테스트**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 429`

- [ ] **Step 8: 워킹 트리를 확인하고 커밋한다**

```bash
git status --short
```

`Assets/StreamingAssets/**`의 `.json`과 각 `.meta`, `Assets/StreamingAssets.meta`,
`Assets/Core/Registries.meta`가 새로 보여야 한다. Unity가 만든 `.meta`는 대응하는 파일과 1:1로만
스테이징한다(규칙 16). Unity 실행이 남긴 그 밖의 변경(`Library/`, `ProjectSettings/` 등)은
스테이징하지 않는다.

```bash
git add Assets/Unity/Editor/CardContentExporter.cs \
        Assets/Unity/Editor/CardContentExporter.cs.meta \
        Assets/StreamingAssets \
        Assets/StreamingAssets.meta \
        Assets/Core/Registries.meta \
        Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs \
        Assets/Core/Tests/EditMode/CardContentEquivalenceJsonTests.cs.meta
git commit -m "feat: 카드 콘텐츠를 JSON으로 내보내고 동등성을 잠근다

손으로 쓴 C# 스펙을 카드당 한 파일로 1회 변환하고, 로더로 읽은 결과가 원본과
같은 CardDefinition을 만드는지 확인한다. 이 동등성이 계획 2의 소비자 전환을
안전하게 만든다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 9: 계획 문서의 상태를 갱신한다**

이 파일의 머리말 `상태`를 다음으로 바꾸고, `docs/superpowers/README.md`의 계획 표에 이 문서를 더한다
(규칙 20).

```markdown
- 상태: `active` — Task 1~6 구현 완료(429 tests 통과). 계획 2(콘텐츠 원본 전환) 대기
```

```bash
git add docs/superpowers/plans/2026-07-31-card-content-json-loading.md docs/superpowers/README.md
git commit -m "docs: 카드 콘텐츠 JSON 로딩 계획의 진행 상태를 갱신한다

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## 완료 조건

- 헤드리스 429 tests 통과, 실패 0
- `Assets/StreamingAssets/Content/Cards/`에 카드당 JSON 파일이 있고, 로더가 읽은 결과가 기존 C#
  스펙과 같은 `CardDefinition`을 만든다
- 깨진 JSON·미등록 효과 키·누락된 필수 키·중복 id가 각각 파일 이름과 함께 보고되고, 하나라도
  실패하면 카탈로그가 만들어지지 않는다
- `FateWeaver.Core`에 `UnityEngine` 참조가 없다 (헤드리스 컴파일이 이를 강제한다)
- 워킹 트리가 깨끗하다 (규칙 18)
