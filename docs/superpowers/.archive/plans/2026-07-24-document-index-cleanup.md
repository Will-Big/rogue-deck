# Documentation Index Cleanup Implementation Plan

> **보관 문서:** 완료된 문서 정리의 구현 기록입니다. 현행 문서 운영 규칙은
> [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Keep only current, reusable Fate Weaver documentation in the main specs/plans directories, archive completed history, remove two abandoned documents, and provide authoritative indexes.

**Architecture:** Preserve the existing `specs`/`plans` split under a new `archive` directory so historical relative paths remain predictable. A central `docs/superpowers/README.md` lists only current authority and active work, while `archive/README.md` exposes historical records separately.

**Tech Stack:** Markdown, Git moves, shell-based link/count validation, existing .NET 5 headless test harness.

## Global Constraints

- Work only in `/Users/ish/Git/rogue-deck-document-index` on branch `refactor/document-index`.
- Preserve user changes in the main checkout.
- Keep 8 existing specs, the documentation governance spec, and the architecture backlog in current directories.
- Move 15 specs and 42 plans to archive; delete only the two explicitly approved documents.
- Do not redesign the run cycle, cards, characters, code, or Unity assets.
- Use `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`.

---

### Task 1: Create the archive and move historical documents

**Files:**
- Create: `docs/superpowers/archive/specs/`
- Create: `docs/superpowers/archive/plans/`
- Move: 15 approved historical specs
- Move: every existing `docs/superpowers/plans/*.md` except `2026-07-16-architecture-refactor-backlog.md` and this implementation plan
- Delete: `docs/superpowers/specs/2026-07-03-combat-slice-signature-enemy-brainstorm-WIP.md`
- Delete: `docs/superpowers/specs/2026-07-12-base-player-character-lineup-design.md`

**Interfaces:**
- Consumes: the exact classification in `docs/superpowers/specs/2026-07-24-document-index-cleanup-design.md`
- Produces: stable current and archive directory membership for Tasks 2–4

- [x] **Step 1: Verify the input set**

Run:

```bash
test "$(find docs/superpowers/specs -maxdepth 1 -name '*.md' | wc -l | tr -d ' ')" = 26
test "$(find docs/superpowers/plans -maxdepth 1 -name '*.md' | wc -l | tr -d ' ')" = 44
```

Expected: both commands exit 0. The plan count is 44 because this implementation plan was added after the 43-document baseline.

- [x] **Step 2: Create archive directories**

Run:

```bash
mkdir -p docs/superpowers/archive/specs docs/superpowers/archive/plans
```

Expected: both directories exist.

- [x] **Step 3: Move the 15 historical specs**

Run one `git mv` for each:

```text
2026-06-22-ugui-card-ui-design.md
2026-06-23-deck-playtest-ui-design.md
2026-06-23-so-card-authoring-design.md
2026-06-24-status-effects-design.md
2026-06-26-card-descriptions-design.md
2026-07-16-unified-target-selection-ux-design.md
2026-07-17-execution-placement-preview-design.md
2026-07-17-multi-target-toggle-selection-design.md
2026-07-18-execution-card-placement-flow-design.md
2026-07-19-card-outline-and-curved-placement-flight-design.md
2026-07-19-card-selection-placement-motion-design.md
2026-07-19-card-type-removal-design.md
2026-07-19-placement-flight-flip-design.md
2026-07-20-run-cycle-skeleton-design.md
2026-07-20-status-key-dropdown-authoring-design.md
```

Source: `docs/superpowers/specs/<name>`
Destination: `docs/superpowers/archive/specs/<name>`

Expected: `archive/specs` contains 15 Markdown files.

- [x] **Step 4: Move the 42 pre-existing historical plans**

Enumerate `docs/superpowers/plans/*.md`, excluding:

```text
2026-07-16-architecture-refactor-backlog.md
2026-07-24-document-index-cleanup.md
```

Move every other result to `docs/superpowers/archive/plans/`.

Expected: `archive/plans` contains 42 Markdown files and current `plans` contains exactly the backlog and this implementation plan.

- [x] **Step 5: Delete only the approved abandoned specs**

Delete through `apply_patch`:

```text
docs/superpowers/specs/2026-07-03-combat-slice-signature-enemy-brainstorm-WIP.md
docs/superpowers/specs/2026-07-12-base-player-character-lineup-design.md
```

Expected: neither path exists; Git records two deletions.

### Task 2: Reconcile current authority and archive links

**Files:**
- Modify: `docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md`
- Modify: `docs/superpowers/specs/2026-06-22-deck-loop-design.md`
- Modify: `docs/superpowers/specs/2026-06-27-warden-lock-enemy-design.md`
- Modify: `docs/superpowers/specs/2026-07-10-battle-scene-visual-design.md`
- Modify: `docs/superpowers/specs/2026-07-15-party-foundation-design.md`
- Modify: `docs/superpowers/specs/2026-07-16-description-registry-design.md`
- Modify: `docs/superpowers/specs/2026-07-19-open-card-authoring-design.md`
- Modify: `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md`
- Modify: moved Markdown files with broken relative links

**Interfaces:**
- Consumes: Task 1’s final paths and the authority rules in the cleanup design
- Produces: internally consistent current documents and resolvable archive references

- [x] **Step 1: Standardize current-document headers**

Add or update `문서 유형`, `주 도메인`, and `상태` near the top of each current document. Mark implemented architecture documents as current references instead of “implementation plan pending.”

Expected: each current spec has an explicit current/reference status and authority scope remains in its body.

- [x] **Step 2: Reconcile party card ownership**

In `2026-07-15-party-foundation-design.md`:

- remove links to the two deleted specs;
- remove the “generic card can belong to anyone” acquisition category;
- state that every card has exactly one character owner;
- replace the unresolved inheritance section with a link to `2026-07-20-character-card-pools-design.md` §4;
- preserve combat-time owner death removal and future-card cancellation.

Expected: party ownership and inheritance no longer contradict the card-pool rules.

- [x] **Step 3: Update roadmap links and statuses**

In `2026-07-16-architecture-refactor-backlog.md`:

- rewrite implementation-record links to `../archive/plans/<name>`;
- keep completed P0 statuses;
- keep unfinished P0-C/P1/P2 work active.

Expected: all historical record links resolve after Task 1.

- [x] **Step 4: Repair moved-document links**

For every local Markdown link in `docs/superpowers/archive`, resolve it from the moved file’s directory. Rewrite links that now target current specs/plans or another archive folder.

Expected: no moved document contains a broken local `.md` link.

### Task 3: Build indexes and enforce the lifecycle

**Files:**
- Create: `docs/superpowers/README.md`
- Create: `docs/superpowers/archive/README.md`
- Modify: `AGENTS.md`
- Move at completion: `docs/superpowers/plans/2026-07-24-document-index-cleanup.md` → `docs/superpowers/archive/plans/2026-07-24-document-index-cleanup.md`

**Interfaces:**
- Consumes: Tasks 1–2’s final document paths and statuses
- Produces: the repository’s documentation entry points and future maintenance rule

- [x] **Step 1: Create the central current-document index**

Write `docs/superpowers/README.md` with:

- state definitions (`current`, `active`, `needs-redesign`, `archived`);
- current architecture, combat/party, authoring/description, content/card-pool, and UX references;
- active architecture backlog;
- run cycle as `needs-redesign`, with the legacy/card economy conflict explained;
- one link to `archive/README.md`;
- same-commit index update policy.

Expected: a new session can determine current authority without opening archive documents.

- [x] **Step 2: Create the archive index**

Write `docs/superpowers/archive/README.md` with separate tables for archived specs and plans. Each of the 57 baseline archived documents must appear exactly once, grouped by domain or work sequence.

Expected: all archived documents are discoverable without appearing in the current index.

- [x] **Step 3: Add the repository instruction**

Append one documentation lifecycle rule to `AGENTS.md`:

```text
새 스펙·계획을 추가하거나 완료·대체할 때 같은 커밋에서 docs/superpowers/README.md를 갱신한다. 완료된 계획·구현 기록은 archive/plans로 옮기고, 현행 specs/plans에는 current 또는 active 문서만 둔다.
```

Expected: future AI sessions receive the index-maintenance requirement automatically.

- [x] **Step 4: Archive this completed implementation plan**

After all content changes are complete, move this plan to:

```text
docs/superpowers/archive/plans/2026-07-24-document-index-cleanup.md
```

Add it to the archive index. This makes the final counts `current plans = 1` and `archive plans = 43`.

### Task 4: Validate and commit the reorganization

**Files:**
- Verify: all changed Markdown and `AGENTS.md`

**Interfaces:**
- Consumes: Tasks 1–3
- Produces: merge-ready documentation branch

- [x] **Step 1: Verify final counts and approved deletions**

Run:

```bash
test "$(find docs/superpowers/specs -maxdepth 1 -name '*.md' | wc -l | tr -d ' ')" = 9
test "$(find docs/superpowers/plans -maxdepth 1 -name '*.md' | wc -l | tr -d ' ')" = 1
test "$(find docs/superpowers/archive/specs -maxdepth 1 -name '*.md' | wc -l | tr -d ' ')" = 15
test "$(find docs/superpowers/archive/plans -maxdepth 1 -name '*.md' | wc -l | tr -d ' ')" = 43
test ! -e docs/superpowers/specs/2026-07-03-combat-slice-signature-enemy-brainstorm-WIP.md
test ! -e docs/superpowers/specs/2026-07-12-base-player-character-lineup-design.md
```

Expected: every command exits 0.

- [x] **Step 2: Validate Markdown links**

Scan Markdown inline links ending in `.md`, strip optional anchors, resolve relative paths from the source document, and fail on every missing target. Ignore HTTP(S) URLs.

Expected: zero broken local Markdown links under `README.md`, `AGENTS.md`, and `docs/`.

- [x] **Step 3: Verify content invariants**

Run searches that fail if:

- a current document links to either deleted filename;
- party foundation declares a generic/shared card acquisition pool;
- the current index lists an archived file as current;
- the current plans directory contains anything except the architecture backlog.

Expected: all searches confirm the approved current/archived boundary.

- [x] **Step 4: Run formatting and full tests**

Run:

```bash
git diff --check
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: no whitespace errors; 324 tests pass with 0 failures.

- [x] **Step 5: Commit the implemented reorganization**

Stage only `AGENTS.md` and `docs/superpowers`. Commit:

```bash
git commit -m "docs: curate and index project documentation"
```

Expected: worktree is clean and the branch contains the design commit, plan commit, and implementation commit.
