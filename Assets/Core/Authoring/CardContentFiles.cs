using System;
using System.Collections.Generic;
using System.IO;

namespace FateWeaver.Core.Authoring
{
    /// <summary>콘텐츠 디렉터리에서 *.json을 읽어 로더의 입력으로 바꾼다. 파일 I/O를 로더 밖에
    /// 격리해 로더가 순수하게 남는다. 개별 카드를 경로 문자열로 찾지 않고 디렉터리를 훑는다
    /// (AGENTS.md 규칙 2·3).</summary>
    public static class CardContentFiles
    {
        public const string CardsFolderName = "Cards";

        public static IReadOnlyList<CardContentSource> ReadDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(
                    "Card content directory not found: " + directory);
            }

            var paths = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);

            var sources = new List<CardContentSource>(paths.Length);
            foreach (var path in paths)
            {
                sources.Add(new CardContentSource(Path.GetFileName(path), File.ReadAllText(path)));
            }

            return sources;
        }
    }
}
