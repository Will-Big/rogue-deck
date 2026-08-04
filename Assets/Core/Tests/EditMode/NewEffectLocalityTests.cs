using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests
{
    /// <summary>P0-B 완료 기준: a brand-new effect ships as one package (handler + spec + description
    /// + registration) without touching any central enum/mapper. Everything Heal lives in this file.</summary>
    public class NewEffectLocalityTests
    {
        private static readonly EffectKey HealKey = new EffectKey("heal");

        private sealed class HealHandler : IEffectHandler
        {
            public EffectKey Key => HealKey;

            public CardTargetKey? TargetFor(CardDefinition card, EffectData effect) => null;

            public void Apply(EffectContext ctx)
            {
                var member = PartyTargeting.LivingById(ctx.State, ctx.Card.OwnerId);
                if (member == null)
                {
                    ctx.Cancel(CardCancellationReason.NoValidTarget);
                    return;
                }

                member.Hp += ctx.EffectValue;
            }
        }

        private sealed class HealSpec : EffectSpec
        {
            public int Value;

            public override EffectKey Key => HealKey;

            public override EffectData ToEffectData() => ApplyCondition(new EffectData(Key, Value));
        }

        private sealed class HealDescriptionHandler : IEffectDescriptionHandler
        {
            public EffectKey Key => HealKey;

            public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
                => new EffectDescriptionFragment(context.SelfTarget(), "치유 " + effectValue);
        }

        [Test]
        public void Heal_spec_maps_and_validates_without_central_changes()
        {
            var spec = new HealSpec { Value = 3 };
            var effect = spec.ToEffectData();
            Assert.AreEqual(HealKey, effect.Key);
            Assert.AreEqual(3, effect.EffectValue);
            Assert.IsEmpty(spec.Validate(AuthoringContext.Default()).ToList());
        }

        [Test]
        public void Extended_catalog_registers_heal_like_any_other_spec()
        {
            var extended = EffectSpecCatalog.All()
                .Concat(new[] { new EffectSpecInfo("치유", typeof(HealSpec), () => new HealSpec()) })
                .ToList();
            Assert.IsTrue(extended.Any(i => i.SpecType == typeof(HealSpec)));
        }

        [Test]
        public void Heal_description_resolves_from_extended_registry()
        {
            var registry = new EffectDescriptionRegistry();
            registry.Register(new HealDescriptionHandler());
            var card = new CardDefinition("heal_touch", "치유의 손길", Side.Player, 1,
                new[] { new EffectData(HealKey, 5) });
            var context = new DescriptionContext(
                new KoreanDescriptionGrammar(),
                new StatusDescriptionRegistry(),
                TestContent.Statuses(),
                card.Id,
                card.Side);
            Assert.AreEqual("치유 5",
                registry.Resolve(HealKey).Describe(new EffectData(HealKey, 5), 5, context).Text);
        }

        // --- Execution-path proof: a Heal card resolved through the real TurnResolver restores an
        // ally's HP. Pattern follows InterventionActionTests.cs's local EffectRegistry() helper +
        // direct CombatState/Zone construction (that test file builds its own registry containing
        // only the handlers a given test needs, rather than touching CombatRegistries). ---

        private static EffectRegistry EffectRegistry()
        {
            var r = new EffectRegistry();
            r.Register(new HealHandler());
            return r;
        }

        [Test]
        public void Heal_card_restores_owner_party_member_hp_through_turn_resolver()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            var def = new CardDefinition(
                "heal_touch", "치유의 손길", Side.Player, 1,
                new[] { new EffectData(HealKey, 5) });
            var card = new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId };
            state.Zone.Add(card);

            new TurnResolver(EffectRegistry()).Resolve(state, 0);

            Assert.AreEqual(25, state.Party[0].Hp);
        }
    }
}
