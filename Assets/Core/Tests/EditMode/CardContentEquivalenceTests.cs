using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Generated;

namespace FateWeaver.Tests
{
    /// <summary>Golden content pinning for the card authoring paths (백로그 §11, P0-B prep).
    /// Purpose: prevent content drift during the P0-B structural migration — each authoring source's
    /// current card content is pinned as sorted golden signature lists. Signatures are shape-agnostic
    /// so the payload migration (Task 2) keeps passing as long as content is unchanged.
    ///
    /// All three starter paths (hand-coded, specs, generated) are content-equivalent and pinned by
    /// cross-path oracle tests, so a future divergence fails instead of being documented.
    ///
    /// When you change card content INTENTIONALLY, update the matching golden array in the same
    /// commit. If a golden test fails and you did not mean to change content, the migration broke
    /// something — do not update the golden.</summary>
    public class CardContentEquivalenceTests
    {
        internal static string Sig(CardDefinition d) => string.Join(";",
            d.Id, d.Name, d.Side, d.Category, d.EnergyCost, d.BaseExecutionOrder,
            d.InterventionAction == null
                ? "-"
                : d.InterventionAction.Key + ":" + d.InterventionAction.InterventionCost
                    + ":" + d.InterventionAction.EffectValue,
            string.Join("|", d.Effects.Select(EffectSig)));

        private static string EffectSig(EffectData e) => string.Join(",",
            e.Key, e.EffectValue,
            e.Condition == null ? "-" : e.Condition.ToString(),
            e.SuccessEffectValue?.ToString() ?? "-",
            e.TargetSelector?.ToString() ?? "-",
            StatusSig(e));

        private static string StatusSig(EffectData e)
            => !(e.Payload is ApplyStatusPayload p)
                ? "-"
                : p.Key + "/" + p.Lifetime.Kind + ":" + p.Lifetime.Count + "/" + p.Target;

        private static List<string> Sigs(IEnumerable<CardDefinition> defs)
            => defs.Select(Sig).OrderBy(s => s).ToList();

        // --- goldens (sorted; captured from the current code, verbatim) -----------------

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

        private static readonly string[] GoldenPartyPrototypeHandCoded =
        {
            "fixture_all_block;[검증] 전체 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/AllPartyMembers",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_move_forward;[검증] 대형 이동;Player;Execution;1;5;-;move_formation,-1,-,-,-,-",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self"
        };

        private static readonly string[] GoldenPartyPrototypeSpecs =
        {
            "fixture_all_block;[검증] 전체 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/AllPartyMembers",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_move_forward;[검증] 대형 이동;Player;Execution;1;5;-;move_formation,-1,-,-,-,-",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self"
        };

        // --- golden pinning tests ---------------------------------------------------------

        [Test]
        public void Handcoded_starter_deck_matches_golden()
            => CollectionAssert.AreEqual(GoldenStarterDeck, Sigs(StarterDeck.Build()));

        [Test]
        public void Starter_specs_match_golden()
            => CollectionAssert.AreEqual(
                GoldenStarterDeck,
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Generated_starter_deck_matches_golden()
            => CollectionAssert.AreEqual(
                GoldenStarterDeck,
                Sigs(GeneratedCards.StarterDeck().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Handcoded_party_prototype_deck_matches_golden()
            => CollectionAssert.AreEqual(GoldenPartyPrototypeHandCoded, Sigs(PartyPrototypeDeck.Build()));

        [Test]
        public void Party_prototype_specs_match_golden()
            => CollectionAssert.AreEqual(
                GoldenPartyPrototypeSpecs,
                Sigs(PartyPrototypeDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        // --- cross-path oracles (pin all three equivalent starter paths) -------------------

        [Test]
        public void Party_prototype_specs_match_handcoded_deck()
            => CollectionAssert.AreEqual(
                Sigs(PartyPrototypeDeck.Build()),
                Sigs(PartyPrototypeDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Starter_specs_match_handcoded_deck()
            => CollectionAssert.AreEqual(
                Sigs(StarterDeck.Build()),
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Generated_starter_deck_matches_handcoded_deck()
            => CollectionAssert.AreEqual(
                Sigs(StarterDeck.Build()),
                Sigs(GeneratedCards.StarterDeck().Select(CardSpecMapper.ToDefinition)));
    }
}
