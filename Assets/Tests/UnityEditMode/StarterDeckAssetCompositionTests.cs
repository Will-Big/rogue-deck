using System.Collections.Generic;
using System.IO;
using System.Linq;
using FateWeaver.Unity;
using FateWeaver.Unity.Editor;
using NUnit.Framework;
using UnityEditor;

namespace FateWeaver.Tests.UnityEditMode
{
    public class StarterDeckAssetCompositionTests
    {
        private const string PoolPath = "Assets/Unity/CardSO/Player/StarterPool.asset";
        private const string DeckPath = "Assets/Unity/CardSO/Player/StarterDeck.asset";

        private static readonly string[] ExpectedPoolIds =
        {
            "vanguard_slash", "parry_strike", "hasten", "probing_strike", "quick_cover",
            "delay", "delayed_strike", "early_guard", "crossover", "riposte", "foresight",
            "breather", "venom_thrust", "last_drop", "spore_veil", "spread_culture",
            "toxic_reclaim", "condensed_burst", "distill", "early_onset", "stable_culture",
            "posthumous_spread"
        };

        private static readonly string[] SelectedIds =
        {
            "probing_strike", "delayed_strike", "quick_cover", "early_guard", "breather",
            "hasten", "toxic_reclaim", "early_onset", "spore_veil", "last_drop"
        };

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
    }
}
