using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>CardAsset의 등급·태그를 카드 JSON에 1회 병합한다. 등급·태그의 원본이 .asset
    /// YAML뿐이라(계획 3b 조사 5) 손 전사를 피하려고 둔다. 계획 3b Task 7이 CardAsset과 함께
    /// 이 테스트를 지운다.</summary>
    public class CardGradeTagMigrationTests
    {
        private const string ContentRoot = "Assets/StreamingAssets/Content";
        private const string CardsDirectory = ContentRoot + "/Cards";

        /// <summary>풀 후보 카드. 등급과 태그를 모두 갖는다.</summary>
        private const int PooledCardCount = 22;

        /// <summary>JSON이 있는 카드 전체 = 풀 22 + 검증용 fixture 4. fixture에도 CardAsset이
        /// 있으므로 병합 대상은 26이다 — 등급은 None이라 생략되고 빈 태그 배열만 쓰인다.
        /// 적 카드 6장은 아직 JSON이 없어 건너뛴다(계획 3b 범위 밖).</summary>
        private const int CardJsonCount = 26;

        private static CardSpec ReadCard(string cardId)
            => ContentJson.Read<CardSpec>(
                File.ReadAllText(Path.Combine(CardsDirectory, cardId + ".json")));

        [Test]
        [Explicit]
        public void Merge_grade_and_tags_into_card_json()
        {
            var merged = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CardAsset)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CardAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || string.IsNullOrEmpty(asset.Id))
                {
                    continue;
                }

                var path = Path.Combine(CardsDirectory, asset.Id + ".json");
                if (!File.Exists(path))
                {
                    continue; // 적 카드는 아직 JSON이 아니다 (계획 3b 범위 밖)
                }

                var spec = ContentJson.Read<CardSpec>(File.ReadAllText(path));
                spec.Grade = asset.Grade;
                spec.Tags = asset.Tags.ToArray();
                File.WriteAllText(path, ContentJson.Write(spec) + "\n");
                merged++;
            }

            TestContext.WriteLine("Merged grade/tags into " + merged + " card JSON files.");
            Assert.AreEqual(CardJsonCount, merged, "JSON이 있는 카드 26장에 병합되어야 한다.");
        }

        [Test]
        public void EveryPooledCardJsonHasAGradeAndTags()
        {
            var pool = ContentJson.Read<PoolSpec>(
                File.ReadAllText(Path.Combine(ContentRoot, "Pools", "starter.json")));

            Assert.AreEqual(PooledCardCount, pool.Cards.Length);
            foreach (var cardId in pool.Cards)
            {
                var spec = ReadCard(cardId);

                Assert.AreNotEqual(CardGrade.None, spec.Grade, cardId + "에 등급이 없다.");
                Assert.IsNotNull(spec.Tags, cardId + "에 태그가 없다.");
                Assert.Greater(spec.Tags.Length, 0, cardId + "에 태그가 없다.");
            }
        }

        [Test]
        public void CardJsonGradeAndTagsMatchTheAuthoredAsset()
        {
            var compared = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CardAsset)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CardAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || string.IsNullOrEmpty(asset.Id)
                    || !File.Exists(Path.Combine(CardsDirectory, asset.Id + ".json")))
                {
                    continue;
                }

                var spec = ReadCard(asset.Id);
                Assert.AreEqual(asset.Grade, spec.Grade, asset.Id + "의 등급이 어긋난다.");
                CollectionAssert.AreEqual(
                    asset.Tags.ToArray(), spec.Tags ?? new string[0],
                    asset.Id + "의 태그가 어긋난다.");
                compared++;
            }

            Assert.AreEqual(CardJsonCount, compared, "비교된 카드 수가 다르다.");
        }
    }
}
