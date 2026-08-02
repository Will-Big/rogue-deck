# Fate Weaver — 프리미티브 카드 프레임과 구조화 설명 설계

- 작성일: 2026-07-31
- 문서 유형: `architecture`, `ux-design`
- 주 도메인: `card-frame`, `card-description`, `responsive-hand`
- 상태: `current` — 실행·개입 카드 프레임, 대상 심볼, 구조화 설명과 반응형 핸드의 권위 문서
- 선행 규칙:
  [위치 대상과 카드 텍스트](2026-07-27-position-targeting-card-text-design.md),
  [카드 설명 레지스트리](2026-07-16-description-registry-design.md)
- 관련 구현:
  `DescriptionComposer`, `CardPresentation`, `CardView`, `HandFanView`, `RailCardView`

## 1. 목적

현재 카드 설명은 대상의 진영을 구분하지 않은 채 `가장 앞의 대상에게` 같은 문구를 반복한다. 예를 들어
`독성 환원`은 적의 독을 소비하고 다시 부여한 뒤 카드 사용자인 아군 자신에게 방어를 주지만, 평문만
읽으면 어느 효과가 적과 자신 중 누구에게 적용되는지 즉시 알기 어렵다.

이 설계는 카드 규칙에서 대상 의미를 구조화해 다음 표현을 만든다.

1. 실행 카드의 독립 대상 칸은 진영과 위치 범위를 **심볼만으로** 표시한다.
2. 설명 본문은 대상이 달라지는 지점에서 줄을 나누고 각 줄 앞에 진영 심볼을 붙인다.
3. 개입 카드는 실행 카드와 다른 프레임을 사용하며 실행 순서와 대상 칸을 두지 않는다.
4. 비용과 실행 순서는 핸드에서 프레임 밖으로 돌출하되 겹치는 카드 사이에서도 읽을 수 있다.
5. 프레임 내부 배치는 해상도마다 다시 만들지 않고, 핸드 간격과 전체 스케일만 반응형으로 조정한다.

## 2. 범위

### 2.1 포함

- 순수 C# 구조화 카드 설명 모델
- 효과 설명 핸들러가 문장과 대상 의미를 함께 반환하는 계약
- 대상 칸 항목과 설명 줄 합성
- 심볼 기반 헤드리스 평문
- `ExecutionCardView.prefab`, `InterventionCardView.prefab`
- 프리미티브 도형으로 조립하는 대상·비용·실행 순서 심볼
- 실행·개입 프리팹을 선택하는 직렬화 카탈로그
- 겹치는 핸드를 보존하는 반응형 간격과 균일 스케일
- 폐기된 `Random`, `SecondFromFront` 선택자의 타입·매핑·테스트 제거
- 승인된 `앞 하나`, `앞 둘`, `뒤 하나`, `뒤 둘`, `모두`, `자신` 범위로의 스키마 정리
- 헤드리스 및 Unity EditMode 검증

### 2.2 제외

- 카드 아트 제작
- 최종 색상·재질·장식 스타일
- 카드 수치와 밸런스 변경
- 실행 영역의 소형 `RailCardView` 재설계
- 개입 액션이 선택하는 카드나 슬롯의 규칙 변경
- 대상 효과의 실행 순서 변경

## 3. 권위 관계

[위치 대상과 카드 텍스트](2026-07-27-position-targeting-card-text-design.md)는 진영별 단일 위치 범위,
실행 시작 시 대상 확정, 앞·뒤 범위의 의미를 계속 소유한다.

이 문서는 그 규칙을 화면과 설명 데이터로 옮기는 구현 경계를 소유한다. 특히 다음 사항을 구체화한다.

- 실행 카드와 개입 카드의 프레임은 서로 다르다.
- 실행 카드 대상 칸에는 글자를 넣지 않는다.
- 개입 카드에는 대상 칸 자체가 없다.
- 헤드리스 평문도 진영 이름이 아니라 심볼을 사용한다.
- 대상 칸의 중복 제거와 능력 문장의 반복 보존은 서로 다른 규칙이다.

## 4. 책임 경계

```text
CardAsset
    |
    v
CardSpec -> CardDefinition / EffectData
    |
    v
DescriptionComposer + description registries       순수 C#
    |
    +--> CardDescriptionLayout
            |-- TargetEntries
            |-- Lines
            `-- PlainText
    |
    v
CardPresentation                                   Unity 경계
    |
    v
CardPrefabCatalog
    |-- ExecutionCardView.prefab
    `-- InterventionCardView.prefab
```

- `CardAsset`과 코어 데이터는 규칙의 단일 원천이다.
- `CardAsset`에 표시용 대상 문자열이나 심볼 필드를 중복 저작하지 않는다.
- 순수 C# 설명 레이어가 진영, 범위, 설명 줄을 결정한다.
- Unity는 전달받은 의미를 심볼 프리팹과 텍스트로 표시하며 규칙을 다시 해석하지 않는다.
- 실제 효과 실행과 대상 확정은 기존 코어 규칙을 따른다. 프레임은 실행 결과를 바꾸지 않는다.

## 5. 구조화 설명 모델

아래 코드는 책임을 설명하기 위한 스케치다. 실제 구현은 프로젝트의 C# 버전과 기존 명명 규칙을 따른다.

```csharp
public enum CardTargetFaction
{
    Ally,
    Enemy
}

public enum CardTargetRange
{
    Self,
    FrontOne,
    FrontTwo,
    BackOne,
    BackTwo,
    All
}

public readonly record struct CardTargetKey(
    CardTargetFaction Faction,
    CardTargetRange Range);

public sealed record EffectDescriptionFragment(
    CardTargetKey? Target,
    string Text);

public sealed record CardDescriptionLine(
    CardTargetKey? Target,
    string Text);

public sealed record CardDescriptionLayout(
    IReadOnlyList<CardTargetKey> TargetEntries,
    IReadOnlyList<CardDescriptionLine> Lines,
    string PlainText);
```

`Ally`는 플레이어 파티, `Enemy`는 적 대형을 뜻한다. 카드 소유자에 대한 상대 명칭이 아니다. 따라서
플레이어 카드의 일반 공격은 `Enemy`, 적 카드의 일반 공격은 `Ally`를 향한다. `Self` 효과의 진영은
카드 사용자의 실제 진영에서 정한다.

`Self`는 진영이 아니다. `Ally/Self` 또는 `Enemy/Self`처럼 진영과 범위를 독립된 두 축으로 표현한다.
따라서 `독성 환원`의 대상은 `Self/Self`가 아니라 다음 두 키다.

```text
Ally / Self
Enemy / FrontOne
```

유닛을 대상으로 하지 않는 효과는 `CardTargetKey?`가 `null`이다. 드로우, 운명력, 덱 조작과 개입 액션
설명은 이 경로를 사용한다.

## 6. 심볼 문법

### 6.1 대상 칸

대상 칸은 실행 카드에만 존재하며 텍스트를 사용하지 않는다. 아군은 왼쪽, 적군은 오른쪽에 두고 양
진영의 전열이 가운데를 향한다.

| 범위 | 아군 | 적군 |
|---|---|---|
| 자신 | `◇◎` | `◎◆` |
| 앞 하나 | `━━━━◇` | `◆━━━━` |
| 앞 둘 | `━━━◇◇` | `◆◆━━━` |
| 뒤 하나 | `◇━━━━` | `━━━━◆` |
| 뒤 둘 | `◇◇━━━` | `━━━◆◆` |
| 모두 | `◇━━━━━` | `━━━━━◆` |

위 문자는 의미를 설명하기 위한 정규 평문 표기다. 실제 Unity 프레임은 다음 프리미티브를 조합한다.

- 레일: 얇은 사각형 `Image`
- 선택 유닛: 회전한 사각형으로 만든 마름모
- 자신: 원형 `Image`를 중첩한 이중 원
- 진영: 채움·윤곽 형태와 방향을 함께 사용
- 색상: 보조 구분 수단이며 색상만으로 진영을 구분하지 않는다

유닛 대상이 없는 실행 카드는 `없음`이라는 글자 대신 원과 사선으로 조립한 `∅` 심볼을 표시한다.

### 6.2 설명 줄

설명 줄은 진영 이름과 위치 범위를 텍스트로 쓰지 않는다. 실행 카드의 가운데 대상 칸이 진영과 위치 범위를
모두 표시하므로, Unity 설명 블록은 각 유닛 대상 문장의 **진영만** 한 글자 심볼로 표시한다.

```text
<적군색>◆</적군색> 피해 3.
<아군색>◆</아군색> 방어 3.
<아군색>◆</아군색> 소비했다면 방어 4.
<적군색>◆</적군색> 취약 1.
```

Unity 진영 심볼은 별도 `Image` 슬롯이나 중첩 `TargetGlyphView`가 아니라 설명 `TMP_Text`의 첫 문자로
들어간다. 심볼과 본문이 하나의 텍스트 흐름을 이루므로 긴 문장의 다음 줄은 심볼 아래 공간까지 사용한다.
심볼 뒤에는 공백 하나만 두며 대괄호는 표시하지 않는다. 두 진영 모두 같은 채운 마름모 `◆`를 사용하고
심볼 한 글자의 색만 다르게 표시한다. 설명 본문은 프리팹의 기본 본문색을 유지한다.

- 모든 `Ally` 범위와 `Ally/Self`: 파랑 `◆`, `#5DADE2`
- 모든 `Enemy` 범위와 `Enemy/Self`: 빨강 `◆`, `#E85D5D`
- 대상 없음: 접두사 없이 본문만 표시

두 색은 `DescriptionLineView`의 `[SerializeField] private Color`로 프리팹에 저작하며 코드 상수로
고정하지 않는다. `Bind`는 선택한 색을 TMP rich-text 색상 태그로 변환해 `◆` 한 글자에만 적용한다.
위치 범위는 설명 줄에서 반복하지 않고 실행 카드 가운데 `SymbolOnlyTargetPanel`의 프리미티브 glyph가
소유한다. 개입 카드와 대상 없는 효과는 진영 심볼을 표시하지 않는다.

헤드리스 `PlainText`에는 가운데 대상 칸이 없으므로 기존 정규 대상 토큰을 유지한다. 대괄호는 이 평문에서
심볼 경계를 나타내는 구두점이며, `Ally/Self`의 `◇◎`와 `Enemy/Self`의 `◎◆`처럼 진영과 범위를 함께
보존한다. 색상을 표현할 수 없는 평문은 윤곽·채움 형태를 계속 사용한다. 따라서 Unity 설명 블록은 같은
형태와 진영색을 사용하면서도 터미널 출력은 대상 정보를 잃지 않는다.

## 7. 합성 규칙

### 7.1 효과 조각

`IEffectDescriptionHandler`는 더 이상 대상 접두사를 포함한 완성 문자열을 반환하지 않는다. 대상 문구를
제외한 효과 문장과 `CardTargetKey?`를 함께 반환한다. Composer는 카드마다 `CardDefinition.Side`를
포함한 설명 컨텍스트를 만들고 핸들러에 전달한다. 핸들러는 카드 진영, `TargetSelector`, 효과 payload를
함께 사용해 실제 대상 진영과 범위를 결정한다.

```text
DamageDescriptionHandler (플레이어 카드)
    -> Enemy/FrontOne, "피해 3"

ApplyStatusDescriptionHandler (플레이어 카드, Self)
    -> Ally/Self, "방어 4"

DamageDescriptionHandler (적 카드)
    -> Ally/FrontOne, "피해 3"

GrantNextTurnFateDescriptionHandler
    -> null, "다음 사용 턴에 운명력 1 획득"
```

조건 성공 문장은 원래 효과의 대상 키를 유지한다. `소비했다면 방어 4`처럼 기본 발동을 생략하는
`SkipOnBasic` 규칙도 기존과 동일하게 적용한다.

### 7.2 설명 줄 생성

Composer는 효과 작성 순서를 바꾸지 않는다.

1. 효과를 원래 순서대로 설명 조각으로 변환한다.
2. 이전 조각과 `CardTargetKey?`가 같으면 같은 줄에 이어 붙인다.
3. 키가 달라지면 줄바꿈한다.
4. 이후 같은 키가 다시 나타나더라도 중간에 다른 대상이 있었다면 새 줄을 만든다.
5. 문장과 효과 횟수는 절대 중복 제거하지 않는다.

따라서 다음 효과는:

```text
Enemy/FrontOne 피해 3
Ally/Self 방어 2
Enemy/FrontOne 피해 3
```

다음 세 줄을 유지한다. 아래 표기는 가운데 대상 칸이 없는 헤드리스 정규 평문이다.

```text
[◆] 피해 3.
[◇◎] 방어 2.
[◆] 피해 3.
```

### 7.3 대상 칸 항목 생성

대상 칸의 중복 제거는 **효과나 설명 문장에 적용되지 않는다**. 실행 카드가 사용하는 대상 키의 목록에만
적용한다.

```text
Enemy/FrontOne 피해 3
Enemy/FrontOne 피해 3
```

위 카드는 피해를 두 번 실행하고 설명도 두 문장을 보존한다. 다만 대상 칸에는 같은
`Enemy/FrontOne` 심볼을 두 번 그릴 이유가 없으므로 대상 항목 하나만 표시한다.

정확한 중복 판정 키는 `(Faction, Range)` 쌍이다. 키를 집합으로 수집한 뒤 대상 칸의 고정 읽기 순서인
`Ally` 왼쪽, `Enemy` 오른쪽으로 정렬한다. 효과 키, 수치, 조건, 문장 문자열은 중복 판정에 사용하지
않는다.

`피해 3 ×2` 같은 축약은 별도의 문장 압축 기능이다. 개입이나 상태가 실행 중 효과를 추가할 수 있으므로
이번 범위에서는 자동 축약하지 않고 두 문장을 그대로 보존한다.

## 8. 진영별 단일 범위 검증

한 실행 카드는 같은 진영에 하나의 위치 범위만 사용한다. 이는 효과를 하나만 허용한다는 뜻이 아니다.

다음은 유효하다.

```text
Enemy/FrontOne 피해 3
Enemy/FrontOne 독 1
Ally/Self 방어 4
```

적 대상 효과 두 개가 같은 `Enemy/FrontOne` 대상을 공유하고, 아군 효과는 별도의 `Ally/Self`를
사용한다.

다음은 유효하지 않다.

```text
Enemy/FrontOne 피해 3
Enemy/BackOne 독 1
```

대상 칸에 적 진영 범위를 하나만 표시하기로 했는데 같은 카드가 적의 앞 하나와 뒤 하나를 동시에 요구하면
어느 범위가 적 효과를 대표하는지 한눈에 판단할 수 없다. 이 경우 Composer와 콘텐츠 부팅 검증은 카드
ID, 진영, 충돌한 두 범위를 포함한 오류를 낸다.

서로 다른 진영이 서로 다른 범위를 쓰는 것은 유효하다. `Enemy/FrontOne`과 `Ally/Self`를 함께 쓰는
`독성 환원`이 대표 사례다.

## 9. `독성 환원` 결과

구조화 결과:

```text
TargetEntries
- Ally / Self
- Enemy / FrontOne

Lines
- Enemy / FrontOne / "독 최대 1 소비. 독 1."
- Ally  / Self     / "소비했다면 방어 4."
```

실행 카드 대상 칸:

```text
◇◎ │ ◆━━━━
```

Unity 설명 본문:

```text
<color=#E85D5D>◆</color> 독 최대 1 소비. 독 1.
<color=#5DADE2>◆</color> 소비했다면 방어 4.
```

헤드리스 평문:

```text
[◆] 독 최대 1 소비. 독 1.
[◇◎] 소비했다면 방어 4.
```

`가장 앞의 대상에게`, `적`, `아군`, `자신` 같은 대상 단어는 설명 문장에 넣지 않는다.

## 10. 폐기 선택자 제거

`Random`과 `SecondFromFront`는 승인된 위치 대상 규칙에서 폐기됐다. 호환용으로 남기거나 새 심볼로
근사하지 않고 이번 작업에서 제거한다.

제거 범위:

- 코어 `TargetSelector.Random`, `TargetSelector.SecondFromFront`
- 저작 `TargetSelectorRef.Random`, `TargetSelectorRef.SecondFromFront`
- `EffectSpec.ToSelector()` 매핑
- `KoreanDescriptionGrammar.Target()`의 해당 문구와 무작위 기본 분기
- 대상 선택 유틸리티와 핸들러의 해당 분기
- 레거시 선택자 전용 테스트와 생성 코드
- ScriptableObject 직렬화 값과 생성 산출물 검증

최종 위치 범위는 다음 닫힌 집합으로 정리한다.

```text
FrontOne
FrontTwo
BackOne
BackTwo
All
```

`Self`는 대형 선택자가 아니라 현재 카드 사용자라는 별도 대상 범위로 유지한다. `FrontTwo`와 `BackTwo`는
프레임 심볼뿐 아니라 실제 대상 확정 규칙과 헤드리스 테스트를 함께 구현한 뒤 카드에서 사용할 수 있다.

현재 소스 기반 카드 콘텐츠에서는 `Random`과 `SecondFromFront`의 명시적 사용이 발견되지 않았다. 그래도
모든 `CardAsset`을 순회해 폐기 값이 직렬화된 에셋이 없는지 부팅 검증으로 확인하며, 발견하면 카드 ID와
에셋 경로를 포함해 실패시킨다.

## 11. Unity 프레임

### 11.1 실행 카드

`ExecutionCardView.prefab`은 다음 레이어를 가진다.

```text
ExecutionCardView
├─ Background + Border
├─ HeaderLayer
│  └─ Name
├─ ArtPanel
├─ SymbolOnlyTargetPanel
│  └─ TargetGlyphView.prefab × 0..2
├─ DescriptionPanel
│  └─ DescriptionLineView.prefab × N
└─ OverlayLayer
   ├─ CostBadge
   ├─ ExecutionOrderBadge
   ├─ OwnerChip
   └─ StatusIcon prefab × N
```

- 비용 원의 기준 크기는 68이다.
- 실행 순서 마름모의 기준 크기는 50이다.
- 비용은 좌상단으로 크게 돌출한다.
- 실행 순서는 오른쪽으로 돌출하되 비용보다 낮은 높이 띠에 둔다.
- 두 배지는 카드 프레임의 마스크에 잘리지 않는 최상위 오버레이에 둔다.
- 대상 칸에는 심볼만 있고 `TMP_Text`를 두지 않는다.

정확한 돌출 오프셋은 프리팹에서 저작하고 지원 해상도·최대 핸드 수 검증으로 확정한다. C#이 배지 좌표를
매 프레임 덮어쓰지 않는다.

### 11.2 개입 카드

`InterventionCardView.prefab`은 실행 카드의 프리팹 변형이 아니라 별도 레이아웃 프리팹이다.

```text
InterventionCardView
├─ Background + Border
├─ HeaderLayer
│  └─ Name
├─ ArtPanel
├─ ExpandedDescriptionPanel
│  └─ DescriptionLineView.prefab × N
└─ OverlayLayer
   ├─ CostBadge
   ├─ OwnerChip
   └─ StatusIcon prefab × N
```

- 실행 순서 배지를 만들지 않는다.
- 대상 칸을 만들지 않는다.
- 실행 카드의 대상 칸 높이를 설명 영역에 추가한다.
- 비용 원은 실행 카드와 같은 크기와 돌출 원칙을 사용한다.

두 프레임은 `CardView`, 설명 줄, 소유자·상태 표현 컴포넌트를 공유할 수 있지만 레이아웃 프리팹은
공유하지 않는다.

### 11.3 프리미티브 자산

사각형과 레일은 색을 입힌 `Image`로 만든다. 마름모는 사각형을 45도 회전한다. 이중 원과 `∅`에는
재사용 가능한 최소 원형 스프라이트만 허용하며 카드별 비트맵 프레임을 만들지 않는다.

`DescriptionLineView.prefab`은 전체 대상 glyph를 포함하지 않고 폭 전체를 사용하는 `TMP_Text` 하나를
표시한다. `DescriptionLineView.Bind`가 `CardDescriptionLine.Target`의 `Faction`만 읽어 같은 `◆ `
접두사를 본문과 같은 문자열 흐름에 추가하고, 아군은 직렬화된 `#5DADE2`, 적군은 직렬화된 `#E85D5D`
색을 심볼 한 글자에만 적용한다. `Range`는 무시하며, 대상이 없으면 접두사를 추가하지 않는다. 중앙
`TargetGlyphView.prefab`과 그 프리미티브 계층은 변경하지 않는다.

프리팹 참조는 `[SerializeField] private`으로 연결한다. `Resources.Load` 문자열, `GameObject.Find`,
런타임 이름 비교를 사용하지 않는다.

## 12. 프리팹 선택과 소비처

`CardPrefabCatalog`는 다음 직렬화 참조를 가진다.

```text
ExecutionCardView
InterventionCardView
TargetGlyphView
DescriptionLineView
```

전체 카드가 필요한 소비처는 카테고리로 프리팹을 조회한다.

- `HandFanView`
- 덱·버림 더미·전체 덱 팝업
- 실행 영역 호버 미리보기
- 플레이스먼트 비행 복사본
- `DeckPlaytestController`의 손패 표현

현재 하나의 `_cardPrefab`만 받는 경로는 카탈로그 또는 프리팹 제공자 참조로 바꾼다. 파일 경로나 카드 ID로
프리팹을 찾지 않는다.

실행 영역의 소형 `RailCardView`는 별도 프리팹이다. 소형 순서 표시는 유지할 수 있지만 핸드의 큰 비용·순서
돌출 배지를 재사용하지 않는다.

## 13. 반응형 핸드

### 13.1 원칙

카드 내부 좌표는 해상도별로 다시 계산하지 않는다. 프리팹 기준 좌표와 비율을 유지하고 핸드의 간격과
전체 스케일만 조정한다.

현재 `CanvasScaler`의 기준 해상도 `1280×720`을 논리 좌표계로 사용한다. `HandFan` 루트는 화면 하단
안전 영역 안에서 좌우로 늘어나며 실제 사용 가능한 폭과 높이를 제공한다.

### 13.2 계산 순서

```text
availableWidth = handRoot.width - safeMargins

spacing = clamp(
    (availableWidth - cardWidth - badgeOverflow) / (cardCount - 1),
    minimumSpacing,
    baseSpacing)

fanScale = min(
    1,
    availableWidth / widthAtMinimumSpacing,
    availableHeight / requiredFanHeight)
```

1. 기준 해상도와 여유 화면에서는 현재 기본값인 카드 폭 170, 간격 150, 약 20 겹침을 유지한다.
2. 화면이 좁아지거나 카드가 늘면 간격을 최소 간격까지 줄여 겹침을 늘린다.
3. 최소 간격으로도 들어가지 않으면 부채꼴 `Content` 전체를 균일 축소한다.
4. 비용, 실행 순서, 본문, 클릭 영역은 같은 루트 아래에서 함께 축소된다.
5. 카드 위치, 회전, 낙차는 기존 `HandFanLayout.PoseFor`의 결정론적 계산을 사용한다.
6. 루트 크기 또는 카드 수가 바뀔 때만 재계산한다. `LateUpdate()`에서 자식 좌표를 반복 지정하지 않는다.

### 13.3 직렬화 설정

다음 시각 튜닝 값은 `const`가 아니라 `[SerializeField] private` 또는 프리팹 `RectTransform`에 둔다.

- 기준 카드 크기
- 기본 간격과 최소 간격
- 카드당 회전량
- 부채꼴 낙차
- 좌우·하단 안전 여백
- 최소 전체 스케일
- 비용·실행 순서 크기와 오프셋

해상도별 `if` 분기나 좌표 테이블을 만들지 않는다.

## 14. 실패 처리와 부팅 검증

다음은 대체 문자열이나 추정 심볼을 표시하지 않고 카드 ID를 포함해 실패시킨다.

- 효과 설명 핸들러가 빈 문장 또는 대상 의미 누락을 반환
- 같은 진영에 서로 다른 위치 범위 사용
- 제거된 `Random`, `SecondFromFront` 직렬화 값 발견
- 지원하지 않는 위치 범위
- 실행·개입 프리팹 참조 누락
- 대상·설명 줄 프리팹 참조 누락
- 카테고리와 프리팹 조합 불일치

개발 빌드와 에디터에서는 가능한 경우 에셋 경로까지 오류에 포함한다. 릴리스 전 부팅 검증을 통과한 카드만
전투 카탈로그에 들어간다.

## 15. 테스트 전략

### 15.1 헤드리스

- `독성 환원`이 `Ally/Self`, `Enemy/FrontOne` 대상 항목을 생성한다.
- 헤드리스 평문에 `적`, `아군`, `자신` 접두사가 없고 심볼 토큰만 있다.
- 적 효과와 아군 자신 효과가 두 줄로 분리된다.
- 대상 없는 효과는 심볼 없는 줄을 만든다.
- 같은 대상의 동일 효과 두 번이 두 문장으로 보존된다.
- 대상 칸만 `(Faction, Range)`로 중복 제거된다.
- 효과 순서가 `Enemy -> Ally -> Enemy`이면 세 줄 순서를 보존한다.
- 같은 진영의 서로 다른 범위가 카드 ID를 포함해 실패한다.
- `Random`, `SecondFromFront` 타입·매핑이 제거됐음을 구조 검사한다.
- 모든 기본 카드가 구조화 설명과 정규 평문을 결정론적으로 생성한다.
- `FrontTwo`, `BackTwo`가 중복 없는 실제 대상 집합을 확정한다.

### 15.2 Unity EditMode

- 카테고리별로 올바른 전체 카드 프리팹을 선택한다.
- 실행 프레임 대상 칸에 `TMP_Text`가 없다.
- 설명 줄은 중첩 `TargetGlyphView`나 별도 심볼 슬롯 없이 폭 전체의 `TMP_Text`를 사용한다.
- 아군·적군 설명은 범위와 무관하게 같은 `◆ ` 접두사를 사용하고 심볼 한 글자만 각각
  `#5DADE2`·`#E85D5D`로 표시한다.
- 진영색은 프리팹 직렬화 값이며 본문색과 다른 부분에는 번지지 않는다.
- 긴 설명의 다음 줄이 진영 심볼 아래까지 흐르고, 대상 없는 설명은 접두사 없이 전체 폭을 사용한다.
- 개입 프레임에는 실행 순서와 대상 패널이 없고 설명 패널이 확장된다.
- 비용과 실행 순서가 프레임 밖에 있으면서 마스크에 잘리지 않는다.
- 겹치는 인접 핸드 카드의 비용과 실행 순서 경계가 서로 가리지 않는다.
- 호버 카드가 형제 순서 최상위에 올라온다.
- 4:3, 16:10, 16:9, 21:9 크기에서 카드가 안전 영역을 벗어나지 않는다.
- 1장부터 최대 핸드 수까지 간격과 전체 스케일이 허용 범위 안에 있다.
- 배치 비행이 원본 카드 카테고리의 프리팹을 유지한다.
- 실행 영역의 `RailCardView`가 핸드 돌출 배지를 참조하지 않는다.

### 15.3 완료 검증

1. 전체 헤드리스 테스트
2. Unity `-batchmode` EditMode 테스트
3. 지원 화면비별 렌더 캡처 비교
4. 프리팹·씬·ScriptableObject의 의도하지 않은 변경 여부 확인
5. 기존 포스터형 프레임 PNG의 참조가 끊겼는지 감사

기존 PNG는 참조 제거를 확인하기 전 삭제하지 않는다. 사용하지 않게 된 파일의 실제 삭제는 구현 계획에서
명시적인 단계로 다룬다.

## 16. 구현 순서의 제약

구현 계획은 다음 의존 순서를 지켜야 한다.

1. 위치 범위 타입 정리와 폐기 선택자 제거
2. 구조화 설명 모델과 헤드리스 테스트
3. 효과 설명 핸들러 마이그레이션
4. `CardPresentation`과 프리팹 카탈로그
5. 프리미티브 심볼·설명 줄 프리팹
6. 실행·개입 전체 카드 프리팹
7. 반응형 핸드와 전체 카드 소비처 교체
8. Unity EditMode 해상도·겹침 검증
9. 전체 회귀 테스트와 사용하지 않는 프레임 자산 감사

각 단계는 앞 단계의 RED/GREEN 검증을 통과한 뒤 진행한다.
