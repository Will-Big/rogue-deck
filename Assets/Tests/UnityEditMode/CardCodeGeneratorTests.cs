using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Unity.Editor;
using NUnit.Framework;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardCodeGeneratorTests
    {
        [Test]
        public void EmitSource_preserves_pool_intervention_constraints()
        {
            var source = CardCodeGenerator.EmitSource(
                new[] { ExecutionCard() },
                new[]
                {
                    StarterPoolSpecs.Hasten(),
                    StarterPoolSpecs.Delay(),
                    StarterPoolSpecs.Breather(),
                    StarterPoolSpecs.Crossover()
                });

            StringAssert.Contains(
                "public static IReadOnlyList<CardSpec> StarterPool()",
                source);
            StringAssert.Contains(
                "InterventionTargetSide = InterventionTargetSideRef.Player",
                source);
            StringAssert.Contains(
                "InterventionTargetSide = InterventionTargetSideRef.Enemy",
                source);
            StringAssert.Contains("InterventionRequireAdjacent = true", source);
        }

        [Test]
        public void EmitSource_keeps_existing_deck_export_when_pool_is_missing()
        {
            var source = CardCodeGenerator.EmitSource(
                new[] { ExecutionCard() },
                null);

            StringAssert.Contains(
                "public static IReadOnlyList<CardSpec> StarterDeck()",
                source);
            StringAssert.DoesNotContain(
                "public static IReadOnlyList<CardSpec> StarterPool()",
                source);
        }

        private static CardSpec ExecutionCard() => new CardSpec
        {
            Id = "test",
            Name = "테스트",
            Side = Side.Player,
            Category = CardCategory.Execution,
            EnergyCost = 1,
            BaseExecutionOrder = 5,
            Effects = System.Array.Empty<EffectSpec>()
        };
    }
}
