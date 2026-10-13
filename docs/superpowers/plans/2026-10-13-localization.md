# 한국어·영어 현지화 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

사람 검토용 개요: [HTML](2026-10-13-localization.html)

**Goal:** 번역 문구와 설명 의미 조립을 분리하여 CSV 템플릿만 바꿔 카드 표현을 변경하고 한국어·영어를 전환한다.

**Architecture:** Simulation은 키·인수·대상으로 이루어진 불변 설명 요청을 만든다. Unity는 공식 CSV Import와 String Tables·Smart Strings로 요청을 렌더링한다. 기존 문자열 UI는 렌더링 결과만 받는다.

**Tech Stack:** C# 9, NUnit 헤드리스, Unity 6000.5.2f1, Unity Localization·필수 Addressables 의존, UTF-8 CSV.

**Spec:** [승인 설계 상세](../specs/2026-10-13-localization-design.md), 2026-10-13 사용자 승인 및 직접 작업 시작 지시. 실행 방식: 현재 세션에서 직접 구현.

## 상세

### Global Constraints

- 한국어·영어가 첫 구현 언어이며 짧은 표현/서술형 선택은 아직 하지 않는다. 표현 방식은 CSV 템플릿으로 바꾼다.
- 코어·Simulation은 UnityEngine과 Unity Localization을 참조하지 않는다. 규칙 RNG·전투 상태는 언어 변경에 관여하지 않는다.
- 카드 규칙은 기존 JSON을 유지한다. 기존 JSON 표시명과 한국어 CSV는 일치 검사한다.
- 외부 신규 폰트는 설치하지 않는다. Localization과 필수 의존 설치는 승인 범위에 포함한다.
- 전용 워크트리와 codex/localization 브랜치에서만 Unity 경로를 수정한다. master 머지는 별도 승인이다.
- 전체 구현 완료 전까지 기존 한국어 경로를 유지하며 단계별로 호출자를 전환한다. 임시 호환은 마지막 전환 작업에서 제거한다.

### Review Focus

- 언어 변경 직전 시작한 비동기 작업: 옛 locale 결과가 새 UI를 덮지 않는다(Task 4).
- 열린 보상·툴팁·더미 창: 전투를 재생성하지 않고 표시만 갱신한다(Task 5).
- 반복 CSV 가져오기와 키 삭제: 기존 ID·GUID 안정성을 지키고 오래된 번역을 남기지 않는다(Task 3).
- 조건 성공값·SkipOnBasic·소비량 보상: 문장을 바꿔도 효과 의미·수치를 잃지 않는다(Task 2).
- 번역자가 쉼표·줄바꿈·복수형을 넣는 경우: CSV 왕복과 실제 Smart String 결과가 보존된다(Task 3·4).

### Task 1: 언어 중립 요청 계약

Files: 새 `Assets/Core/Simulation/Descriptions/LocalizedTextRequest.cs`, `LocalizedCardDescription.cs`, `Assets/Core/Tests/EditMode/LocalizedTextRequestTests.cs`, 각 .meta.

Interfaces: `TranslationTable`(Content/Descriptions/UI); `LocalizedTextRequest(TranslationTable table, string key, IReadOnlyDictionary<string, object> arguments = null)`, `Table`, `Key`, `Arguments`. 인수 값은 string/int/long/decimal/중첩 LocalizedTextRequest로 제한한다. `LocalizedDescriptionLine(CardTargetKey? target, IReadOnlyList<LocalizedTextRequest> sentences)`와 `LocalizedCardDescription(IReadOnlyList<CardTargetKey> targetEntries, IReadOnlyList<LocalizedDescriptionLine> lines)`는 입력을 복사한다.

- [x] 테스트: `Requests_preserve_semantics_without_sentence_copy`에서 상태명 중첩 요청·amount=1이 그대로 보존되고 완성 문장이 포함되지 않는지 확인한다.
- [x] 테스트: `Requests_snapshot_arguments`, `Requests_reject_invalid_contracts`, `Documents_snapshot_lines_and_targets`로 외부 컬렉션 변경·키 누락·빈 인수명·null 값·임의 가변 객체를 검사한다.
- [x] `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter LocalizedTextRequestTests --nologo` 실행: 미구현 타입 때문에 실패 확인.
- [x] 위 계약을 순수 C# 불변 값으로 구현한다. 문자열 포매터·locale 상태는 추가하지 않는다.
- [x] 같은 필터와 `Tools/verify.sh` 통과 후 한국어 메시지로 커밋한다.

### Task 2: 효과에서 언어 중립 설명 문서 생성

Files: `DescriptionContracts.cs`, `DescriptionComposer.cs`, `KoreanDescriptionCatalog.cs`, `BuiltInEffectDescriptionHandlers.cs`, `BuiltInInterventionDescriptionHandlers.cs`, 새 `ConditionDescriptionRequests.cs`, `DescriptionRequestCatalog.cs`, `LocalizedDescriptionComposerTests.cs`.

Interfaces: 최종 계약은 `IEffectDescriptionHandler.Describe(...)`가 target·LocalizedTextRequest를 반환, `IInterventionDescriptionHandler.Describe(...)`는 LocalizedTextRequest 반환, `DescriptionComposer.Compose(CardDefinition, DescriptionRequestCatalog)`는 LocalizedCardDescription 반환. 기존 문자열 경로는 단계별 호환 후 Task 5에서 제거한다. `DescriptionRequestCatalog.CreateDefault(StatusContentCatalog)`는 기존 등록 효과·개입 키를 모두 등록한다.

- [x] 테스트를 먼저 작성: `Venom_thrust_keeps_damage_and_status_as_requests`는 enemy FrontOne, 피해 amount=2, status.poison.name/amount=1을 검사한다. `Toxic_reclaim_preserves_consumption_requirement`는 UpTo 소비량·요구량·대상 두 줄을 검사한다.
- [x] 기본·성공 수치·SkipOnBasic·AllOf·피해 속성·상태 수명·대형 이동·개입·변형 수치 및 모든 콘텐츠 요청을 검증하고 미등록 조건은 실패시킨다.
- [x] 새 테스트 실패 확인 후 각 기존 핸들러에 의미 요청 생성을 이행한다. 문자열의 문장 순서·마침표는 CSV용 문장 래퍼 요청으로 옮긴다.
- [x] 필터 `LocalizedDescriptionComposerTests`와 전체 verify 통과 후 커밋한다.

### Task 3: 공식 패키지·CSV 저작 파이프라인

Files: `Packages/manifest.json`, `packages-lock.json`(Client API 생성), 새 `Assets/Unity/Editor/Localization/LocalizationCsvImporter.cs`, `LocalizationCsvValidator.cs`, `Assets/Unity/Localization/` 설정·ko/en locale·String Tables, `Localization/Csv/{Content,Descriptions,UI}.csv`, `Assets/Tests/UnityEditMode/LocalizationCsvTests.cs`.

Interfaces: `LocalizationCsvImporter.ImportAll()`는 공식 CSV Import API로 테이블을 동기화한다. `LocalizationCsvValidator.Validate()`는 중복·누락·placeholder 계약·JSON 이름 일치 오류 목록을 반환한다. 에디터 설정에는 원본 CSV와 테이블을 연결하고 재가져오기 시 Id·GUID를 유지한다.

- [ ] `Csv_round_trip_preserves_multiline_and_ids`, `Deleted_keys_are_removed`, `Missing_translation_or_argument_contract_fails` 테스트를 먼저 작성한다.
- [ ] Unity registry 호환 버전 확인 후 `UnityEditor.PackageManager.Client`로 Localization을 설치한다. 설치 콜백·domain reload·lock 파일을 확인한다.
- [ ] 기존 전투·보상·결과 문구를 수집해 ko/en 원본 CSV를 작성한다. 공식 Import와 요청 계약에 따른 Smart 플래그 설정을 구현한다.
- [ ] 실제 Unity EditMode 필터 `LocalizationCsvTests`에서 CSV 왕복·재실행·누락 검증 후 커밋한다.

### Task 4: Unity 문장 렌더·locale 수명

Files: 새 `Assets/Unity/Scripts/Text/UnityLocalizedTextRenderer.cs`, `LocalizationBindings.cs`, `LanguageSelectionView.cs`, `LocalizedDescriptionRendererTests.cs` 및 설정·언어 선택 프리팹.

Interfaces: `UnityLocalizedTextRenderer.RenderAsync(LocalizedTextRequest, Locale)`와 `RenderAsync(LocalizedCardDescription, Locale)`는 각각 string·CardDescriptionLayout을 반환한다. `LocalizationBindings`는 테이블 역할을 직렬화 TableReference와 연결한다. 언어 선택은 LocalizationSettings.SelectedLocale만 변경하고 저장한다.

- [ ] 동일 의미 요청을 CSV 단축형·서술형 두 테스트 템플릿으로 렌더링해 서로 다른 문장을 얻는 테스트를 먼저 작성한다. 제품에는 형식 선택 옵션을 만들지 않는다.
- [ ] 중첩 상태명, 조건·복수형·목록, 초기화, fallback, 지원하지 않는 저장 locale과 뒤늦게 완료된 이전 렌더 결과를 테스트한다.
- [ ] 공식 Smart Strings를 호출해 locale 고정 렌더를 구현한다. 전투 상태·RNG를 건드리지 않는다.
- [ ] `LocalizedDescriptionRendererTests` Unity EditMode 통과 후 커밋한다.

### Task 5: 실제 표시 경로 전환

Files: `CardPresentation.cs`, `BattlePresenter.cs`, `BattleScreenController.cs`(배선), `BattleHudView.cs`, `UnitView.cs`, `RewardChoiceView.cs`, `CombatResultView.cs`, `PlaytestKoreanText.cs`, `TimelineTextFormatter.cs`(호환), 관련 프리팹·전투 씬·표시 테스트. 필요한 갱신 컴포넌트는 책임별로 Text 폴더에 둔다.

Interfaces: 카드·안내 문구의 원본 요청을 표시 바인딩이 보관하고 locale 변경 때 렌더링을 다시 요청한다. 기존 UI는 완성 CardDescriptionLayout을 받는다. 정적 라벨은 LocalizeStringEvent, 폰트는 Asset Tables로 배선한다.

- [ ] 언어 변경 전후 손패·레일·적/파티 이름·상태·HUD·보상·결과·열린 툴팁·더미 표시 갱신과 세션·RNG 불변 테스트를 먼저 작성한다.
- [ ] 기존 controller에는 호출·배선만 추가한다. 필요한 표시 변환은 전용 컴포넌트에서 처리한다.
- [ ] 기존 한국어 전용 UI 경로를 제거하고 테스트·개발 로그의 사용처를 새 계약에 맞춘다. 테스트 전용 화면의 영어 번역은 범위 밖이다.
- [ ] 실제 사용 중 프리팹·씬의 고정 문구를 배선하고 사용자의 글꼴 변경은 복사하지 않는다.
- [ ] 전체 verify·Unity EditMode·Addressables 빌드 후 커밋한다.

### Task 6: 검토·인계

Files: 현행 설명 설계와 색인, 승인 설계 상태, 본 계획 체크박스.

- [ ] 전체 diff를 설계의 각 완료 조건과 비교하여 검토한다. 오류가 발견되면 회귀 테스트 후 수정한다.
- [ ] `Tools/verify.sh`와 Unity EditMode XML의 total/passed/failed 및 Addressables 빌드를 확인한다.
- [ ] 실제 ko/en 화면의 Play 검수 항목과 한계를 사용자에게 전달한다. UI 시각 승인을 대신 주장하지 않는다.
- [ ] 완료 계획은 규칙 20에 따라 보관하며 색인을 같은 커밋에서 갱신한다. master는 사용자 머지 승인 전까지 그대로 둔다.
