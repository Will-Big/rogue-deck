using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
namespace FateWeaver.Unity
{
    /// <summary>Publishes only a fully loaded snapshot of Unity's selected locale.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameLocalization : MonoBehaviour
    {
        [SerializeField] private LocalizationBindings _bindings;
        private int _generation;
        public UnityLocalizedTextRenderer Renderer { get; private set; }
        public Task Initialization { get; private set; }
        public event Action Changed;
        private void Awake() => Initialization = InitializeAsync();
        private void OnEnable() => LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        private void OnDisable() { LocalizationSettings.SelectedLocaleChanged -= LocaleChanged; _generation++; }
        private async Task InitializeAsync()
        {
            await LocalizationSettings.InitializationOperation.Task;
            await LoadAsync(LocalizationSettings.SelectedLocale);
        }
        private async void LocaleChanged(Locale locale)
        {
            try { await LoadAsync(locale); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private async Task LoadAsync(Locale locale)
        {
            var generation = ++_generation;
            var renderer = await UnityLocalizedTextRenderer.LoadAsync(_bindings, locale);
            if (generation != _generation || this == null || !isActiveAndEnabled) return;
            Renderer = renderer;
            Changed?.Invoke();
        }
        public string Render(LocalizedTextRequest request)
            => (Renderer ?? throw new InvalidOperationException("Localization is not initialized.")).Render(request);
        public string Name(string key) => Render(DescriptionRequests.Name(key));
        public string UI(string key, params (string Name, object Value)[] arguments) => Render(Request(key, arguments));
        public static LocalizedTextRequest Request(string key, params (string Name, object Value)[] arguments)
        {
            var args = new Dictionary<string, object>();
            foreach (var pair in arguments) args.Add(pair.Name, pair.Value);
            return new LocalizedTextRequest(TranslationTable.UI, key, args);
        }
    }
}
