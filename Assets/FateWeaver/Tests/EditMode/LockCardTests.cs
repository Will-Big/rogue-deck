using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class LockCardTests
    {
        private static CardDefinition LockedJab() => new CardDefinition(
            "locked_jab", "고정된 일격", Side.Enemy, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) })
            { Cost = 0, Category = CardCategory.Action, StartsLocked = true };

        [Test]
        public void Locked_enemy_card_enters_zone_locked()
        {
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[] { new[] { LockedJab() } });
            var session = new DeckCombatSession(
                new[] { new CardDefinition("p", "p", Side.Player, CardType.Attack, 6,
                    new[] { new EffectData(EffectKeys.Damage, 1) }) { Cost = 0, Category = CardCategory.Action } },
                100, new[] { new Enemy("goblin", 100) }, intent, 3, 5, 1);

            var jab = session.CurrentOrder.First(c => c.Def.Id == "locked_jab");
            Assert.IsTrue(jab.IsLocked);
        }

        [Test]
        public void Fate_cannot_reorder_a_locked_card()
        {
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[] { new[] { LockedJab() } });
            var pull = new CardDefinition("pull", "앞당김", Side.Player, CardType.Skill, 0,
                System.Array.Empty<EffectData>())
                { Cost = 1, Category = CardCategory.Fate,
                  FateAction = new FateWeaver.Core.Fate.FateActionData(
                      FateWeaver.Core.Fate.FateActionKeys.ChangeInitiative, 1, -2) };
            var session = new DeckCombatSession(
                new[] { pull }, 100, new[] { new Enemy("goblin", 100) }, intent, 3, 5, 1);

            int zoneIndex = 0;
            for (int i = 0; i < session.CurrentOrder.Count; i++)
                if (session.CurrentOrder[i].Def.Id == "locked_jab") zoneIndex = i;
            int handIndex = 0;
            for (int i = 0; i < session.Hand.Count; i++)
                if (session.Hand[i].Id == "pull") handIndex = i;

            Assert.IsFalse(session.PlayFateCard(handIndex, zoneIndex));
        }
    }
}
