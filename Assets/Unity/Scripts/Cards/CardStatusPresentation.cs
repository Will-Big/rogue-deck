using System;
using UnityEngine;

namespace FateWeaver.Unity
{
    public readonly struct CardStatusDisplayContent
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string IconKey { get; }

        public CardStatusDisplayContent(
            string key,
            string displayName,
            string description,
            string iconKey)
        {
            Key = Required(key, nameof(key));
            DisplayName = Required(displayName, nameof(displayName));
            Description = Required(description, nameof(description));
            IconKey = Required(iconKey, nameof(iconKey));
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Card status display value is required.",
                    parameterName);
            }

            return value;
        }
    }

    public interface ICardStatusDisplaySource
    {
        CardStatusDisplayContent Resolve(string key);
    }

    public readonly struct CardStatusPresentation
    {
        public string Key { get; }
        public Sprite Icon { get; }
        public string Title { get; }
        public string Description { get; }

        public CardStatusPresentation(
            string key,
            Sprite icon,
            string title,
            string description)
        {
            Key = Required(key, nameof(key));
            Icon = icon != null
                ? icon
                : throw new ArgumentNullException(nameof(icon));
            Title = Required(title, nameof(title));
            Description = Required(description, nameof(description));
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Card status presentation value is required.",
                    parameterName);
            }

            return value;
        }
    }
}
