using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>CardArt.asset은 에디터를 열지 않고 손으로 쓴 YAML이다(규칙 17). 그래서 Unity가
    /// 실제로 읽어 스프라이트 셋을 물고 있는지 여기서 확인한다 — 조용히 빈 카탈로그가 되면
    /// 적 카드 아트가 사라진다.</summary>
    public class CardArtCatalogTests
    {
        private const string AssetPath = "Assets/Unity/CardSO/CardArt.asset";
        private const int EnemyArtCount = 3;

        private static CardArtCatalog Load()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardArtCatalog>(AssetPath);
            Assert.IsNotNull(catalog, AssetPath + "를 CardArtCatalog로 읽지 못했다.");
            return catalog;
        }

        [Test]
        public void CatalogHasEveryEnemyArtEntry()
        {
            Assert.AreEqual(EnemyArtCount, Load().Count);
        }

        [Test]
        public void EveryEnemyCardResolvesToASprite()
        {
            var catalog = Load();

            foreach (var id in new[] { "goblin_jab", "crude_guard", "sly_jab" })
            {
                Assert.IsNotNull(catalog.ArtFor(id), id + "의 스프라이트가 비었다.");
            }
        }

        [Test]
        public void AnUnknownIdResolvesToNull()
        {
            Assert.IsNull(Load().ArtFor("no_such_card"));
        }
    }
}
