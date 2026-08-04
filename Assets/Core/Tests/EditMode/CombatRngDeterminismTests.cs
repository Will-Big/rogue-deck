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

        private static string RunSignature(string enemyKind, int seed)
        {
            var policy = enemyKind == "warden" ? WardenDeck.Policy() : GoblinDeck.Policy();
            var enemyId = enemyKind == "warden" ? WardenDeck.EnemyId : GoblinDeck.EnemyId;
            var enemyHp = enemyKind == "warden" ? WardenDeck.StartingHp : GoblinDeck.StartingHp;
            var session = new DeckCombatSession(TestContent.Statuses(),
                TestContent.StarterDeckCards(),
                PlayerHp,
                new[] { new Enemy(enemyId, enemyHp) },
                policy,
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

        [TestCase("goblin")]
        [TestCase("warden")]
        public void Same_seed_produces_identical_full_run(string enemyKind)
        {
            Assert.AreEqual(RunSignature(enemyKind, seed: 7), RunSignature(enemyKind, seed: 7));
            Assert.AreEqual(RunSignature(enemyKind, seed: 41), RunSignature(enemyKind, seed: 41));
        }

        [TestCase("goblin")]
        [TestCase("warden")]
        public void Different_seeds_produce_meaningful_variance(string enemyKind)
        {
            var signatures = new HashSet<string>(
                Enumerable.Range(0, 6).Select(seed => RunSignature(enemyKind, seed)));
            Assert.Greater(signatures.Count, 1);
        }
    }
}
