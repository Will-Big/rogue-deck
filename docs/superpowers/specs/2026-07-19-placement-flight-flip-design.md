# 배치 비행 카드 플립 전환 설계

날짜: 2026-07-19
상태: 승인됨 (사용자 확인)
선행 작업: `docs/superpowers/plans/2026-07-19-card-outline-and-curved-placement-flight.md` (두 구간 Bézier 배치 비행)

## 문제

배치 비행이 끝나는 순간 전체 텍스트 카드(CardView 복제)가 파괴되고 레일 미니 카드(RailCardView)로
갱신되면서, 텍스트 카드 → 일러스트 카드가 한 프레임에 스왑되어 어색하다.

## 목표

착지 직전 안착 구간(경로 `curveSplit` 이후, 9시→12시로 풀리는 구간)에서 비행 카드가 Y축으로
한 바퀴의 절반(edge-on 두 번)을 도는 카드 플립을 수행하고, 90° 순간에 미니 카드 면으로 교체되어
착지 시에는 이미 레일 미니 카드 모습으로 안착한다. 교체 순간은 카드가 모서리만 보이는 상태라
시각적으로 드러나지 않는다.

## 설계

### 1. 미니 면(face) 준비 — `ExecutionRailView.StartPlacementFlight`

- `_cardPrefab`(RailCardView 프리팹)을 비행 RectTransform의 자식으로 `Instantiate` 한다.
  런타임 프리팹 인스턴스화는 기존 `SetCards`의 패턴과 동일하며 즉석 `new GameObject`가 아니다.
- `_placementPreviewCard.Value` 데이터로 `Bind(data, null, null)` 하고 `SetInteractable(false)`,
  모든 `Graphic.raycastTarget = false`로 둔다.
- 앵커 stretch-fill로 비행 카드 전체 Rect를 덮는다 (카드 비율 0.714 vs 미니 0.727 — 허용 오차).
- 시작 시 비활성(`SetActive(false)`).

### 2. 플립 수학 — `PlacementFlightPath.FlipAngle(float settleT)` (순수 함수)

- 전반부 `settleT < 0.5`: 반환 `settleT * 180f` (0°→90°). 앞면(텍스트 카드)이 모서리까지 돌아간다.
- 후반부 `settleT >= 0.5`: 반환 `settleT * 180f - 180f` (-90°→0°). 뒷면(미니 카드)이 펼쳐진다.
- 90° 지점에서 +90°→-90° 점프는 양쪽 모두 edge-on이라 보이지 않는다.
- 이 정의는 거울상(mirror) 문제가 없고 `settleT = 1`에서 정확히 0°로 끝나므로, 기존
  "정확한 목표 자세 스냅" `AppendCallback`과 테스트 단언이 그대로 유지된다.

### 3. 통합 — 진행률 tween 콜백

- 기존 tween 콜백에서 안착 구간일 때 `settleT`를 계산해
  `flight.localRotation = Quaternion.Euler(0f, PlacementFlightPath.FlipAngle(settleT), sample.AngleDegrees)`
  로 Z(접선) 회전과 Y 플립을 합성한다. 첫 구간은 기존과 동일하게 Z 회전만 적용한다.
- `settleT >= 0.5`가 처음 되는 순간 미니 면을 `SetActive(true)` 한다. 미니 면은 불투명한 전체
  프레임이라 뒤쪽의 텍스트 카드를 완전히 가리므로 앞면 그래픽을 끌 필요가 없다.

### 4. 수명 관리

- 미니 면은 비행 카드의 자식이므로 `ClearPlacementFlight`(정상 착지·중도 취소 공통)가 비행 카드를
  파괴할 때 함께 정리된다. 별도 참조 보관·해제 코드가 필요 없다.

## 테스트

- `PlacementFlightPathTests`: `FlipAngle` 경계값(0, 0.5 직전/직후, 1) 순수 검증.
- `ExecutionRailInputTests`: 안착 구간 90° 이전에는 미니 면(RailCardView 자식) 비활성,
  이후에는 활성 + 비행 카드 Y 회전이 (-90°, 0°] 범위, 착지 시 기존 스냅 단언 유지.
- 각 production 변경 전 대응 테스트를 추가하고 의도한 RED를 확인한다.

## 변경 파일

- `Assets/Unity/PlacementFlightPath.cs` — `FlipAngle` 추가
- `Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs` — 플립 각도 테스트
- `Assets/Unity/ExecutionRailView.cs` — 미니 면 생성·활성화, 회전 합성
- `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs` — 플립 통합 단언
- `Assets/Unity/PLAYTEST.md` — 수동 확인 절차 갱신

Core·프리팹·씬 변경 없음. 새 튜닝값 없음 (플립 구간은 기존 `_placementFlightCurveSplit`에 종속).

## 검토한 대안

- **안착 구간 크로스페이드**: 실루엣 알파 상승 + 비행 카드 알파 하강. 가장 단순하지만 사용자가
  카드게임다운 플립 연출을 선택했다.
- **착지 후 크로스페이드**: 연출 시간이 늘어나고 착지의 타격감이 약해져 제외.
- **자식 Y=180° 오프셋 방식 플립**: 0→180° 연속 회전 + 뒷면 미러 보정. 착지 스냅 회전 보정이
  추가로 필요해 두 반쪽 플립(0→90, -90→0) 방식을 채택했다.
