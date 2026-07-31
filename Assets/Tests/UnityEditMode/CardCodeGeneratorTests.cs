using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Authoring;
using FateWeaver.Unity;
using FateWeaver.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        [Test]
        public void Starter_pool_validation_rejects_a_valid_but_incomplete_pool()
        {
            var card = ScriptableObject.CreateInstance<CardAsset>();
            var pool = ScriptableObject.CreateInstance<CardPoolAsset>();
            try
            {
                card.Id = "vanguard_slash";
                var serializedCard = new SerializedObject(card);
                serializedCard.FindProperty("_grade").enumValueIndex = (int)CardGrade.Common;
                var tags = serializedCard.FindProperty("_tags");
                tags.arraySize = 1;
                tags.GetArrayElementAtIndex(0).stringValue = "시작";
                serializedCard.ApplyModifiedPropertiesWithoutUndo();

                var serializedPool = new SerializedObject(pool);
                serializedPool.FindProperty("_id").stringValue = "starter_pool";
                var cards = serializedPool.FindProperty("_cards");
                cards.arraySize = 1;
                cards.GetArrayElementAtIndex(0).objectReferenceValue = card;
                serializedPool.ApplyModifiedPropertiesWithoutUndo();

                var errors = CardCodeGenerator.ValidateStarterPoolAsset(pool);

                Assert.That(errors.Any(error => error.Contains("exactly 22 cards")));
                Assert.That(errors.Any(error => error.Contains("missing expected card id")));
            }
            finally
            {
                Object.DestroyImmediate(pool);
                Object.DestroyImmediate(card);
            }
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
