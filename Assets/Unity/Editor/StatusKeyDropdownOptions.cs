using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Unity.Editor
{
    public sealed class StatusKeyDropdownOption
    {
        public StatusKeyDropdownOption(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    public sealed class StatusKeyDropdownModel
    {
        public StatusKeyDropdownModel(
            IReadOnlyList<StatusKeyDropdownOption> options,
            int selectedIndex)
        {
            Options = options;
            SelectedIndex = selectedIndex;
        }

        public IReadOnlyList<StatusKeyDropdownOption> Options { get; }
        public int SelectedIndex { get; }
    }

    public static class StatusKeyDropdownOptions
    {
        private const string PlaceholderLabel = "(상태 선택)";

        public static StatusKeyDropdownModel CreateDefault(string currentId)
        {
            var authoring = AuthoringContext.Default();
            return Create(
                currentId,
                authoring.RegisteredStatusKeys,
                KoreanDescriptionCatalog.Default.Statuses);
        }

        public static StatusKeyDropdownModel Create(
            string currentId,
            IReadOnlyList<StatusKey> registeredKeys,
            StatusDescriptionRegistry descriptions)
        {
            if (registeredKeys == null)
                throw new ArgumentNullException(nameof(registeredKeys));
            if (descriptions == null)
                throw new ArgumentNullException(nameof(descriptions));

            var options = new List<StatusKeyDropdownOption>();
            var selectedIndex = 0;
            var isRegistered = false;

            for (var i = 0; i < registeredKeys.Count; i++)
            {
                if (registeredKeys[i].Id == currentId)
                {
                    isRegistered = true;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(currentId) && !isRegistered)
            {
                options.Add(new StatusKeyDropdownOption(
                    currentId,
                    "Unknown: " + currentId));
            }

            options.Add(new StatusKeyDropdownOption(string.Empty, PlaceholderLabel));

            for (var i = 0; i < registeredKeys.Count; i++)
            {
                var key = registeredKeys[i];
                var label = descriptions.TryResolve(key, out var displayName)
                    ? displayName + " (" + key.Id + ")"
                    : key.Id;
                options.Add(new StatusKeyDropdownOption(key.Id, label));

                if (key.Id == currentId)
                    selectedIndex = options.Count - 1;
            }

            return new StatusKeyDropdownModel(options, selectedIndex);
        }
    }
}
