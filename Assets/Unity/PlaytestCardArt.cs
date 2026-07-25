using System.Collections.Generic;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Resolves card status icons from Resources. Card face art comes from CardAsset.Art
    /// (inspector-assigned, GUID-based); there is no id→path fallback.</summary>
    public static class PlaytestCardArt
    {
        private static readonly Dictionary<CardStatusIcon, Sprite> StatusIconCache = new Dictionary<CardStatusIcon, Sprite>();

        public const string LockIconResourcePath = "Status/icon_lock";

        public static Sprite LockIconSprite()
            => StatusIconSprite(CardStatusIcon.Lock);

        public static string ResolveStatusIconResourcePath(CardStatusIcon icon)
        {
            switch (icon)
            {
                case CardStatusIcon.Lock:
                    return LockIconResourcePath;
                default:
                    return null;
            }
        }

        public static Sprite StatusIconSprite(CardStatusIcon icon)
        {
            if (StatusIconCache.TryGetValue(icon, out var cached))
            {
                return cached;
            }

            var path = ResolveStatusIconResourcePath(icon);
            if (path == null)
            {
                StatusIconCache[icon] = null;
                return null;
            }

            var sprites = Resources.LoadAll<Sprite>(path);
            if (sprites != null && sprites.Length > 0)
            {
                StatusIconCache[icon] = sprites[0];
                return sprites[0];
            }

            var sprite = Resources.Load<Sprite>(path);
            StatusIconCache[icon] = sprite;
            return sprite;
        }
    }
}
