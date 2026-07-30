# Fate Weaver — 무작위 10장 시작 덱 구성 설계

- 작성일: 2026-07-30
- 문서 유형: `design`
- 주 도메인: `card-content`
- 상태: `current`
- 관련 권위 문서:
  - `docs/superpowers/specs/2026-07-29-starter-pool-so-authoring-design.md`
  - `docs/superpowers/specs/2026-07-20-character-card-pools-design.md`

## 1. 목적

새로 준비한 22장 `StarterPool` 가운데 역할별로 중복 없이 카드를 한 번 무작위 추첨하고, 그 결과를
기존 10장 `StarterDeck.asset`의 고정 구성으로 사용한다. 게임 실행마다 다시 추첨하지 않으며,
추첨 결과는 에셋과 작업 기록에 남긴다.

## 2. 승인된 덱 구성

최종 덱은 서로 다른 카드 10종을 각 1장씩 포함한다.

| 분류 | 후보 수 | 선택 수 |
|---|---:|---:|
| 공격 | 4 | 2 |
| 방어 | 4 | 2 |
| 조작 | 4 | 2 |
| 독 | 10 | 4 |
| 합계 | 22 | 10 |

역할 분류는 카드 아이디어 노트의 태그와 승인된 카드 설계를 따른다.

- 공격: `vanguard_slash`, `probing_strike`, `delayed_strike`, `riposte`
- 방어: `parry_strike`, `quick_cover`, `early_guard`, `foresight`
- 조작: `hasten`, `delay`, `crossover`, `breather`
- 독: `venom_thrust`, `last_drop`, `spore_veil`, `spread_culture`, `toxic_reclaim`,
  `condensed_burst`, `distill`, `early_onset`, `stable_culture`, `posthumous_spread`

하이브리드 효과가 있는 `parry_strike`와 `probing_strike`는 효과 종류가 아니라 카드 노트의 주 역할 태그로
분류한다.

## 3. 추첨 방식

각 분류 안에서 후보마다 운영체제 난수로 생성한 임의 키를 하나씩 부여하고, 키 정렬 순서의 앞에서
필요한 수만큼 선택한다.

- 분류별 추첨은 서로 독립이다.
- 같은 분류 안에서 중복 선택하지 않는다.
- 추첨은 구현 시 한 번만 수행한다.
- 선택된 카드 ID와 추첨 키를 구현 기록에 남긴다.
- 덱 에셋에 반영된 뒤에는 게임 실행이나 시더 재실행으로 다시 추첨하지 않는다.

고정 시드 기반 재추첨이나 런타임 무작위 덱 구성은 이번 범위에 포함하지 않는다.

승인된 1회 추첨 기록은 다음과 같다. 각 역할에서 키를 오름차순으로 정렬하고 앞의 2/2/2/4장을 선택했다.

| 역할 | 무작위 키 | 카드 ID | 선택 |
|---|---|---|---|
| 공격 | `2dbc79f3152c0ed007ef5efc18bb47d6` | `probing_strike` | 예 |
| 공격 | `2e982e12d5c2bebc739ce7d6edad677a` | `delayed_strike` | 예 |
| 공격 | `4aa0e3b19709f57d8199e8ef7d69cc2d` | `riposte` | 아니오 |
| 공격 | `734b834f12e737d61f6415a88e6fc6ea` | `vanguard_slash` | 아니오 |
| 방어 | `478fd58074130b003096ce93daab7605` | `quick_cover` | 예 |
| 방어 | `53177139463d9467ef4d28cd257601e9` | `early_guard` | 예 |
| 방어 | `daed39524a8f73366e8a534fa6230f22` | `parry_strike` | 아니오 |
| 방어 | `fe1413e30c599d58b539d96c403730a9` | `foresight` | 아니오 |
| 조작 | `2de87e7f2f2cff12d3495dadfdc15ee7` | `breather` | 예 |
| 조작 | `36bbb8ac4104fb71438428e647ed9293` | `hasten` | 예 |
| 조작 | `968f785375eb633b7f272a284a402d8b` | `crossover` | 아니오 |
| 조작 | `ff08c04aa7eb546dfd68592897ead2f8` | `delay` | 아니오 |
| 독 | `0698a911914f05e45e1f4a356267a953` | `toxic_reclaim` | 예 |
| 독 | `2d0f68d50354daac58fcd2d12f846ae7` | `early_onset` | 예 |
| 독 | `4579d5c704ebf728b7abed933badbde9` | `spore_veil` | 예 |
| 독 | `872caaef5462c11582a1e5fab6604a78` | `last_drop` | 예 |
| 독 | `8f3130b05c109f5e069d6540d345491c` | `stable_culture` | 아니오 |
| 독 | `95c42dbd8b58126af69afae216a4f250` | `condensed_burst` | 아니오 |
| 독 | `aa064f8fbf38f1eb0c46feb480a9dba9` | `venom_thrust` | 아니오 |
| 독 | `ceac563bde871c7356d693fefd27ad6e` | `spread_culture` | 아니오 |
| 독 | `d0e81489db22570bb635b70c75352ecb` | `distill` | 아니오 |
| 독 | `d5712f5a2f3088b323369c2ffebdebe5` | `posthumous_spread` | 아니오 |

덱에는 선택 행을 역할 순서로 고정한다.

```text
probing_strike
delayed_strike
quick_cover
early_guard
breather
hasten
toxic_reclaim
early_onset
spore_veil
last_drop
```

## 4. 에셋 변경

현재 메인 Unity 체크아웃에는 시더가 생성한 다음 미커밋 콘텐츠가 있다.

- `Assets/Unity/CardSO/Player/StarterPool.asset`
- `Assets/Unity/CardSO/Player/StarterPool/` 아래 22개 `CardAsset`
- 각 에셋과 폴더의 `.meta`
- Inspector에서 임시로 비워진 `StarterDeck.asset`

구현 브랜치에서는 메인 체크아웃의 생성 에셋과 `.meta`를 그대로 복사해 GUID를 보존한다. 복사 전후
파일 해시를 비교하고, 메인 체크아웃의 사용자 변경은 삭제하거나 되돌리지 않는다.

`StarterDeck.asset`은 다음 계약으로 완전히 다시 작성한다.

- `Id = "starter"` 유지
- `Entries` 정확히 10개
- 추첨된 서로 다른 카드 참조 10개
- 모든 `Count = 1`
- null 참조, 중복 카드, 0 이하 장수 없음

`StarterPool.asset`은 22장 후보 풀로 그대로 유지하며, 실제 덱 구성을 표현하는 용도로 바꾸지 않는다.

## 5. 생성 C#과 런타임

Unity의 `Fate Weaver/Generate Cards from SO` 경로로 다음 스냅샷을 갱신한다.

- `GeneratedCards.StarterDeck()` — 추첨된 10장
- `GeneratedCards.StarterPool()` — 전체 22장

Unity 런타임은 계속 `StarterDeck.asset`과 연결된 `CharacterAsset`을 사용한다. 생성 C#은 헤드리스
검증용이며 런타임 덱 원본이 아니다.

## 6. 검증

자동 검증은 다음을 확인한다.

- `StarterPool.asset`이 정확한 22개 ID를 한 번씩 포함한다.
- `StarterDeck.asset`이 정확히 10장이고 모든 `Count`가 1이다.
- 덱 카드 ID가 중복되지 않는다.
- 공격 2장, 방어 2장, 조작 2장, 독 4장이다.
- 덱의 모든 참조가 `StarterPool.asset` 안에도 존재한다.
- `GeneratedCards.StarterDeck()`이 SO 덱과 같은 규칙 서명을 가진다.
- 전체 헤드리스 테스트와 Unity EditMode 테스트가 통과한다.

## 7. 범위 제외

- 카드 능력과 수치 변경
- 22장 풀의 추가·삭제
- 게임 시작마다 무작위 덱 생성
- 카드별 장수를 2장 이상으로 조정
- 기존 캐릭터의 덱 참조 변경
- 카드 아트 추가

## 8. 완료 조건

- 추첨 결과와 추첨 키가 구현 기록에 남아 있다.
- 10장 덱과 22장 풀이 각각의 계약을 만족한다.
- 기존 `StarterDeck.asset`의 과거 카드 8종은 최종 덱 참조에서 제거된다.
- 메인 체크아웃에서 생성된 GUID가 보존된다.
- 관련 생성 C#과 자동 테스트가 동기화된다.
- 작업 브랜치는 검증된 커밋 상태이고, 메인 병합은 사용자 승인 후에만 수행한다.
