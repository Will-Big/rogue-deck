# Card Idea Notebook V2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the single-file card notebook so every card is immediately preserved in one complete/incomplete list, completed cards can be selected in bulk, and exported Markdown can be imported atomically.

**Architecture:** `Tools/card-idea-notebook/index.html` remains the only user-facing file and embeds the pure model, storage, Markdown parser, CSS, and DOM controller. The stored schema moves to version 2: `cards[]` owns both complete and incomplete cards, `activeCardId` selects the edited entry, and no separate draft is persisted or used at runtime. Pure transitions stay exposed through `globalThis.CardIdeaNotebook` for Node tests.

**Tech Stack:** HTML5, CSS, vanilla JavaScript, browser `localStorage`, `FileReader`/`File.text()`, browser `Blob`, Node 18+ built-in `node:test`.

## Global Constraints

- Keep one self-contained user-facing file at `Tools/card-idea-notebook/index.html`; add no runtime file, server, framework, or external package.
- Every new, duplicated, imported, or edited card belongs to `cards[]` and receives a unique ID.
- Every authoring-field edit immediately marks the card `incomplete` and attempts to persist the full state.
- `저장` and `Ctrl+S`/`Cmd+S` validate the active card and mark it `complete`; they are completion actions, not the only persistence path.
- Only `complete` cards can be selected or exported. Bulk selection applies to every complete card, independent of search filtering.
- Markdown import accepts only this tool's bundle format, adds every parsed card as a new complete card, suffixes duplicate names, and changes no state if any card is malformed.
- Schema 1 cards migrate to `complete`; unknown schemas remain protected from writes.
- Storage write failure keeps the in-memory state, exposes a persistent failure status, retries on later changes, and enables the unload warning until a full write succeeds.
- The tool never writes Unity, ScriptableObject, or repository game data.

---

### Task 1: Schema 2 and Complete/Incomplete Card Transitions

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: existing `normalizeCard(input)`, `validateCard(input)`, `writeStore(storage, state)`.
- Produces:
  - `uniqueCardName(cards, requestedName) -> string`
  - `createCard(state, options) -> state`
  - `editCard(state, id, patch) -> state`
  - `completeCard(state, id, options) -> state`
  - `duplicateCard(state, id, options) -> state`
  - schema 2 `readStore(storage)` and `writeStore(storage, state)`.

- [x] **Step 1: Write failing schema and transition tests**

Add tests with literal expectations:

```js
test("migrates schema 1 cards to complete schema 2 cards", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 1,
      cards: [{ id: "a", name: "기존 카드" }],
      activeCardId: "a",
      searchQuery: "",
      exportSelection: ["a"],
    }),
  });

  const state = core.readStore(storage);
  assert.equal(state.schemaVersion, 2);
  assert.equal(state.cards[0].completionStatus, "complete");
});

test("creates uniquely named incomplete cards directly in the list", () => {
  const core = loadCore();
  const first = core.createCard(core.initialState(), { id: "a", now: "2026-07-27T00:00:00.000Z" });
  const second = core.createCard(first, { id: "b", now: "2026-07-27T00:01:00.000Z" });
  assert.deepEqual(first.cards.map((card) => card.name), ["새 카드"]);
  assert.deepEqual(second.cards.map((card) => card.name), ["새 카드", "새 카드 (2)"]);
  assert.equal(second.cards[1].completionStatus, "incomplete");
  assert.equal(second.activeCardId, "b");
});

test("editing a complete card makes it incomplete and unselects it", () => {
  const core = loadCore();
  const complete = core.normalizeCard({ id: "a", name: "완성 카드", completionStatus: "complete" });
  const state = {
    ...core.initialState(),
    cards: [complete],
    activeCardId: "a",
    exportSelection: ["a"],
  };
  const edited = core.editCard(state, "a", { notes: "수정됨" });
  assert.equal(edited.cards[0].completionStatus, "incomplete");
  assert.deepEqual([...edited.exportSelection], []);
});
```

- [x] **Step 2: Run tests and verify RED**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: FAIL because schema 2 and the new transition functions do not exist.

- [x] **Step 3: Implement schema 2 and minimal transitions**

Set `SCHEMA_VERSION = 2`; normalize `completionStatus` to `complete` or `incomplete`; migrate schema 1 during `readStore`; stop storing or constructing `draft`. Implement unique names by testing the requested name, then `name (2)`, `name (3)`, and so on. `completeCard` must throw `카드 이름을 입력하세요.` when validation has an error and otherwise set completion timestamps without changing warning behavior.

- [x] **Step 4: Run tests and verify GREEN**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: all old compatible tests and new Task 1 tests PASS.

- [x] **Step 5: Commit Task 1**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "refactor(tools): unify card draft lifecycle"
```

### Task 2: Strict Markdown Bundle Import

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: `normalizeCard`, `targetSummary`, `bundleMarkdown`, `uniqueCardName`.
- Produces:
  - `parseBundleMarkdown(markdown) -> Card[]` or throws
  - `importCards(state, markdown, options) -> state`
  - `bulkSelection(state, checked) -> state`.

- [x] **Step 1: Write failing round-trip, duplicate-name, bulk-selection, and atomic-error tests**

```js
test("round-trips exported cards through strict Markdown import", () => {
  const core = loadCore();
  const source = [
    core.normalizeCard({
      name: "맹독 호위",
      role: "execution",
      cost: "1",
      executionOrder: "4",
      tags: ["독", "방어"],
      targets: { ally: "backOne", enemy: "frontTwo" },
      abilities: { ally: ["방어 4."], enemy: ["독 2."], none: ["카드 1장 뽑기."] },
      notes: "왕복 확인",
      completionStatus: "complete",
    }),
  ];
  const parsed = core.parseBundleMarkdown(core.bundleMarkdown(source, "2026-07-27"));
  assert.equal(parsed.length, 1);
  assert.equal(parsed[0].name, "맹독 호위");
  assert.deepEqual([...parsed[0].abilities.enemy], ["독 2."]);
  assert.equal(parsed[0].completionStatus, "complete");
});

test("imports duplicate names as new numbered cards", () => {
  const core = loadCore();
  const existing = core.normalizeCard({ id: "a", name: "맹독 호위", completionStatus: "complete" });
  const markdown = core.bundleMarkdown([
    core.normalizeCard({ name: "맹독 호위", completionStatus: "complete" }),
    core.normalizeCard({ name: "맹독 호위", completionStatus: "complete" }),
  ], "2026-07-27");
  const imported = core.importCards(
    { ...core.initialState(), cards: [existing] },
    markdown,
    { ids: ["b", "c"], now: "2026-07-27T01:00:00.000Z" },
  );
  assert.deepEqual(imported.cards.map((card) => card.name), [
    "맹독 호위",
    "맹독 호위 (2)",
    "맹독 호위 (3)",
  ]);
});

test("rejects a malformed bundle without changing existing state", () => {
  const core = loadCore();
  const existing = core.normalizeCard({ id: "a", name: "보존 카드", completionStatus: "complete" });
  const state = { ...core.initialState(), cards: [existing] };
  assert.throws(() => core.importCards(state, "# 잘못된 파일"), /불러올 수 없는 Markdown/);
  assert.deepEqual(state.cards.map((card) => card.name), ["보존 카드"]);
});

test("bulk selection includes all and only complete cards", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "완성", completionStatus: "complete" }),
      core.normalizeCard({ id: "b", name: "미완성", completionStatus: "incomplete" }),
    ],
  };
  assert.deepEqual([...core.bulkSelection(state, true).exportSelection], ["a"]);
});
```

- [x] **Step 2: Run tests and verify RED**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: FAIL because import and bulk-selection functions do not exist.

- [x] **Step 3: Implement the strict parser and atomic import transition**

Require the exact `# Fate Weaver 카드 아이디어` header, integer `카드 수`, matching `##` card count, known role labels, known target line format, and exported `### 능력`/`### 메모` sections. Parse into temporary normalized cards before creating any IDs or changing state. Make every imported card `complete`, assign provided IDs in order, suffix names against the growing result list, activate the first imported card, and perform no mutation on failure.

- [x] **Step 4: Run tests and verify GREEN**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: all Task 1–2 tests PASS.

- [x] **Step 5: Commit Task 2**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): import card Markdown bundles"
```

### Task 3: Immediate-Persistence Browser UI

**Files:**
- Modify: `Tools/card-idea-notebook/index.html`
- Modify: `Tools/card-idea-notebook/index.test.mjs`

**Interfaces:**
- Consumes: Tasks 1–2 state transitions and parser.
- Produces: immediate-persistence controller, status badges, complete-only checkboxes, bulk checkbox, hidden `.md` file input, import button, and storage-recovery state.

- [x] **Step 1: Add failing persistence-recovery tests**

Add a storage transition that can be exercised without DOM:

```js
test("failed immediate persistence keeps memory state dirty until retry succeeds", () => {
  const core = loadCore();
  const storage = new ToggleStorage();
  let session = core.createCard(core.initialState(), { id: "a", now: "2026-07-27T00:00:00.000Z" });
  storage.failWrites = true;
  const failed = core.tryWriteStore(storage, session);
  assert.equal(failed.persistFailed, true);
  assert.equal(failed.state.cards[0].name, "새 카드");

  storage.failWrites = false;
  const recovered = core.tryWriteStore(storage, failed.state);
  assert.equal(recovered.persistFailed, false);
  assert.equal(JSON.parse(storage.getItem(core.STORAGE_KEY)).cards[0].name, "새 카드");
});
```

- [x] **Step 2: Run tests and verify RED**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: FAIL because `tryWriteStore` and the test storage helper do not exist.

- [x] **Step 3: Implement immediate persistence and UI state changes**

Remove the unsaved-change navigation dialog and draft bindings. Create a card immediately when `새 카드` is clicked; bind the form directly to the active `cards[]` entry; on every authoring input call `editCard` then attempt a full write. Render `완성`/`미완성` badges, disable incomplete export checkboxes, remove an edited complete card from selection, and make the existing save button call `completeCard` followed by full persistence.

Add a header bulk checkbox whose checked state means every complete card ID is selected and whose indeterminate state means only some complete cards are selected. Add `Markdown 불러오기` and a hidden `<input type="file" accept=".md,text/markdown,text/plain">`; read one file, parse and validate entirely, then write the combined state once before replacing in-memory state. On storage failure, keep the combined in-memory state unchanged from before import and show the error.

Use `tryWriteStore` for regular immediate changes. If a write fails, retain the new in-memory state, display `보존 실패`, and enable `beforeunload`; later successful full writes clear the failure. Deletion and import remain transactional: replace in-memory state only after their storage write succeeds.

- [x] **Step 4: Run automated tests**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: all tests PASS with zero failures.

- [x] **Step 5: Run browser smoke verification**

Serve only the worktree on loopback:

```bash
python3 -m http.server 8765 --bind 127.0.0.1
```

Verify:

1. `새 카드`, `새 카드 (2)` appear immediately and survive reload without completion.
2. Switching between incomplete cards restores each card's fields without a dialog.
3. Completing a card enables its checkbox; editing it marks it incomplete and unchecks it.
4. Bulk selection selects every complete card and never an incomplete card.
5. Export then import restores cards; duplicate names receive `(2)`, `(3)`.
6. A malformed Markdown file adds no cards.
7. Browser console has no errors.

- [x] **Step 6: Commit Task 3**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): preserve incomplete cards immediately"
```

### Task 4: Documentation, Final Verification, and Archive

**Files:**
- Modify: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`
- Move: `docs/superpowers/plans/2026-07-27-card-idea-notebook-v2.md` to `docs/superpowers/archive/plans/2026-07-27-card-idea-notebook-v2.md`

**Interfaces:**
- Consumes: complete Tasks 1–3 behavior and browser evidence.
- Produces: implementation-complete authoritative spec and archived execution record.

- [x] **Step 1: Mark the design implemented and archive this plan**

Change the spec status to `current — 즉시 보존·완성 상태·Markdown 불러오기 구현 완료`, move this plan to the archive, remove it from active plans, and add it under the archive's external card authoring tool section.

- [x] **Step 2: Self-review documentation**

Run:

```bash
rg -n 'T[B]D|T[O]DO|구현 [대]기|결정 [대]기' \
  docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md \
  docs/superpowers/archive/plans/2026-07-27-card-idea-notebook-v2.md
```

Expected: no matches.

- [x] **Step 3: Run final verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
git diff --check
git status --short
```

Expected: every test passes, no whitespace errors, and only intended documentation changes remain.

- [x] **Step 4: Commit documentation**

```bash
git add docs/superpowers
git commit -m "docs: complete card notebook v2 implementation"
```

## Implementation Result

- 카드 상태를 별도 초안 없이 하나의 `cards[]` 목록으로 통합했다.
- 모든 편집을 즉시 `localStorage`에 보존하고, 저장 버튼은 완성 상태 전환으로 유지했다.
- 완성 카드만 개별·전체 선택 및 Markdown 내보내기에 포함되도록 했다.
- 도구가 내보낸 Markdown 묶음을 원자적으로 불러오고 중복 이름에 번호를 붙이도록 했다.
- Node 테스트 26개와 실제 브라우저 흐름을 검증했으며 브라우저 콘솔 오류는 없었다.
