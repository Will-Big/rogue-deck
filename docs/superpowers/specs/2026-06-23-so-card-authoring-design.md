# Fate Weaver — 하이브리드 카드 저작 (ScriptableObject → 생성 C#) 설계

작성일: 2026-06-23

## 1. 동기

로직은 순수 C#(헤드리스 검증·밸런스 시뮬)으로 두되, **카드 콘텐츠 저작은 Unity 인스펙터**로 하고 싶다.
ScriptableObject는 헤드리스에서 못 읽으므로(에디터 없이 `dotnet test` 로드 불가), "인스펙터 튜닝 + 헤드리스
시뮬 둘 다"를 진짜로 얻으려면 **단일 원천 + 헤드리스용 export**가 필요하다.

**결정(브레인스토밍 합의):**
- **SO가 진실의 원천**, **헤드리스용 export는 "생성된 순수 C#"**(JSON 아님 — Unity는 `System.Text.Json`을
  기본 미포함이라 양쪽 컴파일되는 생성 C#이 파서·직렬화 0으로 가장 깔끔).
- 효과/조건은 **enum-평탄화 `EffectSpec`**(폴리모픽 record를 인스펙터·생성에 쉬운 평탄 데이터로). 현재 카드
  전부 커버; 새 패턴은 enum 추가로 확장.
- **범위 분해**: **(1) SO 저작 + 생성 C# 파이프라인**(이 스펙) → **(2) Phase 2 덱/손패 UI + 프리팹화**(별도 스펙).

## 2. 데이터 흐름 (한 원천 → 양쪽)

```
[저작]  CardAsset (ScriptableObject, 인스펙터)
   │ ToSpec()
   ▼
   CardSpec (순수 데이터, enum-평탄)
   │                                  ┌─[에디터 메뉴: 코드 생성]→ Simulation/Generated/GeneratedCards.cs
   │ CardSpecMapper.ToDefinition       │                              (순수 CardSpec 리터럴)
   ▼                                  │                                   │ CardSpecMapper.ToDefinition
   CardDefinition (순수 코어)           │                                   ▼
   ▲                                  │                          [헤드리스] 생성 리터럴 → CardDefinition (시뮬·테스트)
[Unity 런타임] SO→Spec→Definition ──────┘
```

`CardSpec`(순수, enum-평탄)과 `CardSpecMapper`(순수)가 중심축. SO는 저작용, 생성 C#은 헤드리스 다리.
**양쪽이 동일 mapper로 동일 CardDefinition을 얻는다.** 생성기는 SO 저작 후 재실행해 헤드리스를 동기화한다.

## 3. 컴포넌트

### 3.1 순수 (Assets/FateWeaver/Simulation — 헤드리스 검증 가능)

- **저작 enum** (struct-키 코어 타입을 닫힌 enum으로 노출):
  - `EffectKind` = Damage | ApplyStatus | GrantNextAttackBonus | NullifyNextReward
  - `ConditionKind` = None | FirstToTrigger | WithinNth | BeforeNextEnemyAttack | PrevIsPlayerAttack | NextIsEnemyAttack
  - `StatusKindRef` = None | Stun | Vulnerable | Block | RewardNullified
  - `FateKind` = None | ChangeInitiative | SwapInitiative | Lock
  - 재사용(이미 코어 enum): `Side`, `CardType`, `CardCategory`, `StatusApplyTarget`, `StatusLifetimeKind`
- **`EffectSpec`** (평탄 `[System.Serializable]` struct): `EffectKind Kind`, `int Amount`, `ConditionKind Condition`,
  `int ConditionN`, `int SuccessAmount`, `StatusKindRef Status`, `StatusLifetimeKind Lifetime`, `int LifetimeCount`,
  `StatusApplyTarget Target`.
- **`CardSpec`** (순수): `string Id, Name; Side Side; CardType Type; CardCategory Category; int Cost, BaseInitiative;`
  `EffectSpec[] Effects; FateKind Fate; int FateAmount`.
- **`CardSpecMapper`** (순수 static): `CardDefinition ToDefinition(CardSpec)`.

#### 매핑 규칙 (CardSpecMapper)
- `EffectKind→EffectKey`: Damage→`EffectKeys.Damage`, ApplyStatus→`EffectKeys.ApplyStatus`,
  GrantNextAttackBonus→`EffectKeys.GrantNextPlayerAttackDamageBonus`, NullifyNextReward→`EffectKeys.NullifyNextPlayerConditionReward`.
- `ConditionKind→Condition`: None→`null`, FirstToTrigger→`new FirstToTrigger()`, WithinNth→`new WithinNth(ConditionN)`,
  BeforeNextEnemyAttack→`new BeforeNextEnemyAttack()`, PrevIsPlayerAttack→`new AdjacentCardIs(Previous, Player, Attack)`,
  NextIsEnemyAttack→`new AdjacentCardIs(Next, Enemy, Attack)`.
- `StatusKindRef→StatusKey?`, `StatusLifetimeKind(+LifetimeCount)→StatusLifetime`.
- `EffectSpec→EffectData`:
  - ApplyStatus: `new EffectData(ApplyStatus, Amount){ StatusKey, StatusLifetime, StatusTarget=Target, Condition=(Condition!=None? map : null), SuccessAmount=(Condition!=None? SuccessAmount : (int?)null) }`.
  - 그 외: `Condition!=None ? EffectData.Conditional(key, Amount, cond, SuccessAmount) : new EffectData(key, Amount)`.
- `CardSpec→CardDefinition`:
  - Fate: `new CardDefinition(Id, Name, Side, Type, 0, Array.Empty<EffectData>()){ Cost, Category=Fate, FateAction = new FateActionData(map(Fate), Cost, FateAmount) }`.
  - Action: `new CardDefinition(Id, Name, Side, Type, BaseInitiative, Effects.Select(ToEffectData).ToArray()){ Cost, Category=Action }`.

### 3.2 Unity 저작 (Assets/FateWeaver/Unity — 사용자 검증)

- **`CardAsset : ScriptableObject`**: `Id, DisplayName; Side; CardType; CardCategory; Cost; BaseInitiative;`
  `Sprite Art; string Description; EffectSpec[] Effects; FateKind Fate; int FateAmount` + `CardSpec ToSpec()`.
  (`[CreateAssetMenu]`로 인스펙터에서 생성. Art/Description은 저작 편의용이며 CardSpec엔 안 들어감 — 그건
  표현 계층 `PlaytestCardArt`/`CardDescription`이 담당.)
- **`DeckAsset : ScriptableObject`**: `string Id; List<Entry> Entries`(Entry = `{ CardAsset Card, int Count }`) +
  `IReadOnlyList<CardSpec> ToSpecs()`(count만큼 펼침).
- **`CardCodeGenerator`** (Editor): 메뉴 `Fate Weaver/Generate Cards from SO` — 프로젝트의 CardAsset/DeckAsset을
  읽어 `Assets/FateWeaver/Simulation/Generated/GeneratedCards.cs`(순수 `CardSpec` 팩토리)를 생성·저장.

## 4. 검증

- **헤드리스(내가)**: 
  - `CardSpecMapper` 필드 단위 테스트(각 EffectKind/ConditionKind/StatusKindRef/FateKind 매핑).
  - **등가성 안전망**: 시작덱 10장을 `CardSpec`(순수 `StarterDeckSpecs`)으로 재표현 → ToDefinition → 그 덱으로
    `DeckCombatSession` 불변식(찰나 첫 발동 8 / 강타 콤보 10 / 엄호 적 공격 앞 흡수)을 다시 통과시켜 **손코딩
    `StarterDeck`과 동작 등가**임을 증명.
- **사용자(에디터)**: CardAsset/DeckAsset 저작 → `Generate Cards from SO` 실행 → 생성 파일 컴파일 + 헤드리스
  스위트 그린 확인.

## 5. 단계

- **1a (순수, 헤드리스)**: 저작 enum + `EffectSpec` + `CardSpec` + `CardSpecMapper` + `StarterDeckSpecs` + 매핑/등가성 테스트.
- **1b (Unity/에디터)**: `CardAsset`/`DeckAsset` SO + `CardCodeGenerator` + 시작덱을 SO로 저작 + 생성 실행.

## 6. 범위 밖 (후속)

- **(2) Phase 2 덱/손패 UI + 프리팹화**: 컨트롤러를 `DeckCombatSession` 위로 리워크, 손패·운명력·덱/버림 더미·
  미래 영역 표시, **재사용 UI 요소(CardView 등)를 모두 프리팹화**. 별도 스펙.
- **Composite 조건(AllOf)**: 시작덱 불필요. Phase 3 보상 카드(반격 자세 등) 때 `ConditionKind`에 명명 패턴으로 확장.
- **다중 적 정밀 타깃**: 기존 한계(Enemies[0] 근사) 유지.

## 7. 메모

손코딩 `StarterDeck`(Simulation)은 등가성 테스트의 오라클로 **남겨둔다**(생성본과 동작 일치를 보증). 생성기가
안정되면 런타임·테스트를 생성/Spec 기반으로 옮길 수 있다.
