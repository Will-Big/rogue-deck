# Fate Weaver — 전투 화면 컴포넌트 분해 설계

- 작성일: 2026-08-04
- 상태: `current`
- 권위 범위: 전투 화면 Unity 레이어의 컴포넌트 경계와 책임 분배
- 관련: [전투 화면 시각 설계](2026-07-10-battle-scene-visual-design.md) (구도·표현 방향),
  [확장성·하드코딩 후속 리팩터링 백로그](../plans/2026-07-16-architecture-refactor-backlog.md) §9 P2

## 1. 목적

`BattleScreenController` 하나에 몰린 책임을 컴포넌트 넷으로 나눈다. 목표는 줄 수 감소가 아니라
**변경 지점의 국소화**다 — 특히 캐릭터 아트가 스프라이트 시트 애니메이션으로 바뀔 때 한 컴포넌트만
건드리면 되도록 한다.

## 2. 현재 상태와 문제

2026-08-04 실측: **467줄, `[SerializeField]` 18개, 메서드 25개.**

| 책임 | 메서드 | 쓰는 인스펙터 참조 |
|---|---|---|
| 부팅·세션 생성 | `Start`, `StartSession` | 1 |
| 표현 변환 | `PresentationFor`×2, `ArtFor`, `OwnerPresentation`, `CharacterFor` | 2 |
| 유닛 스폰·갱신 | `SpawnUnits`, `RefreshUnits` | 3 |
| 입력 | `OnHandClicked`, `OnZoneClicked`, `OnEmptyClicked`, `OnHandHovered`, `OnTurnButton`, `TryApplySelection`, `CurrentValidTargets` | 3 |
| 렌더·HUD | `RefreshAll`, `RefreshSelections`, `RefreshHudTexts`, `BindPiles`, `SetMessage` | 12 |

세 가지가 얽혀 있다.

1. **`SetMessage`를 모든 덩어리가 쓴다.** 입력 핸들러가 상태 문구를 직접 쓴다.
2. **`PresentationFor`를 입력과 렌더가 함께 쓴다.** 호버 미리보기(`OnHandHovered`)가 표현 변환을
   부르므로 어느 한쪽의 소유가 될 수 없다.
3. **입력 핸들러가 세션 변경·메시지·갱신을 동시에 한다.**

## 3. 범위

### 3.1 포함

- `BattleScreenController`의 책임을 컴포넌트 넷으로 분배
- 기존 `CardSelectionController`가 선택 취소 입력(클릭 캐처 둘)을 흡수
- `BattleSceneBuilder`가 새 컴포넌트를 생성·배선하도록 갱신하고 씬 재생성

### 3.2 제외

- **데이터 출처 변경.** UI는 계속 `DeckCombatSession.State`를 읽는다. 이벤트 타임라인 기반 전환은
  백로그 §9 P2이며, §12.1이 적었듯 `ResolutionEvent` 6종으로는 HP 변화·상태 부여·만료·대형 이동을
  그릴 수 없어 **코어 이벤트 확충이 선행되어야 한다.** 이 설계는 그것과 직교하며, 표현 갱신 지점을
  국소화해 P2를 쉽게 만든다.
- **입력 핸들러 분리.** §4.1 참고.
- **아트 리소스 교체.** §4.5 참고.
- **시각 디자인 변경.** 레이아웃·색·연출은 그대로다. 이 설계는 구조만 바꾼다.

## 4. 결정

### 4.1 경계선은 "세션을 변경하는가"다

| | 세션을 | 결론 |
|---|---|---|
| 표현 변환 | 읽지도 않는다 (델리게이트로 받는다) | 분리 |
| 유닛 뷰·파일 뷰·HUD 뷰 | 스냅샷을 읽기만 한다 | 분리 |
| 입력 핸들러 | **변경한다** | **컨트롤러에 남는다** |

`PlayExecutionCard`·`PlayInterventionCard`·`ResolveTurn`·`BeginNextTurn` — 세션을 바꾸는 호출은
전부 입력 핸들러 안에 있다. 세션의 소유자와 변경자가 갈리면 소유권이 흐려진다.

**입력을 지금 분리하지 않는 이유:**

- 입력 핸들러는 세션 변경 + 메시지 + 갱신을 함께 한다. 떼어내면 `(session, messageSink,
  refreshCallback)` 셋을 주입받는 형태가 되는데, 생성자 인자 셋은 "이건 원래 컨트롤러였다"는 신호다.
- 입력은 이미 메서드 참조로만 노출된다(`SetCards(cards, onClick, onHover)`,
  `onClick.AddListener`, `_selection.Initialize(...)`). 다른 컴포넌트로 옮겨도 **뷰에 꽂는 배선은
  컨트롤러에 남으므로** 파일만 갈리고 얽힘은 그대로다.
- `CurrentValidTargets`는 입력 콜백이면서 세션 질의다. 세션 소유자 옆이 자연스럽다.

**언제 분리가 정당해지나:** P2 이후. UI가 타임라인을 재생하면 입력은 명령 발행만 하고 갱신은 이벤트
구독으로 바뀌어 `refreshCallback`이 사라진다. 그때 인자가 둘로 줄어 분리 비용이 실제로 떨어진다.

### 4.2 컴포넌트 넷과 배치

| 컴포넌트 | 종류 | 씬 위치 | 참조 |
|---|---|---|---|
| `BattlePresenter` | MonoBehaviour | 관리자 객체 | 2 — `CardArtCatalog`, `CharacterAsset[]` |
| `BattleUnitsView` | MonoBehaviour | 유닛 행들의 공통 부모 | 3 — 유닛 프리팹, 아군 행, 적 행 |
| `BattlePilesView` | MonoBehaviour | 파일 3개의 공통 부모 | 3 — 드로우·버림·전체 덱 |
| ↳ | | | 세션 시작 시 `Bind`(내용 제공자), 매 갱신마다 `Refresh`(개수), 선택 중 `SetInputEnabled` |
| `BattleHudView` | MonoBehaviour | HUD 루트 | 5 — 운명력·메시지 텍스트, 턴 버튼·라벨, 리셋 버튼 |
| `CardSelectionController` | 기존 | 그대로 | +2 — 클릭 캐처 둘 |
| `BattleScreenController` | 기존 | 그대로 | 7 — 위젯 3 + 협력자 4 |

**뷰는 담당 서브트리에 붙인다.** 자기가 조작하는 UI 옆에 있어 참조 경로가 짧고 인스펙터에서
관계가 눈에 보인다. `BattlePresenter`는 조작할 UI가 없으므로 관리자 객체에 둔다.

**협력자 참조는 인스펙터로 할당한다.** `FindObjectOfType`은 규칙 3이 금지하고, 규칙 2가 인스펙터
할당을 요구한다. 컨트롤러의 7개 중 4개가 협력자 참조인 것은 분해의 필연적 대가이며, 위젯 참조
(`_hand`·`_rail`·`_selection`)와는 성격이 다르다.

### 4.3 `BattlePresenter`는 세션도 SO 타입도 모른다

이름은 세션에서, 색은 `CharacterAsset`에서 온다. 둘을 직접 참조하면 다시 얽히므로 이름 조회만
델리게이트로 받는다.

```csharp
public sealed class BattlePresenter : MonoBehaviour
{
    [SerializeField] private CardArtCatalog _cardArt;
    [SerializeField] private CharacterAsset[] _party;

    /// <summary>이름 조회를 주입받아 세션 타입을 모르게 한다.</summary>
    public void Initialize(Func<string, string> ownerName);

    public CardPresentation For(OwnedCard card);
    public CardPresentation For(ExecutionCardInstance card);
}
```

이 형태라야 EditMode 테스트가 가짜 델리게이트로 소유자 분기(적 / 파티 공용 / 개별 캐릭터)를 전부
돌릴 수 있다. **헤드리스 테스트는 붙지 않는다** — `CardPresentation`·`Sprite`·`Color`가
`FateWeaver.Unity`에 있고 헤드리스 프로젝트는 그 어셈블리를 컴파일하지 않는다.

### 4.4 클릭 캐처는 `CardSelectionController`로 간다

`_emptyClickCatcher`·`_dimClickCatcher`는 둘 다 `OnEmptyClicked` → 선택 취소로만 쓰인다. HUD가
아니라 선택 UX이므로 선택을 소유한 기존 컴포넌트가 갖는다. 새 컴포넌트를 만들지 않는다.

### 4.5 아트 리소스는 추상화하지 않는다

카드 아트가 실제 리소스로 바뀔 예정이지만 **지금 추상화하지 않는다.** 근거 둘:

**교체 비용이 작다.** `CardArtCatalog`를 구체 타입으로 아는 프로덕션 코드는 3줄이고, 분해 후에는
`BattlePresenter`와 `BattleSceneBuilder` 두 파일에 모인다. 그리고 **차단막이 이미 있다** —
`CardPresentation`이 `Func<string, Sprite>`를 받으므로 그 아래(`CardPresentation`·`CardView`·
`RailCardView`)는 출처 타입을 모른다.

**추상화하면 틀린 이음매를 고정한다.** 아트 축이 둘인데 타입도 소비자도 다르다.

| | 모양 | 소비자 |
|---|---|---|
| 카드 아트 | `id → Sprite` (정지 이미지) | `CardPresentation` → `CardView`·`RailCardView` |
| 캐릭터 아트 | 스프라이트 시트 애니메이션 | `UnitView` |

`CardArtSource { abstract Sprite ArtFor(id) }` 같은 추상 타입은 "Sprite를 반환한다"를 못 박는데,
캐릭터 애니메이션이 정확히 그 가정을 깬다. 실제 리소스의 형태가 확정된 뒤에 이음매를 정한다.

### 4.6 `BattleUnitsView`는 캐릭터 아트의 미래 진입점이다

이것이 유닛 뷰를 독립 컴포넌트로 두는 가장 강한 근거다. 캐릭터 애니메이션이 들어올 자리는
`UnitView.Bind(displayName, portraitTint)`와 그 `Image _portrait`이고, 지금 `Bind`를 부르는 곳은
`SpawnUnits` 한 곳이다. 분해 후 **`BattleUnitsView`가 유일한 호출자**가 되므로, 애니메이션 도입 시
컨트롤러·HUD·파일 뷰는 건드리지 않는다.

## 5. 데이터 흐름

```
ContentBootstrap ──→ GameContent ──┐
                                   ▼
                        BattleScreenController
                          · 세션 소유 (DeckCombatSession)
                          · 입력 핸들러 (세션을 변경하는 유일한 지점)
                          · 조립
                          │
        ┌─────────────────┼──────────────────┬─────────────────┐
        ▼                 ▼                  ▼                 ▼
  BattlePresenter   BattleUnitsView   BattlePilesView   BattleHudView
  (읽지 않음)        (스냅샷 읽기)      (스냅샷 읽기)      (스냅샷 읽기)
        │
        └──→ CardPresentation ──→ _hand · _rail (컨트롤러가 직접 갱신)
```

컨트롤러의 `RefreshAll`은 위임만 한다:

```csharp
private void RefreshAll()
{
    _hand.SetCards(_session.Hand.Select(_presenter.For).ToList(), OnHandClicked, OnHandHovered);
    _rail.SetCards(_session.CurrentOrder.Select(_presenter.For).ToList(), OnZoneClicked);
    _units.Refresh(_session.State);
    _piles.Refresh(_session);
    _hud.Refresh(_session);
    RefreshSelections();
}

private void RefreshSelections()
{
    bool active = _selection.SelectionActive;
    _piles.SetInputEnabled(!active);
    _hud.SetInputEnabled(!active, turnEnabled: !active && !_session.IsComplete);
}
```

`RefreshSelections`가 파일 뷰와 HUD 뷰 **양쪽**을 건드리는 것은 의도적이다. "선택 중에는 다른 입력을
막는다"는 한 규칙이 두 표면에 걸치며, 그 규칙의 소유자는 선택 상태를 아는 컨트롤러다. 각 뷰는
"입력을 켜라/꺼라"만 알고 이유는 모른다.

파일 뷰는 **내용 제공과 개수 갱신이 별개다.** 세션 시작 시 `Bind`로 지연 평가 제공자를 한 번 꽂고
(`() => Presentations(_session.DrawPile)`), 이후 `Refresh`는 개수만 갱신한다. 지금 `BindPiles`가
하는 일을 그대로 옮긴다.

## 6. 오류 처리

분해가 오류 처리를 바꾸지 않는다. 콘텐츠 로드 실패는 지금처럼 `BattleScreenController.StartSession`이
잡아 화면과 콘솔에 모든 이유를 표시하고 멈춘다(계획 3b에서 폴백을 제거했다). 협력자 참조가 비면
컨트롤러가 시작 시점에 검사해 같은 경로로 보고한다 — 인스펙터 배선 누락이 조용한 `NullReference`가
되지 않게 한다.

## 7. 검증 방향

- **Unity EditMode:** `BattlePresenter` 단위 테스트 신설(소유자 분기 3종, 아트 없음 경로),
  기존 `CardPresentationTests`·`CardSelectionControllerTests` 유지
- **씬 재생성:** `BattleSceneBuilder`가 새 컴포넌트를 만들고 배선하도록 갱신한 뒤 재생성하고,
  배치 EditMode 전체를 돌린다. 규칙 17 개정으로 이 저작은 AI가 직접 한다
- **Play:** 조작감은 눈으로 봐야 하므로 사용자가 확인한다 — 손패 클릭 → 배치, 개입 카드 대상 선택,
  턴 실행, 선택 취소(빈 곳 클릭)

## 8. 열린 항목

- **입력 핸들러 분리.** §4.1대로 P2 이후로 미룬다.
- **`_hand`·`_rail`을 감싸는 다섯째 컴포넌트.** 검토했으나 `SetCards`를 전달하기만 해서 결합은
  그대로고 간접층만 는다. 하지 않는다.
- **아트 이음매.** §4.5대로 실제 리소스 형태가 확정된 뒤 정한다.
- **`BattleScreenController`의 최종 크기.** 약 190줄로 예상하지만 실제 값은 구현 후 기록한다.
