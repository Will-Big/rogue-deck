using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class GrantNextTurnFateTests
    {
        [Test]
        public void Effect_banks_fate_for_the_next_player_turn()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("distill", "증류", Side.Player, 5,
                new[] { new EffectData(EffectKeys.GrantNextTurnFate, 1) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var effects = new EffectRegistry();
            effects.Register(new GrantNextTurnFateHandler());
            new TurnResolver(effects).Resolve(state, 0);

            Assert.AreEqual(1, state.PendingNextTurnFateEnergy);
        }

        [Test]
        public void Next_turn_refill_includes_and_clears_the_banked_bonus()
        {
            var deck = new List<CardDefinition>
            {
                new CardDefinition("distill", "증류", Side.Player, 5,
                    new[] { new EffectData(EffectKeys.GrantNextTurnFate, 1) })
                    { EnergyCost = 1, Category = CardCategory.Execution }
            };
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", 4, 0) }
            });
            var session = new DeckCombatSession(TestContent.Statuses(),
                deck, playerHp: 30,
                enemies: new[] { new Enemy("goblin", 100) },
                enemyPolicy: intent, fateEnergyPerTurn: 3, handSize: 5, seed: 1);

            Assert.IsTrue(session.PlayExecutionCard(0));
            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());

            Assert.AreEqual(4, session.FateEnergy);                       // 3 + 1
            Assert.AreEqual(0, session.State.PendingNextTurnFateEnergy);  // 소거

            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());
            Assert.AreEqual(3, session.FateEnergy);                       // 1회성
        }
    }
}
