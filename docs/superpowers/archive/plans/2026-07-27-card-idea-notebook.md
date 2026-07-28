# Card Idea Notebook Implementation Plan

- **Status:** Completed 2026-07-27
- **Result:** `Tools/card-idea-notebook/index.html` 단일 파일 도구와 17개 자동화 테스트를 완성했다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Build one self-contained HTML file that stores card ideas explicitly in the browser and exports selected saved cards as AI-readable Markdown.

**Architecture:** `Tools/card-idea-notebook/index.html` contains all markup, CSS, pure card/Markdown/storage functions, and DOM bindings. Pure functions are exposed through `globalThis.CardIdeaNotebook` so Node's built-in test runner can extract the embedded core script and verify behavior without a browser dependency.

**Tech Stack:** HTML5, CSS, vanilla JavaScript, browser `localStorage`, browser `Blob`, Node 18+ built-in `node:test`.

## Global Constraints

- The user-facing tool is exactly one self-contained HTML file with no build step, server, framework, or external package.
- Saving is explicit through the save button or `Ctrl+S`/`Cmd+S`; input changes must never auto-save.
- Only saved card versions are exported.
- Card abilities are unrestricted free text split into enemy, ally, and no-target groups.
- Unit target ranges are `none`, `frontOne`, `frontTwo`, `backOne`, `backTwo`, and `all`.
- The tool never writes Unity or repository data directly.

---

### Task 1: Pure card model, validation, and Markdown

**Files:**
- Create: `Tools/card-idea-notebook/index.test.mjs`
- Create: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Produces: `globalThis.CardIdeaNotebook` with `emptyCard`, `normalizeCard`, `validateCard`, `targetSummary`, `cardMarkdown`, and `bundleMarkdown`.
- Consumes: no earlier task interfaces.

- [x] **Step 1: Write the failing core behavior tests**

Create a Node test loader that reads `index.html`, extracts the script marked `data-card-idea-core`, evaluates it in a VM context, and asserts literal Markdown for enemy/ally/none target combinations, omitted blank metadata, warning/error separation, and multiple-card bundles.

```js
test("renders facing ally and enemy ranges in Markdown", () => {
  const card = core.normalizeCard({
    name: "맹독 찌르기",
    role: "execution",
    cost: "1",
    executionOrder: "4",
    targets: { ally: "backOne", enemy: "frontTwo" },
    abilities: { ally: ["방어 4."], enemy: ["피해 5."], none: ["카드 1장 뽑기."] }
  });
  assert.match(
    core.cardMarkdown(card),
    /아군 뒤 하나 `◆━━━━` │ `◆◆━━━` 적군 앞 둘/
  );
});
```

- [x] **Step 2: Run the tests and verify RED**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: FAIL because `Tools/card-idea-notebook/index.html` does not exist.

- [x] **Step 3: Implement the minimal pure core inside the HTML**

Add the HTML shell and a `<script data-card-idea-core>` containing immutable normalization, validation, target glyph maps, card Markdown, bundle Markdown, and dirty comparison functions. Expose them as `globalThis.CardIdeaNotebook`.

- [x] **Step 4: Run the core tests and verify GREEN**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: all Task 1 tests PASS.

- [x] **Step 5: Commit Task 1**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add card idea markdown core"
```

### Task 2: Explicit local storage and editor state

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: Task 1 `normalizeCard`, `validateCard`, and Markdown functions.
- Produces: `readStore(storage)`, `writeStore(storage, state)`, `isDirty(saved, draft)`, `cardsForExport(state)`, and explicit state transitions for save, select, duplicate, delete, and export.

- [x] **Step 1: Write failing state and storage tests**

Add tests proving that typing does not call storage, explicit save persists schema version 1, unknown schema versions throw, unsaved changes are detected, and export reads saved cards rather than the draft.

```js
test("export uses the saved card rather than the unsaved draft", () => {
  const state = {
    cards: [{ ...core.emptyCard(), id: "a", name: "저장본" }],
    draft: { ...core.emptyCard(), id: "a", name: "미저장본" },
    exportSelection: ["a"]
  };
  assert.equal(core.cardsForExport(state)[0].name, "저장본");
});
```

- [x] **Step 2: Run the tests and verify RED**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: FAIL because the state/storage functions do not exist.

- [x] **Step 3: Implement explicit storage and state transitions**

Implement schema-versioned storage, explicit save, dirty comparison, selected saved-card export, duplicate IDs, deletion, and search filtering. Do not attach input events to storage writes.

- [x] **Step 4: Run the tests and verify GREEN**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: all Task 1–2 tests PASS.

- [x] **Step 5: Commit Task 2**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add explicit card draft storage"
```

### Task 3: Three-column browser UI and Markdown download

**Files:**
- Modify: `Tools/card-idea-notebook/index.html`
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md`
- Move after completion: `docs/superpowers/plans/2026-07-27-card-idea-notebook.md` to `docs/superpowers/archive/plans/2026-07-27-card-idea-notebook.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`

**Interfaces:**
- Consumes: Tasks 1–2 core and state interfaces.
- Produces: a complete no-build browser tool at `Tools/card-idea-notebook/index.html`.

- [x] **Step 1: Write failing UI-state tests**

Add pure transition tests for unsaved select/new/duplicate/delete decisions, export blocking when no cards are selected, and `saveThenExport` behavior when the selected current card is dirty.

- [x] **Step 2: Run the tests and verify RED**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: FAIL because the UI-state transition functions do not exist.

- [x] **Step 3: Implement the full self-contained UI**

Build the three-column interface, all fields from the design spec, explicit save and keyboard shortcut, dirty badge, save/discard/cancel dialog, delete confirmation, name/tag search, export checkboxes, live Markdown source preview, validation messages, and Blob download. Keep CSS and JavaScript embedded in `index.html`.

- [x] **Step 4: Run automated tests**

Run: `node --test Tools/card-idea-notebook/index.test.mjs`

Expected: all tests PASS with zero failures.

- [x] **Step 5: Run browser smoke verification**

Serve the worktree locally with `python3 -m http.server 8765` and verify in a browser:

1. Create and explicitly save a card.
2. Reload and confirm it remains.
3. Edit without saving and confirm navigation asks save/discard/cancel.
4. Save a dual-faction card and confirm facing range Markdown.
5. Select one and multiple cards and confirm `.md` download.
6. Confirm the console has no errors.

- [x] **Step 6: Update and archive documentation**

Revise the design spec to state the self-contained single-file implementation, archive this completed plan, and update both indexes in the same commit.

- [x] **Step 7: Run final verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
git diff --check
git status --short
```

Expected: all tests PASS, no whitespace errors, and only intended files changed.

- [x] **Step 8: Commit Task 3**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs docs/superpowers
git commit -m "feat(tools): complete card idea notebook"
```
