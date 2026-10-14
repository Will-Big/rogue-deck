using NUnit.Framework;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>Edit Mode starts without a selected locale, so authored LocalizedStrings cannot resolve.</summary>
    [SetUpFixture]
    public sealed class EditModeLocaleSetup
    {
        private Locale _previous;

        [OneTimeSetUp]
        public void SelectKorean()
        {
            _previous = LocalizationSettings.SelectedLocale;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
            Assert.IsNotNull(LocalizationSettings.SelectedLocale, "Korean locale must be available in Edit Mode.");
        }

        [OneTimeTearDown]
        public void RestorePrevious() => LocalizationSettings.SelectedLocale = _previous;
    }
}
