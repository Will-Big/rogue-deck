using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardStatusIconSpritesTests
    {
        [Test]
        public void Lock_icon_uses_status_resource_path()
        {
            Assert.AreEqual("Status/icon_lock", CardStatusIconSprites.LockIconResourcePath);
            Assert.AreEqual("Status/icon_lock", CardStatusIconSprites.ResolveStatusIconResourcePath(CardStatusIcon.Lock));
        }
    }
}
