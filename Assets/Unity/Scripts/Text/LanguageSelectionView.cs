using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
namespace FateWeaver.Unity
{
    /// <summary>Language buttons select Unity's locale; they do not own language state.</summary>
    public sealed class LanguageSelectionView : MonoBehaviour
    {
        [SerializeField] private Button _korean;
        [SerializeField] private Button _english;
        [SerializeField] private Locale _koreanLocale;
        [SerializeField] private Locale _englishLocale;
        [SerializeField] private string _preferenceKey = "fateweaver.locale";
        private void Choose(Locale locale)
        {
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(_preferenceKey, locale.Identifier.Code);
            PlayerPrefs.Save();
        }
        private void Awake()
        {
            _korean.onClick.AddListener(() => Choose(_koreanLocale));
            _english.onClick.AddListener(() => Choose(_englishLocale));
        }
    }
}
