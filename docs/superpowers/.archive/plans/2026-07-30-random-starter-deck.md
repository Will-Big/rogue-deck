# Random Starter Deck Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix one randomly selected 10-card starter deck from the authored 22-card pool, then keep the Unity asset, pure C# fallbacks, and generated headless snapshot content-equivalent.

**Architecture:** `StarterPool.asset` remains the immutable 22-card candidate set. `StarterDeck.asset` becomes the Unity runtime source for the selected 10 cards, while `StarterDeckSpecs.Build()` names the same fixed selection for the pure C# fallback and `StarterDeck.Build()` maps those specs instead of maintaining a fourth copy of the composition. `CardCodeGenerator.Generate()` exports the Unity assets to `GeneratedCards`, and Unity plus headless tests pin the asset structure and cross-path content.

**Tech Stack:** Unity 6000.5.2f1, C# 9, ScriptableObject YAML assets, NUnit, `dotnet test`, Unity batchmode EditMode tests.

## Global Constraints

- Work only in `/Users/ish/Git/rogue-deck-random-starter-deck` on `feat/random-starter-deck`; do not switch the main checkout branch.
- Preserve the user-authored files in `/Users/ish/Git/rogue-deck` and copy their `.meta` files byte-for-byte so every GUID remains stable.
- `FateWeaver.Core` must not reference `UnityEngine`.
- Do not add packages, card abilities, tuning changes, card art, runtime random selection, or a new runtime deck-building service.
- Keep `StarterPool.asset` at exactly 22 unique cards.
- Keep `StarterDeck.asset` at exactly 10 unique entries with `Count: 1`.
- Final role counts are attack 2, defense 2, manipulation 2, poison 4.
- All Unity batch logs and results go under `/private/tmp`.
- Update `docs/superpowers/README.md` whenever this active plan is added or archived.
- Do not merge to `master` without separate user approval.

## Fixed Draw Record

The approved one-time draw was performed on 2026-07-30 with one 128-bit
`openssl rand -hex 16` key per candidate. Within each role, keys were sorted
ascending and the first 2/2/2/4 candidates were selected.

| Role | Random key | Card ID | Selected |
|---|---|---|---|
| attack | `2dbc79f3152c0ed007ef5efc18bb47d6` | `probing_strike` | yes |
| attack | `2e982e12d5c2bebc739ce7d6edad677a` | `delayed_strike` | yes |
| attack | `4aa0e3b19709f57d8199e8ef7d69cc2d` | `riposte` | no |
| attack | `734b834f12e737d61f6415a88e6fc6ea` | `vanguard_slash` | no |
| defense | `478fd58074130b003096ce93daab7605` | `quick_cover` | yes |
| defense | `53177139463d9467ef4d28cd257601e9` | `early_guard` | yes |
| defense | `daed39524a8f73366e8a534fa6230f22` | `parry_strike` | no |
| defense | `fe1413e30c599d58b539d96c403730a9` | `foresight` | no |
| manipulation | `2de87e7f2f2cff12d3495dadfdc15ee7` | `breather` | yes |
| manipulation | `36bbb8ac4104fb71438428e647ed9293` | `hasten` | yes |
| manipulation | `968f785375eb633b7f272a284a402d8b` | `crossover` | no |
| manipulation | `ff08c04aa7eb546dfd68592897ead2f8` | `delay` | no |
| poison | `0698a911914f05e45e1f4a356267a953` | `toxic_reclaim` | yes |
| poison | `2d0f68d50354daac58fcd2d12f846ae7` | `early_onset` | yes |
| poison | `4579d5c704ebf728b7abed933badbde9` | `spore_veil` | yes |
| poison | `872caaef5462c11582a1e5fab6604a78` | `last_drop` | yes |
| poison | `8f3130b05c109f5e069d6540d345491c` | `stable_culture` | no |
| poison | `95c42dbd8b58126af69afae216a4f250` | `condensed_burst` | no |
| poison | `aa064f8fbf38f1eb0c46feb480a9dba9` | `venom_thrust` | no |
| poison | `ceac563bde871c7356d693fefd27ad6e` | `spread_culture` | no |
| poison | `d0e81489db22570bb635b70c75352ecb` | `distill` | no |
| poison | `d5712f5a2f3088b323369c2ffebdebe5` | `posthumous_spread` | no |

The deck order is the selected rows in role order:

```text
probing_strike
delayed_strike
quick_cover
early_guard
breather
hasten
toxic_reclaim
early_onset
spore_veil
last_drop
```

---

### Task 1: Pin the Unity asset composition

**Files:**
- Create: `Assets/Tests/UnityEditMode/StarterDeckAssetCompositionTests.cs`
- Create via Unity import: `Assets/Tests/UnityEditMode/StarterDeckAssetCompositionTests.cs.meta`
- Copy from main checkout: `Assets/Unity/CardSO/Player/StarterPool.asset`
- Copy from main checkout: `Assets/Unity/CardSO/Player/StarterPool.asset.meta`
- Copy from main checkout: `Assets/Unity/CardSO/Player/StarterPool.meta`
- Copy from main checkout: `Assets/Unity/CardSO/Player/StarterPool/` (22 `.asset` files and their `.meta` files)
- Modify: `Assets/Unity/CardSO/Player/StarterDeck.asset`
- Modify: `docs/superpowers/specs/2026-07-30-random-starter-deck-design.md`

**Interfaces:**
- Consumes: `DeckAsset.Entries`, `CardPoolAsset.Cards`, `CardAsset.Id`, `CardCodeGenerator.EmitSource(IReadOnlyList<CardSpec>, IReadOnlyList<CardSpec>)`.
- Produces: a 22-card pool and a 10-entry deck whose selected IDs are fixed in the order above.

- [x] **Step 1: Write the failing Unity asset contract test**

Create `StarterDeckAssetCompositionTests.cs` with constants for the two asset
paths, the exact 22 expected pool IDs, the exact 10 selected IDs, and the four
role sets. The core assertions are:

```csharp
[Test]
public void Starter_pool_and_deck_match_the_fixed_draw_contract()
{
    var pool = AssetDatabase.LoadAssetAtPath<CardPoolAsset>(PoolPath);
    var deck = AssetDatabase.LoadAssetAtPath<DeckAsset>(DeckPath);

    Assert.NotNull(pool);
    Assert.NotNull(deck);
    Assert.AreEqual("starter_pool", pool.Id);
    Assert.AreEqual("starter", deck.Id);
    CollectionAssert.IsEmpty(pool.Validate());
    CollectionAssert.AreEquivalent(ExpectedPoolIds, pool.Cards.Select(card => card.Id));

    Assert.AreEqual(10, deck.Entries.Length);
    Assert.That(deck.Entries.All(entry => entry.Card != null));
    Assert.That(deck.Entries.All(entry => entry.Count == 1));
    CollectionAssert.AreEqual(
        SelectedIds,
        deck.Entries.Select(entry => entry.Card.Id).ToArray());
    Assert.AreEqual(10, deck.Entries.Select(entry => entry.Card.Id).Distinct().Count());

    var poolIds = new HashSet<string>(pool.Cards.Select(card => card.Id));
    Assert.That(deck.Entries.All(entry => poolIds.Contains(entry.Card.Id)));
    Assert.AreEqual(2, SelectedIds.Count(AttackIds.Contains));
    Assert.AreEqual(2, SelectedIds.Count(DefenseIds.Contains));
    Assert.AreEqual(2, SelectedIds.Count(ManipulationIds.Contains));
    Assert.AreEqual(4, SelectedIds.Count(PoisonIds.Contains));
}

[Test]
public void Generated_snapshot_is_byte_for_byte_current_with_the_assets()
{
    var pool = AssetDatabase.LoadAssetAtPath<CardPoolAsset>(PoolPath);
    var deck = AssetDatabase.LoadAssetAtPath<DeckAsset>(DeckPath);
    var expected = CardCodeGenerator.EmitSource(deck.ToSpecs(), pool.ToSpecs());
    var actual = File.ReadAllText("Assets/Core/Simulation/Generated/GeneratedCards.cs");

    Assert.AreEqual(expected, actual);
}
```

Use these exact role sets:

```csharp
private static readonly HashSet<string> AttackIds = new HashSet<string>
{
    "vanguard_slash", "probing_strike", "delayed_strike", "riposte"
};
private static readonly HashSet<string> DefenseIds = new HashSet<string>
{
    "parry_strike", "quick_cover", "early_guard", "foresight"
};
private static readonly HashSet<string> ManipulationIds = new HashSet<string>
{
    "hasten", "delay", "crossover", "breather"
};
private static readonly HashSet<string> PoisonIds = new HashSet<string>
{
    "venom_thrust", "last_drop", "spore_veil", "spread_culture",
    "toxic_reclaim", "condensed_burst", "distill", "early_onset",
    "stable_culture", "posthumous_spread"
};
```

- [x] **Step 2: Run the focused Unity test and verify RED**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.StarterDeckAssetCompositionTests \
  -testResults /private/tmp/random-starter-red.xml \
  -logFile /private/tmp/random-starter-red.log
```

Expected: failure because `StarterPool.asset` is absent from the worktree and
the old deck/generated snapshot still describe the legacy deck.

- [x] **Step 3: Copy the user-created pool and verify GUID preservation**

Run the following from the worktree:

```bash
cp -p /Users/ish/Git/rogue-deck/Assets/Unity/CardSO/Player/StarterPool.asset Assets/Unity/CardSO/Player/
cp -p /Users/ish/Git/rogue-deck/Assets/Unity/CardSO/Player/StarterPool.asset.meta Assets/Unity/CardSO/Player/
cp -p /Users/ish/Git/rogue-deck/Assets/Unity/CardSO/Player/StarterPool.meta Assets/Unity/CardSO/Player/
cp -Rp /Users/ish/Git/rogue-deck/Assets/Unity/CardSO/Player/StarterPool Assets/Unity/CardSO/Player/
```

Compare SHA-256 manifests built from relative paths:

```bash
(cd /Users/ish/Git/rogue-deck/Assets/Unity/CardSO/Player && \
  find StarterPool StarterPool.asset StarterPool.asset.meta StarterPool.meta -type f -print0 | \
  sort -z | xargs -0 shasum -a 256) > /private/tmp/starter-pool-main.sha256
(cd Assets/Unity/CardSO/Player && \
  find StarterPool StarterPool.asset StarterPool.asset.meta StarterPool.meta -type f -print0 | \
  sort -z | xargs -0 shasum -a 256) > /private/tmp/starter-pool-worktree.sha256
diff -u /private/tmp/starter-pool-main.sha256 /private/tmp/starter-pool-worktree.sha256
```

Expected: `diff` exits 0 with no output. Also verify there are exactly 22 card
assets and 22 card metas.

- [x] **Step 4: Replace the deck YAML with the selected GUIDs**

Keep the existing deck asset GUID and script reference. Replace only `Entries`
with these exact references, all at count 1:

```yaml
  Entries:
  - Card: {fileID: 11400000, guid: 1648cb66e617c46b3af8d8b4a3df8e48, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: bc4af7ec1db1b4718a6d1fea385bac8f, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: 83e583e3340fe41899849253e3a4f7ea, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: a685cc5fad4dc4b9a89081c92b0ce6a5, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: a4405b5075938443eb66b5fce4e647ef, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: 2fd0a870fde6b4c708ab4b66b5b95d6a, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: c97e0e4e9e1f94900adf4c154153086d, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: fb91223f507d4452eaa8f8f43024de5c, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: 03b33173ab0614d5ebe10b260cf236b5, type: 2}
    Count: 1
  - Card: {fileID: 11400000, guid: eb92aa4928626402c9181114e4349844, type: 2}
    Count: 1
```

Append the fixed draw table and selected order from this plan to section 3 of
the current design spec so the authoritative content document retains the
result after this implementation plan is archived.

- [x] **Step 5: Import the assets and verify the structural test is now past the asset assertions**

Run the same focused Unity command from Step 2 with result files
`/private/tmp/random-starter-assets.xml` and
`/private/tmp/random-starter-assets.log`.

Expected: the structural contract passes; the byte-for-byte generated snapshot
test still fails because code generation has not run yet.

- [x] **Step 6: Commit the asset contract**

```bash
git add Assets/Tests/UnityEditMode/StarterDeckAssetCompositionTests.cs \
  Assets/Tests/UnityEditMode/StarterDeckAssetCompositionTests.cs.meta \
  Assets/Unity/CardSO/Player/StarterDeck.asset \
  Assets/Unity/CardSO/Player/StarterPool.asset \
  Assets/Unity/CardSO/Player/StarterPool.asset.meta \
  Assets/Unity/CardSO/Player/StarterPool.meta \
  Assets/Unity/CardSO/Player/StarterPool \
  docs/superpowers/specs/2026-07-30-random-starter-deck-design.md
git commit -m "feat: author fixed random starter deck assets"
```

---

### Task 2: Synchronize the pure C# starter-deck paths

**Files:**
- Modify: `Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs`
- Modify: `Assets/Core/Simulation/StarterDeck.cs`
- Modify: `Assets/Core/Tests/EditMode/StarterDeckTests.cs`
- Modify: `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs`

**Interfaces:**
- Consumes: the existing `StarterPoolSpecs` factory methods and `CardSpecMapper.ToDefinition(CardSpec)`.
- Produces: `StarterDeckSpecs.Build(): IReadOnlyList<CardSpec>` and `StarterDeck.Build(): IReadOnlyList<CardDefinition>` with the exact same fixed 10-card composition.
- Preserves: legacy individual factories such as `StarterDeck.Slash()` and `StarterDeckSpecs.Counter()` for focused rule tests that still use them.

- [x] **Step 1: Rewrite the composition tests first**

Change `StarterDeckTests` to assert the exact selected ID order, ten distinct
IDs, eight execution cards, and two intervention cards:

```csharp
private static readonly string[] SelectedIds =
{
    "probing_strike", "delayed_strike", "quick_cover", "early_guard",
    "breather", "hasten", "toxic_reclaim", "early_onset", "spore_veil",
    "last_drop"
};

[Test]
public void Build_has_the_fixed_ten_card_composition()
{
    var cards = StarterDeck.Build();
    CollectionAssert.AreEqual(SelectedIds, cards.Select(card => card.Id).ToArray());
    Assert.AreEqual(10, cards.Select(card => card.Id).Distinct().Count());
    Assert.AreEqual(8, cards.Count(card => card.Category == CardCategory.Execution));
    Assert.AreEqual(2, cards.Count(card => card.Category == CardCategory.Intervention));
}

[Test]
public void Every_intervention_card_cost_matches_its_action_cost()
{
    foreach (var card in StarterDeck.Build().Where(
                 card => card.Category == CardCategory.Intervention))
    {
        Assert.AreEqual(card.EnergyCost, card.InterventionAction.InterventionCost, card.Id);
    }
}
```

In `StarterDeckSpecEquivalenceTests`, update the composition assertion to 8
execution and 2 intervention cards, add an exact selected-ID assertion, and
make the legacy behavior tests map their explicit factory result rather than
searching the new `Build()` list:

```csharp
private static CardDefinition Def(CardSpec spec) =>
    CardSpecMapper.ToDefinition(spec);

// Examples:
Def(StarterDeckSpecs.QuickCut())
Def(StarterDeckSpecs.PullForward())
var counter = StarterDeckSpecs.Counter();
```

- [x] **Step 2: Run focused headless tests and verify RED**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~StarterDeckTests|FullyQualifiedName~StarterDeckSpecEquivalenceTests"
```

Expected: failures showing the legacy `slash`, `guard`, and 7:3 composition.

- [x] **Step 3: Make `StarterDeckSpecs.Build()` name the fixed pool selection**

Replace only `Build()` and its summary; keep all legacy factory methods:

```csharp
public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
{
    StarterPoolSpecs.ProbingStrike(),
    StarterPoolSpecs.DelayedStrike(),
    StarterPoolSpecs.QuickCover(),
    StarterPoolSpecs.EarlyGuard(),
    StarterPoolSpecs.Breather(),
    StarterPoolSpecs.Hasten(),
    StarterPoolSpecs.ToxicReclaim(),
    StarterPoolSpecs.EarlyOnset(),
    StarterPoolSpecs.SporeVeil(),
    StarterPoolSpecs.LastDrop()
};
```

- [x] **Step 4: Make `StarterDeck.Build()` map the selected specs**

Add `System.Linq` and `FateWeaver.Simulation.Authoring` imports, then replace
the hand-maintained list body:

```csharp
public static IReadOnlyList<CardDefinition> Build()
    => StarterDeckSpecs.Build()
        .Select(CardSpecMapper.ToDefinition)
        .ToList();
```

Keep the old named card helpers because other focused tests call them directly.
Update the class summary so it no longer claims a 7:3 composition.

- [x] **Step 5: Run focused and full headless tests**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~StarterDeckTests|FullyQualifiedName~StarterDeckSpecEquivalenceTests"
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo
```

Expected: focused tests pass. The full suite may still fail only in generated
snapshot/golden tests until Task 3 regenerates the export.

- [x] **Step 6: Commit the pure composition**

```bash
git add Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs \
  Assets/Core/Simulation/StarterDeck.cs \
  Assets/Core/Tests/EditMode/StarterDeckTests.cs \
  Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs
git commit -m "feat: sync core starter deck selection"
```

---

### Task 3: Regenerate and pin the headless snapshot

**Files:**
- Modify by generator: `Assets/Core/Simulation/Generated/GeneratedCards.cs`
- Modify: `Assets/Core/Tests/EditMode/GeneratedCardsTests.cs`
- Modify: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs`

**Interfaces:**
- Consumes: `StarterDeck.asset`, `StarterPool.asset`, and `CardCodeGenerator.Generate()`.
- Produces: `GeneratedCards.StarterDeck()` with 10 selected specs and `GeneratedCards.StarterPool()` with all 22 specs.

- [x] **Step 1: Change generated-content tests before regenerating**

Replace the legacy slash/counter test with:

```csharp
[Test]
public void Generated_snapshots_have_the_fixed_deck_and_complete_pool()
{
    CollectionAssert.AreEqual(
        new[]
        {
            "probing_strike", "delayed_strike", "quick_cover", "early_guard",
            "breather", "hasten", "toxic_reclaim", "early_onset",
            "spore_veil", "last_drop"
        },
        GeneratedCards.StarterDeck().Select(card => card.Id).ToArray());
    Assert.AreEqual(22, GeneratedCards.StarterPool().Count);
    Assert.AreEqual(
        22,
        GeneratedCards.StarterPool().Select(card => card.Id).Distinct().Count());
}
```

In `CardContentEquivalenceTests`, replace the three duplicated starter golden
arrays with one `GoldenStarterDeck` array and point all three starter golden
tests at it:

```csharp
private static readonly string[] GoldenStarterDeck =
{
    "breather;숨 고르기;Player;Intervention;1;0;change_execution_order:1:1;",
    "delayed_strike;늦춘 일격;Player;Execution;1;5;-;damage,5,-,-,FrontMost,-",
    "early_guard;앞선 대비;Player;Execution;1;4;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
    "early_onset;조기 발병;Player;Execution;2;3;-;apply_status,1,-,-,FrontMost,poison/Permanent:0/TargetEnemy|trigger_status,0,-,-,FrontMost,-",
    "hasten;재촉;Player;Intervention;1;0;change_execution_order:1:-1;",
    "last_drop;마지막 한 방울;Player;Execution;1;7;-;apply_status,1,NoFollowingCardOfSide { Side = Player },2,FrontMost,poison/Permanent:0/TargetEnemy",
    "probing_strike;견제타;Player;Execution;1;4;-;damage,4,-,-,FrontMost,-|apply_status,1,-,-,-,block/ThisTurn:0/Self",
    "quick_cover;빠른 엄호;Player;Execution;1;4;-;apply_status,4,-,-,FrontMost,block/ThisTurn:0/PartyBySelector",
    "spore_veil;포자막;Player;Execution;1;5;-;apply_status,1,-,-,FrontMost,poison/Permanent:0/TargetEnemy|apply_status,2,-,-,-,block/ThisTurn:0/Self",
    "toxic_reclaim;독성 환원;Player;Execution;1;5;-;consume_status,0,-,-,FrontMost,-|apply_status,1,-,-,FrontMost,poison/Permanent:0/TargetEnemy|apply_status,4,ConsumedStatusAtLeast { N = 1 },4,-,block/ThisTurn:0/Self"
};
```

- [x] **Step 2: Run generated tests and verify RED**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~GeneratedCardsTests|FullyQualifiedName~CardContentEquivalenceTests"
```

Expected: compilation or assertion failure because `GeneratedCards` still has
the legacy deck and no `StarterPool()` method.

- [x] **Step 3: Generate the C# snapshot from the two Unity assets**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod FateWeaver.Unity.Editor.CardCodeGenerator.Generate \
  -logFile /private/tmp/random-starter-codegen.log
```

Verify the log contains `Generated Assets/Core/Simulation/Generated/GeneratedCards.cs`
and does not contain `Card validation failed` or `Starter pool validation failed`.

- [x] **Step 4: Run focused tests and correct only literal signature mismatches**

Run the focused command from Step 2. Expected: all tests pass. If NUnit reports
a formatting difference in `Condition.ToString()`, copy the actual signature
verbatim into `GoldenStarterDeck`; do not change card values or the selected IDs
to accommodate a golden.

- [x] **Step 5: Run the Unity asset contract**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.StarterDeckAssetCompositionTests \
  -testResults /private/tmp/random-starter-generated.xml \
  -logFile /private/tmp/random-starter-generated.log
```

Expected: both structural and byte-for-byte generation tests pass.

- [x] **Step 6: Commit the generated snapshot and pinning tests**

```bash
git add Assets/Core/Simulation/Generated/GeneratedCards.cs \
  Assets/Core/Tests/EditMode/GeneratedCardsTests.cs \
  Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs
git commit -m "test: pin generated random starter deck"
```

---

### Task 4: Full verification and documentation closeout

**Result (2026-07-30):** Headless 395 passed, 0 failed, 0 skipped; Unity
EditMode 469 passed, 0 failed, 0 skipped (`/private/tmp/random-starter-full-editmode.xml`).
Implementation commits: `401af45`, `837d671`, `98d596b`.

**Files:**
- Move: `docs/superpowers/plans/2026-07-30-random-starter-deck.md` to `docs/superpowers/archive/plans/2026-07-30-random-starter-deck.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`

**Interfaces:**
- Consumes: all committed asset, core, generated, and test changes.
- Produces: a clean, fully verified feature branch and an archived implementation record.

- [x] **Step 1: Run the full headless suite**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo
```

Expected: all tests pass with zero failures and zero errors.

- [x] **Step 2: Run the full Unity EditMode suite**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults /private/tmp/random-starter-full-editmode.xml \
  -logFile /private/tmp/random-starter-full-editmode.log
```

Expected: result is `Passed`, with zero failed tests. Inspect `git status --short`
after Unity exits; stage only the intended test `.meta` and task files.

- [x] **Step 3: Verify the final content contract directly**

Run:

```bash
rg -n "public static IReadOnlyList<CardSpec> Starter(Deck|Pool)" \
  Assets/Core/Simulation/Generated/GeneratedCards.cs
find Assets/Unity/CardSO/Player/StarterPool -name '*.asset' | wc -l
git diff --check
git status --short
```

Expected: generated methods for both deck and pool, 22 card assets, no whitespace
errors, and no unrelated files.

- [x] **Step 4: Archive this completed plan and update both indexes**

Before moving the file, mark every checkbox complete and add the exact headless
and Unity pass counts plus the commit hashes to a short result block below this
header. Then:

```bash
mv docs/superpowers/plans/2026-07-30-random-starter-deck.md \
  docs/superpowers/archive/plans/2026-07-30-random-starter-deck.md
```

Remove the plan from `docs/superpowers/README.md` active plans and add
`[무작위 10장 시작 덱 구현 기록](plans/2026-07-30-random-starter-deck.md)`
under “상태 훅·독 시스템·시작 카드 풀” in
`docs/superpowers/archive/README.md`.

- [x] **Step 5: Commit documentation closeout**

```bash
git add docs/superpowers/README.md \
  docs/superpowers/archive/README.md \
  docs/superpowers/archive/plans/2026-07-30-random-starter-deck.md
git commit -m "docs: archive random starter deck implementation"
```

- [x] **Step 6: Final cleanliness check**

Run:

```bash
git status --short
git log --oneline -5
```

Expected: empty status. Report the selected 10 IDs, test counts, branch name,
and commits. Do not merge or remove the worktree until the user approves.
