# Fate Weaver — 시작 카드 풀 ScriptableObject 저작 설계

- 작성일: 2026-07-29
- 보관일: 2026-08-04
- 문서 유형: `design`
- 주 도메인: `card-authoring`
- 상태: `archived` — **현재 규칙이 아니다.** 이 문서가 설계한 `CardAsset` → `CardPoolAsset` →
  코드 생성 파이프라인은 계획 3b(2026-08-03)가 통째로 지웠고, 상태의 코드 기본값은 계획 3c가
  지웠다. 카드·상태·덱·풀·캐릭터의 현행 원본은 `Assets/StreamingAssets/Content/<종류>/*.json`이며
  `ContentBootstrap.Load`가 읽는다. 현행 설계는
  [카드 변형과 런타임 콘텐츠 로딩](../../specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.5를 본다.
  이 문서는 22장 풀의 **설계 의도와 등급·태그 근거**를 남긴 역사 기록으로만 참고한다.
- 카드 원본: `Tools/card-idea-notebook/시작 카드 풀.md` (22장)
- 관련 권위 문서:
  - `docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md`
  - `docs/superpowers/specs/2026-07-19-open-card-authoring-design.md`
  - `docs/superpowers/specs/2026-07-20-character-card-pools-design.md`
  - `docs/superpowers/specs/2026-07-27-position-targeting-card-text-design.md`

## 1. 목적

문서에 설계된 시작 카드 후보 22장을 Unity `CardAsset`으로 저작할 수 있게 하고, 실제 시작 덱과
분리된 `CardPoolAsset`으로 묶는다. 기존 `StarterDeck.asset`은 유지하며, 어느 카드를 실제 시작 덱에
넣을지는 후속 결정으로 남긴다.

카드 규칙의 진실의 원천은 `CardAsset`이다. 생성 C#은 Unity 없이 실행하는 헤드리스 검증과 Compare
하니스를 위한 제거 가능한 스냅샷일 뿐, 게임 런타임의 입력이나 별도 콘텐츠 원본이 아니다.

## 2. 현재 상태와 문제

- 순수 C# `StarterPoolSpecs.Build()`에는 문서의 22장이 모두 표현되어 있다.
- 실제 22개 `CardAsset`과 이를 묶는 풀 에셋은 없다.
- `CardSpec`에는 `InterventionTargetSide`와 `InterventionRequireAdjacent`가 있지만
  `CardAsset.ToSpec()`과 `CardCodeGenerator`가 두 값을 보존하지 않는다.
- 따라서 `재촉`, `유예`, `숨 고르기`의 대상 진영 제한과 `엇갈림`의 인접 제한이 SO 왕복에서
  손실된다.
- 카드 아이디어 문서의 등급과 태그를 게임 저작 데이터에 보존할 장소가 없다.
- 현재 `GeneratedCards.cs`는 헤드리스 다리 역할을 하지만, 장기적으로 Unity EditMode/Batchmode
  검증만 사용하게 되면 제거할 수 있어야 한다.

## 3. 범위

### 3.1 포함

- 개입 대상 진영과 인접 제한의 `CardAsset → CardSpec → CardDefinition` 왕복 보존
- Unity 전용 카드 등급과 자유 태그 메타데이터
- 22장 후보를 참조하는 전용 `CardPoolAsset`
- 기존 카드 값을 덮어쓰지 않는 22장 최초 생성용 에디터 시더
- 풀을 헤드리스용 생성 C# 스냅샷으로 내보내는 경로
- 순수 헤드리스 및 Unity EditMode 자동 검증

### 3.2 제외

- 실제 시작 덱에 포함할 카드와 장수 결정
- 기존 `StarterDeck.asset` 변경
- 보상 카드 선택, 캐릭터 소유권, 런 획득 로직
- 카드 아트 제작 또는 자동 할당
- 새로운 전투 효과·상태·조건·개입 규칙
- 이번 작업에서 헤드리스 하니스나 생성 C# 제거
- `FateWeaver.Core`의 Unity 종속화

## 4. 핵심 결정

### 4.1 데이터 흐름

```text
[Unity 저작 원본]
CardAsset 22장 ──참조──> CardPoolAsset
      │ ToSpec()                │ ToSpecs()
      └──────────────┬──────────┘
                     ▼
                  CardSpec
                     │
          CardSpecMapper.ToDefinition()
                     ▼
                CardDefinition

[제거 가능한 헤드리스 어댑터]
CardPoolAsset ──Editor export──> GeneratedCards.StarterPool()
                                      │
                                      └─ dotnet test / Compare 전용
```

Unity 런타임은 `GeneratedCards`를 참조하지 않는다. 생성 파일을 지워도 Unity 런타임의 카드 로드 경로는
변하지 않아야 한다.

### 4.2 `CardAsset`의 규칙 필드

`CardAsset`에 다음 직렬화 필드를 추가한다.

```csharp
[SerializeField] private InterventionTargetSideRef _interventionTargetSide;
[SerializeField] private bool _interventionRequireAdjacent;
```

`ToSpec()`은 두 값을 각각 `CardSpec.InterventionTargetSide`와
`CardSpec.InterventionRequireAdjacent`로 옮긴다. 에디터 시더와 생성기 역시 같은 값을 보존해야 한다.

기존 공개 필드 전체를 한 번에 비공개 필드로 마이그레이션하는 작업은 범위 밖이다. 새 필드만
`[SerializeField] private`으로 추가하고 읽기 전용 프로퍼티를 제공해 기존 에셋 YAML의 불필요한
마이그레이션을 피한다.

### 4.3 Unity 전용 메타데이터

등급은 닫힌 enum으로 저작한다.

```csharp
public enum CardGrade
{
    None,
    Common,
    Advanced,
    Rare,
    Other
}
```

태그는 카드 아이디어 노트의 자유 태그를 그대로 옮길 수 있도록 문자열 배열로 저작한다.

```csharp
[SerializeField] private CardGrade _grade = CardGrade.None;
[SerializeField] private string[] _tags = Array.Empty<string>();
```

- 22장 모두 등급은 `Common`이다.
- 태그는 `Tools/card-idea-notebook/시작 카드 풀.md`의 한국어 문자열과 순서를 보존한다.
- 빈 태그와 동일 카드 내 중복 태그는 검증 오류다.
- 등급과 태그는 `CardSpec`, `CardDefinition`, 생성 C#에 포함하지 않는다.
- 전투 규칙은 태그를 조건이나 효과 판정에 사용하지 않는다.

### 4.4 `CardPoolAsset`

`CardPoolAsset`은 후보 카드 집합이며 실제 덱이 아니다.

```csharp
[CreateAssetMenu(menuName = "Fate Weaver/Card Pool", fileName = "CardPool")]
public sealed class CardPoolAsset : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private CardAsset[] _cards = Array.Empty<CardAsset>();

    public string Id { get; }
    public IReadOnlyList<CardAsset> Cards { get; }
    public IReadOnlyList<string> Validate();
    public IReadOnlyList<CardSpec> ToSpecs();
}
```

`StarterPool.asset`의 계약은 다음과 같다.

- `Id = "starter_pool"`
- 카드 참조 22개
- null 참조 없음
- 카드 ID 중복 없음
- 각 문서 카드가 정확히 한 번 존재
- 장수나 덱 편성 순서를 의미하지 않음

`Validate()`는 풀 ID, null 참조, 중복 카드 ID, 카드 메타데이터를 검사해 사람이 읽을 수 있는 오류 목록을
반환한다. `ToSpecs()`는 먼저 `Validate()`를 호출하고 오류가 하나라도 있으면 `InvalidOperationException`을
던진다. 잘못된 풀에서 일부 카드만 조용히 변환하는 동작은 허용하지 않는다.

`DeckAsset`은 실제 덱과 카드별 장수를 계속 담당한다. 두 타입을 합치지 않는다.

### 4.5 22장 에셋 위치

```text
Assets/Unity/CardSO/Player/StarterPool/
  vanguard_slash.asset
  parry_strike.asset
  ...
  posthumous_spread.asset

Assets/Unity/CardSO/Player/StarterPool.asset
```

기존 `Assets/Unity/CardSO/Player/StarterDeck.asset`과 기존 10장 에셋은 수정하지 않는다.

## 5. 시더와 SO 원본성

에디터 메뉴 `Fate Weaver/Seed Starter Pool Assets`를 추가한다.

시더는 `StarterPoolSpecs.Build()`를 **최초 생성 입력**으로만 사용한다.

- 카드 에셋이 없으면 규칙 필드, 등급, 태그를 채워 생성한다.
- 카드 에셋이 이미 있으면 규칙 값, 등급, 태그, 아트, 설명을 덮어쓰지 않는다.
- 누락된 카드만 생성한다.
- `StarterPool.asset`은 22개 참조가 정확히 들어가도록 생성하거나 갱신한다.
- 풀 갱신 전에 모든 카드 ID와 메타데이터를 검증한다.
- 검증 실패 시 `AssetDatabase.SaveAssets()`를 호출하지 않고 오류를 보고한다.

이 규칙으로 최초 시드 이후에는 SO가 진실의 원천이 된다. `StarterPoolSpecs`를 수정하거나 시더를 다시
실행해도 기존 SO 튜닝이 되돌아가지 않는다.

저장소 작업 규칙상 전용 워크트리에서는 실제 SO 콘텐츠를 저작하지 않는다. 워크트리 브랜치에서는 타입,
시더, 생성기, 자동화 테스트를 구현한다. 병합 승인 후 메인 Unity 체크아웃에서 시더를 실행해 실제
22개 `.asset`과 `StarterPool.asset`을 생성한다.

## 6. 생성 C#의 역할

생성기는 기존 `StarterDeck()`과 별도로 다음 스냅샷을 내보낼 수 있어야 한다.

```csharp
public static IReadOnlyList<CardSpec> StarterPool()
```

생성 스냅샷에는 규칙 데이터만 포함한다.

- 개입 키, 효과값, 대상 진영, 인접 제한
- 모든 `EffectSpec`과 조건·대상 선택 데이터
- 등급·태그·아트·표현용 설명은 제외

`GeneratedCards.StarterPool()`은 헤드리스 테스트와 Compare 하니스만 사용한다. 게임 런타임, Unity
컨트롤러, `CardPoolAsset`은 생성 클래스에 의존하지 않는다.

풀 에셋이 아직 없으면 기존 시작 덱 생성은 계속 성공해야 하며, 풀 스냅샷을 생략했다는 명시적 경고를
남긴다. 시더 실행 후에는 풀 검증 실패가 생성 전체를 중단한다.

## 7. 검증과 오류 처리

### 7.1 순수 헤드리스

- `CardSpecMapper`가 개입 대상 진영과 인접 제한을 `InterventionActionData`로 옮긴다.
- 22개 순수 스펙의 기존 규칙·독·설명 테스트가 유지된다.
- 생성 풀 스냅샷이 준비된 뒤에는 22개 ID와 규칙 서명을 SO export 결과와 고정한다.
- 전체 `dotnet test`는 로컬 SDK에 맞춰
  `-p:TargetFramework=net5.0`으로 실행한다.

### 7.2 Unity EditMode

- `CardAsset.ToSpec()`이 개입 제약을 보존한다.
- 등급과 태그는 `CardAsset`에 남고 `CardSpec`에는 들어가지 않는다.
- `CardPoolAsset.ToSpecs()`가 22개 참조를 손실 없이 변환한다.
- null 카드, 중복 카드 ID, 빈 태그, 중복 태그를 거부한다.
- 시더 첫 실행은 누락 에셋을 만들고, 재실행은 기존 카드 값을 보존한다.
- 기존 `StarterDeck.asset`과 기존 10장 카드가 바뀌지 않는다.

Unity 배치 테스트 로그와 결과는 `/private/tmp`에 저장한다.

## 8. 향후 Unity 전용 검증으로 전환

헤드리스 콘텐츠 검증을 없애기로 결정하면 다음 순서로 제거한다.

1. 실제 `CardAsset`/`CardPoolAsset`을 읽는 Unity EditMode/Batchmode 검증을 CI 필수 단계로 만든다.
2. Compare 하니스가 필요하면 Unity 테스트 또는 Unity 전용 도구로 이전한다.
3. `GeneratedCards`를 참조하는 헤드리스 콘텐츠 테스트와 도구를 제거한다.
4. `CardCodeGenerator`의 풀·덱 export와 생성 파일을 제거한다.
5. `StarterPoolSpecs`와 `Tests/Headless`, `Tools/FateWeaver.Headless`의 남은 소비처를 정리한다.

이 전환에서도 `FateWeaver.Core`는 `UnityEngine`을 참조하지 않는 순수 C#으로 유지한다. Unity 종속화는
카드 콘텐츠 로드와 검증 경계에만 둔다.

## 9. 완료 조건

- 개입 카드 4장의 제약이 SO 왕복과 생성 스냅샷에서 손실되지 않는다.
- 22장 후보를 담는 별도 `CardPoolAsset` 경로가 존재한다.
- 시작 덱 에셋과 기존 10장 카드가 변경되지 않는다.
- 등급·태그가 Unity 메타데이터로 보존되며 전투 규칙에는 유입되지 않는다.
- 시더 재실행이 기존 SO 튜닝과 아트를 덮어쓰지 않는다.
- Unity 런타임은 생성 C#을 참조하지 않는다.
- 관련 Unity EditMode 테스트와 전체 헤드리스 테스트가 통과한다.
- 실제 22개 SO 생성은 병합 승인 후 메인 Unity 체크아웃에서 수행한다.
