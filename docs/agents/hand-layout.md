# 손패 레이아웃 조절

전투 씬의 `HandFan`에서 `Hand Fan Layout View` 컴포넌트를 조절한다.
Y 방향 반전은 제공하지 않는다. 유료 에셋은 추가하지 않는다.

## 인스펙터

| 항목 | 의미 |
|---|---|
| Radius | 원호 반지름. 같은 각도에서 커질수록 카드 사이가 벌어진다. |
| Total Angle | 최대 펼침 각도. 0~180도. |
| Rotate With Arc | 카드가 원호 방향으로 기울어진다. 끄면 카드는 수직을 유지한다. |
| Invert Rotation | 카드 기울기 방향만 반대로 한다. 위치는 그대로다. |
| Use Sibling Order | 카드 목록을 연결할 때의 Hierarchy 순서로 배치한다. 끄면 전달된 카드 목록 순서다. 호버로 맨 앞으로 온 카드는 순서 변경으로 취급하지 않는다. |
| Per Item Extra Angle | 카드 사이 간격마다 추가할 각도. 최종 펼침은 0~180도로 제한한다. |
| Adaptive Spread | 카드가 적으면 펼침 각도를 줄인다. |
| Cards For Full Spread | 최대 펼침에 도달하는 장수. 최소 2장. |
| Min Angle | 장수별 펼침의 시작 각도. Total Angle보다 크게 적용되지 않는다. 0·1장은 항상 중앙에 둔다. |
| Use Bottom Baseline | 회전과 배지 돌출을 포함한 손패의 가장 아래를 기준으로 정렬한다. |
| Baseline Padding | HandFan 영역의 아래 끝에서 띄울 여백. 화면 전체 아래 끝이 아닌 HandFan RectTransform 기준이다. |
| Safe Margins | 손패 영역의 가로·세로 총 안전 여백. 좁은 영역에서는 공통 Content를 균일 축소한다. |
| Control Card Scale | 개별 카드 배율을 제어한다. 끄면 연결 시의 프리팹 배율로 복원한다. |
| Card Scale | 개별 카드의 절대 로컬 배율. 영역에 맞추는 Content 배율과 별개다. |
| Smooth | 실행 중 위치·회전·크기를 DOTween으로 전환한다. 첫 배치와 화면 리사이즈는 즉시 맞춘다. |
| Smooth Speed | 커질수록 빠르다. 전환 시간은 1 / speed초다. 게임 일시정지 중에도 동작한다. |

근거: [`HandFanLayoutView`](../../Assets/Unity/Scripts/Cards/HandFanLayoutView.cs),
[`ArcHandLayout.PoseFor`](../../Assets/Core/Simulation/Presentation/ArcHandLayout.cs),
[`HandCardHoverEffect.MoveTo`](../../Assets/Unity/Scripts/Cards/HandCardHoverEffect.cs).

## 조절 순서

Play 중 Card Scale로 크기를 정하고 Radius·Total Angle로 너비와 기울기를 맞춘다.
그다음 Baseline Padding으로 하단 위치를 조절한다. Play 중 수정한 값은 Unity가 Play 종료 시
되돌리므로 원하는 값을 기록해 두었다가 편집 모드에서 저장한다.

마우스를 올린 카드, 선택해 붙잡은 카드, 적은 손패와 많은 손패, 창 크기를 줄인 상태를 각각
확인한다. 호버 크기는 기본 카드의 1.35배이고 위로 46 로컬 단위 이동한다.
근거: [`HandCardHoverEffect`](../../Assets/Unity/Scripts/Cards/HandCardHoverEffect.cs).

## 유지보수 경계

- `HandFanView`: 카드 표시와 선택·배치 호출 연결.
- `HandFanLayoutView`: 인스펙터 설정에 따른 손패 배치 적용.
- `ArcHandLayout`: Unity를 참조하지 않는 원호 위치·회전 계산.
- `HandCardHoverEffect`: 기준 배치 위에 호버·선택 표시를 적용하고 DOTween 전환을 소유.

배치 계산은 헤드리스 테스트, Unity 연결은 EditMode·PlayMode 테스트로 검증한다.
시각적 크기와 배치의 최종 판단은 사용자가 Play에서 한다.
