using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    public class AuthoringValidationTests
    {
        private static CardSpec Execution(params EffectSpec[] effects) => new CardSpec
        {
            Id = "t", Name = "t", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = effects
        };

        [Test]
        public void Valid_starter_content_passes()
            => Assert.IsEmpty(AuthoringValidator.Validate(
                StarterDeckSpecs.Build(), AuthoringContext.Default()));

        [Test]
        public void Unknown_status_key_fails()
        {
            var errors = AuthoringValidator.Validate(
                new[] { Execution(new ApplyStatusSpec {
                    Status = new StatusKeyRef { Id = "no_such_status" }, Count = 1 }) },
                AuthoringContext.Default());
            Assert.IsTrue(errors.Any(e => e.Contains("no_such_status")));
        }

        [Test]
        public void Default_context_exposes_registered_status_keys_in_id_order()
        {
            var keys = AuthoringContext.Default().RegisteredStatusKeys;

            Assert.That(keys, Is.EqualTo(new[] {
                StatusKeys.Block,
                StatusKeys.Contagion,
                StatusKeys.Damaged,
                StatusKeys.Haste,
                StatusKeys.Poison,
                StatusKeys.PoisonDormant,
                StatusKeys.PoisonStasis,
                StatusKeys.RewardNullified,
                StatusKeys.Slow,
                StatusKeys.Vulnerable,
                StatusKeys.Weak
            }));
        }

        [Test]
        public void Empty_status_key_fails()
        {
            var errors = AuthoringValidator.Validate(
                new[] { Execution(new ApplyStatusSpec { Count = 1 }) },
                AuthoringContext.Default());
            Assert.IsNotEmpty(errors);
        }

        [TestCase(2)]
        [TestCase(4)]
        public void Undefined_authored_selector_reports_the_card_id(int rawValue)
        {
            var spec = new CardSpec
            {
                Id = "legacy_selector",
                Category = CardCategory.Execution,
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Value = 1, Selector = (TargetSelectorRef)rawValue }
                }
            };

            var errors = AuthoringValidator.Validate(
                new[] { spec }, AuthoringContext.Default());

            Assert.That(errors, Has.Some.Contains("Card 'legacy_selector'"));
            Assert.That(errors, Has.Some.Contains("unsupported target selector value " + rawValue));
        }

        [Test]
        public void Unknown_intervention_key_fails()
        {
            var errors = AuthoringValidator.Validate(
                new[] { new CardSpec {
                    Id = "t", Name = "t", Side = Side.Player,
                    Category = CardCategory.Intervention, EnergyCost = 1,
                    Intervention = new InterventionKeyRef { Id = "no_such_action" } } },
                AuthoringContext.Default());
            Assert.IsTrue(errors.Any(e => e.Contains("no_such_action")));
        }

        [Test]
        public void Catalog_specs_all_have_registered_runtime_handlers()
        {
            var context = AuthoringContext.Default();
            foreach (var info in EffectSpecCatalog.All())
            {
                var spec = info.Create();
                Assert.IsTrue(context.HasEffect(spec.Key),
                    info.SpecType.Name + " has no runtime handler for key " + spec.Key);
            }
        }
    }
}
