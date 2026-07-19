# Status Key Dropdown Authoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Make ApplyStatusSpec.Status select a registered status key through a Unity Inspector dropdown without changing the serialized StatusKeyRef.Id schema.

**Architecture:** StatusRegistry remains the explicit source of registered runtime statuses and exposes an ID-sorted key snapshot through AuthoringContext. A testable Unity Editor selection model combines those keys with KoreanDescriptionCatalog labels; StatusKeyRefDrawer only writes a selected Id and preserves empty or unknown strings until an author deliberately changes them.

**Tech Stack:** Unity 6000.5.2f1, C# 9, NUnit 3, .NET 6 headless test harness, Unity EditMode batch runner.

## Global Constraints

- FateWeaver.Core must not reference UnityEngine.
- Keep StatusKeyRef.Id as the serialized string; do not migrate existing CardSO YAML.
- Use explicit status behavior/description registration only; do not use reflection or a second hardcoded Editor key list.
- Existing unknown status-key validation remains the authoritative error path and must not be weakened.
- Unknown and empty serialized values must not be rewritten during Inspector repaint.
- This is editor-only UX: do not alter combat rules, timelines, random-number use, status lifetimes, values, or targets.
- Use apply_patch for source and test edits. Run Unity batchmode only with -projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck and store logs/results in /private/tmp.
- Do not stage generated .DS_Store, scene, prefab, ScriptableObject, or project-setting changes from manual Unity validation.

---

## File map

- Assets/Core/Status/StatusRegistry.cs: expose an ID-sorted read-only snapshot of explicitly registered keys.
- Assets/Core/Simulation/Authoring/AuthoringContext.cs: make that snapshot available to authoring consumers.
- Assets/Core/Simulation/Descriptions/StatusDescriptionRegistry.cs: add non-throwing display-name lookup.
- Assets/Core/Tests/EditMode/AuthoringValidationTests.cs: lock down default registered-key exposure while retaining unknown-key validation.
- Assets/Unity/Editor/StatusKeyDropdownOptions.cs: testable editor selection model and labels.
- Assets/Unity/Editor/StatusKeyRefDrawer.cs: Inspector popup for every serialized StatusKeyRef.
- Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef: reference the editor assembly for selection-model tests.
- Assets/Tests/UnityEditMode/StatusKeyDropdownOptionsTests.cs: verify known, empty, and unknown option behavior without GUI event simulation.

### Task 1: Expose registered status keys for authoring

**Files:**
- Modify: Assets/Core/Status/StatusRegistry.cs
- Modify: Assets/Core/Simulation/Authoring/AuthoringContext.cs
- Modify: Assets/Core/Simulation/Descriptions/StatusDescriptionRegistry.cs
- Modify: Assets/Core/Tests/EditMode/AuthoringValidationTests.cs

**Interfaces:**
- Produces: IReadOnlyList<StatusKey> StatusRegistry.RegisteredKeys sorted ordinally by StatusKey.Id.
- Produces: IReadOnlyList<StatusKey> AuthoringContext.RegisteredStatusKeys.
- Produces: bool StatusDescriptionRegistry.TryResolve(StatusKey key, out string displayName).
- Preserves: AuthoringContext.HasStatus and unknown-key validation behavior.

- [ ] **Step 1: Write the failing registered-key test**

Add this test to AuthoringValidationTests after Valid_starter_content_passes:

    [Test]
    public void Default_context_exposes_registered_status_keys_in_id_order()
    {
        var keys = AuthoringContext.Default().RegisteredStatusKeys;

        CollectionAssert.AreEqual(new[]
        {
            StatusKeys.Block,
            StatusKeys.Haste,
            StatusKeys.RewardNullified,
            StatusKeys.Slow,
            StatusKeys.Stun,
            StatusKeys.Vulnerable
        }, keys);
    }

Keep Unknown_status_key_fails unchanged: it proves the dropdown does not turn unknown data into a silently accepted value.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

    DOTNET_CLI_HOME=/private/tmp/status-key-dropdown-dotnet \
      /usr/local/share/dotnet/x64/dotnet test \
      Tests/Headless/FateWeaver.Tests.Headless.csproj --no-restore \
      --filter FullyQualifiedName~AuthoringValidationTests

Expected: compilation fails because AuthoringContext has no RegisteredStatusKeys member.

- [ ] **Step 3: Add read-only registry and description lookup APIs**

In StatusRegistry.cs, add using System; and using System.Linq;, then add this property inside StatusRegistry:

    public IReadOnlyList<StatusKey> RegisteredKeys
        => _behaviors.Keys
            .OrderBy(key => key.Id, StringComparer.Ordinal)
            .ToArray();

This returns an independent snapshot, so callers cannot mutate the behavior dictionary or rely on insertion order.

In AuthoringContext.cs, add using System.Collections.Generic; and this property next to HasStatus:

    public IReadOnlyList<StatusKey> RegisteredStatusKeys => _statuses.RegisteredKeys;

In StatusDescriptionRegistry.cs, add this method without changing Resolve:

    public bool TryResolve(StatusKey key, out string displayName)
        => _names.TryGetValue(key, out displayName);

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the command from Step 2.

Expected: all AuthoringValidationTests pass, including the pre-existing unknown-key failure test.

- [ ] **Step 5: Commit the core authoring API**

    git add Assets/Core/Status/StatusRegistry.cs \
      Assets/Core/Simulation/Authoring/AuthoringContext.cs \
      Assets/Core/Simulation/Descriptions/StatusDescriptionRegistry.cs \
      Assets/Core/Tests/EditMode/AuthoringValidationTests.cs
    git commit -m "feat(authoring): expose registered status keys"

### Task 2: Model and draw the status-key dropdown

**Files:**
- Create: Assets/Unity/Editor/StatusKeyDropdownOptions.cs
- Create: Assets/Unity/Editor/StatusKeyRefDrawer.cs
- Modify: Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef
- Create: Assets/Tests/UnityEditMode/StatusKeyDropdownOptionsTests.cs

**Interfaces:**
- Consumes: AuthoringContext.RegisteredStatusKeys and StatusDescriptionRegistry.TryResolve from Task 1.
- Produces: StatusKeyDropdownOptions.CreateDefault(string currentId) and StatusKeyRefDrawer.
- Contract: labels are "방어 (block)" for a described key, "(상태 선택)" for empty, and "Unknown: legacy_key" for a nonempty unregistered value; unknown IDs remain selected and unchanged until the author chooses another entry.

- [ ] **Step 1: Add the Editor assembly reference and write failing model tests**

In Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef, add "FateWeaver.Unity.Editor" to references after "FateWeaver.Unity".

Create StatusKeyDropdownOptionsTests.cs:

    using NUnit.Framework;
    using FateWeaver.Core.Status;
    using FateWeaver.Simulation.Descriptions;
    using FateWeaver.Unity.Editor;

    namespace FateWeaver.Tests.UnityEditMode
    {
        public class StatusKeyDropdownOptionsTests
        {
            private static StatusDescriptionRegistry Descriptions()
            {
                var descriptions = new StatusDescriptionRegistry();
                descriptions.Register(StatusKeys.Block, "방어");
                return descriptions;
            }

            [Test]
            public void Known_key_uses_registered_korean_label_and_id()
            {
                var model = StatusKeyDropdownOptions.Create(
                    StatusKeys.Block.Id,
                    new[] { StatusKeys.Block, StatusKeys.Slow },
                    Descriptions());

                Assert.AreEqual(1, model.SelectedIndex);
                Assert.AreEqual("(상태 선택)", model.Options[0].Label);
                Assert.AreEqual("방어 (block)", model.Options[1].Label);
                Assert.AreEqual(StatusKeys.Block.Id, model.Options[1].Id);
                Assert.AreEqual("slow", model.Options[2].Label);
            }

            [Test]
            public void Unknown_key_is_preserved_as_the_selected_option()
            {
                var model = StatusKeyDropdownOptions.Create(
                    "legacy_block",
                    new[] { StatusKeys.Block },
                    Descriptions());

                Assert.AreEqual(0, model.SelectedIndex);
                Assert.AreEqual("Unknown: legacy_block", model.Options[0].Label);
                Assert.AreEqual("legacy_block", model.Options[0].Id);
                Assert.AreEqual("(상태 선택)", model.Options[1].Label);
            }

            [Test]
            public void Empty_key_selects_the_placeholder_without_writing_a_value()
            {
                var model = StatusKeyDropdownOptions.Create(
                    string.Empty,
                    new[] { StatusKeys.Block },
                    Descriptions());

                Assert.AreEqual(0, model.SelectedIndex);
                Assert.AreEqual("(상태 선택)", model.Options[0].Label);
                Assert.AreEqual(string.Empty, model.Options[0].Id);
            }
        }
    }

- [ ] **Step 2: Run the focused Unity EditMode test and verify RED**

Run:

    /Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
      -batchmode -nographics \
      -projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck \
      -runTests -testPlatform EditMode \
      -testFilter FateWeaver.Tests.UnityEditMode.StatusKeyDropdownOptionsTests \
      -testResults /private/tmp/status-key-dropdown-options-red.xml \
      -logFile /private/tmp/status-key-dropdown-options-red.log

Expected: compilation fails because StatusKeyDropdownOptions does not exist.

- [ ] **Step 3: Implement the selection model**

Create Assets/Unity/Editor/StatusKeyDropdownOptions.cs:

    using System.Collections.Generic;
    using System.Linq;
    using FateWeaver.Core.Status;
    using FateWeaver.Simulation.Authoring;
    using FateWeaver.Simulation.Descriptions;

    namespace FateWeaver.Unity.Editor
    {
        public sealed class StatusKeyDropdownOption
        {
            public StatusKeyDropdownOption(string id, string label)
            {
                Id = id;
                Label = label;
            }

            public string Id { get; }
            public string Label { get; }
        }

        public sealed class StatusKeyDropdownModel
        {
            public StatusKeyDropdownModel(
                IReadOnlyList<StatusKeyDropdownOption> options,
                int selectedIndex)
            {
                Options = options;
                SelectedIndex = selectedIndex;
            }

            public IReadOnlyList<StatusKeyDropdownOption> Options { get; }
            public int SelectedIndex { get; }
        }

        public static class StatusKeyDropdownOptions
        {
            public static StatusKeyDropdownModel CreateDefault(string currentId)
                => Create(
                    currentId,
                    AuthoringContext.Default().RegisteredStatusKeys,
                    KoreanDescriptionCatalog.Default.Statuses);

            public static StatusKeyDropdownModel Create(
                string currentId,
                IReadOnlyList<StatusKey> registeredKeys,
                StatusDescriptionRegistry descriptions)
            {
                var options = new List<StatusKeyDropdownOption>();
                var selectedIndex = 0;
                var known = registeredKeys.Any(key => key.Id == currentId);
                if (!string.IsNullOrEmpty(currentId) && !known)
                {
                    options.Add(new StatusKeyDropdownOption(
                        currentId, "Unknown: " + currentId));
                }
                else
                {
                    options.Add(new StatusKeyDropdownOption(string.Empty, "(상태 선택)"));
                }

                if (!string.IsNullOrEmpty(currentId) && !known)
                {
                    options.Add(new StatusKeyDropdownOption(string.Empty, "(상태 선택)"));
                }

                foreach (var key in registeredKeys)
                {
                    var label = descriptions.TryResolve(key, out var displayName)
                        ? displayName + " (" + key.Id + ")"
                        : key.Id;
                    options.Add(new StatusKeyDropdownOption(key.Id, label));
                    if (known && key.Id == currentId)
                    {
                        selectedIndex = options.Count - 1;
                    }
                }

                return new StatusKeyDropdownModel(options, selectedIndex);
            }
        }
    }

The model accepts injected keys and descriptions for focused tests. The production drawer calls only CreateDefault, which obtains candidates from the explicit runtime registry rather than duplicating IDs in Editor code.

- [ ] **Step 4: Implement the property drawer**

Create Assets/Unity/Editor/StatusKeyRefDrawer.cs:

    using System.Linq;
    using FateWeaver.Simulation.Authoring;
    using UnityEditor;
    using UnityEngine;

    namespace FateWeaver.Unity.Editor
    {
        [CustomPropertyDrawer(typeof(StatusKeyRef))]
        public sealed class StatusKeyRefDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var id = property.FindPropertyRelative(nameof(StatusKeyRef.Id));
                if (id == null || id.propertyType != SerializedPropertyType.String)
                {
                    EditorGUI.PropertyField(position, property, label, includeChildren: true);
                    return;
                }

                var model = StatusKeyDropdownOptions.CreateDefault(id.stringValue);
                var labels = model.Options.Select(option => option.Label).ToArray();
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.showMixedValue = id.hasMultipleDifferentValues;
                var picked = EditorGUI.Popup(position, label.text, model.SelectedIndex, labels);
                EditorGUI.showMixedValue = false;

                if (!id.hasMultipleDifferentValues && picked != model.SelectedIndex)
                {
                    id.stringValue = model.Options[picked].Id;
                }

                EditorGUI.EndProperty();
            }
        }
    }

The selected unknown option never writes on repaint because picked equals model.SelectedIndex. Selecting a registered option is the only path that replaces the old unknown value.

- [ ] **Step 5: Run the focused Unity EditMode test and verify GREEN**

Run the command from Step 2, replacing both red result/log file names with green.

Expected: exit code 0; the XML reports three passing StatusKeyDropdownOptionsTests and zero failures.

- [ ] **Step 6: Commit the Editor UX**

    git add Assets/Unity/Editor/StatusKeyDropdownOptions.cs \
      Assets/Unity/Editor/StatusKeyRefDrawer.cs \
      Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef \
      Assets/Tests/UnityEditMode/StatusKeyDropdownOptionsTests.cs
    git commit -m "feat(unity): add status key authoring dropdown"

### Task 3: Verify the completed branch and update the implementation record

**Files:**
- Modify: docs/superpowers/plans/2026-07-19-p0b2-implementation-record.md

**Interfaces:**
- Consumes: completed Tasks 1–2 and existing P0-B2 verification record.
- Produces: an explicit record that the follow-up changes only authoring UX and retain the P0-B2 pre-merge Play gate.

- [ ] **Step 1: Add the follow-up verification entry**

Append this dated section to the implementation record:

    ## 2026-07-20 — Status key dropdown follow-up

    - StatusKeyRef.Id remains the serialized schema; registered runtime statuses now drive the Inspector dropdown.
    - Existing unknown values render as Unknown: <key> and continue to fail AuthoringValidator; they are never rewritten on repaint.
    - This change does not alter combat rules or the pending user Play validation gate for the branch.

- [ ] **Step 2: Run headless regression**

    DOTNET_CLI_HOME=/private/tmp/status-key-dropdown-dotnet \
      /usr/local/share/dotnet/x64/dotnet test \
      Tests/Headless/FateWeaver.Tests.Headless.csproj --no-restore

Expected: all tests pass with zero failures.

- [ ] **Step 3: Run Unity EditMode regression**

    /Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
      -batchmode -nographics \
      -projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck \
      -runTests -testPlatform EditMode \
      -testResults /private/tmp/status-key-dropdown-editmode.xml \
      -logFile /private/tmp/status-key-dropdown-editmode.log

Expected: Unity exits 0; the result XML reports zero failures and the log has no compilation errors.

- [ ] **Step 4: Verify the authored-data schema and worktree scope**

    rg -n -U "Status:\n\\s+Id: block" Assets/Unity/CardSO/Player/guard.asset
    git diff master...HEAD --check
    git status --short

Expected: Guard still serializes Status.Id: block; whitespace check is clean; only the implementation-record edit is unstaged before the next step, with no .DS_Store or Unity-generated data staged.

- [ ] **Step 5: Commit the record**

    git add docs/superpowers/plans/2026-07-19-p0b2-implementation-record.md
    git commit -m "docs: record status key dropdown follow-up"

- [ ] **Step 6: Confirm the final handoff state**

    git status --short --branch
    git log --oneline master..HEAD

Expected: worktree is clean; the branch contains the follow-up commits and remains unmerged until the user approves merge after direct Unity validation.

