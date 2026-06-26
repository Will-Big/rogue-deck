using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class StarterDeckSpecEquivalenceTests
    {
        private static CardDefinition Def(string id) =>
            CardSpecMapper.ToDefinition(StarterDeckSpecs.Build().First(s => s.Id == id));

        private static EnemyIntent Goblin(int initiative, int damage) => new EnemyIntent(
            new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", initiative, damage) }
            });

        private static int HandIndex(DeckCombatSession s, string id)
        {
            for (int i = 0; i < s.Hand.Count; i++) if (s.Hand[i].Id == id) return i;
            return -1;
        }

        private static int ZoneIndex(DeckCombatSession s, string id)
        {
            var order = s.CurrentOrder;
            for (int i = 0; i < order.Count; i++) if (order[i].Def.Id == id) return i;
            return -1;
        }

        private static int DamageOf(IReadOnlyList<ResolutionEvent> t, string id)
            => t.OfType<CardResolved>().First(e => e.CardId == id).DamageDealt;

        [Test]
        public void Spec_deck_has_same_composition()
        {
            var specs = StarterDeckSpecs.Build();
            Assert.AreEqual(10, specs.Count);
            Assert.AreEqual(7, specs.Count(s => s.Category == CardCategory.Action));
            Assert.AreEqual(3, specs.Count(s => s.Category == CardCategory.Fate));
        }

        [Test]
        public void Spec_quick_cut_pulled_first_deals_eight()
        {
            var session = new DeckCombatSession(
                new[] { Def("quick_cut"), Def("pull_forward") }, 30,
                new[] { new Enemy("goblin", 100) }, Goblin(5, 3), 3, 5, 1);
            session.PlayActionCard(HandIndex(session, "quick_cut"));
            session.PlayFateCard(HandIndex(session, "pull_forward"), ZoneIndex(session, "quick_cut"));
            Assert.AreEqual(8, DamageOf(session.ResolveTurn(), "quick_cut"));
        }

        [Test]
        public void Spec_counter_immediately_after_enemy_attack_deals_nine()
        {
            var session = new DeckCombatSession(
                new[] { Def("counter_stance") }, 30,
                new[] { new Enemy("goblin", 100) },
                Goblin(6, 4), 3, 5, 1);
            session.PlayActionCard(HandIndex(session, "counter_stance"));
            Assert.AreEqual(9, DamageOf(session.ResolveTurn(), "counter_stance"));
        }

        [Test]
        public void Spec_cover_before_enemy_attack_absorbs()
        {
            var session = new DeckCombatSession(
                new[] { Def("cover") }, 30,
                new[] { new Enemy("goblin", 100) }, Goblin(6, 3), 3, 5, 1);
            session.PlayActionCard(HandIndex(session, "cover"));
            int hp = session.State.PlayerHp;
            session.ResolveTurn();
            Assert.AreEqual(hp, session.State.PlayerHp);
        }
    }
}
