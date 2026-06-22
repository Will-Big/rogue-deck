using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Maps a card id to its art under Resources/. Pure id→name resolution is unit-tested;
    /// Sprite(...) wraps it with a cached Resources.Load. Resources root holds the PNGs by file name.</summary>
    public static class PlaytestCardArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static string ResolveArtName(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return null;
            }

            if (cardId.StartsWith("quick_cut", StringComparison.Ordinal)) return "quick_cut";
            if (cardId.StartsWith("wrist_cut", StringComparison.Ordinal)) return "wrist_cut";
            if (cardId.StartsWith("preemptive_thrust", StringComparison.Ordinal)) return "preemptive_thrust";
            if (cardId.StartsWith("goblin_jab", StringComparison.Ordinal)) return "goblin_jab";

            switch (cardId)
            {
                case "slash": return "slash";
                case "mark": return "mark_target";
                case "counter": return "counter_stance";
                case "chain": return "chain_slash";
                default: return null;
            }
        }

        public static Sprite Sprite(string cardId)
        {
            var name = ResolveArtName(cardId);
            if (name == null)
            {
                return null;
            }

            if (Cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(name);
            Cache[name] = sprite; // cache null too, to avoid repeated misses
            return sprite;
        }
    }
}
