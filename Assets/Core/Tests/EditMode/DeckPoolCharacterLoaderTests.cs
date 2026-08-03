using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>덱·풀·캐릭터 로더가 저작 실수를 로드 시점에 거부하는지 잠근다. 카드 로더와 같은
    /// 형태로, 실패하면 카탈로그를 내주지 않고 모든 이유를 모아 보고한다(설계 §4.5).</summary>
    public class DeckPoolCharacterLoaderTests
    {
        private static CardContentCatalog Cards(params string[] ids)
        {
            var cards = new Dictionary<string, CardDefinition>();
            foreach (var id in ids)
            {
                cards.Add(id, CardSpecMapper.ToDefinition(new CardSpec
                {
                    Id = id,
                    Name = id,
                    Side = Side.Player,
                    Category = CardCategory.Execution
                }));
            }

            return new CardContentCatalog(cards);
        }

        private static CardContentSource Source(string name, string json)
            => new CardContentSource(name, json);

        private static DeckContentCatalog Decks(params CardContentSource[] sources)
        {
            var result = DeckContentLoader.Load(sources, Cards("hasten", "breather"));
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        // --- 덱 -------------------------------------------------------------

        [Test]
        public void DeckLoaderKeepsCardOrderAndRepeats()
        {
            var catalog = Decks(Source(
                "starter.json",
                "{ \"id\": \"starter\", \"cards\": [\"hasten\", \"breather\", \"hasten\"] }"));

            CollectionAssert.AreEqual(
                new[] { "hasten", "breather", "hasten" }, catalog.Get("starter"));
        }

        [Test]
        public void DeckCatalogExposesSortedIds()
        {
            var catalog = Decks(
                Source("z.json", "{ \"id\": \"zulu\", \"cards\": [\"hasten\"] }"),
                Source("a.json", "{ \"id\": \"alpha\", \"cards\": [\"breather\"] }"));

            CollectionAssert.AreEqual(new[] { "alpha", "zulu" }, catalog.Ids);
        }

        [Test]
        public void DeckCatalogGetThrowsForUnknownId()
        {
            var catalog = Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [] }"));

            Assert.Throws<KeyNotFoundException>(() => catalog.Get("ghost_deck"));
        }

        [Test]
        public void DeckLoaderRejectsAnEmptyId()
        {
            var result = DeckContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"\", \"cards\": [] }") },
                Cards("hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors, "starter.json: required key 'id' must be a non-empty string.");
        }

        [Test]
        public void DeckLoaderRejectsADuplicateDeckId()
        {
            var result = DeckContentLoader.Load(
                new[]
                {
                    Source("a.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }"),
                    Source("b.json", "{ \"id\": \"starter\", \"cards\": [\"breather\"] }")
                },
                Cards("hasten", "breather"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors,
                "b.json: duplicate deck id 'starter' (already defined in a.json).");
        }

        [Test]
        public void DeckLoaderRejectsAnUnknownCardId()
        {
            var result = DeckContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"ghost_card\"] }") },
                Cards("hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "starter.json: unknown card id 'ghost_card'.");
        }

        // --- 풀 -------------------------------------------------------------

        [Test]
        public void PoolLoaderAcceptsADistinctCandidateSet()
        {
            var result = PoolContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\", \"breather\"] }") },
                Cards("hasten", "breather"));

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            CollectionAssert.AreEqual(new[] { "hasten", "breather" }, result.Catalog.Get("starter"));
        }

        [Test]
        public void PoolLoaderRejectsADuplicateCardId()
        {
            var result = PoolContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\", \"hasten\"] }") },
                Cards("hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors, "starter.json: duplicate card id 'hasten' in pool.");
        }

        [Test]
        public void PoolLoaderRejectsAnUnknownCardId()
        {
            var result = PoolContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"ghost_card\"] }") },
                Cards("hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "starter.json: unknown card id 'ghost_card'.");
        }

        [Test]
        public void PoolLoaderRejectsADuplicatePoolId()
        {
            var result = PoolContentLoader.Load(
                new[]
                {
                    Source("a.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }"),
                    Source("b.json", "{ \"id\": \"starter\", \"cards\": [\"breather\"] }")
                },
                Cards("hasten", "breather"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors,
                "b.json: duplicate pool id 'starter' (already defined in a.json).");
        }

        // --- 캐릭터 ---------------------------------------------------------

        [Test]
        public void CharacterLoaderReadsIdNameAndDeck()
        {
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source(
                        "member_a.json",
                        "{ \"id\": \"member_a\", \"displayName\": \"파티원 A\", \"deck\": \"starter\" }")
                },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")));

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            var member = result.Catalog.Get("member_a");
            Assert.AreEqual("member_a", member.Id);
            Assert.AreEqual("파티원 A", member.DisplayName);
            Assert.AreEqual("starter", member.Deck);
        }

        [Test]
        public void CharacterCatalogExposesSortedIds()
        {
            var decks = Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }"));
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source("b.json", "{ \"id\": \"member_b\", \"displayName\": \"B\", \"deck\": \"starter\" }"),
                    Source("a.json", "{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\" }")
                },
                decks);

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            CollectionAssert.AreEqual(new[] { "member_a", "member_b" }, result.Catalog.Ids);
        }

        [Test]
        public void CharacterLoaderRejectsAnUnknownDeckId()
        {
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source(
                        "member_a.json",
                        "{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"ghost_deck\" }")
                },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "member_a.json: unknown deck id 'ghost_deck'.");
        }

        [Test]
        public void CharacterLoaderRejectsAnEmptyDisplayName()
        {
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source(
                        "member_a.json",
                        "{ \"id\": \"member_a\", \"displayName\": \"\", \"deck\": \"starter\" }")
                },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "member_a.json: requires a displayName.");
        }

        [Test]
        public void CharacterLoaderRejectsAnEmptyId()
        {
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source("member_a.json", "{ \"id\": \"\", \"displayName\": \"A\", \"deck\": \"starter\" }")
                },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors, "member_a.json: required key 'id' must be a non-empty string.");
        }

        [Test]
        public void CharacterLoaderRejectsADuplicateCharacterId()
        {
            var decks = Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }"));
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source("a.json", "{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\" }"),
                    Source("b.json", "{ \"id\": \"member_a\", \"displayName\": \"A2\", \"deck\": \"starter\" }")
                },
                decks);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(
                result.Errors,
                "b.json: duplicate character id 'member_a' (already defined in a.json).");
        }

        // --- 공통 -----------------------------------------------------------

        [Test]
        public void LoadersReportEveryReasonAtOnce()
        {
            var result = DeckContentLoader.Load(
                new[]
                {
                    Source("a.json", "{ \"id\": \"starter\", \"cards\": [\"ghost_one\"] }"),
                    Source("b.json", "{ \"id\": \"\", \"cards\": [] }")
                },
                Cards("hasten"));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(2, result.Errors.Count, string.Join("\n", result.Errors));
        }

        [Test]
        public void DeckLoaderRejectsAMissingCardsKey()
        {
            var result = DeckContentLoader.Load(
                new[] { Source("starter.json", "{ \"id\": \"starter\" }") },
                Cards("hasten"));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "starter.json: required key 'cards' is missing.");
        }

        [Test]
        public void ContentFolderNamesAreDistinct()
        {
            var names = new[]
            {
                CardContentFiles.CardsFolderName,
                CardContentFiles.StatusesFolderName,
                CardContentFiles.DecksFolderName,
                CardContentFiles.PoolsFolderName,
                CardContentFiles.CharactersFolderName
            };

            CollectionAssert.AllItemsAreUnique(names);
            Assert.IsFalse(names.Any(string.IsNullOrEmpty));
        }
    }
}
