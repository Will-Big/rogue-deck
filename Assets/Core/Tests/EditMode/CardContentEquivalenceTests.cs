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
    /// Known cross-path divergences, intentionally NOT reconciled here (scheduled for P1-A cleanup):
    /// - pull_forward intervention effectValue: hand-coded StarterDeck has -2, specs/generated have -1.
    /// - push_back: absent from the hand-coded StarterDeck; present in specs ("밀어내기") and
    ///   generated ("미룸") with drifted names.
    /// Because of these, only the party prototype pair (currently equivalent) keeps a cross-path
    /// oracle test; the starter paths are each pinned against their own golden.
    ///
    /// When you change card content INTENTIONALLY, update the matching golden array in the same
    /// commit. If a golden test fails and you did not mean to change content, the migration broke
    /// something — do not update the golden.</summary>
    public class CardContentEquivalenceTests
    {
        internal static string Sig(CardDefinition d) => string.Join(";",
            d.Id, d.Name, d.Side, d.Type, d.Category, d.EnergyCost, d.BaseExecutionOrder,
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

        private static readonly string[] GoldenStarterDeckHandCoded =
        {
            "counter_stance;반격;Player;Attack;Execution;2;7;-;damage,4,PreviousExecutedCardIs { Side = Enemy, Type = Attack },9,-,-",
            "cover;엄호;Player;Defense;Execution;1;5;-;apply_status,2,AdjacentCardIs { Direction = Next, Side = Enemy, Type = Attack },7,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "pull_forward;앞당김;Player;Skill;Intervention;1;0;change_execution_order:1:-2;",
            "pull_forward;앞당김;Player;Skill;Intervention;1;0;change_execution_order:1:-2;",
            "quick_cut;찰나의 베기;Player;Attack;Execution;1;5;-;damage,2,FirstToTrigger { },8,-,-",
            "slash;베기;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "slash;베기;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "swap_positions;자리 교환;Player;Skill;Intervention;1;0;swap_execution_order:1:0;"
        };

        private static readonly string[] GoldenStarterDeckSpecs =
        {
            "counter_stance;반격;Player;Attack;Execution;2;7;-;damage,4,PreviousExecutedCardIs { Side = Enemy, Type = Attack },9,-,-",
            "cover;엄호;Player;Defense;Execution;1;5;-;apply_status,2,AdjacentCardIs { Direction = Next, Side = Enemy, Type = Attack },7,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "pull_forward;앞당김;Player;Skill;Intervention;1;0;change_execution_order:1:-1;",
            "push_back;밀어내기;Player;Skill;Intervention;1;0;change_execution_order:1:1;",
            "quick_cut;찰나의 베기;Player;Attack;Execution;1;5;-;damage,2,FirstToTrigger { },8,-,-",
            "slash;베기;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "slash;베기;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "swap_positions;자리 교환;Player;Skill;Intervention;1;0;swap_execution_order:1:0;"
        };

        private static readonly string[] GoldenGeneratedStarterDeck =
        {
            "counter_stance;반격;Player;Attack;Execution;2;7;-;damage,4,PreviousExecutedCardIs { Side = Enemy, Type = Attack },9,-,-",
            "cover;엄호;Player;Defense;Execution;1;5;-;apply_status,2,AdjacentCardIs { Direction = Next, Side = Enemy, Type = Attack },7,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "pull_forward;앞당김;Player;Skill;Intervention;1;0;change_execution_order:1:-1;",
            "push_back;미룸;Player;Skill;Intervention;1;0;change_execution_order:1:1;",
            "quick_cut;찰나의 베기;Player;Attack;Execution;1;5;-;damage,2,FirstToTrigger { },8,-,-",
            "slash;베기;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "slash;베기;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "swap_positions;자리 교환;Player;Skill;Intervention;1;0;swap_execution_order:1:0;"
        };

        private static readonly string[] GoldenPartyPrototypeHandCoded =
        {
            "fixture_all_block;[검증] 전체 방어;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/AllPartyMembers",
            "fixture_attack;[검증] 공격;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_attack;[검증] 공격;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_move_forward;[검증] 대형 이동;Player;Skill;Execution;1;5;-;move_formation,-1,-,-,-,-",
            "fixture_selected_block;[검증] 선택 방어;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "fixture_selected_block;[검증] 선택 방어;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self"
        };

        private static readonly string[] GoldenPartyPrototypeSpecs =
        {
            "fixture_all_block;[검증] 전체 방어;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/AllPartyMembers",
            "fixture_attack;[검증] 공격;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_attack;[검증] 공격;Player;Attack;Execution;1;4;-;damage,4,-,-,-,-",
            "fixture_move_forward;[검증] 대형 이동;Player;Skill;Execution;1;5;-;move_formation,-1,-,-,-,-",
            "fixture_selected_block;[검증] 선택 방어;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "fixture_selected_block;[검증] 선택 방어;Player;Defense;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self"
        };

        // --- golden pinning tests ---------------------------------------------------------

        [Test]
        public void Handcoded_starter_deck_matches_golden()
            => CollectionAssert.AreEqual(GoldenStarterDeckHandCoded, Sigs(StarterDeck.Build()));

        [Test]
        public void Starter_specs_match_golden()
            => CollectionAssert.AreEqual(
                GoldenStarterDeckSpecs,
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Generated_starter_deck_matches_golden()
            => CollectionAssert.AreEqual(
                GoldenGeneratedStarterDeck,
                Sigs(GeneratedCards.StarterDeck().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Handcoded_party_prototype_deck_matches_golden()
            => CollectionAssert.AreEqual(GoldenPartyPrototypeHandCoded, Sigs(PartyPrototypeDeck.Build()));

        [Test]
        public void Party_prototype_specs_match_golden()
            => CollectionAssert.AreEqual(
                GoldenPartyPrototypeSpecs,
                Sigs(PartyPrototypeDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        // --- cross-path oracle (only pair that is currently equivalent) --------------------

        [Test]
        public void Party_prototype_specs_match_handcoded_deck()
            => CollectionAssert.AreEqual(
                Sigs(PartyPrototypeDeck.Build()),
                Sigs(PartyPrototypeDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));
    }
}
