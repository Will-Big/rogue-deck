# Card Notebook Faction and Explicit Save Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Distinguish ally and enemy cards, enforce faction-specific cost and role rules, and make current-card and all-card saves recompute completion instead of forcing completion.

**Architecture:** Add a `faction` discriminator to the existing pure card model and keep enemy invariants in normalization so storage, Markdown, and UI share one source of truth. Replace unconditional completion with pure completion predicates used by current-card save, all-card save, schema migration, and Markdown import. Keep draft auto-preservation separate from explicit completion-state saves.

**Tech Stack:** HTML5, CSS, vanilla JavaScript, browser `localStorage`, Node 18+ built-in `node:test`, in-app browser smoke verification.

## Global Constraints

- Keep the tool self-contained in `Tools/card-idea-notebook/index.html`.
- Add no package, framework, server dependency, Unity data, or game code.
- Use UI label `진영` with values `아군` and `적군`.
- New cards and schema 1–4 cards default to `faction: "ally"`.
- Enemy cards normalize to `role: "execution"` and empty stored cost; show the cost as `없음`.
- Switching from enemy back to ally resets cost to empty and role to `unknown`.
- Completion requires name and faction; ally cost and decided role; and execution order for every execution card.
- Tags, targets, abilities, and notes remain optional.
- Draft edits remain immediately preserved and make a complete card incomplete.
- `Ctrl/Cmd+S` saves the active card; `Ctrl/Cmd+Shift+S` saves all cards.
- Keep existing selection, ordering, filename, targeting, deletion, and storage-failure behavior.

---

### Task 1: Faction Model, Completion Rules, and Schema 5

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Produces `FACTION_LABELS`, `normalizeCard(input)`, `isCardComplete(input)`, `saveCard(state, id, options)`, `saveAllCards(state, options)`, and schema version `5`.
- Preserves `validateCard(input)` for structural errors and target/ability warnings only.

- [x] **Step 1: Add failing faction and save tests**

Add tests that assert:

```js
assert.equal(core.emptyCard().faction, "ally");
assert.deepEqual(
  { faction: enemy.faction, role: enemy.role, cost: enemy.cost },
  { faction: "enemy", role: "execution", cost: "" },
);
assert.equal(core.isCardComplete(allyWithoutCost), false);
assert.equal(core.isCardComplete(completeAlly), true);
assert.equal(core.isCardComplete(enemyWithoutOrder), false);
assert.equal(core.isCardComplete(completeEnemy), true);
assert.equal(core.saveCard(state, "ally").cards[0].completionStatus, "complete");
assert.deepEqual(
  core.saveAllCards(state).cards.map((card) => card.completionStatus),
  ["complete", "incomplete"],
);
```

Also assert that missing cost and execution order produce no warning messages.

- [x] **Step 2: Add failing schema migration tests**

Write schema 4 storage containing a formerly complete card without faction and assert:

```js
assert.equal(migrated.schemaVersion, 5);
assert.equal(migrated.cards[0].faction, "ally");
assert.equal(migrated.cards[0].completionStatus, "incomplete");
assert.deepEqual(migrated.selection, ["a"]);
assert.equal(migrated.activeCardId, "a");
```

Assert current schema 5 round-trips ally and enemy cards, and unknown schemas remain rejected.

- [x] **Step 3: Run Task 1 tests and verify RED**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: failures for missing faction APIs, schema 5, and unconditional completion.

- [x] **Step 4: Implement Task 1 minimally**

Add:

```js
const FACTION_LABELS = Object.freeze({ ally: "아군", enemy: "적군" });
```

Normalize enemy cards to execution with empty cost. Implement one `isCardComplete` predicate and use it in `saveCard`, `saveAllCards`, and schema 1–4 migration. Remove blank cost and blank execution-order warnings while preserving structural errors and target/ability warnings.

- [x] **Step 5: Run Task 1 tests and verify GREEN**

Run the Node suite and expect all tests to pass.

- [x] **Step 6: Commit Task 1**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add card factions and save rules"
```

### Task 2: Faction Markdown and Legacy Import

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes Task 1 faction normalization and completion predicate.
- Produces strict Markdown metadata order `진영`, `비용`, `역할`, `실행순서`, `태그`, `대상`.

- [x] **Step 1: Add failing Markdown tests**

Assert ally and enemy output:

```markdown
- 진영: 아군
- 비용: 1
- 역할: 실행
```

```markdown
- 진영: 적군
- 비용: 없음
- 역할: 실행
```

Round-trip both factions and assert an older card section without `진영` imports as ally. Assert an old structurally valid card missing new completion fields imports as incomplete rather than aborting the bundle.

- [x] **Step 2: Run Task 2 tests and verify RED**

Run the Node suite. Expected: Markdown lacks faction, enemy cost is omitted, and legacy import does not apply the new fallback.

- [x] **Step 3: Implement Task 2 minimally**

Always emit faction. Emit enemy cost as `없음`, parse it only for enemy cards, default omitted faction to ally, and derive imported completion with `isCardComplete`. Preserve atomic rejection for malformed structure and simultaneous ally/enemy self.

- [x] **Step 4: Run Task 2 tests and verify GREEN**

Run the full Node suite and the exact two-inline-script syntax check.

- [x] **Step 5: Commit Task 2**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): round-trip card factions in Markdown"
```

### Task 3: Faction UI, Current/All Save, and Shortcuts

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes Task 1 `FACTION_LABELS`, `saveCard`, and `saveAllCards`.
- Produces `#card-faction`, `#save-all-cards`, faction list pills, and keyboard shortcuts.

- [x] **Step 1: Add the faction and all-save controls**

Place fields in this order: card name, faction, cost, role, execution order, tags. Keep cost and role visible. For enemy cards display cost `없음`, role `실행`, and disable both controls. Add `전체 저장` beside the existing `저장` button.

- [x] **Step 2: Wire faction transitions and rendering**

On faction change, reset enemy→ally to blank cost and unknown role; enforce enemy execution/empty cost through normalization. Show an `아군` or `적군` pill in every list row.

- [x] **Step 3: Wire current/all save behavior**

Current save recomputes only the active card. All save recomputes every card, persists once, preserves active/selection/order, and reports complete/incomplete counts. Neither action throws merely because core fields are blank.

- [x] **Step 4: Wire keyboard shortcuts**

Handle `Ctrl/Cmd+S` and `Ctrl/Cmd+Shift+S` at `window`, prevent the browser default, and dispatch current or all save even while an input or textarea has focus.

- [x] **Step 5: Run automated verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
node -e 'const fs=require("fs"); const h=fs.readFileSync("Tools/card-idea-notebook/index.html","utf8"); let n=0; for(const m of h.matchAll(/<script(?: [^>]*)?>([\s\S]*?)<\/script>/g)){ new Function(m[1]); n++; } if(n!==2) throw new Error(`expected 2 scripts, got ${n}`); console.log("2 scripts parse")'
git diff --check
```

- [x] **Step 6: Run browser smoke verification**

Verify new-card ally default, ally/enemy field states, faction switching reset, current save, all save, both shortcuts, faction pills, schema 5 reload, Markdown preview, and empty console error logs.

- [x] **Step 7: Commit Task 3**

```bash
git add Tools/card-idea-notebook/index.html
git commit -m "feat(tools): add faction and save controls"
```

### Task 4: Review, Documentation Lifecycle, and Final Verification

**Files:**
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`
- Modify: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md`
- Move: `docs/superpowers/plans/2026-07-28-card-notebook-faction-save.md` to `docs/superpowers/archive/plans/2026-07-28-card-notebook-faction-save.md`

**Interfaces:**
- Produces reviewed code, an implemented current spec, an archived plan, and a clean feature branch.

- [x] **Step 1: Request independent code review**

Review faction invariants, completion rules, migration, legacy Markdown, current/all save preservation, shortcuts, and unchanged existing behavior. Fix every Critical or Important issue.

- [x] **Step 2: Update and archive documentation**

Mark the spec implemented, remove the active plan from the central index, add it to the archive index, move the plan, complete every checkbox, and append exact automated/browser evidence.

- [x] **Step 3: Run final verification**

Run the full Node suite, two-script syntax check, `git diff --check`, plan checkbox/link checks, and `git status --short`.

- [x] **Step 4: Commit Task 4**

```bash
git add docs/superpowers/README.md \
  docs/superpowers/archive/README.md \
  docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md \
  docs/superpowers/plans/2026-07-28-card-notebook-faction-save.md \
  docs/superpowers/archive/plans/2026-07-28-card-notebook-faction-save.md
git commit -m "docs: complete card faction and save implementation"
```

## Implementation Result

- 카드 모델과 UI에 `진영`을 추가하고 새 카드와 스키마 1~4 카드를 모두 아군으로 변환했다.
- 적군 카드는 내부 비용 빈 문자열, 표시·Markdown 비용 `없음`, 역할 `실행` 불변식을 공유한다.
- 핵심 정보의 완성 판정을 현재 카드 저장, 전체 저장, 구형 스키마 변환과 Markdown 불러오기에 함께
  적용했다. 아군 비용은 0 이상의 정수, 실행순서는 정수만 완성 조건을 충족한다.
- `저장`·`Ctrl/Cmd+S`는 현재 카드, `전체 저장`·`Ctrl/Cmd+Shift+S`는 모든 카드의 상태를 다시
  판정하며 초안 즉시 보존, 활성 카드, 선택과 순서를 유지한다.
- Markdown은 진영·비용·역할·실행순서 순서를 보존하고, 진영이 없는 구형 Markdown을 아군으로
  불러온다.
- Node 테스트 46개와 inline script 2개 구문 검사가 통과했다.
- 브라우저에서 아군 기본값, 적군 고정 필드, 아군 복귀 초기화, 개별·전체 저장 버튼과 단축키,
  진영 배지, 새로고침 보존, 빈 콘솔 오류 로그를 확인했다.
- 독립 리뷰에서 발견한 숫자 완성 판정 문제를 회귀 테스트와 함께 수정했고 재검토에서 남은
  Critical·Important 문제가 없음을 확인했다.
