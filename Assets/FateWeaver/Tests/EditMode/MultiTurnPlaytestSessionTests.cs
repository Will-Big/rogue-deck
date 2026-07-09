using System;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class MultiTurnPlaytestSessionTests
    {
        [Test]
        public void Resolves_each_turn_and_advances_to_completion()
        {
            var session = new MultiTurnPlaytestSession(SampleMultiTurnScenarios.Chapter8ThreeTurnOpening());

            Assert.AreEqual(0, session.TurnIndex);
            Assert.AreEqual(3, session.TurnCount);
            Assert.IsFalse(session.CurrentTurnResolved);

            session.ResolveTurn();
            Assert.IsTrue(session.CurrentTurnResolved);
            Assert.IsFalse(session.IsComplete);
            Assert.IsTrue(session.AdvanceTurn());
            Assert.AreEqual(1, session.TurnIndex);
            Assert.IsFalse(session.CurrentTurnResolved); // fresh zone for the new turn

            session.ResolveTurn();
            Assert.IsTrue(session.AdvanceTurn());
            Assert.AreEqual(2, session.TurnIndex);

            session.ResolveTurn();
            Assert.IsTrue(session.IsComplete);     // last turn resolved
            Assert.IsFalse(session.AdvanceTurn()); // nothing after the last turn
        }

        [Test]
        public void Fate_manipulation_during_a_turn_changes_resolution()
        {
            // MarkCombo (1 turn): unmanipulated the enemy resolves first so mark stays Basic;
            // delaying the enemy by hand completes the combo.
            var session = new MultiTurnPlaytestSession(SampleMultiTurnScenarios.MarkCombo());
            session.ApplyInterventionAction(
                new InterventionActionData(InterventionActionKeys.ChangeInitiative, cost: 1, amount: 3), "goblin_jab");

            var timeline = session.ResolveTurn();
            var mark = timeline.OfType<CardResolved>().Single(e => e.CardId == "mark");

            Assert.AreEqual(ConditionTier.Success, mark.ConditionTier);
        }

        [Test]
        public void Cannot_manipulate_after_the_turn_is_resolved()
        {
            var session = new MultiTurnPlaytestSession(SampleMultiTurnScenarios.MarkCombo());
            session.ResolveTurn();

            Assert.Throws<InvalidOperationException>(() =>
                session.ApplyInterventionAction(
                    new InterventionActionData(InterventionActionKeys.ChangeInitiative, 1, 3), "goblin_jab"));
        }
    }
}
