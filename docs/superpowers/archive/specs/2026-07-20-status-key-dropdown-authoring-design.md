# 상태 키 드롭다운 저작 UX 설계

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

- 작성일: 2026-07-20
- 상태: 설계 승인
- 관련 작업: P0-B2 `CardType` 제거 후속 저작 UX 개선

## 1. 목적

`ApplyStatusSpec.Status`는 열린 집합의 `StatusKeyRef.Id` 문자열을 직렬화한다. 현재 Unity Inspector는 이
필드를 자유 텍스트로 노출하므로 `block` 같은 키를 오타 내기 쉽고, 등록 여부를 저장·부팅 검증 전에는 알기
어렵다.

기존 직렬화 스키마와 코어의 열린 레지스트리 모델은 유지하면서, Inspector에서 등록된 상태 키를 선택하는
드롭다운을 제공한다. 이 개선은 카드 런타임 규칙, 상태 지속 시간, 수치, 설명 합성에는 영향을 주지 않는
editor-only 저작 UX 작업이다.

## 2. 현재 구조와 제약

- `StatusKeyRef`는 Unity 직렬화가 가능한 `string Id`를 보유하고, 코어에서는 타입 안전 `StatusKey`로 변환한다.
- `AuthoringValidator`는 `AuthoringContext`의 `StatusRegistry`로 빈 키와 미등록 키를 검사한다.
- `StatusRegistry`는 행동 등록만 제공하고 등록 키 열거 API는 없다.
- `KoreanDescriptionCatalog`는 각 기본 상태의 표시명을 명시적으로 등록한다.
- `EffectSpecDrawer`는 `ApplyStatusSpec`의 자식 프로퍼티를 일반적으로 그리므로, `StatusKeyRef`에 대한 별도
  `PropertyDrawer`를 두면 모든 상태 적용 효과에 동일하게 적용된다.
- 반사 기반 자동 등록이나 문자열 탐색을 도입하지 않는다. 새 상태는 기존처럼 행동 핸들러와 설명을 명시적으로
  등록해야 한다.

## 3. 검토한 접근법

### 3.1 채택: 레지스트리 열거 + `StatusKeyRef` 전용 Drawer

`StatusRegistry`가 등록된 `StatusKey`를 안정된 순서로 열거한다. `AuthoringContext`는 그 목록을
저작 전용 읽기 API로 노출한다. Unity Editor의 `StatusKeyRefDrawer`가 이 목록으로 Popup을 만들고,
`KoreanDescriptionCatalog`의 기존 이름을 `방어 (block)`처럼 표시한다.

등록되지 않은 기존 값은 첫 항목 `Unknown: <key>`로 넣고 현재 선택 상태를 유지한다. 사용자가 등록된 항목을
명시적으로 고르기 전에는 `Id`를 바꾸지 않는다. 따라서 예전·외부 데이터의 값을 조용히 지우지 않으며,
기존 `AuthoringValidator` 오류가 그대로 보존된다.

### 3.2 미채택: `StatusKeyRef.Id`를 enum으로 교체

컴파일 타임 안전성은 생기지만 상태 확장을 위해 enum 중앙 수정과 SO 재직렬화가 필요해 열린 레지스트리
아키텍처에 맞지 않는다. 이 작업의 목적도 입력 오류 감소이지 상태 종류를 닫는 것이 아니므로 채택하지 않는다.

### 3.3 미채택: Drawer 안에 키/한국어 이름 목록을 하드코딩

간단해 보이지만 등록된 런타임 상태와 Inspector 후보가 서로 달라질 수 있다. 상태 추가 때 행동 등록·설명
등록·Editor 목록이라는 세 곳을 바꿔야 하므로 단일 진실 공급원 원칙을 해친다.

## 4. 설계

### 4.1 등록 키와 표시명

`StatusRegistry.RegisteredKeys`는 등록된 키를 `Id`의 ordinal 순서로 반환한다. 반환값은 읽기 전용 스냅샷이며
외부에서 레지스트리를 바꿀 수 없다. `AuthoringContext.RegisteredStatusKeys`는 이 API만 저작 UI에 노출한다.

`StatusDescriptionRegistry.TryResolve`을 추가해 표시명을 안전하게 조회한다. Drawer는 등록 키마다 다음 레이블을
만든다.

```text
방어 (block)
```

설명 등록이 아직 없는 등록 키는 런타임 상태 선택을 막지 않고 `block`처럼 ID만 표시한다. 기본 카탈로그의
검증 책임은 기존 `DescriptionCatalogValidator`가 유지한다.

### 4.2 Dropdown 선택 모델

UI와 분리해 테스트 가능한 editor helper가 Popup 항목과 현재 선택 index를 만든다.

| 현재 `StatusKeyRef.Id` | Popup 첫/선택 항목 | 저장 값 |
|---|---|---|
| 빈 값 | `(상태 선택)` | 사용자가 고르기 전까지 빈 값 |
| 등록 값 `block` | `방어 (block)` | 변경 없음 |
| 미등록 값 `legacy_block` | `Unknown: legacy_block` | 변경 없음 |

사용자가 `(상태 선택)`을 고르면 `Id`는 빈 문자열이 된다. 등록 키를 고르면 해당 `StatusKey.Id`가 저장된다.
미등록 항목은 보존 전용이다. Popup을 다시 열거나 Inspector가 repaint되어도 그 값으로 다시 쓰지 않는다.

### 4.3 Unity Drawer 적용 범위

`[CustomPropertyDrawer(typeof(StatusKeyRef))]`를 Unity Editor 어셈블리에 추가한다. Drawer는 `Id` 자식
`SerializedProperty`만 수정하고 `StatusKeyRef`의 직렬화 필드명이나 SO YAML 구조를 바꾸지 않는다. 따라서
기존 Guard SO의

```yaml
Status:
  Id: block
```

는 그대로이며, Inspector 표현만 텍스트 입력에서 드롭다운으로 바뀐다.

## 5. TDD 및 검증

1. 코어 EditMode 테스트로 기본 `AuthoringContext`가 등록된 상태 키를 안정된 순서로 노출하는지 고정한다.
2. Unity EditMode 테스트에서 editor helper가 등록 키의 한국어 레이블을 만들고, 빈 값·미등록 값을 각각
   `(상태 선택)`과 `Unknown: <key>`로 보존하는지 검증한다.
3. helper를 사용한 Drawer를 구현한다. GUI 테스트는 Popup의 즉시 모드 렌더링 자체가 아니라 선택 모델과
   Unity batchmode 컴파일로 검증한다.
4. 전체 headless 및 Unity EditMode 테스트를 실행한다. 기존 `Unknown status key` 저작 검증 테스트도 유지한다.

## 6. 완료 조건

- [ ] `ApplyStatusSpec.Status`가 Inspector에서 등록 상태 키의 드롭다운으로 나타난다.
- [ ] 기본 키는 한국어 표시명과 ID를 함께 보여 준다.
- [ ] 빈 값과 기존 미등록 문자열은 자동 변경 없이 보존된다.
- [ ] 미등록 문자열은 기존 저작 검증 오류를 계속 낸다.
- [ ] `StatusKeyRef.Id` 및 기존 SO YAML 스키마는 변경하지 않는다.
- [ ] 새 상태를 등록하면 별도 Editor 하드코딩 없이 후보에 나타난다.
- [ ] 전체 headless와 Unity EditMode 테스트가 통과한다.

## 7. 범위 밖

- 상태 키를 enum 또는 ScriptableObject 참조로 변경
- 상태별 수치·지속 시간·대상 필드의 UX 개편
- 누락된 상태 설명을 자동으로 생성하거나 검증 완화
- 기존 미등록 키의 자동 마이그레이션·자동 수정
- 카드 런타임 규칙 또는 상태 행동 변경
