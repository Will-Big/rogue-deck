using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>내보낸 JSON이 손으로 쓴 C# 스펙과 같은 카드를 만드는지 잠근다. 계획 2가 소비자를
    /// JSON으로 옮길 때 이 테스트가 안전망이 된다.</summary>
    public class CardContentEquivalenceJsonTests
    {
        private static string ContentDirectory()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return Path.Combine(directory, "Assets", "StreamingAssets", "Content", "Cards");
        }

        private static CardContentCatalog Catalog()
        {
            var result = CardContentLoader.Load(
                CardContentFiles.ReadDirectory(ContentDirectory()), AuthoringContext.Default());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        private static string Signature(CardDefinition def)
            => def.Id + "|" + def.Name + "|" + def.Side + "|" + def.Category
                + "|" + def.EnergyCost + "|" + def.BaseExecutionOrder
                + "|" + (def.InterventionAction == null
                    ? "-"
                    : def.InterventionAction.Key + ":" + def.InterventionAction.EffectValue
                        + ":" + def.InterventionAction.TargetSide
                        + ":" + def.InterventionAction.RequireAdjacentTargets)
                + "|" + string.Join(",", def.Effects.Select(e =>
                    e.Key + ":" + e.EffectValue + ":" + e.TargetSelector
                        + ":" + (e.Condition == null ? "-" : e.Condition.GetType().Name)
                        + ":" + e.SuccessEffectValue + ":" + e.SkipOnBasic
                        + ":" + (e.Payload == null
                            ? "-"
                            : e.Payload.GetType().Name + ContentJson.Write(e.Payload))));

        private static IEnumerable<CardSpec> AuthoredSpecs()
            => StarterPoolSpecs.Build()
                .Concat(StarterDeckSpecs.AllAuthored())
                .Concat(PartyPrototypeDeckSpecs.Build())
                .GroupBy(spec => spec.Id)
                .Select(group => group.First());

        [Test]
        public void ExportedJsonContainsEveryAuthoredCard()
        {
            var catalog = Catalog();

            foreach (var spec in AuthoredSpecs())
            {
                Assert.IsTrue(
                    catalog.Cards.ContainsKey(spec.Id),
                    "내보낸 콘텐츠에 '" + spec.Id + "'가 없다.");
            }
        }

        [Test]
        public void ExportedJsonProducesIdenticalDefinitions()
        {
            var catalog = Catalog();

            foreach (var spec in AuthoredSpecs())
            {
                Assert.AreEqual(
                    Signature(CardSpecMapper.ToDefinition(spec)),
                    Signature(catalog.Get(spec.Id)),
                    "카드 '" + spec.Id + "'가 달라졌다.");
            }
        }

        [Test]
        public void ExportedJsonHasOneFilePerCard()
        {
            var files = Directory.GetFiles(ContentDirectory(), "*.json");

            Assert.AreEqual(AuthoredSpecs().Count(), files.Length);
        }

        [Test]
        public void EveryAuthoredCardFactoryIsRepresentedInTheContent()
        {
            var catalog = Catalog();
            var factories = new[]
                {
                    typeof(StarterPoolSpecs), typeof(StarterDeckSpecs), typeof(PartyPrototypeDeckSpecs)
                }
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(method => method.ReturnType == typeof(CardSpec)
                    && method.GetParameters().Length == 0);

            foreach (var factory in factories)
            {
                var spec = (CardSpec)factory.Invoke(null, null);
                Assert.IsTrue(
                    catalog.Cards.ContainsKey(spec.Id),
                    factory.DeclaringType.Name + "." + factory.Name + "가 만든 '"
                        + spec.Id + "'가 내보낸 콘텐츠에 없다.");
            }
        }
    }
}
