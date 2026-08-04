using System.Collections.Generic;
using System.IO;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Statuses;

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

    /// <summary>콘텐츠 루트 하나를 받아 카탈로그 다섯을 만든다. 순서는 상태 → 카드 → 덱·풀 →
    /// 캐릭터로 고정이다 — 카드 검증이 상태 저작을, 덱·풀 로더가 카드 카탈로그를, 캐릭터 로더가
    /// 덱 카탈로그를 필요로 한다. 파일 I/O는 CardContentFiles가 맡으므로 Unity 없이 돈다.</summary>
    public static class ContentBootstrap
    {
        public static ContentBootstrapResult Load(string contentRoot)
        {
            var errors = new List<string>();

            // 상태가 가장 먼저다. 카드 검증이 "등록된 상태에는 저작된 콘텐츠가 있다"를 전제하므로
            // (ApplyStatusSpec), 그 전제를 세우는 단계가 앞서야 한다.
            var statuses = LoadStatuses(contentRoot);
            if (!statuses.Succeeded)
            {
                return ContentBootstrapResult.Failed(statuses.Errors);
            }

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
                statuses.Catalog, cards.Catalog, decks.Catalog, pools.Catalog, characters.Catalog));
        }

        /// <summary>상태 카탈로그만 읽는다. 부팅의 첫 단계이자, 카탈로그 하나만 필요한 곳
        /// (에디터 드롭다운·테스트)의 단일 진입점이다.</summary>
        public static StatusContentLoadResult LoadStatuses(string contentRoot)
        {
            var errors = new List<string>();
            var sources = Read(contentRoot, CardContentFiles.StatusesFolderName, errors);
            return errors.Count > 0
                ? StatusContentLoadResult.Failed(errors)
                : StatusContentLoader.Load(sources, AuthoringContext.Default());
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
