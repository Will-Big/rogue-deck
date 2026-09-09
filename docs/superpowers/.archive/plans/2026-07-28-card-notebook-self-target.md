# Card Notebook Self Target Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the `자신` target with the `◎` marker to both faction selectors, preserve it through Markdown export/import, reject cards that select self for both factions, and rename the export button to `Markdown 내보내기`.

**Architecture:** Extend the existing pure `TARGETS` registry with one additive `self` value so normalization, selector population, rendering, and strict Markdown parsing continue to share one source of truth. Keep schema version 4 because the stored shape is unchanged. Add one cross-field validation rule for the impossible two-self combination and verify the visible button copy separately from the core behavior.

**Tech Stack:** HTML5, CSS, vanilla JavaScript, browser `localStorage`, Node 18+ built-in `node:test`, in-app browser smoke verification.

## Global Constraints

- Keep the user-facing tool self-contained in `Tools/card-idea-notebook/index.html`; add no runtime dependency, framework, or server requirement.
- Use `◎` without a formation rail for `자신`.
- Allow `자신` in both `targets.ally` and `targets.enemy`, but reject a card whose two target fields are both `self`.
- Resolve `자신` as the current card user; player legacy cards use the current inheritor and enemy execution cards use the executing enemy.
- Keep storage schema version `4`; preserve all existing schema 1–3 migrations.
- Keep every existing range marker, Markdown format, card state transition, selection, ordering, and filename behavior unchanged.
- Rename only the visible button copy from `AI용 Markdown 내보내기` to `Markdown 내보내기`.
- Do not modify Unity, ScriptableObject, or game data.

---

### Task 1: Self Target Core and Markdown Round Trip

**Files:**
- Modify: `Tools/card-idea-notebook/index.test.mjs`
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: existing `TARGETS`, `normalizeCard(input)`, `validateCard(input)`, `targetSummary(input)`, `bundleMarkdown(inputs, date)`, and `parseBundleMarkdown(markdown)`.
- Produces:
  - `TARGETS.self = { label: "자신", ally: "◎", enemy: "◎" }`
  - blocking validation message `아군과 적군에 자신을 동시에 지정할 수 없습니다.`
  - strict Markdown forms `아군 자신 \`◎\`` and `\`◎\` 적군 자신`.

- [x] **Step 1: Write failing self-marker and validation tests**

Add these tests after the existing facing-range test:

```js
test("renders self for either faction and rejects two self targets", () => {
  const core = loadCore();
  const allySelf = core.normalizeCard({
    name: "자기 방어",
    targets: { ally: "self", enemy: "frontOne" },
    abilities: { ally: ["방어 4."], enemy: ["피해 2."] },
  });
  const enemySelf = core.normalizeCard({
    name: "적의 태세",
    targets: { ally: "backOne", enemy: "self" },
    abilities: { ally: ["약화 1."], enemy: ["방어 4."] },
  });
  const twoSelf = core.normalizeCard({
    name: "잘못된 카드",
    targets: { ally: "self", enemy: "self" },
    abilities: { ally: ["방어 1."], enemy: ["방어 1."] },
  });

  assert.equal(core.TARGETS.self.label, "자신");
  assert.equal(
    core.targetSummary(allySelf),
    "아군 자신 `◎` │ `◆━━━━` 적군 앞 하나",
  );
  assert.equal(
    core.targetSummary(enemySelf),
    "아군 뒤 하나 `◆━━━━` │ `◎` 적군 자신",
  );
  assert.deepEqual([...core.validateCard(twoSelf).errors], [
    "아군과 적군에 자신을 동시에 지정할 수 없습니다.",
  ]);

  const state = {
    ...core.initialState(),
    cards: [{ ...twoSelf, id: "two-self" }],
    activeCardId: "two-self",
  };
  assert.throws(
    () => core.completeCard(state, "two-self"),
    /아군과 적군에 자신을 동시에 지정할 수 없습니다/,
  );
});
```

- [x] **Step 2: Write failing strict Markdown round-trip tests**

Add:

```js
test("round-trips ally and enemy self targets through strict Markdown", () => {
  const core = loadCore();
  const source = [
    core.normalizeCard({
      name: "계승자의 방어",
      role: "execution",
      targets: { ally: "self", enemy: "frontOne" },
      abilities: { ally: ["방어 4."], enemy: ["피해 2."] },
      completionStatus: "complete",
    }),
    core.normalizeCard({
      name: "적의 자기 강화",
      role: "execution",
      targets: { ally: "backOne", enemy: "self" },
      abilities: { ally: ["약화 1."], enemy: ["공격 3."] },
      completionStatus: "complete",
    }),
  ];

  const parsed = core.parseBundleMarkdown(
    core.bundleMarkdown(source, "2026-07-28"),
  );
  assert.deepEqual(
    [...parsed.map((card) => [card.targets.ally, card.targets.enemy])],
    [["self", "frontOne"], ["backOne", "self"]],
  );

  const invalid = core.bundleMarkdown([
    core.normalizeCard({
      name: "양쪽 자신",
      role: "execution",
      targets: { ally: "self", enemy: "self" },
      abilities: { ally: ["방어 1."], enemy: ["방어 1."] },
      completionStatus: "complete",
    }),
  ], "2026-07-28");
  assert.throws(
    () => core.parseBundleMarkdown(invalid),
    /아군과 적군에 자신을 동시에 지정할 수 없습니다/,
  );
});
```

- [x] **Step 3: Run Task 1 tests and verify RED**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: the self-marker test fails because `self` normalizes to `none`, the round-trip assertion returns `none`, and the two-self validation error is absent.

- [x] **Step 4: Implement the self target and cross-field validation**

Add `self` to `TARGETS` immediately after `none`:

```js
self: Object.freeze({ label: "자신", ally: "◎", enemy: "◎" }),
```

In `validateCard`, immediately after the card-name error, add:

```js
if (card.targets.ally === "self" && card.targets.enemy === "self") {
  errors.push("아군과 적군에 자신을 동시에 지정할 수 없습니다.");
}
```

Do not special-case selector rendering or Markdown parsing. Both selectors are populated from `TARGETS`, `targetSummary` reads the faction-specific glyph, and `parseTargetMarkdown` enumerates the same registry.

- [x] **Step 5: Run Task 1 tests and verify GREEN**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
```

Expected: all existing tests plus the two new tests pass; ally and enemy self markers round-trip as `self`, and completion rejects the two-self card.

- [x] **Step 6: Commit Task 1**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs
git commit -m "feat(tools): add self card targets"
```

### Task 2: Export Button Copy and Browser Behavior

**Files:**
- Modify: `Tools/card-idea-notebook/index.html`

**Interfaces:**
- Consumes: Task 1 `TARGETS.self` and the existing target `<select>` population loop.
- Produces: visible export button copy `Markdown 내보내기`; ally and enemy selector options whose value is `self` and label is `자신`.

- [x] **Step 1: Run a browser RED copy check**

Open `Tools/card-idea-notebook/index.html` in the in-app browser. Expected: the lower action button visibly reads `AI용 Markdown 내보내기`, proving the requested copy is absent. Do not add a source-text assertion for human-facing prose.

- [x] **Step 2: Change the visible button copy**

Replace:

```html
<button type="button" class="primary wide-action" id="export-markdown">AI용 Markdown 내보내기</button>
```

with:

```html
<button type="button" class="primary wide-action" id="export-markdown">Markdown 내보내기</button>
```

Keep the element ID and every event listener unchanged.

- [x] **Step 3: Run automated verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
node -e 'const fs=require("fs"); const h=fs.readFileSync("Tools/card-idea-notebook/index.html","utf8"); let n=0; for(const m of h.matchAll(/<script(?: [^>]*)?>([\s\S]*?)<\/script>/g)){ new Function(m[1]); n++; } if(n!==2) throw new Error(`expected 2 scripts, got ${n}`); console.log("2 scripts parse")'
git diff --check
```

Expected: every Node test passes, exactly two inline scripts parse, and no whitespace errors exist.

- [x] **Step 4: Run browser GREEN smoke verification**

Open `Tools/card-idea-notebook/index.html` in the in-app browser and verify:

1. Both the ally and enemy range selectors contain `자신`.
2. Selecting ally self renders `◎` in the ally marker and Markdown preview.
3. Selecting enemy self renders `◎` in the enemy marker and Markdown preview.
4. Selecting self for both factions shows the blocking message and prevents completion.
5. Reload preserves either valid self target through schema 4 local storage.
6. Exporting and re-importing cards with ally self and enemy self preserves their respective targets.
7. The lower action button reads exactly `Markdown 내보내기`.
8. Existing front/back/all markers and the export dialog still behave as before.
9. Browser console error logs are empty.

- [x] **Step 5: Commit Task 2**

```bash
git add Tools/card-idea-notebook/index.html
git commit -m "refactor(tools): rename Markdown export button"
```

### Task 3: Documentation Lifecycle and Final Verification

**Files:**
- Modify: `docs/superpowers/specs/2026-07-27-position-targeting-card-text-design.md`
- Modify: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`
- Move: `docs/superpowers/plans/2026-07-28-card-notebook-self-target.md` to `docs/superpowers/archive/plans/2026-07-28-card-notebook-self-target.md`

**Interfaces:**
- Consumes: verified Task 1 core behavior and Task 2 browser behavior.
- Produces: current implemented tool documentation, archived implementation record, and a clean reviewed feature branch.

- [x] **Step 1: Mark the tool implementation complete and archive this plan**

Change the position-targeting spec status to:

```text
current — 위치 대상·자신 대상 규칙과 카드 표기 확정, 카드 노트 반영
```

Change the card-notebook spec status to:

```text
current — 자신 대상 `◎`와 Markdown 내보내기 명칭 구현 완료
```

In `docs/superpowers/README.md`, keep the current spec entries but update their authority text to mention five position ranges plus `자신` and generic Markdown import/export. Remove this plan from the active-plan table. Add the archived plan link under the external card-authoring section of `docs/superpowers/archive/README.md`, move the plan to `archive/plans/`, mark every checkbox complete, and append an `Implementation Result` section with the exact test count and browser evidence.

- [x] **Step 2: Request independent code review**

Use `superpowers:requesting-code-review`. The reviewer must check:

- `self` is accepted and rendered for both factions.
- two simultaneous self targets are blocked during completion and strict import.
- schema 4 and existing saved cards remain compatible.
- strict Markdown round-trip preserves the faction-specific self target.
- the button copy changes without changing its ID or export behavior.
- documentation status, central index, and archive lifecycle are consistent.

Fix every Critical or Important finding with a failing regression test when behavior changes.

- [x] **Step 3: Run final verification**

Run:

```bash
node --test Tools/card-idea-notebook/index.test.mjs
node -e 'const fs=require("fs"); const h=fs.readFileSync("Tools/card-idea-notebook/index.html","utf8"); let n=0; for(const m of h.matchAll(/<script(?: [^>]*)?>([\s\S]*?)<\/script>/g)){ new Function(m[1]); n++; } if(n!==2) throw new Error(`expected 2 scripts, got ${n}`); console.log("2 scripts parse")'
git diff --check
git status --short
```

Expected: all Node tests pass, exactly two inline scripts parse, no whitespace errors exist, and only the intended documentation/archive changes remain before the final documentation commit.

- [x] **Step 4: Commit Task 3**

```bash
git add docs/superpowers/README.md \
  docs/superpowers/archive/README.md \
  docs/superpowers/specs/2026-07-27-position-targeting-card-text-design.md \
  docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md \
  docs/superpowers/plans/2026-07-28-card-notebook-self-target.md \
  docs/superpowers/archive/plans/2026-07-28-card-notebook-self-target.md
git commit -m "docs: complete card self target implementation"
```

## Implementation Result

- 아군과 적군 위치 범위에 `자신`을 추가하고 양 진영 모두 `◎`로 표시했다.
- 한 카드가 아군과 적군을 동시에 `자신`으로 지정하면 완성 저장과 strict Markdown 불러오기를 차단한다.
- 저장 스키마 `4`를 유지하면서 아군·적군 `자신` 대상의 strict Markdown 왕복을 지원한다.
- 기존 버튼 ID와 동작을 유지한 채 표시 문구를 `Markdown 내보내기`로 변경했다.
- Node 테스트 38개가 모두 통과했고, HTML의 inline script 2개가 모두 구문 검사를 통과했다.
- 브라우저에서 양 진영의 `자신` 선택·`◎` 표시·새로고침 후 보존, 동시 `자신` 경고와 미완성 유지,
  변경된 버튼 문구, 빈 콘솔 오류 로그를 확인했다.
