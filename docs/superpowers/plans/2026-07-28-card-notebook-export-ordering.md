# Card Notebook Export Naming and Ordering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users persist a default Markdown filename, choose a one-off filename at export time, and reorder cards through a dedicated drag handle whose order survives reload and controls Markdown output.

**Architecture:** Keep the self-contained `Tools/card-idea-notebook/index.html` and its pure core/DOM controller split. Schema 4 adds only `exportFileName`; `cards[]` remains the single source of truth for list and export order. Pure `downloadFileName` and `reorderCards` functions carry filename and ordering rules, while the controller connects them to a native dialog and HTML drag-and-drop.

**Tech Stack:** HTML5, CSS, vanilla JavaScript, browser `localStorage`, native `<dialog>` and drag-and-drop APIs, Node 18+ built-in `node:test`, in-app browser smoke verification.

## Global Constraints

- Keep one self-contained user-facing file at `Tools/card-idea-notebook/index.html`; add no runtime dependency, framework, or server requirement.
- Read and write only `.md` files.
- Persist only the lower action-bar default filename; never persist an edit made inside the export dialog.
- An empty export name must still download as `fate-weaver-card-ideas-YYYY-MM-DD.md`.
- `cards[]` array order is the permanent list, local storage, and Markdown bundle order.
- Reordering must preserve `activeCardId`, `selection[]`, card content, and completion state.
- Disable drag reordering whenever the trimmed search query is non-empty.
- Do not modify Unity, ScriptableObject, or game data.

---

### Task 1: Schema 4 and Markdown Download Names

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: `initialState()`, `writeStore(storage, state)`, `readStore(storage)`.
- Produces:
  - schema 4 state field `exportFileName: string`
  - `downloadFileName(input: string, date: string) -> string`.

- [ ] **Step 1: Write failing schema 4 and filename tests**

Add literal behavior tests:

```js
test("migrates schema 3 and round-trips the schema 4 default export file name", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 3,
      cards: [{ id: "a", name: "기존 카드", completionStatus: "complete" }],
      activeCardId: "a",
      searchQuery: "",
      selection: ["a"],
    }),
  });

  const migrated = core.readStore(storage);
  assert.equal(migrated.schemaVersion, 4);
  assert.equal(migrated.exportFileName, "");
  assert.deepEqual([...migrated.selection], ["a"]);

  core.writeStore(storage, { ...migrated, exportFileName: "독 카드풀" });
  assert.equal(core.readStore(storage).exportFileName, "독 카드풀");
});

test("normalizes Markdown download names and permits an empty name", () => {
  const core = loadCore();
  assert.equal(core.downloadFileName(" 독 카드풀 ", "2026-07-28"), "독 카드풀.md");
  assert.equal(core.downloadFileName("독 카드풀.MD", "2026-07-28"), "독 카드풀.MD");
  assert.equal(core.downloadFileName("독 카드풀.txt", "2026-07-28"), "독 카드풀.txt.md");
  assert.equal(
    core.downloadFileName("   ", "2026-07-28"),
    "fate-weaver-card-ideas-2026-07-28.md",
  );
});
```

Update existing schema assertions from `3` to `4` and require schema 1 and 2 migrations to end at 4.

- [ ] **Step 2: Run Task 1 tests and verify RED**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: FAIL because the current schema is 3, schema 3 reads `exportSelection`, and `downloadFileName` is undefined.

- [ ] **Step 3: Implement schema 4 and filename normalization**

Set `SCHEMA_VERSION = 4`. Add `exportFileName: ""` to `initialState`, serialize it as a string in `writeStore`, and return it from `readStore`. Accept schemas 1, 2, 3, and 4. Read shared selection from `saved.selection` for schema 3 and 4, otherwise migrate `saved.exportSelection`.

Add and export:

```js
function downloadFileName(input, date) {
  const name = String(input ?? "").trim();
  const base = name || `fate-weaver-card-ideas-${date}`;
  return /\.md$/i.test(base) ? base : `${base}.md`;
}
```

- [ ] **Step 4: Run Task 1 tests and verify GREEN**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: every schema and filename test passes with zero failures.

- [ ] **Step 5: Commit Task 1**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): persist card export filenames"
```

### Task 2: Pure Card Reordering

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: schema 4 state and existing `cardsForExport(state)`.
- Produces: `reorderCards(state, draggedId, targetId, placement: "before" | "after") -> state`.

- [ ] **Step 1: Write failing insertion and preservation tests**

Add:

```js
test("inserts a dragged card before or after a target without changing selection or active card", () => {
  const core = loadCore();
  const cards = ["a", "b", "c", "d"].map((id) => core.normalizeCard({
    id,
    name: id,
    completionStatus: "complete",
  }));
  const state = {
    ...core.initialState(),
    cards,
    activeCardId: "b",
    selection: ["a", "c"],
  };

  const after = core.reorderCards(state, "a", "c", "after");
  assert.deepEqual([...after.cards.map((card) => card.id)], ["b", "c", "a", "d"]);
  assert.equal(after.activeCardId, "b");
  assert.deepEqual([...after.selection], ["a", "c"]);

  const before = core.reorderCards(state, "d", "b", "before");
  assert.deepEqual([...before.cards.map((card) => card.id)], ["a", "d", "b", "c"]);
  assert.strictEqual(core.reorderCards(state, "b", "b", "before"), state);
});

test("exports selected cards in the reordered list order", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: ["a", "b", "c"].map((id) => core.normalizeCard({
      id,
      name: id,
      completionStatus: "complete",
    })),
    selection: ["a", "c"],
  };

  const reordered = core.reorderCards(state, "c", "a", "before");
  assert.deepEqual(
    [...core.cardsForExport(reordered).map((card) => card.id)],
    ["c", "a"],
  );
});
```

- [ ] **Step 2: Run Task 2 tests and verify RED**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: FAIL with `core.reorderCards is not a function`.

- [ ] **Step 3: Implement immutable insertion**

Add and export:

```js
function reorderCards(state, draggedId, targetId, placement) {
  const cards = [...(state.cards ?? [])];
  if (draggedId === targetId || !["before", "after"].includes(placement)) return state;
  const draggedIndex = cards.findIndex((card) => card.id === draggedId);
  const originalTargetIndex = cards.findIndex((card) => card.id === targetId);
  if (draggedIndex < 0 || originalTargetIndex < 0) return state;

  const [dragged] = cards.splice(draggedIndex, 1);
  const targetIndex = cards.findIndex((card) => card.id === targetId);
  cards.splice(placement === "after" ? targetIndex + 1 : targetIndex, 0, dragged);
  return { ...state, cards };
}
```

The returned state changes only `cards`. Unknown IDs, the same ID, and invalid placement return the original state object.

- [ ] **Step 4: Run Task 2 tests and verify GREEN**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: all tests pass and the new export-order assertion returns `["c", "a"]`.

- [ ] **Step 5: Commit Task 2**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): reorder card notebook cards"
```

### Task 3: Filename Dialog and Drag Handle UI

**Files:**
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: Task 1 `exportFileName`, `downloadFileName`; Task 2 `reorderCards`.
- Produces: persistent lower filename input, one-off native export dialog, dedicated drag handles, drop indicators, search-time drag disabling.

- [ ] **Step 1: Run a browser RED smoke check**

Serve the worktree on loopback and inspect the current page. Verify the old UI has no `기본 파일명` textbox, no export-name dialog, and no `순서 변경` drag handles. This establishes the visible behavior is absent before production UI changes.

- [ ] **Step 2: Add filename controls and dialog markup**

Add a wide lower action-bar label:

```html
<label class="wide-action export-name-field">
  기본 파일명
  <input id="export-file-name" type="text" placeholder="fate-weaver-card-ideas-YYYY-MM-DD">
</label>
```

Add a native dialog after the delete dialog:

```html
<dialog id="export-dialog">
  <form id="export-form" method="dialog">
    <div class="dialog-body">
      <h2>Markdown 파일명</h2>
      <label>
        파일명
        <input id="export-dialog-name" type="text" autocomplete="off">
      </label>
      <p>확장자 .md는 자동으로 붙습니다. 빈 이름도 내보낼 수 있습니다.</p>
    </div>
    <div class="dialog-actions">
      <button type="button" class="ghost" data-export-action="cancel">취소</button>
      <button type="submit" class="primary">내보내기</button>
    </div>
  </form>
</dialog>
```

Style the lower field without absolute positioning so the separate action row remains non-overlapping.

- [ ] **Step 3: Connect persistent and one-off filenames**

Register `exportFileName`, `exportDialog`, `exportForm`, and `exportDialogName` in `elements`. Render `state.exportFileName` into the lower field. On lower-field input, persist `{ ...state, exportFileName: elements.exportFileName.value }` without editing any card.

Change `downloadSelectedCards(fileName)` to set:

```js
anchor.download = core.downloadFileName(fileName, date);
```

Keep `exportStatus` as the first gate. Only after it returns ready, copy `state.exportFileName` into the dialog, open it, focus it, and select its contents. The form submit downloads using the dialog value and closes the dialog. The cancel button closes it. Native `Escape` cancellation requires no state mutation.

- [ ] **Step 4: Add drag handle markup, styles, and controller events**

Change the card row grid to `24px 24px minmax(0, 1fr)`. Create a `button` handle with `type="button"`, text `⠿`, and accessible name `${card.name} 순서 변경`. Its `draggable` value is `!state.searchQuery.trim()`, and its title is `검색 중에는 순서를 변경할 수 없습니다.` while disabled.

Track only transient `draggedCardId`. On handle `dragstart`, store the ID in `dataTransfer`, set `effectAllowed = "move"`, and add `.dragging`. On each target row `dragover`, call `preventDefault`, compare `clientY` with the row midpoint, and show exactly one of `.drop-before` or `.drop-after`. On `drop`, call:

```js
persistRegular(core.reorderCards(state, draggedCardId, card.id, placement));
draggedCardId = "";
renderList();
```

On `dragend` and after every drop, clear all transient classes. Do not make the checkbox or card-open button draggable. Do nothing if search is non-empty, the IDs are equal, or an ID is missing.

- [ ] **Step 5: Run automated verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
node -e 'const fs=require("fs"); const h=fs.readFileSync("Tools/card-idea-notebook/index.html","utf8"); for(const m of h.matchAll(/<script(?: [^>]*)?>([\\s\\S]*?)<\\/script>/g)) new Function(m[1]); console.log("scripts parse")'
git diff --check
```

Expected: all Node tests pass, both scripts parse, and no whitespace errors exist.

- [ ] **Step 6: Run browser GREEN smoke verification**

Verify in the real browser:

1. Lower default filename survives reload.
2. The dialog starts with the lower default, but a one-off edit does not change that default.
3. Names with and without `.md` download as Markdown; an empty dialog value downloads with the dated fallback.
4. Handles alone start dragging; checkbox selection and card opening still work.
5. Dropping before and after changes the visible order and survives reload.
6. Active card and checked cards survive a reorder.
7. Exported cards follow the reordered list.
8. A non-empty search disables every handle and displays the exact explanation.
9. Browser console error logs are empty.

- [ ] **Step 7: Commit Task 3**

```bash
git add Tools/card-idea-notebook/index.html
git commit -m "feat(tools): add card export and ordering controls"
```

### Task 4: Documentation Lifecycle and Final Review

**Files:**
- Modify: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`
- Move: `docs/superpowers/plans/2026-07-28-card-notebook-export-ordering.md` to `docs/superpowers/archive/plans/2026-07-28-card-notebook-export-ordering.md`

**Interfaces:**
- Consumes: the verified schema 4 and UI behavior from Tasks 1–3.
- Produces: an implemented current spec, archived plan record, clean reviewed feature branch.

- [ ] **Step 1: Mark the design implemented and archive this plan**

Change the spec status to `current — 기본 파일명·드래그 순서 구현 완료`. Remove this plan from active plans, add `[카드 노트 파일명과 순서](plans/2026-07-28-card-notebook-export-ordering.md)` to the archive's external card authoring section, move the plan to `archive/plans/`, mark every checkbox complete, and append an `Implementation Result` section listing the exact verification evidence.

- [ ] **Step 2: Request independent code review**

Use `superpowers:requesting-code-review` against the implementation branch. The reviewer must check schema migration, filename persistence boundaries, empty filename fallback, drag insertion direction, active/selection preservation, filtered-search disabling, browser event cleanup, and documentation lifecycle. Fix every Critical or Important finding through a new failing test where behavior changes.

- [ ] **Step 3: Run final verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
node -e 'const fs=require("fs"); const h=fs.readFileSync("Tools/card-idea-notebook/index.html","utf8"); let n=0; for(const m of h.matchAll(/<script(?: [^>]*)?>([\\s\\S]*?)<\\/script>/g)){ new Function(m[1]); n++; } if(n!==2) throw new Error(`expected 2 scripts, got ${n}`); console.log("2 scripts parse")'
git diff --check
git status --short
```

Expected: all tests pass, exactly two scripts parse, no whitespace errors exist, and only intended implementation/archive changes are present before the final commit.

- [ ] **Step 4: Commit Task 4**

```bash
git add docs/superpowers
git commit -m "docs: complete card export ordering implementation"
```
