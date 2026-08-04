using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;
using FateWeaver.Core.Authoring;
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

        /// <summary>라벨의 원본도 JSON이다. 상태만 필요하므로 부팅 전체가 아니라
        /// ContentBootstrap.LoadStatuses를 쓴다.</summary>
        public static StatusKeyDropdownModel CreateDefault(string currentId)
        {
            var authoring = AuthoringContext.Default();
            var statuses = ContentBootstrap.LoadStatuses(UnityContentRoot.Path);
            return Create(
                currentId,
                authoring.RegisteredStatusKeys,
                KoreanDescriptionCatalog.CreateDefault(statuses.Catalog).Statuses);
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
