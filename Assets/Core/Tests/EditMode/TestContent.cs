using System.IO;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Statuses;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>테스트가 저장소의 커밋된 콘텐츠를 읽는 단 하나의 진입점. 상태 규칙의 원본이
    /// JSON뿐이므로 테스트도 거기서 읽는다 — 코드 기본값을 되살리지 않는다.</summary>
    public static class TestContent
    {
        private static StatusContentCatalog _statuses;

        /// <summary>Assets 폴더가 보일 때까지 올라가 콘텐츠 루트를 찾는다. 테스트 실행 디렉터리는
        /// 헤드리스(bin/...)와 Unity(Library/...)가 다르므로 경로를 박지 않는다.</summary>
        public static string Root()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return Path.Combine(directory, "Assets", "StreamingAssets", "Content");
        }

        /// <summary>파일에서 만든 상태 카탈로그. 한 번 읽어 재사용한다.</summary>
        public static StatusContentCatalog Statuses()
        {
            if (_statuses == null)
            {
                var result = ContentBootstrap.LoadStatuses(Root());
                Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
                _statuses = result.Catalog;
            }

            return _statuses;
        }
    }
}
