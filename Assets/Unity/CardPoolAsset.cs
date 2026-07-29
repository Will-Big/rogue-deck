using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Authoring;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Inspector-authored candidate card set. Unlike DeckAsset, a pool has no counts or order semantics.</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Card Pool", fileName = "CardPool")]
    public sealed class CardPoolAsset : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private CardAsset[] _cards = Array.Empty<CardAsset>();

        public string Id => _id;
        public IReadOnlyList<CardAsset> Cards => _cards ?? Array.Empty<CardAsset>();

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(_id))
            {
                errors.Add("Card pool id must not be blank.");
            }

            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            var cards = _cards ?? Array.Empty<CardAsset>();
            for (int cardIndex = 0; cardIndex < cards.Length; cardIndex++)
            {
                var card = cards[cardIndex];
                if (card == null)
                {
                    errors.Add($"Card pool contains a null card at index {cardIndex}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(card.Id))
                {
                    errors.Add($"Card pool contains a blank card id at index {cardIndex}.");
                }
                else if (!cardIds.Add(card.Id))
                {
                    errors.Add($"Card pool contains duplicate card id '{card.Id}'.");
                }

                if (card.Grade == CardGrade.None)
                {
                    errors.Add($"Card '{card.Id}' must have a grade.");
                }

                ValidateTags(card, errors);
            }

            return errors;
        }

        public IReadOnlyList<CardSpec> ToSpecs()
        {
            var errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Card pool validation failed:\n" + string.Join("\n", errors));
            }

            var cards = _cards ?? Array.Empty<CardAsset>();
            var specs = new List<CardSpec>(cards.Length);
            foreach (var card in cards)
            {
                specs.Add(card.ToSpec());
            }

            return specs;
        }

        private static void ValidateTags(CardAsset card, ICollection<string> errors)
        {
            var tags = card.Tags;
            if (tags.Count == 0)
            {
                errors.Add($"Card '{card.Id}' must have at least one tag.");
                return;
            }

            var uniqueTags = new HashSet<string>(StringComparer.Ordinal);
            for (int tagIndex = 0; tagIndex < tags.Count; tagIndex++)
            {
                var tag = tags[tagIndex];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    errors.Add($"Card '{card.Id}' contains an empty tag at index {tagIndex}.");
                    continue;
                }

                if (!uniqueTags.Add(tag))
                {
                    errors.Add($"Card '{card.Id}' contains duplicate tag '{tag}'.");
                }
            }
        }
    }
}
