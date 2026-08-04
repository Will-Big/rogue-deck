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
        /// <summary>호출마다 새로 만든다 — 코어의 TestContent와 같은 이유로(가변 Rules) 인스턴스를
        /// 공유하지 않는다.</summary>
        public static StatusContentCatalog Statuses()
        {
            var result = ContentBootstrap.LoadStatuses(UnityContentRoot.Path);
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        /// <summary>저장소 JSON 전체. 상태와 같은 이유로 호출마다 새로 만든다.</summary>
        public static GameContent Content()
        {
            var result = ContentBootstrap.Load(UnityContentRoot.Path);
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Content;
        }

        public static CardContentCatalog Cards() => Content().Cards;
    }
}
