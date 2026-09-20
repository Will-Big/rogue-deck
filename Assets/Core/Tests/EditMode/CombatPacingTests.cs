using System;
using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>전투 길이를 잰다(전투 템포 설계 「검증 절차」). Compare는 무조작 대 조작을 보는
    /// 도구라 "몇 턴에 끝나는가"를 재지 못하므로, 탐욕 스크립트로 한 판을 끝까지 돌려 턴 수를 센다.
    ///
    /// 경계가 넓은 이유는 이것이 밸런스 목표의 가드이지 정확한 값의 골든이 아니기 때문이다.
    /// 카드 한 장을 조정할 때마다 깨지면 의미가 없다. 목표(4~5턴)에서 크게 벗어날 때만 걸린다.</summary>
    public class CombatPacingTests
    {
        /// <summary>매 턴 손패의 실행 카드를 비용이 큰 것부터 에너지가 다할 때까지 낸다.
        /// 개입 카드는 건너뛴다 — 대상 선택이 필요해 스크립트로 표현할 수 없다.</summary>
        private static int TurnsToWin(string battleId, int runSeed, int maxTurns = 20)
        {
            var content = TestContent.Content();
            var run = RunSetup.NewRun(content, new[] { "member_a", "member_b" }, runSeed);
            var context = new CombatNodeContext(
                content.Statuses,
                content.CombatRules,
                new FixedBattleEncounter(
                    new ContentEncounterSource(content, CombatRegistries.EnemyPolicies()), battleId),
                new CharacterPoolRewardSource(content));
            var node = CombatNode.Begin(run, context);

            for (int turn = 0; turn < maxTurns; turn++)
            {
                PlayGreedily(node.Session);
                node.Session.ResolveTurn();
                if (node.Session.IsComplete)
                {
                    Assert.AreEqual(
                        Outcome.Win, node.Session.Outcome,
                        battleId + " 시드 " + runSeed + ": 탐욕 스크립트가 졌다.");
                    return turn + 1;
                }

                Assert.IsTrue(node.Session.BeginNextTurn());
            }

            Assert.Fail(battleId + " 시드 " + runSeed + ": " + maxTurns + "턴 안에 끝나지 않았다.");
            return -1;
        }

        private static void PlayGreedily(DeckCombatSession session)
        {
            bool played = true;
            while (played)
            {
                played = false;
                var order = session.Hand
                    .Select((card, index) => (card, index))
                    .Where(pair => pair.card.Def.Category == CardCategory.Execution
                        && pair.card.Def.EnergyCost <= session.FateEnergy)
                    .OrderByDescending(pair => pair.card.Def.EnergyCost)
                    .ToList();

                foreach (var pair in order)
                {
                    if (session.PlayExecutionCard(pair.index))
                    {
                        played = true;
                        break;
                    }
                }
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void A_single_goblin_falls_in_three_to_six_turns(int runSeed)
        {
            var turns = TurnsToWin("goblin_single", runSeed);

            Assert.That(turns, Is.InRange(3, 6), "단독 고블린 전투 턴 수: " + turns);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void The_runt_pair_falls_in_three_to_seven_turns(int runSeed)
        {
            var turns = TurnsToWin("goblin_pair", runSeed);

            Assert.That(turns, Is.InRange(3, 7), "짝 전투 턴 수: " + turns);
        }

        [Test]
        public void Flank_jab_hits_the_back_runt_while_cleave_hits_the_front()
        {
            var content = TestContent.Content();
            var runts = new ContentEncounterSource(content, CombatRegistries.EnemyPolicies())
                .For("goblin_pair").Enemies;
            var session = new DeckCombatSession(
                content.Statuses,
                new[]
                {
                    new PartyMemberLoadout(
                        "hero", "용사", 40,
                        new[] { content.Cards.Get("flank_jab"), content.Cards.Get("cleave") })
                },
                runts,
                content.CombatRules.Party,
                partyCards: null,
                fateEnergyPerTurn: 3,
                seed: 1);
            var backHpBefore = session.State.Enemies[1].Hp;
            var frontHpBefore = session.State.Enemies[0].Hp;

            while (session.PlayExecutionCard(0))
            {
            }

            session.ResolveTurn();

            Assert.AreEqual(backHpBefore - 3, session.State.Enemies[1].Hp, "파고들기는 뒷줄을 때린다.");
            Assert.AreEqual(frontHpBefore - 4, session.State.Enemies[0].Hp, "베기는 앞줄을 때린다.");
        }
    }
}
