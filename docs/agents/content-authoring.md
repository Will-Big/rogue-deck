# 콘텐츠 저작 — JSON이 원본이다

**언제 읽나:** 카드·상태·덱·풀·캐릭터를 더하거나 고칠 때, ScriptableObject로 저작하려는 생각이
들 때. AGENTS.md 규칙 5가 이 문서를 가리킨다.

## 원본의 위치

```
Assets/StreamingAssets/Content/<종류>/*.json
```

종류는 `Cards`·`Decks`·`Pools`·`Characters`·`Statuses`·`Enemies`·`Battles`다. 전투 규칙(턴당
운명력·파티 규모·생존자 수별 드로우·보상 장수)은 종류 폴더가 아니라 루트 단일 파일
`Content/combat_rules.json`이다.

부팅 시 `ContentBootstrap.Load`가 읽어 코어 데이터로 만든다. **새 카드를 C# 상수로 박지 않는다.**

## SO 카드 저작 파이프라인은 없다

이 문장이 필요한 이유는 **규칙 5가 정반대였기 때문이다.** 첫 커밋(2026-07-15)의 규칙 5는
*"콘텐츠는 코드가 아니라 ScriptableObject로 저작한다"*였고, `ac075e3`(2026-08-04)이 뒤집었다.
그래서 `.archive/`의 오래된 계획을 읽으면 SO 저작을 지시하는 문서를 만나게 된다 — 그것은 폐기된
이력이다.

무엇이 지워졌는지:

| 지워진 것 | 지운 것 |
|---|---|
| `CardAsset` · `CardPoolAsset` · `DeckAsset`과 코드 생성 경로 | 2026-08-03 계획 3b |
| 상태의 코드 기본값 | 계획 3c |
| C# 카드 스펙 | 계획 3d |

`Tools/verify.sh --lint`의 R5 검사가 이 셋의 부활을 막는다.

## 남은 ScriptableObject 셋

`CardPrefabCatalog` · `CardArtCatalog` · `CharacterAsset`.

**표현 자원 전용이며 규칙 수치를 담지 않는다.** id → Sprite, id → Prefab 같은 매핑만 든다.
그 경계를 넘는 순간 — 피해량·비용·확률 같은 것이 SO로 들어가는 순간 — 규칙 5 위반이다.

## AI 세션에서 JSON을 직접 저작한다

카드·콘텐츠의 추가와 수정은 AI 세션에서 `Assets/StreamingAssets/Content/`의 JSON을 직접 편집한다.
인스펙터 카드 저작이나 별도 카드 편집 도구를 만들거나 사용하지 않는다.

1. 기존 JSON과 해당 코어 스펙·로더를 읽어 필드·`key`·참조 id를 확인한다.
2. 요청한 카드·상태·풀·덱·캐릭터·적·편성 JSON을 직접 수정한다.
3. `Tools/verify.sh`로 로더·직렬화·헤드리스 회귀를 검증한다. 밸런스 변경은 규칙 12의 Compare도 확인한다.

효과·조작 JSON의 필드 이름과 구조는 코어 레코드를 그대로 따른다 — 효과 한 줄은 `EffectData`, 조작은
`InterventionActionData`, `payload` 객체는 그 종류의 코어 페이로드 레코드의 속성 이름을 camelCase로 쓴다.
판별자는 `key`이고, 세기 숫자는 `effectValue`, 종류 전용 선택값만 `payload`에 둔다. 카드 JSON은 형식
버전을 갖지 않는다: 형식을 바꾸면 그 커밋에서 모든 카드를 함께 고친다. 시작 조건의 `kind`는 예외다
(닫힌 조건 이름). 근거: [효과 JSON 페이로드 중첩 설계](../superpowers/specs/2026-10-17-effect-json-payload-nesting-design.md).

초안과 검수 메모는 [`docs/card-design/`](../card-design/)에 보관한다. 메모를 런타임 원본으로 쓰지 않는다.

## 풀과 덱은 로더 모양만 같다

`PoolContentLoader`는 *"덱 로더와 같은 형태이되 같은 카드 id가 두 번 오는 것을 거부한다 — 풀은
후보 집합이라 중복이 저작 실수다"*([PoolContentLoader.cs:30](../../Assets/Core/Authoring/Decks/PoolContentLoader.cs)).
**덱은 같은 카드를 여러 장 담을 수 있고 풀은 못 담는다.** 스키마가 닮았다고 한쪽 코드를 복사해
쓰면 이 검증이 조용히 사라진다.

부팅 순서는 **카드 → 덱·풀 → 캐릭터**로 고정이다 — 덱·풀이 카드 id를 참조하므로 카드 카탈로그가
먼저 있어야 한다([DeckContentLoader.cs:30](../../Assets/Core/Authoring/Decks/DeckContentLoader.cs)).
