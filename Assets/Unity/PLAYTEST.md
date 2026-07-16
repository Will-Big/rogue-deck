# Fate Weaver Unity Playtest (uGUI)

## 최초 1회 세팅

1. `Window ▸ TextMeshPro ▸ Import TMP Essential Resources`.
2. `Fate Weaver ▸ Create Korean TMP Font` — `Resources/Fonts/KoreanTMP.asset` 생성.
   - 소스 폰트는 리포에 커밋된 **Pretendard**(`Assets/Unity/Fonts/Pretendard-Regular.ttf`, OFL)라
     OS 상관없이(macOS/Windows/Linux) 동작한다. 생성 애셋은 씬이 참조하는 guid에 자동으로 핀되어 별도 배선이 필요 없다.
   - 실패 시 수동 대체: `Window ▸ TextMeshPro ▸ Font Asset Creator`에서 위 Pretendard TTF를 Source로,
     Atlas Population Mode = **Dynamic**으로 생성해 `Assets/Unity/Resources/Fonts/KoreanTMP.asset`로 저장.
3. 필요 시 `Fate Weaver ▸ Seed Starter Card Assets`, `Fate Weaver ▸ Seed Enemy Card Assets`,
   `Fate Weaver ▸ Generate Cards from SO`를 실행해 카드 SO와 생성 코드를 갱신한다.

> `Resources/Fonts/`(생성된 동적 아틀라스)만 gitignore 대상이다. Pretendard TTF는 커밋되어 있으니
> 머신마다 2번(과 필요 시 3번)만 다시 실행하면 된다.

## 실행

1. `Assets/Scenes/FateWeaverPlaytest.unity`를 열고 Play.
   - 간수 잠금 적 테스트는 `Assets/Scenes/FateWeaverWardenPlaytest.unity`를 연다.
2. 손패의 실행 카드를 클릭하면 운명력을 지불하고 미래 영역에 직접 배치된다.
3. 손패의 개입 카드를 클릭한 뒤 미래 영역의 대상 카드를 선택하면 실행 순서 변경/교환 같은 개입이 적용된다.
4. `턴 실행` → `다음 턴`으로 진행(HP·상태 이월). 승패가 나면 종료.

### 전투 화면 (시각 개편 1–2단계)

1. `Fate Weaver ▸ Build Battle Scene`으로 `Assets/Scenes/FateWeaverBattle.unity`를 생성(재실행 시 덮어씀)하고 Play.
2. 구도: 유닛 무대(유닛별 HP 바) / 스크롤 실행 레일(미니 카드, 호버 시 전체 카드) / 곡선 손패 /
   덱 버튼 3종(좌하 뽑을 덱 · 우하 버린 덱 · 우상 전체 덱) / 좌측 운명력 / 우측 턴 버튼.
3. 카드 입력은 호버 = 확대 보기, 첫 클릭 = 선택이다. 실행 카드는 레일을 다시 클릭해 배치하고,
   단일 개입은 레일 대상을 클릭하며, 교환은 서로 다른 대상 2개 선택 후 우하단 `확인`을 누른다.
   직접 아군 대상 카드는 살아 있는 파티원을 클릭한다. 빈 곳 또는 딤 클릭은 비용 없이 취소한다.
   구현 계획: `docs/superpowers/plans/2026-07-10-battle-screen-skeleton.md`.

### 통합 대상 선택 체크리스트

1. 대상 없는 실행 카드는 마우스를 따라가고 실행 순서 영역 클릭으로 배치된다.
2. 선택 방어는 손패 카드 중심에서 아군까지 화살표가 표시되며 확인 버튼 없이 즉시 배치된다.
3. 앞당김/미룸은 같은 화살표 규칙으로 실행 순서 카드 하나를 즉시 선택한다.
4. 자리 교환은 화살표 시작점이 손패 카드에 고정되고, 선택 가능한 대상은 황금색 테두리,
   선택한 두 대상은 푸른색 테두리로 표시된 뒤 확인 버튼이 나타난다. 번호 배지는 표시되지 않는다.
5. 빈 영역 취소는 카드와 운명력을 소비하지 않고 모든 선택 표현을 지운다.

## 범위 / 검증

- 카드 위젯은 `CardView`(프리팹) + `CardPresentation`(뷰모델) + `PlaytestCardArt`/`PlaytestKoreanText`(룩업).
- 덱 전투 진행 로직은 `DeckCombatSession`(순수 C#)이며 헤드리스 테스트로 검증된다.
- 컨트롤러/프리팹/에디터 빌더는 헤드리스 컴파일 대상이 아니므로 Unity Play에서만 검증된다.
- `PlaytestCardArt.ResolveArtName` / `PlaytestKoreanText.CardName`은 `FateWeaver.Tests.UnityEditMode`
  EditMode 테스트로 가드된다(Unity Test Runner에서 실행).
- 카드 설명은 `DescriptionComposer` + `KoreanDescriptionVocabulary`(순수 C#)가 카드의 효과 데이터에서
  조립하며, `FateWeaver.Tests.EditMode`의 `DescriptionComposerTests`(헤드리스)로 가드된다.
