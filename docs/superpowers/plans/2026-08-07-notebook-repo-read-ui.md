# 카드 저작 노트북 저장소 읽기 UI 구현 계획 (계획 B)

- 작성일: 2026-08-07
- 상태: `active`
- 설계: [카드 저작 노트북 JSON 전환](../specs/2026-08-05-card-authoring-json-notebook-design.md)
- 선행: [노트북 JSON 코어 (계획 A)](../archive/plans/2026-08-05-notebook-json-core.md) — 2026-08-07 완료·머지

## 설계 개요 (사람 검수용)

이 절만 읽고 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은 읽지
않아도 된다.

**무엇을 만드나** — 노트북이 저장소 폴더를 연결해 카드·풀·상태 JSON을 **읽고 보여주는** 데까지
만든다. 화면 골격(탭·3분할·요약), 카드 목록과 상태 배지, 풀 편성·분포·오류 표시, JSON 원문이
들어간다. **편집과 파일 쓰기는 하지 않는다** — 저작은 이 계획이 끝나도 기존 Markdown 경로가
담당한다.

**구조**

| 객체 | 책임 (한 줄) | 이 객체가 모르는 것 |
|---|---|---|
| 저장소 게이트웨이 | 폴더 핸들을 얻고 네 경로의 파일을 문자열로 읽어 준다 | 문자열의 의미, 화면 |
| 콘텐츠 스토어 | 읽은 카드·풀·상태를 들고 각각에 읽기 상태를 붙인다 | DOM, 파일 입출력 |
| 요약 화면 | 읽은 수치와 오류·충돌·미반영 개수를 그린다 | 무엇을 어떻게 읽었는지 |
| 카드 화면 | 카드 목록과 선택된 카드의 원문을 그린다 | 풀, 파일 입출력 |
| 풀 화면 | 풀 목록과 편성·분포·오류를 그린다 | 카드의 내부 값 |
| 노트북 조정자 | 탭 전환과 선택을 화면들에 배선한다 | 규칙 판단, 표시 변환 |

**의존 방향** — `저장소 게이트웨이 → 콘텐츠 스토어 → 요약·카드·풀 화면 ← 노트북 조정자`

**확장 축**
- *갈아끼울 수 있는 것* — 읽는 폴더 종류와 탭. 풀이 늘면 목록이 함께 는다. 집계·검색 규칙은
  전부 코어의 순수 함수라 화면을 열지 않고 바꾼다.
- *한번 정하면 고정되는 것* — 이 계획은 **읽기만 한다.** 편집 폼·미반영 저장·파일 쓰기는
  계획 C다. 브라우저는 Chrome·Edge로 묶인다. 새 화면은 별도 `<script>` 블록이며 기존 Markdown
  UI 스크립트를 건드리지 않는다.

**대안과 기각 이유**
1. *계획 B에서 편집·쓰기까지 한 번에* — 기각. 쓰기가 저장소를 직접 바꾸고 안전망이 git뿐이라,
   읽기 파이프라인이 옳다는 확신 없이 쓰기를 붙이면 실패가 저장소에 남는다. 계획 A가 라운드트립을
   UI 전에 잠근 것과 같은 이유다.
2. *기존 Markdown UI를 그 자리에서 개조* — 기각. 자유 문장 카드와 구조화 카드는 같은 폼을 공유할
   수 없다. 새 화면을 옆에 세우고 계획 C 끝에서 옛 경로를 통째로 지운다.

**이 선택으로 나중에 어려워지는 것**
- 계획 B가 끝나도 **저작은 여전히 Markdown 경로로만 된다.** 한 화면에 두 세계가 보이고, 저작자는
  어느 쪽이 진짜인지 헷갈린다. 계획 C가 끝나야 해소된다.
- 읽기 전용이라 **미반영 개념이 아직 없다.** 상태 판정기가 실제로는 `same`·`missing`만 내고
  `modified`·`new`·`conflict`는 계획 C 전까지 화면에 나타나지 않는다. 배선은 해 두지만 그 세 상태의
  검증은 계획 C 몫이다.
- 폴더 권한을 `read`로만 받는다. **계획 C가 쓰기를 붙일 때 승격 승인을 한 번 더 받아야 한다.**
  폴더를 다시 고를 필요는 없지만(기억한 핸들에 `requestPermission`을 걸면 된다) 클릭 한 번은
  없앨 수 없다. 지금 `readwrite`를 미리 받아 두는 쪽은 쓰지도 못할 권한을 요구하는 것이라 택하지
  않았다.
- 새 화면과 옛 화면이 **같은 `index.html` 안에서 스크립트 둘로 공존한다.** 파일이 3천 줄을 넘고,
  계획 C가 옛 스크립트를 지울 때까지 그 상태가 이어진다.

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.

> **에이전트 작업자에게:** 필수 서브 스킬 — `superpowers:executing-plans`로 태스크 단위로
> 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

**목표:** 노트북이 저장소를 연결해 콘텐츠를 읽고, 무엇을 읽었고 무엇이 잘못됐는지 보여준다.
편집과 쓰기는 하지 않는다.

**아키텍처:** 집계·검색·요약 규칙은 전부 **코어 스크립트의 순수 함수**로 넣어 `node:test`로
잠근다(Task 1~4). 브라우저 API와 DOM은 **새 `<script data-repo-ui>` 블록**에만 둔다(Task 5~8).
기존 Markdown UI 스크립트는 한 줄도 건드리지 않으며, 헤더의 모드 전환이 둘 중 하나만 보여준다.

**기술 스택:** 브라우저 JS (빌드 없음), File System Access API, IndexedDB,
Node `node:test`

## 전역 제약

- **규칙 14:** 외부 패키지를 추가하지 않는다. 노트북은 의존성 0으로 유지한다.
- **규칙 15:** 메인 체크아웃의 브랜치를 전환하지 않는다. 전용 워크트리에서 작업한다.
- **규칙 17:** 여백·색·타이포·연출처럼 **눈으로 맞춰야 하는 저작은 사용자 몫이다.** 이 계획의
  CSS는 배치가 성립하는 최소치만 넣고, 시각 조정은 사용자에게 넘긴다.
- **규칙 27:** 커밋 메시지 제목과 본문은 한국어로 쓴다. 형식은 `타입(범위): 한국어 제목`이고
  제목은 "…한다"로 끝난다.
- **노트북은 빌드 단계가 없다.** `index.html` 하나로 브라우저에서 열린다.
- **테스트 하니스는 `<script data-card-idea-core>` 블록만 읽는다.** 새 UI 스크립트에 넣은 코드는
  `node --test`가 보지 못하므로, **테스트할 가치가 있는 로직은 전부 코어 블록에 둔다.**
- **`File System Access API`는 `file://`에서 동작하지 않는다.** `showDirectoryPicker`가 보안
  컨텍스트를 요구하므로 로컬 서버로 열어야 한다(아래 검증 명령).
- 이 계획은 **파일을 쓰지 않는다.** `showDirectoryPicker`의 `mode`는 `"read"`다.

## 검증 명령

**노트북 단위 테스트** (모든 태스크 끝에서 실행):

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

**헤드리스** (Task 8 끝에서 한 번. 이 계획은 C#을 건드리지 않으므로 회귀 확인용이다):

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

**브라우저 수동 검수** (Task 5부터. `file://`에서는 폴더 선택이 동작하지 않는다):

```bash
python3 -m http.server 8765 --directory /Users/ish/Git/rogue-deck
```

그리고 `http://localhost:8765/Tools/card-idea-notebook/index.html`을 Chrome이나 Edge로 연다.
폴더 선택 대화상자에서는 **저장소 루트**(`rogue-deck`)를 고른다.

**시작 시점 기준선 (2026-08-07 실측, master `74ec428`):** 노트북 **102/102**, 헤드리스
**526/526**. 카드 JSON **26**(전부 `side: Player`), 풀 JSON **1**(`starter`, 22장), 상태 JSON **11**.
`starter`에 없는 아군 카드는 `fixture_all_block`·`fixture_attack`·`fixture_move_forward`·
`fixture_selected_block` 넷이다.

## 계획 A가 남긴 것

코어에 순수 함수 여덟이 있고 **아직 아무도 호출하지 않는다.** 이 계획이 첫 호출자다.

| 함수 | 이 계획에서의 쓰임 |
|---|---|
| `parseAuthoringSchema(text)` | 폴더 연결 직후 1회. 카드 리더의 인자 |
| `readCardJson(text, schema)` | 카드 파일마다. 실패하면 그 파일만 격리 |
| `writeCardJson(card, schema)` | **상태 판정에만** 쓴다. 파일로 내보내지 않는다 |
| `readPoolJson(text)` / `writePoolJson(pool)` | 풀. 위와 같다 |
| `validateContent({…})` | 읽기 끝난 뒤 1회. 요약과 오류 목록의 원천 |
| `resolveCardState` / `resolvePoolState` | 목록의 상태 배지 |

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Tools/card-idea-notebook/index.html` 코어 블록 (수정) | Task 1~4의 순수 함수 추가 |
| `Tools/card-idea-notebook/index.html` 마크업 (수정) | 모드 전환 헤더와 새 3분할 추가 |
| `Tools/card-idea-notebook/index.html` 새 `<script data-repo-ui>` (신규) | 게이트웨이·스토어·화면 셋·조정자 |
| `Tools/card-idea-notebook/index.test.mjs` (수정) | Task 1~4의 테스트, Task 5~8의 마크업 검사 |

기존 `<script data-card-idea-core>`의 Markdown 함수들과 두 번째 `<script>`(Markdown UI)는
**이 계획에서 한 줄도 바뀌지 않는다.**

---

### Task 1: 콘텐츠 경로와 상태 JSON 읽기

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록, `globalThis.CardIdeaNotebook` export 직전)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Produces: `CONTENT_PATHS` → 경로 세그먼트 배열 넷. Task 5의 게이트웨이가 순회한다.
  `readStatusJson(text)` → `{ status, errors }`. `status`는 `{ key, displayName, base }`이고
  오류가 있으면 `null`이다. Task 4의 요약과 Task 5의 스토어가 쓴다.

상태는 효과 편집기의 드롭다운 재료라 `key`와 `displayName`만 필요하다(설계 §11.5). 수명·성장치는
읽지 않는다 — 이 계획도 계획 C도 상태를 쓰지 않으므로 모델에 들고 있을 이유가 없다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`index.test.mjs` 끝에 붙인다.

```js

const statusesDir = new URL("../../Assets/StreamingAssets/Content/Statuses/", import.meta.url);

function readStatusFile(name) {
  return readFileSync(fileURLToPath(new URL(name, statusesDir)), "utf8");
}

test("콘텐츠 경로 넷을 세그먼트 배열로 노출한다", () => {
  const core = loadCore();
  assert.deepEqual(core.CONTENT_PATHS.schema,
    ["Tools", "card-idea-notebook", "authoring-schema.json"]);
  assert.deepEqual(core.CONTENT_PATHS.statuses,
    ["Assets", "StreamingAssets", "Content", "Statuses"]);
  assert.deepEqual(core.CONTENT_PATHS.cards,
    ["Assets", "StreamingAssets", "Content", "Cards"]);
  assert.deepEqual(core.CONTENT_PATHS.pools,
    ["Assets", "StreamingAssets", "Content", "Pools"]);
});

test("상태 파일에서 키와 표시 이름만 읽는다", () => {
  const core = loadCore();
  const { status, errors } = core.readStatusJson(readStatusFile("poison.json"));
  assert.deepEqual(errors, []);
  assert.equal(status.key, "poison");
  assert.equal(status.displayName, "독");
  assert.equal(status.base, readStatusFile("poison.json"));
  assert.deepEqual(Object.keys(status), ["key", "displayName", "base"],
    "수명·성장치는 읽지 않는다 - 노트북이 상태를 쓰지 않는다");
});

test("표시 이름이 없으면 키를 대신 쓴다", () => {
  const core = loadCore();
  const { status } = core.readStatusJson('{"key":"mystery"}');
  assert.equal(status.displayName, "mystery");
});

test("저장소의 상태 열한 개를 전부 읽는다", () => {
  const core = loadCore();
  const names = readdirSync(fileURLToPath(statusesDir)).filter((n) => n.endsWith(".json"));
  assert.ok(names.length >= 11, `상태가 11개 이상이어야 한다. 실제 ${names.length}`);

  const keys = [];
  for (const name of names) {
    const { status, errors } = core.readStatusJson(readStatusFile(name));
    assert.deepEqual(errors, [], name);
    keys.push(status.key);
  }

  assert.ok(keys.includes("poison"));
  assert.ok(keys.includes("block"));
  assert.equal(new Set(keys).size, keys.length, "상태 키가 중복이면 안 된다");
});

test("깨진 상태 파일과 키 없는 상태는 이유를 준다", () => {
  const core = loadCore();
  const broken = core.readStatusJson("{ 아님");
  assert.equal(broken.status, null);
  assert.equal(broken.errors.length, 1);

  const missing = core.readStatusJson('{"displayName":"독"}');
  assert.equal(missing.status, null);
  assert.ok(missing.errors.some((message) => message.includes("key")));
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 5개.** `Cannot read properties of undefined (reading 'schema')`와
`core.readStatusJson is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

`index.html`의 코어 블록에서 `globalThis.CardIdeaNotebook = Object.freeze({` **바로 위**에
넣는다.

```js
    /// 노트북이 아는 저장소 경로 넷(설계 3). 세그먼트 배열인 것은 File System Access API가
    /// 경로 문자열이 아니라 디렉터리 핸들을 한 칸씩 타고 내려가기 때문이다.
    const CONTENT_PATHS = Object.freeze({
      schema: Object.freeze(["Tools", "card-idea-notebook", "authoring-schema.json"]),
      statuses: Object.freeze(["Assets", "StreamingAssets", "Content", "Statuses"]),
      cards: Object.freeze(["Assets", "StreamingAssets", "Content", "Cards"]),
      pools: Object.freeze(["Assets", "StreamingAssets", "Content", "Pools"]),
    });

    /// 상태 파일에서 드롭다운 재료만 뽑는다(설계 11.5). 수명·성장치를 들고 있지 않는 것은
    /// 노트북이 상태를 쓰지 않기 때문이다 - 읽지 않는 값을 모델에 두면 언젠가 되쓰게 된다.
    function readStatusJson(text) {
      let raw;
      try {
        raw = JSON.parse(text);
      } catch (error) {
        return { status: null, errors: [`JSON을 읽을 수 없습니다: ${error.message}`] };
      }

      if (!raw || typeof raw !== "object" || !raw.key) {
        return { status: null, errors: ["필수 키 'key'가 없습니다."] };
      }

      return {
        status: {
          key: String(raw.key),
          displayName: String(raw.displayName ?? raw.key),
          base: text,
        },
        errors: [],
      };
    }
```

export 블록의 `parseAuthoringSchema` 위에 두 줄 더한다:

```js
    globalThis.CardIdeaNotebook = Object.freeze({
      CONTENT_PATHS,
      readStatusJson,
      parseAuthoringSchema,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **107/107** (기준선 102 + 신규 5).

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 상태 파일과 콘텐츠 경로를 안다

상태에서 드롭다운 재료인 키와 표시 이름만 읽는다. 수명·성장치를 들고 있지 않는 것은
노트북이 상태를 쓰지 않기 때문이다.

경로는 문자열이 아니라 세그먼트 배열이다 - File System Access API가 디렉터리 핸들을
한 칸씩 타고 내려간다."
```

---

### Task 2: 풀 관점 집계 — 분포와 소속

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 3~5(계획 A)의 카드·풀 모델
- Produces:
  `poolDistribution({ pool, cardsById })` →
  `{ total, present, missing: [id], grades: [{value,count}], tags: […], costs: […], orders: […] }`.
  Task 8의 풀 화면이 그린다.
  `poolMembership(pools)` → `Map<cardId, [poolId]>`. Task 3의 목록 필터와 Task 7의 카드 줄이 쓴다.

**정렬 규칙을 함수가 정한다.** 등급·태그는 **개수 내림차순**(동률이면 값 오름차순), 비용·순서는
**값 오름차순**이다. 앞의 둘은 "무엇이 많은가"가, 뒤의 둘은 "어디에 몰렸는가"가 질문이기 때문이다.
화면이 정렬을 다시 하지 않도록 여기서 끝낸다.

`missing`을 세지 않고 따로 담는 이유는 설계 §10.4다 — 없는 카드를 분포에 섞으면 집계가 거짓이
되고, 버리면 노트북을 열었다 닫는 것만으로 편성이 바뀐다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

정확값은 **합성 픽스처**로 검사한다. 저장소의 `starter`에는 카드가 늘어도 깨지지 않는 거친 검사만
건다 — 콘텐츠가 바뀔 때마다 실패하는 테스트는 신호가 아니라 잡음이다.

```js

function cardsByIdOf(core, schema, specs) {
  const map = new Map();
  for (const spec of specs) {
    const { card } = core.readCardJson(JSON.stringify({
      id: spec.id, name: spec.id, side: "Player", category: spec.category ?? "Execution",
      ...spec,
    }, null, 2), schema);
    map.set(card.id, card);
  }
  return map;
}

test("풀의 등급·태그를 개수 내림차순으로 집계한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [
    { id: "a", grade: "Common", tags: ["시작", "공격"] },
    { id: "b", grade: "Common", tags: ["시작"] },
    { id: "c", grade: "Rare", tags: ["시작", "공격"] },
  ]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b","c"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.grades, [
    { value: "Common", count: 2 },
    { value: "Rare", count: 1 },
  ]);
  assert.deepEqual(distribution.tags, [
    { value: "시작", count: 3 },
    { value: "공격", count: 2 },
  ]);
});

test("비용과 실행 순서는 값 오름차순으로 집계한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [
    { id: "a", energyCost: 2, baseExecutionOrder: 5 },
    { id: "b", energyCost: 1, baseExecutionOrder: 3 },
    { id: "c", energyCost: 1, baseExecutionOrder: 5 },
  ]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b","c"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.costs, [
    { value: 1, count: 2 },
    { value: 2, count: 1 },
  ]);
  assert.deepEqual(distribution.orders, [
    { value: 3, count: 1 },
    { value: 5, count: 2 },
  ]);
});

test("개입 카드는 실행 순서 분포에서 빠진다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [
    { id: "a", baseExecutionOrder: 4 },
    { id: "b", category: "Intervention", intervention: { kind: "lock" } },
  ]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.orders, [{ value: 4, count: 1 }],
    "개입 카드에는 baseExecutionOrder가 없다");
  assert.equal(distribution.total, 2, "그래도 편성 장수에는 들어간다");
});

test("없는 카드는 세지 않고 따로 담는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [{ id: "a", grade: "Common", tags: ["시작"] }]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","ghost"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.equal(distribution.total, 2);
  assert.equal(distribution.present, 1);
  assert.deepEqual(distribution.missing, ["ghost"]);
  assert.deepEqual(distribution.grades, [{ value: "Common", count: 1 }],
    "없는 카드를 분포에 섞으면 집계가 거짓이 된다");
});

test("저장소의 starter를 없는 카드 없이 집계한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = new Map();
  for (const name of readdirSync(fileURLToPath(cardsDir)).filter((n) => n.endsWith(".json"))) {
    const { card } = core.readCardJson(readCardFile(name), schema);
    cardsById.set(card.id, card);
  }
  const { pool } = core.readPoolJson(
    readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8"));

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.missing, [], "저장소 풀에는 없는 카드가 없어야 한다");
  assert.equal(distribution.present, distribution.total);
  assert.equal(distribution.total, pool.cards.length);
  assert.ok(distribution.tags.length > 0);
  assert.equal(distribution.grades.reduce((sum, e) => sum + e.count, 0), distribution.present,
    "등급 개수의 합이 카드 수와 같아야 한다");
});

test("카드 소속 풀을 역방향 표로 만든다", () => {
  const core = loadCore();
  const pools = [
    core.readPoolJson('{"id":"starter","cards":["a","b"]}').pool,
    core.readPoolJson('{"id":"mycologist","cards":["b","b","c"]}').pool,
  ];

  const membership = core.poolMembership(pools);
  assert.deepEqual(membership.get("a"), ["starter"]);
  assert.deepEqual(membership.get("b"), ["starter", "mycologist"],
    "같은 풀 안의 중복은 한 번만 센다");
  assert.deepEqual(membership.get("c"), ["mycologist"]);
  assert.equal(membership.get("ghost"), undefined);
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 6개.** `core.poolDistribution is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

`readStatusJson` 아래에 넣는다.

```js
    /// 집계 결과를 화면이 다시 정렬하지 않도록 여기서 순서를 끝낸다.
    /// order === "value"면 값 오름차순(비용·순서), 아니면 개수 내림차순 후 값 오름차순(등급·태그).
    function countEntries(counts, order) {
      const entries = [];
      for (const [value, count] of counts) entries.push({ value, count });

      const byValue = (a, b) => (a.value < b.value ? -1 : a.value > b.value ? 1 : 0);
      entries.sort(order === "value" ? byValue : (a, b) => b.count - a.count || byValue(a, b));
      return entries;
    }

    /// 풀 하나를 집합으로 본 모습(설계 11.4). 없는 카드는 세지 않고 missing에 담는다 -
    /// 분포에 섞으면 집계가 거짓이 되고, 버리면 노트북을 열었다 닫는 것만으로 편성이 바뀐다.
    function poolDistribution({ pool, cardsById }) {
      const grades = new Map();
      const tags = new Map();
      const costs = new Map();
      const orders = new Map();
      const missing = [];
      const bump = (counts, key) => counts.set(key, (counts.get(key) ?? 0) + 1);

      for (const cardId of pool.cards) {
        const card = cardsById.get(cardId);
        if (!card) {
          missing.push(cardId);
          continue;
        }

        bump(grades, card.grade);
        for (const tag of card.tags ?? []) bump(tags, tag);
        bump(costs, card.energyCost);
        if (card.category === "Execution") bump(orders, card.baseExecutionOrder);
      }

      return {
        total: pool.cards.length,
        present: pool.cards.length - missing.length,
        missing,
        grades: countEntries(grades, "count"),
        tags: countEntries(tags, "count"),
        costs: countEntries(costs, "value"),
        orders: countEntries(orders, "value"),
      };
    }

    /// 카드 id로 소속 풀을 찾는 역방향 표. 참조는 풀→카드 단방향이므로(설계 7) 역방향이
    /// 필요한 화면은 이 표를 만들어 쓴다. 풀 안의 중복은 한 번만 센다 - 중복 자체는
    /// 검증기가 오류로 잡고, 소속 여부는 그것과 별개다.
    function poolMembership(pools) {
      const membership = new Map();
      for (const pool of pools) {
        for (const cardId of pool.cards) {
          const owners = membership.get(cardId);
          if (!owners) membership.set(cardId, [pool.id]);
          else if (!owners.includes(pool.id)) owners.push(pool.id);
        }
      }

      return membership;
    }
```

export 블록에 두 줄 더한다:

```js
      validateContent,
      poolDistribution,
      poolMembership,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **113/113** (107 + 신규 6).

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 풀을 집합으로 집계한다

등급·태그는 개수 내림차순, 비용·순서는 값 오름차순으로 낸다 - 앞의 둘은 무엇이
많은가가, 뒤의 둘은 어디에 몰렸는가가 질문이다. 화면이 다시 정렬하지 않도록
순서를 여기서 끝낸다.

없는 카드는 분포에서 빼고 missing에 담는다. 섞으면 집계가 거짓이 되고 버리면
노트북을 열었다 닫는 것만으로 편성이 바뀐다."
```

---

### Task 3: 카드 목록 검색과 필터

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 2의 `poolMembership`
- Produces: `CARD_FILTERS` → `["all","modified","conflict","error","orphan"]`.
  `cardListView({ cards, states, errorIds, membership, query, filter, poolId })` →
  `[{ card, state, hasError, pools }]`. Task 7의 카드 목록이 그대로 그린다.

**목록 한 줄이 알아야 하는 것을 여기서 다 계산한다.** 화면은 이 배열만 반복하므로 검색·필터
규칙이 DOM을 모른다. 설계 §11.3의 필터 넷(`수정됨만`·`충돌만`·`오류만`·`고아만`)과 풀별 필터가
전부 여기에 있다.

`states`는 카드 **id**로 색인한 `Map`이다. 설계 §5가 `uid`를 두는 이유는 저작 중 `id`가 바뀌기
때문인데, 이 계획에는 편집이 없어 `id`가 파일명 그대로 고정이다. **계획 C가 편집을 붙일 때 색인
키를 `uid`로 바꾼다.**

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```js

function viewCards(core, schema, specs) {
  return specs.map((spec) => core.readCardJson(JSON.stringify({
    id: spec.id, name: spec.name ?? spec.id, side: spec.side ?? "Player",
    category: "Execution", ...spec,
  }, null, 2), schema).card);
}

test("검색이 이름·id·태그를 훑는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [
    { id: "vanguard_slash", name: "선봉 베기", tags: ["시작", "공격"] },
    { id: "spore_veil", name: "포자 장막", tags: ["독"] },
  ]);
  const view = (query) => core.cardListView({ cards, query, filter: "all" })
    .map((row) => row.card.id);

  assert.deepEqual(view("선봉"), ["vanguard_slash"], "이름으로 찾는다");
  assert.deepEqual(view("spore"), ["spore_veil"], "id로 찾는다");
  assert.deepEqual(view("독"), ["spore_veil"], "태그로 찾는다");
  assert.deepEqual(view("VANGUARD"), ["vanguard_slash"], "대소문자를 무시한다");
  assert.deepEqual(view("  "), ["vanguard_slash", "spore_veil"], "공백만이면 거르지 않는다");
  assert.deepEqual(view("없는말"), []);
});

test("필터가 상태·오류·고아를 가른다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [
    { id: "same_card" }, { id: "edited" }, { id: "clashing" },
    { id: "broken" }, { id: "enemy_card", side: "Enemy" },
  ]);
  const states = new Map([["edited", "modified"], ["clashing", "conflict"]]);
  const errorIds = new Set(["broken"]);
  const membership = new Map([["same_card", ["starter"]]]);
  const view = (filter) => core.cardListView({ cards, states, errorIds, membership, filter })
    .map((row) => row.card.id);

  assert.deepEqual(view("all"),
    ["same_card", "edited", "clashing", "broken", "enemy_card"]);
  assert.deepEqual(view("modified"), ["edited"]);
  assert.deepEqual(view("conflict"), ["clashing"]);
  assert.deepEqual(view("error"), ["broken"]);
  assert.deepEqual(view("orphan"), ["edited", "clashing", "broken"],
    "적군 카드는 고아가 아니다 - 풀은 아군 것이다");
});

test("풀별 필터가 소속만 남긴다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [{ id: "a" }, { id: "b" }]);
  const membership = new Map([["a", ["starter"]], ["b", ["mycologist"]]]);

  const rows = core.cardListView({ cards, membership, filter: "all", poolId: "starter" });
  assert.deepEqual(rows.map((row) => row.card.id), ["a"]);
});

test("줄마다 상태·오류·소속을 붙여 준다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [{ id: "a" }]);

  const [plain] = core.cardListView({ cards, filter: "all" });
  assert.equal(plain.state, "same", "상태를 모르면 저장소와 같은 것으로 본다");
  assert.equal(plain.hasError, false);
  assert.deepEqual(plain.pools, []);

  const [marked] = core.cardListView({
    cards,
    states: new Map([["a", "conflict"]]),
    errorIds: new Set(["a"]),
    membership: new Map([["a", ["starter", "mycologist"]]]),
    filter: "all",
  });
  assert.equal(marked.state, "conflict");
  assert.equal(marked.hasError, true);
  assert.deepEqual(marked.pools, ["starter", "mycologist"]);
});

test("필터 목록을 노출한다", () => {
  const core = loadCore();
  assert.deepEqual([...core.CARD_FILTERS],
    ["all", "modified", "conflict", "error", "orphan"]);
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 5개.** `core.cardListView is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

`poolMembership` 아래에 넣는다.

```js
    const CARD_FILTERS = Object.freeze([
      "all", "modified", "conflict", "error", "orphan",
    ]);

    /// 검색은 이름·id·태그를 훑는다(설계 11.3). id가 파일명이자 풀이 참조하는 키라
    /// 이름만큼 자주 찾는 대상이다.
    function matchesQuery(card, query) {
      const needle = String(query ?? "").trim().toLocaleLowerCase();
      if (!needle) return true;

      const haystack = [card.name, card.id, ...(card.tags ?? [])];
      return haystack.some((value) => String(value).toLocaleLowerCase().includes(needle));
    }

    /// 고아는 "어느 풀에도 없는 아군 카드"다(설계 7). 적군 카드는 풀에 담기지 않으므로
    /// 고아일 수 없다 - 풀은 플레이어 캐릭터의 것이다.
    function matchesFilter(row, filter) {
      if (filter === "modified") return row.state === "modified";
      if (filter === "conflict") return row.state === "conflict";
      if (filter === "error") return row.hasError;
      if (filter === "orphan") return row.card.side === "Player" && row.pools.length === 0;
      return true;
    }

    /// 목록 한 줄이 알아야 하는 것을 카드마다 계산하고 걸러 준다. 화면은 이 배열만 반복하므로
    /// 검색·필터 규칙이 DOM을 모른다.
    /// states는 카드 id로 색인한다 - 이 계획에는 편집이 없어 id가 파일명 그대로 고정이다.
    /// 계획 C가 편집을 붙이면 id가 저작 중에 바뀌므로 색인 키를 uid로 바꾼다(설계 5).
    function cardListView({ cards, states, errorIds, membership, query, filter, poolId }) {
      const stateOf = states ?? new Map();
      const failing = errorIds ?? new Set();
      const owners = membership ?? new Map();
      const rows = [];

      for (const card of cards) {
        const row = {
          card,
          state: stateOf.get(card.id) ?? "same",
          hasError: failing.has(card.id),
          pools: owners.get(card.id) ?? [],
        };

        if (poolId && !row.pools.includes(poolId)) continue;
        if (!matchesQuery(card, query)) continue;
        if (!matchesFilter(row, filter)) continue;
        rows.push(row);
      }

      return rows;
    }
```

export 블록에 두 줄 더한다:

```js
      poolMembership,
      CARD_FILTERS,
      cardListView,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **118/118** (113 + 신규 5).

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 카드 목록을 검색하고 거른다

목록 한 줄이 알아야 하는 상태·오류·소속을 카드마다 미리 계산해 준다. 화면은 이
배열만 반복하므로 검색·필터 규칙이 DOM을 모른다.

고아는 어느 풀에도 없는 아군 카드다. 적군 카드는 풀에 담기지 않으므로 고아일 수
없다 - 풀은 플레이어 캐릭터의 것이다."
```

---

### Task 4: 읽은 결과 요약

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: 계획 A의 `validateContent` 결과와 상태 판정 결과
- Produces: `contentSummary({ cards, pools, statuses, validation, cardStates, poolStates })` →
  `{ counts: {cards, pools, statuses}, errors, warnings, conflicts, pending }`.
  `summaryLines(summary)` → 문장 두 줄. Task 6의 요약 줄이 그대로 쓴다.

설계 §11.2의 두 줄을 만든다. 문장을 코어에 두는 이유는 테스트가 문구까지 잠글 수 있어서다 —
화면 문자열이 코드 여기저기 흩어지면 바뀐 것을 아무도 모른다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```js

test("읽은 수치와 문제 개수를 센다", () => {
  const core = loadCore();
  const summary = core.contentSummary({
    cards: [{}, {}, {}],
    pools: [{}],
    statuses: [{}, {}],
    validation: { errors: [{ message: "x" }], warnings: [{ message: "y" }, { message: "z" }] },
    cardStates: new Map([["a", "same"], ["b", "modified"], ["c", "conflict"]]),
    poolStates: new Map([["starter", "new"]]),
  });

  assert.deepEqual(summary.counts, { cards: 3, pools: 1, statuses: 2 });
  assert.equal(summary.errors, 1);
  assert.equal(summary.warnings, 2);
  assert.equal(summary.conflicts, 1);
  assert.equal(summary.pending, 2, "modified와 new가 미반영이다");
});

test("상태 표가 없어도 0으로 센다", () => {
  const core = loadCore();
  const summary = core.contentSummary({
    cards: [], pools: [], statuses: [],
    validation: { errors: [], warnings: [] },
  });
  assert.equal(summary.conflicts, 0);
  assert.equal(summary.pending, 0);
});

test("요약을 두 줄 문장으로 만든다", () => {
  const core = loadCore();
  const summary = core.contentSummary({
    cards: new Array(26).fill({}),
    pools: [{}],
    statuses: new Array(11).fill({}),
    validation: { errors: [], warnings: [] },
  });

  assert.deepEqual(core.summaryLines(summary), [
    "카드 26 · 풀 1 · 상태 11 을 읽었습니다",
    "오류 0 · 충돌 0 · 미반영 0",
  ]);
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 3개.** `core.contentSummary is not a function`.

- [ ] **Step 3: 최소 구현을 쓴다**

`cardListView` 아래에 넣는다.

```js
    /// 읽은 직후 화면(설계 11.2)이 쓰는 수치. 읽기와 검증이 이미 끝난 결과만 세므로
    /// 이 함수는 파일도 스키마도 모른다.
    function contentSummary({ cards, pools, statuses, validation, cardStates, poolStates }) {
      const tally = (states) => {
        let conflict = 0;
        let pending = 0;
        for (const state of (states ?? new Map()).values()) {
          if (state === "conflict") conflict += 1;
          else if (state === "modified" || state === "new") pending += 1;
        }

        return { conflict, pending };
      };

      const card = tally(cardStates);
      const pool = tally(poolStates);

      return {
        counts: { cards: cards.length, pools: pools.length, statuses: statuses.length },
        errors: validation.errors.length,
        warnings: validation.warnings.length,
        conflicts: card.conflict + pool.conflict,
        pending: card.pending + pool.pending,
      };
    }

    /// 요약 두 줄. 문장을 코어에 두면 테스트가 문구까지 잠근다 - 화면 문자열이 흩어지면
    /// 바뀐 것을 아무도 모른다.
    function summaryLines(summary) {
      return [
        `카드 ${summary.counts.cards} · 풀 ${summary.counts.pools} `
        + `· 상태 ${summary.counts.statuses} 을 읽었습니다`,
        `오류 ${summary.errors} · 충돌 ${summary.conflicts} · 미반영 ${summary.pending}`,
      ];
    }
```

export 블록에 두 줄 더한다:

```js
      cardListView,
      contentSummary,
      summaryLines,
```

- [ ] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **121/121** (118 + 신규 3).

- [ ] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 읽은 결과를 요약한다

읽기와 검증이 끝난 결과만 세므로 이 함수는 파일도 스키마도 모른다. 요약 문장을
코어에 두면 테스트가 문구까지 잠근다 - 화면 문자열이 흩어지면 바뀐 것을 아무도 모른다."
```

---

### Task 5: 저장소 게이트웨이 — 폴더 연결과 읽기

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (마크업에 모드 전환 헤더, 새 `<script data-repo-ui>` 신설)
- Test: `Tools/card-idea-notebook/index.test.mjs` (마크업 검사)

**Interfaces:**
- Consumes: Task 1의 `CONTENT_PATHS`·`readStatusJson`, 계획 A의 리더·검증기·상태 판정기
- Produces: 새 스크립트 안의 `repoStore` — `{ connected, root, schema, statuses, cards, pools,
  quarantined, validation, cardStates, poolStates, membership, cardsById }`.
  Task 6~8의 화면이 읽는다. `connectRepo()`·`reloadRepo()` 두 진입점.

이 태스크는 **브라우저 API를 다루므로 `node:test`로 검증할 수 없다.** 그래서 두 가지로 나눠 잡는다 —
마크업 존재는 단위 테스트가, 동작은 아래 수동 검수 절차가 맡는다. **읽기 파이프라인의 규칙은
전부 Task 1~4의 순수 함수에 있으므로**, 여기 남는 것은 파일을 문자열로 가져오는 배관뿐이다.

- [ ] **Step 1: 실패하는 마크업 테스트를 쓴다**

```js

test("저장소 모드 전환과 연결 버튼이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="mode-repo"/);
  assert.match(html, /id="mode-markdown"/);
  assert.match(html, /id="repo-connect"/);
  assert.match(html, /id="repo-reload"/);
  assert.match(html, /id="repo-unsupported"/);
});

test("저장소 UI 스크립트가 코어와 분리되어 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /<script data-repo-ui>/);

  const core = html.match(/<script data-card-idea-core>([\s\S]*?)<\/script>/)[1];
  assert.equal(core.includes("showDirectoryPicker"), false,
    "브라우저 API는 코어에 들어가지 않는다 - 코어는 node:test가 돌린다");
  assert.equal(core.includes("document."), false,
    "DOM도 코어에 들어가지 않는다");
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 2개.** `id="mode-repo"`를 찾지 못한다.

- [ ] **Step 3: 헤더에 모드 전환을 넣는다**

`index.html`의 `<header class="app-header">` 블록(374~383줄)에서 `<p class="header-status" …>`
**바로 앞**에 넣는다.

```html
    <div class="mode-switch" role="group" aria-label="작업 모드">
      <button type="button" id="mode-markdown" class="mode-button is-active">아이디어</button>
      <button type="button" id="mode-repo" class="mode-button">저장소</button>
    </div>
```

- [ ] **Step 4: 저장소 화면의 골격 마크업을 넣는다**

`</main>`(기존 Markdown 작업 영역의 끝, 544줄) **바로 뒤**에 넣는다. 이 태스크에서는
연결 상태와 요약 자리만 만들고, 세 판의 내용은 Task 6~8이 채운다.

```html
  <main class="workspace repo-workspace" id="repo-workspace" hidden>
    <aside class="pane library-pane" aria-label="저장소 목록">
      <div class="pane-scroll">
        <div class="pane-header">
          <div class="pane-title">
            <strong>저장소</strong>
            <span class="count" id="repo-count">–</span>
          </div>
          <button type="button" class="primary" id="repo-connect">폴더 연결</button>
        </div>
        <div class="library-tools">
          <p class="repo-hint" id="repo-unsupported" hidden>
            이 브라우저는 폴더 연결을 지원하지 않습니다. Chrome이나 Edge로 열어 주세요.
          </p>
          <button type="button" id="repo-reload" disabled>저장소 다시 읽기</button>
        </div>
        <div class="repo-list" id="repo-list"></div>
      </div>
    </aside>

    <section class="pane editor-pane" aria-label="선택한 항목">
      <div class="pane-scroll">
        <div class="pane-header">
          <div class="pane-title"><strong id="repo-detail-title">선택된 항목 없음</strong></div>
        </div>
        <div class="repo-summary" id="repo-summary"></div>
        <div class="repo-detail" id="repo-detail"></div>
      </div>
    </section>

    <aside class="pane preview-pane" aria-label="JSON 원문">
      <div class="pane-scroll">
        <div class="pane-header">
          <div class="pane-title"><strong id="repo-source-title">JSON 원문</strong></div>
        </div>
        <pre class="markdown-preview" id="repo-source"></pre>
      </div>
    </aside>
  </main>
```

- [ ] **Step 5: 최소한의 CSS를 넣는다**

`<style>` 안, `.toast.visible` 규칙(365줄) **바로 뒤**에 넣는다. 규칙 17에 따라 배치가
성립하는 최소치만 넣는다 — 색·여백의 최종 조정은 사용자 몫이다.

**첫 규칙을 빼면 모드 전환이 동작하지 않는다.** `.workspace`의 `display: grid`가 작성자
스타일이라 브라우저 기본 스타일의 `[hidden] { display: none }`을 이긴다 — 2026-08-07 실측으로,
`hidden`을 걸어도 `getComputedStyle(main).display`가 `"grid"`로 남았다.

```css
    .workspace[hidden] { display: none; }
    .mode-switch { display: flex; gap: 4px; }
    .mode-button { padding: 6px 12px; font-size: 12px; }
    .mode-button.is-active { color: #1b1a15; border-color: var(--gold); background: var(--gold); }
    .repo-hint { margin-bottom: 8px; color: var(--warning); font-size: 12px; line-height: 1.5; }
    .repo-list { display: grid; gap: 7px; padding: 0 10px 10px; }
    .repo-summary { padding: 14px 16px; border-bottom: 1px solid var(--line); font-size: 13px; }
    .repo-summary p { margin: 0 0 4px; color: var(--muted); }
    .repo-detail { padding: 14px 16px; }
```

- [ ] **Step 6: 저장소 UI 스크립트를 신설한다**

기존 UI 스크립트의 닫는 `</script>`(파일 끝 `</body>` 직전) **바로 뒤**에 새 블록을 통째로
넣는다. 기존 두 스크립트는 건드리지 않는다.

```html
  <script data-repo-ui>
  (() => {
    "use strict";

    const core = globalThis.CardIdeaNotebook;
    const byId = (id) => document.getElementById(id);
    const elements = {
      modeMarkdown: byId("mode-markdown"),
      modeRepo: byId("mode-repo"),
      markdownWorkspace: document.querySelector("main.workspace:not(.repo-workspace)"),
      repoWorkspace: byId("repo-workspace"),
      connect: byId("repo-connect"),
      reload: byId("repo-reload"),
      unsupported: byId("repo-unsupported"),
      count: byId("repo-count"),
      list: byId("repo-list"),
      summary: byId("repo-summary"),
      detailTitle: byId("repo-detail-title"),
      detail: byId("repo-detail"),
      sourceTitle: byId("repo-source-title"),
      source: byId("repo-source"),
    };

    const HANDLE_DB_NAME = `${core.STORAGE_KEY}.handles`;
    const HANDLE_STORE = "handles";
    const REPO_HANDLE_KEY = "repoRoot";

    const repoStore = {
      connected: false,
      root: null,
      schema: null,
      statuses: [],
      cards: [],
      pools: [],
      cardsById: new Map(),
      membership: new Map(),
      quarantined: [],
      validation: { errors: [], warnings: [] },
      cardStates: new Map(),
      poolStates: new Map(),
      message: "",
    };

    // 기존 UI 스크립트와 같은 저장소를 쓰되 키가 다르다. 같은 헬퍼를 복제하는 이유는 두
    // 스크립트가 서로를 참조하지 않게 하기 위해서다 - 계획 C가 옛 스크립트를 통째로 지운다.
    function openHandleDb() {
      return new Promise((resolve, reject) => {
        const request = indexedDB.open(HANDLE_DB_NAME, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(HANDLE_STORE);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error("폴더 기억 저장소를 열 수 없습니다."));
      });
    }

    async function withHandleStore(mode, run) {
      const db = await openHandleDb();
      try {
        return await new Promise((resolve, reject) => {
          const request = run(db.transaction(HANDLE_STORE, mode).objectStore(HANDLE_STORE));
          request.onsuccess = () => resolve(request.result);
          request.onerror = () => reject(request.error);
        });
      } finally {
        db.close();
      }
    }

    async function rememberRoot(handle) {
      try {
        await withHandleStore("readwrite", (store) => store.put(handle, REPO_HANDLE_KEY));
      } catch {
        /* 다음에 폴더를 다시 고르게 될 뿐 읽은 결과에는 영향이 없다. */
      }
    }

    async function recallRoot() {
      try {
        return (await withHandleStore("readonly", (store) => store.get(REPO_HANDLE_KEY))) ?? null;
      } catch {
        return null;
      }
    }

    /// 기억한 핸들은 새 탭에서 권한이 만료된다. 조용히 다시 묻지 않고 false를 돌려주면
    /// 호출부가 버튼을 누르게 한다 - 권한 요청은 사용자 제스처를 요구한다.
    async function hasReadPermission(handle, interactive) {
      const options = { mode: "read" };
      if ((await handle.queryPermission(options)) === "granted") return true;
      if (!interactive) return false;
      return (await handle.requestPermission(options)) === "granted";
    }

    async function directoryAt(root, segments) {
      let handle = root;
      for (const segment of segments) handle = await handle.getDirectoryHandle(segment);
      return handle;
    }

    async function fileTextAt(root, segments) {
      const directory = await directoryAt(root, segments.slice(0, -1));
      const handle = await directory.getFileHandle(segments[segments.length - 1]);
      return (await handle.getFile()).text();
    }

    /// 폴더의 *.json을 이름순으로 읽는다. 읽기에 실패한 파일은 던지지 않고 failures에 담는다 -
    /// 카드 한 장이 깨졌다고 도구가 열리지 않으면 그 카드를 고칠 수단도 사라진다(설계 10.2).
    async function readJsonFolder(root, segments) {
      const directory = await directoryAt(root, segments);
      const names = [];
      for await (const [name, entry] of directory.entries()) {
        if (entry.kind === "file" && name.endsWith(".json")) names.push(name);
      }
      names.sort();

      const files = [];
      const failures = [];
      for (const name of names) {
        try {
          const handle = await directory.getFileHandle(name);
          files.push({ name, text: await (await handle.getFile()).text() });
        } catch (error) {
          failures.push({ scope: "file", name, messages: [String(error?.message ?? error)] });
        }
      }

      return { files, failures };
    }

    /// 파싱 실패한 파일은 격리하고 나머지를 연다(설계 10.2). 읽는 순서는 상태 → 카드 → 풀이며,
    /// 풀이 카드 id 배열이라 카드에 의존하기 때문이다(설계 10.1, ContentBootstrap과 같은 이유).
    async function loadRepo(root) {
      const schema = core.parseAuthoringSchema(await fileTextAt(root, core.CONTENT_PATHS.schema));
      const quarantined = [];

      const statusFolder = await readJsonFolder(root, core.CONTENT_PATHS.statuses);
      const statuses = [];
      for (const file of statusFolder.files) {
        const { status, errors } = core.readStatusJson(file.text);
        if (status) statuses.push(status);
        else quarantined.push({ scope: "status", name: file.name, messages: errors });
      }

      const cardFolder = await readJsonFolder(root, core.CONTENT_PATHS.cards);
      const cards = [];
      for (const file of cardFolder.files) {
        const { card, errors } = core.readCardJson(file.text, schema);
        if (card) cards.push(card);
        else quarantined.push({ scope: "card", name: file.name, messages: errors });
      }

      const poolFolder = await readJsonFolder(root, core.CONTENT_PATHS.pools);
      const pools = [];
      for (const file of poolFolder.files) {
        const { pool, errors } = core.readPoolJson(file.text);
        if (pool) pools.push(pool);
        else quarantined.push({ scope: "pool", name: file.name, messages: errors });
      }

      quarantined.push(
        ...statusFolder.failures, ...cardFolder.failures, ...poolFolder.failures);

      const cardsById = new Map();
      for (const card of cards) cardsById.set(card.id, card);

      const cardStates = new Map();
      for (const card of cards) {
        cardStates.set(card.id,
          core.resolveCardState({ stored: card.base, pending: card, schema }));
      }

      const poolStates = new Map();
      for (const pool of pools) {
        poolStates.set(pool.id, core.resolvePoolState({ stored: pool.base, pending: pool }));
      }

      return {
        schema,
        statuses,
        cards,
        pools,
        cardsById,
        membership: core.poolMembership(pools),
        quarantined,
        validation: core.validateContent({
          cards, pools, statusKeys: statuses.map((status) => status.key), schema,
        }),
        cardStates,
        poolStates,
      };
    }

    function applyLoaded(root, loaded) {
      Object.assign(repoStore, loaded, { connected: true, root, message: "" });
      render();
    }

    function failWith(error) {
      repoStore.connected = false;
      repoStore.message = `저장소를 읽지 못했습니다: ${error?.message ?? error}`;
      render();
    }

    async function connectRepo() {
      try {
        const root = await window.showDirectoryPicker({
          id: "fateWeaverRepoRoot",
          mode: "read",
        });
        await rememberRoot(root);
        applyLoaded(root, await loadRepo(root));
      } catch (error) {
        if (error?.name === "AbortError") return;
        failWith(error);
      }
    }

    async function reloadRepo() {
      if (!repoStore.root) return;
      try {
        applyLoaded(repoStore.root, await loadRepo(repoStore.root));
      } catch (error) {
        failWith(error);
      }
    }

    /// 이 계획에서는 요약 한 줄만 그린다. 목록·상세·원문은 Task 6~8이 채운다.
    function render() {
      elements.reload.disabled = !repoStore.connected;
      elements.count.textContent = repoStore.connected
        ? String(repoStore.cards.length)
        : "–";
      elements.summary.textContent = repoStore.message
        || (repoStore.connected ? "읽었습니다." : "폴더를 연결하세요.");
    }

    function setMode(mode) {
      const repo = mode === "repo";
      elements.repoWorkspace.hidden = !repo;
      elements.markdownWorkspace.hidden = repo;
      elements.modeRepo.classList.toggle("is-active", repo);
      elements.modeMarkdown.classList.toggle("is-active", !repo);
    }

    async function restoreRoot() {
      const root = await recallRoot();
      if (!root) return;

      // 권한이 살아 있을 때만 조용히 읽는다. 만료됐으면 버튼을 누르게 둔다.
      if (!(await hasReadPermission(root, false))) {
        repoStore.root = root;
        repoStore.message = "폴더 권한이 만료되었습니다. 폴더 연결을 다시 눌러 주세요.";
        render();
        return;
      }

      try {
        applyLoaded(root, await loadRepo(root));
      } catch (error) {
        failWith(error);
      }
    }

    if (typeof window.showDirectoryPicker !== "function") {
      elements.unsupported.hidden = false;
      elements.connect.disabled = true;
    } else {
      elements.connect.addEventListener("click", connectRepo);
      restoreRoot();
    }

    elements.reload.addEventListener("click", reloadRepo);
    elements.modeMarkdown.addEventListener("click", () => setMode("markdown"));
    elements.modeRepo.addEventListener("click", () => setMode("repo"));

    render();
  })();
  </script>
```

- [ ] **Step 7: 단위 테스트 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **123/123** (121 + 신규 2). 기존 테스트 회귀 없음 — 특히
`exposes every card grade in the authoring form`이 계속 통과해야 한다.

- [ ] **Step 8: 브라우저에서 수동 검수한다**

```bash
python3 -m http.server 8765 --directory /Users/ish/Git/rogue-deck
```

Chrome이나 Edge로 `http://localhost:8765/Tools/card-idea-notebook/index.html`을 열고 확인한다.

1. 헤더에 `아이디어`·`저장소` 두 버튼이 있고 기본은 `아이디어`다. **기존 화면이 그대로다.**
2. `저장소`를 누르면 3분할이 바뀌고 `폴더를 연결하세요.`가 보인다.
3. `폴더 연결`을 눌러 저장소 루트(`rogue-deck`)를 고른다. 좌상단 개수가 **26**이 되고
   요약이 `읽었습니다.`로 바뀐다.
4. 콘솔에 오류가 없다.
5. 탭을 새로 고친다. 권한이 남아 있으면 자동으로 다시 읽고, 아니면
   `폴더 권한이 만료되었습니다.`가 보인다. **둘 다 정상이다.**
6. `아이디어`로 돌아가면 Markdown 저작이 그대로 동작한다.

**3번에서 개수가 26이 아니면 멈추고 원인을 찾는다.** 폴더를 잘못 골랐거나(저장소 루트여야 한다)
격리가 일어난 것이다.

- [ ] **Step 9: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 저장소 폴더를 연결해 읽는다

File System Access API로 저장소 루트를 한 번 고르면 핸들을 IndexedDB에 기억한다.
권한이 만료되면 조용히 다시 묻지 않고 버튼을 누르게 한다 - 권한 요청은 사용자
제스처를 요구한다.

파싱 실패한 파일은 격리하고 나머지를 연다. 카드 한 장이 깨졌다고 도구가 열리지
않으면 그 카드를 고칠 수단도 함께 사라진다.

새 화면은 별도 스크립트 블록이고 기존 Markdown UI는 한 줄도 바뀌지 않는다.
헤더의 모드 전환이 둘 중 하나만 보여준다."
```

---

### Task 6: 요약 화면과 격리·오류 목록

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (`<script data-repo-ui>`)
- Test: `Tools/card-idea-notebook/index.test.mjs` (마크업 검사)

**Interfaces:**
- Consumes: Task 4의 `contentSummary`·`summaryLines`, Task 5의 `repoStore`
- Produces: `renderSummary()` — 요약 두 줄과 오류·격리 목록을 그린다. Task 7·8이 `render()`에서
  함께 호출된다. 항목을 누르면 `selectEntry(scope, id)`를 부른다.

설계 §11.2 그대로다 — 오류나 충돌이 있으면 그 목록이 요약 자리를 차지하고, 항목을 누르면 해당
카드·풀로 이동한다. `selectEntry`는 이 태스크에서 선택만 기록하고, 실제 이동은 Task 7·8이 채운다.

- [ ] **Step 1: 실패하는 마크업 테스트를 쓴다**

```js

test("요약과 문제 목록 자리가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="repo-summary"/);
  assert.match(html, /id="repo-problems"/);
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 1개.** `id="repo-problems"`를 찾지 못한다.

- [ ] **Step 3: 마크업에 문제 목록 자리를 넣는다**

Task 5가 넣은 `<div class="repo-summary" id="repo-summary"></div>` **바로 뒤**에 넣는다.

```html
        <div class="repo-problems" id="repo-problems"></div>
```

`<style>`의 `.repo-detail` 규칙 뒤에 넣는다.

```css
    .repo-problems { padding: 0 16px 14px; display: grid; gap: 6px; }
    .repo-problem {
      display: grid;
      gap: 2px;
      padding: 8px 10px;
      border: 1px solid var(--line);
      border-radius: 9px;
      background: #191b18;
      text-align: left;
      font-size: 12px;
      line-height: 1.5;
    }
    .repo-problem .where { color: var(--muted); font-size: 11px; }
    .repo-problem.is-error { border-color: var(--danger); }
    .repo-problem.is-warning { border-color: var(--warning); }
```

- [ ] **Step 4: 요약 렌더러를 쓴다**

`<script data-repo-ui>` 안, `render()` **바로 위**에 넣는다.

```js
    const PROBLEM_LABELS = Object.freeze({
      card: "카드",
      pool: "풀",
      status: "상태",
      file: "파일",
    });

    let selection = { scope: "", id: "" };

    function selectEntry(scope, id) {
      selection = { scope, id };
      render();
    }

    function problemButton({ level, scope, id, message }) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = `repo-problem is-${level}`;

      const where = document.createElement("span");
      where.className = "where";
      where.textContent = `${PROBLEM_LABELS[scope] ?? scope} · ${id}`;
      button.append(where);

      const text = document.createElement("span");
      text.textContent = message;
      button.append(text);

      button.addEventListener("click", () => selectEntry(scope, id));
      return button;
    }

    /// 격리된 파일을 오류보다 먼저 보여준다. 그 파일들은 아예 읽히지 않아 검증기가 보지도
    /// 못했으므로, 검증 오류 0이라고 안심하면 안 되기 때문이다(설계 10.2).
    function renderProblems() {
      elements.problems.replaceChildren();
      if (!repoStore.connected) return;

      for (const entry of repoStore.quarantined) {
        elements.problems.append(problemButton({
          level: "error",
          scope: entry.scope,
          id: entry.name,
          message: `${entry.messages.join(" ")} (읽지 못해 격리했습니다)`,
        }));
      }

      for (const error of repoStore.validation.errors) {
        elements.problems.append(problemButton({ level: "error", ...error }));
      }

      for (const warning of repoStore.validation.warnings) {
        elements.problems.append(problemButton({ level: "warning", ...warning }));
      }
    }

    function renderSummary() {
      elements.summary.replaceChildren();

      if (repoStore.message) {
        const line = document.createElement("p");
        line.textContent = repoStore.message;
        elements.summary.append(line);
        return;
      }
      if (!repoStore.connected) {
        const line = document.createElement("p");
        line.textContent = "폴더를 연결하세요.";
        elements.summary.append(line);
        return;
      }

      const summary = core.contentSummary({
        cards: repoStore.cards,
        pools: repoStore.pools,
        statuses: repoStore.statuses,
        validation: repoStore.validation,
        cardStates: repoStore.cardStates,
        poolStates: repoStore.poolStates,
      });

      for (const text of core.summaryLines(summary)) {
        const line = document.createElement("p");
        line.textContent = text;
        elements.summary.append(line);
      }

      // 상태가 0이면 폴더 선택이 잘못됐다는 신호다(설계 11.5).
      if (!repoStore.statuses.length) {
        const line = document.createElement("p");
        line.textContent = "상태를 하나도 읽지 못했습니다. 저장소 루트를 고른 것이 맞나요?";
        elements.summary.append(line);
      }
    }
```

- [ ] **Step 5: `elements`와 `render()`를 잇는다**

`elements` 객체에서 `summary: byId("repo-summary"),` **바로 뒤**에 한 줄 더한다.

```js
      problems: byId("repo-problems"),
```

그리고 `render()`를 통째로 바꾼다.

```js
    function render() {
      elements.reload.disabled = !repoStore.connected;
      elements.count.textContent = repoStore.connected
        ? String(repoStore.cards.length)
        : "–";
      renderSummary();
      renderProblems();
    }
```

- [ ] **Step 6: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **124/124** (123 + 신규 1).

- [ ] **Step 7: 브라우저에서 수동 검수한다**

Task 5 Step 8과 같은 방법으로 열고 폴더를 연결한 뒤 확인한다.

1. 요약 두 줄이 `카드 26 · 풀 1 · 상태 11 을 읽었습니다` / `오류 0 · 충돌 0 · 미반영 0`이다.
2. 문제 목록에 **경고 넷**이 보인다 — `fixture_all_block`·`fixture_attack`·
   `fixture_move_forward`·`fixture_selected_block`의 `어느 풀에도 없습니다.`
3. 카드 하나를 일부러 깨뜨려 격리가 보이는지 확인한다.

   ```bash
   cp Assets/StreamingAssets/Content/Cards/vanguard_slash.json /private/tmp/vanguard_slash.json.bak
   printf '{ 깨짐' > Assets/StreamingAssets/Content/Cards/vanguard_slash.json
   ```

   `저장소 다시 읽기`를 누르면 **도구가 계속 열려 있고** 카드가 25로 줄며 문제 목록 맨 위에
   `카드 · vanguard_slash.json … (읽지 못해 격리했습니다)`가, 그 아래 풀의
   `없는 카드입니다: 'vanguard_slash'`가 보인다.

   ```bash
   cp /private/tmp/vanguard_slash.json.bak Assets/StreamingAssets/Content/Cards/vanguard_slash.json
   git status --short Assets/StreamingAssets/Content/Cards/
   ```

   **`git status`가 깨끗해야 한다.** 그렇지 않으면 복원이 안 된 것이므로 멈추고 고친다.

- [ ] **Step 8: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 읽은 결과와 문제를 요약한다

격리된 파일을 검증 오류보다 먼저 보여준다. 그 파일들은 아예 읽히지 않아 검증기가
보지도 못했으므로, 오류 0이라고 안심하면 안 된다.

상태를 하나도 못 읽으면 폴더 선택이 잘못됐다는 신호이므로 따로 알린다."
```

---

### Task 7: 카드 화면 — 목록·배지·원문

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (마크업의 탭·검색·필터, `<script data-repo-ui>`)
- Test: `Tools/card-idea-notebook/index.test.mjs` (마크업 검사)

**Interfaces:**
- Consumes: Task 3의 `CARD_FILTERS`·`cardListView`, Task 6의 `selectEntry`
- Produces: `renderList()`가 탭에 따라 카드 목록 또는 풀 목록을 그린다(풀은 Task 8).
  `renderDetail()`이 선택한 카드의 요약을, 우측이 원문을 그린다.

설계 §11.3의 목록 줄 셋이 기준이다 — 이름과 `id`, 그 아래 속성 줄, 그리고 태그와 상태 배지.
편집기는 계획 C이므로 **중앙 판에는 읽기 전용 요약만** 넣고, 원문은 우측이 맡는다.

- [ ] **Step 1: 실패하는 마크업 테스트를 쓴다**

```js

test("저장소 탭과 검색·필터가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="repo-tab-cards"/);
  assert.match(html, /id="repo-tab-pools"/);
  assert.match(html, /id="repo-search"/);
  assert.match(html, /id="repo-filter"/);
  for (const label of ["전체", "수정됨만", "충돌만", "오류만", "고아만"]) {
    assert.match(html, new RegExp(`>${label}<\\/option>`));
  }
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 1개.** `id="repo-tab-cards"`를 찾지 못한다.

- [ ] **Step 3: 마크업에 탭과 검색·필터를 넣는다**

Task 5가 넣은 `<button type="button" id="repo-reload" disabled>저장소 다시 읽기</button>`
**바로 뒤**에 넣는다.

```html
          <div class="repo-tabs" role="group" aria-label="목록 종류">
            <button type="button" id="repo-tab-cards" class="mode-button is-active">카드</button>
            <button type="button" id="repo-tab-pools" class="mode-button">풀</button>
          </div>
          <label>
            검색
            <input id="repo-search" type="search" placeholder="이름 · id · 태그">
          </label>
          <label>
            필터
            <select id="repo-filter">
              <option value="all">전체</option>
              <option value="modified">수정됨만</option>
              <option value="conflict">충돌만</option>
              <option value="error">오류만</option>
              <option value="orphan">고아만</option>
            </select>
          </label>
```

`<style>`의 `.repo-problem.is-warning` 뒤에 넣는다.

```css
    .repo-tabs { display: flex; gap: 4px; margin-bottom: 10px; }
    .repo-entry {
      display: grid;
      gap: 3px;
      width: 100%;
      padding: 9px 11px;
      border: 1px solid var(--line);
      border-radius: 11px;
      background: var(--panel);
      text-align: left;
      font-size: 12px;
      line-height: 1.5;
    }
    .repo-entry.is-selected { border-color: var(--gold); }
    .repo-entry .headline { display: flex; justify-content: space-between; gap: 8px; }
    .repo-entry .headline .id { color: var(--muted); font-size: 11px; }
    .repo-entry .meta { color: var(--muted); font-size: 11px; }
    .repo-entry .badge { font-size: 11px; }
    .repo-entry .badge.is-conflict { color: var(--danger); }
    .repo-entry .badge.is-modified,
    .repo-entry .badge.is-new { color: var(--warning); }
    .repo-entry .badge.is-error { color: var(--danger); }
    .repo-fields { display: grid; gap: 6px; font-size: 13px; }
    .repo-fields div { display: flex; gap: 10px; }
    .repo-fields dt { min-width: 84px; color: var(--muted); }
```

- [ ] **Step 4: 카드 목록과 상세 렌더러를 쓴다**

`<script data-repo-ui>` 안, `render()` **바로 위**에 넣는다.

```js
    const STATE_BADGES = Object.freeze({
      same: "● 저장소와 동일",
      modified: "✎ 수정됨",
      new: "＋ 신규",
      conflict: "⚠ 충돌",
      missing: "− 저장소에만 있음",
    });
    const SIDE_LABELS = Object.freeze({ Player: "아군", Enemy: "적군" });
    const CATEGORY_LABELS = Object.freeze({ Execution: "실행", Intervention: "개입" });

    let tab = "cards";
    let query = "";
    let filter = "all";

    /// 검증 오류가 붙은 카드 id. 목록의 `오류만` 필터와 줄 배지가 같은 판정을 쓰도록
    /// 한 곳에서 만든다.
    function erroredCardIds() {
      const ids = new Set();
      for (const error of repoStore.validation.errors) {
        if (error.scope === "card") ids.add(error.id);
      }
      for (const entry of repoStore.quarantined) {
        if (entry.scope === "card") ids.add(entry.name.replace(/\.json$/, ""));
      }

      return ids;
    }

    function cardEntry(row) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "repo-entry";
      if (selection.scope === "card" && selection.id === row.card.id) {
        button.classList.add("is-selected");
      }

      const headline = document.createElement("div");
      headline.className = "headline";
      const name = document.createElement("span");
      name.textContent = row.card.name || "(이름 없음)";
      const id = document.createElement("span");
      id.className = "id";
      id.textContent = row.card.id || "(id 없음)";
      headline.append(name, id);
      button.append(headline);

      const meta = document.createElement("div");
      meta.className = "meta";
      const parts = [
        SIDE_LABELS[row.card.side] ?? row.card.side,
        CATEGORY_LABELS[row.card.category] ?? row.card.category,
        `비용${row.card.energyCost}`,
      ];
      if (row.card.category === "Execution") parts.push(`순서${row.card.baseExecutionOrder}`);
      if (row.pools.length) parts.push(row.pools.join("·"));
      meta.textContent = parts.join(" · ");
      button.append(meta);

      const badge = document.createElement("div");
      badge.className = `badge is-${row.hasError ? "error" : row.state}`;
      const tags = (row.card.tags ?? []).join(" · ");
      badge.textContent = row.hasError
        ? `⚠ 오류${tags ? ` — ${tags}` : ""}`
        : `${STATE_BADGES[row.state] ?? row.state}${tags ? ` — ${tags}` : ""}`;
      button.append(badge);

      button.addEventListener("click", () => selectEntry("card", row.card.id));
      return button;
    }

    function renderCardList() {
      const rows = core.cardListView({
        cards: repoStore.cards,
        states: repoStore.cardStates,
        errorIds: erroredCardIds(),
        membership: repoStore.membership,
        query,
        filter,
      });

      elements.count.textContent = `${rows.length}/${repoStore.cards.length}`;
      for (const row of rows) elements.list.append(cardEntry(row));
    }

    function fieldRow(term, value) {
      const row = document.createElement("div");
      const dt = document.createElement("dt");
      dt.textContent = term;
      const dd = document.createElement("dd");
      dd.textContent = value;
      row.append(dt, dd);
      return row;
    }

    /// 편집기는 계획 C다. 여기서는 카드가 무엇인지 읽을 수 있을 만큼만 보여주고,
    /// 실제 값의 원본은 우측 원문이 맡는다.
    function renderCardDetail(card) {
      elements.detailTitle.textContent = `${card.name} · ${card.id}`;

      const fields = document.createElement("dl");
      fields.className = "repo-fields";
      fields.append(
        fieldRow("진영", SIDE_LABELS[card.side] ?? card.side),
        fieldRow("분류", CATEGORY_LABELS[card.category] ?? card.category),
        fieldRow("비용", String(card.energyCost)),
        fieldRow("등급", card.grade),
        fieldRow("태그", (card.tags ?? []).join(" · ") || "없음"),
        fieldRow("소속 풀", (repoStore.membership.get(card.id) ?? []).join(" · ") || "없음"),
      );

      if (card.category === "Execution") {
        fields.append(
          fieldRow("실행 순서", String(card.baseExecutionOrder)),
          fieldRow("효과", card.effects === null
            ? "없음"
            : card.effects.map((effect) => effect.kind).join(" → ") || "0개"),
        );
      } else {
        fields.append(fieldRow("개입", card.intervention?.kind ?? "없음"));
      }

      if (card.unknownKeys.length) {
        fields.append(fieldRow("모르는 키", card.unknownKeys.join(" · ")));
      }

      elements.detail.append(fields);
      elements.sourceTitle.textContent = `Cards/${card.id}.json`;
      elements.source.textContent = card.base;
    }
```

- [ ] **Step 5: `render()`에 목록·상세를 잇는다**

`render()`를 통째로 바꾼다.

```js
    function render() {
      elements.reload.disabled = !repoStore.connected;
      elements.list.replaceChildren();
      elements.detail.replaceChildren();
      elements.detailTitle.textContent = "선택된 항목 없음";
      elements.sourceTitle.textContent = "JSON 원문";
      elements.source.textContent = "";

      renderSummary();
      renderProblems();

      if (!repoStore.connected) {
        elements.count.textContent = "–";
        return;
      }

      if (tab === "cards") renderCardList();

      if (selection.scope === "card") {
        const card = repoStore.cardsById.get(selection.id);
        if (card) renderCardDetail(card);
      }
    }
```

그리고 이벤트 배선을 `elements.reload.addEventListener(...)` **바로 뒤**에 더한다.

```js
    elements.search.addEventListener("input", () => {
      query = elements.search.value;
      render();
    });
    elements.filter.addEventListener("change", () => {
      filter = elements.filter.value;
      render();
    });
    elements.tabCards.addEventListener("click", () => setTab("cards"));
    elements.tabPools.addEventListener("click", () => setTab("pools"));
```

`elements` 객체에서 `list: byId("repo-list"),` **바로 뒤**에 넉 줄 더한다.

```js
      tabCards: byId("repo-tab-cards"),
      tabPools: byId("repo-tab-pools"),
      search: byId("repo-search"),
      filter: byId("repo-filter"),
```

그리고 `setMode` **바로 뒤**에 넣는다.

```js
    /// 탭은 좌측 목록만 바꾼다. 중앙과 우측은 선택한 대상을 따라가므로(설계 11.1)
    /// 탭을 옮길 때 선택을 비운다 - 카드 탭에서 고른 카드가 풀 탭에 남으면 어긋난다.
    function setTab(next) {
      tab = next;
      selection = { scope: "", id: "" };
      elements.tabCards.classList.toggle("is-active", next === "cards");
      elements.tabPools.classList.toggle("is-active", next === "pools");
      render();
    }
```

- [ ] **Step 6: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **125/125** (124 + 신규 1).

- [ ] **Step 7: 브라우저에서 수동 검수한다**

폴더를 연결한 뒤 확인한다.

1. 카드 26줄이 보이고 개수가 `26/26`이다. 각 줄에 이름과 `id`, 진영·분류·비용·순서·소속 풀,
   그리고 `● 저장소와 동일 — 시작 · 공격`이 보인다.
2. 검색에 `독`을 치면 목록이 줄고 개수가 따라 바뀐다. 지우면 26으로 돌아온다.
3. 필터를 `고아만`으로 바꾸면 `fixture_*` **넷만** 남는다.
4. 카드를 누르면 중앙에 속성이, 우측에 `Cards/<id>.json` 원문이 그대로 보인다.
   **원문이 저장소 파일과 한 글자도 다르지 않아야 한다.**
5. 요약의 경고를 누르면 그 카드가 선택된다.

- [ ] **Step 8: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 저장소 카드를 목록과 원문으로 보여준다

목록 줄에 id를 함께 낸다 - 파일명이자 풀이 참조하는 키라 이름보다 중요하다.
검색·필터 규칙은 코어의 순수 함수에 있고 화면은 그 결과만 반복한다.

탭을 옮길 때 선택을 비운다. 중앙과 우측이 선택한 대상을 따라가므로 카드 탭에서
고른 것이 풀 탭에 남으면 어긋난다."
```

---

### Task 8: 풀 화면 — 목록·편성·분포·오류

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (`<script data-repo-ui>`)
- Test: `Tools/card-idea-notebook/index.test.mjs` (마크업 검사)

**Interfaces:**
- Consumes: Task 2의 `poolDistribution`, Task 7의 `selectEntry`·`fieldRow`
- Produces: `renderPoolList()`·`renderPoolDetail(pool)` — `render()`가 탭에 따라 부른다.
  이 태스크가 끝나면 계획 B가 끝난다.

설계 §11.4 그대로다. 편성·분포·오류 세 덩어리이며 **드래그 재정렬과 담기·빼기는 계획 C**다.
분포를 편성 바로 아래 두는 이유는 풀이 후보 집합이라 밸런스를 집합 단위로 봐야 하기 때문이다.

- [ ] **Step 1: 실패하는 마크업 테스트를 쓴다**

```js

test("풀 편성·분포 자리의 스타일이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /\.pool-roster\b/);
  assert.match(html, /\.pool-distribution\b/);
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 1개.** `.pool-roster`를 찾지 못한다.

- [ ] **Step 3: CSS를 넣는다**

`<style>`의 `.repo-fields dt` 뒤에 넣는다.

```css
    .pool-section { margin-top: 16px; }
    .pool-section h4 { margin-bottom: 8px; color: var(--muted); font-size: 12px; }
    .pool-roster { display: grid; gap: 5px; font-size: 12px; }
    .pool-roster .slot {
      display: grid;
      grid-template-columns: 28px minmax(0, 1fr) auto;
      gap: 8px;
      align-items: baseline;
      padding: 6px 9px;
      border: 1px solid var(--line);
      border-radius: 9px;
    }
    .pool-roster .slot.is-missing { border-color: var(--danger); color: var(--danger); }
    .pool-roster .slot .index { color: var(--dim); }
    .pool-roster .slot .tail { color: var(--muted); font-size: 11px; }
    .pool-distribution { display: grid; gap: 6px; font-size: 12px; }
    .pool-distribution div { display: flex; gap: 10px; }
    .pool-distribution dt { min-width: 48px; color: var(--muted); }
```

- [ ] **Step 4: 풀 렌더러를 쓴다**

`renderCardDetail` **바로 뒤**에 넣는다.

```js
    function poolEntry(pool) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "repo-entry";
      if (selection.scope === "pool" && selection.id === pool.id) {
        button.classList.add("is-selected");
      }

      const headline = document.createElement("div");
      headline.className = "headline";
      const name = document.createElement("span");
      name.textContent = pool.id;
      const count = document.createElement("span");
      count.className = "id";
      count.textContent = `${pool.cards.length}장`;
      headline.append(name, count);
      button.append(headline);

      const state = repoStore.poolStates.get(pool.id) ?? "same";
      const badge = document.createElement("div");
      badge.className = `badge is-${state}`;
      badge.textContent = STATE_BADGES[state] ?? state;
      button.append(badge);

      button.addEventListener("click", () => selectEntry("pool", pool.id));
      return button;
    }

    function renderPoolList() {
      elements.count.textContent = String(repoStore.pools.length);
      for (const pool of repoStore.pools) elements.list.append(poolEntry(pool));
    }

    function distributionRow(term, entries, format) {
      return fieldRow(term, entries.length
        ? entries.map((entry) => format(entry)).join(" · ")
        : "없음");
    }

    function poolSection(title, body) {
      const section = document.createElement("section");
      section.className = "pool-section";
      const heading = document.createElement("h4");
      heading.textContent = title;
      section.append(heading, body);
      return section;
    }

    /// 편성 · 분포 · 오류 세 덩어리(설계 11.4). 분포가 편성 바로 아래인 이유는 풀이 후보
    /// 집합이라 밸런스를 집합 단위로 봐야 하기 때문이다.
    /// 드래그 재정렬과 담기·빼기는 계획 C다.
    function renderPoolDetail(pool) {
      const distribution = core.poolDistribution({ pool, cardsById: repoStore.cardsById });
      elements.detailTitle.textContent = `${pool.id} · ${distribution.total}장`;

      const roster = document.createElement("div");
      roster.className = "pool-roster";
      pool.cards.forEach((cardId, index) => {
        const card = repoStore.cardsById.get(cardId);
        const slot = document.createElement("div");
        slot.className = card ? "slot" : "slot is-missing";

        const order = document.createElement("span");
        order.className = "index";
        order.textContent = String(index + 1);

        const label = document.createElement("span");
        label.textContent = card ? `${card.name}  ${cardId}` : `⚠ 없는 카드  ${cardId}`;

        const tail = document.createElement("span");
        tail.className = "tail";
        tail.textContent = card ? [card.grade, ...(card.tags ?? [])].join(" · ") : "";

        slot.append(order, label, tail);
        roster.append(slot);
      });
      elements.detail.append(poolSection("편성", roster));

      const stats = document.createElement("dl");
      stats.className = "pool-distribution";
      stats.append(
        distributionRow("등급", distribution.grades, (e) => `${e.value} ${e.count}`),
        distributionRow("태그", distribution.tags, (e) => `${e.value} ${e.count}`),
        distributionRow("비용", distribution.costs, (e) => `${e.value}: ${e.count}`),
        distributionRow("순서", distribution.orders, (e) => `${e.value}: ${e.count}`),
      );
      elements.detail.append(poolSection("분포", stats));

      const failures = repoStore.validation.errors
        .filter((error) => error.scope === "pool" && error.id === pool.id);
      const problems = document.createElement("div");
      problems.className = "repo-problems";
      if (failures.length) {
        for (const error of failures) {
          problems.append(problemButton({ level: "error", ...error }));
        }
      } else {
        const none = document.createElement("p");
        none.textContent = "없음";
        problems.append(none);
      }
      elements.detail.append(poolSection("오류", problems));

      elements.sourceTitle.textContent = `Pools/${pool.id}.json`;
      elements.source.textContent = pool.base;
    }
```

- [ ] **Step 5: `render()`에 풀을 잇는다**

`render()`의 마지막 두 블록을 바꾼다.

```js
      if (tab === "cards") renderCardList();
      else renderPoolList();

      if (selection.scope === "card") {
        const card = repoStore.cardsById.get(selection.id);
        if (card) renderCardDetail(card);
      }
      if (selection.scope === "pool") {
        const pool = repoStore.pools.find((entry) => entry.id === selection.id);
        if (pool) renderPoolDetail(pool);
      }
```

**요약의 풀 오류를 누르면 풀 탭으로 옮겨야 한다.** `selectEntry`를 바꾼다.

```js
    function selectEntry(scope, id) {
      selection = { scope, id };
      if (scope === "card" || scope === "pool") {
        const next = scope === "card" ? "cards" : "pools";
        tab = next;
        elements.tabCards.classList.toggle("is-active", next === "cards");
        elements.tabPools.classList.toggle("is-active", next === "pools");
      }
      render();
    }
```

- [ ] **Step 6: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

기대: 노트북 **126/126** (125 + 신규 1), 헤드리스 **526/526** (이 계획은 C#을 건드리지 않는다).

- [ ] **Step 7: 브라우저에서 최종 검수한다**

폴더를 연결한 뒤 확인한다.

1. `풀` 탭에 `starter  22장  ● 저장소와 동일` 한 줄이 보인다.
2. 누르면 중앙에 편성 22줄이 **저장 순서 그대로**(1번 `선봉 베기  vanguard_slash`,
   22번 `posthumous_spread`) 나온다.
3. 분포가 `등급 Common 22` / `비용 1: 19 · 2: 3` / `순서 3: 2 · 4: 5 · 5: 8 · 6: 2 · 7: 1`이다.
   태그는 `시작 22`가 맨 앞이다.
4. 오류가 `없음`이다.
5. 우측이 `Pools/starter.json` 원문이고 저장소 파일과 같다.
6. `카드` 탭으로 돌아갔다가 다시 오면 상태가 유지된다.
7. 풀에 없는 카드 id를 손으로 넣어 확인한다.

   ```bash
   cp Assets/StreamingAssets/Content/Pools/starter.json /private/tmp/starter.json.bak
   node -e "const f='Assets/StreamingAssets/Content/Pools/starter.json';const fs=require('fs');const p=JSON.parse(fs.readFileSync(f,'utf8'));p.cards.push('ghost_card');fs.writeFileSync(f,JSON.stringify(p,null,2)+'\n')"
   ```

   `저장소 다시 읽기` 후: 편성 23번 줄이 `⚠ 없는 카드  ghost_card`로 붉게 보이고, 오류에
   `없는 카드입니다: 'ghost_card'`가 나오며, **분포의 등급 합은 22 그대로다.**

   ```bash
   cp /private/tmp/starter.json.bak Assets/StreamingAssets/Content/Pools/starter.json
   git status --short Assets/StreamingAssets/Content/
   ```

   **`git status`가 깨끗해야 한다.**

- [ ] **Step 8: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 풀 편성과 분포를 보여준다

편성 · 분포 · 오류 세 덩어리다. 분포가 편성 바로 아래인 이유는 풀이 후보 집합이라
밸런스를 집합 단위로 봐야 하기 때문이다.

없는 카드는 편성에서 지우지 않고 붉게 표시하며 분포에서만 뺀다 - 조용히 지우면
노트북을 열었다 닫는 것만으로 편성이 바뀐다."
```

---

## 완료 기준

계획 B가 끝났을 때:

1. `node --test "Tools/card-idea-notebook/*.test.mjs"`가 **126/126** 통과한다.
2. `dotnet test`가 **526/526** 통과한다 — 이 계획은 C#을 건드리지 않는다.
3. 노트북을 Chrome·Edge에서 로컬 서버로 열고 저장소 루트를 고르면 **카드 26 · 풀 1 · 상태 11**을
   읽고, 오류 0 · 충돌 0 · 미반영 0에 경고 넷(`fixture_*`의 고아 경고)이 나온다.
4. 카드 파일 하나를 일부러 깨뜨려도 **도구가 열리고** 그 파일만 격리되며 나머지 25장이 정상으로
   보인다.
5. 풀에 없는 카드 id를 손으로 넣으면 편성에 남은 채 붉게 표시되고 오류가 뜬다.
6. **`아이디어` 모드가 계획 A 시점과 똑같이 동작한다** — Markdown 저작·내보내기·불러오기.
7. 검수 중 만든 임시 변경이 전부 복원되어 `git status`가 깨끗하다.

설계 §16 검수 기준 중 이 계획이 담당하는 것은 **6**(깨진 파일에도 도구가 열린다)과
**8의 절반**(없는 카드 id를 유지한다 — 내보내기 차단은 계획 C)이다. 나머지(2·4·5, 8의 나머지)는
편집과 쓰기가 필요하므로 계획 C가 맡는다.

## 다음

계획 B가 머지되면 계획 C를 작성한다. 범위는 설계 §6 효과·개입 편집기, 카드 기본 필드 폼,
§7 풀 편성 조작과 카드 화면의 소속 풀 체크박스, 미반영 저장과 §14 마이그레이션
(`SCHEMA_VERSION` 7), §10.3 충돌 해결 UI, §12 diff 요약과 파일 쓰기(권한을 `readwrite`로 승격),
Markdown 경로와 그 스크립트 제거, `시작 카드 풀.md` 삭제,
[플레이어 캐릭터 및 카드풀](../specs/2026-07-20-character-card-pools-design.md) §1 개정,
옛 노트북 스펙 `archive/` 이동이다.
