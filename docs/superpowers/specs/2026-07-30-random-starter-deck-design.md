# Fate Weaver — 무작위 10장 시작 덱 구성 설계

- 작성일: 2026-07-30
- 문서 유형: `design`
- 주 도메인: `card-content`
- 상태: `current`
- 관련 권위 문서:
  - `docs/superpowers/archive/specs/2026-07-29-starter-pool-so-authoring-design.md` (보관: SO 저작 파이프라인은 제거됨, 22장 설계 의도만 참고)
  - `docs/superpowers/specs/2026-07-20-character-card-pools-design.md`

## 1. 목적

새로 준비한 22장 `StarterPool` 가운데 역할별로 중복 없이 카드를 한 번 무작위 추첨하고, 그 결과를
10장 시작 덱(`Decks/starter.json`, 저작 당시에는 `StarterDeck.asset`)의 고정 구성으로 사용한다. 게임 실행마다 다시 추첨하지 않으며,
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

## 4. 콘텐츠 위치

> **2026-08-04 갱신.** 이 절은 원래 SO 에셋(`Assets/Unity/CardSO/Player/StarterPool*`)을 가리켰다.
> 계획 3b가 그 경로를 통째로 지웠고, 현재 위치는 아래와 같다.

- `Assets/StreamingAssets/Content/Cards/*.json` — 22장 풀 카드 (+ fixture 4)
- `Assets/StreamingAssets/Content/Pools/starter.json` — 22장 풀의 id 목록
- `Assets/StreamingAssets/Content/Decks/starter.json` — 추첨으로 고정된 10장

이 문서가 설계한 **역할별 2/2/2/4 추첨을 한 번만 돌려 고정한다**는 규칙 자체는 유효하며,
추첨 결과가 `Decks/starter.json`에 박혀 있다는 점만 달라졌다.

`Decks/starter.json`의 계약은 다음과 같다. (원문의 GUID 보존·`.meta` 복사 절차는 SO 시절의 것으로,
JSON에는 GUID가 없어 불필요해졌다.)

- `id = "starter"` 유지
- 카드 id 정확히 10개
- 서로 다른 카드 10종
- 중복 id 없음

`Pools/starter.json`은 22장 후보 풀로 그대로 유지하며, 실제 덱 구성을 표현하는 용도로 바꾸지 않는다.

## 5. 런타임

> **2026-08-04 갱신.** 이 절은 원래 `Fate Weaver/Generate Cards from SO` 메뉴로 갱신하던
> `GeneratedCards.StarterDeck()`·`StarterPool()` 스냅샷을 설명했다. 계획 3b가 코드 생성 경로를
> 통째로 지웠으므로 갱신할 스냅샷이 없다.

런타임은 `ContentBootstrap.Load`가 읽은 `GameContent.Decks`/`Pools`를 그대로 쓴다. 별도의 생성
단계도, 헤드리스 전용 스냅샷도 없다 — 헤드리스와 Unity가 같은 JSON을 읽는다.

## 6. 검증

자동 검증은 다음을 확인한다.

- `Pools/starter.json`이 정확한 22개 ID를 한 번씩 포함한다.
- `Decks/starter.json`이 정확히 10장이다.
- 덱 카드 ID가 중복되지 않는다.
- 공격 2장, 방어 2장, 조작 2장, 독 4장이다.
- 덱의 모든 참조가 `Pools/starter.json` 안에도 존재한다.
- 덱·풀의 모든 카드 ID가 `Cards/*.json`에 실재한다 (로더가 부팅 시 거부한다).
- 전체 헤드리스 테스트와 Unity EditMode 테스트가 통과한다.

## 7. 범위 제외

- 카드 능력과 수치 변경
- 22장 풀의 추가·삭제
- 게임 시작마다 무작위 덱 생성
- 카드별 장수를 2장 이상으로 조정
- 기존 캐릭터의 덱 참조 변경
- 카드 아트 추가

## 8. 완료 조건

- 추첨 결과와 추첨 키가 구현 기록(§3 표)에 남아 있다. **충족**
- 10장 덱과 22장 풀이 각각의 계약을 만족한다. **충족** — `DeckPoolCharacterContentTests`가 잠근다
- ~~기존 `StarterDeck.asset`의 과거 카드 8종은 최종 덱 참조에서 제거된다.~~ **충족** (에셋 자체가
  `Decks/starter.json`으로 대체됨)
- ~~메인 체크아웃에서 생성된 GUID가 보존된다.~~ **무효** — JSON에는 GUID가 없다 (계획 3b)
- ~~관련 생성 C#과 자동 테스트가 동기화된다.~~ **무효** — 코드 생성 경로가 사라졌다 (계획 3b)
- 작업 브랜치는 검증된 커밋 상태이고, 메인 병합은 사용자 승인 후에만 수행한다. **충족**
