using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    /// <summary>Repo invariant (AGENTS.md rule 7): same scenario + seed = same timeline. Runs the full
    /// session loop — deck shuffle, reshuffle, and RNG-driven enemy policies — and compares complete
    /// run signatures (per-turn hand order + resolution events).</summary>
    public class CombatRngDeterminismTests
    {
        private const int PlayerHp = 30;
        private const int Turns = 8;

        private static string RunSignature(int seed)
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                TestContent.StarterDeckCards(),
                PlayerHp,
                new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) },
                GoblinDeck.Policy(),
                seed: seed);

            var signature = new StringBuilder();
            for (int turn = 0; turn < Turns && !session.IsComplete; turn++)
            {
                signature.Append("hand:");
                signature.AppendLine(string.Join(",", session.Hand.Select(c => c.Def.Id)));
                foreach (var resolutionEvent in session.ResolveTurn())
                {
                    signature.AppendLine(resolutionEvent.ToString());
                }

                session.BeginNextTurn();
            }

            return signature.ToString();
        }

        [Test]
        public void Same_seed_produces_identical_full_run()
        {
            Assert.AreEqual(RunSignature(seed: 7), RunSignature(seed: 7));
            Assert.AreEqual(RunSignature(seed: 41), RunSignature(seed: 41));
        }

        [Test]
        public void Different_seeds_produce_meaningful_variance()
        {
            var signatures = new HashSet<string>(
                Enumerable.Range(0, 6).Select(seed => RunSignature(seed)));
            Assert.Greater(signatures.Count, 1);
        }
    }
}
