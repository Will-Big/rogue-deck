# 상태 등록 지점 통합 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-08-03
- 상태: `완료` — 2026-08-03, Task 1~3 전부 구현
- 권위 문서: [`specs/2026-07-30-card-mutation-and-runtime-content-design.md`](../specs/2026-07-30-card-mutation-and-runtime-content-design.md)
- 선행 계획: [`../archive/plans/2026-08-02-status-content-and-authoring-surface.md`](../archive/plans/2026-08-02-status-content-and-authoring-surface.md)
- 브랜치: `refactor/status-registration-consolidation`

**Goal:** 상태 하나를 추가할 때 손대야 하는 곳을 7곳에서 4곳으로 줄이고, 수치·이름 변경을 JSON 한
줄로 만든다.

**Architecture:** 상태에 관한 모든 저작 데이터를 `StatusContentDefaults`(그리고 그것이 내보내는 JSON)
한 곳으로 모은다. 중복 나열하던 `StatusSpecCatalog`과 프로덕션 호출자가 없는 `StatusRuleCatalog`을
없애고, 표시 이름을 코드에서 콘텐츠로 옮긴다. 동작(behavior)과 키 상수는 코드에 남는다 — 데이터로
뺄 수 없고, 컴파일 타임 상수로서 값을 한다.

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), NUnit, Newtonsoft.Json,
`FateWeaver.Core`(UnityEngine 미참조)

## Global Constraints

- 헤드리스 테스트 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- 착수 시점 기준선: **헤드리스 446/446 0 skipped, Unity EditMode 520/520**, 카드 JSON 26, 상태 JSON 11
- `FateWeaver.Core`에서 `UnityEngine`을 참조하지 않는다 (규칙 6)
- 결정론: 반복 순서가 사전 구현·파일 시스템 순서에 의존하지 않는다 (규칙 7)
- 튜닝 수치를 계산식에 박지 않는다. 데이터 표에 둔다 (규칙 8)
- 새 상태 = 동작 클래스 1개 + 명시적 등록. **리플렉션 자동 등록 금지** (규칙 9)
- 카드 설명을 하드코딩하지 않는다 (규칙 10)
- 새 Unity 에셋은 1:1 `.meta`와 함께 커밋한다 (규칙 16)
- 워킹 트리를 깨끗이 남긴다 (규칙 18)
- 문서 색인을 같은 커밋에서 갱신한다 (규칙 20)
- C# 9 한계: `record struct` 금지, 기본 인터페이스 구현 금지, 파일 범위 네임스페이스 금지
- **`[SerializeReference]` 필드를 바꾸면 코드와 `.asset` YAML을 같은 커밋에서 함께 옮긴다.**
  선행 계획이 이 함정을 밟았고 헤드리스는 잡지 못한다 — Unity EditMode만 잡는다
- Unity 배치에서 `-runTests`와 `-quit`를 **함께 쓰지 않는다**

## 목표 상태

| | 지금 | 이후 |
|---|---|---|
| 상태 추가 | 7곳 | **4곳** |
| 수치·이름 변경 | 코드 2~3곳 + 재컴파일 | **JSON 한 줄** |

남는 4곳: 동작 클래스, `StatusKeys` 상수 1줄, `CombatRegistries` 등록 1줄, `StatusContentDefaults`
항목 1개(그리고 그것이 내보내는 JSON 1개).

없어지는 3곳: `StatusSpecCatalog`, `StatusRuleCatalog`, `KoreanDescriptionCatalog`의 표시 이름 등록.

---

## Task 1: `StatusRuleCatalog`을 제거한다

**Files:**
- Delete: `Assets/Core/Status/StatusRuleCatalog.cs` (+ `.meta`)
- Modify: `Assets/Core/Authoring/Statuses/StatusContentDefaults.cs`
- Modify: `Assets/Core/Tests/EditMode/StatusTests.cs`, `Assets/Core/Tests/EditMode/SlowHasteStatusTests.cs`

**Interfaces:**
- Produces: `StatusContentDefaults.Catalog().Rules`가 배율의 유일한 출처가 된다

`StatusRuleCatalog.Default()`는 프로덕션 호출자가 **0곳**이고 테스트 2곳만 부른다. 배율 상수 3개는
`StatusContentDefaults`가 참조해 쓰고 있으므로, 그리로 옮기면 클래스 전체가 사라진다.
`StatusRule`·`StatusRuleSet` 클래스는 남긴다 — 런타임이 쓴다.

- [x] **Step 1: 기준선을 기록한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 446`

- [x] **Step 2: 배율을 `StatusContentDefaults`로 인라인한다**

`StatusContentDefaults.cs`의 `Multiplier(StatusKeys.Vulnerable, StatusRuleCatalog.VulnerableIncomingPercent)`
같은 호출에서 상수 참조를 실제 숫자로 바꾸고, 그 숫자가 무엇인지 주석으로 남긴다. 매직 넘버가 아니라
**데이터 표의 값**이므로 규칙 8을 지킨다 — 계산식에 박는 게 아니다.

```csharp
            // 취약 150 = 받는 피해 +50%, 약화·손상 75 = -25%.
            Multiplier(StatusKeys.Vulnerable, 150),
            Multiplier(StatusKeys.Weak, 75),
            Multiplier(StatusKeys.Damaged, 75),
```

- [x] **Step 3: 테스트 2곳을 전환한다**

`StatusTests.cs`와 `SlowHasteStatusTests.cs`의 `StatusRuleCatalog.Default()`를
`StatusContentDefaults.Catalog().Rules`로 바꾼다. 단언은 바꾸지 않는다.

`StatusTests.cs`에는 `state.StatusRules.Set(...)`으로 배율을 덮어쓰는 테스트가 있다. `StatusRules`가
`StatusContent.Rules`로 위임하는 프로퍼티이므로 여전히 동작하지만, 전투마다 새 카탈로그가 만들어져
전투 간 누수가 없는지 확인한다.

- [x] **Step 4: 클래스를 지운다**

```bash
git rm Assets/Core/Status/StatusRuleCatalog.cs Assets/Core/Status/StatusRuleCatalog.cs.meta
/usr/bin/grep -rn "StatusRuleCatalog" Assets --include='*.cs'
```
두 번째 명령의 출력이 비어야 한다.

- [x] **Step 5: 검증하고 커밋한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 446` (테스트 수 변화 없음 — 순수 이동)

```bash
git status --short
git add -A Assets
git commit -m "refactor: 프로덕션이 쓰지 않는 StatusRuleCatalog을 제거한다

배율은 이미 StatusContentDefaults가 소유한다. Default()는 테스트 2곳만
부르고 있었다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: `StatusSpecCatalog`을 제거한다

**Files:**
- Delete: `Assets/Core/Authoring/Statuses/StatusSpecCatalog.cs` (+ `.meta`)
- Modify: `Assets/Core/Authoring/Statuses/StatusSpec.cs` (+ 3개 서브클래스)
- Modify: `Assets/Core/Authoring/Statuses/StatusContentDefaults.cs`
- Modify: `Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs`
- Modify: `Assets/Core/Authoring/Specs/ApplyStatusSpec.cs`
- Modify: `Assets/Core/Tests/EditMode/StatusContentTests.cs`

**Interfaces:**
- Produces:
  - `abstract StatusSpec.NewInstance()` → `StatusSpec` — 자기 타입의 빈 인스턴스
  - `StatusContentDefaults.Specs()`가 판별자 표의 유일한 출처
  - `StatusContentDefaults.HasContent(StatusKey)` → `bool` (`StatusSpecCatalog.HasContent` 대체)

`StatusSpecCatalog`과 `StatusContentDefaults`는 **같은 11개 상태를 각각 나열**한다. 그래서 둘을 묶는
테스트가 따로 필요했다. 기본값 목록 하나만 남기면 그 테스트도 함께 사라진다.

- [x] **Step 1: 빈 인스턴스 팩터리를 스펙에 넣는다**

컨버터가 `Populate` 대상 인스턴스를 만들어야 하는데, 지금은 카탈로그의 `Create` 델리게이트가 그
역할을 한다. 그걸 스펙 자신에게 옮긴다. **리플렉션(`Activator.CreateInstance`)을 쓰지 않는다** —
규칙 9는 명시적 등록을 요구하고, 여기서 리플렉션을 쓰면 "타입이 어디서 오는지"가 코드에서 사라진다.

`StatusSpec.cs`에 추가:

```csharp
        /// <summary>자기 타입의 빈 인스턴스. JSON 컨버터가 Populate 대상으로 쓴다.
        /// 리플렉션 대신 각 타입이 스스로 답한다 (규칙 9).</summary>
        public virtual StatusSpec NewInstance() => new StatusSpec();
```

3개 서브클래스에 각각 한 줄:

```csharp
        public override StatusSpec NewInstance() => new MultiplierStatusSpec();
```

상태를 새로 추가할 때는 보통 기존 스펙 타입을 재사용하므로 이 오버라이드를 건드릴 일이 없다 — 새
스펙 *타입*을 만들 때만 한 줄 는다.

- [x] **Step 2: 컨버터가 기본값에서 표를 만들게 한다**

`StatusSpecJsonConverter.BuildFactories()`를 바꾼다.

```csharp
        private static Dictionary<string, Func<StatusSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<StatusSpec>>();
            foreach (var spec in StatusContentDefaults.Specs())
            {
                var key = spec.Key.Id;
                if (table.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "Duplicate status key '" + key + "' in StatusContentDefaults.");
                }

                var prototype = spec;
                table.Add(key, () =>
                {
                    var created = prototype.NewInstance();
                    created.Key = prototype.Key;
                    return created;
                });
            }

            return table;
        }
```

`prototype` 지역 변수는 클로저가 루프 변수를 잡지 않게 한다 (C# 9의 `foreach` 변수는 반복마다
새로 만들어지지만, 의도를 드러내기 위해 명시한다).

- [x] **Step 3: `HasContent`를 옮긴다**

`StatusContentDefaults`에 추가하고, `ApplyStatusSpec.Validate`의 호출을 바꾼다.

```csharp
        public static bool HasContent(StatusKey key)
        {
            foreach (var spec in Specs())
            {
                if (spec.Key.ToKey() == key) return true;
            }

            return false;
        }
```

- [x] **Step 4: 카탈로그를 지우고 테스트를 정리한다**

```bash
git rm Assets/Core/Authoring/Statuses/StatusSpecCatalog.cs Assets/Core/Authoring/Statuses/StatusSpecCatalog.cs.meta
/usr/bin/grep -rn "StatusSpecCatalog" Assets --include='*.cs'
```

`StatusContentTests.cs`에서 `StatusSpecCatalog`을 쓰는 테스트를 `StatusContentDefaults.Specs()`
기준으로 바꾼다. `DefaultsCoverEveryAuthorableStatus`는 **삭제한다** — 카탈로그와 기본값이 하나가
되었으므로 검사할 어긋남이 없다. 삭제 이유를 커밋 메시지에 적는다.

`RoundTripsEveryRegisteredStatusSpecKind`는 `StatusContentDefaults.Specs()`를 순회하도록 바꿔 남긴다.

- [x] **Step 5: 검증하고 커밋한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 445` (`DefaultsCoverEveryAuthorableStatus` 1개 삭제)

```bash
git add -A Assets
git commit -m "refactor: 상태 판별자 표를 기본값 목록에서 만든다

StatusSpecCatalog과 StatusContentDefaults가 같은 11개를 각각 나열하고
있었다. 둘을 묶던 테스트도 함께 사라진다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: 표시 이름을 상태 콘텐츠로 옮긴다

**Files:**
- Modify: `Assets/Core/Authoring/Statuses/StatusSpec.cs` (`DisplayName`)
- Modify: `Assets/Core/Authoring/Statuses/StatusContentDefaults.cs`
- Modify: `Assets/Core/Authoring/Statuses/StatusContentCatalog.cs`
- Modify: `Assets/Core/Authoring/Statuses/StatusContentLoader.cs` (이름 검증)
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionCatalogValidator.cs`
- Modify: `Assets/StreamingAssets/Content/Statuses/*.json` (익스포터 재실행)

**Interfaces:**
- Produces:
  - `StatusSpec.DisplayName` (string, 필수)
  - `StatusContentCatalog.DisplayNameOf(StatusKey)` → `string`
  - `KoreanDescriptionCatalog.CreateDefault(StatusContentCatalog)` 오버로드

상태 이름은 상태에 관한 저작 데이터다. 코드에 11줄로 박아두는 대신 상태가 자기 이름을 소유하면
등록 지점이 하나 줄고, 이름 변경이 재컴파일 없이 끝난다.

규칙 10과 충돌하지 않는다 — 그 규칙이 막는 것은 **카드 본문을 통째로 저작하는 것**이고, 상태 이름은
컴포저가 문장에 끼워 넣는 명사다. 오히려 하드코딩이 하나 줄어든다.

- [x] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/StatusContentTests.cs`에 더한다.

```csharp
        [Test]
        public void StatusContentCarriesItsDisplayName()
        {
            var catalog = StatusContentDefaults.Catalog();

            Assert.AreEqual("약화", catalog.DisplayNameOf(StatusKeys.Weak));
            Assert.AreEqual("독", catalog.DisplayNameOf(StatusKeys.Poison));
        }

        [Test]
        public void RejectsAStatusWithNoDisplayName()
        {
            var result = StatusContentLoader.Load(
                new[] { new CardContentSource(
                    "block.json", "{ \"key\": \"block\", \"lifetime\": \"ThisTurn\" }") },
                AuthoringContext.Default());

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("displayName")));
        }
```

- [x] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter StatusContentTests`
Expected: 컴파일 실패 — `DisplayNameOf` 없음

- [x] **Step 3: 스펙과 카탈로그에 이름을 넣는다**

`StatusSpec`에 `public string DisplayName;`을 더하고 `Validate`에 빈 값 검사를 넣는다.
`StatusContentCatalog`에 `DisplayNameOf(StatusKey)`를 더한다(없으면
`StatusContentCatalog.Spec`이 이미 하는 것처럼 `KeyNotFoundException`).

`StatusContentDefaults`의 11개 항목에 이름을 채운다 — 값은
`KoreanDescriptionCatalog`에서 그대로 옮긴다.

| 키 | 이름 | 키 | 이름 |
|---|---|---|---|
| `block` | 방어 | `poison` | 독 |
| `slow` | 둔화 | `poison_dormant` | 독 잠복 |
| `haste` | 가속 | `poison_stasis` | 독 안정 |
| `vulnerable` | 취약 | `contagion` | 전염 |
| `weak` | 약화 | `damaged` | 손상 |
| `reward_nullified` | 조건 보상 무효 | | |

- [x] **Step 4: 설명 카탈로그가 콘텐츠에서 이름을 읽게 한다**

`KoreanDescriptionCatalog`의 `statuses.Register(StatusKeys.X, "...")` 11줄을 지우고,
`StatusContentCatalog`에서 채운다.

**함께 고칠 것 하나** — 선행 계획의 최종 리뷰가 남긴 항목이다. `KoreanDescriptionCatalog.Default`는
프로세스 전역 싱글턴이고 그 `StatusContent`가 `StatusContentDefaults.Catalog()`로 **고정**돼 있다.
후속 계획이 로더를 부팅에 배선하면 카드 텍스트는 코드 기본값을, 규칙은 파일을 보게 되어 갈린다.
`CreateDefault(StatusContentCatalog)` 오버로드를 주고 호출자가 카탈로그를 넘기게 한다. 인자 없는
`CreateDefault()`는 기본값 카탈로그를 쓰는 편의 오버로드로 남긴다.

- [x] **Step 5: 검증을 로더로 옮긴다**

`DescriptionCatalogValidator`가 "등록된 모든 상태에 이름이 있는가"를 검사한다면, 이름이 콘텐츠로
갔으므로 그 검사는 `StatusContentLoader`의 완전성 검사 옆으로 옮긴다. 검사 자체를 잃지 않는다.

- [x] **Step 6: 익스포터를 다시 돌린다**

```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod FateWeaver.Unity.Editor.CardContentExporter.ExportAll \
  -logFile /private/tmp/fw-name-export.log
```

Run: `cat Assets/StreamingAssets/Content/Statuses/weak.json`
Expected: `"displayName": "약화"`가 있다

- [x] **Step 7: 전체 검증**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 447`

```bash
/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode -testResults /private/tmp/fw-name.xml \
  -logFile /private/tmp/fw-name.log
```
Expected: 실패 0건

- [x] **Step 8: 커밋하고 문서를 갱신한다**

```bash
git status --short
git add -A Assets docs
git commit -m "refactor: 상태의 표시 이름을 콘텐츠로 옮긴다

상태 이름은 상태에 관한 저작 데이터다. 코드 11줄이 사라지고 이름 변경이
재컴파일 없이 끝난다. 설명 카탈로그가 전투와 다른 카탈로그 인스턴스를
읽던 문제도 함께 고친다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

이 계획의 상태 줄과 `docs/superpowers/README.md`를 함께 갱신한다(규칙 20).

---

## 완료 조건

- 헤드리스 447 tests 통과, 실패 0 / Unity EditMode 실패 0
- `StatusRuleCatalog`·`StatusSpecCatalog`이 저장소에 없다
- `KoreanDescriptionCatalog`에 상태 이름이 하드코딩돼 있지 않다
- 상태 하나를 추가할 때 손대는 곳이 **4곳**이다: 동작 클래스, `StatusKeys` 1줄,
  `CombatRegistries` 1줄, `StatusContentDefaults` 항목 1개
- 상태 JSON 11개가 `displayName`을 갖는다
- 워킹 트리가 깨끗하다 (규칙 18)

## 구현 결과 (2026-08-03)

**최종 수치:** 헤드리스 **447/447** 0 skipped, Unity EditMode **521/521** 실패 0
(기준선 446 / 520 — 신규 2, 삭제 1). 카드 JSON 26(변경 없음), 상태 JSON 11(전부 `displayName` 획득).

커밋 3개: `StatusRuleCatalog` 제거 → 판별자 표 통합 → 표시 이름 이동.

계획에서 벗어난 점 셋:

1. **삭제한 테스트의 실제 이름은 `DefaultsCoverEveryRegisteredStatus`** 다 (계획은
   `DefaultsCoverEveryAuthorableStatus`로 적었다). 같은 테스트이며, 두 목록을 묶던 역할도 같다.
2. **Task 3 Step 5는 옮길 것이 없었다.** `DescriptionCatalogValidator`에는 "등록된 모든 상태에
   이름이 있는가"라는 완전성 검사가 애초에 없었고, 카드가 실제로 거는 상태만 `Resolve`한다. 검사를
   잃지 않았다 — `StatusContentLoader`가 이미 "등록된 상태는 전부 저작돼야 한다"를 강제하고,
   여기에 `displayName` 필수 검사가 더해져 이름 누락이 로드 실패로 잡힌다. 코드 기본값 경로는
   `StatusDescriptionRegistry.Register`가 빈 이름에 던지므로 `KoreanDescriptionCatalog` 정적
   초기화에서 즉시 터진다.
3. **인라인 JSON 픽스처 다섯 개에 `displayName`을 채웠다.** `displayName` 필수 검사가 생기면서
   기존 로더 테스트 3개가 깨졌다 — 그 테스트들의 관심사(중복·미등록 행동·완전성)를 유지하려면
   픽스처가 검사를 통과해야 한다. 검사 자체를 겨냥한 `RejectsAStatusWithNoDisplayName`만 이름을
   비운 채 남겼다.

`SlowHasteStatusTests`는 `StatusContentDefaults.Catalog().Rules` 대신 그 파일이 이미 들고 있던
`Content` 카탈로그의 `.Rules`를 쓴다 — 같은 기본값이고, 테스트가 `Content`로 넘기는 카탈로그와
규칙이 어긋날 수 없게 된다.

## 후속

이 계획 다음은 **계획 3(콘텐츠 원본 전환)** 이다 — 소비자를 JSON으로 돌리고
`CardCodeGenerator`·`GeneratedCards.cs`·SO의 규칙 필드를 제거한다. 그 뒤에 **계획 3.5**가 개입
액션을 효과처럼 다형화하고 `CardSpec`을 실행/개입으로 쪼갠다(핸들러가 실제로 읽는 파라미터가
액션마다 달라 지금은 `lock` 카드가 안 쓰는 칸 넷을 들고 있다). 마지막이 **계획 4(카드 변형)** 다.
