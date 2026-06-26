using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class FutureZoneTests
    {
        private static ActionCardInstance Card(string id, int initiative, Side side = Side.Player)
        {
            var def = new CardDefinition(id, id, side, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, 1) });
            return new ActionCardInstance(def);
        }

        [Test]
        public void ResolutionOrder_is_ascending_and_stable_on_ties()
        {
            var zone = new FutureZone();
            zone.Add(Card("A", 3));
            zone.Add(Card("B", 1));
            zone.Add(Card("C", 1)); // tie with B; inserted after B

            var order = zone.ResolutionOrder().Select(c => c.Def.Id).ToArray();

            // ascending initiative; B before C because of stable tie-break
            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, order);
        }

        [Test]
        public void ResolutionOrder_prioritizes_player_cards_when_initiative_ties()
        {
            var zone = new FutureZone();
            zone.Add(Card("enemy", 2, Side.Enemy));
            zone.Add(Card("player", 2, Side.Player));
            zone.Add(Card("faster_enemy", 1, Side.Enemy));

            var order = zone.ResolutionOrder().Select(c => c.Def.Id).ToArray();

            CollectionAssert.AreEqual(new[] { "faster_enemy", "player", "enemy" }, order);
        }
    }
}
