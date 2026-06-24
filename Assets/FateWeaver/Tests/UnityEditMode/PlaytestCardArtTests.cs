using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class PlaytestCardArtTests
    {
        [Test]
        public void Renamed_ids_map_to_art_files()
        {
            Assert.AreEqual("mark_target", PlaytestCardArt.ResolveArtName("mark"));
            Assert.AreEqual("counter_stance", PlaytestCardArt.ResolveArtName("counter"));
            Assert.AreEqual("chain_slash", PlaytestCardArt.ResolveArtName("chain"));
            Assert.AreEqual("slash", PlaytestCardArt.ResolveArtName("slash"));
            Assert.AreEqual("counter_stance", PlaytestCardArt.ResolveArtName("counter_stance"));
        }

        [Test]
        public void Resource_path_includes_cards_subfolder()
        {
            Assert.AreEqual("Cards/quick_cut", PlaytestCardArt.ResolveResourcePath("quick_cut"));
            Assert.AreEqual("Cards/counter_stance", PlaytestCardArt.ResolveResourcePath("counter_stance"));
            Assert.IsNull(PlaytestCardArt.ResolveResourcePath(""));
        }

        [Test]
        public void Suffixed_ids_normalize_by_prefix()
        {
            Assert.AreEqual("quick_cut", PlaytestCardArt.ResolveArtName("quick_cut_t3"));
            Assert.AreEqual("wrist_cut", PlaytestCardArt.ResolveArtName("wrist_cut_t2"));
            Assert.AreEqual("preemptive_thrust", PlaytestCardArt.ResolveArtName("preemptive_thrust_t1"));
            Assert.AreEqual("goblin_jab", PlaytestCardArt.ResolveArtName("goblin_jab_t1"));
        }

        [Test]
        public void Goblin_card_ids_map_to_monster_art_files()
        {
            Assert.AreEqual("goblin_jab", PlaytestCardArt.ResolveArtName("goblin_jab"));
            Assert.AreEqual("crude_guard", PlaytestCardArt.ResolveArtName("crude_guard"));
            Assert.AreEqual("sly_jab", PlaytestCardArt.ResolveArtName("sly_jab"));
        }

        [Test]
        public void Cards_without_art_resolve_to_null()
        {
            Assert.IsNull(PlaytestCardArt.ResolveArtName("prep"));
            Assert.IsNull(PlaytestCardArt.ResolveArtName(""));
        }
    }
}
