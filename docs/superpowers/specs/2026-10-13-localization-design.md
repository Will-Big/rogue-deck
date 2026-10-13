# 한국어·영어 현지화 전환 제안

사람 검토용 개요: [HTML](2026-10-13-localization-design.html)

## 상세

### 상태와 범위

2026-10-13 작성. 2026-10-13 사용자가 승인하고 전용 워크트리에서 구현 시작을 지시했다. 구현을 완료했고 자동 검증을 통과했다. 사용자 화면 검수와 master 머지는 별도다. 사용자가 지정한 첫 언어는 한국어·영어다. 카드 규칙 JSON 저작, 효과 기반 설명 자동 생성, 순수 C# 규칙 경계를 유지하면서 CSV 저작과 Unity Localization을 도입한다. 제품의 다국어 설명 경로는 이 문서가 기존 한국어 전용 설계의 문장 생성 부분을 대체한다. 기존 레지스트리 설계는 진단·샌드박스 호환 API에 적용한다.

첫 범위는 전투·보상·결과 화면의 카드명, 효과·조건·개입 설명, 상태명·툴팁, 캐릭터·적 이름, HUD·버튼·안내 문구, 언어 선택·저장이다. 테스트 전용 화면과 개발자 콘솔 로그의 영문 번역은 제외한다. 기존 한국어 로그 포매터의 새 설명 계약 적응은 포함한다. 태그처럼 플레이어에게 표시하지 않는 규칙·저작 식별자는 번역하지 않는다.

### 확인한 현행 구조

- `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs:15`는 `KoreanDescriptionCatalog`를 받고, 같은 파일 `:108`은 접미사·마침표·조건절을 직접 이어 붙인다.
- `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs:14`에 피해 문구가, `KoreanDescriptionGrammar.cs:14`에 한국어 조건절이 들어 있다.
- `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs:46`는 상태 콘텐츠의 표시명을 설명 레지스트리에 등록한다.
- `Assets/StreamingAssets/Content/Cards/venom_thrust.json:4`는 카드 이름을, `Statuses/poison.json:4`는 상태 표시명을 보관한다.
- `Assets/Unity/Scripts/Cards/CardPresentation.cs:88`는 `PlaytestKoreanText`를 통해 이름을 얻고 `:92`는 설명 컴포저를 호출한다. `Assets/Unity/Scripts/Text/PlaytestKoreanText.cs:23`에는 별도 한국어 이름 매핑이 있다.
- `Assets/Unity/Scripts/Battle/BattleHudView.cs:60`은 한국어 HUD 문구를 직접 만든다. `Assets/Unity/Prefabs/RewardChoiceView.prefab:150` 등에는 고정 한국어 텍스트가 직렬화되어 있다.
- `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs:44`는 한국어 개발 로그를 만든다.
- `Packages/packages-lock.json`의 dependencies에 `com.unity.localization`과 `com.unity.addressables`가 없다. `ProjectSettings/ProjectVersion.txt`의 에디터 버전은 6000.5.2f1이다.
- `Tests/Headless/FateWeaver.Tests.Headless.csproj`는 Simulation 소스도 헤드리스에서 컴파일한다. Unity 의존을 이곳으로 옮길 수 없다.

### 책임과 계약

1. 기존 효과·개입 설명 핸들러는 레지스트리 확장 방식을 유지하고, 제품 경로의 `Request` 메서드에서 최종 문자열 대신 불변 `LocalizedTextRequest`를 반환한다. 기존 `Describe`와 한국어 카탈로그는 헤드리스 진단·개발 로그·테스트 전용 샌드박스 호환에만 남긴다. 구현 근거: `BuiltInEffectDescriptionHandlers.cs`의 `Request`, `DescriptionRequestCatalog.cs`의 `CreateDefault`, `BattlePresenter.cs`의 `For`. 요청은 테이블 역할, 키, 이름 있는 인수로 구성한다. 인수는 숫자·문자열 식별자·중첩 요청으로 제한하며 Unity 객체를 포함하지 않는다.
2. `DescriptionComposer`는 효과 데이터에서 언어 중립 설명 문서를 생성한다. 대상별 묶음, 기본·성공 효과, SkipOnBasic, 소비 요구량·비례 보상, 상태 수명·피해 속성을 보존한다. 조건도 의미별 키와 인수로 표현한다. 알려지지 않은 핸들러·조건은 검증 실패시킨다.
3. `UnityLocalizedTextRenderer`는 요청을 선택 언어의 String Table·Smart Strings로 렌더링한다. 중첩 요청은 같은 locale을 고정한 한 렌더 패스에서 처리한다. 대상 glyph는 번역하지 않고 기존 구조화 대상 값으로 표시한다. 언어별 문장 마침·조건과 본문의 순서·목록 연결은 CSV 템플릿이 정한다. 한국어 어간을 영어에 재사용하지 않는다.
4. `LocalizationSettings`가 현재 locale의 유일한 원본이다. 프리팹의 언어 선택 컴포넌트는 SelectedLocale만 변경하고 선택을 저장한다. 저장값이 없으면 시스템 언어, 지원하지 않으면 한국어를 선택한다.
5. 정적 라벨은 `LocalizeStringEvent`, 폰트는 Asset Table을 사용한다. 카드·상태·유닛·안내 메시지는 렌더링 전 요청을 유지하고 locale 변경 시 다시 렌더링한다. 문자열만 저장하면 갱신할 수 없으므로 안내 메시지도 키와 인수를 보관한다. 열린 보상·툴팁·더미 창도 갱신 대상이다.
6. 기존 `BattleScreenController`는 배선·호출 순서만 맡는다. CSV 가져오기, 문장 변환, locale 상태를 추가하지 않는다. 선택 언어 갱신은 전용 표시 컴포넌트가 담당한다. 조정자에 다른 책임이 필요해지면 규칙 30에 따라 구현 전에 보고한다.

### CSV와 Unity 자원

제안 원본 경로는 `Localization/Csv/`이며 역할별 `Content.csv`, `Descriptions.csv`, `UI.csv`를 둔다. Unity 공식 CSV 열 형식인 `Key,Id,Shared Comments,Korean(ko),English(en)`을 사용한다. 번역 원본은 CSV 하나이며 Unity Table 에셋은 가져온 결과로 함께 버전 관리한다. 테이블에서 직접 편집한 결과를 채택할 때는 공식 Export로 CSV에 반영한 뒤 재가져오기·검증한다.

Key는 콘텐츠 ID에 연결한다(`card.venom_thrust.name`, `status.poison.name`, `enemy.goblin.name`). JSON의 name/displayName은 기존 콘텐츠 로더·헤드리스 호환용 기본 표시명으로 보존한다. 출시 UI는 CSV를 사용하며 원본 표시명과 한국어 CSV 불일치는 검사 오류로 잡는다. JSON 필드를 제거하는 스키마 변경은 이번 범위가 아니다.

새 CSV 행은 Id를 비워 Unity가 할당하게 하되, 한번 할당된 값과 Table GUID·meta는 보존한다. CSV 원본의 키 삭제도 Table에 반영하고 오래된 행을 검사한다. Unity 공식 CSV Import/Export API를 사용하고 자체 CSV 런타임 로더·번역 테이블 엔진을 만들지 않는다. Smart 여부는 CSV에서 저절로 복원된다고 가정하지 않고 요청 키 계약에 따라 Import 후 명시적으로 설정·검증한다.

테이블 역할과 실제 String Table Collection 참조, 폰트·프리팹은 직렬화 참조로 연결한다. 부팅 시 Localization 초기화·필요 테이블 사전 로딩이 완료된 후 표시한다. 비동기 렌더 응답은 locale 세대와 뷰 바인딩을 확인하여 이전 언어·이전 카드의 응답을 버린다. 언어 변경은 전투 세션을 다시 만들거나 RNG를 사용하지 않는다. 런타임 누락은 한국어 fallback과 개발 로그로 처리하되 검사에서는 양쪽 언어의 누락·빈 문구를 실패시킨다.

Localization 및 그 필수 의존(Addressables 포함)의 설치는 사용자 승인 대상이다(AGENTS 규칙 14). 버전은 구현 시작 시 해당 에디터 지원과 Unity registry를 확인해 고정한다. 폰트는 기존 에셋의 글리프를 확인해 사용하고 신규 외부 폰트가 필요하면 별도 승인을 받는다.

### 대표 실행 예시

`venom_thrust`의 피해 value=2, poison count=1을 읽는다(해당 JSON의 effects). 피해 핸들러는 `effect.damage`와 amount=2, 상태 핸들러는 상태명 요청 `status.poison.name`과 amount=1을 만든다. 컴포저는 같은 적 대상 아래 두 문장 요청을 묶는다. CSV의 한국어 예시 템플릿은 `피해 {amount}.`와 `{status} {amount}.`, 영어 예시 템플릿은 `Deal {amount} damage.`와 `Apply {amount} {status}.`다. Unity가 중첩 상태명을 독/Poison으로 렌더링하면 본문은 `피해 2. 독 1.` / `Deal 2 damage. Apply 1 Poison.`가 된다. 이름은 맹독 찌르기/Venom Thrust이고 대상 glyph는 동일하다. 영어는 검수할 번역 초안이다.

### 검증과 완료 조건

- 헤드리스: 모든 콘텐츠의 설명 요청 생성, 미등록 키, 기본·조건 성공·복합 조건·소비 보상·상태 수명·피해 속성·개입 요청, 변형된 EffectData 수치 반영을 검사한다. 최종 Unity Smart String 문장을 헤드리스에서 검증했다고 주장하지 않는다.
- CSV 검사: UTF-8, 인용·줄바꿈·쉼표, 중복 키·ID, 필수 이름, 양쪽 locale의 빈 문구, 요청 인수와 placeholder 계약을 검사한다. 복수형 템플릿의 문법 차이는 허용하고 인수 이름·타입 계약을 확인한다.
- Unity EditMode: 공식 CSV 가져오기·내보내기 왕복, Smart 플래그, 실제 한국어·영어 렌더, 조건·중첩 요청·복수형, fallback, Addressables 콘텐츠 빌드·참조를 검사한다. 가져오기 재실행 시 ID·참조 안정성을 확인한다.
- locale 변경 전후 전투 상태·이벤트·RNG가 같고 손패·레일·유닛 상태명·보상·열린 툴팁·안내 문구가 새 언어로 바뀌는지 확인한다.
- 전체 `Tools/verify.sh`와 필요한 Unity 배치 검증을 통과한다. 사용자는 Play에서 두 언어의 글자 누락·줄바꿈·카드 넘침·화면 크기를 검수한다.
- 워크트리에서 구현·커밋하고 master 머지는 별도 승인 후 수행한다. 완료 시 기존 한국어 전용 설계의 대체 범위를 기록하고 색인을 갱신한다.

### 대안과 제약

전체 설명 컴포저를 Unity로 옮기면 패키지 사용은 단순해지지만 의미 해석의 헤드리스 검증을 잃는다. 한국어·영어 전용 핸들러를 복제하면 초기 전환은 빠르지만 새 효과마다 구현을 복제한다. 위 제안은 의미 요청을 순수 C#에 두고 문장 렌더만 Unity에 두어 이 두 문제를 피한다.

대신 문자열 반환을 기대하는 테스트와 표시 호출자를 요청 모델에 맞춰 이행해야 한다. 번역만 바꿔도 CSV 가져오기와 Addressables 빌드가 필요하다. 문장 템플릿만으로 모든 언어의 문법을 해결할 수는 없으므로 한국어·영어의 복합 조건·조사·연결 문장을 실제로 검수한다. 한국어 JSON 표시명과 CSV 간 일치 검사가 추가된다.

### 외부 기능 근거

- [Unity Localization 1.5 CSV 공식 문서](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/CSV.html): 공식 CSV 열, Import/Export, 신규 Id 할당.
- [Unity Localization 1.5 Smart Strings 공식 문서](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Smart/SmartStrings.html): 변수 기반 문장과 언어별 포매팅.
