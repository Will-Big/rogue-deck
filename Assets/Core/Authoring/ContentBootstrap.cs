using System.Collections.Generic;
using System.IO;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 1회의 결과. 실패하면 번들을 내주지 않고 모든 이유를 모아 보고한다
    /// (설계 §4.5).</summary>
    public sealed class ContentBootstrapResult
    {
        private ContentBootstrapResult(GameContent content, IReadOnlyList<string> errors)
        {
            Content = content;
            Errors = errors;
        }

        public bool Succeeded => Content != null;
        public GameContent Content { get; }
        public IReadOnlyList<string> Errors { get; }

        public static ContentBootstrapResult Ok(GameContent content)
            => new ContentBootstrapResult(content, new string[0]);

        public static ContentBootstrapResult Failed(IReadOnlyList<string> errors)
            => new ContentBootstrapResult(null, errors);
    }

    /// <summary>콘텐츠 루트 하나를 받아 카탈로그 넷을 만든다. 순서는 카드 → 덱·풀 → 캐릭터로
    /// 고정이다 — 덱·풀 로더가 카드 카탈로그를, 캐릭터 로더가 덱 카탈로그를 필요로 한다.
    /// 파일 I/O는 CardContentFiles가 맡으므로 Unity 없이 돈다.</summary>
    public static class ContentBootstrap
    {
        public static ContentBootstrapResult Load(string contentRoot)
        {
            var errors = new List<string>();

            var cards = CardContentLoader.Load(
                Read(contentRoot, CardContentFiles.CardsFolderName, errors),
                AuthoringContext.Default());
            if (!cards.Succeeded || errors.Count > 0)
            {
                errors.AddRange(cards.Errors);
                return ContentBootstrapResult.Failed(errors);
            }

            var decks = DeckContentLoader.Load(
                Read(contentRoot, CardContentFiles.DecksFolderName, errors), cards.Catalog);
            var pools = PoolContentLoader.Load(
                Read(contentRoot, CardContentFiles.PoolsFolderName, errors), cards.Catalog);

            if (!decks.Succeeded)
            {
                errors.AddRange(decks.Errors);
            }

            if (!pools.Succeeded)
            {
                errors.AddRange(pools.Errors);
            }

            if (errors.Count > 0)
            {
                return ContentBootstrapResult.Failed(errors);
            }

            var characters = CharacterContentLoader.Load(
                Read(contentRoot, CardContentFiles.CharactersFolderName, errors),
                decks.Catalog);
            if (!characters.Succeeded)
            {
                errors.AddRange(characters.Errors);
            }

            if (errors.Count > 0)
            {
                return ContentBootstrapResult.Failed(errors);
            }

            return ContentBootstrapResult.Ok(new GameContent(
                cards.Catalog, decks.Catalog, pools.Catalog, characters.Catalog));
        }

        /// <summary>폴더가 없으면 던지지 않고 이유로 바꾼다 — 부팅은 모든 이유를 모아 보고한다.</summary>
        private static IReadOnlyList<CardContentSource> Read(
            string contentRoot, string folderName, List<string> errors)
        {
            var directory = Path.Combine(contentRoot, folderName);
            if (!Directory.Exists(directory))
            {
                errors.Add("Content directory not found: " + directory);
                return new CardContentSource[0];
            }

            return CardContentFiles.ReadDirectory(directory);
        }
    }
}
