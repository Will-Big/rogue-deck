# Fate Weaver Unity Playtest (uGUI)

## 준비와 콘텐츠 저작

저장된 [`Assets/Scenes/FateWeaverBattle.unity`](../Scenes/FateWeaverBattle.unity)를 직접 연다.
전투 화면 생성 메뉴를 실행하는 단계는 없다. 씬·프리팹은 저장된 에셋을 직접 수정하고 참조를 배선한다
([AGENTS.md 규칙 17](../../AGENTS.md)).

카드·콘텐츠는 AI 세션에서 `Assets/StreamingAssets/Content/`의 JSON을 직접 수정한다.
인스펙터 카드 저작과 별도 카드 편집 도구는 사용하지 않는다. 변경 후 `Tools/verify.sh`로 검증한다
([콘텐츠 저작 절차](../../docs/agents/content-authoring.md)).

### 폰트는 문자 구성이 바뀔 때만 재생성한다

[`KoreanTMP.asset`](Fonts/KoreanTMP.asset)과 원본
[`Pretendard-Regular.ttf`](Fonts/Pretendard-Regular.ttf)는 커밋된 파일이다. 새 클론에서 폰트 생성은
필요하지 않으며 생성 아틀라스도 gitignore 대상이 아니다([.gitignore 「Korean TMP font」](../../.gitignore)).

게임에 표시하는 문자 구성이 바뀌어 폰트 갱신이 필요할 때만 `Fate Weaver ▸ Create Korean TMP Font`를
실행한다. 생성기는 `Assets/Unity/Fonts/KoreanTMP.asset`를 만들고 기존 씬 참조 GUID를 보존한다
([KoreanTmpFontCreator.cs](Editor/KoreanTmpFontCreator.cs)).

실패 시 수동 대체는 `Window ▸ TextMeshPro ▸ Font Asset Creator`에서 위 Pretendard TTF를 Source로,
Atlas Population Mode를 **Dynamic**으로 선택해 문자를 구운 뒤 **Static**으로 바꿔 같은 경로에 저장한다. 기존 GUID·씬 참조를 보존하고
추가한 문자가 화면에서 올바르게 나오는지 확인한다.

## 실행

1. `Assets/Scenes/FateWeaverBattle.unity`를 열고 Play.
2. 구도: 유닛 무대(유닛별 HP 바) / 스크롤 실행 레일(미니 카드, 호버 시 전체 카드) / 곡선 손패 /
   덱 버튼 3종(좌하 뽑을 덱 · 우하 버린 덱 · 우상 전체 덱) / 좌측 운명력 / 우측 턴 버튼.
3. 카드 입력은 호버 = 확대 보기, 첫 클릭 = 선택이다. 실행 카드는 손패 호버로 보이는 레일 실루엣을
   클릭해 배치하고, 단일 개입은 레일 대상을 클릭하며, 교환은 서로 다른 대상 2개 선택 후 우하단 `확인`을
   누른다. 빈 곳 또는 딤 클릭은 비용 없이 취소한다.

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

- 카드 위젯은 프리팹 `CardView`와 뷰모델 `CardPresentation`을 사용한다. 아트는 표현 자원 카탈로그로
  배선한다([CardPresentation.cs](Scripts/Cards/CardPresentation.cs), [콘텐츠 저작 경계](../../docs/agents/content-authoring.md)).
- 덱 전투 진행과 설명 조립은 순수 C# 헤드리스 테스트로 검증한다. 설명은 `DescriptionComposer`가
  효과 데이터에서 언어 중립 요청으로 조립하고 Unity 현지화 표가 문장으로 만든다
  ([DescriptionComposer.Requests.cs](../Core/Simulation/Descriptions/DescriptionComposer.Requests.cs),
  [현지화 저작](../../docs/agents/localization.md)).
- 컨트롤러·프리팹·씬 참조는 Unity 배치 EditMode로 검증하고, 조작감·레이아웃·연출은 Play로 확인한다
  ([배치 실행 절차](../../docs/agents/unity-batch-runs.md)).
- 상태 아이콘 경로는 Unity EditMode 테스트로 검증한다
  ([CardStatusIconSpritesTests.cs](../Tests/UnityEditMode/CardStatusIconSpritesTests.cs)).

## 전투 노드 한 사이클 (2026-09-15)

1. 고블린을 이기면 보상 패널이 뜨고 카드가 1~3장, 카드마다 소유 파티원 이름이 보인다.
2. 카드의 `선택`을 누르면 선택 버튼이 잠기고 `다음 전투`가 나타난다. `건너뛰기`도 같다.
3. `다음 전투`를 누르면 새 전투가 시작되고, `전체 덱`에 방금 고른 카드가 들어 있다.
4. 지면 패배 패널이 뜨고 `처음부터`로 처음 덱의 새 런이 시작된다.
5. 전투 중에 HUD의 `초기화`를 누르면 처음 덱의 새 런이 시작된다. 보상·패배 패널이 떠 있는 동안에는 패널이 화면을 덮어 `초기화`를 누를 수 없다(의도된 현재 동작).
6. 같은 런 시드에서 같은 순서로 플레이하면 첫 보상 후보가 매번 같다.
