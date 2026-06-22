using System;
using UnityEngine;

namespace FateWeaver.Unity
{
    public static class RuntimeOsFontLoader
    {
        public const string MalgunGothicFamily = "Malgun Gothic";

        public static Font LoadMalgunGothic(int fontSize)
        {
            if (fontSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize));
            }

            var installedFonts = Font.GetOSInstalledFontNames();
            var matchedFamily = Array.Find(
                installedFonts,
                name => string.Equals(name, MalgunGothicFamily, StringComparison.OrdinalIgnoreCase));
            if (matchedFamily == null)
            {
                Debug.LogWarning("OS font not found: " + MalgunGothicFamily);
                return null;
            }

            var font = Font.CreateDynamicFontFromOSFont(matchedFamily, fontSize);
            if (font == null)
            {
                Debug.LogWarning("Failed to create dynamic font: " + matchedFamily);
            }

            return font;
        }
    }
}
