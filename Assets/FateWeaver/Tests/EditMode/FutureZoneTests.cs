using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class FutureZoneTests
    {
        private static ActionCardInstance Card(string id, int initiative)
        {
            var def = new CardDefinition(id, id, Side.Player, CardType.Attack, initiative,
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
    }
}
