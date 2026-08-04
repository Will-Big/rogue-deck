# Fate Weaver — 카드 변형과 런타임 콘텐츠 로딩 설계

- 작성일: 2026-07-30
- 개정일: 2026-07-31
- 문서 유형: `design`
- 주 도메인: `card-authoring`
- 상태: `current`
- 구현 상태: 미착수 (요구·경계·구조 확정)
- 관련 권위 문서:
  - `docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md`
  - `docs/superpowers/specs/2026-07-19-open-card-authoring-design.md`
  - `docs/superpowers/archive/specs/2026-07-29-starter-pool-so-authoring-design.md` (보관: SO 저작 파이프라인은 제거됨, 22장 설계 의도만 참고)
  - `docs/superpowers/specs/2026-07-16-description-registry-design.md`

## 1. 목적

두 가지 요구를 만족하는 카드 데이터 구조와 콘텐츠 로딩 경로를 확정한다.

1. **플레이어 카드는 카드의 고유 정보와 분리된다.** 플레이어가 소유한 카드는 전투 중·런 중에
   강화·변경될 수 있고, 일시적 변경과 영구적 변경이 모두 존재한다. 변경은 수치에 한정되지 않으며
   설명 텍스트가 바뀌거나 능력이 추가·치환되는 것까지 포함한다.
2. **추후 UGC(모딩) 콘텐츠가 가능해야 한다.** 외부 제작자가 게임을 다시 컴파일하지 않고 카드를
   추가·수정할 수 있어야 한다.

이 문서는 요구·경계와 그것을 만족하는 구조를 확정한다. 단계별 구현 계획은 별도 계획 문서에서
다룬다.

## 2. 현재 상태와 문제

### 2.1 플레이어 카드에 변형을 담을 자리가 없다

3계층 카드 모델(`CardDefinition` / `OwnedCard` / `ExecutionCardInstance`)은 이름만 존재하고
중간 계층이 비어 있다. `Assets/Core/Cards/OwnedCard.cs`는 `Def`와 `OwnerId` 두 필드뿐이다.

```csharp
public sealed class OwnedCard
{
    public CardDefinition Def { get; }
    public string OwnerId { get; }
}
```

영구 강화가 얹힐 자리가 없으므로 현재 구조로는 다음을 표현할 수 없다.

- 런 중 카드를 영구 강화한다 (피해 +2)
- 카드에 능력을 하나 추가한다 (설명 텍스트가 늘어난다)
- 카드의 능력을 다른 능력으로 치환한다
- 이번 전투에만 유효한 강화를 얹고 전투 종료와 함께 소멸시킨다

런 지속 소유도 `RunMember.Cards`가 `List<CardDefinition>`이라 변형을 담지 못한다.

### 2.2 전투 중 효과가 카드에 도달하지 못한다

`EffectContext`가 주는 것은 `ExecutionCardInstance`와 `CombatState`뿐이고, `CombatState`는
`Party`·`Enemies`·`Zone`·`FateEnergy`만 들고 `Deck`을 갖고 있지 않다(덱은 `DeckCombatSession`이
따로 보관). 따라서 카드 효과가 카드를 강화할 배관 자체가 없다.

### 2.3 콘텐츠가 컴파일 시점에 고정된다

현재 저작 경로는 편집 시점 코드 생성이다.

```
CardAsset (SO, 인스펙터 저작)
  → Fate Weaver/Generate Cards from SO (에디터 메뉴, 수동 실행)
  → Assets/Core/Simulation/Generated/GeneratedCards.cs (순수 C# 리터럴)
  → 코어가 컴파일
```

런타임에는 돌지 않으므로 실행 성능 문제는 없다. 그러나 세 가지 제약이 남는다.

- **모딩 불가.** 새 카드를 넣으려면 C# 재컴파일이 필요하다.
- **유지보수 한계.** 카드 하나가 한 줄짜리 거대 리터럴이라 카드 수가 늘면 diff와 검토가 무너진다.
- **런타임 변경 불가.** 카드 정의가 컴파일 상수이므로 §2.1의 변형을 담을 경로가 애초에 없다.

## 3. 범위

### 3.1 포함

- `OwnedCard`의 변형 저장 구조와 변형의 표현 방식
- 전투 한정 변형의 위치와 소멸 시점
- 전투 중 카드 효과·개입 효과가 변형을 일으키는 경로
- 변형을 데이터로 직렬화해 런 세이브에 담을 수 있는 형태
- 편집 시점 코드 생성을 런타임 데이터 로딩으로 대체하는 경로
- UGC 콘텐츠의 검증 경계
- 새 저작 포맷에 대응하는 카드 저작 도구

### 3.2 제외

- 강화 획득 규칙(어떤 노드에서 무엇을 강화하는가) — 런 사이클 재설계에 속한다
- 구체적인 강화 카탈로그 저작
- 모드 배포·구독·버전 관리
- 세이브 파일의 저장·로드 구현 자체 (이 문서는 담길 수 있는 형태까지 확정한다)
- 손패의 카드를 대상으로 삼는 강화
- 덱 전체를 한 번에 강화하는 효과 (캐릭터 단위 버프는 기존 상태 이상으로 표현한다)
- 상태 이상의 강도·수명 파라미터화 — `plans/2026-07-30-status-rule-and-debuffs.md`가 다룬다

## 4. 결정

### 4.1 변형은 수치 패치가 아니라 카드에 대한 연산이다

요구 1.2("무엇이든 바뀔 수 있다")를 수치 패치로는 만족할 수 없다. 설명이 EffectData에서 자동
생성되므로(규칙 10, 설명 레지스트리 설계), **효과 목록 자체를 바꾸면 텍스트는 따라온다.** 따라서
변형의 단위는 카드에 대한 연산이다.

| 연산 | 의미 |
|---|---|
| 효과 추가 | 효과 목록 끝에 효과 하나를 더한다 |
| 효과 제거 | 지정한 효과를 목록에서 뺀다 |
| 효과 치환 | 지정한 효과를 다른 효과로 바꾼다 |
| 효과 수치 변경 | 지정한 효과의 값만 바꾼다 |
| 카드 속성 변경 | `EnergyCost`·`BaseExecutionOrder`·`Name`을 바꾼다 |

마지막 연산이 필요한 이유는 강화가 효과 목록 밖도 건드리기 때문이다("비용 1 감소", "실행 순서를
영구히 앞당김"). 효과 목록만 다루는 4종으로는 표현되지 않는다.

카드 본문 텍스트를 직접 저장하는 필드는 두지 않는다. 텍스트를 데이터로 들고 있으면 효과와 설명이
어긋날 수 있고, 이는 설명 레지스트리 설계가 금지하는 이중 원본이다.

### 4.2 변형의 대상은 효과 종류(`EffectKey`)로 지정한다

제거·치환·수치 변경이 가리키는 "지정한 효과"는 `EffectKey`로 식별한다.

인덱스나 효과별 고유 id로 지정하면 대상이 카드마다 달라져, `"피해 +2"` 강화 하나를 **여러 카드에
공통으로 적용할 수 없다.** 범용 강화 카탈로그(§3.2에서 저작은 제외했으나 구조는 이 문서가 정한다)를
가능하게 하는 것은 카드에 독립적인 지정 방식뿐이다. 부수적으로 저작 부담이 없고, 카드 정의가
패치되어도 의미가 유지된다.

한 카드가 같은 `EffectKey`를 두 번 갖는 경우(현재 저작된 카드 중에는 없다) 변형이 "첫 번째만"인지
"전부"인지를 명시한다.

### 4.3 카드의 세 형태와 두 수명

```
CardDefinition (원본)     콘텐츠 데이터에서 생성. 불변. 같은 카드를 가진 모두가 공유
      +
변형 목록 2개             Permanent(런) / Combat(전투)
      ↓
OwnedCard.Effective       게임이 실제로 보는 카드. 이름·비용·효과·설명 전부 다를 수 있다
```

```csharp
public sealed class OwnedCard
{
    CardDefinition Source;        // 콘텐츠에서 온 원본. 되돌리기와 세이브의 기준점
    string OwnerId;
    List<CardMutation> Permanent; // 런 세이브에 기록
    List<CardMutation> Combat;    // 전투 종료 시 비움
    CardDefinition Effective;     // Source → Permanent → Combat, lazy
}
```

전투 해결·UI·`DescriptionComposer`는 **전부 `Effective`만 본다.** 강화된 카드는 설명까지 포함해
진짜 다른 카드다. 불변인 것은 원본 템플릿 하나뿐이며, 필드 이름을 `Def`가 아니라 `Source`로 두어
"이것이 곧 이 카드"라는 오해를 막는다.

**원본을 남기는 이유는 되돌리기다.** 전투 한정 강화를 전투 끝에 빼야 하는데, 변형된 결과만 들고
있으면 그 증가분이 전투 한정인지 영구인지 구분할 수 없다. 원본 + 변형 목록이면 `Combat` 목록을
버리는 것으로 정확히 복원된다. 부수적으로 세이브가 작아지고, UI가 "강화됨"을 표시할 수 있으며,
같은 정의를 공유하는 두 소유 카드가 서로 다른 강화를 가질 수 있다.

런 지속 소유는 `RunMember.Cards`를 `List<CardDefinition>`에서 `List<OwnedCard>`로 바꿔 담는다.
전투 덱은 이 객체를 복제 없이 그대로 쓴다.

`Effective`는 lazy 계산하고 변형 목록이 바뀔 때 무효화한다. 변형이 없으면 `Source`를 그대로
반환한다(할당 없음). 무효화 지점이 변형의 추가·제거 한 곳뿐이라 어긋날 여지가 없다.

**두 수명은 `OwnedCard` 안에 함께 둔다.**

| 목록 | 수명 | 소멸 |
|---|---|---|
| `Permanent` | 런 | 세이브에 기록된다 |
| `Combat` | 전투 | 전투 종료 시 `ClearCombatMutations()`, 전투 시작 시 방어적으로 한 번 더 |

전투용 복제본을 만들어 수명을 객체 수명으로 보장하는 방식은 **채택하지 않는다.** 전투 중에 발생한
영구 강화("영구적으로 피해 +2")가 복제본에 얹히면 전투 종료와 함께 사라지고, 복제본에서 원본으로
되돌릴 경로가 없기 때문이다. 두 목록을 같은 객체에 두면 영구 강화가 그 전투에 즉시 적용되면서 런에도
남는다.

`ExecutionCardInstance`는 변형을 담지 않는다. 카드를 존에 낼 때마다 새로 생성되므로 실제 수명이
전투가 아니라 **카드 1회 해결**이며, 그 범위의 일회성 보정은 이미
`GrantNextDamageCardBonus`/`ExecutionCardInstance.ConsumePendingDamageBonus`가 담당한다.

### 4.4 변형은 직렬화 가능한 데이터이며 저작 스펙을 재사용한다

영구 변형은 런 세이브에 들어가야 한다. 따라서 변형은 델리게이트나 코드가 아니라 데이터여야 하고,
기존 저작 스펙 타입(`EffectSpec` 계열)을 재사용한다. 명시적 등록 목록 규칙(리플렉션 자동 등록
금지)이 여기에도 적용되며, `CardMutation`은 `EffectSpecCatalog`와 같은 형태의 명시적 카탈로그에
등록한다.

그런데 `OwnedCard`는 `FateWeaver.Core`에 있고 `EffectSpec`은 `FateWeaver.Simulation`에 있어
참조 방향이 반대다. 따라서 **`Assets/Core/Simulation/Authoring/`(19개 파일)을
`Assets/Core/Authoring/`으로 옮긴다.** 이 폴더에는 UnityEngine 참조가 하나도 없으므로(두 asmdef
모두 `noEngineReferences: true`) 이동은 폴더와 네임스페이스 치환뿐이다.

코어가 저작 관심사를 떠안는다는 반론은 성립하지 않는다. `EffectSpec.ToLiteral()`은 코드 생성
전용인데 §4.5에 따라 코드 생성 자체가 사라지므로 함께 제거되고, 남는 `Validate()`는 §4.6이 코어에
요구하는 기능이다.

세이브에 담기는 형태:

```json
{ "defId": "slash", "ownerId": "member_a",
  "permanent": [ { "kind": "changeEffectValue", "effect": "damage", "delta": 2 } ] }
```

`Combat` 목록은 저장하지 않는다(전투 중 세이브는 §3.2에서 제외). 무작위 강화가 도입되면
`RunState.Rng`를 경유한다(규칙 7).

### 4.5 저작 데이터는 JSON으로 두고 런타임에 읽어 코어 객체로 주입한다

> **구현 상태 (2026-08-05):** 계획 3a·3b·3c·3d로 **이 절은 구현됐다.** 아래 본문은 그 시점의
> 설계 의도를 그대로 둔 것이다. 완료된 부분은 인용 블록으로 표시한다.

```
StreamingAssets/Content/Statuses/*.json  상태당 1파일 ← 가장 먼저 읽는다
StreamingAssets/Content/Cards/*.json     카드당 1파일 (기본 콘텐츠 + 모드)
StreamingAssets/Content/{Decks,Pools,Characters}/*.json
   │ 부팅(또는 모드 로드) 1회 · Newtonsoft 파싱
   ▼ AuthoringValidator — 실패 시 로드 거부 + 이유 보고
CardSpec → CardDefinition                Dictionary<string, CardDefinition>

Unity: 초기값 저작이 아니라 표현만 담당. 카드 아트는 id → Sprite 매핑 SO로 남는다.
```

카드 규칙의 유일한 원본은 JSON이다. 코드 생성은 사라진다 —
`Assets/Unity/Editor/CardCodeGenerator.cs`, `Assets/Core/Simulation/Generated/GeneratedCards.cs`,
각 `EffectSpec`의 `ToLiteral()`이 모두 제거 대상이다. 기존 카드 SO는 에디터 익스포터로 **1회**
JSON으로 변환하며, 재저작은 없다.

> **완료:** 위 셋은 계획 3b가 제거했고 `CardAsset`·`CardPoolAsset`·`DeckAsset`도 함께 사라졌다.
> 상태의 코드 기본값(`StatusContentDefaults`)은 계획 3c가 제거했으며, 그 결과
> `StatusSpecJsonConverter`가 판별자 표를 행동 레지스트리(`CombatRegistries.Statuses()`)에서
> 만든다 — 각 `IStatusBehavior`가 `NewSpec()`으로 자기 스펙 타입을 답한다.
> 부팅 진입점은 `ContentBootstrap.Load(콘텐츠루트)` 하나이며 **상태 → 카드 → 덱·풀 → 캐릭터**
> 순서로 카탈로그 다섯을 만들어 `GameContent`로 돌려준다(카드 검증이 상태 저작을 전제한다).
> `CombatState`는 상태 카탈로그를 생성자에서 요구하고, `KoreanDescriptionCatalog`의 전역
> `Default`는 없어졌다.
>
> **완료 (계획 3d, 2026-08-05):** 골든 테스트 축으로 살아 있던 C# 목록 —
> `StarterPoolSpecs`·`StarterDeckSpecs`·`PartyPrototypeDeckSpecs`·`StarterDeck`·
> `PartyPrototypeDeck`·`PartyPrototypeCharacterSpecs`·`ContentExportWriter`·`CardContentExporter`
> — 가 전부 사라졌다. 테스트는 이제 합성 픽스처(`CardFixtures`·`UnityCardFixtures`)와 JSON
> 카탈로그(`TestContent`·`UnityTestContent`) 둘로만 카드를 얻는다. `Assets/StreamingAssets/Content/`가
> 플레이어 카드·상태·덱·풀·캐릭터의 유일한 원본이다.
>
> **남은 것은 셋이다:**
> - **계획 3.5 (개입 액션 다형화·카드 스펙 분리)** — `CardSpec`을 실행/개입으로 쪼갠다. 지금은
>   `lock` 카드가 안 쓰는 칸 넷을 들고 있다.
> - **계획 4 (`CardMutation`)** — 카드 변형의 기반. `OwnedCard`가 영구·전투 변형 2목록을 갖는다.
> - **적 카드의 JSON 전환** — 남은 마지막 C# 카드 정의(`GoblinDeck`·`WardenDeck`)이며, 아직 계획이
>   없다. 적 정책·행동 패턴 설계가 선행돼야 한다.

카드당 1파일로 쪼개는 이유는 §2.3의 diff 붕괴를 직접 해결하고, 모드가 카드 한 장만 교체할 수 있게
하며, 모더가 기본 콘텐츠를 그대로 예제로 삼을 수 있게 하기 위해서다. 경로는 콘텐츠 루트 상수 하나만
두고 나머지는 폴더 스캔이므로 개별 에셋을 문자열로 찾지 않는다(규칙 2·3).

- 읽기는 부팅(또는 모드 로드) 시점 1회다. 런타임에 저작 데이터를 다시 쓰지 않는다.
- 모든 원본 카드를 사전에 상주시킨다. 보상 카드 선택·덱 뷰어가 전부 필요로 하고, 규모는 카드
  수십~수백 장 × 효과 1~3개라 문제가 되지 않는다. 코드 생성 경로도 결국 같은 객체를 메모리에 만들고
  있었고, 차이는 리터럴이냐 파싱이냐뿐이다.
- `OwnedCard`는 사전의 `CardDefinition`을 **참조**한다. 같은 카드를 10장 가져도 정의 객체는 1개다.
- 파싱 실패와 규칙 위반은 침묵하지 않는다. 기존 `AuthoringValidator`를 재사용하고, 실패한 모드
  콘텐츠는 로드를 거부하며 이유를 보고한다.

**직렬화는 Newtonsoft.Json(`com.unity.nuget.newtonsoft-json`)을 쓴다.** Unity 내장
`JsonUtility`는 다형성을 지원하지 않아 `EffectSpec[]`을 직렬화하면 서브타입이 소실되고, Unity의
.NET 프로파일에는 `System.Text.Json`이 없다. Newtonsoft는 순수 관리 어셈블리라 코어의
`noEngineReferences`를 깨지 않고, `Tests/Headless/FateWeaver.Tests.Headless.csproj`가 이미
`PackageReference`를 쓰고 있어 헤드리스 경로에서도 같은 라이브러리가 돈다. `JsonConverter`로 타입
판별자(`"kind": "damage"`)를 직접 제어하므로 기존 `EffectSpecCatalog`를 그대로 판별자 테이블로 쓸
수 있고, 파싱 오류에 줄·열 위치가 실려 모드 검증기가 구체적인 이유를 보고할 수 있다. 이는 규칙 14에
따른 사전 승인 사항으로 승인되었다.

### 4.6 코어는 UnityEngine을 참조하지 않는다 (유지)

요구 2를 근거로 코어·Unity 경계를 재검토했고, **경계를 유지하는 쪽이 두 요구 모두에 유리하다는
결론이다.**

- 모더가 Unity 에셋(SO·프리팹)으로 콘텐츠를 만들려면 Unity 에디터와 AssetBundle 빌드가 필요하다.
  순수 데이터 파일이면 텍스트 편집기로 충분하다.
- 코어가 UnityEngine을 참조하지 않으면 모드 검증기를 CLI로 실행할 수 있다. 유저 카드가 규칙을
  위반하는지 게임 실행 없이 판정할 수 있다는 뜻이다.
- 변형과 세이브를 Unity 직렬화에 묶으면 모드 호환과 세이브 마이그레이션이 함께 어려워진다.
- 헤드리스 테스트(규칙 12)와 결정론 검증(규칙 7)이 순수 코어에 의존한다.

즉 §2.3의 걸림돌은 경계 자체가 아니라 **편집 시점 코드 생성**이다. 경계는 그대로 두고 생성을
로딩으로 바꾼다.

### 4.7 전투 중 강화의 경로

`ExecutionCardInstance`가 `CardDefinition` 대신 `OwnedCard`를 참조하도록 바꾼다. 이 한 번의 변경
(생성 지점 7곳)으로 §2.2의 배관 부재가 해소된다.

| 대상 | 경로 | 추가 배관 |
|---|---|---|
| 실행 카드가 자기 자신을 강화 | `ctx.Card.Owned` | 없음 |
| 개입 카드가 존의 실행 카드를 강화 | `ctx.Target.Owned` | 없음 |
| 캐릭터 단위 버프 | 변형이 아니라 기존 `StatusBag` | 없음 |

개입 경로는 `InterventionPlayContext.Target`이 이미 `ExecutionCardInstance`이고
`IInterventionActionHandler.Targeting`(`TargetingRequirement`)이 UI 대상 선택을 이미 담당하므로,
강화 핸들러 1개를 등록하는 것으로 끝난다(규칙 9). 손패 대상 강화는 `TargetingRequirement`에 새
종류와 손패 접근 주입이 필요하므로 §3.2에서 제외했다. 확장 가능한 구조라 나중에 더해도 기존 설계를
깨지 않는다.

### 4.8 모드는 기존 효과 키의 조합만 저작한다

모드가 새 효과 **키**를 추가하는 것은 허용하지 않는다. 새 키는 코드 로딩을 뜻하고, 그러면 규칙 7의
결정론을 검증할 수 없으며 범위가 크게 늘어난다. 모드는 등록된 키의 조합·수치·조건만 저작한다.
`AuthoringValidator`가 미등록 키를 로드 시점에 거부한다.

### 4.9 카드 저작 도구

인스펙터 저작(`EffectSpecDrawer`의 효과 드롭다운)을 잃는 대신 `Tools/card-idea-notebook`이 저작
도구를 겸한다. 현재 이 도구의 `abilities`는 자유 텍스트 줄 목록이라, 구조화된 효과 편집기를 새로
넣어야 한다.

- **Markdown 내보내기는 유지하고 JSON 내보내기를 추가한다.** `시작 카드 풀.md`가 그 산출물이고
  `index.test.mjs`가 이를 검증하고 있어, 대체가 아니라 순증으로 둔다. 아이디어 단계(자유 텍스트)와
  저작 단계(구조화된 효과)가 한 도구 안에 공존한다.
- **효과 스키마를 두 곳에 정의하지 않는다.** C#이 `EffectSpecCatalog`에서 효과 스키마 JSON을
  내보내고 도구가 그것을 읽어 폼을 만든다. 효과를 추가해도 원본은 C# 한 곳이다.

## 5. 열린 항목

- 같은 id의 카드를 모드가 제공할 때의 처리 (덮어쓰기 / 거부 / 우선순위 선언)
- 세이브 스키마 버전과 마이그레이션 규칙 — 세이브 구현 시점에 정한다
- 한 카드가 같은 `EffectKey`를 두 번 갖는 카드가 실제로 저작될 때의 "첫 번째만 / 전부" 기본값

## 6. 검증 방향

- 같은 `CardDefinition`을 공유하는 두 `OwnedCard`가 서로 다른 영구 변형을 갖고, 각자 다르게
  해결되는 헤드리스 테스트
- 영구 변형이 있는 카드의 설명이 변형된 효과 목록에서 자동 생성되는 테스트
- 전투 한정 변형이 전투 종료 이후에 남지 않는 테스트, 그리고 종료 경로를 건너뛴 뒤 다음 전투 시작
  시점에도 남지 않는 테스트
- 전투 중에 얹은 영구 변형이 그 전투에 즉시 적용되고 전투 종료 이후에도 남는 테스트
- 카드 속성 변경(비용·실행 순서) 변형이 배치와 해결에 반영되는 테스트
- 변형 목록을 직렬화·역직렬화한 뒤 해결 결과가 동일한 테스트
- ~~기존 카드 SO에서 변환한 JSON이 기존 `GeneratedCards` 스냅샷과 같은 `CardDefinition`을 만드는
  동등성 테스트 (변환 1회 시점의 안전망)~~ **완료** — `CardContentEquivalenceJsonTests`가 이 축을
  맡았고, `GeneratedCards`가 사라진 지금은 남은 C# 스펙 목록과 대조한다. 계획 3d가 그 목록을
  지울 때 이 테스트는 JSON 골든 대조로 바뀐다.
- 규칙을 위반하는 모드 콘텐츠가 로드를 거부당하고 줄 위치를 포함한 이유를 보고하는 테스트
