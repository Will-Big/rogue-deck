# Card Frame Follow-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:brainstorming for Task 2's visual rule, then superpowers:writing-plans and superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the reviewed primitive card-frame work after its checkpoint merge, finish the execution-order badge review, add a subtle per-card hand-height rhythm without losing the shallow fan, and archive the completed primitive-frame plan.

**Architecture:** Keep authored card-internal coordinates in the execution/intervention prefabs. Keep `HandFanLayout.PoseFor` as the deterministic shallow-arc baseline; the follow-up hand treatment must be a small presentation-only offset composed on top of that pose, not a replacement for responsive spacing/scale or a rules-layer change. JSON-backed status projection remains a separate deferred integration in `docs/superpowers/README.md`.

**Tech Stack:** Unity 6000.5.2f1, uGUI `RectTransform`, pure C# presentation math, NUnit headless/EditMode/PlayMode tests.

## Global Constraints

- Continue only in `/Users/ish/Git/rogue-deck-card-frame-design` on `refactor/card-frame-design`; do not create or switch worktrees/branches.
- The user adjusts prefab numeric values; preserve those values unless a new visual review explicitly changes them.
- Do not implement JSON/status runtime wiring, SO fallback content, or effective-value text colors in this follow-up.
- Do not replace the deterministic shallow fan with a straight row, random offsets, per-resolution tables, or `LateUpdate()` positioning.
- Unity automated results and captures go under `/private/tmp`; restore generated Unity noise before committing.
- Do not merge to `master` again without explicit user approval.

---

## Checkpoint State

- The primitive frame, inline faction symbols, colored target grammar, responsive hand, generic four-column status-grid prefabs, and JSON-independent tooltip view components are implemented.
- The JSON status projection and shared runtime hover wiring remain deferred in `docs/superpowers/README.md`.
- Removed poster assets were audited at 0 GUID references before deletion.
- Verification before this handoff:
  - headless: **426/426 passed**;
  - Unity EditMode: **592 total, 585 passed, 0 failed, 7 skipped**;
  - Unity PlayMode: **2/2 passed**;
  - explicit render capture: **9/9 passed**, seven PNGs under `/private/tmp/primitive-card-frame-captures/`.
- Local `.superpowers/` and `graphify-out` scratch artifacts are not project changes and must remain uncommitted.

### Relevant checkpoint commits

- `f6c6668 docs: record card frame verification checkpoint`
- `c73442b test(ui): update playmode card fixture`
- `335a6c3 chore(ui): retire poster card frame assets`
- `1bb65c8 test(ui): lock card form factor layouts`
- `13fb2eb docs: defer JSON card status integration`
- `a9c1d4f refactor(ui): author generic card status prefabs`

---

### Task 1: Verify the user-adjusted execution-order badge

**Files:**
- Modify/commit: `Assets/Unity/Prefabs/ExecutionCardView.prefab`
- Test: `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs`

**Interfaces:**
- Preserves the execution-order badge size `50×50`, rotation `45°`, top-right anchor and partial frame protrusion.
- Uses the user-authored anchored position `x = -10`, `y = -100` instead of the prior `x = 10`, `y = -105`.

- [x] **Step 1: Inspect the serialized diff**

Expected: the only semantic prefab change is `ExecutionOrderBadge.m_AnchoredPosition`; remove trailing-space-only `m_Name` serialization noise without changing the position.

- [x] **Step 2: Run the focused prefab contract**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardFramePrefabTests \
  -testResults /private/tmp/execution-order-badge-editmode.xml \
  -logFile /private/tmp/execution-order-badge-editmode.log
```

Expected: all focused tests pass and `Execution_frame_has_symbol_target_panel_and_protruding_badges` still proves partial protrusion, size and rotation.

- [x] **Step 3: Regenerate the execution capture**

Run the explicit `FateWeaver.Tests.UnityEditMode.CardFrameRenderCapture` filter. Inspect `execution-1280x720.png` and confirm the moved badge is visibly associated with the execution card without colliding with the target/description blocks.

- [x] **Step 4: Commit the reviewed prefab**

```bash
git add Assets/Unity/Prefabs/ExecutionCardView.prefab
git commit -m "fix(ui): improve execution order badge visibility"
```

---

### Task 2: Design and implement subtle hand-card height variation

**Files:**
- Modify after design approval: `Assets/Core/Simulation/Presentation/HandFanLayout.cs`
- Modify after design approval: `Assets/Unity/HandFanView.cs`
- Modify after design approval: `docs/superpowers/specs/2026-07-31-primitive-card-frame-design.md`
- Test: `Assets/Core/Tests/EditMode/HandFanLayoutTests.cs`
- Test: `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`
- Test: `Assets/Tests/UnityEditMode/CardFrameResponsiveLayoutTests.cs`
- Test: `Assets/Tests/UnityPlayMode/HandFanResponsivePlayModeTests.cs`

**Interfaces:**
- `HandFanLayout.PoseFor(index, count, spacing, anglePerCard, arcDrop)` remains the deterministic shallow-arc source.
- The approved design adds a deterministic, small per-card Y contribution on top of `FanPose.YOffset`.
- Responsive spacing, uniform scaling, hover/held baselines, sibling restoration and placement-flight start poses continue consuming the final composed pose.

- [ ] **Step 1: Resume the visual design gate**

Use `superpowers:brainstorming` and obtain user approval for one deterministic micro-height pattern. Compare exactly these approaches:

1. a small alternating offset around the shallow arc;
2. a center-relative symmetric offset that preserves left/right mirror symmetry;
3. a monotonic offset, only if the user prefers cost readability over symmetric fan balance.

Recommend the smallest symmetric pattern that makes adjacent costs attributable. Do not assume increasing `_arcDrop` alone solves badge occlusion: `_badgeOverflow` currently affects total width/scale, not visibility between neighboring cards.

- [ ] **Step 2: Amend the authoritative primitive-frame spec and write the approved implementation plan**

The amendment must fix the exact offset sequence/formula, maximum logical-pixel delta at scale `1`, behavior for odd/even hand counts, and the requirement that the original parabolic arc and angle remain intact. Update `docs/superpowers/README.md` in the same design commit only if the authority description changes.

- [ ] **Step 3: Execute the approved plan with RED/GREEN verification**

At minimum, tests must prove:

- the existing centered single-card pose stays unchanged;
- the original shallow-arc and tilt contribution remains present;
- adjacent cards receive the approved small deterministic height difference;
- odd/even hands obey the approved symmetry rule;
- resize preserves hover/held order and recomputes the same deterministic poses;
- 4:3, 16:10, 16:9 and 21:9 captures keep every cost attributable to its card.

- [ ] **Step 4: Commit only after user visual approval**

Use a focused implementation commit followed by a documentation checkpoint commit if the plan requires separate review history.

---

### Task 3: Close and archive the primitive-card-frame work

**Files:**
- Move: `docs/superpowers/plans/2026-07-31-primitive-card-frame.md` to `docs/superpowers/archive/plans/`
- Move after all handoff tasks finish: this file to `docs/superpowers/archive/plans/`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`

**Interfaces:**
- The primitive card-frame design remains `current`.
- The JSON status projection plan remains active/deferred until its README gate is satisfied.

- [ ] **Step 1: Run final verification**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/card-frame-follow-up-editmode.xml \
  -logFile /private/tmp/card-frame-follow-up-editmode.log
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform PlayMode \
  -testResults /private/tmp/card-frame-follow-up-playmode.xml \
  -logFile /private/tmp/card-frame-follow-up-playmode.log
```

Expected: all three suites report zero failures, structural audits are empty, and only local tool artifacts remain untracked.

- [ ] **Step 2: Obtain final visual approval**

Regenerate the seven card-frame captures. The user verifies the execution-order badge, target grammar, execution/intervention form factors, status-grid structure and subtle hand-height rhythm.

- [ ] **Step 3: Archive both completed plans and update indexes**

Remove both active plan rows from `docs/superpowers/README.md`, add both plans to `docs/superpowers/archive/README.md`, keep the card-status tooltip plan active/deferred, and commit the completion record.

- [ ] **Step 4: Confirm branch state**

```bash
git status --short
git log --oneline --decorate -12
```

Expected: no uncommitted project file. Do not merge to `master` without explicit user approval.
