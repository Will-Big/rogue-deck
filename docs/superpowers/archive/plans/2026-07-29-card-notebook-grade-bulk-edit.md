# Card Notebook Grade and Bulk Edit Implementation Plan

**Status:** Completed on 2026-07-29.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ally card grades, schema 6 migration, range/additive card selection, and field-aware bulk editing to the standalone card idea notebook.

**Architecture:** Keep `Tools/card-idea-notebook/index.html` self-contained and extend its pure JavaScript core before connecting DOM behavior. Grade normalization remains the single source of truth for storage and Markdown, while selection and bulk-edit helpers operate on immutable state and expose aggregate field values to the existing form. The UI continues to preview and explicitly save the active card even when the form edits multiple selected cards.

**Tech Stack:** HTML5, CSS, vanilla JavaScript, browser `localStorage`, Node 18+ built-in `node:test`, in-app browser smoke verification.

## Global Constraints

- Keep the tool self-contained in `Tools/card-idea-notebook/index.html`.
- Add no package, framework, server dependency, Unity data, or game code.
- Grade labels are exactly `없음`, `일반`, `고급`, `희귀`, `기타`.
- Grade storage values are exactly `none`, `common`, `advanced`, `rare`, `other`.
- New ally cards start at `common`; enemy cards always normalize to `none`.
- Every schema 1–5 card migrates to `grade: "none"` regardless of faction or any stray stored grade.
- Schema 1–4 faction migration and completion recalculation remain unchanged.
- Enemy-to-ally faction changes reset grade to `common`, cost to empty, and role to `unknown`.
- Grade does not participate in completion status.
- Plain row click replaces selection, `Shift+click` selects the visible anchor range, and `Ctrl/Cmd+click` toggles one card.
- A plain checkbox click toggles one card; checkbox modifier clicks use the same range and toggle rules.
- Range selection uses filtered visible order and replaces the previous selection.
- Selection persists in `selection[]`; the Shift anchor is session-only and initializes from the active card.
- A field is enabled when at least one edit target can change it and applies only to those compatible cards.
- Bulk changes mark only actually changed cards incomplete and trigger one `localStorage` write.
- `저장` and the Markdown preview continue to use the active card; `전체 저장` continues to use every card.

---

### Task 1: Grade Model, Schema 6, and Markdown

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Produces `GRADE_LABELS`, `validGrade(value)`, grade-aware `normalizeCard(input)`, grade-aware `changeCardFaction(input, faction)`, and schema version `6`.
- Preserves existing `isCardComplete(input)` behavior because grade is never a completion requirement.
- Produces Markdown metadata order `진영`, `등급`, `비용`, `역할`, `실행순서`, `태그`, `대상`.

- [x] **Step 1: Write failing grade normalization and faction-transition tests**

Add literal assertions that catch a missing or incorrect grade invariant:

```js
assert.equal(core.emptyCard().grade, "common");
assert.equal(core.normalizeCard({ faction: "ally", grade: "rare" }).grade, "rare");
assert.equal(core.normalizeCard({ faction: "enemy", grade: "rare" }).grade, "none");
assert.equal(core.normalizeCard({ faction: "ally", grade: "invalid" }).grade, "common");

const enemy = core.changeCardFaction({
  faction: "ally",
  grade: "advanced",
  role: "intervention",
  cost: "2",
}, "enemy");
assert.deepEqual(
  { faction: enemy.faction, grade: enemy.grade, role: enemy.role, cost: enemy.cost },
  { faction: "enemy", grade: "none", role: "execution", cost: "" },
);

const allyAgain = core.changeCardFaction(enemy, "ally");
assert.deepEqual(
  { faction: allyAgain.faction, grade: allyAgain.grade, role: allyAgain.role, cost: allyAgain.cost },
  { faction: "ally", grade: "common", role: "unknown", cost: "" },
);
```

- [x] **Step 2: Run the Node suite and verify RED**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: grade assertions fail because the model has no grade field or registry.

- [x] **Step 3: Implement the grade model minimally**

Add:

```js
const GRADE_LABELS = Object.freeze({
  none: "없음",
  common: "일반",
  advanced: "고급",
  rare: "희귀",
  other: "기타",
});

function validGrade(value) {
  return Object.hasOwn(GRADE_LABELS, value) ? value : "common";
}
```

Add `grade: "common"` to `emptyCard()`. In `normalizeCard`, force enemy cards to `none` and normalize ally values with `validGrade`. In `changeCardFaction`, set enemy grade to `none` and returning ally grade to `common`. Export `GRADE_LABELS`.

- [x] **Step 4: Run the Node suite and verify GREEN**

Run the same Node command. Expect all existing tests plus the new grade model test to pass.

- [x] **Step 5: Write failing schema 6 migration tests**

Cover schema 5 ally and enemy cards, a schema 3 card, and a current schema 6 card:

```js
assert.deepEqual(
  core.readStore(schemaFiveStorage).cards.map((card) => card.grade),
  ["none", "none"],
);
assert.equal(core.readStore(schemaThreeStorage).cards[0].grade, "none");
assert.equal(core.readStore(schemaSixStorage).cards[0].grade, "rare");
assert.equal(core.readStore(schemaSixStorage).schemaVersion, 6);
```

Also update existing current-schema expectations from `5` to `6`, while retaining schema 1–4 ally migration and selection preservation assertions.

- [x] **Step 6: Run the Node suite and verify schema RED**

Expected: failures show schema 5 is still current and prior cards do not receive the required `none` migration.

- [x] **Step 7: Implement schema 6 migration**

Set `SCHEMA_VERSION = 6`. Accept versions 1 through 6. Use separate migration decisions:

```js
const needsFactionMigration = saved.schemaVersion <= 4;
const needsGradeMigration = saved.schemaVersion <= 5;
```

Normalize each legacy card with forced ally faction only when `needsFactionMigration`, and forced `grade: "none"` when `needsGradeMigration`. Recalculate completion status only for schema 1–4. Preserve schema 5 faction, completion, card order, active card, selection, search query, and filename.

- [x] **Step 8: Run the Node suite and verify schema GREEN**

Expect schema 1–6 coverage to pass and schema 99 to remain rejected.

- [x] **Step 9: Write failing grade Markdown tests**

Assert literal metadata and legacy behavior:

```js
assert.match(core.cardMarkdown(rareAlly), /- 진영: 아군\n- 등급: 희귀\n- 비용: 1/);
assert.match(core.cardMarkdown(enemy), /- 진영: 적군\n- 등급: 없음\n- 비용: 없음/);
assert.equal(
  core.parseBundleMarkdown(core.bundleMarkdown([rareAlly], "2026-07-29"))[0].grade,
  "rare",
);
assert.equal(core.parseBundleMarkdown(legacyWithoutGrade)[0].grade, "none");
assert.throws(() => core.parseBundleMarkdown(markdownWithUnknownGrade), /알 수 없는 등급/);
```

- [x] **Step 10: Run the Node suite and verify Markdown RED**

Expected: output lacks `등급`, round-trip loses it, and the parser rejects `등급` as unknown metadata.

- [x] **Step 11: Implement grade Markdown**

Write `- 등급: ${GRADE_LABELS[card.grade]}` immediately after faction. Parse an optional grade line, defaulting omitted legacy grade to `none`; reject labels outside `GRADE_LABELS`. Enemy normalization still forces `none`.

- [x] **Step 12: Run the Node suite and commit Task 1**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add card grades and schema 6"
```

Expected: all tests pass.

### Task 2: Range and Additive Selection

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Produces `selectCard(state, visibleIds, cardId, mode, anchorId)`.
- `mode` is one of `replace`, `toggle`, `range`.
- Returns `{ state, anchorId }`; `state.selection` and `state.activeCardId` are updated, while the anchor remains outside persisted state.
- Preserves `bulkSelection(state, checked)` and existing delete/export semantics.

- [x] **Step 1: Write failing pure selection tests**

Use card order `["a", "b", "c", "d", "e"]` and assert:

```js
const replaced = core.selectCard(state, ["a", "b", "c", "d", "e"], "b", "replace", "a");
assert.deepEqual([...replaced.state.selection], ["b"]);
assert.equal(replaced.state.activeCardId, "b");
assert.equal(replaced.anchorId, "b");

const toggled = core.selectCard(replaced.state, ["a", "b", "c", "d", "e"], "d", "toggle", "b");
assert.deepEqual([...toggled.state.selection], ["b", "d"]);
assert.equal(toggled.state.activeCardId, "d");

const ranged = core.selectCard(toggled.state, ["b", "d", "e"], "e", "range", "b");
assert.deepEqual([...ranged.state.selection], ["b", "d", "e"]);
assert.equal(ranged.anchorId, "b");
```

Add cases for reverse range, missing anchor fallback to active card, toggling the active card off with remaining selection, and toggling the last selected card off while keeping it active.
Also assert that creating or duplicating a card selects only the new card, and importing a bundle selects only its first
new card, so the newly activated card is immediately the form's edit target.

- [x] **Step 2: Run the Node suite and verify RED**

Expected: `core.selectCard` is missing.

- [x] **Step 3: Implement immutable selection calculation**

Implement `selectCard` using only IDs present in `state.cards`. For `range`, find anchor and clicked indexes in
`visibleIds`, replace selection with the inclusive slice, and keep the prior anchor. For `toggle`, preserve card-list
order in the resulting selection. If the active card is removed and selection remains, choose the first selected card in
card-list order; if selection becomes empty, keep the clicked card active. Update `createCard`, `duplicateCard`, and
`importCards` to set `selection` to the newly activated card ID.

- [x] **Step 4: Run the Node suite and verify GREEN**

Expect all selection and prior delete/export tests to pass.

- [x] **Step 5: Commit Task 2**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add range card selection"
```

### Task 3: Aggregate Values and Field-Aware Bulk Editing

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Produces `editTargetCards(state)`, `fieldAggregate(state, field)`, `isFieldApplicable(card, field)`, and `editSelectedField(state, field, value)`.
- Field names are `name`, `faction`, `grade`, `cost`, `role`, `executionOrder`, `tags`, `targets.ally`, `targets.enemy`, `abilities.ally`, `abilities.enemy`, `abilities.none`, and `notes`.
- `fieldAggregate` returns `{ kind: "empty" | "common" | "mixed", value, applicableCount }`.
- Array-backed form values use normalized newline/comma strings so equality and DOM display are deterministic.

- [x] **Step 1: Write failing edit-target and aggregate tests**

Assert that nonempty selection wins over the active card, empty selection falls back to active, and inapplicable cards are ignored:

```js
assert.deepEqual(
  core.editTargetCards({ ...state, selection: ["ally-a", "enemy"] }).map((card) => card.id),
  ["ally-a", "enemy"],
);
assert.deepEqual(
  core.editTargetCards({ ...state, selection: [], activeCardId: "ally-b" }).map((card) => card.id),
  ["ally-b"],
);
assert.deepEqual(core.fieldAggregate(state, "grade"), {
  kind: "mixed",
  value: "",
  applicableCount: 2,
});
assert.deepEqual(core.fieldAggregate(enemyOnlyState, "grade"), {
  kind: "empty",
  value: "",
  applicableCount: 0,
});
```

Add common-value assertions for text, select, tag, target, ability, and note fields. The production break caught is accidental inclusion of enemy values in ally-only fields or incorrect array equality.

- [x] **Step 2: Run the Node suite and verify aggregate RED**

Expected: aggregate APIs are missing.

- [x] **Step 3: Implement applicability and aggregate helpers**

Apply:

```text
all cards: name, faction, tags, both targets, all abilities, notes
ally only: grade, cost, role
execution only: executionOrder
```

Serialize tags as `", "` joined text and abilities as newline-joined text. Compare normalized primitive strings. Return `empty` when no edit target is applicable.

- [x] **Step 4: Run the Node suite and verify aggregate GREEN**

Expect aggregate tests and all previous tests to pass.

- [x] **Step 5: Write failing bulk-edit tests**

Build mixed ally/enemy selections and assert:

```js
const graded = core.editSelectedField(state, "grade", "rare");
assert.equal(graded.cards.find((card) => card.id === "ally-a").grade, "rare");
assert.equal(graded.cards.find((card) => card.id === "enemy").grade, "none");

const renamed = core.editSelectedField(state, "name", "같은 이름");
assert.deepEqual(renamed.cards.map((card) => card.name), ["같은 이름", "같은 이름", "같은 이름"]);

const factionChanged = core.editSelectedField(state, "faction", "enemy");
assert.deepEqual(
  factionChanged.cards.map(({ faction, grade, role, cost }) => ({ faction, grade, role, cost })),
  [
    { faction: "enemy", grade: "none", role: "execution", cost: "" },
    { faction: "enemy", grade: "none", role: "execution", cost: "" },
    { faction: "enemy", grade: "none", role: "execution", cost: "" },
  ],
);
```

Also assert execution order changes only execution cards; unchanged values preserve completion status and object identity; changed cards alone become `incomplete`; nested targets and abilities retain their sibling values.

- [x] **Step 6: Run the Node suite and verify bulk-edit RED**

Expected: `editSelectedField` is missing.

- [x] **Step 7: Implement bulk editing minimally**

Map cards once, apply only to IDs returned by `editTargetCards`, skip incompatible cards, and use `changeCardFaction` for faction edits. For nested target and ability fields, replace only the named child. Normalize with `tagList`, `textLines`, `validGrade`, `validRole`, or `validTarget` as appropriate. Compare the normalized field value before marking a card `incomplete`; return the original state when no card changes.

- [x] **Step 8: Run the Node suite and verify bulk-edit GREEN**

Expected: all core behavior passes without DOM involvement.

- [x] **Step 9: Commit Task 3**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add field-aware bulk editing"
```

### Task 4: Grade and Multi-Edit User Interface

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes Task 1 `GRADE_LABELS`, Task 2 `selectCard`, and Task 3 aggregate/edit helpers.
- Produces a visible grade field and list pill, session-only `selectionAnchorId`, mixed form presentation, and single-write bulk event handling.
- Keeps Markdown preview, completion badge, validation, and `저장` bound to `activeCardId`.

- [x] **Step 1: Write failing HTML contract tests**

Read `index.html` as text and assert user-facing form contracts that would break if grade or mixed editing were omitted:

```js
assert.match(html, /id="card-grade"/);
assert.match(html, /data-card-field="grade"/);
assert.match(html, />없음<\/option>/);
assert.match(html, />일반<\/option>/);
assert.match(html, />고급<\/option>/);
assert.match(html, />희귀<\/option>/);
assert.match(html, />기타<\/option>/);
```

Keep these assertions limited to the static accessibility/authoring contract; behavioral selection and editing remain covered by the real pure functions.

- [x] **Step 2: Run the Node suite and verify UI RED**

Expected: the grade control and field mapping are absent.

- [x] **Step 3: Add grade markup, styling, and element binding**

Insert grade between faction and cost with all five options. Add `grade` to the element registry and `cardFromForm`. Add `.grade-pill` styling compatible with existing faction/role/completion pills and render the grade label in each card row.

- [x] **Step 4: Connect row and checkbox selection**

Initialize:

```js
let selectionAnchorId = state.activeCardId;
```

Derive click mode from `event.shiftKey` and `event.ctrlKey || event.metaKey`. Row plain click uses `replace`; checkbox
plain click and Ctrl/Cmd use `toggle`; Shift uses `range`. Handle checkbox selection from one click listener and prevent
its default toggle so a following `change` event cannot apply the selection twice. Pass
`core.filteredCards(...).map(card => card.id)` as visible order, assign the returned anchor, persist returned state once,
and call `renderAll`.

- [x] **Step 5: Render aggregate form values**

For each mapped field, call `core.fieldAggregate`. Show common values normally. For mixed select fields, insert/select a
disabled transient `__mixed` option labelled `혼합`; for mixed input/textarea fields, show an empty value with
placeholder `혼합 값`. Disable a control only when `applicableCount === 0` or no card exists. For an enemy-only
selection, explicitly display the normalized fixed values `없음`, `없음`, and `실행` for grade, cost, and role while
leaving those controls disabled. A mixed ally/enemy selection enables those fields and displays aggregates computed from
the allies only.

Render the editor title as `<N>장 선택` when more than one card is selected. Keep the Markdown preview, completion badge, validation messages, target summary fallback, and save action based on the active card rather than aggregate values.

- [x] **Step 6: Route form events through one field edit**

Use each control's `data-card-field` to call:

```js
const nextState = core.editSelectedField(state, field, control.value);
if (nextState !== state) persistRegular(nextState);
renderAll();
```

Ignore `__mixed`. This produces one persistence attempt per user event and allows faction changes to re-render all newly applicable fields immediately.

- [x] **Step 7: Run the complete Node suite**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: all tests pass with no warnings or errors.

- [ ] **Step 8: Perform in-app browser verification — blocked by browser security policy**

Open `Tools/card-idea-notebook/index.html` and verify:

1. New ally grade is `일반`; changing to enemy shows disabled `없음`; returning to ally shows `일반`.
2. Plain click selects one card, Ctrl/Cmd toggles cards, and Shift selects an inclusive visible range.
3. Search-filtered Shift selection never selects hidden cards.
4. Mixed fields show `혼합` or `혼합 값`; common fields show their shared value.
5. Mixed ally/enemy grade, cost, and role remain enabled for allies and leave enemies unchanged.
6. Execution order changes only execution cards.
7. One bulk edit marks only changed cards incomplete and survives reload.
8. Active-card Markdown preview and `저장` remain single-card operations.
9. List grade pills, delete, export, drag ordering, and current/all save shortcuts still work.

- [x] **Step 9: Commit Task 4**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add grade and bulk edit UI"
```

### Task 5: Final Verification and Documentation Closure

**Files:**
- Modify: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md`
- Move: `docs/superpowers/plans/2026-07-29-card-notebook-grade-bulk-edit.md`
  to `docs/superpowers/archive/plans/2026-07-29-card-notebook-grade-bulk-edit.md`
- Modify: `docs/superpowers/README.md`

**Interfaces:**
- Produces a clean, verified feature branch with the plan archived and the design marked implemented.

- [x] **Step 1: Run final automated verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
git diff --check
git status --short
```

Expected: all Node tests pass, `git diff --check` prints nothing, and status contains only expected documentation closure changes after the next step.

- [x] **Step 2: Close the current documents**

Change the design status to schema 6 grade and multi-edit implementation complete. Move this plan into `docs/superpowers/archive/plans/`, remove its active-plan row from `docs/superpowers/README.md`, and add it to the archived implementation-plan index following the existing ordering.

- [x] **Step 3: Verify document links and commit**

Run:

```bash
rg -n "2026-07-29-card-notebook-grade-bulk-edit" docs/superpowers
git diff --check
git add docs/superpowers/README.md \
  docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md \
  docs/superpowers/plans/2026-07-29-card-notebook-grade-bulk-edit.md \
  docs/superpowers/archive/plans/2026-07-29-card-notebook-grade-bulk-edit.md
git commit -m "docs: complete card grade and bulk edit work"
```

Expected: references point only to the archived plan, and the commit succeeds.

- [x] **Step 4: Run final branch verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
git diff --check
git status --short
```

Expected: the complete suite passes and the worktree is clean.

## Implementation Result

- `3a79f71` added grade normalization, schema 6 migration, and grade-aware Markdown.
- `bb1cca9` added plain, additive, and visible-range card selection.
- `2f79374` added aggregate values and field-aware bulk editing.
- `77f9c78` connected grade and bulk editing to the browser UI.
- `48bf4e0` aligned loaded active cards with preserved selections to prevent editing a hidden target.
- `node --test Tools/card-idea-notebook/index.test.mjs`: 54 passed, 0 failed.
- Both inline scripts parsed successfully with `node:vm`; `git diff --check` reported no errors.
- In-app browser navigation to the local `file://` page was blocked by browser security policy, so interactive browser
  verification could not be repeated in this session.
