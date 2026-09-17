using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Enemies;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CombatNodeTests
    {
        // --- 합성 재료 -------------------------------------------------------

        private static CardDefinition Hit() => CardFixtures.Damage("hit", 5, cost: 0);

        private static RunMember Member(string id, int hitCount)
            => new RunMember(id, id, 20, 0, Enumerable.Range(0, hitCount).Select(_ => Hit()));

        private static PartyTuning Tuning() => new PartyTuning
        {
            MinPartySize = 1,
            MaxPartySize = 3,
            DrawByLivingCount = new Dictionary<int, int> { { 1, 3 }, { 2, 4 }, { 3, 5 } }
        };

        private sealed class FixedEncounter : IEncounterSource
        {
            private readonly Func<EncounterSetup> _make;
            public FixedEncounter(Func<EncounterSetup> make) => _make = make;
            public EncounterSetup Pick(Random encounterRng) => _make();
        }

        /// <summary>편성 스트림에서 실제로 뽑은 값을 기록한다 — 편성 시드가 다음 노드 순번에서
        /// 파생됨을 잠그는 테스트 전용.</summary>
        private sealed class RecordingEncounter : IEncounterSource
        {
            public readonly List<int> Recorded = new List<int>();

            public EncounterSetup Pick(Random encounterRng)
            {
                Recorded.Add(encounterRng.Next());
                return new EncounterSetup(new[]
                {
                    new EncounterEnemy(
                        new Enemy("dummy#0", "dummy", 5),
                        new SequencePolicy(new[] { new EnemyCardBundle(Array.Empty<CardDefinition>()) }))
                });
            }
        }

        /// <summary>HP 5짜리 적. turnCards가 비면 아무것도 하지 않는다.</summary>
        private static IEncounterSource Dummy(int hp = 5, params CardDefinition[] turnCards)
            => new FixedEncounter(() => new EncounterSetup(new[]
            {
                new EncounterEnemy(
                    new Enemy("dummy#0", "dummy", hp),
                    new SequencePolicy(new[] { new EnemyCardBundle(turnCards) }))
            }));

        private static CardDefinition Smash() => CardFixtures.EnemyAttack("smash", 1, 999);

        /// <summary>캐릭터 id → 풀 카드 id. 카드 정의는 id마다 하나를 공유한다.</summary>
        private sealed class FakePools : IRewardCandidateSource
        {
            private readonly Dictionary<string, string[]> _pools;
            private readonly Dictionary<string, CardDefinition> _cards = new Dictionary<string, CardDefinition>();

            public FakePools(Dictionary<string, string[]> pools) => _pools = pools;

            /// <summary>Eligible이 마지막으로 받은 생존자 목록. Session.State.Party가 아니라
            /// 이걸 단언해야 BuildOffer가 RunState.LivingMembers(죽은 사람도 포함) 대신
            /// 실제 생존자만 넘기는지 결정론적으로 잠글 수 있다.</summary>
            public IReadOnlyList<string> LastLiving { get; private set; }

            public IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds)
            {
                LastLiving = livingCharacterIds;
                var pairs = new List<RewardCandidate>();
                foreach (var characterId in livingCharacterIds)
                {
                    foreach (var cardId in _pools[characterId].OrderBy(id => id, StringComparer.Ordinal))
                    {
                        if (!_cards.TryGetValue(cardId, out var card))
                        {
                            card = CardFixtures.Damage(cardId, 1, cost: 0);
                            _cards.Add(cardId, card);
                        }

                        pairs.Add(new RewardCandidate(card, characterId));
                    }
                }

                return pairs;
            }
        }

        private static FakePools SixEach() => new FakePools(new Dictionary<string, string[]>
        {
            { "a", new[] { "a1", "a2", "a3", "a4", "a5", "a6" } },
            { "b", new[] { "b1", "b2", "b3", "b4", "b5", "b6" } }
        });

        private static CombatNodeContext Context(
            IEncounterSource encounters, IRewardCandidateSource rewards, int rewardChoices = 3)
            => new CombatNodeContext(
                TestContent.Statuses(), Tuning(), fateEnergyPerTurn: 3, rewardChoices: rewardChoices,
                encounters: encounters, rewardCandidates: rewards);

        /// <summary>turn번째 턴(0부터)에 hit 한 장을 내고 해석해 이긴다. 그 전 턴은 아무것도 내지 않는다.</summary>
        private static void WinOnTurn(CombatNode node, int turn)
        {
            for (int t = 0; t < turn; t++)
            {
                node.Session.ResolveTurn();
                Assert.IsTrue(node.Session.BeginNextTurn(), "전제: 전투가 아직 끝나지 않았어야 한다.");
            }

            Assert.IsTrue(node.Session.PlayExecutionCard(0), "전제: 손패 첫 장이 hit이어야 한다.");
            node.Session.ResolveTurn();
            Assert.AreEqual(Outcome.Win, node.Session.Outcome, "전제: 이번 턴에 이겨야 한다.");
        }

        private static string[] Describe(RewardOffer offer)
            => offer.Candidates.Select(c => c.Card.Id + "@" + c.OwnerId).ToArray();

        // --- 시작 ------------------------------------------------------------

        [Test]
        public void Begin_enters_a_node_and_seeds_the_session_from_the_combat_stream()
        {
            var run = new RunState(new[] { Member("a", 6) }, runSeed: 11);

            var node = CombatNode.Begin(run, Context(Dummy(), SixEach()));

            Assert.AreEqual(0, node.NodeIndex);
            Assert.AreEqual(1, run.NodesEntered);
            Assert.AreEqual(SeedDerivation.NodeSeed(11, 0), node.NodeSeed);
            Assert.AreEqual(
                SeedDerivation.Stream(node.NodeSeed, SeedStream.Combat),
                node.Session.State.RngSeed);
            Assert.AreEqual(CombatNodePhase.Combat, node.Phase);
            Assert.IsNull(node.Offer);
        }

        [Test]
        public void Begin_rejects_more_than_one_enemy()
        {
            var twoEnemies = new FixedEncounter(() => new EncounterSetup(new[]
            {
                new EncounterEnemy(new Enemy("dummy#0", "dummy", 5), new SequencePolicy(Array.Empty<EnemyCardBundle>())),
                new EncounterEnemy(new Enemy("dummy#1", "dummy", 5), new SequencePolicy(Array.Empty<EnemyCardBundle>()))
            }));
            var run = new RunState(new[] { Member("a", 6) }, runSeed: 11);

            Assert.Throws<InvalidOperationException>(() => CombatNode.Begin(run, Context(twoEnemies, SixEach())));
        }

        [Test]
        public void Begin_draws_the_encounter_from_the_upcoming_node_seed()
        {
            var recording = new RecordingEncounter();
            var run = new RunState(new[] { Member("a", 6) }, runSeed: 11);

            var first = CombatNode.Begin(run, Context(recording, SixEach()));
            WinOnTurn(first, 0);
            first.Conclude();
            first.Skip();

            CombatNode.Begin(run, Context(recording, SixEach()));

            Assert.AreEqual(2, recording.Recorded.Count);
            for (int i = 0; i < 2; i++)
            {
                var expected = new Random(
                    SeedDerivation.Stream(SeedDerivation.NodeSeed(11, i), SeedStream.Encounter)).Next();
                Assert.AreEqual(expected, recording.Recorded[i]);
            }
        }

        // --- 패배 ------------------------------------------------------------

        [Test]
        public void Defeat_ends_the_run_without_an_offer()
        {
            var run = new RunState(new[] { Member("a", 6) }, runSeed: 11);
            var node = CombatNode.Begin(run, Context(Dummy(99, Smash()), SixEach()));

            node.Session.ResolveTurn();
            Assert.AreEqual(Outcome.Lose, node.Session.Outcome, "전제: 999 피해로 한 턴에 져야 한다.");
            node.Conclude();

            Assert.AreEqual(CombatNodePhase.Defeated, node.Phase);
            Assert.IsNull(node.Offer);
            Assert.AreEqual(RunOutcome.Defeat, run.Outcome);
            Assert.Throws<InvalidOperationException>(() => CombatNode.Begin(run, Context(Dummy(), SixEach())));
        }

        // --- 보상 ------------------------------------------------------------

        [Test]
        public void Same_node_and_survivors_give_the_same_offer_however_the_fight_went()
        {
            var fast = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(fast, turn: 0);
            fast.Conclude();

            // turn: 3 — 카드 12장·턴당 4장 뽑기라 턴 0~2는 셔플 한 번(개전 시)의 몫을 다 쓰고,
            // 턴 3에서 버림더미가 다시 섞여 들어간다(Deck.Draw). 턴 2까지는 재셔플이 없어 전투 RNG
            // 소비량이 fast와 같아지므로, 이 전제(아래)를 실제로 갈라놓으려면 턴 3 이상이 필요하다.
            var slow = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(slow, turn: 3);
            slow.Conclude();

            Assert.AreEqual(CombatNodePhase.Reward, fast.Phase);
            CollectionAssert.AreEqual(Describe(fast.Offer), Describe(slow.Offer));
            // 전제: 두 전투가 실제로 다른 양의 전투 RNG를 소비했다는 확인. 이게 없으면 위 비교는
            // "결정 4: 보상은 전투 수행과 무관"이 아니라 우연히 같은 RNG 소비량을 비교한 것일 수
            // 있다. Offer는 이미 Conclude에서 만들어졌으므로 여기서 Rng.Next()를 더 불러도
            // 보상에는 영향이 없다.
            Assert.AreNotEqual(
                fast.Session.State.Rng.Next(), slow.Session.State.Rng.Next(),
                "전제: 두 전투의 전투 RNG 소비량이 달라야 한다.");
        }

        [Test]
        public void A_different_node_index_gives_a_different_offer()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var context = Context(Dummy(), SixEach());

            var first = CombatNode.Begin(run, context);
            WinOnTurn(first, 0);
            first.Conclude();
            first.Skip();

            var second = CombatNode.Begin(run, context);
            WinOnTurn(second, 0);
            second.Conclude();

            Assert.AreEqual(1, second.NodeIndex);
            // 우연히 같을 확률은 12·10·8분의 1 수준이다. 같게 나오면 알고리즘이 아니라 시드 우연이므로
            // 런 시드를 12로 바꾸고 그 사실을 이 주석에 적는다.
            CollectionAssert.AreNotEqual(Describe(first.Offer), Describe(second.Offer));
        }

        [Test]
        public void The_offer_matches_a_hand_rolled_reward_stream()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();

            var rng = new Random(SeedDerivation.Stream(node.NodeSeed, SeedStream.Reward));
            var pairs = SixEach().Eligible(new[] { "a", "b" }).ToList();
            var expected = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var pick = pairs[rng.Next(pairs.Count)];
                expected.Add(pick.Card.Id + "@" + pick.OwnerId);
                pairs.RemoveAll(pair => pair.Card.Id == pick.Card.Id);
            }

            CollectionAssert.AreEqual(expected, Describe(node.Offer));
        }

        [Test]
        public void Dead_characters_are_never_owners()
        {
            // a는 카드가 없고 앞줄이라 적의 999 피해에 먼저 죽는다. b의 hit이 적을 잡아 이긴다.
            var pools = SixEach();
            var run = new RunState(new[] { Member("a", 0), Member("b", 6) }, 11);
            var node = CombatNode.Begin(run, Context(Dummy(5, Smash()), pools));
            WinOnTurn(node, 0);
            Assert.IsFalse(node.Session.State.Party.First(m => m.Id == "a").IsAlive, "전제: a가 죽어야 한다.");

            node.Conclude();

            Assert.AreEqual(3, node.Offer.Candidates.Count);
            Assert.IsTrue(node.Offer.Candidates.All(c => c.OwnerId == "b"));
            Assert.IsTrue(node.Offer.Candidates.All(c => c.Card.Id.StartsWith("b")));
            // 결정론적 확인: BuildOffer가 RunState.LivingMembers(죽은 "a"도 포함)가 아니라 세션의
            // 실제 생존자만 Eligible에 넘겼다는 증거. 위 어서션들은 시드 11에서 우연히도 통과할 수
            // 있으므로 이 확인이 없으면 회귀를 못 잡는다.
            CollectionAssert.AreEqual(new[] { "b" }, pools.LastLiving);
        }

        [Test]
        public void Overlapping_pools_still_offer_distinct_cards()
        {
            var shared = new FakePools(new Dictionary<string, string[]>
            {
                { "a", new[] { "c1", "c2", "c3" } },
                { "b", new[] { "c1", "c2", "c3" } }
            });
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), shared));
            WinOnTurn(node, 0);
            node.Conclude();

            CollectionAssert.AreEquivalent(
                new[] { "c1", "c2", "c3" }, node.Offer.Candidates.Select(c => c.Card.Id).ToArray());
        }

        [Test]
        public void Fewer_distinct_cards_than_choices_offers_what_exists()
        {
            var tiny = new FakePools(new Dictionary<string, string[]>
            {
                { "a", new[] { "only_a" } },
                { "b", new[] { "only_b" } }
            });
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), tiny));
            WinOnTurn(node, 0);
            node.Conclude();

            Assert.AreEqual(2, node.Offer.Candidates.Count);
        }

        [Test]
        public void Choose_adds_the_card_to_its_owner_only()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var node = CombatNode.Begin(run, Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();
            var chosen = node.Offer.Candidates[1];
            var other = chosen.OwnerId == "a" ? "b" : "a";

            node.Choose(1);

            Assert.AreEqual(CombatNodePhase.Done, node.Phase);
            Assert.AreEqual(7, run.Party.First(m => m.Id == chosen.OwnerId).Cards.Count);
            Assert.AreSame(chosen.Card, run.Party.First(m => m.Id == chosen.OwnerId).Cards.Last());
            Assert.AreEqual(6, run.Party.First(m => m.Id == other).Cards.Count);
            Assert.Throws<InvalidOperationException>(() => node.Choose(0));
        }

        [Test]
        public void Choose_rejects_an_out_of_range_index()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();

            Assert.Throws<ArgumentOutOfRangeException>(() => node.Choose(3));
            Assert.AreEqual(CombatNodePhase.Reward, node.Phase);
        }

        [Test]
        public void Skip_leaves_every_deck_unchanged()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var node = CombatNode.Begin(run, Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();

            node.Skip();

            Assert.AreEqual(CombatNodePhase.Done, node.Phase);
            Assert.IsTrue(run.Party.All(m => m.Cards.Count == 6));
        }

        [Test]
        public void The_next_node_starts_with_the_chosen_card_in_the_deck()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var context = Context(Dummy(), SixEach());
            var first = CombatNode.Begin(run, context);
            WinOnTurn(first, 0);
            first.Conclude();
            var chosenId = first.Offer.Candidates[0].Card.Id;
            first.Choose(0);

            var next = CombatNode.Begin(run, context);

            Assert.AreEqual(1, next.NodeIndex);
            Assert.AreEqual(13, next.Session.AllDeckCards.Count);
            Assert.AreEqual(1, next.Session.AllDeckCards.Count(card => card.Def.Id == chosenId));
        }

        // --- 잘못된 호출 -----------------------------------------------------

        [Test]
        public void Conclude_before_the_combat_ends_throws()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6) }, 11), Context(Dummy(), SixEach()));

            Assert.Throws<InvalidOperationException>(node.Conclude);
        }

        [Test]
        public void Choose_and_skip_outside_the_reward_phase_throw()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6) }, 11), Context(Dummy(), SixEach()));

            Assert.Throws<InvalidOperationException>(() => node.Choose(0));
            Assert.Throws<InvalidOperationException>(node.Skip);
        }
    }
}
