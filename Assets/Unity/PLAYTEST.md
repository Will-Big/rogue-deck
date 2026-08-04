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
4. 22장 시작 카드 후보 풀은 병합 후 메인 Unity 체크아웃에서
   `Fate Weaver ▸ Seed Starter Pool Assets`를 한 번 실행해 생성한다.
   - `Assets/Unity/CardSO/Player/StarterPool/`에는 없는 카드만 생성하며, 재실행해도 기존 카드의 규칙,
     등급, 태그, 아트, 설명을 덮어쓰지 않는다.
   - `Assets/Unity/CardSO/Player/StarterPool.asset`은 후보 22장의 참조만 담는다.
     기존 `StarterDeck.asset`과 시작 덱 카드 장수는 바꾸지 않는다.
   - 이후 `Generate Cards from SO`를 실행하면 유효한 풀은 헤드리스용 `StarterPool()` 스냅샷으로 함께
     내보낸다. 풀 에셋이 아직 없으면 기존 `StarterDeck()`만 생성하고 경고를 남긴다.

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
3. 카드 입력은 호버 = 확대 보기, 첫 클릭 = 선택이다. 실행 카드는 손패 호버로 보이는 레일 실루엣을
   클릭해 배치하고, 단일 개입은 레일 대상을 클릭하며, 교환은 서로 다른 대상 2개 선택 후 우하단 `확인`을
   누른다. 빈 곳 또는 딤 클릭은 비용 없이 취소한다.
   구현 계획: `docs/superpowers/plans/2026-07-10-battle-screen-skeleton.md`.

### 통합 대상 선택 체크리스트

1. 모든 실행 카드는 호버만으로 상태 효과가 반영된 자동 위치에 정적인 알파 0.5 푸른 실루엣을 표시한다.
   호버 종료 시 미선택 실루엣은 사라진다. 실루엣이 1.0↔1.06 크기로 부드럽게 반복된다.
   손패 카드를 클릭하면 회전 없는 호버 자세로 고정되고 카드 내부 색은 유지된 채 프레임 외곽선만
   푸르게 표시된다. 실행 카드 실루엣을 클릭하면 큰 카드가 하단 손패에서 출발해 숫자 3의 윗고리처럼
   12시→2시→10시→9시 방향으로 감긴다. 9시 이후에는 실루엣 아래로 내려가지 않고 12시 정방향으로
   풀리며 축소·안착한 뒤 실제 레일 카드로 전환된다.
   안착 구간에서 카드가 Y축으로 한 번 뒤집히며, 모서리만 보이는 90° 순간 이후에는
   금색 테두리(임시 뒷면 프레임) 안에 해당 카드의 일러스트가 담긴 카드 뒷면이 펼쳐져
   착지한 뒤 실제 레일 카드로 전환된다.
   연속 클릭해도 적용과 갱신은 한 번만 일어나야 한다.
2. 실행 카드 배치 대기 중 기존 레일 카드에 호버하면 전체 카드 상세보기가 정상적으로 나타난다.
3. 운명력이 부족한 실행 카드는 호버 위치는 보이지만 손패 클릭으로 배치 대기 상태에 들어가지 않는다.
4. 조작 카드의 단일·다중 레일 대상 선택, 재선택 취소와 확인 버튼 동작은 기존과 같다.
5. 빈 영역 취소는 카드와 운명력을 소비하지 않고 실행 실루엣 또는 조작 선택 표현을 지운다.

### 프리미티브 카드 프레임 체크리스트

1. `Enemy → Ally → Enemy` 순서의 효과는 적군 한 줄, 아군 한 줄로 전역 그룹화된다.
2. 설명 블록의 진영 심볼은 양쪽 모두 `◆`이며, 적군은 붉은색·아군은 푸른색으로만 구분된다.
3. 대상 블록에서 아군의 앞은 오른쪽, 적군의 앞은 왼쪽을 향하고 `All`은 다른 위치 glyph와 같은 폭이다.
4. 실행 카드의 대상 0/1/2개 상태는 모두 한 가로줄을 사용한다. 무대상은 `∅`, 한 진영은 가운데,
   두 진영은 아군 왼쪽·적군 오른쪽 순서로 표시된다.
5. 개입 카드는 대상 블록과 `∅`가 없고, 그 영역과 간격까지 확장 설명 블록이 사용한다.
6. 실행·개입 혼합 손패를 4:3, 16:10, 16:9, 21:9에서 확인한다.
7. 크기 변경 뒤에도 가장 나중에 호버하거나 든 카드가 맨 위에 남는다.

## 범위 / 검증

- 카드 위젯은 `CardView`(프리팹) + `CardPresentation`(뷰모델) + `PlaytestCardArt`/`PlaytestKoreanText`(룩업).
- 덱 전투 진행 로직은 `DeckCombatSession`(순수 C#)이며 헤드리스 테스트로 검증된다.
- 컨트롤러/프리팹/에디터 빌더는 헤드리스 컴파일 대상이 아니므로 Unity Play에서만 검증된다.
- `PlaytestCardArt.ResolveArtName` / `PlaytestKoreanText.CardName`은 `FateWeaver.Tests.UnityEditMode`
  EditMode 테스트로 가드된다(Unity Test Runner에서 실행).
- 카드 설명은 `DescriptionComposer` + `KoreanDescriptionVocabulary`(순수 C#)가 카드의 효과 데이터에서
  조립하며, `FateWeaver.Tests.EditMode`의 `DescriptionComposerTests`(헤드리스)로 가드된다.
