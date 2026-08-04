using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class CardTargetSnapshotTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new ApplyStatusHandler());
            return effects;
        }

        [Test]
        public void Capture_retains_the_original_enemy_object_references_for_the_card()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var front = new Enemy("front", 10);
            var back = new Enemy("back", 10);
            state.Enemies.Add(front);
            state.Enemies.Add(back);
            var card = new ExecutionCardInstance(new CardDefinition(
                "snapshot", "Snapshot", Side.Player, 1, new EffectData[0]));
            var key = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontTwo);

            var snapshot = CardTargetSnapshot.Capture(state, card, new[] { key });
            state.Enemies.Reverse();

            CollectionAssert.AreEqual(new[] { front, back }, snapshot.EnemyTargets(key));
        }

        [Test]
        public void Ownerless_self_resolves_the_only_living_ally_without_a_formation_fallback()
        {
            var state = new CombatState(TestContent.Statuses());
            var only = state.AddSoloPlayer(10);
            var card = new ExecutionCardInstance(new CardDefinition(
                "ownerless_self", "Ownerless Self", Side.Player, 1, new EffectData[0]));
            var key = new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self);

            var snapshot = CardTargetSnapshot.Capture(state, card, new[] { key });

            Assert.IsNull(card.CancellationReason);
            CollectionAssert.AreEqual(new[] { only }, snapshot.PartyTargets(key));
        }

        [Test]
        public void Capture_uses_the_explicit_player_enemy_target_for_the_card_snapshot()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var front = new Enemy("front", 10);
            var selected = new Enemy("selected", 10);
            state.Enemies.Add(front);
            state.Enemies.Add(selected);
            var card = new ExecutionCardInstance(new CardDefinition(
                "legacy_target", "Legacy Target", Side.Player, 1, new EffectData[0]))
            {
                TargetId = selected.Id
            };
            var key = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne);

            var snapshot = CardTargetSnapshot.Capture(state, card, new[] { key }, new[] { key });

            CollectionAssert.AreEqual(new[] { selected }, snapshot.EnemyTargets(key));
        }

        [Test]
        public void Explicit_target_id_does_not_override_an_authored_positional_selector()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("front", 10));
            state.Enemies.Add(new Enemy("middle", 10));
            state.Enemies.Add(new Enemy("explicit", 10));
            var effect = new EffectData(EffectKeys.Damage, 2)
            {
                TargetSelector = TargetSelector.FrontTwo
            };
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition(
                "positional", "Positional", Side.Player, 1, new[] { effect }))
            {
                OwnerId = CombatState.SoloPlayerId,
                TargetId = "explicit"
            });

            new TurnResolver(Effects()).Resolve(state, 0);

            Assert.AreEqual(8, state.Enemies[0].Hp);
            Assert.AreEqual(8, state.Enemies[1].Hp);
            Assert.AreEqual(10, state.Enemies[2].Hp);
        }

        [Test]
        public void Capture_cancels_conflicting_ranges_for_the_same_faction_before_effects()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("enemy", 10));
            var card = new ExecutionCardInstance(new CardDefinition(
                "conflict", "Conflict", Side.Player, 1, new EffectData[0]));

            CardTargetSnapshot.Capture(state, card, new[]
            {
                new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne),
                new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.BackOne)
            });

            Assert.AreEqual(CardCancellationReason.NoValidTarget, card.CancellationReason);
        }

        [Test]
        public void Ownerless_self_cancels_when_multiple_living_allies_are_ambiguous()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Add(new PartyMember("a", "A", 10));
            state.Party.Add(new PartyMember("b", "B", 10));
            var card = new ExecutionCardInstance(new CardDefinition(
                "ambiguous_self", "Ambiguous Self", Side.Player, 1, new EffectData[0]));
            var key = new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self);

            CardTargetSnapshot.Capture(state, card, new[] { key });

            Assert.AreEqual(CardCancellationReason.NoValidTarget, card.CancellationReason);
        }

        [Test]
        public void Damage_handler_declares_the_default_enemy_front_target()
        {
            var effect = new EffectData(EffectKeys.Damage, 2);
            var card = new CardDefinition("strike", "Strike", Side.Player, 1, new[] { effect });

            var key = new DamageHandler().TargetFor(card, effect);

            Assert.AreEqual(
                new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne),
                key.Value);
        }

        [Test]
        public void Later_effect_does_not_promote_a_new_target_after_snapshot_target_dies()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("a", 2));
            state.Enemies.Add(new Enemy("b", 2));
            state.Enemies.Add(new Enemy("c", 2));
            var damage = new EffectData(EffectKeys.Damage, 2)
            {
                TargetSelector = TargetSelector.FrontTwo
            };
            var poison = EffectData.ApplyStatus(
                StatusKeys.Poison,
                StatusApplyTarget.TargetEnemy,
                count: 1) with
            {
                TargetSelector = TargetSelector.FrontTwo
            };
            var card = new ExecutionCardInstance(new CardDefinition(
                "snapshot_kill", "Snapshot Kill", Side.Player, 1, new[] { damage, poison }))
            {
                OwnerId = CombatState.SoloPlayerId
            };
            state.Zone.Add(card);

            new TurnResolver(Effects(), new StatusRegistry())
                .Resolve(state, 0);

            Assert.AreEqual(2, state.Enemies[2].Hp);
            Assert.IsFalse(state.Enemies[2].Statuses.Has(StatusKeys.Poison));
        }
    }
}
