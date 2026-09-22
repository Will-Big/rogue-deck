# HandFan 시각 테스트 씬 구현 계획

사람 검토용 개요: [HTML](2026-09-22-handfan-sandbox.html)

## 상세

### 목표와 실행 경계

상태: active — 구현·자동 검증 완료, 사용자 Play 검수·master 머지 대기. 근거: 이번 대화에서 승인한 카드 추가·선택 제거·전체 비우기·장수 표시 구성.
기존 HandFan에 카드가 늘고 줄 때 배치와 호버를 눈으로 확인하는 독립 씬을 만든다.
실제 드로우·덱·전투 상태·새 이동 연출·자동 시나리오·범용 테스트 프레임워크는 만들지 않는다.
기존 호버 효과는 그대로 사용한다. 향후 기능을 위한 인터페이스나 레지스트리를 미리 만들지 않는다.

이 계획은 사용자 요청에 따라 작성했다. 실행 근거는 이 상세이며, HTML과 다르면 중단하고 확인한다.
구현 시 superpowers:executing-plans를 기본 후보로 사용한다. 사용자가 병렬 실행을 선택하면
superpowers:subagent-driven-development를 사용한다. 2026-09-22 사용자 승인 후 이 세션에서 순차 구현했다.

### 확인한 기존 구현

아래 경로는 저장소 루트 기준이며 작성 시 직접 확인했다.

- `Assets/Unity/Scripts/Cards/HandFanView.cs`, SetCards: SetCards는 기존 뷰를 제거하고 카탈로그에서 다시 생성한다. 증감 후 목록 전체를 전달한다. 풀링·차분 갱신으로 바꾸지 않는다.
- 같은 파일의 SetSelection(index, CardView.SelectionKind)으로 선택 테두리를 지정할 수 있다.
- `Assets/Unity/Scripts/Cards/CardPresentation.cs`, FromDefinition: 정의와 설명 카탈로그를 표현 데이터로 변환한다.
- `Assets/Unity/Scripts/Content/CardPrefabCatalog.cs`, Create/ValidateOrThrow: 카테고리별 실제 프리팹 인스턴스와 참조 검증을 제공한다.
- `Assets/Unity/Scripts/Battle/CombatNodeFlow.cs:42`: ContentBootstrap.Load(UnityContentRoot.Path) 및 Succeeded/Errors 처리 예시.
- `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs:42`: CreateDefault(StatusContentCatalog).
- `Assets/Unity/Editor/BattleSceneBuilder.cs:138`: 전투 씬의 HandFan 배치와 Content 자식 참조 구성.
- `Assets/Tests/UnityPlayMode/HandFanResponsivePlayModeTests.cs`: 크기 변경에 대한 기존 레이아웃 회귀 검사.
- `docs/agents/worktrees.md`, 규칙 15·16: Assets 변경은 전용 워크트리에서 수행한다.

- 최신 기준: master `80f0586`의 `HandFanView` + `HandFanLayoutView`를 사용한다. 배치 설정은 `Assets/Unity/Scripts/Cards/HandFanLayoutView.cs`, 조절 방법은 `docs/agents/hand-layout.md`가 근거다. 테스트 프리팹의 HandFan은 최신 전투 씬 설정을 복사하고 내부 Content 참조를 다시 연결한다.

### 확정할 동작

1. 씬 시작은 빈 핸드다. 추가 버튼은 인스펙터의 카드 ID 목록을 순서대로 반복한다. 무작위는 쓰지 않는다.
2. 카드 클릭은 제거 대상을 선택한다. 선택은 데이터 ID가 아니라 목록 인덱스를 사용해 중복 카드를 구분한다.
3. 추가 시 기존 선택 인덱스를 유지한다. 선택 제거 후에는 선택을 해제한다. 미선택 제거는 아무 변화가 없다.
4. 전체 비우기는 목록·선택·추가 순서를 초기화한다. 빈 상태에서 다시 눌러도 안전하다.
5. 장수는 실제 테스트 목록 길이로 표시한다. 미선택이면 제거 버튼, 빈 목록이면 비우기 버튼을 비활성화한다.
6. ID 목록이 비었거나 존재하지 않는 ID가 있으면 부분 실행하지 않는다. 오류를 패널에 표시하고 조작을 비활성화한다.
7. 필수 씬 참조 누락은 Editor 검증으로 잡는다. 런타임에는 명확한 오류 로그를 남기고 비활성화하며 null 예외를 반복하지 않는다.
8. 카드 수에 임의의 게임 규칙 상한을 넣지 않는다. 대량 카드 성능 보장이나 최적화는 이번 범위 밖이다.

### 파일과 책임

아래 경로에 구현과 저장 에셋을 추가했다. C# namespace는 FateWeaver.Unity.

| 파일 | 책임 | 모르는 것 |
|---|---|---|
| `Assets/Unity/Scripts/Testing/HandFanSandboxState.cs` | 테스트 목록과 선택 상태 관리(일반 C# 클래스) | Unity 객체·전투 규칙·JSON |
| `Assets/Unity/Scripts/Testing/HandFanSandboxCardSource.cs` | 기존 JSON을 테스트용 CardPresentation 목록으로 변환(MonoBehaviour) | 현재 핸드·버튼·배치 |
| `Assets/Unity/Scripts/Testing/HandFanSandboxPanel.cs` | 버튼 입력과 상태 텍스트 표시(MonoBehaviour) | 카드 정의·목록 변경 정책 |
| `Assets/Unity/Scripts/Testing/HandFanSandboxController.cs` | Source → State → HandFan/Panel 호출 순서 배선(MonoBehaviour) | 카드 설명 조합·선택 유효성 판단·레이아웃 계산 |
| `Assets/Unity/Prefabs/Testing/HandFanSandbox.prefab` | Canvas·패널·실제 HandFan과 관리 컴포넌트의 저장된 구성 | 전투 흐름 |
| `Assets/Scenes/HandFanSandbox.unity` | 테스트 프리팹과 직렬화된 입력·카메라 구성의 독립 진입점 | 전투 씬·빌드 진입 순서 |
| `Assets/Tests/UnityEditMode/HandFanSandboxTests.cs` | 중복 카드·선택·초기화·콘텐츠 오류 검사 | 시각적 품질 판정 |
| `Assets/Tests/UnityEditMode/HandFanSandboxPanelTests.cs` | 버튼 상태·구독 수명 검사 | 카드 데이터·레이아웃 |
| `Assets/Tests/UnityEditMode/HandFanSandboxSceneTests.cs` | 저장된 씬·프리팹의 필수 참조와 버튼 배선 검사 | Play의 시각적 결과 |

새 폴더와 에셋의 .meta도 함께 커밋한다. 기존 HandFanView, 전투 씬, 코어와 패키지는 변경하지 않는 계획이다.
그 변경이 필요해지면 원인을 먼저 보고한다. Controller는 전용 조정자라 연결이 모이지만 정책은 State,
표시 변환은 Source와 Panel에 둔다. 기존 관리자에 책임을 추가하지 않는다(규칙 30).

### 인터페이스와 구현 기준

State는 아래 계약을 갖는다. CardPresentation은 이미 Unity 표현 타입이므로 Core로 옮기지 않는다.

```csharp
public HandFanSandboxState(IReadOnlyList<CardPresentation> samples);
public IReadOnlyList<CardPresentation> Cards { get; }
public int SelectedIndex { get; } // 미선택은 -1
public bool CanRemove { get; }
public bool CanClear { get; }
public void Add();
public void Select(int index);
public void RemoveSelected();
public void Clear();
```

State는 비어 있지 않은 samples 사본을 보관한다. Add는 cursor 위치의 표현을 추가하고 순환한다.
Select는 범위 밖 입력을 -1로 정규화한다. RemoveSelected는 CanRemove일 때만 RemoveAt 후 -1로 초기화한다.
Clear는 목록을 비우고 selected=-1, cursor=0으로 되돌린다. 외부에 가변 List를 노출하지 않는다.

Source의 직렬화 필드는 private string[] _cardIds와 private CardArtCatalog _artCatalog다.
콘텐츠 ID는 콘텐츠 키이며 객체·파일 경로 검색에 사용하지 않는다. 아트 카탈로그는 선택 참조다.
메서드 계약:

```csharp
public bool TryLoad(out IReadOnlyList<CardPresentation> samples, out string error);
```

TryLoad는 ContentBootstrap.Load(UnityContentRoot.Path) 후 Succeeded를 검사하고,
KoreanDescriptionCatalog.CreateDefault(result.Content.Statuses)를 생성한다. ID를 모두 검증한 뒤
CardPresentation.FromDefinition(result.Content.Cards.Get(id), korean, resolver)로 변환한다.
resolver는 아트 참조가 있으면 ArtFor, 없으면 null이다. 실패 시 samples는 빈 목록, error는 원인이다.
가짜 카드 정의·하드코딩 설명·별도 콘텐츠 JSON·새 SO 타입은 만들지 않는다.

Panel은 직렬화된 Button 3개와 TMP_Text 장수/오류 필드를 가진다.

```csharp
public event Action AddRequested;
public event Action RemoveRequested;
public event Action ClearRequested;
public void ShowState(int count, bool canRemove, bool canClear);
public void ShowError(string message);
```

Panel의 OnEnable/OnDisable은 자기 버튼 리스너만 등록/해제한다. ShowState는 장수 텍스트와 버튼 상태를 갱신한다.
ShowError는 오류 텍스트를 표시하고 모든 조작 버튼을 끈다. 버튼 리스너를 매 갱신마다 추가하지 않는다.
Controller는 직렬화 참조 Source/Panel/HandFan을 검증하고 초기화 1회, 구독/해제를 대칭으로 수행한다.
모델 변경 후 호출의 핵심은 다음이다(실제 메서드 내부 흐름):

```csharp
_hand.SetCards(_state.Cards, OnCardClicked, null);
_hand.SetSelection(_state.SelectedIndex, CardView.SelectionKind.Primary);
_panel.ShowState(_state.Cards.Count, _state.CanRemove, _state.CanClear);
```

카드 클릭은 State.Select(index) 후 SetSelection과 Panel 상태만 갱신한다. 선택만 바뀔 때 뷰를 재생성하지 않는다.
추가/제거/비우기는 State 메서드 호출 후 위 전체 갱신을 수행한다. 비활성화·재활성화 시 중복 구독을 피한다.

### Task 1 — 카드 목록 조작과 표현 연결

- [x] 사용자가 개요를 승인하면 전용 워크트리와 영어 브랜치 `codex/handfan-sandbox`를 만든다. 메인 브랜치를 전환하지 않는다. using-git-worktrees 스킬과 `docs/agents/worktrees.md`를 따른다.
- [x] 훅 설정을 확인하고 미설정일 때 `Tools/setup-dev.sh`를 실행한다. 실행 전 사용자 변경과 작업 기준 커밋을 기록한다.
- [x] 위 Testing 스크립트 4개와 HandFanSandboxTests.cs를 작성한다. 기존 패턴과 책임 경계를 유지하고 public 필드/런타임 객체 검색은 사용하지 않는다.
- [x] 다음 핵심 테스트를 먼저 작성해 실패를 확인한다. A/B는 UnityTestContent.Cards()의 brace/quick_cover를 기존 FromDefinition으로 변환한 값이다.

```csharp
var state = new HandFanSandboxState(new[] { a, b });
state.Add(); state.Add(); state.Add();
Assert.That(state.Cards.Select(c => c.Id), Is.EqualTo(new[] { a.Id, b.Id, a.Id }));
state.Select(2);
state.RemoveSelected();
Assert.That(state.Cards.Select(c => c.Id), Is.EqualTo(new[] { a.Id, b.Id }));
Assert.That(state.SelectedIndex, Is.EqualTo(-1));
state.Clear(); state.Add();
Assert.That(state.Cards.Single().Id, Is.EqualTo(a.Id));
```

- [x] 추가 검사: 빈 목록 제거/비우기 반복, 가운데 제거 후 순서, 추가 후 기존 선택 유지, 범위 밖 선택 해제, 빈 ID 목록과 누락 ID에서 TryLoad 실패. 각 실패는 목록 불변 또는 빈 출력/오류 메시지로 단언한다.
- [x] 구현 후 Unity EditMode 테스트를 워크트리 대상으로 실행한다. 구체 명령은 아래 공통 검증 절을 사용한다.
- [x] 관련 파일만 커밋한다. 제목 예: `feat(testing): 핸드 시각 테스트 조작부를 추가한다`.

### Task 2 — 저장된 테스트 씬과 검수

- [x] Unity 도구를 사용하기 전에 해당 unity-cli/ui-ugui 스킬을 읽는다. 워크트리에서 에디터 저작으로 프리팹과 씬을 저장한다. 일회성 저작 스크립트를 쓴다면 Editor에만 두고 저장 결과에 런타임 생성 의존을 남기지 않는다.
- [x] 화면 상단에 조작 버튼·장수·오류 텍스트, 하단에 HandFan을 배치한다. 기존 전투 HandFan의 RectTransform과 직렬화 튜닝 값을 기준으로 복사하고 CardPrefabCatalog는 기존 에셋을 직접 할당한다. 프리팹을 복제한 카드 자산은 만들지 않는다.
- [x] Source의 ID 목록은 저작 단계에서 기존 JSON의 brace와 quick_cover로 지정한다. 코드에 ID를 상수로 넣지 않는다. 폰트·입력 액션·카메라 참조도 직렬화한다. 런타임 BattleUiKit 팩토리로 화면을 만들지 않는다.
- [x] Canvas, GraphicRaycaster, EventSystem/InputSystemUIInputModule과 필요한 카메라를 저장한다. 전투 시작 컴포넌트와 CombatNodeFlow는 넣지 않는다. 빌드 씬 목록은 변경하지 않는다.
- [x] SceneTests는 실제 저장된 씬을 열어 Sandbox 프리팹 연결, Missing Script 부재, 필수 직렬화 참조, 카탈로그 ValidateOrThrow, 버튼과 입력 배선, JSON ID 해석 성공을 검증한다. 테스트에서 임의의 대체 씬을 만들어 통과시키지 않는다.
- [x] EditMode 결과 XML과 `Tools/verify.sh` 전체 성공을 확인한다. 기존 HandFan 레이아웃 테스트를 변경해 회귀를 숨기지 않는다.
- [ ] 사용자 Play 검수를 받고 아래 체크리스트 결과를 기록한다. 워크트리 씬 경로는 아래 구현 결과를 참고한다. 시각 검수는 사용자가 하고 자동 테스트 통과를 시각 승인으로 기록하지 않는다.
- [x] 씬·프리팹·관련 .meta·테스트를 커밋한다. 제목 예: `feat(testing): HandFan 시각 테스트 씬을 추가한다`.

### 검증과 완료 조건

자동 검사 명령은 워크트리 루트에서 실행한다.

```bash
Tools/verify.sh
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults /private/tmp/handfan-sandbox-editmode.xml \
  -logFile /private/tmp/handfan-sandbox-editmode.log
```

Unity 실행에는 -quit를 붙이지 않는다. 종료 코드와 XML result/total/passed/failed/skipped를 모두 확인한다.
Unity 장애는 `docs/agents/unity-batch-runs.md`를 따른다. verify.sh 통과만으로 Unity 검증 완료라 말하지 않는다.
코어 규칙 추가가 없으므로 별도 코어 테스트나 밸런스 Compare는 필요하지 않다.

사용자 Play 체크리스트(수치는 대표 실행 예시):

- 빈 핸드에서 시작 → 추가 1회 → 1장 및 장수 일치.
- 5장까지 추가 → 가운데 카드 클릭 → 테두리 확인 → 제거 → 나머지 4장 순서와 재배치 확인.
- 12장까지 추가 → 간격·각도·축소·호버 관찰 → Game 뷰 폭 변경 후 반응 확인.
- 선택 상태에서 추가 → 기존 선택 유지. 중복 모양 카드 중 끝 카드 선택/제거 → 선택한 위치만 제거.
- 전체 비우기 두 번 → 오류 없음 → 다시 추가하면 첫 샘플부터 표시.
- 씬 재진입/조작부 재활성화 후 추가 한 번에 한 장만 증가. Console의 새 오류 없음.

직접 눈으로 발견한 기존 HandFan 문제는 기록하고, 테스트 씬 구현에 섞어 자동 수정하지 않는다.
시각 승인까지 끝나면 계획 두 파일을 archive로 옮기고 중앙 색인의 행을 같은 커밋에서 제거한다.
master 머지는 별도 사용자 승인과 전체 verify 통과 후에만 수행하고 완료 브랜치·워크트리를 정리한다.

### 검토 초점

중복 ID의 개별 선택, 빈 상태 반복 조작, 누락 콘텐츠, 재활성화 중복 이벤트, 좁은 화면·많은 카드의 표현을
위 자동 검사 또는 사용자 Play 단계에 각각 배정했다. 마지막 항목의 시각적 품질은 자동 합격 처리하지 않는다.

### 구현 결과 — 2026-09-22

- 브랜치: `codex/handfan-sandbox`. 작업 위치: `/Users/ish/.codex/worktrees/handfan-sandbox/rogue-deck`.
- 씬: `Assets/Scenes/HandFanSandbox.unity`. 프리팹: `Assets/Unity/Prefabs/Testing/HandFanSandbox.prefab`.
- 구현 커밋: `b79fc6a`(조작부), `29059d3`(저장 씬·입력 회귀 검사).
- `Tools/verify.sh`: 헤드리스 785개, 노트북 161개 통과. 실행 로그: `/private/tmp/handfan-master-verify-sequential.log`.
- 전체 Unity EditMode: 1005개 통과, 11개 스킵, 실패 0개. 결과: `/private/tmp/handfan-master-final.xml`.
- 신규 검사는 상태·콘텐츠 8개, 패널 2개, 씬·입력·조정자 7개다. 근거: 위 표의 세 테스트 파일.
- 저작은 일회성 Editor 스크립트로 수행한 후 저장 에셋만 남겼다. 입력 참조는 기존 `UIInputActions.inputactions`의 영구 서브에셋이다. Input System의 기본 입력 자동 할당이 저장 누락을 가릴 수 있어 `HandFanSandboxSceneTests.Prefab_stores_persistent_input_references_without_default_action_fallback`이 프리팹 에셋 자체를 검사한다.
- 사용자 확인: 워크트리 프로젝트를 Unity에서 열고 해당 씬에서 Play한다. 상단 버튼으로 장수를 늘리고 카드를 클릭해 선택한 뒤 제거한다. `SandboxControls`의 CardSource 인스펙터에서 테스트 카드 ID 목록을 바꿀 수 있다.
- 사용자 시각 검수와 master 머지는 아직 수행하지 않았다. 시각 확인 전에는 문서를 보관하지 않는다.

최종 독립 리뷰에서 HandFan 내부 카탈로그를 삭제했을 때의 반복 예외 처리를 보완했다.
`Assets/Unity/Scripts/Testing/HandFanSandboxController.cs`의 RefreshCards는 표시 예외를 오류 메시지로
전환하고 조작부를 중단한다. 정상 복구를 자동 시도하지 않으므로 참조 수정 후 씬을 다시 시작한다.
근거 테스트: `HandFanSandboxSceneTests.Missing_hand_catalog_reports_error_and_stops_further_commands`
(실패 확인 후 수정, 최종 전체 테스트 통과). 기존 HandFan은 변경하지 않았다.
리뷰가 판단을 유보한 간격·잘림·호버·좁은 화면은 규칙 17에 따라 사용자 Play 확인으로 남긴다.

### 최신 master HandFan 반영 — 2026-09-22 사용자 수정 요청

초기 작업 기준 이후 master에 통합된 `80f0586`을 작업 브랜치로 가져왔다.
기존 테스트 프리팹은 새 `_layout` 참조가 연결되지 않아 회귀 테스트가 실패했다.
Unity 에디터 저작으로 최신 전투 씬의 `HandFanLayoutView` 설정과 RectTransform 값을 반영하고,
테스트 프리팹 내부의 Content와 HandFanView._layout을 연결했다.
근거: `Assets/Tests/UnityEditMode/HandFanSandboxSceneTests.cs`의
`Saved_prefab_wires_the_current_configurable_hand_layout`과
`Sandbox_cards_use_inspector_arc_settings_after_add_remove_and_clear`.
새 테스트는 저장 배선뿐 아니라 카드 추가 후 Radius 변경 → 간격 변화 → 선택 제거 → 전체 비우기를 확인한다.
사용자는 테스트 씬 HandFan의 **Hand Fan Layout View** 인스펙터에서 Radius, Total Angle,
Baseline Padding, Position Offset, Card Scale을 조절할 수 있다. 옵션 의미는
[손패 레이아웃 조절](../../agents/hand-layout.md)을 따른다.

최신 기준 재검증: Unity EditMode 1005개 통과·11개 스킵·실패 0개, 헤드리스 785개와 노트북 161개 통과. 위 구현 결과의 로그 경로는 이 최신 실행을 가리킨다. 병렬 실행에서는 기존 BootstrapReportsAnUnknownEnemyPolicy가 공용 임시 폴더 삭제 경합으로 한 번 실패했으며, Unity 종료 후 Tools/verify.sh 순차 실행에서 통과했다(근거: Assets/Core/Tests/EditMode/ContentBootstrapTests.cs의 고정 임시 경로).
