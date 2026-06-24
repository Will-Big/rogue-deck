using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Maps a card id to its art under Resources/Cards. Pure id→path resolution is unit-tested;
    /// Sprite(...) wraps it with a cached Resources.Load.</summary>
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
                case "guard": return "guard";
                case "counter_stance": return "counter_stance";
                case "cover": return "cover";
                case "pull_forward": return "pull_forward";
                case "swap_positions": return "swap_positions";
                case "crude_guard": return "crude_guard";
                case "sly_jab": return "sly_jab";
                case "mark": return "mark_target";
                case "counter": return "counter_stance";
                case "chain": return "chain_slash";
                default: return null;
            }
        }

        public static string ResolveResourcePath(string cardId)
        {
            var name = ResolveArtName(cardId);
            return name == null ? null : "Cards/" + name;
        }

        public static Sprite Sprite(string cardId)
        {
            var path = ResolveResourcePath(cardId);
            if (path == null)
            {
                return null;
            }

            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);
            Cache[path] = sprite; // cache null too, to avoid repeated misses
            return sprite;
        }
    }
}
