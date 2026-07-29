# Starter Pool ScriptableObject Authoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a lossless `CardAsset` authoring path and a validated `CardPoolAsset`/editor pipeline for the 22-card starter pool without changing the existing starter deck assets.

**Architecture:** `CardAsset` remains the Unity source of truth and converts rule data to pure `CardSpec`; `CardPoolAsset` groups unique candidate cards without deck counts. `CardCodeGenerator` seeds missing starter-pool assets without overwriting existing cards and optionally exports a validated pool snapshot for headless consumers.

**Tech Stack:** Unity 6 ScriptableObject/Editor APIs, C# 9-compatible runtime code, NUnit EditMode tests, pure .NET headless tests.

## Global Constraints

- Do not switch the main checkout branch; work only in `/Users/ish/Git/rogue-deck-starter-pool-so` on `feat/starter-pool-so`.
- Keep `FateWeaver.Core` free of `UnityEngine` references and preserve deterministic rules.
- Do not modify `Assets/Unity/CardSO/Player/StarterDeck.asset` or the existing ten starter-card assets.
- Do not create the 22 production `.asset` files in this linked worktree; run the seeder only after merge approval in the main Unity checkout.
- Do not add external packages.
- Store Unity batchmode logs and test results under `/private/tmp`.
- New source/test files under `Assets/` must include Unity-generated `.meta` files before commit.

---

### Task 1: Preserve CardAsset rules and metadata

**Files:**
- Create: `Assets/Unity/CardGrade.cs`
- Modify: `Assets/Unity/CardAsset.cs`
- Create: `Assets/Tests/UnityEditMode/CardAssetAuthoringTests.cs`

**Interfaces:**
- Consumes: `InterventionTargetSideRef`, `CardSpec`.
- Produces: `CardAsset.InterventionTargetSide`, `CardAsset.InterventionRequireAdjacent`, `CardAsset.Grade`, `CardAsset.Tags`, and `CardAsset.ToSpec()` with lossless intervention constraints.

- [ ] **Step 1: Write failing CardAsset behavior tests**

Create tests that instantiate a real `CardAsset`, populate private serialized fields through `SerializedObject`, and assert:

```csharp
var spec = card.ToSpec();
Assert.AreEqual(InterventionTargetSideRef.Enemy, spec.InterventionTargetSide);
Assert.IsTrue(spec.InterventionRequireAdjacent);
Assert.AreEqual(CardGrade.Common, card.Grade);
CollectionAssert.AreEqual(new[] { "시작", "실행력" }, card.Tags);
Assert.IsNull(typeof(CardSpec).GetField("Grade"));
Assert.IsNull(typeof(CardSpec).GetField("Tags"));
```

The production mutation caught is omission of either serialized intervention field from `ToSpec()`, or accidental leakage of Unity metadata into core authoring data.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/ish/Git/rogue-deck-starter-pool-so \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardAssetAuthoringTests \
  -testResults /private/tmp/starter-pool-card-asset-red.xml \
  -logFile /private/tmp/starter-pool-card-asset-red.log \
  -quit
```

Expected: FAIL because `CardGrade` and the four read-only properties do not exist and `ToSpec()` drops the two rule values.

- [ ] **Step 3: Add the minimal serialized fields and mapping**

Define:

```csharp
public enum CardGrade
{
    None,
    Common,
    Advanced,
    Rare,
    Other
}
```

Add to `CardAsset`:

```csharp
[SerializeField] private InterventionTargetSideRef _interventionTargetSide;
[SerializeField] private bool _interventionRequireAdjacent;
[SerializeField] private CardGrade _grade = CardGrade.None;
[SerializeField] private string[] _tags = Array.Empty<string>();

public InterventionTargetSideRef InterventionTargetSide => _interventionTargetSide;
public bool InterventionRequireAdjacent => _interventionRequireAdjacent;
public CardGrade Grade => _grade;
public IReadOnlyList<string> Tags => _tags ?? Array.Empty<string>();
```

Copy the two rule fields in `ToSpec()` and keep grade/tags out of `CardSpec`.

- [ ] **Step 4: Run the focused test and verify GREEN**

Re-run the Step 2 command with `green` result/log filenames. Expected: PASS.

### Task 2: Validate and convert candidate pools

**Files:**
- Create: `Assets/Unity/CardPoolAsset.cs`
- Create: `Assets/Tests/UnityEditMode/CardPoolAssetTests.cs`

**Interfaces:**
- Consumes: authored `CardAsset` references and `CardAsset.ToSpec()`.
- Produces: `CardPoolAsset.Id`, `CardPoolAsset.Cards`, `Validate()`, and `ToSpecs()`.

- [ ] **Step 1: Write failing pool validation tests**

Use real transient `CardAsset` objects and set the pool's private fields with `SerializedObject`. Cover these independent behaviors:

```csharp
CollectionAssert.IsEmpty(validPool.Validate());
CollectionAssert.AreEqual(new[] { "alpha", "beta" }, validPool.ToSpecs().Select(x => x.Id));
StringAssert.Contains("null", nullCardPool.Validate().Single());
StringAssert.Contains("duplicate", duplicateIdPool.Validate().Single());
StringAssert.Contains("grade", missingGradePool.Validate().Single());
StringAssert.Contains("empty tag", emptyTagPool.Validate().Single());
StringAssert.Contains("duplicate tag", duplicateTagPool.Validate().Single());
Assert.Throws<InvalidOperationException>(() => invalidPool.ToSpecs());
```

The production mutation caught is accepting a partial/ambiguous pool or silently converting only valid entries.

- [ ] **Step 2: Run the focused test and verify RED**

Run Unity EditMode batchmode with:

```text
-testFilter FateWeaver.Tests.UnityEditMode.CardPoolAssetTests
-testResults /private/tmp/starter-pool-asset-red.xml
-logFile /private/tmp/starter-pool-asset-red.log
```

Expected: FAIL because `CardPoolAsset` does not exist.

- [ ] **Step 3: Implement CardPoolAsset**

Create the specified ScriptableObject with private `_id` and `_cards`. `Validate()` must:

```text
reject blank pool id
reject null card references
reject blank card ids
reject duplicate card ids using StringComparer.Ordinal
reject CardGrade.None
reject null/blank tags
reject duplicate tags within one card using StringComparer.Ordinal
```

`ToSpecs()` calls `Validate()`, throws `InvalidOperationException` containing all messages when any error exists, and otherwise converts every card in stored order.

- [ ] **Step 4: Run the focused test and verify GREEN**

Re-run Step 2 with `green` filenames. Expected: PASS.

### Task 3: Seed missing cards without overwriting authored values

**Files:**
- Modify: `Assets/Unity/Editor/CardCodeGenerator.cs`
- Create: `Assets/Tests/UnityEditMode/StarterPoolSeederTests.cs`

**Interfaces:**
- Consumes: `StarterPoolSpecs.Build()`, the 22 ID-to-tag mappings from `Tools/card-idea-notebook/시작 카드 풀.md`, and `CardPoolAsset.Validate()`.
- Produces: menu `Fate Weaver/Seed Starter Pool Assets` and a parameterized editor-only seeding entry point for isolated tests.

- [ ] **Step 1: Write failing seed behavior tests**

In a unique temporary `Assets/Tests/Temp/StarterPoolSeeder-<guid>` folder:

```csharp
var first = CardCodeGenerator.SeedStarterPoolAssets(cardFolder, poolPath);
Assert.IsEmpty(first);
Assert.AreEqual(22, AssetDatabase.LoadAssetAtPath<CardPoolAsset>(poolPath).Cards.Count);
```

Then mutate one generated card's cost, description, grade, tags, and art-compatible reference fields, save, rerun the seeder, and assert those values are unchanged. Delete the temporary folder in `TearDown`.

Also assert all 22 cards are `Common`, have the exact ordered tags from the Markdown source, and map `hasten`, `delay`, `breather`, and `crossover` to their correct target-side/adjacency constraints.

The production mutation caught is reapplying bootstrap specs or metadata to an existing SO.

- [ ] **Step 2: Run the focused test and verify RED**

Run Unity EditMode batchmode with:

```text
-testFilter FateWeaver.Tests.UnityEditMode.StarterPoolSeederTests
-testResults /private/tmp/starter-pool-seeder-red.xml
-logFile /private/tmp/starter-pool-seeder-red.log
```

Expected: FAIL because the menu and parameterized seeder do not exist.

- [ ] **Step 3: Implement the bootstrap catalog and idempotent seeder**

Add constants:

```csharp
private const string StarterPoolCardFolder = PlayerCardFolder + "/StarterPool";
private const string StarterPoolAssetPath = PlayerCardFolder + "/StarterPool.asset";
```

Add an ordered, exact ID-to-tag catalog for all 22 cards. For each spec:

```text
load existing card by path
if missing: create transient CardAsset, apply spec and Common/tags
if present: do not call Apply and do not alter any field
```

Validate all prospective references through a transient `CardPoolAsset` before saving. On errors, destroy unsaved transient objects and return the messages without calling `AssetDatabase.SaveAssets()`. On success, create missing cards, create/update only the pool ID/reference list, save, refresh, and return an empty error list.

- [ ] **Step 4: Run the focused test and verify GREEN**

Re-run Step 2 with `green` filenames. Expected: PASS and temporary assets removed.

### Task 4: Export optional starter-pool headless snapshots

**Files:**
- Modify: `Assets/Unity/Editor/CardCodeGenerator.cs`
- Create: `Assets/Tests/UnityEditMode/CardCodeGeneratorTests.cs`
- Modify: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs` only after a real SO-generated `StarterPool()` method exists.

**Interfaces:**
- Consumes: starter deck specs and optional validated pool specs.
- Produces: emitted `StarterDeck()` plus optional `StarterPool()` literals that preserve intervention target-side and adjacency fields.

- [ ] **Step 1: Write failing emitter tests**

Exercise a public editor-only pure emission method with a one-card deck and four intervention pool specs. Assert the emitted source contains:

```csharp
public static IReadOnlyList<CardSpec> StarterPool()
InterventionTargetSide = InterventionTargetSideRef.Player
InterventionTargetSide = InterventionTargetSideRef.Enemy
InterventionRequireAdjacent = true
```

Also emit with a null optional pool and assert `StarterDeck()` remains present while `StarterPool()` is absent.

The production mutation caught is dropping intervention constraints or making the pre-pool deck export fail.

- [ ] **Step 2: Run the focused test and verify RED**

Run Unity EditMode batchmode with:

```text
-testFilter FateWeaver.Tests.UnityEditMode.CardCodeGeneratorTests
-testResults /private/tmp/starter-pool-generator-red.xml
-logFile /private/tmp/starter-pool-generator-red.log
```

Expected: FAIL because the emitter accepts only the starter deck and drops both constraint fields.

- [ ] **Step 3: Implement optional pool export**

Update `Generate()` to load `StarterPool.asset` optionally:

```text
missing pool -> log a warning and emit the existing starter deck only
present valid pool -> emit both methods
present invalid pool -> log errors and write no generated file
```

Make the pure source emitter callable from EditMode tests. Extend `EmitSpec()` to serialize both intervention constraint fields. Do not add `StarterPool()` to committed `GeneratedCards.cs` until it has been produced from the real SO pool in the main checkout.

- [ ] **Step 4: Run the focused test and verify GREEN**

Re-run Step 2 with `green` filenames. Expected: PASS.

### Task 5: Verify, document, and archive the completed plan

**Files:**
- Modify: `Assets/Unity/PLAYTEST.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`
- Move: `docs/superpowers/plans/2026-07-30-starter-pool-so-authoring.md` to `docs/superpowers/archive/plans/2026-07-30-starter-pool-so-authoring.md`

**Interfaces:**
- Consumes: implemented menus, tests, and approved design.
- Produces: operator instructions and a clean, indexed implementation record.

- [ ] **Step 1: Update operator documentation**

Document that `Seed Starter Pool Assets` creates only missing starter-pool cards, preserves existing card fields, and must be run in the main Unity checkout after merge. Document that `Generate Cards from SO` exports the pool only when `StarterPool.asset` exists and validates.

- [ ] **Step 2: Generate `.meta` files and run focused Unity tests**

Run the four focused EditMode fixtures together, writing results/logs to `/private/tmp`. Inspect the XML and Unity log for failures and unexpected errors.

- [ ] **Step 3: Run full verification**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/ish/Git/rogue-deck-starter-pool-so \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/starter-pool-full-editmode.xml \
  -logFile /private/tmp/starter-pool-full-editmode.log \
  -quit
git diff --check
git status --short
```

Expected: all headless and EditMode tests pass, no whitespace errors, no production starter pool/deck `.asset` changes, and only intended source/test/doc/meta changes.

- [ ] **Step 4: Archive and index the plan**

Move this completed plan to `docs/superpowers/archive/plans/`, add it to `docs/superpowers/archive/README.md`, and ensure `docs/superpowers/README.md` lists no completed plan as active.

- [ ] **Step 5: Commit the verified implementation**

Stage only the intended paths and commit:

```bash
git commit -m "feat: add starter pool SO authoring pipeline"
```
