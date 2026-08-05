using System.Collections.Generic;
using System.IO;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>테스트가 저장소의 커밋된 콘텐츠를 읽는 단 하나의 진입점. 상태 규칙의 원본이
    /// JSON뿐이므로 테스트도 거기서 읽는다 — 코드 기본값을 되살리지 않는다.</summary>
    public static class TestContent
    {
        private static string _root;

        /// <summary>Assets 폴더가 보일 때까지 올라가 콘텐츠 루트를 찾는다. 테스트 실행 디렉터리는
        /// 헤드리스(bin/...)와 Unity(Library/...)가 다르므로 경로를 박지 않는다.</summary>
        public static string Root()
        {
            if (_root != null)
            {
                return _root;
            }

            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return _root = Path.Combine(directory, "Assets", "StreamingAssets", "Content");
        }

        /// <summary>파일에서 만든 상태 카탈로그. **호출마다 새로 만든다** — 카탈로그의 Rules는
        /// 가변이고(StatusRuleSet.Set) 그것을 바꿔 보는 테스트가 있으므로, 인스턴스를 공유하면
        /// 한 테스트의 배율 변경이 뒤 테스트로 샌다. 경로 탐색만 캐시한다.</summary>
        public static StatusContentCatalog Statuses()
        {
            var result = ContentBootstrap.LoadStatuses(Root());
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        /// <summary>저장소 JSON 전체를 읽은 콘텐츠 번들. 상태 카탈로그와 같은 이유로 호출마다
        /// 새로 만든다 — 카탈로그의 Rules가 가변이다.</summary>
        public static GameContent Content()
        {
            var result = ContentBootstrap.Load(Root());
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Content;
        }

        public static CardContentCatalog Cards() => Content().Cards;

        /// <summary>Decks/starter.json이 지정한 10장을 정의 객체로 편다. 과거에는 C# 코드가 시작
        /// 덱을 조립했지만, 이제 원본이 JSON이라는 점만 다르다.</summary>
        public static IReadOnlyList<CardDefinition> StarterDeckCards()
        {
            var content = Content();
            var cards = new List<CardDefinition>();
            foreach (var id in content.Decks.Get("starter"))
            {
                cards.Add(content.Cards.Get(id));
            }

            return cards;
        }
    }
}
