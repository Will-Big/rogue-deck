# Fate Weaver — 카드 상태 그리드와 호버 툴팁 설계

- 작성일: 2026-08-03
- 문서 유형: `architecture`, `ux-design`
- 주 도메인: `card-status`, `card-tooltip`, `card-frame`
- 상태: `current` — 카드에 직접 붙은 상태 아이콘과 호버 설명의 권위 문서
- 선행 규칙:
  [프리미티브 카드 프레임과 구조화 설명](2026-07-31-primitive-card-frame-design.md),
  [카드 변형과 런타임 콘텐츠 로딩](2026-07-30-card-mutation-and-runtime-content-design.md)
- 관련 구현: `CardPresentation`, `CardView`, `ExecutionCardView.prefab`, `InterventionCardView.prefab`

## 1. 목적

카드 프레임의 현재 `LockIcon`은 잠금 전용 슬롯처럼 보이지만 실제 요구는 카드에 직접 붙은 상태를
0개 이상 표시하는 일반 상태 영역이다. 이 설계는 잠금 하나에 묶인 이름과 배치를 일반화하고, 상태가
여럿일 때 네 칸씩 아래로 늘어나는 그리드와 아이콘 호버 설명을 제공한다.

상태 규칙과 콘텐츠 원본은 별도 작업 흐름에서 ScriptableObject에서 JSON 및 중앙 카탈로그로 이동하고
있다. 카드 UI는 그 저장·관리 구조를 직접 참조하지 않고, 화면에 표시할 준비가 끝난 데이터만 받는다.
따라서 데이터 원본 전환과 이 프레임 작업은 서로 독립적으로 진행할 수 있다.

## 2. 범위

### 2.1 포함

- 실행·개입 카드의 일반 상태 아이콘 그리드
- 한 행 네 칸, 행 수 제한 없이 아래로 확장하는 배치
- 아이콘마다 제목과 설명을 보여주는 포인터 호버 툴팁
- 현재 잠금 상태를 일반 표시 데이터로 변환하는 기존 경로
- 카드 재바인딩과 비활성화 때 생성 아이콘과 툴팁 정리
- 프리팹 구조와 Unity EditMode 계약 검증

### 2.2 제외

- 새 상태 종류 또는 상태 규칙 추가
- JSON 스키마, 상태 카탈로그, 런·전투 상태 수명주기 변경
- 런 또는 전투 전체에 걸린 상태를 카드마다 중복 표시
- 피해·방어·비용 등 유효 수치가 변했을 때의 텍스트 색상 피드백
- 툴팁의 최종 장식, 애니메이션, 지연 표시, 화면 가장자리 회피

## 3. 표시 대상

상태 그리드에는 **해당 카드 인스턴스에 직접 붙은 상태만** 표시한다. 현재 구현에서 잠금은 첫 번째
표시 대상이다. 앞으로 카드에 직접 붙는 다른 상태가 추가되면 같은 목록에 항목을 더한다.

런 전체 또는 전투 전체의 상태가 카드 결과에 간접 영향을 주더라도 그 상태를 모든 카드에 아이콘으로
반복하지 않는다. 피해·방어·비용 같은 유효 텍스트가 원본과 달라졌다는 피드백은 향후 해당 텍스트
일부의 색을 바꾸는 별도 기능으로 다룬다.

## 4. 책임 경계

```text
카드·상태 규칙과 콘텐츠 원본
    |  SO 또는 JSON/중앙 카탈로그
    v
표현 어댑터
    |  표시할 아이콘, 제목, 설명을 확정
    v
CardPresentation
    `-- StatusIcons: IReadOnlyList<CardStatusPresentation>
            |-- Icon: Sprite
            |-- Title: string
            `-- Description: string
    |
    v
CardView
    |-- CardStatusGrid
    |     `-- CardStatusIconView × N
    `-- shared CardStatusTooltipView
```

`CardView`는 상태 키를 다시 해석하거나 레지스트리에서 메타데이터를 찾지 않는다. 상태 규칙과 콘텐츠를
아는 표현 어댑터가 `CardStatusPresentation`을 만들어 전달한다. 현재 잠금 경로도 같은 계약을 사용하며,
나중에 JSON·중앙 상태 카탈로그로 원본이 바뀌어도 어댑터만 변경한다.

`CardStatusPresentation`은 Unity 표현 경계의 값 객체다. 아이콘 `Sprite`, 비어 있지 않은 제목과 설명을
필수로 가진다. 잘못된 표시 데이터는 생성 또는 바인딩 시 조용히 건너뛰지 않고 즉시 실패하게 하여
프리팹·콘텐츠 누락을 개발 중에 발견한다.

잠금의 기본 표시 문구는 다음 의미를 전달한다.

```text
제목: 잠금
설명: 이 카드는 실행 순서를 변경할 수 없습니다.
```

## 5. 프리팹과 배치

실행 카드와 개입 카드 모두 다음 일반 구조를 사용한다.

```text
CardView root
  CardStatusGrid
    StatusIconTemplate (inactive)
      CardStatusIconView
        Image
```

`CardStatusGrid`에는 Unity 표준 `GridLayoutGroup`을 사용하고 별도 배치 컴포넌트를 만들지 않는다.

| 속성 | 값 |
|---|---|
| Cell Size | `26 × 26` |
| Spacing | `4 × 4` |
| Constraint | `Fixed Column Count` |
| Constraint Count | `4` |
| Start Corner | `Upper Left` |
| Start Axis | `Horizontal` |
| Child Alignment | `Upper Left` |

그리드 RectTransform은 현재 첫 행의 위쪽 변을 고정하고 pivot의 Y를 `1`로 둔다. Unity 표준
`ContentSizeFitter`의 Vertical Fit을 `Preferred Size`로 설정해 행 수에 맞춰 높이만 바꾼다. Horizontal
Fit은 사용하지 않으며 네 열의 폭은 고정한다.

현재 상태 영역의 첫 행 위치를 기준으로 삼는다. 첫 아이콘은 왼쪽 위에서 시작하고 마지막 행의 남는
아이콘을 가운데 정렬하지 않는다. 다섯 번째 아이콘부터 두 번째 행에 놓이며 새 행은 항상 아래쪽으로
늘어난다. 아이콘이 없으면 그리드 전체를 숨긴다.

템플릿은 생성 항목과 구분되는 비활성 직렬화 참조다. 자식 순서에 기대어 첫 자식을 템플릿으로 간주하지
않는다. 다시 바인딩할 때 이전에 생성한 아이콘을 모두 제거한 뒤 현재 목록을 다시 만든다.

## 6. 호버 툴팁

상태 아이콘의 `CardStatusIconView`는 `IPointerEnterHandler`와 `IPointerExitHandler`를 구현한다.
포인터 진입 시 공유 툴팁에 자신의 제목과 설명을 전달하고, 이탈 시 자신이 열었던 툴팁을 닫는다.

툴팁 패널은 카드 또는 상태 그리드의 자식으로 두지 않는다. 전투 Canvas의 기존 오버레이 계층에
`CardStatusTooltipView.prefab` 인스턴스를 한 개 두고 제목과 본문 `TMP_Text`를 직렬화한다. 이 구조는
카드 마스크와 그리드 클리핑에 툴팁이 잘리는 문제를 피하고, 아이콘마다 패널을 복제하지 않는다. 첫
버전은 포인터 화면 좌표에 일정한 오프셋을 더해 패널을 놓으며 화면 가장자리 회피는 후속 범위다.

프리팹 에셋인 `CardView`는 씬의 툴팁 인스턴스를 직접 참조할 수 없다. 배선은 다음처럼 명시적으로
전달한다.

```text
BattleScreenController (serialized CardStatusTooltipView)
    |-- HandFanView
    |-- ExecutionRailView
    `-- PileView
          |
          v
CardPrefabCatalog.Create(..., CardStatusTooltipView)
          |
          v
CardView -> generated CardStatusIconView
```

컨트롤러는 같은 툴팁 뷰를 카드를 생성하는 각 호스트에 초기화 시 한 번 전달하고, 호스트는
`CardPrefabCatalog.Create`를 통해 생성한 모든 전체 카드 뷰에 넘긴다. 손패, 실행 레일의 전체 카드
미리보기, 더미 팝업이 같은 패널을 공유한다. 연결이 필요한 대화형 카드에서 참조가 빠지면 생성 또는
바인딩 시 즉시 실패한다. 배치 이동용 복제처럼 모든 Graphic의 raycast가 꺼진 비대화형 뷰는 툴팁을
열지 않는다.

아이콘 `Image`의 raycast target은 켜서 호버 이벤트를 받는다. 툴팁 패널 자체의 그래픽은 raycast를
막지 않는다. 아이콘에는 클릭 동작을 추가하지 않으므로 기존 카드 선택·사용 입력은 유지한다.

다음 경우 공유 툴팁을 즉시 숨긴다.

- 포인터가 현재 아이콘을 벗어날 때
- 카드를 새 데이터로 다시 바인딩할 때
- 카드 또는 상태 아이콘이 비활성화될 때
- 툴팁을 연 아이콘이 재생성 과정에서 제거될 때

런타임 문자열 탐색이나 하드코딩된 경로는 사용하지 않는다. 아이콘 템플릿과 툴팁 내부 요소는
`[SerializeField] private` 참조로 프리팹에 저작하고, 씬 인스턴스에서 카드 프리팹으로 넘어가는 참조는
위 생성 경로의 인자로 전달한다. 전역 정적 서비스나 `FindObjectOfType`을 사용하지 않는다.

## 7. 검증

Unity EditMode 테스트는 최소한 다음 계약을 검증한다.

1. 실행·개입 카드 프리팹이 `CardStatusGrid`, 비활성 `StatusIconTemplate`, 필요한 직렬화 참조를 가진다.
2. 두 그리드 모두 네 열 고정, `26 × 26` 셀, `4 × 4` 간격, 왼쪽 위 시작, 위쪽 pivot과 세로
   `Preferred Size` 설정이다.
3. 상태 0·1·4·5개를 바인딩하면 각각 0·1·4·5개의 활성 아이콘이 생기고, 다섯 번째가 두 번째 행에
   놓인다.
4. 재바인딩은 이전 생성 아이콘을 남기지 않으며 상태 0개일 때 그리드를 숨긴다.
5. 각 아이콘은 전달받은 Sprite를 표시하고 호버 시 제목·설명을 공유 툴팁에 전달한다.
6. 포인터 이탈, 재바인딩, 비활성화 시 열린 툴팁이 남지 않는다.
7. 일반 이름과 목록을 사용하며 `LockIcon`, `_lockBadge` 같은 잠금 전용 구조가 남지 않는다.
8. 손패·실행 레일 미리보기·더미 팝업이 같은 툴팁 인스턴스를 명시적으로 전달받고 대화형 카드의 누락
   참조는 실패한다.

헤드리스 규칙 테스트는 이 작업에서 추가하지 않는다. 상태 규칙이나 결정론을 바꾸지 않고 Unity 표현
계약만 변경하기 때문이다.

## 8. 후속 작업

카드 변형과 런·전투 상태 중앙관리 작업이 원본값과 유효값의 표현 계약을 확정한 뒤, 피해·방어·비용 등
변경된 텍스트 span에만 색상 피드백을 추가한다. 이 피드백은 상태 아이콘으로 대체하지 않으며, 현재
작업에서는 구현하지 않는다.
