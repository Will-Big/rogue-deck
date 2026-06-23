using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Authoring;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Inspector-authored deck: a list of cards with counts. Expands to flat CardSpecs.</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Deck", fileName = "Deck")]
    public sealed class DeckAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public CardAsset Card;
            public int Count;
        }

        public string Id;
        public Entry[] Entries = Array.Empty<Entry>();

        public IReadOnlyList<CardSpec> ToSpecs()
        {
            var specs = new List<CardSpec>();
            foreach (var entry in Entries)
            {
                if (entry.Card == null)
                {
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    specs.Add(entry.Card.ToSpec());
                }
            }

            return specs;
        }
    }
}
