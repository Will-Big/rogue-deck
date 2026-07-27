# Card Notebook Shared Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separate the card list from its action bar and replace export-only selection with one complete/incomplete card selection used by bulk deletion and guarded Markdown export.

**Architecture:** Keep the self-contained `Tools/card-idea-notebook/index.html`, but migrate stored state to schema 3 with `selection[]`. Pure transitions determine selection, deletion targets, bulk deletion, and export eligibility; the DOM controller only renders those decisions and persists them. The library pane becomes a two-row grid whose first row scrolls and whose action bar occupies normal layout space.

**Tech Stack:** HTML5, CSS, vanilla JavaScript, browser `localStorage`, Node 18+ built-in `node:test`, in-app browser smoke verification.

## Global Constraints

- Keep one self-contained user-facing file at `Tools/card-idea-notebook/index.html`; add no runtime dependency, framework, or server requirement.
- Every complete and incomplete card can be selected through the same checkbox.
- Bulk selection applies to all cards, independent of search filtering.
- Delete selected cards when selection is non-empty; otherwise delete only the active card.
- Block the entire Markdown export when any selected card is incomplete.
- Preserve schema 1 and 2 local data through explicit schema 3 migration.
- Keep deletion transactional: replace in-memory state only after the full storage write succeeds.
- Do not modify Unity, ScriptableObject, or game data.

---

### Task 1: Schema 3 Shared Selection and Pure Transitions

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: `normalizeCard(input)`, `initialState()`, `readStore(storage)`, `writeStore(storage, state)`.
- Produces:
  - schema 3 state field `selection: string[]`
  - `selectedCards(state) -> Card[]`
  - `bulkSelection(state, checked) -> state`
  - `deletionIds(state) -> string[]`
  - `deleteCards(state, ids) -> state`
  - `exportStatus(state) -> { kind: "ready" } | { kind: "error", message: string }`
  - `cardsForExport(state) -> Card[]`.

- [ ] **Step 1: Write failing schema migration and shared-selection tests**

Add literal tests:

```js
test("migrates schema 2 export selection into schema 3 shared selection", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 2,
      cards: [
        { id: "a", name: "완성", completionStatus: "complete" },
        { id: "b", name: "미완성", completionStatus: "incomplete" },
      ],
      activeCardId: "b",
      searchQuery: "",
      exportSelection: ["a", "b"],
    }),
  });

  const state = core.readStore(storage);
  assert.equal(state.schemaVersion, 3);
  assert.deepEqual([...state.selection], ["a", "b"]);
});

test("editing a selected complete card keeps it selected while making it incomplete", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [core.normalizeCard({ id: "a", name: "카드", completionStatus: "complete" })],
    selection: ["a"],
  };

  const edited = core.editCard(state, "a", { notes: "수정" });
  assert.equal(edited.cards[0].completionStatus, "incomplete");
  assert.deepEqual([...edited.selection], ["a"]);
});

test("bulk selection includes complete and incomplete cards", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "완성", completionStatus: "complete" }),
      core.normalizeCard({ id: "b", name: "미완성", completionStatus: "incomplete" }),
    ],
  };

  assert.deepEqual([...core.bulkSelection(state, true).selection], ["a", "b"]);
  assert.deepEqual([...core.bulkSelection(state, false).selection], []);
});
```

- [ ] **Step 2: Run Task 1 migration tests and verify RED**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: FAIL because the current schema is 2 and state still uses `exportSelection`.

- [ ] **Step 3: Implement schema 3 and shared selection**

Set `SCHEMA_VERSION = 3`. Make `initialState()` return `selection: []`; make `writeStore` serialize only existing card IDs from `selection`; make `readStore` accept schemas 1, 2, and 3. For schemas 1 and 2, read `exportSelection`; for schema 3, read `selection`. Preserve selected complete and incomplete IDs that still exist. Remove the edit-time selection removal.

- [ ] **Step 4: Run Task 1 migration tests and verify GREEN**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: all schema and selection tests PASS.

- [ ] **Step 5: Write failing deletion and guarded-export tests**

Add:

```js
test("chooses selected cards for deletion and falls back to the active card", () => {
  const core = loadCore();
  const base = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "첫 카드" }),
      core.normalizeCard({ id: "b", name: "둘째 카드" }),
      core.normalizeCard({ id: "c", name: "셋째 카드" }),
    ],
    activeCardId: "b",
  };

  assert.deepEqual([...core.deletionIds({ ...base, selection: ["a", "c"] })], ["a", "c"]);
  assert.deepEqual([...core.deletionIds(base)], ["b"]);
});

test("bulk deletion preserves an unselected active card and selects a fallback when active is deleted", () => {
  const core = loadCore();
  const cards = [
    core.normalizeCard({ id: "a", name: "첫 카드" }),
    core.normalizeCard({ id: "b", name: "둘째 카드" }),
    core.normalizeCard({ id: "c", name: "셋째 카드" }),
  ];
  const kept = core.deleteCards({
    ...core.initialState(),
    cards,
    activeCardId: "b",
    selection: ["a", "c"],
  }, ["a", "c"]);
  assert.equal(kept.activeCardId, "b");
  assert.deepEqual([...kept.cards.map((card) => card.id)], ["b"]);

  const fallback = core.deleteCards({
    ...core.initialState(),
    cards,
    activeCardId: "b",
    selection: ["b", "c"],
  }, ["b", "c"]);
  assert.equal(fallback.activeCardId, "a");
  assert.deepEqual([...fallback.selection], []);
});

test("blocks all export when selection contains an incomplete card", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "완성", completionStatus: "complete" }),
      core.normalizeCard({ id: "b", name: "미완성", completionStatus: "incomplete" }),
    ],
    selection: ["a", "b"],
  };

  assert.deepEqual({ ...core.exportStatus(state) }, {
    kind: "error",
    message: "미완성 카드는 내보낼 수 없습니다. 먼저 완성 상태로 저장하거나 선택을 해제하세요.",
  });
  assert.deepEqual([...core.cardsForExport(state)], []);
});
```

- [ ] **Step 6: Run deletion and export tests and verify RED**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: FAIL because `deletionIds`, `deleteCards`, and incomplete-selection export blocking do not exist.

- [ ] **Step 7: Implement pure deletion and guarded export**

Implement `selectedCards` in list order. `deletionIds` returns selected existing IDs when non-empty, otherwise the active ID when it exists. `deleteCards` removes all requested IDs, removes those IDs from selection, preserves an unselected active card, and otherwise activates the first remaining card. `exportStatus` reports no selection first, then the exact incomplete warning, and only then returns ready. `cardsForExport` returns an empty list unless export status is ready.

- [ ] **Step 8: Run the complete Node suite and verify GREEN**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: all tests PASS with zero failures.

- [ ] **Step 9: Commit Task 1**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "refactor(tools): share card selection actions"
```

### Task 2: Non-Overlapping Library Layout and Browser Actions

**Files:**
- Modify: `Tools/card-idea-notebook/index.html`
- Modify: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`
- Move: `docs/superpowers/plans/2026-07-28-card-notebook-shared-selection.md` to `docs/superpowers/archive/plans/2026-07-28-card-notebook-shared-selection.md`

**Interfaces:**
- Consumes: Task 1 `selection`, `bulkSelection`, `deletionIds`, `deleteCards`, and `exportStatus`.
- Produces: non-overlapping library grid, shared checkboxes, transactional bulk-delete dialog, incomplete-export toast, browser-verified final tool.

- [ ] **Step 1: Implement the library grid and shared checkbox rendering**

Make `.library-pane` a grid with `grid-template-rows: minmax(0, 1fr) auto`; set its `.pane-scroll` to `min-height: 0`; remove absolute positioning from `.library-actions`; and reduce `.card-list` bottom padding to ordinary list spacing. Rename visible and accessible copy from export selection to shared selection, enable every card checkbox, bind it to `state.selection`, and compute the bulk checkbox against all card IDs.

- [ ] **Step 2: Connect transactional bulk deletion**

At confirmation time, capture `core.deletionIds(state)` instead of one active ID. Render one-card copy with its name and multi-card copy as `선택한 N장의 카드를 삭제합니다. 삭제한 카드는 복구할 수 없습니다.` On confirmation, call `core.deleteCards(state, pendingDeleteCardIds)`, write the entire next state before replacing in-memory state, then close and render. Keep the dialog open and current memory unchanged if storage fails.

- [ ] **Step 3: Connect guarded export**

Call `core.exportStatus(state)` before creating a Blob. When it returns an error, show its message and create no download. When ready, `cardsForExport` returns exactly the selected complete cards.

- [ ] **Step 4: Run automated verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
node -e 'const fs=require("fs"); const h=fs.readFileSync("Tools/card-idea-notebook/index.html","utf8"); for(const m of h.matchAll(/<script(?: [^>]*)?>([\\s\\S]*?)<\\/script>/g)) new Function(m[1]); console.log("scripts parse")'
git diff --check
```

Expected: all Node tests PASS, both scripts parse, and no whitespace errors exist.

- [ ] **Step 5: Run browser smoke verification**

Serve the worktree on loopback and verify:

1. Create enough cards to overflow the library; the final card scrolls fully above the separate action bar.
2. Complete and incomplete cards can both be checked.
3. `전체 체크` selects every card even while search filters the visible list.
4. Selecting two cards and deleting removes exactly those two after count-based confirmation.
5. With no selection, delete removes only the active card after name-based confirmation.
6. A selected incomplete card blocks export and shows the exact warning.
7. Completing all selected cards permits Markdown export.
8. Reload preserves shared selection and console logs contain no errors.

- [ ] **Step 6: Mark the design implemented and archive the plan**

Change the spec status to `current — 공용 선택·일괄 삭제·목록 레이아웃 구현 완료`. Remove this plan from active plans, add it to the archive's external card authoring tool section, move it to `archive/plans/`, and mark every plan checkbox complete.

- [ ] **Step 7: Run final verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
git diff --check
git status --short
```

Expected: all tests pass, no whitespace errors, and only intended documentation/archive changes remain.

- [ ] **Step 8: Commit Task 2**

```bash
git add Tools/card-idea-notebook/index.html docs/superpowers
git commit -m "feat(tools): add shared card actions"
```
