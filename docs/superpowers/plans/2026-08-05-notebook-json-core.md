# 카드 저작 노트북 JSON 코어 구현 계획 (계획 A)

- 작성일: 2026-08-05
- 개정일: 2026-08-06 — 계획 3.5(개입 다형화·카드 스펙 분리)에 맞춰 다시 썼다
- 상태: `active`
- 설계: [카드 저작 노트북 JSON 전환](../specs/2026-08-05-card-authoring-json-notebook-design.md)

## 설계 개요 (사람 검수용)

이 절만 읽고 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은 읽지
않아도 된다.

**무엇을 만드나** — 카드 저작 노트북이 게임과 같은 JSON을 직접 읽고 쓸 수 있게 하는 **순수 함수
층**을 만든다. C# 저작 타입에서 파라미터 구조를 뽑아 스키마 파일 하나를 생성하고, 노트북은 그것만
읽어 카드·풀 JSON을 모델로 바꾸고 되돌린다. **UI는 손대지 않는다** — 화면 개편은 계획 B다.

**구조**

| 객체 | 책임 (한 줄) | 이 객체가 모르는 것 |
|---|---|---|
| 저작 스키마 생성기 | C# 저작 타입에서 노트북이 읽을 스키마 파일을 만든다 | 노트북의 폼·화면, 카드의 실제 값 |
| `authoring-schema.json` | 저작 가능한 종류와 각각의 파라미터 구조를 담는다 | 카드의 값, 검증 규칙 |
| 스키마 로더 | 스키마 파일을 폼이 쓸 형태로 바꾼다 | 카드 내용, 저장소 접근 |
| 카드·풀 리더 | JSON 문자열을 노트북 모델로 옮긴다 | 어떻게 다시 쓰이는지, 값이 유효한지 |
| 카드·풀 라이터 | 모델을 저장소와 바이트가 같은 문자열로 되돌린다 | 값이 유효한지, 어디에 저장되는지 |
| 검증기 | 게임 로더가 거부할 것을 내보내기 전에 잡는다 | 파일 입출력, 화면 표시 |
| 상태 판정기 | 저장소 원본과 미반영 편집분의 관계를 다섯 상태로 가른다 | 무엇을 표시할지, 충돌을 어떻게 풀지 |

**의존 방향** — `C# 저작 타입 → 스키마 생성기 → authoring-schema.json → 스키마 로더 → 리더·라이터 → 검증기·상태 판정기`

**확장 축**
- *갈아끼울 수 있는 것* — 효과와 개입의 종류. C#에 스펙 클래스를 더하고 명부에 등록하면 스키마가
  따라오고 **노트북 소스는 바뀌지 않는다.**
- *한번 정하면 고정되는 것* — 노트북이 아는 파일 종류가 카드·풀 둘이다. 저작 필드 타입이 정수·
  불리언·상태키·열거 넷이다. 이 계획의 산출물은 순수 함수뿐이고 파일 입출력과 화면은 계획 B다.

**대안과 기각 이유**
1. *노트북 안에 파라미터 표를 복제* — 기각. C# 쪽이 늘 때마다 손으로 맞춰야 하고, 어긋나도 조용하다.
2. *노트북이 C# 소스를 직접 파싱* — 기각. 파일 위치와 C# 문법 양쪽에 결합되어 폴더를 재정리하거나
   필드를 프로퍼티로 바꾸면 깨진다. 생성기는 경로가 아니라 타입을 본다.

**이 선택으로 나중에 어려워지는 것**
- 스키마가 다룰 수 있는 필드 타입이 넷뿐이라, 그 밖의 타입을 저작 스펙에 넣으면 생성기가 던진다.
  의도된 조기 실패지만, 저작 표현을 넓히려면 생성기의 타입 표와 노트북 폼을 함께 열어야 한다.
- **라운드트립이 Newtonsoft의 직렬화 순서에 매여 있다.** 계획 3.5가 실측했듯 그 순서는 "기반 클래스
  먼저"가 아니라 `Order` 속성과 리플렉션 순서가 함께 정한다. C# 쪽 필드를 재배치하면 노트북 출력이
  조용히 달라지고, 그것을 잡는 건 노트북 테스트가 아니라 C# 쪽 왕복 테스트다.
- 카드가 실행·개입 두 타입으로 갈려 **스키마의 필드 목록도 둘**이다. 셋째 분류가 생기면 스키마·
  리더·라이터 세 곳을 함께 연다.
- 이 계획이 끝나도 노트북 UI는 여전히 Markdown 경로로 동작한다. 두 경로가 공존하는 기간이 계획 B가
  끝날 때까지 이어지고, 그동안 저작자는 어느 쪽이 진짜인지 헷갈릴 수 있다.

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.

> **에이전트 작업자에게:** 필수 서브 스킬 — `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans`로 태스크 단위로 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

**목표:** 저작 스키마 생성기와 노트북의 JSON 읽기·쓰기·검증 코어를 만든다. UI는 건드리지 않는다.

**아키텍처:** C# 헤드리스 테스트가 `EffectSpecCatalog`와 `InterventionSpecCatalog`를 리플렉션해
`Tools/card-idea-notebook/authoring-schema.json`을 생성한다(설계 §4). 노트북의 코어 스크립트에는
그 스키마를 읽어 카드·풀 JSON을 모델로 바꾸고 되돌리는 **순수 함수**를 **추가**한다. 성공 기준은
저장소의 카드 26장과 풀 1개를 읽어 다시 쓰면 **바이트가 같은 것**이다(설계 §8).

**기술 스택:** C# (LangVersion 9, NUnit, Newtonsoft.Json 13), 브라우저 JS (빌드 없음),
Node `node:test` + `node:vm`

## 계획 3.5가 바꾼 전제

2026-08-06 머지된 계획 3.5가 개입 저작을 평평한 네 필드에서 중첩 스펙으로 바꾸고 `CardSpec`을
실행·개입 두 타입으로 쪼갰다. 이 계획은 그 뒤 기준으로 다시 쓰였다. 옛 판을 기억하는 세션은 다음이
**전부 바뀌었다**는 것부터 알아야 한다.

| 항목 | 옛 판 | 지금 |
|---|---|---|
| 개입 저작 | 평평한 네 필드 | `"intervention": {"kind": …, …}` 중첩 하나 |
| `CardSpec` | 단일 클래스 | 추상 기반 + `ExecutionCardSpec`·`InterventionCardSpec` |
| 스키마의 `cardFields` | 배열 하나 | 분류별 배열 둘 |
| 스키마의 `interventions` | 키 문자열 배열 | `{kind, label, fields[]}` — 효과와 같은 모양 |
| `InterventionKeyRef` | 존재 | **삭제됨.** 그 JSON 컨버터도 함께 사라졌다 |
| 키 순서의 근거 | "C# 필드 선언 순서" | `Order` 속성 + 리플렉션 순서 (아래 표) |

가장 큰 이득은 **개입이 효과와 같은 모양이 됐다는 것**이다. 스키마에서 둘 다 `{kind, label, fields[]}`로
나오므로, 계획 B의 개입 폼은 효과 행 렌더러를 그대로 재사용한다.

> **설계 문서가 이 부분에서 낡았다.** [카드 저작 노트북 JSON 전환](../specs/2026-08-05-card-authoring-json-notebook-design.md)의
> §4 스키마 표(`interventions`의 출처를 `InterventionActionRegistry`로 적음), §5 카드 모델(개입 네
> 필드), §6 개입 섹션(고정 네 칸 폼), §8의 `InterventionKeyRef` 언급은 계획 3.5 이전 기준이다.
> **충돌하면 이 계획을 따른다** — 여기가 코드와 대조해 다시 쓴 쪽이다. 설계 문서 개정은 별도 작업이며,
> 그때까지 이 문단이 대체 관계를 밝힌다.

## 이 계획의 경계

**이 계획은 순수 추가다.** 기존 Markdown 저작 경로(`cardMarkdown`·`parseBundleMarkdown`·
`normalizeCard`·UI 전체)를 **하나도 건드리지 않는다.** 노트북은 계획 A가 끝난 뒤에도 지금과 똑같이
동작한다. 새 함수들은 `CardIdeaNotebook` export에 얹히기만 하고 아직 아무도 호출하지 않는다.

**계획 B(별도 문서, 계획 A 완료 후 작성)가 맡을 것:**
설계 §6 효과 편집기 UI, §7 풀 편성 화면, §11 화면 구성, §12 쓰기 정책의 diff 요약과 파일 쓰기,
§13 마이그레이션(`SCHEMA_VERSION` 7), Markdown 경로 제거, `시작 카드 풀.md` 삭제,
`2026-07-20-character-card-pools-design.md` §1 개정, 옛 노트북 스펙 `archive/` 이동.

나누는 이유는 라운드트립 바이트 동일성이 이 전환에서 가장 깨지기 쉬운 지점이고, UI를 뒤엎기 전에
그것부터 테스트로 잠가야 하기 때문이다. 그리고 이렇게 나누면 계획 A 내내 도구가 살아 있다.

## 전역 제약

- **규칙 15:** 메인 체크아웃(`/Users/ish/Git/rogue-deck`)의 브랜치를 전환하지 않는다. 전용 워크트리에서 작업한다.
- **규칙 27:** 커밋 메시지 제목과 본문은 한국어로 쓴다. 형식은 `타입(범위): 한국어 제목`이고 제목은 "…한다"로 끝난다.
- **규칙 14:** 외부 패키지를 추가하지 않는다. 노트북은 의존성 0으로 유지한다.
- **LangVersion 9.** `Tests/Headless`가 Unity 6의 컴파일러를 흉내내므로 C# 10 이상 문법(파일 스코프 네임스페이스, `record struct`)은 컴파일에 실패한다.
- **`Assets/Core/Tests/EditMode/`는 별도 어셈블리다**(`FateWeaver.Tests.EditMode.asmdef`). 코어의 `internal` 멤버에 접근할 수 없으므로 `ContentJson.Plain`도 `ContentJson.Nested`도 쓸 수 없다. 필요한 직렬화 설정은 테스트가 직접 만든다.
- **노트북은 빌드 단계가 없다.** `index.html` 하나로 브라우저에서 열린다.
- **`InterventionKeyRef`와 그 JSON 컨버터는 존재하지 않는다** (계획 3.5가 삭제). 직렬화 설정을 손으로 조립하는 코드에서 참조하면 컴파일이 깨진다. `StatusKeyRefJsonConverter`는 살아 있다.

## 검증 명령

**헤드리스** (모든 태스크 끝에서 실행):

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

**노트북** (태스크 2부터):

```bash
node --test Tools/card-idea-notebook/
```

**시작 시점 기준선 (2026-08-06 실측, master `16647f7`):** 헤드리스 **525/525 통과**.
카드 JSON **26**(실행 22 + 개입 4), 풀 JSON **1**, 상태 JSON **11**.

## 실측한 직렬화 규칙

계획 전체가 이 사실들 위에 서 있다. 전부 저장소의 실제 파일에서 확인했다(2026-08-06 재검증).

| 사실 | 근거 |
|---|---|
| 들여쓰기 2칸, 줄바꿈 LF, **파일 끝에 개행 1개** | `vanguard_slash.json` 마지막 두 바이트가 `7d 0a` |
| **키 순서는 선언 순서가 아니다** | 아래 절에서 따로 다룬다. 이 계획은 순서를 추측하지 않고 직렬화기에 물어본다 |
| 효과는 `kind`가 맨 앞, `condition`이 맨 뒤 | `riposte.json` = `kind, value, selector, condition` |
| 개입도 `kind`가 맨 앞 | `delay.json` = `kind, delta, targetSide` |
| 기본값 멤버 생략 | `fixture_attack.json`에 `grade`가 없다(`CardGrade.None`=0). `crossover.json`에 `targetSide`가 없다(`Any`=0) |
| **빈 배열은 생략되지 않는다** | 같은 파일에 `"tags": []`가 있다. `string[]`의 기본값은 `null`이라 `[]`는 기본값이 아니다 |
| **null 배열은 생략된다** | 저장소 26장에는 실례가 없다(전부 `tags` 키를 갖는다) — 노트북이 만든 카드에서만 나온다. 규칙 자체는 `DefaultValueHandling.Ignore`가 보장한다 |
| `side`·`category`는 기본값이어도 항상 쓴다 | `CardSpec`의 `DefaultValueHandling.Include` 처방 |
| 조건 전체가 기본값이면 `condition` 키 자체가 없다 | `ConditionSpec`이 struct이고 `vanguard_slash`에 없다 |

### 키 순서를 추측하지 않는 이유

계획 3.5가 실측으로 확인한 것: Newtonsoft의 `DefaultContractResolver`는 `Order ?? -1`로 **안정
정렬**하고, `Type.GetFields`는 **파생 클래스 필드를 기반보다 먼저** 돌려준다. 그래서 `Order`가 없는
파생 필드가 `Order` 없는 기반 필드보다 앞선다 — "기반 먼저"라는 직관과 반대다.

그 결과 `CardSpec`의 기반 필드는 명시적 `Order`(음수)를, `Grade`·`Tags`는 큰 양수를 달고 있으며,
파생 필드는 `Order` 없이 그 사이에 놓인다. 실제 출력 순서는 이렇다.

```text
실행 카드:  id, name, side, category, energyCost, baseExecutionOrder, effects, grade, tags
개입 카드:  id, name, side, category, energyCost, intervention, grade, tags
```

**이 순서를 노트북에 상수로 박지 않는다.** Task 1의 생성기가 빈 인스턴스를 직렬화해 순서를 읽어
스키마에 싣고, 노트북은 스키마가 준 순서를 따른다. C# 쪽이 필드를 재배치하면 스키마가 따라 바뀌고
골든 비교가 한 번 실패해 알려준다.

`JSON.stringify(obj, null, 2)`가 이 형식과 일치한다 — 2칸 들여쓰기, `": "` 구분자, 빈 배열 `[]`.
그래서 노트북은 **키를 올바른 순서로 삽입한 평범한 객체**를 만들고 `JSON.stringify(obj, null, 2) + "\n"`을
쓰면 된다. 직접 만든 포매터가 필요 없다.

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Core/Tests/EditMode/TestContent.cs` (수정) | `RepoRoot()` 추가. 기존 `Root()`가 이것을 쓰도록 |
| `Assets/Core/Tests/EditMode/AuthoringSchemaExportTests.cs` (신규) | 스키마 생성·골든 비교. 이 계획의 유일한 C# 신규 파일 |
| `Tools/card-idea-notebook/authoring-schema.json` (생성물) | 커밋한다. 노트북이 읽는 유일한 C# 유래 파일 |
| `Tools/card-idea-notebook/index.html` (수정) | 코어 스크립트에 순수 함수 추가. UI 스크립트 무변경 |
| `Tools/card-idea-notebook/index.test.mjs` (수정) | 새 함수의 테스트 추가. 기존 테스트 무변경 |

---

### Task 1: 저작 스키마 생성기

**Files:**
- Modify: `Assets/Core/Tests/EditMode/TestContent.cs:18-33`
- Create: `Assets/Core/Tests/EditMode/AuthoringSchemaExportTests.cs`
- Create (생성물): `Tools/card-idea-notebook/authoring-schema.json`

**Interfaces:**
- Consumes: `EffectSpecCatalog.All()`, `InterventionSpecCatalog.All()`
- Produces: `TestContent.RepoRoot()` → 저장소 루트 절대 경로.
  `Tools/card-idea-notebook/authoring-schema.json` → Task 2가 읽는다.

> **옛 판과 달라진 점:** 개입 목록의 출처가 런타임 레지스트리(`CombatRegistries.InterventionActions()`)가
> 아니라 **저작 명부**(`InterventionSpecCatalog`)다. 저작 도구가 알아야 하는 건 "실행 가능한 것"이
> 아니라 "저작 가능한 것"이고, 파라미터 구조는 저작 스펙에만 있기 때문이다. 그래서 옛 판의
> `InterventionActionRegistry.RegisteredKeys` 추가 단계는 **필요 없어져 삭제했다** — 두 목록이
> 어긋나는지는 `InterventionSpecCatalogTests`가 이미 검사한다.

- [ ] **Step 1: `TestContent`에 저장소 루트를 노출한다**

`Assets/Core/Tests/EditMode/TestContent.cs`의 `_root` 필드와 `Root()`를 통째로 바꾼다:

```csharp
        private static string _root;
        private static string _repoRoot;

        /// <summary>Assets 폴더가 보일 때까지 올라가 저장소 루트를 찾는다. 테스트 실행 디렉터리는
        /// 헤드리스(bin/...)와 Unity(Library/...)가 다르므로 경로를 박지 않는다.</summary>
        public static string RepoRoot()
        {
            if (_repoRoot != null)
            {
                return _repoRoot;
            }

            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return _repoRoot = directory;
        }

        /// <summary>콘텐츠 루트.</summary>
        public static string Root()
            => _root ?? (_root = Path.Combine(
                RepoRoot(), "Assets", "StreamingAssets", "Content"));
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/AuthoringSchemaExportTests.cs`를 만든다. 첫 실행에서는 파일이 없으므로
반드시 실패한다.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>카드 저작 노트북이 읽는 스키마를 EffectSpecCatalog와 InterventionSpecCatalog에서
    /// 생성하고, 커밋된 파일과 다르면 갱신한 뒤 실패한다(설계 §4). 노트북이 C# 파일 위치나 문법에
    /// 결합되지 않게 하는 것이 목적이므로, 생성기는 경로가 아니라 타입만 본다.</summary>
    public sealed class AuthoringSchemaExportTests
    {
        [Test]
        public void SchemaFileMatchesCatalog()
        {
            var expected = BuildSchema().ToString(Formatting.Indented) + "\n";
            var path = Path.Combine(
                TestContent.RepoRoot(), "Tools", "card-idea-notebook", "authoring-schema.json");

            var actual = File.Exists(path) ? File.ReadAllText(path) : null;
            if (actual == expected)
            {
                return;
            }

            File.WriteAllText(path, expected);
            Assert.Fail(
                "authoring-schema.json이 EffectSpecCatalog와 달라 갱신했다. 커밋에 포함하고 "
                + "테스트를 다시 실행하라. 경로: " + path);
        }

        /// <summary>키 순서를 추측하지 않고 Newtonsoft에게 물어본다. 노트북이 재현해야 하는 순서가
        /// 바로 이 직렬화기의 순서이므로, 같은 계약(camelCase + 키 참조 컨버터)으로 빈 인스턴스를
        /// 직렬화해 속성 순서를 읽는다. 기본값도 봐야 하므로 Include를 쓴다.</summary>
        private static JsonSerializer OrderProbe()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DefaultValueHandling = DefaultValueHandling.Include
            };
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new StatusKeyRefJsonConverter());
            return JsonSerializer.Create(settings);
        }

        private static List<string> PropertyOrder(object instance)
        {
            var order = new List<string>();
            foreach (var property in JObject.FromObject(instance, OrderProbe()).Properties())
            {
                order.Add(property.Name);
            }

            return order;
        }

        private static JObject BuildSchema()
        {
            var schema = new JObject();
            schema["effects"] = BuildEffects();
            schema["interventions"] = BuildInterventions();
            schema["condition"] = BuildCondition();
            schema["cardFields"] = BuildCardFields();
            schema["sides"] = Names(typeof(Side));
            schema["categories"] = Names(typeof(CardCategory));
            schema["grades"] = Names(typeof(CardGrade));
            schema["selectors"] = Names(typeof(TargetSelectorRef));
            schema["statusTargets"] = Names(typeof(StatusApplyTarget));
            return schema;
        }

        /// <summary>카드 분류마다 키 순서가 다르다 — 계획 3.5가 CardSpec을 두 타입으로 쪼갰기
        /// 때문이다. 분류 이름을 키로 쓰므로 노트북이 card.category로 바로 색인할 수 있다.</summary>
        private static JObject BuildCardFields()
        {
            var fields = new JObject();
            fields[CardCategory.Execution.ToString()] =
                new JArray(PropertyOrder(new ExecutionCardSpec()).ToArray());
            fields[CardCategory.Intervention.ToString()] =
                new JArray(PropertyOrder(new InterventionCardSpec()).ToArray());
            return fields;
        }

        private static JArray BuildEffects()
        {
            var effects = new JArray();
            foreach (var info in EffectSpecCatalog.All())
            {
                var entry = new JObject();
                entry["kind"] = info.Create().Key.Id;
                entry["label"] = info.DisplayName;

                var fields = new JArray();
                foreach (var name in PropertyOrder(info.Create()))
                {
                    if (name == "condition")
                    {
                        continue;
                    }

                    fields.Add(DescribeField(info.SpecType, name));
                }

                entry["fields"] = fields;
                effects.Add(entry);
            }

            return effects;
        }

        private static JObject BuildCondition()
        {
            var fields = new JArray();
            foreach (var name in PropertyOrder(new ConditionSpec()))
            {
                if (name == "kind")
                {
                    continue;
                }

                fields.Add(DescribeField(typeof(ConditionSpec), name));
            }

            var condition = new JObject();
            condition["kinds"] = Names(typeof(ConditionKind));
            condition["fields"] = fields;
            return condition;
        }

        /// <summary>개입은 효과와 같은 모양으로 낸다 — 계획 3.5가 InterventionSpec을 EffectSpec처럼
        /// 다형화했으므로, 노트북의 개입 폼이 효과 행 렌더러를 그대로 재사용할 수 있다.
        /// 조건을 걸러내지 않는 것이 효과와 유일하게 다른 점이다 — 개입에는 조건 시스템이 없다.</summary>
        private static JArray BuildInterventions()
        {
            var interventions = new JArray();
            foreach (var info in InterventionSpecCatalog.All())
            {
                var entry = new JObject();
                entry["kind"] = info.Create().Key.Id;
                entry["label"] = info.DisplayName;

                var fields = new JArray();
                foreach (var name in PropertyOrder(info.Create()))
                {
                    fields.Add(DescribeField(info.SpecType, name));
                }

                entry["fields"] = fields;
                interventions.Add(entry);
            }

            return interventions;
        }

        /// <summary>필드 하나를 노트북이 폼 컨트롤로 바꿀 수 있는 형태로 옮긴다. 모르는 타입에서
        /// 던지는 것이 핵심이다 — 노트북이 그릴 수 없는 필드를 C#에 추가하면 여기서 걸린다.</summary>
        private static JObject DescribeField(Type owner, string camelName)
        {
            var field = FieldFor(owner, camelName);
            var entry = new JObject();
            entry["name"] = camelName;

            var type = field.FieldType;
            if (type == typeof(int))
            {
                entry["type"] = "int";
            }
            else if (type == typeof(bool))
            {
                entry["type"] = "bool";
            }
            else if (type == typeof(StatusKeyRef))
            {
                entry["type"] = "status";
            }
            else if (type.IsEnum)
            {
                entry["type"] = "enum";
                entry["options"] = Names(type);
            }
            else
            {
                throw new InvalidOperationException(
                    "노트북이 그릴 수 없는 저작 필드 타입이다: " + owner.Name + "." + field.Name
                    + " (" + type.Name + "). authoring-schema의 타입 표를 넓히거나 필드를 바꿔라.");
            }

            return entry;
        }

        private static FieldInfo FieldFor(Type owner, string camelName)
        {
            foreach (var field in owner.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(field.Name, camelName, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }

            throw new InvalidOperationException(
                "필드를 찾지 못했다: " + owner.Name + "." + camelName);
        }

        private static JArray Names(Type enumType) => new JArray(Enum.GetNames(enumType));
    }
}
```

- [ ] **Step 3: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter SchemaFileMatchesCatalog
```

기대: **FAIL**, 메시지 `authoring-schema.json이 EffectSpecCatalog와 달라 갱신했다`.
그리고 `Tools/card-idea-notebook/authoring-schema.json`이 **생겨 있어야 한다.**

- [ ] **Step 4: 생성된 스키마를 눈으로 검수한다**

```bash
cat Tools/card-idea-notebook/authoring-schema.json
```

확인할 것 다섯. **하나라도 다르면 멈추고 원인을 찾는다** — 이 순서가 틀리면 Task 4의 라운드트립이
전부 깨진다.

1. `effects` 배열의 길이가 **8**이다 (`damage`, `apply_status`, `grant_next_player_damage_card_bonus`,
   `nullify_next_player_condition_reward`, `move_formation`, `consume_status`, `trigger_status`,
   `grant_next_turn_fate`).
2. `apply_status`의 `fields`가 `status`, `count`, `target`, `selector` **이 순서**다.
   `spore_veil.json`의 키 순서와 같아야 한다.
3. `interventions` 배열의 길이가 **3**이다.
   - `change_execution_order` → `fields`가 `delta`, `targetSide`. `delay.json`과 같은 순서다.
   - `swap_execution_order` → `fields`가 `targetSide`, `requireAdjacent`.
   - `lock` → `fields`가 **빈 배열**이다. 파라미터가 없는 액션이며, 이것이 계획 3.5의 결과다.
   - `targetSide`의 `options`가 `Any`, `Player`, `Enemy`다.
4. `cardFields`가 **객체**이고 키가 둘이다.
   - `Execution` → `id`, `name`, `side`, `category`, `energyCost`, `baseExecutionOrder`, `effects`,
     `grade`, `tags`. `vanguard_slash.json`의 키 순서와 같아야 한다.
   - `Intervention` → `id`, `name`, `side`, `category`, `energyCost`, `intervention`, `grade`, `tags`.
     `delay.json`의 키 순서와 같아야 한다.
5. `selectors`가 `None`, `FrontOne`, `BackOne`, `All`, `FrontTwo`, `BackTwo`다.
   `TargetSelectorRef`의 숫자 값은 연속이 아니지만(`None=0, FrontOne=1, BackOne=3, All=5,
   FrontTwo=6, BackTwo=7`) **이름만 쓰므로 상관없다.**

`interventionSides`라는 최상위 키는 **없다.** 진영 제한은 이제 개입 스펙 안의 필드이므로 3번의
`options`로 나온다 — 효과의 열거 필드와 똑같은 취급이다.

- [ ] **Step 5: 다시 실행해 통과를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

기대: **Passed! - Failed: 0, Passed: 526** (기준선 525 + 신규 1).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Tests/EditMode/TestContent.cs Assets/Core/Tests/EditMode/AuthoringSchemaExportTests.cs Tools/card-idea-notebook/authoring-schema.json
git commit -m "feat(core): 카드 저작 스키마를 저작 명부에서 생성한다

노트북이 효과와 개입의 파라미터 구조를 알아야 폼을 만드는데, 표를 복제하면 손으로
맞춰야 하고 C# 소스를 직접 읽으면 파일 위치와 문법에 결합된다. 헤드리스 테스트가
리플렉션으로 authoring-schema.json을 생성하고 커밋된 파일과 다르면 갱신 후 실패하게 한다.

키 순서는 추측하지 않고 Newtonsoft에게 빈 인스턴스를 직렬화시켜 읽는다 - 노트북이
재현해야 하는 순서가 바로 그 직렬화기의 순서이고, 그 순서는 선언 순서가 아니기 때문이다.
카드가 실행·개입 두 타입이므로 키 순서도 분류마다 따로 낸다."
```

---

### Task 2: 노트북의 스키마 로더

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 스크립트, `globalThis.CardIdeaNotebook` export 블록 직전)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 1의 `authoring-schema.json`
- Produces: `parseAuthoringSchema(text)` → 아래 형태의 객체. Task 3~6이 인자로 받는다.

```js
{
  effects: { apply_status: { kind, label, fields: [{name, type, options?}] }, … },  // kind로 색인
  effectOrder: ["damage", "apply_status", …],   // 카탈로그 등록 순서. 드롭다운 순서다
  interventions: { change_execution_order: { kind, label, fields: [...] }, … },     // 효과와 같은 모양
  interventionOrder: ["change_execution_order", "swap_execution_order", "lock"],
  condition: { kinds: [...], fields: [{name, type, options?}] },
  cardFields: { Execution: [...], Intervention: [...] },   // 분류별 키 순서
  sides: [...], categories: [...], grades: [...],
  selectors: [...], statusTargets: [...]
}
```

**효과와 개입을 같은 모양으로 푸는 것이 요점이다.** 둘 다 `{kind, label, fields[]}`이므로 계획 B의
폼 렌더러 하나가 양쪽을 그린다. 옛 판의 `interventions`(문자열 배열)와 `interventionSides`는 없다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tools/card-idea-notebook/index.test.mjs`의 맨 끝에 붙인다. 파일 상단의 `loadCore()`와
`htmlUrl`은 이미 있으므로 그대로 쓴다.

```js
const schemaUrl = new URL("./authoring-schema.json", import.meta.url);

function loadSchema() {
  const core = loadCore();
  return core.parseAuthoringSchema(readFileSync(fileURLToPath(schemaUrl), "utf8"));
}

test("생성된 스키마에서 효과 여덟 종을 읽는다", () => {
  const schema = loadSchema();
  assert.equal(schema.effectOrder.length, 8);
  assert.ok(schema.effects.apply_status, "apply_status가 있어야 한다");
  assert.equal(schema.effects.apply_status.label, "상태 부여");
});

test("효과 필드의 이름과 순서를 저작 파일과 같게 읽는다", () => {
  const schema = loadSchema();
  const names = schema.effects.apply_status.fields.map((field) => field.name);
  assert.deepEqual(names, ["status", "count", "target", "selector"]);
});

test("필드 타입과 열거 항목을 읽는다", () => {
  const schema = loadSchema();
  const fields = schema.effects.apply_status.fields;
  assert.equal(fields.find((f) => f.name === "count").type, "int");
  assert.equal(fields.find((f) => f.name === "status").type, "status");
  const target = fields.find((f) => f.name === "target");
  assert.equal(target.type, "enum");
  assert.ok(target.options.includes("TargetEnemy"));
});

test("분류별 카드 키 순서를 저작 파일과 같게 읽는다", () => {
  const schema = loadSchema();
  assert.deepEqual(schema.cardFields.Execution, [
    "id", "name", "side", "category", "energyCost", "baseExecutionOrder",
    "effects", "grade", "tags",
  ]);
  assert.deepEqual(schema.cardFields.Intervention, [
    "id", "name", "side", "category", "energyCost", "intervention", "grade", "tags",
  ]);
});

test("개입 세 종을 효과와 같은 모양으로 읽는다", () => {
  const schema = loadSchema();
  assert.deepEqual(schema.interventionOrder,
    ["change_execution_order", "swap_execution_order", "lock"]);

  const change = schema.interventions.change_execution_order;
  assert.equal(change.label, "실행 순서 변경");
  assert.deepEqual(change.fields.map((f) => f.name), ["delta", "targetSide"]);
  assert.equal(change.fields.find((f) => f.name === "delta").type, "int");
  assert.deepEqual(change.fields.find((f) => f.name === "targetSide").options,
    ["Any", "Player", "Enemy"]);

  assert.deepEqual(schema.interventions.lock.fields, [],
    "lock은 파라미터가 없다 — 계획 3.5의 결과다");
});

test("스키마가 깨지면 이유를 던진다", () => {
  const core = loadCore();
  assert.throws(() => core.parseAuthoringSchema("{}"), /effects/);
  assert.throws(() => core.parseAuthoringSchema("not json"), /스키마/);
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: **FAIL**, `core.parseAuthoringSchema is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

`index.html`의 코어 스크립트에서 `globalThis.CardIdeaNotebook = Object.freeze({` **바로 위**에
넣는다:

```js
    /// 생성된 저작 스키마를 폼이 쓸 형태로 바꾼다. 이 파일은 헤드리스 테스트가 만들므로
    /// 손으로 고치지 않는다 - 내용이 이상하면 dotnet test를 돌린다.
    function parseAuthoringSchema(text) {
      let raw;
      try {
        raw = JSON.parse(text);
      } catch (error) {
        throw new Error(`저작 스키마를 읽을 수 없습니다: ${error.message}`);
      }

      if (!Array.isArray(raw.effects) || !raw.effects.length) {
        throw new Error("저작 스키마에 effects 배열이 없습니다.");
      }
      if (!Array.isArray(raw.interventions) || !raw.interventions.length) {
        throw new Error("저작 스키마에 interventions 배열이 없습니다.");
      }
      if (!raw.cardFields || !Array.isArray(raw.cardFields.Execution)) {
        throw new Error("저작 스키마에 분류별 cardFields가 없습니다.");
      }

      /// 효과와 개입은 스키마에서 같은 모양이므로 같은 함수로 색인한다.
      function indexByKind(entries) {
        const byKind = {};
        const order = [];
        for (const entry of entries) {
          byKind[entry.kind] = Object.freeze({
            kind: entry.kind,
            label: entry.label,
            fields: Object.freeze(entry.fields.map((field) => Object.freeze({ ...field }))),
          });
          order.push(entry.kind);
        }

        return { byKind: Object.freeze(byKind), order: Object.freeze(order) };
      }

      const effects = indexByKind(raw.effects);
      const interventions = indexByKind(raw.interventions);
      const cardFields = {};
      for (const [category, names] of Object.entries(raw.cardFields)) {
        cardFields[category] = Object.freeze([...names]);
      }

      return Object.freeze({
        effects: effects.byKind,
        effectOrder: effects.order,
        interventions: interventions.byKind,
        interventionOrder: interventions.order,
        condition: Object.freeze({
          kinds: Object.freeze([...raw.condition.kinds]),
          fields: Object.freeze(raw.condition.fields.map((f) => Object.freeze({ ...f }))),
        }),
        cardFields: Object.freeze(cardFields),
        sides: Object.freeze([...raw.sides]),
        categories: Object.freeze([...raw.categories]),
        grades: Object.freeze([...raw.grades]),
        selectors: Object.freeze([...raw.selectors]),
        statusTargets: Object.freeze([...raw.statusTargets]),
      });
    }
```

그리고 export 블록의 목록 맨 앞에 한 줄 더한다:

```js
    globalThis.CardIdeaNotebook = Object.freeze({
      parseAuthoringSchema,
      ROLE_LABELS,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: 새 테스트 6개 PASS, 기존 테스트 전부 PASS(회귀 없음).

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 저작 스키마를 읽는다

효과 여덟 종과 개입 세 종의 파라미터 구조, 그리고 분류별 카드 키 순서를 생성된
authoring-schema.json에서 가져온다. 효과와 개입이 같은 모양이라 색인 함수 하나로
둘을 처리한다. 아직 아무도 호출하지 않으며 Markdown 저작 경로는 그대로다."
```

---

### Task 3: 카드 JSON을 모델로 읽기

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 스크립트)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 2의 `parseAuthoringSchema` 결과
- Produces: `readCardJson(text, schema)` → `{ card, errors }`.
  `errors`가 비어 있지 않으면 `card`는 `null`이다. Task 4가 `card`를 되돌린다.

카드 모델(설계 §5 + 보존 필드):

```js
{
  uid: "",                    // 노트북 내부 식별자. 파일에 나가지 않는다
  id, name,
  side, category,             // 문자열. "Player" / "Execution"
  energyCost,                 // 숫자. 없으면 0
  baseExecutionOrder,         // 숫자. 실행 카드에서만 의미가 있다
  effects,                    // null 또는 배열. null과 []는 다르다. 개입 카드는 항상 null
  intervention,               // 개입 행 하나 또는 null. 실행 카드는 항상 null
  grade,                      // 문자열. 없으면 "None"
  tags,                       // null 또는 배열
  unknownKeys: [],            // 모르는 최상위 키 이름. 보존하되 오류로 표시한다(설계 §10.2)
  extra: {},                  // 그 키들의 원본 값
  base: "",                   // 저장소에서 읽은 원본 문자열
}
```

효과 행 모델:

```js
{ kind, params: { … }, condition: { kind, n, successEffectValue, skipOnBasic }, raw: null }
```

개입 행 모델 — **효과 행과 같되 조건이 없다.** 개입에는 조건 시스템이 없기 때문이다:

```js
{ kind, params: { … }, raw: null }
```

`kind`가 스키마에 없으면 양쪽 다 `raw`에 원본 객체를 담고 Task 4의 writer가 그것을 그대로 쓴다.
설계 §10.2의 "이해하지 못한 것은 표시하되 보존한다"다.

**분류에 없는 필드는 모델에 남아 있어도 파일에 나가지 않는다.** 라이터가 `schema.cardFields[category]`를
따르므로, 실행 카드의 `intervention`이나 개입 카드의 `effects`는 키 목록에 없어 그냥 건너뛴다.
계획 3.5가 C# 쪽에서 타입으로 갈라놓은 것을 노트북은 키 목록으로 지킨다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`index.test.mjs` 끝에 붙인다.

```js
const cardsDir = new URL("../../Assets/StreamingAssets/Content/Cards/", import.meta.url);

function readCardFile(name) {
  return readFileSync(fileURLToPath(new URL(name, cardsDir)), "utf8");
}

test("실행 카드를 모델로 읽는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card, errors } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.deepEqual(errors, []);
  assert.equal(card.id, "vanguard_slash");
  assert.equal(card.name, "선봉 베기");
  assert.equal(card.side, "Player");
  assert.equal(card.category, "Execution");
  assert.equal(card.energyCost, 1);
  assert.equal(card.baseExecutionOrder, 3);
  assert.equal(card.grade, "Common");
  assert.deepEqual(card.tags, ["시작", "공격"]);
  assert.equal(card.effects.length, 1);
  assert.deepEqual(card.effects[0].params, { value: 5, selector: "FrontOne" });
  assert.equal(card.effects[0].condition, null);
});

test("조건부 효과의 조건을 읽는다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("riposte.json"), loadSchema());
  assert.deepEqual(card.effects[0].condition, {
    kind: "PrevExecutedIsEnemyDamageCard", n: 0, successEffectValue: 7, skipOnBasic: false,
  });
});

test("개입 카드를 중첩 스펙으로 읽는다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("hasten.json"), loadSchema());
  assert.equal(card.category, "Intervention");
  assert.equal(card.effects, null, "개입 카드에는 effects가 없다");
  assert.equal(card.intervention.kind, "change_execution_order");
  assert.deepEqual(card.intervention.params, { delta: -1, targetSide: "Player" });
});

test("파라미터가 없는 개입도 읽는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = JSON.stringify({
    id: "seal", name: "봉인", side: "Player", category: "Intervention",
    energyCost: 1, intervention: { kind: "lock" },
  }, null, 2);
  const { card, errors } = core.readCardJson(text, schema);
  assert.deepEqual(errors, []);
  assert.equal(card.intervention.kind, "lock");
  assert.deepEqual(card.intervention.params, {});
});

test("생략된 개입 파라미터는 모델에 나타나지 않는다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("crossover.json"), loadSchema());
  assert.equal(card.intervention.kind, "swap_execution_order");
  assert.deepEqual(card.intervention.params, { requireAdjacent: true },
    "targetSide는 Any라 파일에 없고 모델에도 없어야 한다");
});

test("빈 배열과 없는 배열을 구분한다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("fixture_attack.json"), loadSchema());
  assert.deepEqual(card.tags, [], "tags는 빈 배열로 저작되어 있다");
  assert.equal(card.grade, "None", "grade는 생략되어 있다");
});

test("모르는 효과 kind를 버리지 않고 보존한다", () => {
  const core = loadCore();
  const text = JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution",
    effects: [{ kind: "teleport", distance: 3 }],
  }, null, 2);
  const { card, errors } = core.readCardJson(text, loadSchema());
  assert.deepEqual(errors, []);
  assert.equal(card.effects[0].kind, "teleport");
  assert.deepEqual(card.effects[0].raw, { kind: "teleport", distance: 3 });
});

test("모르는 최상위 키를 보존하고 이름을 알려준다", () => {
  const core = loadCore();
  const text = JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution", flavour: "설명",
  }, null, 2);
  const { card } = core.readCardJson(text, loadSchema());
  assert.deepEqual(card.unknownKeys, ["flavour"]);
  assert.equal(card.extra.flavour, "설명");
});

test("깨진 JSON은 카드를 만들지 않고 이유를 준다", () => {
  const core = loadCore();
  const { card, errors } = core.readCardJson("{ 이건 JSON이 아니다", loadSchema());
  assert.equal(card, null);
  assert.equal(errors.length, 1);
});

test("필수 키가 빠지면 이유를 준다", () => {
  const core = loadCore();
  const { card, errors } = core.readCardJson('{"id":"x","name":"y"}', loadSchema());
  assert.equal(card, null);
  assert.ok(errors.some((message) => message.includes("side")));
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: **FAIL**, `core.readCardJson is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

`parseAuthoringSchema` 아래에 넣는다:

```js
    const CARD_REQUIRED_KEYS = Object.freeze(["id", "name", "side", "category"]);

    /// CardSpec에 대응하지 않는 노트북 전용 키. 모델에는 있고 파일에는 없다.
    const CARD_LOCAL_KEYS = Object.freeze(["uid", "unknownKeys", "extra", "base"]);

    function emptyConditionValue() {
      return { kind: "None", n: 0, successEffectValue: 0, skipOnBasic: false };
    }

    /// 스키마가 아는 파라미터만 골라 담는다. 파일에 없는 키는 모델에도 넣지 않는다 -
    /// 생략된 기본값을 되살려 넣으면 왕복에서 없던 키가 생긴다.
    function readParams(entry, known) {
      const params = {};
      for (const field of known.fields) {
        if (Object.hasOwn(entry, field.name)) params[field.name] = entry[field.name];
      }

      return params;
    }

    /// 효과 하나를 모델 행으로. 스키마에 없는 kind는 원본을 통째로 들고 있는다(설계 §10.2).
    function readEffectEntry(entry, schema) {
      const kind = String(entry?.kind ?? "");
      const known = schema.effects[kind];
      if (!known) {
        return { kind, params: {}, condition: null, raw: entry };
      }

      let condition = null;
      if (entry.condition) {
        condition = { ...emptyConditionValue(), ...entry.condition };
      }

      return { kind, params: readParams(entry, known), condition, raw: null };
    }

    /// 개입 하나를 모델 행으로. 효과와 같되 조건이 없다 - 개입에는 조건 시스템이 없다.
    function readInterventionEntry(entry, schema) {
      const kind = String(entry?.kind ?? "");
      const known = schema.interventions[kind];
      if (!known) {
        return { kind, params: {}, raw: entry };
      }

      return { kind, params: readParams(entry, known), raw: null };
    }

    function readCardJson(text, schema) {
      let raw;
      try {
        raw = JSON.parse(text);
      } catch (error) {
        return { card: null, errors: [`JSON을 읽을 수 없습니다: ${error.message}`] };
      }

      const errors = [];
      for (const key of CARD_REQUIRED_KEYS) {
        if (!Object.hasOwn(raw, key)) errors.push(`필수 키 '${key}'가 없습니다.`);
      }
      if (errors.length) return { card: null, errors };

      /// 모르는 키의 기준이 분류마다 다르다 - 실행 카드의 intervention은 모르는 키다.
      const category = String(raw.category);
      const fields = schema.cardFields[category];
      if (!fields) {
        return { card: null, errors: [`모르는 카드 분류입니다: '${category}'`] };
      }

      const known = new Set(fields);
      const unknownKeys = [];
      const extra = {};
      for (const key of Object.keys(raw)) {
        if (known.has(key)) continue;
        unknownKeys.push(key);
        extra[key] = raw[key];
      }

      const card = {
        uid: "",
        id: String(raw.id),
        name: String(raw.name),
        side: String(raw.side),
        category,
        energyCost: Number(raw.energyCost ?? 0),
        baseExecutionOrder: Number(raw.baseExecutionOrder ?? 0),
        effects: Array.isArray(raw.effects)
          ? raw.effects.map((entry) => readEffectEntry(entry, schema))
          : null,
        intervention: raw.intervention
          ? readInterventionEntry(raw.intervention, schema)
          : null,
        grade: String(raw.grade ?? schema.grades[0]),
        tags: Array.isArray(raw.tags) ? [...raw.tags] : null,
        unknownKeys,
        extra,
        base: text,
      };

      return { card, errors: [] };
    }
```

export 블록에 두 줄을 더한다:

```js
      parseAuthoringSchema,
      readCardJson,
      emptyConditionValue,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: 새 테스트 10개 PASS, 기존 테스트 회귀 없음.

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 카드 JSON을 모델로 읽는다

null 배열과 빈 배열을 구분해 들고, 모르는 효과 kind와 모르는 최상위 키는 원본을
보존한다 - 노트북을 한 번 거쳤다고 남의 저작이 사라지면 안 된다(설계 10.2).

개입은 효과와 같은 형태의 행 하나로 읽는다. 모르는 키의 기준이 분류마다 다르므로
스키마의 분류별 키 목록을 기준으로 삼는다 - 실행 카드의 intervention은 모르는 키다."
```

---

### Task 4: 모델을 카드 JSON으로 되돌리기 — 라운드트립

이 계획에서 가장 중요한 태스크다. 여기가 통과하면 나머지는 부수적이다.

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 스크립트)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 3의 `readCardJson`, Task 2의 스키마
- Produces: `writeCardJson(card, schema)` → 파일에 쓸 문자열(끝에 개행 포함)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`index.test.mjs` 끝에 붙인다. `readdirSync`가 필요하므로 파일 상단 import를 고친다:

```js
import { existsSync, readdirSync, readFileSync } from "node:fs";
```

그리고:

```js
test("저장소의 모든 카드가 바이트 그대로 왕복한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const names = readdirSync(fileURLToPath(cardsDir)).filter((n) => n.endsWith(".json"));
  assert.ok(names.length >= 26, `카드가 26장 이상이어야 한다. 실제 ${names.length}`);

  const broken = [];
  for (const name of names) {
    const original = readCardFile(name);
    const { card, errors } = core.readCardJson(original, schema);
    if (errors.length) {
      broken.push(`${name}: ${errors.join(", ")}`);
      continue;
    }
    const written = core.writeCardJson(card, schema);
    if (written !== original) broken.push(name);
  }

  assert.deepEqual(broken, [], "왕복에서 바뀐 카드가 없어야 한다");
});

test("파일 끝에 개행을 하나 붙인다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const written = core.writeCardJson(card, schema);
  assert.ok(written.endsWith("}\n"));
  assert.ok(!written.endsWith("}\n\n"));
});

test("기본값 멤버를 생략하되 side와 category는 항상 쓴다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution",
  }, null, 2), schema);
  const written = JSON.parse(core.writeCardJson(card, schema));
  assert.deepEqual(Object.keys(written), ["id", "name", "side", "category"]);
});

test("분류에 없는 키는 모델에 있어도 나가지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  // 실행 카드 모델에 개입을 억지로 넣어도 실행 카드의 키 목록에 없으므로 무시된다.
  card.intervention = { kind: "lock", params: {}, raw: null };
  const written = JSON.parse(core.writeCardJson(card, schema));

  assert.equal(written.intervention, undefined);
  assert.ok(written.effects, "실행 카드의 효과는 그대로 나간다");
});

test("파라미터 없는 개입은 kind만 쓴다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "seal", name: "봉인", side: "Player", category: "Intervention",
    energyCost: 1, intervention: { kind: "lock" },
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original);
});

test("모르는 효과 kind를 원본 그대로 되돌린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution",
    effects: [{ kind: "teleport", distance: 3 }],
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original);
});

test("모르는 최상위 키를 원본 그대로 되돌린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution", flavour: "설명",
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original);
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: **FAIL**, `core.writeCardJson is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

`readCardJson` 아래에 넣는다. 생략 규칙이 이 함수의 전부다 — 위 "실측한 직렬화 규칙" 표를
코드로 옮긴 것이다.

```js
    /// 기본값이면 생략한다. C#의 DefaultValueHandling.Ignore를 그대로 옮긴 것이다.
    /// 배열은 null일 때만 생략한다 - string[]의 기본값이 null이라 []는 기본값이 아니다.
    function isDefaultValue(value, type, schema) {
      if (value === null || value === undefined) return true;
      if (Array.isArray(value)) return false;
      if (type === "int") return Number(value) === 0;
      if (type === "bool") return value !== true;
      if (type === "status") return String(value) === "";
      if (type === "enum") return value === schema[0];
      return false;
    }

    /// 파라미터 블록 하나를 파일 형태로. 효과와 개입이 스키마에서 같은 모양이라 함께 쓴다.
    function writeParams(entry, known) {
      const out = { kind: entry.kind };
      for (const field of known.fields) {
        const value = entry.params[field.name];
        const fallback = field.type === "enum" ? field.options : null;
        if (isDefaultValue(value, field.type, fallback)) continue;
        out[field.name] = value;
      }

      return out;
    }

    function writeEffectEntry(effect, schema) {
      if (effect.raw) return effect.raw;

      const entry = writeParams(effect, schema.effects[effect.kind]);
      if (effect.condition && effect.condition.kind !== schema.condition.kinds[0]) {
        const condition = { kind: effect.condition.kind };
        for (const field of schema.condition.fields) {
          const value = effect.condition[field.name];
          if (isDefaultValue(value, field.type, null)) continue;
          condition[field.name] = value;
        }
        entry.condition = condition;
      }

      return entry;
    }

    /// 개입은 효과에서 조건만 뺀 것이다.
    function writeInterventionEntry(intervention, schema) {
      if (intervention.raw) return intervention.raw;
      return writeParams(intervention, schema.interventions[intervention.kind]);
    }

    /// 카드 하나를 저장소 파일과 바이트가 같은 문자열로. 키 순서와 어떤 키가 나갈지는 둘 다
    /// 스키마의 분류별 cardFields가 정한다 - 그래서 실행 카드에 intervention이, 개입 카드에
    /// effects가 실릴 수 없다(설계 8, 계획 3.5의 타입 분리).
    function writeCardJson(card, schema) {
      const always = new Set(["side", "category"]);
      const enumOf = { side: schema.sides, category: schema.categories, grade: schema.grades };
      const typeOf = {
        id: "string", name: "string",
        side: "enum", category: "enum", grade: "enum",
        energyCost: "int", baseExecutionOrder: "int",
      };

      const fields = schema.cardFields[card.category];
      if (!fields) throw new Error(`모르는 카드 분류입니다: '${card.category}'`);

      const out = {};
      for (const key of fields) {
        if (key === "effects") {
          if (card.effects !== null) {
            out.effects = card.effects.map((effect) => writeEffectEntry(effect, schema));
          }
          continue;
        }
        if (key === "intervention") {
          if (card.intervention !== null) {
            out.intervention = writeInterventionEntry(card.intervention, schema);
          }
          continue;
        }
        if (key === "tags") {
          if (card.tags !== null) out.tags = [...card.tags];
          continue;
        }

        const value = card[key];
        const type = typeOf[key] ?? "string";
        if (!always.has(key) && isDefaultValue(value, type, enumOf[key] ?? null)) continue;
        out[key] = value;
      }

      for (const key of card.unknownKeys) {
        out[key] = card.extra[key];
      }

      return `${JSON.stringify(out, null, 2)}\n`;
    }
```

export 블록에 한 줄 더한다:

```js
      readCardJson,
      writeCardJson,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: 새 테스트 7개 PASS. 특히 `저장소의 모든 카드가 바이트 그대로 왕복한다`가 통과해야 한다.
이 테스트는 실행 카드 22장과 **개입 카드 4장**을 모두 훑으므로, 개입 중첩 처리가 틀리면 여기서 걸린다.

**여기서 실패하면 절대 테스트를 느슨하게 고치지 말 것.** 실패한 카드 이름이 메시지에 나오므로
그 파일과 `writeCardJson`의 출력을 직접 비교한다:

```bash
node -e "const {readFileSync}=require('fs');const a=readFileSync('Assets/StreamingAssets/Content/Cards/toxic_reclaim.json','utf8');console.log(JSON.stringify(a))"
```

흔한 원인 넷: 스키마의 필드 순서가 틀림(Task 1 Step 4로 돌아간다), 생략 규칙이 어긋남,
파일 끝 개행 처리, 그리고 **분류별 키 목록을 안 쓰고 한 목록을 공용으로 쓴 경우**.

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 카드 모델을 JSON으로 되돌린다

저장소의 카드 26장을 읽어 다시 쓰면 바이트가 같다. 키 순서는 스키마의 cardFields가
정하고 생략 규칙은 DefaultValueHandling.Ignore를 그대로 옮겼다. 이것이 맞아야
저작하지 않은 카드가 diff에 뜨지 않는다."
```

---

### Task 5: 풀 읽기와 쓰기

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 스크립트)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Produces: `readPoolJson(text)` → `{ pool, errors }`, `writePoolJson(pool)` → 문자열.
  풀 모델은 `{ id, cards: [...], unknownKeys: [], extra: {}, base: "" }`.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```js
const poolsDir = new URL("../../Assets/StreamingAssets/Content/Pools/", import.meta.url);

test("풀을 읽고 카드 순서를 그대로 보존한다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool, errors } = core.readPoolJson(text);
  assert.deepEqual(errors, []);
  assert.equal(pool.id, "starter");
  assert.equal(pool.cards.length, 22);
  assert.equal(pool.cards[0], "vanguard_slash");
  assert.equal(pool.cards[21], "posthumous_spread");
});

test("저장소의 모든 풀이 바이트 그대로 왕복한다", () => {
  const core = loadCore();
  const names = readdirSync(fileURLToPath(poolsDir)).filter((n) => n.endsWith(".json"));
  assert.ok(names.length >= 1);
  for (const name of names) {
    const original = readFileSync(fileURLToPath(new URL(name, poolsDir)), "utf8");
    const { pool } = core.readPoolJson(original);
    assert.equal(core.writePoolJson(pool), original, name);
  }
});

test("중복 카드를 지우지 않고 그대로 들고 있는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","a","b"]}');
  assert.deepEqual(pool.cards, ["a", "a", "b"]);
});

test("깨진 풀은 이유를 준다", () => {
  const core = loadCore();
  const broken = core.readPoolJson("{ 아님");
  assert.equal(broken.pool, null);
  assert.equal(broken.errors.length, 1);

  const missing = core.readPoolJson('{"id":"p"}');
  assert.equal(missing.pool, null);
  assert.ok(missing.errors.some((message) => message.includes("cards")));
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: **FAIL**, `core.readPoolJson is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

```js
    const POOL_REQUIRED_KEYS = Object.freeze(["id", "cards"]);
    const POOL_FIELDS = Object.freeze(["id", "cards"]);

    function readPoolJson(text) {
      let raw;
      try {
        raw = JSON.parse(text);
      } catch (error) {
        return { pool: null, errors: [`JSON을 읽을 수 없습니다: ${error.message}`] };
      }

      const errors = [];
      for (const key of POOL_REQUIRED_KEYS) {
        if (!Object.hasOwn(raw, key)) errors.push(`필수 키 '${key}'가 없습니다.`);
      }
      if (errors.length) return { pool: null, errors };

      const known = new Set(POOL_FIELDS);
      const unknownKeys = [];
      const extra = {};
      for (const key of Object.keys(raw)) {
        if (known.has(key)) continue;
        unknownKeys.push(key);
        extra[key] = raw[key];
      }

      return {
        pool: {
          id: String(raw.id),
          cards: Array.isArray(raw.cards) ? raw.cards.map(String) : [],
          unknownKeys,
          extra,
          base: text,
        },
        errors: [],
      };
    }

    function writePoolJson(pool) {
      const out = { id: pool.id, cards: [...pool.cards] };
      for (const key of pool.unknownKeys) out[key] = pool.extra[key];
      return `${JSON.stringify(out, null, 2)}\n`;
    }
```

export 블록에 두 줄 더한다:

```js
      readPoolJson,
      writePoolJson,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 풀 JSON을 읽고 되돌린다

저작 순서를 그대로 보존한다. 중복 카드도 지우지 않고 들고 있는다 - 조용히 고치면
노트북을 열었다 닫는 것만으로 풀 편성이 바뀐다(설계 10.4)."
```

---

### Task 6: 검증

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 스크립트)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 3~5의 모델
- Produces: `validateContent({cards, pools, statusKeys, schema})` →
  `{ errors: [{scope, id, message}], warnings: [{scope, id, message}] }`.
  `scope`는 `"card"` 또는 `"pool"`이다. Task 7과 계획 B의 UI가 이것을 그린다.

설계 §9의 표를 그대로 옮긴다. 각 규칙의 근거는 C# 로더에 있으므로 메시지를 비슷하게 맞춘다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```js
function cardOf(core, schema, overrides) {
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution", ...overrides,
  }, null, 2), schema);
  return card;
}

function poolOf(core, cards) {
  return core.readPoolJson(JSON.stringify({ id: "starter", cards })).pool;
}

const STATUS_KEYS = ["poison", "block", "haste"];

test("id 형식과 중복을 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const bad = core.validateContent({
    cards: [cardOf(core, schema, { id: "Vanguard Slash" })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(bad.errors.some((e) => e.message.includes("형식")));

  const dupe = core.validateContent({
    cards: [cardOf(core, schema, {}), cardOf(core, schema, {})],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(dupe.errors.some((e) => e.message.includes("중복")));
});

test("개입 카드의 액션을 검사한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const missing = core.validateContent({
    cards: [cardOf(core, schema, { category: "Intervention" })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(missing.errors.some((e) => e.message.includes("개입 액션")));

  const unknown = core.validateContent({
    cards: [cardOf(core, schema, {
      category: "Intervention", intervention: { kind: "teleport" },
    })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(unknown.errors.some((e) => e.message.includes("teleport")));
});

test("등록되지 않은 상태 키를 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, {
      effects: [{ kind: "apply_status", status: "posion", count: 1 }],
    })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("posion")));
});

test("consume_status의 maxAmount 하한을 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, {
      effects: [{ kind: "consume_status", status: "poison", maxAmount: 0 }],
    })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("maxAmount")));
});

test("풀 소속 카드에만 등급과 태그를 요구한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const card = cardOf(core, schema, { tags: [] });

  const free = core.validateContent({
    cards: [card], pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.deepEqual(free.errors, [], "풀에 없으면 등급·태그가 없어도 정상이다");

  const pooled = core.validateContent({
    cards: [card], pools: [poolOf(core, ["probe"])], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(pooled.errors.some((e) => e.message.includes("등급")));
  assert.ok(pooled.errors.some((e) => e.message.includes("태그")));
});

test("풀의 없는 카드와 중복을 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, { grade: "Common", tags: ["시작"] })],
    pools: [poolOf(core, ["probe", "probe", "ghost"])],
    statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("ghost")));
  assert.ok(result.errors.some((e) => e.message.includes("중복")));
});

test("효과 없는 실행 카드와 고아 카드는 경고에 그친다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, { effects: [] })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.deepEqual(result.errors, []);
  assert.ok(result.warnings.length >= 2, "효과 0개 경고와 고아 경고");
});

test("모르는 최상위 키는 부팅 거부라고 알린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution", flavour: "설명",
  }, null, 2), schema);
  const result = core.validateContent({
    cards: [card], pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("flavour")));
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: **FAIL**, `core.validateContent is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

```js
    const CARD_ID_PATTERN = /^[a-z0-9_]+$/;

    /// 로더가 거부할 것을 내보내기 전에 잡는다(설계 9). 통과가 부팅 성공을 보장하지는 않지만,
    /// 실패가 드러나는 시점을 게임 실행에서 저작 중으로 당긴다.
    function validateContent({ cards, pools, statusKeys, schema }) {
      const errors = [];
      const warnings = [];
      const statuses = new Set(statusKeys);
      const fail = (id, message) => errors.push({ scope: "card", id, message });
      const warn = (id, message) => warnings.push({ scope: "card", id, message });

      const seenIds = new Set();
      const byId = new Map();
      for (const card of cards) {
        if (!card.id) fail(card.id, "id가 없습니다.");
        else if (!CARD_ID_PATTERN.test(card.id)) {
          fail(card.id, `id 형식이 잘못되었습니다. 소문자·숫자·밑줄만 씁니다: '${card.id}'`);
        } else if (seenIds.has(card.id)) {
          fail(card.id, `id가 중복입니다: '${card.id}'`);
        } else {
          seenIds.add(card.id);
        }
        byId.set(card.id, card);

        if (!card.name) fail(card.id, "이름이 없습니다.");

        for (const key of card.unknownKeys) {
          fail(card.id, `모르는 키 '${key}'가 있어 부팅이 이 카드를 거부합니다.`);
        }

        if (card.category === "Intervention") {
          if (!card.intervention) {
            fail(card.id, "개입 카드에는 개입 액션이 필요합니다.");
          } else if (card.intervention.raw) {
            fail(card.id, `노트북이 모르는 개입입니다: '${card.intervention.kind}'`);
          }
          continue;
        }

        const effects = card.effects ?? [];
        if (!effects.length) warn(card.id, "실행 카드인데 효과가 없습니다.");

        for (const effect of effects) {
          if (effect.raw) {
            fail(card.id, `노트북이 모르는 효과입니다: '${effect.kind}'`);
            continue;
          }

          const status = effect.params.status;
          if (status !== undefined && !statuses.has(status)) {
            fail(card.id, `등록되지 않은 상태 키입니다: '${status}'`);
          }
          if (effect.kind === "consume_status" && Number(effect.params.maxAmount ?? 0) < 1) {
            fail(card.id, "consume_status의 maxAmount는 1 이상이어야 합니다.");
          }
        }
      }

      const pooled = new Set();
      for (const pool of pools) {
        const seen = new Set();
        for (const cardId of pool.cards) {
          pooled.add(cardId);
          const card = byId.get(cardId);
          if (!card) {
            errors.push({ scope: "pool", id: pool.id, message: `없는 카드입니다: '${cardId}'` });
            continue;
          }
          if (!seen.add(cardId)) {
            errors.push({ scope: "pool", id: pool.id, message: `카드가 중복입니다: '${cardId}'` });
            continue;
          }

          if (card.grade === schema.grades[0]) {
            errors.push({ scope: "pool", id: pool.id, message: `'${cardId}'에 등급이 없습니다.` });
          }

          const tags = card.tags ?? [];
          if (!tags.length) {
            errors.push({ scope: "pool", id: pool.id, message: `'${cardId}'에 태그가 없습니다.` });
          }

          const seenTags = new Set();
          for (const tag of tags) {
            if (!String(tag).trim()) {
              errors.push({ scope: "pool", id: pool.id, message: `'${cardId}'에 빈 태그가 있습니다.` });
            } else if (!seenTags.add(tag)) {
              errors.push({ scope: "pool", id: pool.id, message: `'${cardId}'에 중복 태그 '${tag}'가 있습니다.` });
            }
          }
        }
      }

      for (const card of cards) {
        if (card.side === schema.sides[0] && !pooled.has(card.id)) {
          warn(card.id, "어느 풀에도 없습니다.");
        }
      }

      return { errors, warnings };
    }
```

export 블록에 한 줄 더한다:

```js
      validateContent,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

- [ ] **Step 5: 저장소 콘텐츠가 실제로 통과하는지 확인한다**

검증이 진짜인지 보는 가장 좋은 방법은 부팅이 받아들이는 콘텐츠에 걸어보는 것이다.
`index.test.mjs` 끝에 하나 더 붙인다:

```js
test("저장소의 실제 콘텐츠가 검증을 통과한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const statusesDir = new URL("../../Assets/StreamingAssets/Content/Statuses/", import.meta.url);
  const statusKeys = readdirSync(fileURLToPath(statusesDir))
    .filter((n) => n.endsWith(".json"))
    .map((n) => JSON.parse(readFileSync(fileURLToPath(new URL(n, statusesDir)), "utf8")).key);

  const cards = readdirSync(fileURLToPath(cardsDir))
    .filter((n) => n.endsWith(".json"))
    .map((n) => core.readCardJson(readCardFile(n), schema).card);
  const pools = readdirSync(fileURLToPath(poolsDir))
    .filter((n) => n.endsWith(".json"))
    .map((n) => core.readPoolJson(
      readFileSync(fileURLToPath(new URL(n, poolsDir)), "utf8")).pool);

  const result = core.validateContent({ cards, pools, statusKeys, schema });
  assert.deepEqual(result.errors, [], "부팅이 받아들이는 콘텐츠는 오류가 없어야 한다");
});
```

```bash
node --test Tools/card-idea-notebook/
```

기대: PASS. **실패하면 검증이 너무 엄격한 것이다** — 부팅은 이 콘텐츠를 받아들이므로
노트북도 받아들여야 한다. 규칙을 C# 로더에 맞춰 완화한다.

- [ ] **Step 6: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 로더의 거부 사유를 미리 잡는다

id 형식·중복, 개입 키, 상태 키, maxAmount 하한, 그리고 풀에 담겼기 때문에 생기는
등급·태그 규칙을 검사한다. 저장소의 실제 콘텐츠가 통과하는지도 함께 잠근다."
```

---

### Task 7: 읽기 상태 판정

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 스크립트)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 3~5의 모델
- Produces: `resolveCardState({stored, pending, schema})`와 `resolvePoolState({stored, pending})` →
  둘 다 상태 문자열 다섯 중 하나: `"same"` · `"modified"` · `"new"` · `"conflict"` · `"missing"`.
  계획 B의 카드 목록과 풀 목록이 이 값을 배지로 그린다(설계 §11.3·§11.4).

설계 §10.3의 표를 그대로 옮긴다. `stored`는 저장소에서 방금 읽은 문자열(없으면 `null`),
`pending`은 노트북이 들고 있는 카드 모델(없으면 `null`).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```js
test("저장소와 같으면 same이다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  assert.equal(core.resolveCardState({ stored: text, pending: card, schema }), "same");
});

test("노트북 쪽이 다르면 modified다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  card.name = "바뀐 이름";
  assert.equal(core.resolveCardState({ stored: text, pending: card, schema }), "modified");
});

test("저장소에 없으면 new다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  card.base = null;
  assert.equal(core.resolveCardState({ stored: null, pending: card, schema }), "new");
});

test("양쪽이 다 바뀌었으면 conflict다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  card.name = "내 변경";
  const stored = text.replace("선봉 베기", "남의 변경");
  assert.equal(core.resolveCardState({ stored, pending: card, schema }), "conflict");
});

test("저장소만 바뀌었고 미반영이 없으면 same으로 받아들인다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  const stored = text.replace("선봉 베기", "남의 변경");
  assert.equal(core.resolveCardState({ stored, pending: card, schema }), "same");
});

test("노트북에 없고 저장소에만 있으면 missing이다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  assert.equal(core.resolveCardState({ stored: text, pending: null, schema }), "missing");
});

test("풀도 같은 다섯 상태로 판정한다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool } = core.readPoolJson(text);

  assert.equal(core.resolvePoolState({ stored: text, pending: pool }), "same");
  assert.equal(core.resolvePoolState({ stored: text, pending: null }), "missing");

  const edited = core.readPoolJson(text).pool;
  edited.cards.push("새_카드");
  assert.equal(core.resolvePoolState({ stored: text, pending: edited }), "modified");

  const moved = text.replace("vanguard_slash", "남이_바꾼_카드");
  assert.equal(core.resolvePoolState({ stored: moved, pending: edited }), "conflict");
  assert.equal(core.resolvePoolState({ stored: moved, pending: pool }), "same");
});
```

다섯 번째 테스트의 의미를 놓치지 말 것 — 저장소가 바뀌었고 노트북에 미반영이 없으면
**조용히 갱신하고 `base`를 새로 잡는다**(설계 §10.3). 호출부가 그 갱신을 하고, 이 함수는
"충돌 아님"만 알려준다.

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

기대: **FAIL**, `core.resolveCardState is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

```js
    /// 카드 하나의 읽기 상태(설계 10.3). base는 마지막으로 저장소에서 읽은 원본이고,
    /// 지금 쓰면 나올 문자열과 base를 비교해 미반영 여부를 판정한다.
    function resolveCardState({ stored, pending, schema }) {
      if (!pending) return "missing";
      if (stored === null || stored === undefined) return "new";
      if (!pending.base) return "new";

      const current = writeCardJson(pending, schema);
      const dirty = current !== pending.base;
      const moved = stored !== pending.base;

      if (dirty && moved) return "conflict";
      if (dirty) return "modified";
      return "same";
    }

    /// 풀도 같은 계약이다. 스키마가 필요 없다는 것만 다르다 - 풀 직렬화는 키가 둘뿐이다.
    function resolvePoolState({ stored, pending }) {
      if (!pending) return "missing";
      if (stored === null || stored === undefined) return "new";
      if (!pending.base) return "new";

      const dirty = writePoolJson(pending) !== pending.base;
      const moved = stored !== pending.base;

      if (dirty && moved) return "conflict";
      if (dirty) return "modified";
      return "same";
    }
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test Tools/card-idea-notebook/
```

다섯 번째 테스트가 통과하는 이유를 확인할 것: `dirty`가 false이므로 `moved`가 true여도
`"same"`이 나온다. 호출부는 그때 `base`를 `stored`로 갈아끼운다.

- [ ] **Step 5: export하고 커밋**

export 블록에 한 줄 더한다:

```js
      resolveCardState,
      resolvePoolState,
```

```bash
node --test Tools/card-idea-notebook/
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

기대: 노트북 테스트 전부 PASS, 헤드리스 **526/526**.

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 카드의 읽기 상태를 판정한다

저장소 원본과 미반영 편집분을 비교해 same·modified·new·conflict·missing을 가른다.
저장소만 바뀌고 미반영이 없으면 충돌이 아니라 조용한 갱신이다(설계 10.3)."
```

---

## 완료 기준

계획 A가 끝났을 때:

1. `dotnet test`가 **526/526** 통과한다.
2. `node --test Tools/card-idea-notebook/`이 새 테스트 전부와 기존 테스트 전부를 통과한다.
3. `Tools/card-idea-notebook/authoring-schema.json`이 커밋되어 있고 효과 8종과 개입 3종을 담는다.
4. **노트북을 브라우저로 열면 지금과 똑같이 동작한다.** Markdown 저작·내보내기·불러오기가 그대로다.
5. 저장소의 카드 26장과 풀 1개가 노트북 코어를 왕복해도 바이트가 같다.

설계 §16 검수 기준 중 이 계획이 담당하는 것은 1·3·7이다. 나머지(2·4·5·6·8)는 UI와 쓰기
경로가 필요하므로 계획 B가 맡는다.

## 다음

계획 A가 머지되면 계획 B를 작성한다. 범위는 이 문서 "이 계획의 경계" 절에 적어 두었다.
