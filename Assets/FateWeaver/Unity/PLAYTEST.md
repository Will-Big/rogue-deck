# Fate Weaver Unity Playtest (uGUI)

## 최초 1회 세팅

1. `Window ▸ TextMeshPro ▸ Import TMP Essential Resources`.
2. `Fate Weaver ▸ Create Korean TMP Font` — `Resources/Fonts/KoreanTMP.asset` 생성.
   - 소스 폰트는 리포에 커밋된 **Pretendard**(`Assets/FateWeaver/Unity/Fonts/Pretendard-Regular.ttf`, OFL)라
     OS 상관없이(macOS/Windows/Linux) 동작한다. 생성 애셋은 씬이 참조하는 guid에 자동으로 핀되어 별도 배선이 필요 없다.
   - 실패 시 수동 대체: `Window ▸ TextMeshPro ▸ Font Asset Creator`에서 위 Pretendard TTF를 Source로,
     Atlas Population Mode = **Dynamic**으로 생성해 `Assets/FateWeaver/Unity/Resources/Fonts/KoreanTMP.asset`로 저장.
3. `Fate Weaver ▸ Build Playtest Scene (uGUI)` — Canvas/CardView 프리팹/컨트롤러를 생성·연결.

> `Resources/Fonts/`(생성된 동적 아틀라스)만 gitignore 대상이다. Pretendard TTF는 커밋되어 있으니
> 머신마다 2번(과 필요 시 3번)만 다시 실행하면 된다.

## 실행

1. `Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity`를 열고 Play.
   - 간수 잠금 적 테스트는 `Assets/FateWeaver/Scenes/FateWeaverWardenPlaytest.unity`를 연다.
2. 상단 버튼으로 시나리오 선택.
3. 미래 영역의 카드(이미지 + 이름/실행 순서 + 하단 설명)를 눌러 주/보조 대상 선택.
4. 개입 액션(실행 순서 ±2 / 교환 / 고정) 적용.
5. `턴 실행` → `다음 턴`으로 진행(HP·상태 이월). 승패가 나거나 마지막 턴이면 종료.

## 범위 / 검증

- 카드 위젯은 `CardView`(프리팹) + `CardPresentation`(뷰모델) + `PlaytestCardArt`/`PlaytestKoreanText`(룩업).
- 멀티턴 진행 로직은 `MultiTurnPlaytestSession`(순수 C#)이며 헤드리스 테스트로 검증된다.
- 컨트롤러/프리팹/에디터 빌더는 헤드리스 컴파일 대상이 아니므로 Unity Play에서만 검증된다.
- `PlaytestCardArt.ResolveArtName` / `PlaytestKoreanText.CardName`은 `FateWeaver.Tests.UnityEditMode`
  EditMode 테스트로 가드된다(Unity Test Runner에서 실행).
- 카드 설명은 `DescriptionComposer` + `KoreanDescriptionVocabulary`(순수 C#)가 카드의 효과 데이터에서
  조립하며, `FateWeaver.Tests.EditMode`의 `DescriptionComposerTests`(헤드리스)로 가드된다.
