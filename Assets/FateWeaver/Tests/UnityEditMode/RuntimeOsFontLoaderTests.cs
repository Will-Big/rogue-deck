using NUnit.Framework;
using UnityEngine;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class RuntimeOsFontLoaderTests
    {
        [Test]
        public void Loads_Malgun_Gothic_from_the_OS_font_registry()
        {
            var font = RuntimeOsFontLoader.LoadMalgunGothic(fontSize: 16);

            Assert.IsNotNull(font);
            StringAssert.Contains("Malgun Gothic", font.name);
            font.RequestCharactersInTexture("한글", 16, FontStyle.Normal);
            Assert.IsTrue(font.GetCharacterInfo('한', out _, 16, FontStyle.Normal));
            Assert.IsTrue(font.GetCharacterInfo('글', out _, 16, FontStyle.Normal));
            Object.DestroyImmediate(font);
        }
    }
}
