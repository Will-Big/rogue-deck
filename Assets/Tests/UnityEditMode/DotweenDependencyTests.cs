using DG.Tweening;
using NUnit.Framework;

namespace FateWeaver.Tests.UnityEditMode
{
    public class DotweenDependencyTests
    {
        [Test]
        public void Dotween_runtime_is_available_to_unity_tests()
        {
            Assert.IsNotNull(typeof(DOTween));
            Assert.IsNotNull(typeof(Tween));
        }
    }
}
