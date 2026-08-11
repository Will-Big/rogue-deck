# 카드 저작 노트북 편집 구현 계획 (계획 C)

- 작성일: 2026-08-07
- 완료일: 2026-08-07 — Task 1~8 전부 실행. 실행 기록은 맨 아래에 있다
- 상태: `completed`
- 설계: [카드 저작 노트북 JSON 전환](../specs/2026-08-05-card-authoring-json-notebook-design.md)
- 선행: [노트북 JSON 코어 (계획 A)](../archive/plans/2026-08-05-notebook-json-core.md),
  [노트북 저장소 읽기 UI (계획 B)](../archive/plans/2026-08-07-notebook-repo-read-ui.md) — 둘 다 2026-08-07 완료·머지

## 설계 개요 (사람 검수용)

이 절만 읽고 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은 읽지
않아도 된다.

**무엇을 만드나** — 노트북이 읽어 온 카드·풀을 **편집**할 수 있게 한다. 기본 필드 폼, 구조화
효과·개입 편집기, 풀 편성 조작, 그리고 편집분을 브라우저에 보관하는 미반영 저장소.
**파일은 쓰지 않는다** — 저장소 반영은 계획 D다.

**구조**

| 객체 | 책임 (한 줄) | 이 객체가 모르는 것 |
|---|---|---|
| 편집 명령 | 카드·풀 모델에 변경 하나를 적용해 새 모델을 돌려준다 | 화면, 어디에 보관되는지 |
| 미반영 보관소 | 저장소와 다른 편집분만 브라우저에 보관한다 | 편집의 의미, 유효성 |
| 폼 렌더러 | 스키마의 필드 타입을 입력 컨트롤로 바꾼다 | 카드의 값이 유효한지 |
| 효과 행 편집기 | 효과·개입 행 하나를 그리고 조작한다 | 카드의 나머지 필드 |
| 풀 편성 편집기 | 풀의 카드 목록과 순서를 조작한다 | 카드의 내용 |
| 저작 화면 조정자 | 편집 이벤트를 명령으로 옮기고 다시 그린다 | 규칙 판단, 표시 변환 |

**의존 방향** — `편집기(입력) → 조정자 → 편집 명령 → 미반영 보관소 → 편집기(표시)`

**확장 축**
- *갈아끼울 수 있는 것* — 효과와 개입의 종류. C#에 스펙을 더하면 스키마가 따라오고 **폼이 저절로
  생긴다.** 계획 A가 스키마를 만든 이유가 여기서 처음 값을 낸다.
- *한번 정하면 고정되는 것* — 파일 쓰기는 계획 D. 저작 필드 타입 넷(정수·불리언·상태키·열거).
  미반영은 `localStorage` 하나다.

**대안과 기각 이유**
1. *편집과 쓰기를 한 계획에* — 기각. 쓰기가 저장소를 직접 바꾸고 안전망이 git뿐이라, 편집 모델이
   옳다는 확신 없이 쓰기를 붙이면 실패가 저장소에 남는다. 계획 A→B와 같은 이유이고, 태스크도
   12개로 불어난다.
2. *카드 저작을 먼저 완결하고 풀은 나중에* — 기각. 새로 만든 카드는 풀에 들어가야 게임에 나오므로,
   카드만 쓸 수 있으면 "저작 완결"이 아니라 고아 카드 양산이다. 설계 §7이 카드 화면에 소속 풀
   체크박스를 둔 것도 그래서다.

**이 선택으로 나중에 어려워지는 것**
- 계획 C가 끝나도 **저장소에는 아무것도 반영되지 않는다.** 편집분이 브라우저에만 쌓이므로, 그
  상태로 브라우저 프로필을 잃으면 작업이 사라진다. 계획 D까지 그렇다.
- **Markdown 경로가 계획 D 끝까지 남는다.** 한 파일에 스크립트 셋이 공존하고 3700줄을 넘는다.
- 편집이 붙으면 `id`가 저작 중에 바뀐다. 계획 B가 상태·선택을 **카드 `id`로 색인**해 뒀으므로
  **`uid`로 바꾸는 작업이 이 계획에 포함된다.** 미루면 id를 고치는 순간 선택이 튄다.
- 미반영을 `localStorage` 하나에 담으므로 **브라우저 용량 한계(대개 5~10MB)가 그대로 상한이다.**
  카드 26장 규모에서는 문제가 없지만 수백 장이 되면 다시 봐야 한다.

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.

> **에이전트 작업자에게:** 필수 서브 스킬 — `superpowers:executing-plans`로 태스크 단위로
> 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

**목표:** 노트북에서 카드와 풀을 편집하고, 그 편집분이 브라우저에 보존되며, 저장소와 다르다는
사실이 화면에 나타난다. 파일은 쓰지 않는다.

**아키텍처:** 편집 규칙은 전부 **코어 스크립트의 순수 함수**로 넣어 `node:test`로 잠근다
(Task 1~5). 폼과 이벤트는 계획 B가 만든 **`<script data-repo-ui>` 블록**에만 둔다(Task 6~8).
기존 Markdown UI 스크립트는 이 계획에서도 한 줄도 건드리지 않는다.

**기술 스택:** 브라우저 JS (빌드 없음), `localStorage`, Node `node:test`

## 설계 §14를 이 계획에서 하지 않는 이유

설계 §14는 `SCHEMA_VERSION` 6 → 7 전환과 옛 데이터 백업을 말한다. **그것은 계획 D 몫이다.**

`SCHEMA_VERSION`과 `STORAGE_KEY`는 **아직 살아 있는 Markdown 경로가 쓰는 것**이다
(`readStore`·`writeStore`·`normalizeCard`). 이 계획에서 버전을 올리면 Markdown 저작이 그 자리에서
깨진다 — 계획 B·C가 지켜 온 "옛 경로 무변경" 약속을 어긴다.

그래서 미반영은 **별도 키**(`fate-weaver.card-idea-notebook.pending`)에 **자체 버전 1**로 담는다.
두 저장소가 잠시 공존하고, 계획 D가 Markdown 경로를 지울 때 §14대로 옛 키를 백업으로 옮긴다.
설계의 의도(옛 데이터를 지우지 않고 안내한다)는 그대로이고 시점만 옮긴다.

## 전역 제약

- **규칙 14:** 외부 패키지를 추가하지 않는다. 노트북은 의존성 0으로 유지한다.
- **규칙 15:** 메인 체크아웃의 브랜치를 전환하지 않는다. 전용 워크트리에서 작업한다.
- **규칙 17:** 여백·색·타이포처럼 **눈으로 맞춰야 하는 저작은 사용자 몫이다.** 이 계획의 CSS는
  배치가 성립하는 최소치만 넣는다.
- **규칙 27:** 커밋 메시지는 `타입(범위): 한국어 제목`이고 제목은 "…한다"로 끝난다.
- **테스트 하니스는 `<script data-card-idea-core>` 블록만 읽는다.** 테스트할 가치가 있는 로직은
  전부 코어 블록에 둔다.
- **기존 Markdown UI 스크립트(2302~3057줄)와 그 마크업을 건드리지 않는다.**
- **이 계획은 파일을 쓰지 않는다.** 폴더 권한은 `read` 그대로다.
- **모델을 제자리에서 고치지 않는다.** 편집 명령은 언제나 새 객체를 돌려준다 — 계획 B의
  `resolveCardState`가 `base`와 "지금 쓰면 나올 문자열"을 비교하므로, 모델을 뒤집으면 비교 대상이
  함께 바뀌어 상태 판정이 무너진다.

## 검증 명령

**노트북 단위 테스트** (모든 태스크 끝에서 실행):

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

**헤드리스** (Task 8 끝에서 한 번. 이 계획은 C#을 건드리지 않으므로 회귀 확인용이다):

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

**브라우저 수동 검수** (Task 6부터):

```bash
python3 -m http.server 8765 --directory /Users/ish/Git/rogue-deck
```

Chrome이나 Edge로 `http://localhost:8765/Tools/card-idea-notebook/index.html`을 열고 폴더
선택에서 **저장소 루트**를 고른다. `file://`에서는 `showDirectoryPicker`가 동작하지 않는다.

**폴더 선택 대화상자는 사람이 눌러야 한다.** 계획 B에서 실측한 바로는 에이전트가 그 버튼을 실제로
클릭하면 `AbortError: The user aborted a request.`로 거부된다 — 네이티브 대화상자를 자동화가
띄우지 못한다. 나머지 흐름은 `window.showDirectoryPicker`를 `fetch` 기반 가짜 디렉터리 핸들로
바꿔치면 에이전트도 검수할 수 있고, 계획 B가 그 방법으로 전부 확인했다.

**시작 시점 기준선 (2026-08-07 실측, master `3643854`):** 노트북 **126/126**, 헤드리스
**526/526**. `index.html` **3722줄** — 코어 709~2301, Markdown UI 2302~3057, 저장소 UI 3059~3720.

## 계획 A·B가 남긴 것

| 이름 | 이 계획에서의 쓰임 |
|---|---|
| `readCardJson` / `writeCardJson` | 읽기는 그대로. 쓰기는 **상태 판정에만** 쓴다 |
| `readPoolJson` / `writePoolJson` | 위와 같다 |
| `validateContent` | 편집할 때마다 다시 돌려 오류를 즉시 보여준다 |
| `resolveCardState` / `resolvePoolState` | 편집분이 생기면 `modified`·`new`를 처음으로 낸다 |
| `cardListView` | **색인 키를 `id`에서 `uid`로 바꾼다**(Task 2) |
| `poolDistribution` / `poolMembership` | 편성을 고칠 때마다 다시 계산한다 |
| `parseAuthoringSchema` | 폼 컨트롤의 원천. 이 계획이 스키마의 `fields`를 처음 쓴다 |

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Tools/card-idea-notebook/index.html` 코어 블록 (수정) | Task 1~5의 순수 함수 추가, `readCardJson`의 `uid` 지정 |
| `Tools/card-idea-notebook/index.html` 마크업 (수정) | 새 카드 버튼, 편집 폼 자리 |
| `Tools/card-idea-notebook/index.html` `<script data-repo-ui>` (수정) | Task 6~8의 폼·이벤트 |
| `Tools/card-idea-notebook/index.test.mjs` (수정) | Task 1~5의 테스트, Task 6~8의 마크업 검사 |

---

### Task 1: 미반영 보관소

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록, `globalThis.CardIdeaNotebook` export 직전)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Produces:
  `PENDING_STORAGE_KEY`, `emptyPending()` → `{ version, cards: {}, pools: {} }`,
  `readPending(storage)` → `{ pending, errors }`,
  `writePending(storage, pending)` → `{ persisted, error }`,
  `putPendingCard(pending, card)` / `dropPendingCard(pending, uid)`,
  `putPendingPool(pending, pool)` / `dropPendingPool(pending, id)`,
  `applyPending({ cards, pools, pending })` → `{ cards, pools }`.
  Task 6~8의 조정자가 전부 쓴다.

미반영은 **카드는 `uid`로, 풀은 `id`로** 색인한다. 풀의 `id`는 저작 중에 바뀌지 않는다 — 풀 이름을
바꾸는 기능은 이 계획에도 설계에도 없다.

`applyPending`이 하는 일은 설계 §10.1의 한 줄 그대로다: `저장소 파일 + 미반영 편집분 = 화면에
보이는 것`. 저장소에 있는 것은 대체하고, 저장소에 없는 미반영은 **뒤에 덧붙인다**.

- [x] **Step 1: 실패하는 테스트를 쓴다**

`index.test.mjs` 끝에 붙인다. 파일에 이미 있는 `MemoryStorage`(389줄)를 쓰지 않고 작은 것을 새로
만드는 이유는, 그쪽이 Markdown 경로의 저장 실패 시나리오를 위해 만들어진 것이고 계획 D가 그
테스트들과 함께 지울 후보이기 때문이다.

```js

function pendingStorage() {
  const items = new Map();
  return {
    getItem: (key) => (items.has(key) ? items.get(key) : null),
    setItem: (key, value) => { items.set(key, String(value)); },
    removeItem: (key) => { items.delete(key); },
  };
}

test("미반영이 없으면 빈 상태를 준다", () => {
  const core = loadCore();
  const { pending, errors } = core.readPending(pendingStorage());
  assert.deepEqual(errors, []);
  assert.deepEqual(pending.cards, {});
  assert.deepEqual(pending.pools, {});
});

test("미반영을 쓰고 다시 읽는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const storage = pendingStorage();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  card.name = "바뀐 이름";

  const written = core.writePending(storage, core.putPendingCard(core.emptyPending(), card));
  assert.equal(written.persisted, true);

  const { pending } = core.readPending(storage);
  assert.equal(pending.cards[card.uid].name, "바뀐 이름");
});

test("미반영에서 항목을 뺀다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const added = core.putPendingCard(core.emptyPending(), card);
  assert.equal(Object.keys(added.cards).length, 1);

  const removed = core.dropPendingCard(added, card.uid);
  assert.deepEqual(removed.cards, {});
  assert.equal(Object.keys(added.cards).length, 1, "원본을 제자리에서 고치지 않는다");
});

test("미반영 편집분을 저장소 위에 얹는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const edited = { ...card, name: "덮어쓴 이름" };
  const fresh = { ...card, uid: "new:1", id: "brand_new", name: "새 카드", base: null };

  const merged = core.applyPending({
    cards: [card],
    pools: [],
    pending: core.putPendingCard(core.putPendingCard(core.emptyPending(), edited), fresh),
  });

  assert.equal(merged.cards.length, 2);
  assert.equal(merged.cards[0].name, "덮어쓴 이름", "같은 uid는 대체한다");
  assert.equal(merged.cards[1].id, "brand_new", "저장소에 없는 미반영은 뒤에 붙인다");
});

test("풀도 같은 방식으로 얹는다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool } = core.readPoolJson(text);
  const edited = { ...pool, cards: [...pool.cards, "새_카드"] };

  const merged = core.applyPending({
    cards: [],
    pools: [pool],
    pending: core.putPendingPool(core.emptyPending(), edited),
  });

  assert.equal(merged.pools.length, 1);
  assert.equal(merged.pools[0].cards.length, pool.cards.length + 1);
});

test("깨진 미반영 데이터는 버리고 이유를 준다", () => {
  const core = loadCore();
  const storage = pendingStorage();
  storage.setItem(core.PENDING_STORAGE_KEY, "{ 아님");

  const { pending, errors } = core.readPending(storage);
  assert.deepEqual(pending.cards, {});
  assert.equal(errors.length, 1);
});

test("모르는 미반영 버전은 버리고 이유를 준다", () => {
  const core = loadCore();
  const storage = pendingStorage();
  storage.setItem(core.PENDING_STORAGE_KEY, JSON.stringify({ version: 99, cards: {}, pools: {} }));

  const { pending, errors } = core.readPending(storage);
  assert.deepEqual(pending.cards, {});
  assert.ok(errors.some((message) => message.includes("99")));
});

test("저장에 실패해도 던지지 않고 알린다", () => {
  const core = loadCore();
  const storage = {
    getItem: () => null,
    setItem: () => { throw new Error("quota"); },
    removeItem: () => {},
  };

  const result = core.writePending(storage, core.emptyPending());
  assert.equal(result.persisted, false);
  assert.ok(result.error.includes("quota"));
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 8개.** `core.readPending is not a function`.

- [x] **Step 3: 최소 구현을 쓴다**

`globalThis.CardIdeaNotebook = Object.freeze({` **바로 위**에 넣는다.

```js
    /// Markdown 경로의 STORAGE_KEY와 별개다. 그쪽은 아직 살아 있고 SCHEMA_VERSION을 올리면
    /// 그 자리에서 깨지므로, 미반영은 자체 키와 자체 버전을 쓴다. 설계 14의 6 → 7 전환은
    /// Markdown 경로를 지우는 계획 D가 맡는다.
    const PENDING_STORAGE_KEY = "fate-weaver.card-idea-notebook.pending";
    const PENDING_VERSION = 1;

    function emptyPending() {
      return { version: PENDING_VERSION, cards: {}, pools: {} };
    }

    function readPending(storage) {
      const raw = storage.getItem(PENDING_STORAGE_KEY);
      if (!raw) return { pending: emptyPending(), errors: [] };

      let saved;
      try {
        saved = JSON.parse(raw);
      } catch (error) {
        return {
          pending: emptyPending(),
          errors: [`미반영 편집분을 읽을 수 없습니다: ${error.message}`],
        };
      }

      if (!saved || saved.version !== PENDING_VERSION) {
        return {
          pending: emptyPending(),
          errors: [`지원하지 않는 미반영 데이터 버전: ${saved?.version}`],
        };
      }

      return {
        pending: {
          version: PENDING_VERSION,
          cards: { ...(saved.cards ?? {}) },
          pools: { ...(saved.pools ?? {}) },
        },
        errors: [],
      };
    }

    /// 던지지 않는다. 용량 초과는 저작 중에 실제로 일어나고, 그때 편집을 막는 것보다
    /// 알리고 계속 쓰게 하는 편이 낫다 - 화면의 모델은 여전히 살아 있다.
    function writePending(storage, pending) {
      try {
        storage.setItem(PENDING_STORAGE_KEY, JSON.stringify(pending));
        return { persisted: true, error: "" };
      } catch (error) {
        return { persisted: false, error: String(error?.message ?? error) };
      }
    }

    function putPendingCard(pending, card) {
      return { ...pending, cards: { ...pending.cards, [card.uid]: card } };
    }

    function dropPendingCard(pending, uid) {
      const cards = { ...pending.cards };
      delete cards[uid];
      return { ...pending, cards };
    }

    function putPendingPool(pending, pool) {
      return { ...pending, pools: { ...pending.pools, [pool.id]: pool } };
    }

    function dropPendingPool(pending, id) {
      const pools = { ...pending.pools };
      delete pools[id];
      return { ...pending, pools };
    }

    /// 설계 10.1의 한 줄: 저장소 파일 + 미반영 편집분 = 화면에 보이는 것.
    /// 저장소에 있는 것은 대체하고, 저장소에 없는 미반영은 뒤에 덧붙인다.
    function applyPending({ cards, pools, pending }) {
      const usedCardUids = new Set();
      const mergedCards = cards.map((card) => {
        const edited = pending.cards[card.uid];
        if (!edited) return card;
        usedCardUids.add(card.uid);
        return edited;
      });
      for (const [uid, card] of Object.entries(pending.cards)) {
        if (!usedCardUids.has(uid)) mergedCards.push(card);
      }

      const usedPoolIds = new Set();
      const mergedPools = pools.map((pool) => {
        const edited = pending.pools[pool.id];
        if (!edited) return pool;
        usedPoolIds.add(pool.id);
        return edited;
      });
      for (const [id, pool] of Object.entries(pending.pools)) {
        if (!usedPoolIds.has(id)) mergedPools.push(pool);
      }

      return { cards: mergedCards, pools: mergedPools };
    }
```

export 블록의 `CONTENT_PATHS` **위**에 넣는다:

```js
    globalThis.CardIdeaNotebook = Object.freeze({
      PENDING_STORAGE_KEY,
      emptyPending,
      readPending,
      writePending,
      putPendingCard,
      dropPendingCard,
      putPendingPool,
      dropPendingPool,
      applyPending,
      CONTENT_PATHS,
```

- [x] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **134/134** (기준선 126 + 신규 8).

- [x] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 미반영 편집분을 보관한다

Markdown 경로의 STORAGE_KEY와 별개 키를 쓴다. 그쪽은 아직 살아 있고 SCHEMA_VERSION을
올리면 그 자리에서 깨지므로, 설계 14의 6 to 7 전환은 Markdown 경로를 지우는 계획 D가 맡는다.

저장에 실패해도 던지지 않고 알린다 - 용량 초과는 저작 중에 실제로 일어나고, 그때
편집을 막는 것보다 알리고 계속 쓰게 하는 편이 낫다."
```

---

### Task 2: uid 도입과 색인 전환

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록의 `readCardJson`·`cardListView`, 저장소 UI의 색인)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Produces: `repoUid(id)` → `"file:<id>"`, `newUid(sequence)` → `"new:<n>"`, `isNewUid(uid)`.
  `readCardJson`이 `uid`를 `repoUid(id)`로 채운다. `cardListView`의 `states`가 **uid로 색인된다.**

계획 B는 상태와 선택을 카드 `id`로 색인했다. 이 계획이 편집을 붙이면 **`id`가 저작 중에 바뀌므로**
선택이 튄다. 설계 §5가 `uid`를 두는 이유가 정확히 이것이다.

**`errorIds`는 `id`로 남긴다.** 검증은 파일 단위이고 로더도 `id`로 보므로, 두 색인이 다른 것이
맞다. 헷갈리지 않도록 함수 주석에 적는다.

`uid`에 접두사를 붙이는 이유는 충돌 때문이다. 접두사가 없으면 새 카드에 `vanguard_slash`라는 id를
주는 순간 저장소 카드의 uid와 겹친다.

- [x] **Step 1: 실패하는 테스트를 쓴다**

```js

test("저장소에서 읽은 카드에 파일 uid를 붙인다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.equal(card.uid, "file:vanguard_slash");
  assert.equal(core.isNewUid(card.uid), false);
});

test("신규 uid는 접두사로 구분된다", () => {
  const core = loadCore();
  assert.equal(core.newUid(1), "new:1");
  assert.equal(core.isNewUid(core.newUid(1)), true);
  assert.notEqual(core.newUid(1), core.repoUid("1"),
    "접두사가 없으면 새 카드의 id가 저장소 카드의 uid와 겹칠 수 있다");
});

test("id를 바꿔도 uid는 그대로다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const renamed = { ...card, id: "renamed_card" };
  assert.equal(renamed.uid, "file:vanguard_slash");
});

test("목록의 상태는 uid로, 오류는 id로 색인한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const renamed = { ...card, id: "renamed_card" };

  const [row] = core.cardListView({
    cards: [renamed],
    states: new Map([[renamed.uid, "modified"]]),
    errorIds: new Set(["renamed_card"]),
    filter: "all",
  });

  assert.equal(row.state, "modified", "id를 바꿔도 상태가 따라온다");
  assert.equal(row.hasError, true, "검증 오류는 현재 id로 붙는다");
});

test("uid를 모르면 저장소와 같은 것으로 본다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const [row] = core.cardListView({ cards: [card], filter: "all" });
  assert.equal(row.state, "same");
});

test("왕복은 uid에 영향받지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original,
    "uid는 노트북 내부 식별자이고 파일에 나가지 않는다");
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 5개.** `card.uid`가 `""`이고 `core.newUid is not a function`.
`왕복은 uid에 영향받지 않는다`는 이미 통과한다 — `uid`는 `cardFields`에 없어 직렬화되지 않는다.

- [x] **Step 3: uid 헬퍼를 넣고 `readCardJson`을 고친다**

`readStatusJson` **바로 위**에 넣는다.

```js
    /// 노트북 내부 식별자. id는 저작 중에 바뀌지만 uid는 바뀌지 않으므로, 선택과 미반영은
    /// uid에 매달린다(설계 5). 접두사를 붙이는 이유는 충돌이다 - 없으면 새 카드에
    /// 'vanguard_slash'라는 id를 주는 순간 저장소 카드의 uid와 겹친다.
    const REPO_UID_PREFIX = "file:";
    const NEW_UID_PREFIX = "new:";

    function repoUid(id) {
      return REPO_UID_PREFIX + id;
    }

    function newUid(sequence) {
      return NEW_UID_PREFIX + sequence;
    }

    function isNewUid(uid) {
      return String(uid).startsWith(NEW_UID_PREFIX);
    }
```

`readCardJson`의 카드 리터럴에서 `uid: "",`를 바꾼다:

```js
      const card = {
        uid: repoUid(String(raw.id)),
```

`cardListView`의 상태 조회를 uid로 바꾸고 주석을 고친다:

```js
    /// 목록 한 줄이 알아야 하는 것을 카드마다 계산하고 걸러 준다. 화면은 이 배열만 반복하므로
    /// 검색·필터 규칙이 DOM을 모른다.
    /// states는 uid로, errorIds는 id로 색인한다 - 편집 상태는 카드를 따라가야 하고(id는
    /// 저작 중에 바뀐다) 검증은 파일 단위여서 로더와 같은 id로 봐야 한다.
    function cardListView({ cards, states, errorIds, membership, query, filter, poolId }) {
      const stateOf = states ?? new Map();
      const failing = errorIds ?? new Set();
      const owners = membership ?? new Map();
      const rows = [];

      for (const card of cards) {
        const row = {
          card,
          state: stateOf.get(card.uid) ?? "same",
          hasError: failing.has(card.id),
          pools: owners.get(card.id) ?? [],
        };
```

export 블록에 세 줄 더한다:

```js
      applyPending,
      repoUid,
      newUid,
      isNewUid,
      CONTENT_PATHS,
```

- [x] **Step 4: 저장소 UI의 색인을 uid로 바꾼다**

`<script data-repo-ui>`의 `loadRepo`에서 카드 상태 표를 uid로 만든다:

```js
      const cardStates = new Map();
      for (const card of cards) {
        cardStates.set(card.uid,
          core.resolveCardState({ stored: card.base, pending: card, schema }));
      }
```

`renderCardList`가 `cardsById` 대신 `uid`로 선택을 걸도록 `cardEntry`의 선택 비교와 클릭을 바꾼다:

```js
      if (selection.scope === "card" && selection.id === row.card.uid) {
        button.classList.add("is-selected");
      }
```

```js
      button.addEventListener("click", () => selectEntry("card", row.card.uid));
```

`loadRepo`의 반환에 uid 색인을 더한다. `cardsById`는 풀이 카드 id로 참조하므로 **그대로 둔다**:

```js
      const cardsById = new Map();
      const cardsByUid = new Map();
      for (const card of cards) {
        cardsById.set(card.id, card);
        cardsByUid.set(card.uid, card);
      }
```

```js
      return {
        schema,
        statuses,
        cards,
        pools,
        cardsById,
        cardsByUid,
```

`repoStore` 초기값에 한 줄 더한다:

```js
      cardsById: new Map(),
      cardsByUid: new Map(),
```

`render()`의 카드 상세 조회를 uid로 바꾼다:

```js
      if (selection.scope === "card") {
        const card = repoStore.cardsByUid.get(selection.id);
        if (card) renderCardDetail(card);
      }
```

**요약의 문제 항목은 `id`로 온다.** `selectEntry("card", id)`가 uid를 기대하게 됐으므로
`problemButton`의 클릭에서 uid로 바꾼다:

```js
      button.addEventListener("click", () => {
        if (scope !== "card") {
          selectEntry(scope, id);
          return;
        }
        // 검증 오류와 격리 항목은 카드 id로 온다. 선택은 uid를 쓰므로 여기서 옮긴다.
        const card = repoStore.cardsById.get(id)
          ?? repoStore.cardsById.get(String(id).replace(/\.json$/, ""));
        if (card) selectEntry("card", card.uid);
      });
```

- [x] **Step 5: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **139/139** (134 + 신규 5. `왕복은 uid에 영향받지 않는다`는 Step 2에서 이미 통과했다).

- [x] **Step 6: 브라우저에서 회귀를 확인한다**

계획 B가 만든 화면이 그대로여야 한다. 폴더를 연결하고 확인한다.

1. 카드 목록 `26/26`, 요약 두 줄이 그대로다.
2. 카드를 누르면 상세와 원문이 나온다. **선택 표시가 옳은 줄에 붙는다.**
3. 요약의 고아 경고를 누르면 그 카드가 선택된다.

- [x] **Step 7: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 카드를 uid로 식별한다

id는 저작 중에 바뀌지만 uid는 바뀌지 않으므로 선택과 미반영이 uid에 매달린다.
접두사를 붙이는 이유는 충돌이다 - 없으면 새 카드에 vanguard_slash라는 id를 주는
순간 저장소 카드의 uid와 겹친다.

검증 오류는 id로 남긴다. 검증은 파일 단위이고 로더도 id로 보므로 두 색인이 다른
것이 맞다."
```

---

### Task 3: 카드 기본 필드 편집과 새 카드

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Task 2의 `newUid`
- Produces: `EDITABLE_CARD_FIELDS`, `setCardField(card, field, value)` → 새 카드,
  `createCardModel({ schema, uid })` → 빈 카드, `parseTagsInput(text)` → 태그 배열.
  Task 6의 폼이 쓴다.

**이름이 `editCardField`·`createCard`가 아닌 이유:** 그 둘은 **Markdown 경로가 이미 쓰고 있다**
(`index.html`의 코어 블록 앞부분). 같은 스코프에 다시 선언하면 함수 선언이 호이스팅으로 옛 것을
가려 Markdown 저작이 그 자리에서 깨진다 — 2026-08-07 실행 중 실측으로 확인했고, Markdown 관련
테스트 여섯이 한꺼번에 실패했다. 계획 D가 옛 경로를 지우면 이름을 되돌릴 수 있지만, 그때도
되돌릴 이유는 없다.

값 변환을 명령이 맡는 이유는 폼이 언제나 문자열을 주기 때문이다. 화면마다 `Number()`를 부르면
한 곳이 빠졌을 때 조용히 문자열이 모델에 들어가고, 그러면 `writeCardJson`이 `"1"`을 써서 왕복이
깨진다.

- [x] **Step 1: 실패하는 테스트를 쓴다**

```js

test("편집 가능한 기본 필드 목록을 노출한다", () => {
  const core = loadCore();
  assert.deepEqual([...core.EDITABLE_CARD_FIELDS], [
    "id", "name", "side", "category", "energyCost", "baseExecutionOrder", "grade", "tags",
  ]);
});

test("숫자 필드를 문자열로 받아도 숫자로 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const edited = core.setCardField(card, "energyCost", "3");
  assert.equal(edited.energyCost, 3);
  assert.equal(typeof edited.energyCost, "number");

  assert.equal(core.setCardField(card, "energyCost", "").energyCost, 0,
    "빈 값은 0이다 - 기본값이라 파일에서 생략된다");
  assert.equal(core.setCardField(card, "baseExecutionOrder", "-2").baseExecutionOrder, -2);
  assert.equal(core.setCardField(card, "energyCost", "abc").energyCost, 0,
    "숫자가 아니면 0으로 떨어뜨린다 - NaN이 모델에 들어가면 왕복이 깨진다");
});

test("태그를 쉼표와 줄바꿈으로 나눈다", () => {
  const core = loadCore();
  assert.deepEqual(core.parseTagsInput("시작, 공격\n독"), ["시작", "공격", "독"]);
  assert.deepEqual(core.parseTagsInput("  시작 ,, 공격  "), ["시작", "공격"]);
  assert.deepEqual(core.parseTagsInput(""), []);
  assert.deepEqual(core.parseTagsInput("시작, 시작"), ["시작", "시작"],
    "중복을 여기서 지우지 않는다 - 검증기가 오류로 잡아 사용자가 고친다");
});

test("문자열 필드는 다듬어 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.equal(core.setCardField(card, "name", "  새 이름  ").name, "새 이름");
  assert.equal(core.setCardField(card, "id", " new_id ").id, "new_id");
});

test("모르는 필드는 무시하고 원본을 그대로 준다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.equal(core.setCardField(card, "flavour", "설명"), card);
});

test("편집은 원본을 제자리에서 고치지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const edited = core.setCardField(card, "name", "바뀐 이름");
  assert.equal(card.name, "선봉 베기");
  assert.equal(edited.uid, card.uid, "uid는 편집으로 바뀌지 않는다");
});

test("분류를 바꿔도 반대쪽 값이 사라지지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const asIntervention = core.setCardField(card, "category", "Intervention");
  assert.equal(asIntervention.category, "Intervention");
  assert.equal(asIntervention.effects.length, 1,
    "효과가 모델에 남는다 - 되돌릴 때 값이 살아 있어야 한다(설계 5)");

  const written = JSON.parse(core.writeCardJson(asIntervention, schema));
  assert.equal(written.effects, undefined, "그래도 파일에는 나가지 않는다");
});

test("새 카드는 스키마의 기본값으로 시작한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const card = core.createCardModel({ schema, uid: core.newUid(1) });

  assert.equal(card.uid, "new:1");
  assert.equal(card.id, "");
  assert.equal(card.side, "Player");
  assert.equal(card.category, "Execution");
  assert.equal(card.grade, "None");
  assert.deepEqual(card.effects, []);
  assert.equal(card.intervention, null);
  assert.deepEqual(card.tags, []);
  assert.equal(card.base, null, "저장소에 없으므로 base가 없다");
  assert.equal(core.resolveCardState({ stored: null, pending: card, schema }), "new");
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 8개.** `core.setCardField is not a function`.

- [x] **Step 3: 최소 구현을 쓴다**

`readCardJson` **바로 아래**에 넣는다.

```js
    const EDITABLE_CARD_FIELDS = Object.freeze([
      "id", "name", "side", "category", "energyCost", "baseExecutionOrder", "grade", "tags",
    ]);

    const NUMBER_CARD_FIELDS = Object.freeze(["energyCost", "baseExecutionOrder"]);

    /// 폼은 언제나 문자열을 준다. 화면마다 Number()를 부르면 한 곳이 빠졌을 때 조용히 문자열이
    /// 모델에 들어가고, 그러면 writeCardJson이 "1"을 써서 왕복이 깨진다.
    function toCardNumber(value) {
      const parsed = Number(String(value).trim());
      return Number.isFinite(parsed) ? parsed : 0;
    }

    /// 중복 태그를 여기서 지우지 않는다 - 검증기가 오류로 잡아 사용자가 고치는 편이,
    /// 입력이 조용히 달라지는 것보다 낫다.
    function parseTagsInput(text) {
      return String(text ?? "")
        .split(/[,\n]/)
        .map((tag) => tag.trim())
        .filter((tag) => tag.length > 0);
    }

    /// 필드 하나를 바꾼 새 카드. 제자리에서 고치지 않는 이유는 resolveCardState가 base와
    /// "지금 쓰면 나올 문자열"을 비교하기 때문이다 - 모델을 뒤집으면 비교 대상이 함께 바뀐다.
    function setCardField(card, field, value) {
      if (!EDITABLE_CARD_FIELDS.includes(field)) return card;

      if (field === "tags") return { ...card, tags: parseTagsInput(value) };
      if (NUMBER_CARD_FIELDS.includes(field)) return { ...card, [field]: toCardNumber(value) };
      return { ...card, [field]: String(value).trim() };
    }

    /// 빈 카드. 분류에 없는 쪽 필드도 채워 두는 이유는 설계 5 그대로다 - 편집 중 분류를
    /// 바꿔도 값이 사라지지 않아야 하고, 어차피 라이터가 분류별 키 목록만 내보낸다.
    function createCardModel({ schema, uid }) {
      return {
        uid,
        id: "",
        name: "",
        side: schema.sides[0],
        category: schema.categories[0],
        energyCost: 0,
        baseExecutionOrder: 0,
        effects: [],
        intervention: null,
        grade: schema.grades[0],
        tags: [],
        unknownKeys: [],
        extra: {},
        base: null,
      };
    }
```

export 블록에 네 줄 더한다:

```js
      readCardJson,
      EDITABLE_CARD_FIELDS,
      setCardField,
      parseTagsInput,
      createCardModel,
      writeCardJson,
```

- [x] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **147/147** (139 + 신규 8).

- [x] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 카드의 기본 필드를 편집한다

값 변환을 명령이 맡는다. 폼은 언제나 문자열을 주므로 화면마다 Number()를 부르면
한 곳이 빠졌을 때 조용히 문자열이 모델에 들어가고 왕복이 깨진다.

분류를 바꿔도 반대쪽 값이 모델에 남는다. 어차피 라이터가 분류별 키 목록만
내보내므로 파일에는 나가지 않는다."
```

---

### Task 4: 효과·개입 행 편집 명령

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: 스키마의 `effects`·`interventions`·`condition`
- Produces:
  `emptyEffectRow(kind, spec)`, `addEffect(card, kind, schema)`, `removeEffect(card, index)`,
  `duplicateEffect(card, index)`, `moveEffect(card, from, to)`,
  `setEffectKind(card, index, kind, schema)`, `setEffectParam(card, index, name, value, schema)`,
  `setEffectCondition(card, index, patch)`,
  `setIntervention(card, kind, schema)`, `setInterventionParam(card, name, value, schema)`.
  Task 7의 편집기가 쓴다.

**효과와 개입이 스키마에서 같은 모양이므로 파라미터 변환 함수 하나가 둘을 다룬다**(계획 3.5의
결과). 다른 것은 개입에 조건이 없다는 점과 행이 하나뿐이라는 점이다.

**`raw` 행은 편집하지 않는다.** 노트북이 모르는 `kind`는 원본을 통째로 들고 있으므로(설계 §10.2),
파라미터나 종류를 바꾸면 보존하기로 한 것이 깨진다. 삭제와 이동은 허용한다 — 그것은 사용자가
명시적으로 지시한 변경이다.

- [x] **Step 1: 실패하는 테스트를 쓴다**

```js

function probeCard(core, schema, overrides) {
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution", ...overrides,
  }, null, 2), schema);
  return card;
}

test("새 효과 행은 스키마의 기본값으로 시작한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const added = core.addEffect(probeCard(core, schema, {}), "apply_status", schema);

  assert.equal(added.effects.length, 1);
  assert.deepEqual(added.effects[0].params,
    { status: "", count: 0, target: "Self", selector: "None" });
  assert.equal(added.effects[0].condition, null);
  assert.equal(added.effects[0].raw, null);
});

test("기본값뿐인 효과는 kind만 파일로 나간다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const added = core.addEffect(probeCard(core, schema, {}), "apply_status", schema);
  const written = JSON.parse(core.writeCardJson(added, schema));

  assert.deepEqual(written.effects, [{ kind: "apply_status" }],
    "기본값은 생략된다 - 폼이 값을 다 채워도 왕복 규칙은 그대로다");
});

test("효과를 삭제·복제·이동한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, {
    effects: [{ kind: "damage", value: 5 }, { kind: "grant_next_turn_fate", value: 1 }],
  });

  assert.deepEqual(core.removeEffect(base, 0).effects.map((e) => e.kind),
    ["grant_next_turn_fate"]);

  const duplicated = core.duplicateEffect(base, 0);
  assert.deepEqual(duplicated.effects.map((e) => e.kind),
    ["damage", "damage", "grant_next_turn_fate"]);
  assert.notEqual(duplicated.effects[0], duplicated.effects[1],
    "복제본이 같은 객체를 가리키면 한쪽을 고칠 때 둘 다 바뀐다");

  assert.deepEqual(core.moveEffect(base, 0, 1).effects.map((e) => e.kind),
    ["grant_next_turn_fate", "damage"]);
  assert.deepEqual(core.moveEffect(base, 0, 9).effects.map((e) => e.kind),
    ["grant_next_turn_fate", "damage"], "범위를 넘으면 끝으로 보낸다");
  assert.equal(base.effects.length, 2, "원본을 제자리에서 고치지 않는다");
});

test("효과 종류를 바꾸면 파라미터가 통째로 갈린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, {
    effects: [{ kind: "damage", value: 5, selector: "FrontOne" }],
  });

  const changed = core.setEffectKind(base, 0, "grant_next_turn_fate", schema);
  assert.equal(changed.effects[0].kind, "grant_next_turn_fate");
  assert.deepEqual(changed.effects[0].params, { value: 0 },
    "새 종류의 필드만 남는다 - selector는 이 효과에 없다");
});

test("효과 종류를 바꿔도 조건은 남는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, {
    effects: [{ kind: "damage", value: 5, condition: { kind: "FirstToTrigger" } }],
  });

  const changed = core.setEffectKind(base, 0, "move_formation", schema);
  assert.equal(changed.effects[0].condition.kind, "FirstToTrigger",
    "조건은 효과 종류와 독립이다");
});

test("파라미터를 타입에 맞게 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = core.addEffect(probeCard(core, schema, {}), "consume_status", schema);

  assert.equal(core.setEffectParam(base, 0, "maxAmount", "3", schema)
    .effects[0].params.maxAmount, 3);
  assert.equal(core.setEffectParam(base, 0, "status", "poison", schema)
    .effects[0].params.status, "poison");
  assert.equal(core.setEffectParam(base, 0, "selector", "All", schema)
    .effects[0].params.selector, "All");

  const swap = core.addEffect(probeCard(core, schema, {}), "damage", schema);
  assert.equal(core.setEffectParam(swap, 0, "value", "abc", schema).effects[0].params.value, 0);
});

test("불리언 파라미터를 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = core.setIntervention(
    probeCard(core, schema, { category: "Intervention" }), "swap_execution_order", schema);

  assert.equal(base.intervention.params.requireAdjacent, false);
  assert.equal(core.setInterventionParam(base, "requireAdjacent", true, schema)
    .intervention.params.requireAdjacent, true);
});

test("조건을 붙이고 뗀다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = core.addEffect(probeCard(core, schema, {}), "damage", schema);

  const withCondition = core.setEffectCondition(base, 0, { kind: "WithinNth", n: "3" });
  assert.deepEqual(withCondition.effects[0].condition,
    { kind: "WithinNth", n: 3, successEffectValue: 0, skipOnBasic: false });

  const bumped = core.setEffectCondition(withCondition, 0, { successEffectValue: "7" });
  assert.equal(bumped.effects[0].condition.kind, "WithinNth", "다른 칸은 유지된다");
  assert.equal(bumped.effects[0].condition.successEffectValue, 7);

  assert.equal(core.setEffectCondition(withCondition, 0, null).effects[0].condition, null);
});

test("개입을 걸고 종류를 바꾸고 뗀다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, { category: "Intervention" });

  const locked = core.setIntervention(base, "lock", schema);
  assert.equal(locked.intervention.kind, "lock");
  assert.deepEqual(locked.intervention.params, {});

  const changed = core.setIntervention(locked, "change_execution_order", schema);
  assert.deepEqual(changed.intervention.params, { delta: 0, targetSide: "Any" });

  assert.equal(core.setIntervention(changed, "", schema).intervention, null);
});

test("모르는 효과 행은 파라미터도 종류도 바뀌지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution",
    effects: [{ kind: "teleport", distance: 3 }],
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);

  assert.equal(core.setEffectParam(card, 0, "distance", "9", schema), card);
  assert.equal(core.setEffectKind(card, 0, "damage", schema), card);
  assert.equal(core.writeCardJson(card, schema), original,
    "보존하기로 한 것이 편집 명령으로 깨지면 안 된다");

  assert.deepEqual(core.removeEffect(card, 0).effects, [],
    "삭제는 허용한다 - 사용자가 명시적으로 지시한 변경이다");
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 11개.** `core.addEffect is not a function`.

- [x] **Step 3: 최소 구현을 쓴다**

`createCardModel` **바로 아래**에 넣는다.

```js
    /// 스키마 필드 하나의 빈 값. writeCardJson의 isDefaultValue와 짝이 맞아야 한다 -
    /// 여기서 낸 값이 곧 "파일에서 생략되는 값"이다.
    function defaultParamValue(field) {
      if (field.type === "int") return 0;
      if (field.type === "bool") return false;
      if (field.type === "status") return "";
      return field.options[0];
    }

    function coerceParamValue(field, value) {
      if (field.type === "int") return toCardNumber(value);
      if (field.type === "bool") return value === true || value === "true";
      return String(value);
    }

    /// 효과와 개입이 스키마에서 같은 모양이라 한 함수가 둘을 만든다(계획 3.5의 결과).
    function emptyParams(spec) {
      const params = {};
      for (const field of spec.fields) params[field.name] = defaultParamValue(field);
      return params;
    }

    function replaceEffects(card, effects) {
      return { ...card, effects };
    }

    function addEffect(card, kind, schema) {
      const spec = schema.effects[kind];
      if (!spec) return card;

      return replaceEffects(card, [
        ...(card.effects ?? []),
        { kind, params: emptyParams(spec), condition: null, raw: null },
      ]);
    }

    function removeEffect(card, index) {
      const effects = [...(card.effects ?? [])];
      if (index < 0 || index >= effects.length) return card;

      effects.splice(index, 1);
      return replaceEffects(card, effects);
    }

    /// 파라미터를 얕은 복사한다. 같은 객체를 가리키면 한쪽을 고칠 때 둘 다 바뀐다.
    function duplicateEffect(card, index) {
      const effects = [...(card.effects ?? [])];
      const source = effects[index];
      if (!source) return card;

      effects.splice(index + 1, 0, {
        ...source,
        params: { ...source.params },
        condition: source.condition ? { ...source.condition } : null,
      });
      return replaceEffects(card, effects);
    }

    function moveEffect(card, from, to) {
      const effects = [...(card.effects ?? [])];
      if (from < 0 || from >= effects.length) return card;

      const [moved] = effects.splice(from, 1);
      effects.splice(Math.max(0, Math.min(to, effects.length)), 0, moved);
      return replaceEffects(card, effects);
    }

    /// 종류가 바뀌면 파라미터가 통째로 갈린다 - 계획 3.5가 파라미터를 액션에 소속시켰으므로
    /// 쓰지 않는 칸이 남을 자리가 없다. 조건은 효과 종류와 독립이라 유지한다.
    function setEffectKind(card, index, kind, schema) {
      const effects = [...(card.effects ?? [])];
      const current = effects[index];
      const spec = schema.effects[kind];
      if (!current || current.raw || !spec) return card;

      effects[index] = { kind, params: emptyParams(spec), condition: current.condition, raw: null };
      return replaceEffects(card, effects);
    }

    function setEffectParam(card, index, name, value, schema) {
      const effects = [...(card.effects ?? [])];
      const current = effects[index];
      if (!current || current.raw) return card;

      const field = schema.effects[current.kind]?.fields.find((entry) => entry.name === name);
      if (!field) return card;

      effects[index] = {
        ...current,
        params: { ...current.params, [name]: coerceParamValue(field, value) },
      };
      return replaceEffects(card, effects);
    }

    /// patch가 null이면 조건을 뗀다. 아니면 있는 조건 위에 얹는다 - 폼이 칸 하나씩 보내므로
    /// 나머지 칸이 유지되어야 한다.
    function setEffectCondition(card, index, patch) {
      const effects = [...(card.effects ?? [])];
      const current = effects[index];
      if (!current || current.raw) return card;

      if (patch === null) {
        effects[index] = { ...current, condition: null };
        return replaceEffects(card, effects);
      }

      const merged = { ...emptyConditionValue(), ...current.condition, ...patch };
      effects[index] = {
        ...current,
        condition: {
          kind: String(merged.kind),
          n: toCardNumber(merged.n),
          successEffectValue: toCardNumber(merged.successEffectValue),
          skipOnBasic: merged.skipOnBasic === true || merged.skipOnBasic === "true",
        },
      };
      return replaceEffects(card, effects);
    }

    /// 개입은 효과에서 조건을 뺀 것이고 행이 하나뿐이다. kind가 비면 뗀다.
    function setIntervention(card, kind, schema) {
      if (!kind) return { ...card, intervention: null };

      const spec = schema.interventions[kind];
      if (!spec) return card;
      return { ...card, intervention: { kind, params: emptyParams(spec), raw: null } };
    }

    function setInterventionParam(card, name, value, schema) {
      const current = card.intervention;
      if (!current || current.raw) return card;

      const field = schema.interventions[current.kind]?.fields
        .find((entry) => entry.name === name);
      if (!field) return card;

      return {
        ...card,
        intervention: {
          ...current,
          params: { ...current.params, [name]: coerceParamValue(field, value) },
        },
      };
    }
```

export 블록에 열 줄 더한다:

```js
      createCardModel,
      addEffect,
      removeEffect,
      duplicateEffect,
      moveEffect,
      setEffectKind,
      setEffectParam,
      setEffectCondition,
      setIntervention,
      setInterventionParam,
      writeCardJson,
```

- [x] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **158/158** (147 + 신규 11).

- [x] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 효과와 개입 행을 편집한다

효과와 개입이 스키마에서 같은 모양이라 파라미터 함수 하나가 둘을 다룬다. 종류가
바뀌면 파라미터가 통째로 갈리고 조건만 남는다 - 조건은 효과 종류와 독립이다.

모르는 kind 행은 파라미터도 종류도 바뀌지 않는다. 원본을 통째로 보존하기로 했으므로
편집 명령이 그것을 깨면 안 된다. 삭제와 이동은 허용한다."
```

---

### Task 5: 풀 편성 편집 명령

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (코어 블록)
- Test: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Produces: `addCardsToPool(pool, cardIds)`, `removeFromPool(pool, index)`,
  `moveInPool(pool, from, to)`. Task 8의 풀 편성 UI가 쓴다.

**빼기는 인덱스로 한다.** 풀에 같은 카드가 두 번 들어 있을 수 있고(설계 §10.4가 지우지 않기로
했다), id로 빼면 어느 쪽을 뺄지 정할 수 없다.

**담기는 이미 있는 카드를 건너뛴다.** 설계 §7의 체크박스가 이미 소속된 풀을 비활성으로 두므로
정상 흐름에서는 일어나지 않지만, 여기서 막아 두면 UI가 실수해도 중복이 새로 생기지 않는다.

- [x] **Step 1: 실패하는 테스트를 쓴다**

```js

test("풀 맨 끝에 카드를 담는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b"]}');

  assert.deepEqual(core.addCardsToPool(pool, ["c", "d"]).cards, ["a", "b", "c", "d"]);
  assert.deepEqual(pool.cards, ["a", "b"], "원본을 제자리에서 고치지 않는다");
});

test("이미 있는 카드는 다시 담지 않는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b"]}');
  assert.deepEqual(core.addCardsToPool(pool, ["b", "c"]).cards, ["a", "b", "c"]);
  assert.deepEqual(core.addCardsToPool(pool, ["c", "c"]).cards, ["a", "b", "c"],
    "같은 요청 안의 중복도 한 번만 담는다");
});

test("이미 있는 중복은 담기로 사라지지 않는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","a"]}');
  assert.deepEqual(core.addCardsToPool(pool, ["b"]).cards, ["a", "a", "b"],
    "저장소에 있던 중복은 그대로 둔다 - 검증기가 오류로 잡고 사용자가 뺀다");
});

test("인덱스로 뺀다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","a","b"]}');

  assert.deepEqual(core.removeFromPool(pool, 0).cards, ["a", "b"],
    "같은 카드가 둘일 때 id로는 어느 쪽을 뺄지 정할 수 없다");
  assert.deepEqual(core.removeFromPool(pool, 9).cards, ["a", "a", "b"]);
});

test("편성 순서를 바꾼다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b","c"]}');

  assert.deepEqual(core.moveInPool(pool, 0, 2).cards, ["b", "c", "a"]);
  assert.deepEqual(core.moveInPool(pool, 2, 0).cards, ["c", "a", "b"]);
  assert.deepEqual(core.moveInPool(pool, 0, 9).cards, ["b", "c", "a"], "범위를 넘으면 끝으로");
  assert.deepEqual(core.moveInPool(pool, 9, 0).cards, ["a", "b", "c"], "없는 자리는 무시한다");
});

test("편성을 고쳐도 왕복 형식이 유지된다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool } = core.readPoolJson(text);

  const edited = core.removeFromPool(core.addCardsToPool(pool, ["새_카드"]), 0);
  const written = core.writePoolJson(edited);

  assert.ok(written.endsWith("]\n}\n"));
  assert.deepEqual(JSON.parse(written).cards, edited.cards);
  assert.equal(core.writePoolJson(pool), text, "원본은 그대로 왕복한다");
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 6개.** `core.addCardsToPool is not a function`.

- [x] **Step 3: 최소 구현을 쓴다**

`writePoolJson` **바로 아래**에 넣는다.

```js
    /// 이미 있는 카드는 건너뛴다. 설계 7의 체크박스가 이미 소속된 풀을 비활성으로 두므로
    /// 정상 흐름에서는 일어나지 않지만, 여기서 막으면 UI가 실수해도 중복이 새로 생기지 않는다.
    /// 저장소에 이미 있던 중복은 건드리지 않는다 - 검증기가 오류로 잡고 사용자가 뺀다.
    function addCardsToPool(pool, cardIds) {
      const present = new Set(pool.cards);
      const added = [];
      for (const cardId of cardIds) {
        if (present.has(cardId)) continue;
        present.add(cardId);
        added.push(cardId);
      }

      return added.length ? { ...pool, cards: [...pool.cards, ...added] } : pool;
    }

    /// 인덱스로 뺀다. 풀에 같은 카드가 두 번 들어 있을 수 있으므로(설계 10.4가 지우지 않기로
    /// 했다) id로는 어느 쪽을 뺄지 정할 수 없다.
    function removeFromPool(pool, index) {
      if (index < 0 || index >= pool.cards.length) return pool;

      const cards = [...pool.cards];
      cards.splice(index, 1);
      return { ...pool, cards };
    }

    function moveInPool(pool, from, to) {
      if (from < 0 || from >= pool.cards.length) return pool;

      const cards = [...pool.cards];
      const [moved] = cards.splice(from, 1);
      cards.splice(Math.max(0, Math.min(to, cards.length)), 0, moved);
      return { ...pool, cards };
    }
```

export 블록에 세 줄 더한다:

```js
      writePoolJson,
      addCardsToPool,
      removeFromPool,
      moveInPool,
```

- [x] **Step 4: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **164/164** (158 + 신규 6).

- [x] **Step 5: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 풀 편성을 조작한다

빼기는 인덱스로 한다. 풀에 같은 카드가 두 번 들어 있을 수 있으므로 id로는 어느
쪽을 뺄지 정할 수 없다.

담기는 이미 있는 카드를 건너뛰되 저장소에 있던 중복은 건드리지 않는다 - 조용히
고치면 노트북을 열었다 닫는 것만으로 편성이 바뀐다."
```

---

### Task 6: 카드 기본 필드 폼과 미반영 배선

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (마크업의 새 카드 버튼, `<script data-repo-ui>`)
- Test: `Tools/card-idea-notebook/index.test.mjs` (마크업 검사)

**Interfaces:**
- Consumes: Task 1의 미반영 보관소, Task 3의 편집 명령
- Produces: 저장소 UI 안의 `applyCardEdit(next)` — 편집된 카드를 미반영에 넣고 다시 그린다.
  Task 7·8이 그대로 부른다. `newCard()` — 새 카드를 만들고 선택한다.

**오류 카드는 폼 대신 원본 JSON을 읽기 전용으로 보여준다**(설계 §11.3). 모르는 최상위 키를 가진
카드가 그렇다 — 폼으로 열면 §10.2가 보존하기로 한 것을 폼이 표현하지 못하는 만큼 망가뜨린다.

- [x] **Step 1: 실패하는 마크업 테스트를 쓴다**

```js

test("새 카드 버튼과 편집 폼 자리가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="repo-new-card"/);
  assert.match(html, /id="repo-pending-note"/);
});

test("편집 명령이 저장소 UI에만 배선된다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const markdownUi = html.split("<script data-repo-ui>")[0].split("</script>").pop();
  assert.equal(markdownUi.includes("applyCardEdit"), false,
    "Markdown UI 스크립트는 이 계획에서 바뀌지 않는다");
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 1개.** `id="repo-new-card"`를 찾지 못한다.
(두 번째 테스트는 이미 통과한다 — 아직 아무것도 배선하지 않았다.)

- [x] **Step 3: 마크업과 CSS를 넣는다**

저장소 목록 헤더의 `<button type="button" class="primary" id="repo-connect">폴더 연결</button>`을
둘로 나눈다:

```html
          <div class="repo-header-actions">
            <button type="button" id="repo-new-card">＋ 새 카드</button>
            <button type="button" class="primary" id="repo-connect">폴더 연결</button>
          </div>
```

`<div class="repo-summary" id="repo-summary"></div>` **바로 뒤**에 넣는다:

```html
        <p class="repo-hint" id="repo-pending-note" hidden></p>
```

`<style>`의 `.pool-distribution dt` 뒤에 넣는다:

```css
    .repo-header-actions { display: flex; gap: 6px; }
    .repo-form { display: grid; gap: 10px; }
    .repo-form label { display: grid; gap: 4px; font-size: 12px; color: var(--muted); }
    .repo-form input,
    .repo-form select { padding: 7px 9px; border: 1px solid var(--line); border-radius: 8px;
      color: var(--ink); background: #191b18; }
    .repo-form .row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    .repo-readonly { color: var(--warning); font-size: 12px; line-height: 1.6; }
```

- [x] **Step 4: 미반영 배선과 폼을 쓴다**

`<script data-repo-ui>`의 `repoStore` 선언 **바로 뒤**에 넣는다:

```js
    let pending = core.emptyPending();
    let newCardSequence = 0;
    let pendingNote = "";

    /// 편집 하나를 미반영에 반영한다. 저장소 원본과 같아지면 미반영에서 뺀다 -
    /// 되돌린 편집이 계속 "수정됨"으로 남으면 diff 요약이 거짓말을 한다.
    function applyCardEdit(next) {
      const unchanged = next.base !== null
        && core.writeCardJson(next, repoStore.schema) === next.base;
      pending = unchanged
        ? core.dropPendingCard(pending, next.uid)
        : core.putPendingCard(pending, next);
      persistPending();
      refreshFromPending();
      selection = { scope: "card", id: next.uid };
      render();
    }

    function applyPoolEdit(next) {
      const unchanged = next.base !== null && core.writePoolJson(next) === next.base;
      pending = unchanged
        ? core.dropPendingPool(pending, next.id)
        : core.putPendingPool(pending, next);
      persistPending();
      refreshFromPending();
      selection = { scope: "pool", id: next.id };
      render();
    }

    function persistPending() {
      const result = core.writePending(window.localStorage, pending);
      pendingNote = result.persisted
        ? ""
        : `편집분을 브라우저에 저장하지 못했습니다: ${result.error}`;
    }

    /// 저장소에서 읽은 것 위에 미반영을 얹어 화면이 보는 목록을 다시 만든다(설계 10.1).
    /// 상태·색인·검증이 전부 이 결과에서 나오므로 편집 한 번마다 통째로 다시 계산한다 -
    /// 카드 26장 규모에서 부분 갱신은 이득보다 어긋날 위험이 크다.
    function refreshFromPending() {
      if (!repoStore.connected) return;

      const merged = core.applyPending({
        cards: repoStore.storedCards,
        pools: repoStore.storedPools,
        pending,
      });

      repoStore.cards = merged.cards;
      repoStore.pools = merged.pools;
      repoStore.cardsById = new Map(merged.cards.map((card) => [card.id, card]));
      repoStore.cardsByUid = new Map(merged.cards.map((card) => [card.uid, card]));
      repoStore.membership = core.poolMembership(merged.pools);
      repoStore.cardStates = new Map(merged.cards.map((card) => [
        card.uid,
        core.resolveCardState({
          stored: repoStore.storedCardText.get(card.uid) ?? null,
          pending: card,
          schema: repoStore.schema,
        }),
      ]));
      repoStore.poolStates = new Map(merged.pools.map((pool) => [
        pool.id,
        core.resolvePoolState({
          stored: repoStore.storedPoolText.get(pool.id) ?? null,
          pending: pool,
        }),
      ]));
      repoStore.validation = core.validateContent({
        cards: merged.cards,
        pools: merged.pools,
        statusKeys: repoStore.statuses.map((status) => status.key),
        schema: repoStore.schema,
      });
    }

    function newCard() {
      newCardSequence += 1;
      const card = core.createCardModel({
        schema: repoStore.schema,
        uid: core.newUid(newCardSequence),
      });
      tab = "cards";
      elements.tabCards.classList.add("is-active");
      elements.tabPools.classList.remove("is-active");
      applyCardEdit(card);
    }
```

`loadRepo`의 반환에 **저장소 원본 두 벌**을 더한다. `refreshFromPending`이 매번 원본에서 다시
쌓으려면 편집되지 않은 사본이 필요하다.

**원본 텍스트를 `uid`로 색인한다.** `id`로 색인하면 사용자가 `id`를 바꾸는 순간 원본을 못 찾아
`resolveCardState`가 그 카드를 `신규`로 판정한다. `uid`는 바뀌지 않으므로 이름을 바꾼 카드가
`수정됨`으로 남는다 — 옛 파일이 저장소에 그대로 남는 문제는 계획 D의 §12
`저장소에만 있음` 목록이 다룬다.

```js
      const storedCardText = new Map();
      for (const card of cards) storedCardText.set(card.uid, card.base);
      const storedPoolText = new Map();
      for (const pool of pools) storedPoolText.set(pool.id, pool.base);

      return {
        schema,
        statuses,
        cards,
        pools,
        storedCards: cards,
        storedPools: pools,
        storedCardText,
        storedPoolText,
        cardsById,
        cardsByUid,
```

`repoStore` 초기값에 네 줄 더한다:

```js
      storedCards: [],
      storedPools: [],
      storedCardText: new Map(),
      storedPoolText: new Map(),
```

`applyLoaded`가 미반영을 다시 얹도록 고친다:

```js
    function applyLoaded(root, loaded) {
      Object.assign(repoStore, loaded, { connected: true, root, message: "" });
      refreshFromPending();
      render();
    }
```

`renderCardDetail`을 폼으로 바꾼다. 기존 함수를 통째로 교체한다:

```js
    function formField(label, control) {
      const wrap = document.createElement("label");
      const text = document.createElement("span");
      text.textContent = label;
      wrap.append(text, control);
      return wrap;
    }

    function textControl(value, onChange) {
      const input = document.createElement("input");
      input.type = "text";
      input.value = value;
      input.addEventListener("change", () => onChange(input.value));
      return input;
    }

    function numberControl(value, onChange) {
      const input = document.createElement("input");
      input.type = "number";
      input.step = "1";
      input.value = String(value);
      input.addEventListener("change", () => onChange(input.value));
      return input;
    }

    function selectControl(options, value, onChange) {
      const select = document.createElement("select");
      for (const option of options) {
        const node = document.createElement("option");
        node.value = option;
        node.textContent = option;
        select.append(node);
      }
      select.value = value;
      select.addEventListener("change", () => onChange(select.value));
      return select;
    }

    /// 모르는 최상위 키를 가진 카드는 폼 대신 원문을 읽기 전용으로 보여준다(설계 11.3).
    /// 폼으로 열면 10.2가 보존하기로 한 것을 폼이 표현하지 못하는 만큼 망가뜨린다.
    function renderCardDetail(card) {
      elements.detailTitle.textContent = `${card.name || "(이름 없음)"} · ${card.id || "(id 없음)"}`;
      elements.sourceTitle.textContent = card.id ? `Cards/${card.id}.json` : "Cards/(id 없음).json";
      elements.source.textContent = card.base ?? core.writeCardJson(card, repoStore.schema);

      if (card.unknownKeys.length) {
        const note = document.createElement("p");
        note.className = "repo-readonly";
        note.textContent = `모르는 키 ${card.unknownKeys.join(" · ")}가 있어 편집할 수 없습니다.`
          + " 부팅이 이 카드를 거부하므로 그 키를 손으로 지운 뒤 다시 읽어 주세요.";
        elements.detail.append(note);
        return;
      }

      const form = document.createElement("div");
      form.className = "repo-form";
      const edit = (field) => (value) => applyCardEdit(core.setCardField(card, field, value));

      const identity = document.createElement("div");
      identity.className = "row";
      identity.append(
        formField("id", textControl(card.id, edit("id"))),
        formField("이름", textControl(card.name, edit("name"))),
      );
      form.append(identity);

      const classify = document.createElement("div");
      classify.className = "row";
      classify.append(
        formField("진영", selectControl(repoStore.schema.sides, card.side, edit("side"))),
        formField("분류",
          selectControl(repoStore.schema.categories, card.category, edit("category"))),
      );
      form.append(classify);

      const numbers = document.createElement("div");
      numbers.className = "row";
      numbers.append(formField("비용", numberControl(card.energyCost, edit("energyCost"))));
      if (card.category === "Execution") {
        numbers.append(formField("실행 순서",
          numberControl(card.baseExecutionOrder, edit("baseExecutionOrder"))));
      }
      form.append(numbers);

      const meta = document.createElement("div");
      meta.className = "row";
      meta.append(
        formField("등급", selectControl(repoStore.schema.grades, card.grade, edit("grade"))),
        formField("태그", textControl((card.tags ?? []).join(", "), edit("tags"))),
      );
      form.append(meta);

      elements.detail.append(form);
    }
```

`renderSummary` 끝에 미반영 저장 실패 안내를 붙인다. 함수 마지막 `}` 직전에 넣는다:

```js
      elements.pendingNote.hidden = !pendingNote;
      elements.pendingNote.textContent = pendingNote;
```

`elements`에 한 줄, 초기화에 두 줄 더한다:

```js
      summary: byId("repo-summary"),
      pendingNote: byId("repo-pending-note"),
      newCard: byId("repo-new-card"),
```

```js
    elements.newCard.addEventListener("click", newCard);
```

그리고 스크립트 맨 끝의 `render();` **바로 위**에 미반영 복원을 넣는다:

```js
    const restored = core.readPending(window.localStorage);
    pending = restored.pending;
    if (restored.errors.length) pendingNote = restored.errors.join(" ");
```

- [x] **Step 5: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **166/166** (164 + 신규 2).

- [x] **Step 6: 브라우저에서 수동 검수한다**

폴더를 연결하고 확인한다.

1. 카드를 고르면 중앙이 **폼**이다. id·이름·진영·분류·비용·실행 순서·등급·태그가 보인다.
2. 이름을 고치면 목록 배지가 `✎ 수정됨`으로 바뀌고 요약의 `미반영`이 1이 된다.
   **우측 원문은 아직 저장소 것 그대로다**(diff 토글은 계획 D).
3. 이름을 되돌리면 배지가 `● 저장소와 동일`로 돌아가고 미반영이 0이 된다.
4. `분류`를 `Intervention`으로 바꾸면 실행 순서 칸이 사라지고, 검증 오류에
   `개입 카드에는 개입 액션이 필요합니다.`가 뜬다.
5. `＋ 새 카드`를 누르면 목록 맨 끝에 `＋ 신규` 배지의 빈 카드가 생기고 선택된다.
   오류에 `id가 없습니다.`가 뜬다.
6. **탭을 새로 고쳐도 편집분이 남아 있다.**
7. 카드에 손으로 모르는 키를 넣고 다시 읽으면 폼 대신 안내가 나온다.

   ```bash
   node -e "const f='Assets/StreamingAssets/Content/Cards/vanguard_slash.json';const fs=require('fs');const c=JSON.parse(fs.readFileSync(f,'utf8'));c.flavour='설명';fs.writeFileSync(f,JSON.stringify(c,null,2)+'\n')"
   ```

   확인 후 되돌리고 `git status`가 깨끗한지 본다.

   ```bash
   git checkout Assets/StreamingAssets/Content/Cards/vanguard_slash.json && git status --short
   ```

- [x] **Step 7: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 카드 기본 필드를 편집한다

편집 하나마다 저장소 원본 위에 미반영을 통째로 다시 얹는다. 상태·색인·검증이 전부
그 결과에서 나오므로, 카드 26장 규모에서 부분 갱신은 이득보다 어긋날 위험이 크다.

저장소 원본과 같아지면 미반영에서 뺀다 - 되돌린 편집이 계속 수정됨으로 남으면
diff 요약이 거짓말을 한다.

모르는 키를 가진 카드는 폼 대신 안내를 보여준다. 폼으로 열면 보존하기로 한 것을
폼이 표현하지 못하는 만큼 망가뜨린다."
```

---

### Task 7: 효과·개입 편집기

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (`<script data-repo-ui>`, CSS)
- Test: `Tools/card-idea-notebook/index.test.mjs` (마크업 검사)

**Interfaces:**
- Consumes: Task 4의 행 편집 명령, Task 6의 `applyCardEdit`
- Produces: `renderEffectRows(card)` — 실행 카드의 효과 섹션. `renderInterventionRow(card)` —
  개입 카드의 개입 섹션. `renderCardDetail`이 분류에 따라 하나만 부른다.

**개입 폼이 효과 행 렌더러를 그대로 재사용한다.** 계획 3.5가 개입을 `EffectSpec`처럼 다형화했고
계획 A의 스키마가 둘을 같은 `{kind, label, fields[]}`로 내므로, 파라미터 칸을 그리는 함수 하나가
양쪽을 그린다. 다른 것은 개입에 조건이 없고 행이 하나라는 점뿐이다.

- [x] **Step 1: 실패하는 마크업 테스트를 쓴다**

```js

test("효과 편집기의 스타일이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /\.effect-row\b/);
  assert.match(html, /\.effect-params\b/);
  assert.match(html, /\.effect-condition\b/);
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 1개.** `.effect-row`를 찾지 못한다.

- [x] **Step 3: CSS를 넣는다**

`.repo-readonly` 뒤에 넣는다.

```css
    .effect-list { display: grid; gap: 8px; }
    .effect-row {
      display: grid;
      gap: 8px;
      padding: 9px 11px;
      border: 1px solid var(--line);
      border-radius: 10px;
      background: #191b18;
    }
    .effect-row.is-locked { border-color: var(--warning); }
    .effect-head { display: flex; gap: 8px; align-items: center; }
    .effect-head select { flex: 1; }
    .effect-params { display: flex; flex-wrap: wrap; gap: 8px; }
    .effect-params label { display: grid; gap: 3px; font-size: 11px; color: var(--muted); }
    .effect-params input[type="number"] { width: 84px; }
    .effect-condition { display: flex; flex-wrap: wrap; gap: 8px; align-items: end; }
    .effect-actions { display: flex; gap: 4px; }
    .effect-actions button { padding: 4px 8px; font-size: 11px; }
    .effect-glyph { color: var(--gold); font-size: 12px; letter-spacing: .12em; }
```

- [x] **Step 4: 효과·개입 렌더러를 쓴다**

`renderCardDetail` **바로 위**에 넣는다.

```js
    /// 위치 범위의 글리프 표기. 기존 도구에서 검증된 표기이고 방향 감각을 숫자보다 잘
    /// 전달하므로 selector 드롭다운 옆에 남긴다(설계 6).
    const SELECTOR_GLYPHS = Object.freeze({
      None: "",
      FrontOne: "◆━━━━",
      FrontTwo: "◆◆━━━",
      BackOne: "━━━━◆",
      BackTwo: "━━━◆◆",
      All: "━━━━━",
    });

    function statusOptions() {
      return repoStore.statuses.map((status) => status.key);
    }

    function statusLabel(key) {
      return repoStore.statuses.find((status) => status.key === key)?.displayName ?? key;
    }

    function checkboxControl(value, onChange) {
      const input = document.createElement("input");
      input.type = "checkbox";
      input.checked = value === true;
      input.addEventListener("change", () => onChange(input.checked));
      return input;
    }

    /// 스키마의 필드 타입 넷이 컨트롤 넷으로 간다(설계 6의 표). 효과와 개입이 같은 모양이라
    /// 이 함수 하나가 양쪽을 그린다.
    function paramControl(field, value, onChange) {
      if (field.type === "int") return numberControl(value ?? 0, onChange);
      if (field.type === "bool") return checkboxControl(value, onChange);
      if (field.type === "status") {
        const select = document.createElement("select");
        const none = document.createElement("option");
        none.value = "";
        none.textContent = "(없음)";
        select.append(none);
        for (const key of statusOptions()) {
          const node = document.createElement("option");
          node.value = key;
          node.textContent = `${statusLabel(key)} (${key})`;
          select.append(node);
        }
        select.value = value ?? "";
        select.addEventListener("change", () => onChange(select.value));
        return select;
      }

      return selectControl(field.options, value ?? field.options[0], onChange);
    }

    function paramsBlock(spec, params, onParam) {
      const block = document.createElement("div");
      block.className = "effect-params";
      for (const field of spec.fields) {
        const control = paramControl(field, params[field.name], (next) => onParam(field.name, next));
        block.append(formField(field.name, control));

        if (field.name === "selector") {
          const glyph = document.createElement("output");
          glyph.className = "effect-glyph";
          glyph.textContent = SELECTOR_GLYPHS[params.selector] ?? "";
          block.append(glyph);
        }
      }

      return block;
    }

    function conditionBlock(card, index, effect) {
      const wrap = document.createElement("details");
      wrap.open = Boolean(effect.condition);

      const summary = document.createElement("summary");
      summary.textContent = effect.condition ? `조건: ${effect.condition.kind}` : "조건";
      wrap.append(summary);

      const body = document.createElement("div");
      body.className = "effect-condition";
      const kinds = repoStore.schema.condition.kinds;
      const current = effect.condition ?? core.emptyConditionValue();

      body.append(formField("kind", selectControl(kinds, current.kind, (next) => {
        applyCardEdit(next === kinds[0]
          ? core.setEffectCondition(card, index, null)
          : core.setEffectCondition(card, index, { kind: next }));
      })));

      if (effect.condition) {
        for (const field of repoStore.schema.condition.fields) {
          body.append(formField(field.name, paramControl(
            field,
            current[field.name],
            (value) => applyCardEdit(
              core.setEffectCondition(card, index, { [field.name]: value })),
          )));
        }
      }

      wrap.append(body);
      return wrap;
    }

    function actionButton(label, onClick) {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = label;
      button.addEventListener("click", onClick);
      return button;
    }

    function effectRow(card, effect, index) {
      const row = document.createElement("div");
      row.className = effect.raw ? "effect-row is-locked" : "effect-row";

      const head = document.createElement("div");
      head.className = "effect-head";

      if (effect.raw) {
        const locked = document.createElement("span");
        locked.textContent = `해석 못 함: ${effect.kind}`;
        head.append(locked);
      } else {
        head.append(selectControl(
          repoStore.schema.effectOrder.map((kind) => kind),
          effect.kind,
          (next) => applyCardEdit(core.setEffectKind(card, index, next, repoStore.schema)),
        ));
      }

      const actions = document.createElement("div");
      actions.className = "effect-actions";
      actions.append(
        actionButton("▲", () => applyCardEdit(core.moveEffect(card, index, index - 1))),
        actionButton("▼", () => applyCardEdit(core.moveEffect(card, index, index + 1))),
        actionButton("복제", () => applyCardEdit(core.duplicateEffect(card, index))),
        actionButton("삭제", () => applyCardEdit(core.removeEffect(card, index))),
      );
      head.append(actions);
      row.append(head);

      if (effect.raw) return row;

      row.append(paramsBlock(
        repoStore.schema.effects[effect.kind],
        effect.params,
        (name, value) => applyCardEdit(
          core.setEffectParam(card, index, name, value, repoStore.schema)),
      ));
      row.append(conditionBlock(card, index, effect));
      return row;
    }

    function renderEffectRows(card) {
      const section = document.createElement("div");
      section.className = "effect-list";
      (card.effects ?? []).forEach((effect, index) => {
        section.append(effectRow(card, effect, index));
      });

      const add = document.createElement("div");
      add.className = "effect-actions";
      add.append(selectControl(
        ["(효과 추가)", ...repoStore.schema.effectOrder],
        "(효과 추가)",
        (kind) => {
          if (kind !== "(효과 추가)") {
            applyCardEdit(core.addEffect(card, kind, repoStore.schema));
          }
        },
      ));
      section.append(add);
      return poolSection("효과", section);
    }

    /// 개입 섹션은 효과 행과 같은 컨트롤이며 행이 하나뿐이고 조건이 없다(설계 6).
    function renderInterventionRow(card) {
      const row = document.createElement("div");
      row.className = "effect-row";

      const head = document.createElement("div");
      head.className = "effect-head";
      head.append(selectControl(
        ["(없음)", ...repoStore.schema.interventionOrder],
        card.intervention?.kind ?? "(없음)",
        (kind) => applyCardEdit(core.setIntervention(
          card, kind === "(없음)" ? "" : kind, repoStore.schema)),
      ));
      row.append(head);

      if (card.intervention && !card.intervention.raw) {
        row.append(paramsBlock(
          repoStore.schema.interventions[card.intervention.kind],
          card.intervention.params,
          (name, value) => applyCardEdit(
            core.setInterventionParam(card, name, value, repoStore.schema)),
        ));
      }
      if (card.intervention?.raw) {
        row.className = "effect-row is-locked";
        const locked = document.createElement("p");
        locked.className = "repo-readonly";
        locked.textContent = `해석 못 함: ${card.intervention.kind}`;
        row.append(locked);
      }

      return poolSection("개입", row);
    }
```

`renderCardDetail`의 마지막 `elements.detail.append(form);` **바로 뒤**에 두 줄 더한다:

```js
      elements.detail.append(form);
      if (card.category === "Execution") elements.detail.append(renderEffectRows(card));
      else elements.detail.append(renderInterventionRow(card));
```

- [x] **Step 5: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **167/167** (166 + 신규 1).

- [x] **Step 6: 브라우저에서 수동 검수한다**

1. `vanguard_slash`를 고르면 효과 섹션에 `damage` 행 하나가 나오고 `value 5`, `selector FrontOne`,
   그 옆에 글리프 `◆━━━━`가 보인다.
2. `value`를 7로 바꾸면 배지가 `✎ 수정됨`이 되고, 되돌리면 `● 저장소와 동일`로 돌아온다.
3. `(효과 추가)`에서 `apply_status`를 고르면 행이 붙고 **상태 드롭다운에 11개**가 한국어 이름과
   함께 나온다. 상태를 고르지 않으면 오류에 `등록되지 않은 상태 키입니다: ''`가 뜬다.
4. `▲`·`▼`로 순서가 바뀌고 `복제`·`삭제`가 동작한다.
5. `riposte`를 고르면 조건이 펼쳐진 채 `PrevExecutedIsEnemyDamageCard`와
   `successEffectValue 7`이 보인다. `kind`를 `None`으로 바꾸면 조건 칸이 사라진다.
6. 개입 카드(`hasten`)를 고르면 개입 섹션에 `change_execution_order`와 `delta`·`targetSide`가
   나온다. `lock`으로 바꾸면 **파라미터 칸이 하나도 없다.**
7. 카드에 손으로 모르는 효과 kind를 넣고 다시 읽으면 그 행이 `해석 못 함`으로 잠긴다.

- [x] **Step 7: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 효과와 개입을 구조화 폼으로 저작한다

파라미터 칸을 그리는 함수 하나가 효과와 개입 양쪽을 그린다. 계획 3.5가 개입을
다형화하고 계획 A의 스키마가 둘을 같은 모양으로 내므로 렌더러가 하나면 된다.

스키마의 필드 타입 넷이 컨트롤 넷으로 간다. C#에 스펙을 더하면 폼이 저절로 생기고
노트북 소스는 바뀌지 않는다.

해석 못 하는 행은 잠근다. 종류와 파라미터를 바꿀 수 없고 삭제와 이동만 된다."
```

---

### Task 8: 풀 편성 조작과 소속 풀 체크박스

**Files:**
- Modify: `Tools/card-idea-notebook/index.html` (`<script data-repo-ui>`, CSS)
- Test: `Tools/card-idea-notebook/index.test.mjs` (마크업 검사)

**Interfaces:**
- Consumes: Task 5의 풀 편집 명령, Task 6의 `applyPoolEdit`
- Produces: `renderPoolDetail`의 편성 섹션에 조작 버튼, `renderCardDetail`에 소속 풀 체크박스.
  이 태스크가 끝나면 계획 C가 끝난다.

**소유권은 풀에 있다**(설계 §7). 카드 화면의 체크박스는 **담기만** 하고, 위치 조정과 제거는 풀
화면에서만 된다. 이미 소속된 풀은 체크된 채 비활성이라 체크 해제로 제거되는 사고가 없다.

- [x] **Step 1: 실패하는 마크업 테스트를 쓴다**

```js

test("풀 담기와 소속 표시의 스타일이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /\.pool-membership\b/);
  assert.match(html, /\.pool-picker\b/);
});
```

- [x] **Step 2: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: **FAIL 1개.** `.pool-membership`을 찾지 못한다.

- [x] **Step 3: CSS를 넣는다**

`.effect-glyph` 뒤에 넣는다.

```css
    .pool-membership { display: grid; gap: 5px; font-size: 12px; }
    .pool-membership label { display: flex; gap: 7px; align-items: center; color: var(--ink); }
    .pool-membership .owned { color: var(--muted); font-size: 11px; }
    .pool-picker { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
    .pool-roster .slot .slot-actions { display: flex; gap: 4px; }
    .pool-roster .slot .slot-actions button { padding: 2px 7px; font-size: 11px; }
```

`.pool-roster .slot`의 열 정의를 조작 버튼 자리까지 넷으로 늘린다:

```css
    .pool-roster .slot {
      display: grid;
      grid-template-columns: 28px minmax(0, 1fr) auto auto;
      gap: 8px;
      align-items: center;
      padding: 6px 9px;
      border: 1px solid var(--line);
      border-radius: 9px;
    }
```

- [x] **Step 4: 편성 조작과 소속 체크박스를 쓴다**

`renderPoolDetail`의 편성 루프에서 슬롯에 조작 버튼을 붙인다. `slot.append(order, label, tail);`을
바꾼다:

```js
        const slotActions = document.createElement("div");
        slotActions.className = "slot-actions";
        slotActions.append(
          actionButton("▲", () => applyPoolEdit(core.moveInPool(pool, index, index - 1))),
          actionButton("▼", () => applyPoolEdit(core.moveInPool(pool, index, index + 1))),
          actionButton("빼기", () => applyPoolEdit(core.removeFromPool(pool, index))),
        );

        slot.append(order, label, tail, slotActions);
```

편성 섹션 뒤에 `＋ 카드 담기`를 붙인다. `elements.detail.append(poolSection("편성", roster));`
**바로 뒤**에 넣는다:

```js
      const picker = document.createElement("div");
      picker.className = "pool-picker";
      const candidates = repoStore.cards
        .filter((card) => card.side === repoStore.schema.sides[0])
        .filter((card) => card.id && !pool.cards.includes(card.id));
      picker.append(selectControl(
        ["(카드 담기)", ...candidates.map((card) => card.id)],
        "(카드 담기)",
        (cardId) => {
          if (cardId !== "(카드 담기)") {
            applyPoolEdit(core.addCardsToPool(pool, [cardId]));
          }
        },
      ));
      elements.detail.append(picker);
```

카드 화면에 소속 풀 체크박스를 붙인다. `renderCardDetail`의 효과·개입 섹션 **뒤**에 넣는다:

```js
      elements.detail.append(renderPoolMembership(card));
```

그리고 `renderCardDetail` **바로 위**에 함수를 넣는다:

```js
    /// 소유권은 풀에 있다(설계 7). 이 컨트롤은 담기만 하고 위치 조정과 제거는 풀 화면에서만
    /// 된다. 이미 소속된 풀을 체크된 채 비활성으로 두면 한 컨트롤이 "현재 상태"와 "이번에 담을
    /// 곳"을 동시에 보여주고, 체크 해제로 제거되는 사고가 없다.
    function renderPoolMembership(card) {
      const block = document.createElement("div");
      block.className = "pool-membership";
      const owners = repoStore.membership.get(card.id) ?? [];
      const chosen = new Set();

      for (const pool of repoStore.pools) {
        const owned = owners.includes(pool.id);
        const label = document.createElement("label");
        const box = document.createElement("input");
        box.type = "checkbox";
        box.checked = owned;
        box.disabled = owned || !card.id;
        box.addEventListener("change", () => {
          if (box.checked) chosen.add(pool.id);
          else chosen.delete(pool.id);
        });

        const name = document.createElement("span");
        name.textContent = pool.id;
        label.append(box, name);

        if (owned) {
          const note = document.createElement("span");
          note.className = "owned";
          note.textContent = "(소속)";
          label.append(note);
        }
        block.append(label);
      }

      const submit = actionButton("선택한 풀에 담기", () => {
        for (const poolId of chosen) {
          const pool = repoStore.pools.find((entry) => entry.id === poolId);
          if (pool) applyPoolEdit(core.addCardsToPool(pool, [card.id]));
        }
      });
      submit.disabled = !card.id;
      block.append(submit);

      return poolSection("소속 풀", block);
    }
```

- [x] **Step 5: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

기대: 노트북 **168/168** (167 + 신규 1), 헤드리스 **526/526**.

- [x] **Step 6: 브라우저에서 최종 검수한다**

1. `풀` 탭 → `starter` → 편성 각 줄에 `▲`·`▼`·`빼기`가 있다.
2. `빼기`를 누르면 22 → 21장이 되고 배지가 `✎ 수정됨`, 요약의 미반영이 1이 된다.
   **분포가 즉시 따라 바뀐다.**
3. `▲`·`▼`로 순서가 바뀐다.
4. `(카드 담기)`에서 `fixture_attack`을 고르면 맨 끝에 붙고 **고아 경고가 사라진다.**
5. 카드 탭에서 카드 하나를 고르면 `소속 풀` 섹션에 `starter`가 **체크된 채 비활성**이다.
6. 고아 카드(`fixture_move_forward`)를 고르면 체크가 풀려 있고, 체크 후
   `선택한 풀에 담기`를 누르면 풀 편성 끝에 붙는다.
7. `＋ 새 카드` → id·이름·효과를 채우고 풀에 담는다. **오류가 0이 된다.**
8. 탭을 새로 고쳐도 편집분 전부가 남아 있다.
9. **`git status`가 깨끗하다** — 이 계획은 파일을 쓰지 않는다.

- [x] **Step 7: 커밋**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): 노트북이 풀 편성을 조작한다

소유권은 풀에 있다. 카드 화면의 체크박스는 담기만 하고 위치 조정과 제거는 풀
화면에서만 된다.

이미 소속된 풀은 체크된 채 비활성이다. 한 컨트롤이 현재 상태와 이번에 담을 곳을
동시에 보여주고, 체크 해제로 제거되는 사고가 없다."
```

---

## 완료 기준

계획 C가 끝났을 때:

1. `node --test "Tools/card-idea-notebook/*.test.mjs"`가 **168/168** 통과한다.
2. `dotnet test`가 **526/526** 통과한다 — 이 계획은 C#을 건드리지 않는다.
3. 저장소 카드를 폼으로 고치면 목록 배지가 `✎ 수정됨`이 되고, 되돌리면 `● 저장소와 동일`로
   돌아온다.
4. `＋ 새 카드`로 만든 카드에 id·이름·효과를 채우고 풀에 담으면 **검증 오류가 0이 된다.**
5. 효과 종류를 바꾸면 파라미터 칸이 통째로 바뀌고, `lock` 개입은 파라미터 칸을 하나도 내지 않는다.
6. 탭을 새로 고쳐도 편집분이 전부 남아 있다.
7. 모르는 효과 `kind`가 든 카드를 열면 그 행이 잠기고, 모르는 최상위 키가 든 카드는 폼 대신
   안내가 나온다.
8. **`git status`가 깨끗하다.** 이 계획은 파일을 쓰지 않는다.

설계 §16 검수 기준 중 이 계획이 담당하는 것은 **7의 절반**(모르는 효과 kind를 편집으로 깨뜨리지
않는다 — 내보낸 뒤 파일이 바뀌지 않는지는 계획 D가 확인한다)이다. 2·4·5와 8의 나머지는 쓰기가
필요하므로 계획 D가 맡는다.

---

## 실행 기록 (2026-08-07)

Task 1~8을 순서대로 실행했다. **노트북 168/168, 헤드리스 526/526.** 저장소 콘텐츠 파일은 하나도
바뀌지 않았다 — 이 계획은 파일을 쓰지 않는다.

### 계획이 놓친 것 넷

1. **이름 충돌 — 가장 컸다.** `createCard`와 `editCardField`는 **Markdown 경로가 이미 쓰고 있다.**
   같은 스코프에 다시 선언하니 함수 선언 호이스팅이 옛 것을 가려 **Markdown 테스트 여섯이 한꺼번에
   실패했다.** `createCardModel`·`setCardField`로 바꿨다. Task 4·5 착수 전에는 이름 충돌을 먼저
   grep으로 확인했고 그쪽은 깨끗했다.

2. **계획 B의 테스트 둘이 옛 계약을 인코딩하고 있었다.** `cardListView`의 `states`를 카드 `id`로
   색인하던 테스트를 uid로 옮겼다. 계획은 이 갱신을 적지 않았다.

3. **`renderSummary`의 이른 반환.** 미반영 저장 실패 안내를 `renderSummary` 끝에 두라고 했는데,
   그 함수에는 `message`·미연결 두 갈래의 이른 반환이 있어 연결 전에는 안내가 표시되지 않는다.
   `render()`로 옮겼다.

4. **탭 동기화 누락 — 검수로 잡았다.** `applyPoolEdit`이 `selection`만 바꾸고 탭은 그대로 둬서
   **목록은 카드인데 상세는 풀인 화면**이 나왔다. `selectEntry`만 탭을 맞추고 있던 것을
   `selectWithTab`으로 뽑아 편집 경로도 함께 쓰게 했다.

### 테스트 개수의 어긋남

계획이 Task 2를 `신규 5`, Task 4를 `신규 11`로 셌지만 실제 테스트는 6개와 10개였다. 총계는
우연히 맞아떨어졌고(158) 최종 수치도 계획대로 **168**이다. 중간 기대치만 1씩 어긋났다.

### 검수 방법과 그 한계

계획 B와 같이 `window.showDirectoryPicker`를 `fetch` 기반 가짜 디렉터리 핸들로 바꿔치고 나머지는
진짜 코드를 태웠다. **가짜 핸들의 `fetch`에 `{ cache: "reload" }`가 필요하다** — 없으면 브라우저
HTTP 캐시가 옛 내용을 돌려줘서 "저장소 다시 읽기"가 바뀐 파일을 못 본다. 실제 File System Access
API는 HTTP 캐시를 거치지 않으므로 **이것은 대역의 한계이지 제품 결함이 아니다.**

검수 입력에서 `venom_thrust`를 새 카드 id로 골랐다가 그것이 이미 저장소에 있고 `starter`에도
들어 있다는 것을 발견했다. 노트북은 옳게 동작했다 — 중복 id 오류를 냈고, 같은 id가 이미 풀에
있어 소속 체크박스가 비활성이었다. `thorn_lash`로 다시 했다.

### 실측한 검수 결과

| 항목 | 결과 |
|---|---|
| 폼 | id·이름·진영·분류·비용·실행 순서·등급·태그 여덟 칸 |
| 편집 ↔ 되돌리기 | `✎ 수정됨 · 미반영 1` ↔ `● 저장소와 동일 · 미반영 0` |
| 분류 전환 | `Intervention`으로 바꾸면 실행 순서 칸이 사라지고 `개입 액션이 필요합니다` 오류 |
| 효과 편집기 | `damage value=5 selector=FrontOne` + 글리프 `◆━━━━` |
| 조건 | `riposte`가 펼쳐진 채 `successEffectValue=7` |
| 상태 드롭다운 | 11개 + `(없음)`, 한국어 이름과 키 병기 |
| 개입 | `hasten`이 `delta=-1 targetSide=Player`, `lock`은 **파라미터 칸 0개** |
| 행 조작 | `▲`·`▼`·복제·삭제 전부 동작 |
| 풀 조작 | 빼기 22→21에 분포가 즉시 따라감, 담기로 22 복귀 |
| 풀 규칙 | `fixture_attack`을 담자 **등급·태그 오류**가 뜨고 빼낸 카드가 고아 경고로 이동 |
| 소속 체크박스 | 소속 풀이 `체크 + 비활성 + (소속)` |
| 새 카드 완주 | id·이름·등급·태그·효과를 채우고 풀에 담아 **오류 0** |
| 보존 | 새로고침 후 미반영 유지 |
| 모르는 키 | 폼 대신 안내, 원문은 그대로 |

### 계획 D에 넘기는 것

"다음" 절 그대로다. 저장소 UI 스크립트에 쓰기를 붙이고 옛 Markdown 스크립트와 모드 전환을 지운다.

## 다음

계획 C가 머지되면 계획 D를 작성한다. 범위는 설계 §10.3 충돌 해결 UI, §12 diff 요약과 파일 쓰기
(폴더 권한 `readwrite` 승격, 내보내기 직전 재읽기), 우측 `저장소 ↔ 현재` diff 토글,
§14 마이그레이션(`SCHEMA_VERSION` 7과 옛 키 백업), Markdown 경로와 그 UI 스크립트·마크업 제거,
`시작 카드 풀.md` 삭제,
[플레이어 캐릭터 및 카드풀](../specs/2026-07-20-character-card-pools-design.md) §1 개정,
옛 노트북 스펙 `archive/` 이동이다.
