using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Unity;
using NUnit.Framework;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>Unity EditMode 테스트의 콘텐츠 진입점. 코어 테스트 어셈블리를 참조하지 않으므로
    /// 루트는 프로덕션의 UnityContentRoot에서 받는다 — 경로 상수를 새로 만들지 않는다.</summary>
    public static class UnityTestContent
    {
        private static StatusContentCatalog _statuses;

        public static StatusContentCatalog Statuses()
        {
            if (_statuses == null)
            {
                var result = ContentBootstrap.LoadStatuses(UnityContentRoot.Path);
                Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
                _statuses = result.Catalog;
            }

            return _statuses;
        }
    }
}
