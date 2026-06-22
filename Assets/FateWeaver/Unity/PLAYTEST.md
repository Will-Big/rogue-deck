# Fate Weaver Unity Playtest (uGUI)

## 최초 1회 세팅

1. `Window ▸ TextMeshPro ▸ Import TMP Essential Resources`.
2. `Fate Weaver ▸ Create Korean TMP Font` — `Resources/Fonts/KoreanTMP.asset` 생성.
   - 실패 시 수동 대체: `Window ▸ TextMeshPro ▸ Font Asset Creator`에서 `C:/Windows/Fonts/malgun.ttf`를
     Source로, Atlas Population Mode = **Dynamic**으로 생성해 `Assets/FateWeaver/Unity/Resources/Fonts/KoreanTMP.asset`로 저장.
3. `Fate Weaver ▸ Build Playtest Scene (uGUI)` — Canvas/CardView 프리팹/컨트롤러를 생성·연결.

> `Resources/Fonts/`는 gitignore 대상(생성물 + 시스템 폰트). 머신마다 2~3번을 다시 실행한다.

## 실행

1. `Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity`를 열고 Play.
2. 상단 버튼으로 시나리오 선택.
3. 미래 영역의 카드(이미지 + 이름/주도력 + 하단 설명)를 눌러 주/보조 대상 선택.
4. 운명 액션(주도력 ±2 / 교환 / 고정) 적용.
5. `턴 실행` → `다음 턴`으로 진행(HP·상태 이월). 승패가 나거나 마지막 턴이면 종료.

## 범위 / 검증

- 카드 위젯은 `CardView`(프리팹) + `CardPresentation`(뷰모델) + `PlaytestCardArt`/`PlaytestKoreanText`(룩업).
- 멀티턴 진행 로직은 `MultiTurnPlaytestSession`(순수 C#)이며 헤드리스 테스트로 검증된다.
- 컨트롤러/프리팹/에디터 빌더는 헤드리스 컴파일 대상이 아니므로 Unity Play에서만 검증된다.
- `PlaytestCardArt.ResolveArtName` / `PlaytestKoreanText.CardDescription`는 `FateWeaver.Tests.UnityEditMode`
  EditMode 테스트로 가드된다(Unity Test Runner에서 실행).
