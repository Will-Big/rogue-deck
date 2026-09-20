# B안 카드 프리팹 적용 계획

사람 검토용 [HTML 개요](2026-09-20-card-b-prefab.html). 상태: `active`, 구현 후 자동 검증·시각 검수 진행 중.
실행 시 `superpowers:executing-plans` 또는 사용자가 선택한 `superpowers:subagent-driven-development`를 적용한다.

## 상세

### 1. 목표·권위·변경 경계

2026-09-20 대화에서 확정한 B안 카드 외형과 단일/복합 대상 표현을 실제 전체 카드 프리팹에 적용한다.
시각 기준은 [B 배치 보존본](references/2026-09-20-card-b-layout.html)과
[모두·자신 비교 보존본](references/2026-09-20-card-target-comparison.html)이다.
보존본은 대화 목업이며 숫자·효과·카드명은 예시다. 배치 보존본에 남은 서술형 문구는 폐기하고,
문구는 비교 보존본 및 아래 계약을 따른다. 이 문서와 HTML 개요가 다르면 구현을 멈추고 확인한다.

이 계획 승인 후 이전 프레임 설계의 §6 진영 기호·모두/자신 모양, §11 카드 배치,
§15 관련 시각 테스트만 이 계획으로 대체한다. 코어 대상 의미·합성 순서·카탈로그 선택·반응형 손패의
균일 스케일 원칙은 유지한다. 기존 프레임 계획의 관련 시각 단계를 중복 실행하지 않는다.

- 코어 규칙, JSON 콘텐츠, 소유권 모델, 효과 실행 순서, 밸런스는 변경하지 않는다.
- `DescriptionComposer`의 `CardDescriptionLine.Text`를 그대로 표시한다. `피해 8.`, `방어 3.`을
  서술형으로 늘리거나 Unity에서 문장을 재합성하지 않는다. 수치·조건·문장 순서와 문장부호를 보존한다.
- 표현은 기존 uGUI LayoutGroup/LayoutElement/TMP/Image로 구현한다. 새 패키지·에셋은 추가하지 않는다.
- 메인 체크아웃의 Assets/Packages/ProjectSettings를 수정하지 않는다. 실행 단계에서 전용 워크트리와
  영어 브랜치 `codex/card-b-prefab`를 만든다. 이번 계획 작성은 문서만 변경한다.
- 기존 폰트 에셋 변경은 사용자 작업으로 보존한다. 캡처는 복제 폰트/아틀라스를 사용한다.
- 사용자 역할은 캡처 시각 검수와 Play 확인이다. 프리팹/씬 저작·배선·자동 검증은 구현자가 한다.

### 2. 확인한 기준선

아래 근거는 계획 작성 시 읽은 파일이다. 실행 시 다시 읽어 변경 여부를 확인한다.

| 사실 | 근거 |
|---|---|
| 두 프리팹은 별도 카테고리이며 실행만 대상/순서 참조를 갖는다 | `Assets/Unity/Prefabs/ExecutionCardView.prefab:2179`, `InterventionCardView.prefab:1839` |
| 전체 카드의 기준 크기는 200×280이다 | `Assets/Unity/Prefabs/ExecutionCardView.prefab:2048` |
| 손패는 프리팹 크기를 170×238 설정으로 덮어쓴다 | `Assets/Unity/Scripts/Cards/HandFanView.cs:30`, `:69`; `Assets/Scenes/FateWeaverBattle.unity:6280` |
| 레일 전체 카드 확대는 별도의 200×280 값을 사용한다 | `Assets/Unity/Scripts/Battle/ExecutionRailView.cs:34`, `:369` |
| 같은 대상 키의 문장을 합치고 문장부호를 생성하며, 본문 그룹은 첫 등장 순서를 보존한다 | `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs:101` |
| 대상 띠 데이터는 아군 우선으로 정렬돼 본문 그룹 순서와 다를 수 있다 | 위 파일 `:152` |
| 현재 설명 줄은 TMP 하나에 색이 있는 ◆를 덧붙인다 | `Assets/Unity/Scripts/Cards/DescriptionLineView.cs:28` |
| CardView가 대상 및 설명 자식을 생성한다 | `Assets/Unity/Scripts/Cards/CardView.cs:130`, `:169` |
| null 대상 본문도 실제 테스트 입력에 존재한다 | `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs:804` |
| 대상 없음 실행은 Empty 기호를 표시한다 | 위 파일 `:778` |
| 소유자가 없는 플레이어 데이터에는 파티 이름 폴백 경로가 있다 | `Assets/Unity/Scripts/Battle/BattlePresenter.cs:69` |
| 캡처는 Explicit 테스트이며 폰트/카탈로그 복제를 제공한다 | `Assets/Tests/UnityEditMode/CardFrameRenderCapture.cs:17`, `:131` |

### 3. 적용 계약

**외형.** 실행은 황토색 순서 탭, 개입은 보라색 종류 탭. 색은 프리팹 직렬화값으로 둔다.
순서 탭만 돌출한다. 이름은 카드 내부 상단 가운데, 비용은 내부 오른쪽에 두고 둘의 세로 중심을 맞춘다.
타이틀은 본문보다 크게 하되 긴 이름은 최대 두 줄로 배치한다. 아트는 상단 헤더와 짧은 간격으로 연결한다.
하단은 실제 소유자 이름과 카드 종류만 낮은 한 줄로 표시한다. 소유자 앞 장식 기호를 두지 않는다.
아트 없음 폴백·카드 뒷면·선택 외곽선·상태 아이콘·클릭/호버 동작을 보존한다.

**크기 제안.** 검토 기본안은 프리팹 200×336, 손패 170×285.6으로 목업의 세로 비율을 유지한다.
별도 크기 변경 응답이 없어 이 제안값으로 초기 구현했다. 사용자 시각 검수에서 크기 변경이 필요하면 HTML과 이 절을 함께 갱신한다.
기존 크기를 선택하면 아래 영역 비율을 200×280에 적용하되 본문을 자르지 않는다.
200×336 기준 첫 저작값: 좌우 여백 10, 헤더 내부 높이 36, 아트 높이 125, 대상 띠 높이 22,
하단 높이 15, 영역 사이 간격 6, 나머지 높이는 설명 패널. 이름 15, 본문 11, 대상 제목 10.
목업과 같은 비율을 유지할 출발점이며 최종 미세 조정은 캡처 검수로 결정한다.
카드 외형 크기를 바꾸지 않고 문자열 길이에 따라 내부 본문 여유 공간을 사용한다.

**대상 띠.** 표시 순서는 목업대로 적군 왼쪽·아군 오른쪽이다. 코어 배열을 변경하지 않고 UI에서만 정렬한다.
기호는 적군 ◆ / 아군 ●와 범위 모양의 조합이다. 문자 라벨은 대상 띠에 없다.
한 항목이면 가운데, 구분선 없음. 두 항목이면 동일 폭 두 칸 사이에 세로 구분선 하나.
`All`은 여러 칸을 괄호로 감싼 모양, `Self`는 외곽 원+중심점. 3칸은 전체 범위의 도상이지 인원수 표시가 아니다.
모양은 프리팹 Image 조합으로 만든다. Unicode 괄호/원 글리프에 의존하지 않는다.
전방/후방 1·2명은 기존 진영별 방향 의미를 보존한다. 현재 `TargetGlyphView.Bind`의 Enemy 미러링이
새 진영 기호까지 뒤집지 않도록 진영 표식과 범위 visual 루트를 분리한다.
0개 실행 대상은 기존 Empty, 개입은 대상 띠와 Empty 모두 없음. 범위가 같은 아군/적군은 합치지 않는다.

**설명.** 각 `CardDescriptionLine`을 제목/본문/구분선이 분리된 하나의 그룹으로 표현한다.
제목은 `◆ 적군 전방 1명`, `● 아군 자신`처럼 그룹 상단에 고정하고 본문과 따로 정렬한다.
본문은 원문 그대로 왼쪽 정렬한다. 그룹 하나면 본문 블록을 제목 아래 남은 공간의 세로 중앙에 둔다.
둘 이상이면 기존처럼 그룹 사이 가로 구분선과 다음 그룹의 위쪽 제목을 표시한다.
전체 본문 그룹 순서는 입력 `Lines` 그대로다. 대상 띠 순서를 맞추려고 능력 문장을 재정렬하지 않는다.
null 대상 줄은 제목을 숨기고 원문을 남긴다. 한 대상+null 본문은 두 그룹으로 취급해 null 효과를 유실하지 않는다.
그룹 0개면 설명 패널은 빈 상태이며 이전 Bind의 자식/구분선이 남지 않는다.
제목은 고정 좌표가 아니라 자기 그룹의 상단 앵커다. 긴 본문과 겹치거나 본문과 함께 중앙으로 이동하지 않는다.

**긴 문구.** 작은 단일 그룹은 세로 중앙, 긴 그룹은 위에서부터 자연스럽게 채운다.
Preferred Height를 먼저 확보하고 남는 높이만 분배한다. 제목 공간을 본문에 빌려주지 않는다.
설명 전체가 넘치면 TMP AutoSize를 직렬화된 최소 크기까지 허용한다. 생략기호·Mask로 숨기지 않는다.
최소 크기에서도 넘치는 현행 콘텐츠가 있으면 캡처와 카드 ID를 보고하고 미세 레이아웃 검수에서 해결한다.
무한 길이의 임의 콘텐츠를 고정 크기 카드에 모두 넣는 것은 이 계획의 보장이 아니다.

**소유자.** 개입도 실제 `OwnerDisplayName`을 표시한다. 이름 없음을 ‘공용’이나 가짜 이름으로 채우지 않는다.
`BattlePresenter`의 null owner 폴백은 카드 이름 표시를 null로 반환하도록 바꾸고 `CardView`는 영역을 숨긴다.
`IsPartyOwned`와 저장 데이터 의미는 이번에 삭제하지 않는다. 적 카드의 기존 소유자 숨김 동작도 보존한다.

### 4. 책임·파일·인터페이스 제안

규칙 30 검토 사항: CardView는 이미 19개 직렬화 참조를 가지며 대상/설명 자식 생성까지 수행한다
(`CardView.cs:23–41`, `:130–189`). 아래 분리는 계획 승인 대상이며 구현 전에 말없이 수행하지 않는다.

| 파일/객체 | 책임 | 모르는 것 |
|---|---|---|
| 기존 `CardView.cs` | 전체 카드 표시 갱신 순서 조정 | 대상 띠 구분선 수·본문 배치 계산 |
| 새 `Cards/CardTargetStripView.cs` | 대상 목록을 기호 띠로 표시 | 효과 문장·카드 비용 |
| 새 `Cards/CardDescriptionPanelView.cs` | 설명 그룹 목록을 배치 | 전투 실행·카드 입력 |
| 기존 `Cards/DescriptionLineView.cs` | 한 그룹의 제목·본문 표시 | 다른 그룹·카드 크기 결정 |
| 기존 `Cards/TargetGlyphView.cs` | 대상 키를 저작된 기호로 표시 | 피해·소유자 |
| 기존 `Content/CardPrefabCatalog.cs` | 카테고리별 프리팹 공급 | 대상 규칙·본문 정렬 |

새 클래스의 전체 경로 접두사는 `Assets/Unity/Scripts/`이다. 새 `.cs.meta`를 함께 커밋한다.
범위 라벨/진영 색·기호는 DescriptionLineView 안의 중첩 `[Serializable]` 값 DTO 목록으로 인스펙터에 둔다.
중앙 규칙 switch를 추가하지 않는다. 라벨 목록은 여섯 범위와 두 진영을 모두 검증하고 누락은 명시적으로 실패한다.

```csharp
// 제안 인터페이스: 모두 Unity 표현 계층에 둔다.
CardTargetStripView.Configure(TargetGlyphView glyphPrefab);
CardTargetStripView.Bind(IReadOnlyList<CardTargetKey> entries);
CardDescriptionPanelView.Configure(DescriptionLineView linePrefab);
CardDescriptionPanelView.Bind(IReadOnlyList<CardDescriptionLine> lines);
DescriptionLineView.Bind(CardDescriptionLine line); // 기존 공개 시그니처 유지
DescriptionLineView.SetLayout(bool onlyGroup, bool showSeparator);
```

Strip은 두 개의 저작된 슬롯과 하나의 저작된 separator를 사용하며 슬롯 아래 glyph만 기존 프리팹으로 복제한다.
Panel은 기존 DescriptionLineView 프리팹을 그룹 수만큼 복제한다. `new GameObject`·런타임 문자열 탐색 없음.
Line 프리팹의 구조는 `Separator`, `Heading`, `BodyArea/BodyText`다. heading 및 separator는 Inspector 참조.
한 그룹일 때 BodyArea를 늘리고 BodyText의 TMP 정렬을 MidlineLeft가 아닌 **Middle/Left (`TextAlignmentOptions.Left`)**로 설정한다
(글꼴 기준선이 아니라 영역 중앙). 다중 그룹은 TopLeft. 레이아웃은 LayoutGroup/LayoutElement로 맡긴다.
기존 `_text`는 본문 TMP 참조로 유지해 문자열 본문 접근을 단순하게 한다.

### 5. 구현 작업

#### 작업 1 — 분리된 설명 제목과 본문

파일: `DescriptionLineView.cs`, `DescriptionLineView.prefab`, 새 `CardDescriptionPanelView.cs`,
`CardFramePrefabTests.cs`, `CardViewPlayModeTests.cs` (모두 기존 경로는 §2 및 §4 기준).

- [x] 새 테스트 `Description_heading_stays_top_while_single_body_is_centered`를 추가한다.
  입력은 `new CardDescriptionLine(new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.All), "피해 8.")`.
  본문이 정확히 `피해 8.`이고 제목이 적군/모두를 포함하며, `Canvas.ForceUpdateCanvases` 후 제목이
  BodyArea 위에 있고 본문 정렬이 MidLeft임을 검증한다. 수정 전 실패를 확인한다.
- [x] 두 그룹/한 그룹/null/빈 목록을 아래 입력으로 검증한다. 제목·구분선 개수와 본문 원문 보존을 확인한다.
  `enemy All → "피해 8. 약화 1."`, `ally Self → "방어 3."`, `null → "카드 1장 뽑기."`.
  기대값: 2그룹이면 가로선 1개, 한 그룹이면 0개, null 제목 0개, 빈 목록은 자식 0개.
- [x] Line 프리팹을 분리하고 Bind는 `_text.text = line.Text`만 본문에 쓴다.
  대상이 있으면 직렬화 라벨을 조회해 Heading을 채운다. null이면 Heading 비활성화.
- [x] Panel.Bind에서 이전 자식을 즉시 비활성화·부모 분리한 뒤 Destroy하고 새 그룹을 생성한다.
  EditMode에서는 DestroyImmediate. `onlyGroup = lines.Count == 1`, `showSeparator = index > 0` 전달.
- [x] 긴 문구(원문 반복 포함)와 재Bind `2→1→0→2`를 검증한다. 테스트에서 실제 레이아웃 갱신 후
  heading/body bounds가 겹치지 않고 본문이 panel bounds 안에 있는지 확인한다.
- [x] 관련 EditMode 및 PlayMode 테스트를 실행하고 한국어 제목으로 독립 커밋한다.

기존 `CardFramePrefabTests` 안에 추가할 원문 보존 테스트의 시작 코드다. 이 클래스의 기존
`InstantiateDescriptionLine()`과 `CardPrefabCatalogTests.Field<T>()`를 사용한다.

```csharp
[Test]
public void Description_body_preserves_compact_text_without_faction_prefix()
{
    var view = InstantiateDescriptionLine();
    try
    {
        view.Bind(new CardDescriptionLine(
            new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.All),
            "피해 8. 약화 1."));
        var body = CardPrefabCatalogTests.Field<TMP_Text>(view, "_text");
        Assert.AreEqual("피해 8. 약화 1.", body.text);
    }
    finally { Object.DestroyImmediate(view.gameObject); }
}
```

이 테스트는 현재 ◆ 접두사 때문에 RED다. 이후 제목 관련 단언은 새 `_headingText` 직렬화 참조를
사용해 본문과 별도 컴포넌트임을 검증한다. Label 조회 DTO는 `_factionLabels`/`_rangeLabels`에
저장하고 바인딩 전에 중복·누락을 검증한다. 최소 구현의 핵심은 다음과 같다.

```csharp
// DescriptionLineView.Bind의 본문 계약; 제목은 별도의 직렬화 TMP 참조에 쓴다.
_text.text = line.Text;
_headingText.gameObject.SetActive(line.Target.HasValue);
// Panel.Bind의 그룹 배치 계약
for (int i = 0; i < lines.Count; i++)
{
    var group = Instantiate(_linePrefab, _content);
    group.Bind(lines[i]);
    group.SetLayout(lines.Count == 1, i > 0);
}
```

`_linePrefab`/`_content`는 Panel의 직렬화 참조다. 생성 루프 앞의 정리 절차는 위 체크 항목을 따른다.

#### 작업 2 — 대상 띠와 모두·자신 기호

파일: 새 `CardTargetStripView.cs`, `TargetGlyphView.cs`, `TargetGlyphView.prefab`,
실행 프리팹 안의 대상 띠, `CardFramePrefabTests.cs`.

- [x] 아래 6입력에 대해 표시 개수·중앙·구분선·방향 테스트를 먼저 추가하고 실패를 확인한다.
  `[]`, `[Enemy/FrontOne]`, `[Ally/FrontTwo]`, `[Enemy/All, Ally/Self]`, `[Ally/All]`, `[Enemy/Self]`.
- [x] 기존 `Target_glyph_has_no_faction_shape_nodes_and_all_has_two_endpoints`는 새 문법으로 대체한다.
  기존 ‘방향 미러’, 선택된 범위 visual 하나만 활성화, 진영색 적용 테스트는 유지한다.
- [x] All은 칸 전체를 감싼 괄호, Self는 원+중심점으로 프리팹 저작한다. 새 외부 그래픽 자산을 쓰지 않는다.
  기존 원 도형 및 Image 조합을 활용한다. 진영 기호는 scope visual 밖에 두어 미러링 영향에서 분리한다.
- [x] Strip.Bind는 입력을 복사해 적군 우선 표시한다. 원본 리스트를 정렬/변형하지 않는다.
  슬롯 한 개는 가운데, 두 개는 분할, 빈 목록은 Empty 하나. separator는 entries.Count == 2일 때만 활성화.
- [x] `2→1→0→2`, 반대 진영으로 재Bind, All/Self 대칭 모양을 검증하고 커밋한다.

#### 작업 3 — 전체 프레임 통합과 소유자 표시

파일: `ExecutionCardView.prefab`, `InterventionCardView.prefab`, `CardView.cs`,
`Assets/Unity/Scripts/Battle/BattlePresenter.cs`, `CardPrefabCatalogTests.cs`,
`CardFramePrefabTests.cs`, `BattlePresenterTests.cs`, `CardViewPlayModeTests.cs`.

- [x] 프리팹 계약 테스트를 먼저 수정한다: 비용 bounds는 root 내부, 실행 순서 탭만 root 위로 돌출,
  개입에 종류 탭은 있으나 순서 TMP/대상 패널은 없음. 이름/비용 중심이 카드 내부 상단에서 일치한다.
- [x] CardView에 `_targetStrip`/`_descriptionPanel` 참조를 두고 기존 생성 루프를 위임한다.
  Configure는 카탈로그의 기존 glyph/line 프리팹을 두 컴포넌트에 전달한다. 새 레이아웃 판단은 추가하지 않는다.
  기존 중복 참조/정리 코드는 모든 호출처·테스트를 확인한 뒤 제거한다.
- [x] §3의 프레임 크기·영역 저작값으로 실제 프리팹을 배선한다. category 탭과 대상 진영색은 서로 독립이다.
  대상/설명 패널은 각 프리팹에 실제 저장된 컴포넌트다. 런타임 컴포넌트 조립으로 대체하지 않는다.
- [x] `BattlePresenter.Resolve`의 null owner 분기에서 카드 이름 폴백을 제거한다.
  `PartySharedCardUsesTheSharedOwnerName` 테스트는 이름 null/가짜 소유자 없음으로 교체하고,
  실제 소유자 이름 유지 및 적 소유자 숨김 테스트는 유지한다. 코어 소유권을 삭제하지 않는다.
- [x] 기존 아트 폴백·뒷면·선택 Primary/Secondary·잠금·상태 hover raycast·category mismatch 테스트를 통과시킨다.
  카탈로그 GUID와 전체 카드 프리팹 GUID를 보존한다. 실제 prefab 인스턴스로 재Bind 테스트도 수행한다.
- [x] PlayMode fixture의 이전 private field 배선을 새 컴포넌트 구조로 갱신하고 커밋한다.

#### 작업 4 — 카드 소비처 크기와 실제 화면 검증

파일: `Assets/Scenes/FateWeaverBattle.unity`, `HandFanView.cs`,
`Assets/Unity/Scripts/Battle/ExecutionRailView.cs`, `CardFrameResponsiveLayoutTests.cs`,
`CardFrameRenderCapture.cs`, `CardFrameRenderCaptureSafetyTests.cs`.

- [x] 전체 카드 크기를 다시 지정하는 모든 소비처를 `rg 'sizeDelta|PreviewSize|_cardSize' Assets/Unity/Scripts`로
  재검색한다. 손패 `_cardSize`와 레일 확대 미리보기의 크기/위치 계산을 함께 갱신한다.
- [x] 레일 PreviewSize 상수를 추가 확대하지 않는다. 선택된 전체 프리팹 RectTransform의 저작된 크기를
  미리보기 생성 시 읽고 해당 rect 크기로 상하좌우 화면 경계를 계산한다. 소형 RailCardView 크기는 유지한다.
- [x] 손패는 승인된 비율의 직렬화 `_cardSize`를 사용하고 기존 얕은 호·균일 스케일 계산을 유지한다.
  카드 내부를 프레임마다 재배치하는 LateUpdate는 추가하지 않는다.
- [x] 기존 4:3/16:10/16:9/21:9, 혼합 손패 1~5장 테스트를 새 비율로 실행한다.
  겹친 상태에서 안쪽으로 이동한 비용의 가림을 확인한다. 상시 노출이 불가능하면 실제 캡처로 검토하고
  독단적으로 비용을 다시 밖으로 옮기거나 손패 팬 알고리즘을 바꾸지 않는다.
- [x] 렌더 캡처에 단일 적/단일 아군/양쪽/All/Self/무대상/개입/긴 이름/긴 설명 사례를 추가한다.
  font isolation과 캡처 안전성 테스트를 유지한다. 기본 테스트에서 건너뛰는 Explicit 캡처는 별도로 실행한다.
- [x] 레일 hover와 배치 비행, 손패 hover에서 스케일·뒷면·상태·선택을 확인한다.
  렌더 결과와 자동 검증을 제출하고 사용자 Play/시각 검수를 받은 뒤 이 작업을 완료한다.

### 6. 실행 명령과 완료 기준

실행 워크트리에서 `Tools/setup-dev.sh`를 먼저 실행한다. 아래 `<worktree>`는 그 반환 절대 경로다.
기존 테스트를 통째로 삭제하지 않고 새 시각 계약과 충돌하는 단언만 교체한다.

```bash
Tools/verify.sh
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath <worktree> -runTests -testPlatform EditMode \
  -testResults /private/tmp/card-b-editmode.xml -logFile /private/tmp/card-b-editmode.log
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath <worktree> -runTests -testPlatform PlayMode \
  -testFilter FateWeaver.Tests.UnityPlayMode.CardViewPlayModeTests \
  -testResults /private/tmp/card-b-playmode.xml -logFile /private/tmp/card-b-playmode.log
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath <worktree> -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardFrameRenderCapture \
  -testResults /private/tmp/card-b-capture.xml -logFile /private/tmp/card-b-capture.log
```

`-quit`는 붙이지 않는다. XML의 total>0, failed=0, result=Passed를 확인한다.
캡처는 `/private/tmp/primitive-card-frame-captures/`의 이번 실행 PNG를 확인한다.
코어를 바꾸지 않아도 전체 verify를 실행한다. Unity 결과를 헤드리스 통과로 대신하지 않는다.

완료 체크: 원문 문구 보존 / 소유자 무기호 / 내부 이름·비용 / 확대 아트 / 대상 수별 구분선 /
All·Self / 고정 제목·단일 본문 중앙 / null·빈 설명 / 재Bind / 화면비·소비처 / 사용자 시각 검수.
한국어 커밋 제목 형식은 `feat(ui): B안 카드 프레임을 적용한다` 같은 현재형이다.
master 머지는 별도 사용자 승인 후 전체 verify 통과 상태에서만 한다. 완료 후 계획을 archive로 옮기고
색인의 계획 행을 삭제하며, 최종 표현 계약은 current 설계 문서에 남긴다. 브랜치와 워크트리를 정리한다.

### 7. 검토 초점

1. 재Bind 때 이전 두 번째 기호/구분선/제목 잔존 — 작업 1·2·3의 PlayMode 전이 테스트.
2. 한 진영 효과와 null 대상 효과가 함께 있을 때 유실 — 작업 1의 3종 입력.
3. 긴 한국어 설명과 긴 이름의 겹침/축소 — 작업 1 bounds와 작업 4 캡처.
4. All의 3칸을 인원수로 오해, Self와 진영 ● 혼동 — 작업 2 기호 차이 및 작업 4 시각 검수.
5. 커진 전체 카드/안쪽 비용이 손패·레일 끝에서 가려짐 — 작업 4 화면비와 실제 hover 검수.


### 8. 구현 검수 인계 (2026-09-21)

브랜치 `codex/card-b-prefab`, 워크트리 `/Users/ish/.codex/worktrees/card-b-prefab/rogue-deck`.
master 머지와 사용자 Play/시각 승인은 아직 하지 않았다. 그 단계가 남아 있으므로 계획은 active로 유지한다.

- [실제 프리팹 5종 캡처](references/2026-09-21-card-b-prefab-gallery.png): 복합 대상, 적군 모두, 아군 자신, 긴 이름·설명, 개입.
- [960×720 혼합 손패](references/2026-09-21-card-b-hand-960x720.png): 안쪽 비용과 돌출 탭의 노출 확인.
- 본문은 `DescriptionLineView.Bind`에서 원문 그대로 표시한다. 제목은 별도 TMP이며 `SetLayout`으로
  단일 본문만 Middle/Left, 다중 본문은 TopLeft로 설정한다.
- `CardDescriptionPanelView.Bind`와 `CardTargetStripView.Bind`가 생성 자식의 수명과 배치를 맡고,
  카탈로그 공급 데이터는 그대로 `CardView.Configure`에서 위임한다.
- `HandFanView.SetCards`는 저작된 200×336 프레임을 균일 축소하고,
  `HandCardHoverEffect.Capture/Enlarge/Restore`는 그 기본 배율을 보존한다.
- `ExecutionRailView.OnHover`는 실제 프리팹 크기로 확대 영역의 상하좌우 경계를 계산한다.
- `CardFramePrefabTests.Every_content_card_keeps_body_inside_its_authored_area`로 저장소 JSON 전체의
  본문 범위와 제목 최대 두 줄을 검증한다. 상태·선택·뒷면·비행·입력 테스트도 유지한다.

구현 중 판단과 비용:

1. 제안 크기 200×336을 기본으로 사용했다. 손패 기본 크기는 170×285.6이며 공간이 작으면 기존
   반응형 계산으로 더 줄어든다. 비용: 실제 화면에서 글자가 작다고 판단되면 크기/손패 영역 재검수가 필요하다.
2. 직렬화 이전은 설명·대상·전체 프레임을 묶어 검증했다. 부분 이전 상태로 나누어 커밋하지 않았다.
   비용: 되돌리기 단위가 세 개의 부분 변경보다 크다.
3. 같은 진영에 여러 대상 범위가 전달되면 같은 슬롯에 모두 표시한다. 진영 사이에만 구분선을 둔다.
   비용: 현재 콘텐츠를 넘는 조밀한 범위 조합은 추가 가독성 검수가 필요하다.

배치 저작 도구는 프리팹 저장 뒤 제거했다. 메인 체크아웃의 폰트는 수정하지 않았으며 워크트리 테스트가
생성한 폰트 차이는 이름 있는 stash `codex/card-b-prefab Unity 테스트 생성 폰트 변경 보존`에 보관했다.
프리팹 팔레트 저장 후 Unity Mono 종료 중 네이티브 크래시가 한 번 있었지만 저장 완료를 확인했고,
재기동 후 테스트와 캡처로 결과를 다시 검증한다.
