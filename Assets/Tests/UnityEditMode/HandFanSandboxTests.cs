using System;
using System.Linq;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class HandFanSandboxTests
    {
        private CardPresentation[] Samples()
        {
            var content = UnityTestContent.Content();
            var korean = KoreanDescriptionCatalog.CreateDefault(content.Statuses);
            return new[] { "brace", "quick_cover" }.Select(id =>
                CardPresentation.FromDefinition(content.Cards.Get(id), korean)).ToArray();
        }

        [Test]
        public void Duplicate_cards_are_removed_by_selected_position_and_clear_restarts_sequence()
        {
            var state = new HandFanSandboxState(Samples());
            state.Add(); state.Add(); state.Add();
            Assert.That(state.Cards.Select(c => c.Id), Is.EqualTo(new[] { "brace", "quick_cover", "brace" }));
            state.Select(2); state.RemoveSelected();
            Assert.That(state.Cards.Select(c => c.Id), Is.EqualTo(new[] { "brace", "quick_cover" }));
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            Assert.That(state.CanRemove, Is.False);
            state.Clear(); state.Clear(); state.Add();
            Assert.That(state.Cards.Single().Id, Is.EqualTo("brace"));
        }

        [Test]
        public void Adding_preserves_selection_and_removing_middle_preserves_remaining_order()
        {
            var state = new HandFanSandboxState(Samples());
            state.Add(); state.Add(); state.Select(1); state.Add();
            Assert.That(state.SelectedIndex, Is.EqualTo(1));
            Assert.That(state.CanRemove, Is.True);
            state.RemoveSelected();
            Assert.That(state.Cards.Select(c => c.Id), Is.EqualTo(new[] { "brace", "brace" }));
            Assert.That(state.CanClear, Is.True);
        }

        [Test]
        public void Invalid_selection_and_empty_operations_are_safe()
        {
            var state = new HandFanSandboxState(Samples());
            state.RemoveSelected(); state.Clear(); state.Clear();
            Assert.That(state.Cards, Is.Empty);
            Assert.That(state.CanClear, Is.False);
            state.Add(); state.Select(0); state.Select(1); state.RemoveSelected();
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            Assert.That(state.Cards.Count, Is.EqualTo(1));
            state.Select(-2);
            Assert.That(state.CanRemove, Is.False);
        }

        [Test]
        public void Samples_are_copied_and_empty_sample_configuration_is_rejected()
        {
            var samples = Samples();
            var state = new HandFanSandboxState(samples);
            samples[0] = samples[1];
            state.Add();
            Assert.That(state.Cards.Single().Id, Is.EqualTo("brace"));
            Assert.Throws<ArgumentException>(() => new HandFanSandboxState(Array.Empty<CardPresentation>()));
        }

        [TestCase("brace", "quick_cover", true)]
        [TestCase("brace", "missing_sandbox_card", false)]
        [TestCase("brace", "", false)]
        [TestCase(null, null, false)]
        public void Source_loads_all_samples_or_reports_error_without_partial_output(string first, string second, bool success)
        {
            var root = new GameObject("SourceTest");
            try
            {
                var source = root.AddComponent<HandFanSandboxCardSource>();
                var serialized = new SerializedObject(source);
                var ids = serialized.FindProperty("_cardIds");
                ids.arraySize = first == null ? 0 : 2;
                if (first != null)
                {
                    ids.GetArrayElementAtIndex(0).stringValue = first;
                    ids.GetArrayElementAtIndex(1).stringValue = second;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(source.TryLoad(out var samples, out var error), Is.EqualTo(success));
                if (success)
                {
                    Assert.That(samples.Select(c => c.Id), Is.EqualTo(new[] { "brace", "quick_cover" }));
                    Assert.That(samples.All(c => !string.IsNullOrEmpty(c.Description)), Is.True);
                    Assert.That(error, Is.Empty);
                }
                else
                {
                    Assert.That(samples, Is.Empty);
                    Assert.That(error, Is.Not.Empty);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
