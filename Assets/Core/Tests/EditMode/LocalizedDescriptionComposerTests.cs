using System;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation.Descriptions;
namespace FateWeaver.Tests.EditMode
{
    public class LocalizedDescriptionComposerTests
    {
        [Test]
        public void Venom_thrust_keeps_damage_and_status_as_requests()
        {
            var doc = Compose("venom_thrust");
            Assert.AreEqual(new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne), doc.Lines[0].Target);
            var damage = Body(doc.Lines[0].Sentences[0]);
            var status = Body(doc.Lines[0].Sentences[1]);
            Assert.AreEqual("effect.damage", damage.Key);
            Assert.AreEqual(2, damage.Arguments["amount"]);
            Assert.AreEqual("effect.apply_status", status.Key);
            Assert.AreEqual(1, status.Arguments["amount"]);
            Assert.AreEqual("status.poison.name", ((LocalizedTextRequest)status.Arguments["status"]).Key);
        }
        [Test]
        public void Toxic_reclaim_preserves_consumption_requirement()
        {
            var doc = Compose("toxic_reclaim");
            Assert.AreEqual(2, doc.Lines.Count);
            var consume = Body(doc.Lines[0].Sentences[0]);
            Assert.AreEqual("effect.consume_status.up_to", consume.Key);
            Assert.AreEqual(1, consume.Arguments["amount"]);
            var conditional = doc.Lines[1].Sentences[0];
            Assert.AreEqual("sentence", conditional.Key);
            conditional = Body(conditional);
            Assert.AreEqual("effect.required", conditional.Key);
            Assert.AreEqual("condition.consumed", ((LocalizedTextRequest)conditional.Arguments["condition"]).Key);
        }
        [Test]
        public void Conditional_effects_keep_success_values_and_skip_on_basic()
        {
            var effect = new EffectData(EffectKeys.Damage, 3) { SuccessEffectValue = 9, SkipOnBasic = true, TargetFaction = CardTargetFaction.Enemy };
            var altered = new CardDefinition("test", "test", Side.Player, 0, new[] { effect })
            { EnemyTarget = CardTargetRange.FrontOne, StartCondition = new FateWeaver.Core.Conditions.FirstToTrigger() };
            var result = DescriptionComposer.Compose(altered, DescriptionRequestCatalog.CreateDefault(TestContent.Statuses()));
            Assert.AreEqual(1, result.Lines[0].Sentences.Count);
            Assert.AreEqual(9, Body(result.Lines[0].Sentences[0]).Arguments["amount"]);
        }
        [Test]
        public void All_content_has_requests_without_locale_dependency()
        {
            var catalog = DescriptionRequestCatalog.CreateDefault(TestContent.Statuses());
            foreach (var card in TestContent.Cards().Cards.Values)
            {
                var doc = DescriptionComposer.Compose(card, catalog);
                Assert.IsNotEmpty(doc.Lines, card.Id);
                Assert.IsTrue(doc.Lines.All(l => l.Sentences.All(s => !string.IsNullOrWhiteSpace(s.Key))), card.Id);
            }
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                catalog.Effects.Resolve(new EffectKey("unregistered")));
        }
        [Test]
        public void Scaling_and_damage_traits_preserve_signed_amounts()
        {
            var effect = new EffectData(EffectKeys.Damage, 7)
            {
                TargetFaction = CardTargetFaction.Enemy,
                Scaling = new EffectResultScaling("consumed", -2),
                Payload = new DamagePayload(DamageTraits.Of(DamageTrait.Piercing, DamageTrait.IgnoresMultipliers))
            };
            var card = new CardDefinition("scaled", "scaled", Side.Player, 0, new[] { effect }) { EnemyTarget = CardTargetRange.FrontOne };
            var body = Body(DescriptionComposer.Compose(card, DescriptionRequestCatalog.CreateDefault(TestContent.Statuses())).Lines[0].Sentences[0]);
            Assert.AreEqual("effect.scaled", body.Key);
            Assert.AreEqual(-2, body.Arguments["amount"]);
            var damage = (LocalizedTextRequest)body.Arguments["body"];
            Assert.AreEqual(7, damage.Arguments["amount"]);
            var suffix = (LocalizedTextRequest)damage.Arguments["traits"];
            var list = (LocalizedTextRequest)suffix.Arguments["traits"];
            Assert.AreEqual("damage_trait.Piercing.name", ((LocalizedTextRequest)list.Arguments["first"]).Key);
            Assert.AreEqual("damage_trait.IgnoresMultipliers.name", ((LocalizedTextRequest)list.Arguments["next"]).Key);
        }
        [Test]
        public void Compound_conditions_keep_children_and_unknown_conditions_fail()
        {
            var request = ConditionDescriptionRequests.Create(new FateWeaver.Core.Conditions.AllOf(new FateWeaver.Core.Conditions.Condition[]
            { new FateWeaver.Core.Conditions.FirstToTrigger(), new FateWeaver.Core.Conditions.WithinNth(3) }));
            Assert.AreEqual("condition.all", request.Key);
            Assert.AreEqual("condition.first", ((LocalizedTextRequest)request.Arguments["first"]).Key);
            Assert.AreEqual(3, ((LocalizedTextRequest)request.Arguments["next"]).Arguments["n"]);
            Assert.Throws<ArgumentException>(() => ConditionDescriptionRequests.Create(new UnknownCondition()));
        }
        private sealed record UnknownCondition : FateWeaver.Core.Conditions.Condition;

        private static LocalizedCardDescription Compose(string id) => DescriptionComposer.Compose(
            TestContent.Cards().Get(id), DescriptionRequestCatalog.CreateDefault(TestContent.Statuses()));
        private static LocalizedTextRequest Body(LocalizedTextRequest sentence)
            => (LocalizedTextRequest)sentence.Arguments["body"];
    }
}
