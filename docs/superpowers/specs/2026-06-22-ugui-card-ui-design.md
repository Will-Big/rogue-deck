# Fate Weaver — 이미지 기반 카드 UI (uGUI + TMP) 설계

작성일: 2026-06-22

## 1. 목표와 범위

플레이테스트 화면을 **IMGUI 텍스트 UI → uGUI 이미지 카드 UI**로 전환한다.
미래 영역의 각 카드를 **아트 이미지 + 이름/실행 순서 + 하단 설명 텍스트 블록**으로 그려,
"이 카드가 무슨 짓을 하는지"가 한눈에 보이게 한다(Slay the Spire식).

- **유지**: 멀티턴 진행 로직(`MultiTurnPlaytestSession`, 순수 C#·헤드리스 검증 완료)과 세션 API
  (`ApplyInterventionAction` / `ResolveTurn` / `AdvanceTurn`), 시나리오 피커, 개입 액션, 턴/HP/상태 이월.
- **교체**: `FateWeaverPlaytestController`의 `OnGUI` 전체 → uGUI 빌드/바인딩. IMGUI 전용
  `RuntimeOsFontLoader` 폐기.
- **신규**: 카드 뷰(프리팹+컴포넌트), 카드 표현 뷰모델, 아트/설명 룩업, 에디터 빌더, 한글 TMP 폰트.
- **범위 밖**(후속): 드래그앤드롭, 이벤트 타임라인 애니메이션 재생, 덱 구성 화면, 카드 호버 확대 연출.

## 2. 아키텍처

### 2.1 런타임 컴포넌트

- **`CardPresentation`** (struct, `FateWeaver.Unity`): 코어 타입을 UI에서 분리하는 뷰모델.
  `ExecutionCardInstance` → `{ string Id, string DisplayName, int ExecutionOrder, Side Side, string Description,
  Sprite Art (nullable), bool IsLocked }`. `CardView`는 코어(`ExecutionCardInstance`/`CardDefinition`)에
  직접 의존하지 않는다 — 이후 uGUI 고도화/다른 화면에서도 재사용하는 seam.
- **`CardView`** (MonoBehaviour, 프리팹에 부착): 직렬화 참조
  `Image art`, `Image artFallback`, `TMP_Text nameText`, `TMP_Text executionOrderText`,
  `TMP_Text descriptionText`, `Image selectionOutline`, `GameObject lockBadge`, `Button button`.
  - `Bind(CardPresentation data, Action onClick)`: 아트가 있으면 `art`에 스프라이트, 없으면 `art`를 끄고
    `artFallback`을 켜고 측(side)별 단색 틴트 + 이름만. 설명/이름/실행 순서 텍스트 채움. 클릭 콜백 등록.
  - `SetSelection(SelectionKind kind)`: `None/Primary/Secondary` → `selectionOutline` 색/표시.
  - `lockBadge`는 `IsLocked`로 토글.
- **`FateWeaverPlaytestController`** (재작성): `OnGUI` 제거.
  - 직렬화 참조: `CardView cardPrefab`, `RectTransform cardRow`(HorizontalLayoutGroup),
    상태/메시지/타임라인용 `TMP_Text`들, 시나리오 버튼 컨테이너, 개입액션·턴실행·다음턴·초기화 `Button`들.
  - `RefreshCards()`: `cardRow`의 자식을 모두 제거 → `_session.CurrentOrder`마다 `cardPrefab` Instantiate,
    `CardPresentation`으로 Bind, 선택/고정 상태 반영.
  - 선택 로직(주/보조 토글), 개입 액션 적용, RESOLVE/NEXT/RESET은 기존 의미 그대로 uGUI 핸들러로 이전.
  - 텍스트(시나리오명/HP/상태/결과/메시지/타임라인)는 TMP로 출력. 기존 한글 문자열 로직 재사용.

### 2.2 아트·설명 룩업 (재사용 가능, 순수 로직)

- **`PlaytestCardArt`** (static): `Sprite Sprite(string cardId)`.
  - id 정규화(접두 일치): `quick_cut*`→`quick_cut`, `wrist_cut*`→`wrist_cut`,
    `preemptive_thrust*`→`preemptive_thrust`, `goblin_jab*`→`goblin_jab`.
    직결: `slash`, `mark`→`mark_target`, `counter`→`counter_stance`, `chain`→`chain_slash`.
    매핑 없음(`prep` 등)→`null`.
  - `Resources.Load<Sprite>(name)` + 정적 캐시(Dictionary). 자산은 이미 `Assets/Unity/Resources/`
    루트에 있어 경로 = 파일명(확장자 제외).
- **`PlaytestKoreanText.CardDescription(string cardId)`**: id → 손글씨 한글 설명(아래 표). 미등록=`""`.
  적 공격 카드(`goblin_jab`/`preemptive_thrust`)는 시나리오마다 피해 수치가 달라(예: goblin_jab 1 또는 3)
  **수치 없는 일반 문구**로 적는다. 정확한 피해는 해석 결과(타임라인)에서 확인된다.

### 2.3 카드 아트 매핑 / 설명 텍스트

| 카드 id | 표시 이름 | 아트(Resources) | 하단 설명(한글) |
|---|---|---|---|
| `quick_cut*` | 찰나의 베기 | `quick_cut` | 피해 2. 이번 턴에 가장 먼저 발동하면 대신 피해 10. |
| `slash` | 베기 | `slash` | 피해 2. |
| `mark` | 표식 새기기 | `mark_target` | 다음 카드가 플레이어 공격이고 적 공격보다 먼저면, 다음 플레이어 공격 피해 +6. |
| `counter` | 반격 자세 | `counter_stance` | 방어 2. 바로 앞에서 적이 공격했다면 피해 7 (3번째 안이면 +2). |
| `chain` | 연쇄 베기 | `chain_slash` | 피해 1. 바로 앞이 플레이어 실행 카드이고 3번째 안이면 추가 피해 5. |
| `prep` | 준비 | (없음→폴백) | 피해 1. |
| `wrist_cut*` | 손목 베기 | `wrist_cut` | 피해 3. 다음 플레이어 조건 보상을 무효화. |
| `goblin_jab*` | 고블린 찌르기 | `goblin_jab` | 고블린의 빠른 찌르기. |
| `preemptive_thrust*` | 선제 찌르기 | `preemptive_thrust` | 선제 일격. |

### 2.4 에디터 빌더 (수작업 최소화)

`Editor/FateWeaverPlaytestSceneCreator` 확장. 메뉴 항목 추가:

- **`Fate Weaver/Build Playtest Scene (uGUI)`**: Canvas(Screen Space Overlay)+CanvasScaler+EventSystem 생성 →
  `CardView.prefab`을 코드로 조립(Image/TMP/Outline/Button)해 `Assets/Unity/Prefabs/`에 저장 →
  상태/카드줄(HorizontalLayoutGroup)/개입액션·진행 버튼/타임라인 패널 배치 → 컨트롤러 부착·참조 와이어링 →
  씬 저장. 드래그 없이 한 번 클릭으로 완성, 생성 후 인스펙터에서 자유롭게 수정 가능.
- **`Fate Weaver/Create Korean TMP Font`**: 맑은 고딕(`C:/Windows/Fonts/malgun.ttf`)에서 **동적(Dynamic)**
  `TMP_FontAsset` 생성 → `Assets/Unity/Resources/Fonts/`에 저장하고 TMP 기본/폴백으로 지정
  (런타임에 한글 글리프 온디맨드 추가). 빌더가 PNG들의 `TextureImporter.textureType=Sprite`도 보장.

## 3. 데이터 흐름

```
MultiTurnPlaytestSession (순수 C#)
        │ CurrentOrder: IReadOnlyList<ExecutionCardInstance>
        ▼
FateWeaverPlaytestController.RefreshCards()
        │ 카드마다 CardPresentation 생성
        │   DisplayName = PlaytestKoreanText.CardName(id)
        │   Description = PlaytestKoreanText.CardDescription(id)
        │   Art         = PlaytestCardArt.Sprite(id)
        ▼
CardView.Bind(presentation, onClick) → 화면
        │ onClick → 컨트롤러 선택 로직(주/보조) → SetSelection 갱신
```

개입 액션/턴 진행 버튼은 기존과 동일하게 `_session`을 호출하고, 성공 시 `RefreshCards()` + 상태/타임라인
TMP 갱신.

## 4. 역할 분담

- **구현(내가 작성하는 코드)**: `CardPresentation`, `CardView`, `FateWeaverPlaytestController` 재작성,
  `PlaytestCardArt`, `PlaytestKoreanText.CardDescription`, 에디터 빌더 2개 메뉴, `RuntimeOsFontLoader` 제거,
  고아 `Assets/Cards.meta` 정리.
- **에디터(사용자가 수행 — 내가 구동 불가)**:
  1. `Window ▸ TextMeshPro ▸ Import TMP Essential Resources` (최초 1회).
  2. `Fate Weaver ▸ Create Korean TMP Font` 실행.
  3. `Fate Weaver ▸ Build Playtest Scene (uGUI)` 실행.
  4. Play로 확인하고 콘솔 오류 보고.

## 5. 검증

- Unity 계층(MonoBehaviour/에디터/프리팹)은 헤드리스 컴파일 대상이 아니므로 **사용자가 Play로 검증**.
- 순수 로직인 `PlaytestCardArt`의 id 정규화는 선택적으로 작은 EditMode 테스트로 가드 가능.
- TMP 미임포트 상태면 `CardView`/컨트롤러의 `TMPro` 참조가 컴파일되지 않으므로, **TMP Essentials 임포트가
  컴파일 선행 조건**임을 빌드 단계 순서로 보장(사용자 1번 단계가 먼저).

## 6. 리스크 / 메모

- PNG 임포트 타입이 `Default(Texture)`이면 `Resources.Load<Sprite>`가 null → 빌더가 Sprite로 보정.
- 동적 TMP 폰트 자산 스크립트 생성이 버전 이슈로 실패할 경우, 폴백: Font Asset Creator(Dynamic)로 수동 생성
  후 동일 경로에 저장하는 수동 절차를 PLAYTEST.md에 병기.
- `prep`처럼 아트 없는 카드/적 카드 폴백 경로를 항상 유지(아트 유무와 무관하게 위젯 구조 동일).
