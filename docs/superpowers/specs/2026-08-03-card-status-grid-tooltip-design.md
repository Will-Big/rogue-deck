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

카드·상태 규칙과 콘텐츠의 유일한 저작 원본은 JSON으로 확정한다. 카드 UI는 JSON 파일이나 구체적인
로더를 직접 참조하지 않고, 부팅 시 JSON을 읽어 만든 중앙 카탈로그가 제공하는 순수 C# 표시 데이터를
받는다. Unity는 그 데이터의 아이콘 키만 Sprite로 해석한다. ScriptableObject 또는 코드 기본값을
콘텐츠 대체 원본으로 두지 않는다.

## 2. 범위

### 2.1 포함

- 실행·개입 카드의 일반 상태 아이콘 그리드
- 한 행 네 칸, 행 수 제한 없이 아래로 확장하는 배치
- 아이콘마다 제목과 설명을 보여주는 포인터 호버 툴팁
- JSON 중앙 카탈로그의 상태 표시 데이터를 Unity 표시 모델로 투영하는 경계
- 현재 잠금 상태를 일반 표시 데이터로 변환하는 경로
- 카드 재바인딩과 비활성화 때 생성 아이콘과 툴팁 정리
- 프리팹 구조와 Unity EditMode 계약 검증

### 2.2 제외

- 새 상태 종류 또는 상태 규칙 추가
- JSON 로더, 상태 규칙 레지스트리, 런·전투 상태 수명주기 자체의 구현
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
카드·상태 JSON
    |
    v
중앙 콘텐츠 카탈로그
    `-- StatusDisplayContent (순수 C#)
            |-- Key
            |-- DisplayName
            |-- Description
            `-- IconKey
    |
    v
CardStatusPresentationFactory                    Unity 경계
    |-- JSON 표시 문자열은 그대로 전달
    `-- StatusIconCatalog에서 IconKey -> Sprite 해석
    |
    v
CardPresentation
    `-- StatusIcons: IReadOnlyList<CardStatusPresentation>
            |-- Key: string
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

`CardView`는 상태 키를 다시 해석하거나 레지스트리에서 메타데이터를 찾지 않는다. JSON을 읽은 중앙
카탈로그가 순수 C# `StatusDisplayContent`를 제공하고, Unity의 `CardStatusPresentationFactory`가
`StatusIconCatalog`의 인스펙터 저작 매핑으로 Sprite를 결합한다. JSON에는 Unity 타입이나 에셋 경로를
넣지 않고 안정적인 `iconKey`만 둔다.

JSON 리팩터링이 이 브랜치에 들어오기 전 임시 SO, `CardStatusIcon` enum switch, 하드코딩 문자열
카탈로그를 만들지 않는다. UI와 프리팹은 `CardStatusPresentation`을 직접 바인딩하는 테스트로 먼저
완성할 수 있지만, 실제 게임의 잠금 표시 배선은 JSON 중앙 카탈로그가 합쳐진 뒤 같은 factory 입력에
연결한다. 따라서 합류 시 UI나 프리팹을 다시 고치지 않고 조립 지점만 연결한다.

중앙 JSON 모델의 구체적인 클래스명과 파일 배치는 진행 중인 콘텐츠 리팩터링이 소유한다. 다만 카드
상태 표시가 가능한 레코드는 결과 카탈로그에서 다음 의미를 제공해야 한다.

```json
{
  "key": "lock",
  "displayName": "잠금",
  "description": "이 카드는 실행 순서를 변경할 수 없습니다.",
  "iconKey": "lock"
}
```

기존 `StatusContentCatalog`를 확장하든 별도 투영 인터페이스를 제공하든 이 네 값을 JSON 한 원천에서
얻어야 한다. 같은 제목·설명·아이콘 키를 Unity 에셋이나 C# 상수에 중복 저작하지 않는다.

`CardStatusPresentation`은 Unity 표현 경계의 값 객체다. 안정적인 상태 키, 아이콘 `Sprite`, 비어 있지
않은 제목과 설명을 필수로 가진다. JSON 표시 데이터 누락이나 등록되지 않은 `iconKey`는 생성 또는
바인딩 시 조용히 건너뛰지 않고 즉시 실패하게 하여 콘텐츠 누락을 부팅 검증에서 발견한다.

잠금의 기본 표시 문구는 다음 의미를 전달한다.

```text
잠금
이 카드는 실행 순서를 변경할 수 없습니다.
```

`제목:`과 `설명:` 접두사는 화면에 출력하지 않는다. 제목과 설명은 각각 별도 `TMP_Text`에 바인딩한다.
어두운 툴팁 배경을 기준으로 제목은 따뜻한 금색 `#F2C14E`, 설명은 밝은 회색 `#E8EDF2`를 사용한다.
두 색은 `CardStatusTooltipView.prefab`에 저작하고 코드 상수로 두지 않는다.

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
`CardStatusTooltipView.prefab` 인스턴스를 한 개 두고 제목과 본문 `TMP_Text`를 직렬화한다. 제목과
본문에는 전달받은 내용만 넣고 필드명 접두사를 조립하지 않는다. 이 구조는
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
9. JSON 표시 데이터가 `CardStatusPresentation`으로 투영될 때 key·제목·설명이 보존되고 등록되지 않은
   `iconKey`는 실패한다. UI 코드에는 잠금 제목·설명 문자열이나 상태별 switch가 없다.
10. 툴팁은 `제목:`·`설명:` 접두사 없이 두 TMP 필드에 내용을 표시하며 제목 `#F2C14E`, 설명
    `#E8EDF2`의 프리팹 색상 계약을 지킨다.

헤드리스 규칙 테스트는 이 작업에서 추가하지 않는다. 상태 규칙이나 결정론을 바꾸지 않고 Unity 표현
계약만 변경하기 때문이다.

## 8. 후속 작업

카드 변형과 런·전투 상태 중앙관리 작업이 원본값과 유효값의 표현 계약을 확정한 뒤, 피해·방어·비용 등
변경된 텍스트 span에만 색상 피드백을 추가한다. 이 피드백은 상태 아이콘으로 대체하지 않으며, 현재
작업에서는 구현하지 않는다.
