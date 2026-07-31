using System.Linq;
using FateWeaver.Core.Authoring;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CardContentLoaderTests
    {
        private const string Slash =
            "{ \"id\": \"slash\", \"name\": \"베기\", \"side\": \"Player\","
            + " \"category\": \"Execution\", \"energyCost\": 1, \"baseExecutionOrder\": 4,"
            + " \"effects\": [ { \"kind\": \"damage\", \"value\": 5 } ] }";

        private static CardContentLoadResult Load(params CardContentSource[] sources)
            => CardContentLoader.Load(sources, AuthoringContext.Default());

        [Test]
        public void LoadsACardIntoTheCatalog()
        {
            var result = Load(new CardContentSource("slash.json", Slash));

            Assert.IsTrue(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.AreEqual("베기", result.Catalog.Get("slash").Name);
            Assert.AreEqual(5, result.Catalog.Get("slash").Effects[0].EffectValue);
        }

        [Test]
        public void ReportsMalformedJsonWithFileNameAndLineNumber()
        {
            var result = Load(new CardContentSource(
                "broken.json",
                "{ \"id\": \"x\", \"name\": \"x\", \"side\": \"Player\", \"category\": \"Execution\""));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Catalog);
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains("broken.json", result.Errors[0]);
            StringAssert.Contains("line 1", result.Errors[0]);
        }

        [Test]
        public void ReportsAnUnknownEffectKindInsteadOfSkippingIt()
        {
            var result = Load(new CardContentSource(
                "typo.json",
                "{ \"id\": \"x\", \"name\": \"x\", \"side\": \"Player\","
                + " \"category\": \"Execution\","
                + " \"effects\": [ { \"kind\": \"dmage\", \"value\": 5 } ] }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("typo.json", result.Errors[0]);
            StringAssert.Contains("dmage", result.Errors[0]);
        }

        [Test]
        public void ReportsAMissingRequiredKeyRatherThanDefaultingIt()
        {
            var result = Load(new CardContentSource(
                "nosides.json", "{ \"id\": \"x\", \"name\": \"x\", \"category\": \"Execution\" }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("side", result.Errors[0]);
        }

        [Test]
        public void ReportsADuplicateIdAcrossFiles()
        {
            var result = Load(
                new CardContentSource("a.json", Slash),
                new CardContentSource("b.json", Slash));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("slash", result.Errors[0]);
            StringAssert.Contains("b.json", result.Errors[0]);
        }

        [Test]
        public void ReportsAuthoringValidationFailures()
        {
            var result = Load(new CardContentSource(
                "badstatus.json",
                "{ \"id\": \"x\", \"name\": \"x\", \"side\": \"Player\","
                + " \"category\": \"Execution\", \"effects\": ["
                + " { \"kind\": \"apply_status\", \"status\": \"no_such_status\", \"value\": 1 } ] }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("no_such_status", result.Errors[0]);
        }

        [Test]
        public void ReportsEveryFailingFileAtOnce()
        {
            var result = Load(
                new CardContentSource(
                    "one.json",
                    "{ \"id\": \"a\", \"name\": \"x\", \"side\": \"Player\", \"category\": \"Execution\""),
                new CardContentSource(
                    "two.json",
                    "{ \"id\": \"b\", \"name\": \"x\", \"side\": \"Player\", \"category\": \"Execution\""));

            Assert.AreEqual(2, result.Errors.Count);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("one.json")));
            Assert.IsTrue(result.Errors.Any(e => e.Contains("two.json")));
        }

        [Test]
        public void ReportsANullIdInsteadOfThrowing()
        {
            var result = Load(new CardContentSource(
                "nullid.json",
                "{ \"id\": null, \"name\": \"x\", \"side\": \"Player\", \"category\": \"Execution\" }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("nullid.json", result.Errors[0]);
            StringAssert.Contains("id", result.Errors[0]);
        }

        [Test]
        public void ReportsANonStringStatusKeyWithItsLocation()
        {
            var result = Load(new CardContentSource(
                "badkey.json",
                "{ \"id\": \"x\", \"name\": \"x\", \"side\": \"Player\","
                + " \"category\": \"Execution\", \"effects\": ["
                + " { \"kind\": \"apply_status\", \"status\": 5, \"value\": 1 } ] }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("badkey.json", result.Errors[0]);
            StringAssert.Contains("string", result.Errors[0]);
        }

        [Test]
        public void ExposesIdsInSortedOrderForDeterminism()
        {
            var result = Load(
                new CardContentSource("z.json", Slash.Replace("slash", "zeta")),
                new CardContentSource("a.json", Slash.Replace("slash", "alpha")));

            CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, result.Catalog.Ids);
        }
    }
}
