# 한국어·영어 번역 저작과 검증

## 원본과 표시 경계

번역 원본은 저장소의 `Localization/Csv/Content.csv`, `Descriptions.csv`, `UI.csv`다.
`Content`는 이름·용어, `Descriptions`는 효과·조건·문장 연결 템플릿, `UI`는 버튼·안내 문구를 갖는다.
Unity 테이블은 이 CSV에서 가져온 결과다. Key·Id·Shared Comments·Korean(ko)·English(en) 열을 유지한다.
Unity에서 배정한 Id를 바꾸지 않는다.

의미 요청은 순수 C#이 만들고 Unity Smart Strings가 최종 문장을 만든다. 근거:
`Assets/Core/Simulation/Descriptions/DescriptionComposer.Requests.cs`의 `Compose`,
`Assets/Unity/Scripts/Text/UnityLocalizedTextRenderer.cs`의 `Render`.

## 문구 변경

예를 들어 `Descriptions.csv`의 `effect.apply_status` 한국어 값을
`{status} {amount}`에서 `{status} {amount}을 부여합니다`로 바꾸면 된다.
현재 `sentence` 템플릿이 마침표를 붙이므로 효과 템플릿에 마침표를 중복해서 넣지 않는다.
문장 마침까지 바꾸고 싶으면 `sentence` 템플릿을 함께 바꾼다. 영어 문구는 별도 열에서 저작한다.

카드 설명 줄 머리말(`◆ 적군 전방 1명`)은 `Descriptions.csv`의 `target.{진영}.{범위}` 12행이다.
진영·범위를 따로 번역해 잇지 않는다 — 영어는 어순·복수형이 달라(`1 front enemy`, `All allies`) 조합
단위로 저작한다. `DescriptionLineView` 프리팹의 `_targetLabels`가 인스펙터 `LocalizedString`으로 이 키를
가리키며, 기호(●◆)와 색만 프리팹에 직접 둔다. 근거: `DescriptionLineView.cs`의 `Bind`.

카드의 효과·수치는 `Assets/StreamingAssets/Content`의 JSON을 계속 수정한다.
수치를 번역 CSV에 복사하지 않는다. 카드 이름 등 JSON 기본 표시명은 한국어 CSV와 같게 유지한다.
설명 인수 이름을 번역하거나 삭제하지 않는다. 언어별 어순·복수형 표현은 변경할 수 있다.

## Unity로 가져오기

1. CSV를 UTF-8로 저장한다. 쉼표·줄바꿈이 있는 셀은 CSV 인용 규칙을 따른다.
2. Unity 메뉴 `Fate Weaver > Localization > Import CSV`를 실행한다.
3. 실패하면 콘솔의 중복 키·빈 번역·인수 불일치·JSON 이름 불일치 오류를 수정한다.
4. CSV와 생성된 `Assets/Unity/Localization` 테이블·meta를 함께 커밋한다.
5. 번역 자원이 바뀌면 Addressables 콘텐츠를 다시 빌드한다.

가져오기 구현은 `Assets/Unity/Editor/Localization/LocalizationCsvImporter.cs`의 `ImportAll`,
검증은 `LocalizationCsvValidator.cs`의 `Validate`다. 공식 CSV Import/Export를 사용하고
재실행 시 기존 ID·GUID를 보존하며 삭제한 CSV 키를 테이블에서도 제거한다.
문장 템플릿은 가져오기 후 Smart 플래그를 명시적으로 설정한다.

Unity 테이블에서 편집할 경우 공식 Export 결과를 CSV에 반영한 뒤 다시 가져온다.
생성된 테이블만 고치고 CSV에 반영하지 않으면 다음 가져오기 때 원본 CSV로 돌아간다.

## 언어 선택과 폰트

전투 화면의 한국어/English 버튼으로 언어를 선택한다. 선택은 `fateweaver.locale` PlayerPrefs에
저장된다. 저장값이 없거나 지원하지 않으면 시스템 언어, 그다음 한국어를 선택한다.
근거: `LanguageSelectionView.cs`의 `Choose`, `LocalizationCsvImporter.cs`의 `EnsureSettings`.

폰트는 `FW_Fonts` Asset Table의 `font.main`을 사용한다. 현재 두 locale 모두 기존 글꼴을 복사한
`Assets/Unity/Localization/Fonts/Main.asset`을 가리킨다. 새 외부 폰트를 추가한 것이 아니다.
근거: `Assets/Unity/Editor/Localization/LocalizationAuthoring.cs`의 `Author`.

## 검증 명령

워크트리에서 실행한다. 메인 체크아웃의 Unity 에디터 경로는 변경하지 않는다.

```bash
Tools/verify.sh
```

Unity EditMode/PlayMode 배치는 [Unity 배치 실행](unity-batch-runs.md)을 따른다.
새 현지화 테스트는 `LocalizationPipelineTests`, `LocalizationSceneTests`, `LocalizationPlayModeTests`다.
PlayMode 테스트는 열린 더미·보상·고정 툴팁, 표시 수치 보존, 언어 저장과 빠른 전환,
전투 세션·시드 RNG 불변을 확인한다.

Addressables를 헤드리스로 빌드할 때는 같은 에디터 실행 파일에 아래 인수를 준다.

```text
-batchmode -projectPath <워크트리> -executeMethod FateWeaver.Unity.Editor.LocalizationAuthoring.BuildBatch -logFile <로그>
```

빌드 함수가 종료 코드를 설정한다. 로그의 성공 메시지와 종료 코드를 확인한다.
사용자는 Play에서 두 언어의 카드 넘침·줄바꿈·글자 누락·언어 버튼 배치를 검수한다.

## 진단과 호환 범위

기존 `KoreanDescriptionCatalog`·`Describe` API는 헤드리스 진단·개발 로그·테스트 전용
샌드박스의 호환용으로 남아 있다. 실제 전투 화면은 `BattlePresenter`의 `FromLocalized` 경로를 쓴다.
테스트 전용 화면과 콘솔 로그의 영문 번역은 이번 범위에서 제외한다.
