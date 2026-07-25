using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class PlaytestCardArtTests
    {
        [Test]
        public void Lock_icon_uses_status_resource_path()
        {
            Assert.AreEqual("Status/icon_lock", PlaytestCardArt.LockIconResourcePath);
            Assert.AreEqual("Status/icon_lock", PlaytestCardArt.ResolveStatusIconResourcePath(CardStatusIcon.Lock));
        }
    }
}
