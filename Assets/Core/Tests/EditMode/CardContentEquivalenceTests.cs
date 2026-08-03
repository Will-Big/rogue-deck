using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    /// <summary>Golden content pinning for the card authoring paths (백로그 §11, P0-B prep).
    /// Purpose: prevent content drift during the P0-B structural migration — each authoring source's
    /// current card content is pinned as sorted golden signature lists. Signatures are shape-agnostic
    /// so the payload migration (Task 2) keeps passing as long as content is unchanged.
    ///
    /// 남은 두 저작 경로(runtime, specs)는 내용이 같고 교차 신탁 테스트가 그것을 잠근다.
    /// 계획 3b가 코드 생성 경로(GeneratedCards)를 지우면서 세 번째 축이 사라졌다 — 카드 규칙의
    /// 원본은 이제 Content/Cards/*.json이고, 남은 C# 스펙은 계획 3d가 지운다.
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
                    + ":" + d.InterventionAction.EffectValue
                    + ":" + (d.InterventionAction.TargetSide?.ToString() ?? "-")
                    + ":" + d.InterventionAction.RequireAdjacentTargets,
            string.Join("|", d.Effects.Select(EffectSig)));

        private static string EffectSig(EffectData e) => string.Join(",",
            e.Key, e.EffectValue,
            e.Condition == null ? "-" : e.Condition.ToString(),
            e.SuccessEffectValue?.ToString() ?? "-",
            e.TargetSelector?.ToString() ?? "-",
            e.SkipOnBasic,
            PayloadSig(e));

        private static string PayloadSig(EffectData e)
            => e.Payload switch
            {
                // Task 4: lifetime kind/count and the suppress-marker key no longer ride on the
                // payload — they're derived from the status's StatusContentCatalog entry at apply
                // time, so a payload-only signature can no longer see them (nor should it: the whole
                // point is that the card no longer carries that information).
                ApplyStatusPayload p => p.Key + "/" + p.Target,
                ConsumeStatusPayload p => p.Key + "/" + p.MaxAmount + "/" + p.DamageBonusPerConsumed,
                TriggerStatusPayload p => p.Key.ToString(),
                _ => "-"
            };

        private static List<string> Sigs(IEnumerable<CardDefinition> defs)
            => defs.Select(Sig).OrderBy(s => s).ToList();

        // --- goldens (sorted; captured from the current code, verbatim) -----------------

        private static readonly string[] GoldenStarterDeck =
        {
            "breather;숨 고르기;Player;Intervention;1;0;change_execution_order:1:1:Player:False;",
            "delayed_strike;늦춘 일격;Player;Execution;1;5;-;damage,5,-,-,FrontMost,False,-",
            "early_guard;앞선 대비;Player;Execution;1;4;-;apply_status,4,-,-,-,False,block/Self",
            "early_onset;조기 발병;Player;Execution;2;3;-;apply_status,1,-,-,FrontMost,False,poison/TargetEnemy|trigger_status,0,-,-,FrontMost,False,poison",
            "hasten;재촉;Player;Intervention;1;0;change_execution_order:1:-1:Player:False;",
            "last_drop;마지막 한 방울;Player;Execution;1;7;-;apply_status,1,NoFollowingCardOfSide { Side = Player },2,FrontMost,False,poison/TargetEnemy",
            "probing_strike;견제타;Player;Execution;1;4;-;damage,4,-,-,FrontMost,False,-|apply_status,1,-,-,-,False,block/Self",
            "quick_cover;빠른 엄호;Player;Execution;1;4;-;apply_status,4,-,-,FrontMost,False,block/PartyBySelector",
            "spore_veil;포자막;Player;Execution;1;5;-;apply_status,1,-,-,FrontMost,False,poison/TargetEnemy|apply_status,2,-,-,-,False,block/Self",
            "toxic_reclaim;독성 환원;Player;Execution;1;5;-;consume_status,0,-,-,FrontMost,False,poison/1/0|apply_status,1,-,-,FrontMost,False,poison/TargetEnemy|apply_status,4,ConsumedStatusAtLeast { N = 1 },4,-,True,block/Self"
        };

        private static readonly string[] GoldenPartyPrototypeHandCoded =
        {
            "fixture_all_block;[검증] 전체 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,False,block/AllPartyMembers",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,False,-",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,False,-",
            "fixture_move_forward;[검증] 대형 이동;Player;Execution;1;5;-;move_formation,-1,-,-,-,False,-",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,False,block/Self",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,False,block/Self"
        };

        private static readonly string[] GoldenPartyPrototypeSpecs =
        {
            "fixture_all_block;[검증] 전체 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,False,block/AllPartyMembers",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,False,-",
            "fixture_attack;[검증] 공격;Player;Execution;1;4;-;damage,4,-,-,-,False,-",
            "fixture_move_forward;[검증] 대형 이동;Player;Execution;1;5;-;move_formation,-1,-,-,-,False,-",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,False,block/Self",
            "fixture_selected_block;[검증] 선택 방어;Player;Execution;1;5;-;apply_status,4,-,-,-,False,block/Self"
        };

        // --- golden pinning tests ---------------------------------------------------------

        [Test]
        public void Runtime_starter_deck_matches_golden()
            => CollectionAssert.AreEqual(GoldenStarterDeck, Sigs(StarterDeck.Build()));

        [Test]
        public void Starter_specs_match_golden()
            => CollectionAssert.AreEqual(
                GoldenStarterDeck,
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

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
        public void Starter_specs_match_runtime_deck()
            => CollectionAssert.AreEqual(
                Sigs(StarterDeck.Build()),
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));
    }
}
