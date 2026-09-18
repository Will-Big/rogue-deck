using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>카드가 종료 단위다(전투 실행 계약 스펙 §2, 계획 T5). 승패는 카드 하나(직접 반응 포함)가 끝났을 때만
    /// 판정하고 패배가 우선한다. 결판이 나면 다음 카드·턴 종료 상태 처리는 수행하지 않는다.</summary>
    public class CardCompletionBoundaryTests
    {
        private static readonly EffectKey HealKey = new EffectKey("test_heal");
        private static readonly StatusKey Counter = new StatusKey("test_counter");

        /// <summary>테스트 전용 회복: 고른 아군의 HP를 수치만큼 올린다. 제품 회복 능력이 아니다.</summary>
        private sealed class HealHandler : IEffectHandler
        {
            public EffectKey Key => HealKey;

            public void Apply(EffectContext ctx)
            {
                foreach (var member in ctx.RequireTargets().Party)
                {
                    member.Hp += ctx.EffectValue;
                }
            }
        }

        /// <summary>공격받으면 공격한 쪽에게 수치만큼 피해(반응 공격에는 발동하지 않는다).</summary>
        private sealed class CounterReaction : IReactionHandler
        {
            public StatusKey Key => Counter;
            public CombatSignalKey SignalKey => CombatSignalKeys.Attacked;

            public bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal)
                => signal.Origin == EffectOrigin.Primary
                    && state.Enemies.Concat<object>(state.Party).Any(u =>
                        (u is Enemy e && e.Id == signal.TargetId && e.Hp > 0)
                        || (u is PartyMember m && m.Id == signal.TargetId && m.IsAlive));

            public IReadOnlyList<ReactionEffect> EffectsFor(StatusInstance instance, CombatSignal signal)
                => new[] { new ReactionEffect(new EffectData(EffectKeys.Damage, instance.Magnitude), ReactionTarget.SignalSource) };
        }

        private static TurnResolver Resolver()
        {
            var effects = CombatRegistries.Effects();
            effects.Register(new HealHandler());
            var reactions = CombatRegistries.Reactions();
            reactions.Register(new CounterReaction());
            return new TurnResolver(effects, CombatRegistries.Statuses(), reactions);
        }

        private static EffectData Hit(int value)
            => new EffectData(EffectKeys.Damage, value) { TargetFaction = CardTargetFaction.Enemy };

        private static EffectData Heal(int value)
            => new EffectData(HealKey, value) { TargetFaction = CardTargetFaction.Ally };

        private static ExecutionCardInstance PlayerCard(
            string id, int order, string owner, CardTargetRange? ally, CardTargetRange? enemy, params EffectData[] effects)
            => new ExecutionCardInstance(new CardDefinition(id, id, Side.Player, order,
                effects.Select((e, i) => e with { Id = "e" + i }).ToArray())
            {
                AllyTarget = ally,
                EnemyTarget = enemy
            })
            {
                OwnerId = owner
            };

        [Test]
        public void Defeat_wins_when_both_sides_are_dead()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(1).TakeDamage(1);
            state.Enemies.Add(new Enemy("e", 0));
            Assert.AreEqual(Outcome.Lose, CombatOutcomeEvaluator.Evaluate(state));
        }

        [Test]
        public void Ongoing_while_both_sides_have_someone_alive()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(1);
            state.Enemies.Add(new Enemy("e", 1));
            Assert.AreEqual(Outcome.Ongoing, CombatOutcomeEvaluator.Evaluate(state));
        }

        // V11: 수행자가 카드 도중 죽어도 남은 효과를 수행한다. 죽은 Self에게는 적용되지 않고 다른 생존 아군에게는 적용된다.
        [Test]
        public void A_card_keeps_going_after_its_owner_dies()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var p = new PartyMember("p", "P", 2);
            var q = new PartyMember("q", "Q", 10);
            state.Party.Add(p);
            state.Party.Add(q);
            var a = new Enemy("a", 20);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 5);
            state.Enemies.Add(a);
            state.Zone.Add(PlayerCard("rally", 1, "p", CardTargetRange.All, CardTargetRange.FrontOne,
                Hit(1), EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 3)));

            var events = Resolver().Resolve(state, 0);

            Assert.IsFalse(p.IsAlive, "반격으로 수행자가 죽는다");
            CollectionAssert.AreEqual(
                new[] { "q" }, events.OfType<StatusApplied>().Select(e => e.HolderId).ToArray(),
                "죽은 수행자는 대상에서 빠지고 생존 아군에게는 적용된다");
            Assert.AreEqual(Outcome.Ongoing, events.OfType<TurnEnded>().Single().Outcome, "q가 살아 있으므로 패배가 아니다");
        }

        [Test]
        public void A_dead_owner_self_effect_is_not_applied_and_the_card_resolves()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var p = new PartyMember("p", "P", 2);
            state.Party.Add(p);
            state.Party.Add(new PartyMember("q", "Q", 10));
            var a = new Enemy("a", 20);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 5);
            state.Enemies.Add(a);
            state.Zone.Add(PlayerCard("guard", 1, "p", CardTargetRange.Self, CardTargetRange.FrontOne,
                Hit(1), EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 3)));

            var events = Resolver().Resolve(state, 0);

            Assert.IsEmpty(events.OfType<StatusApplied>());
            Assert.AreEqual("guard", events.OfType<CardResolved>().Single().CardId);
        }

        // V12: 마지막 적을 처치해도 같은 카드의 뒤 효과(생존 아군 회복)는 수행하고, 카드가 끝난 뒤 승리한다.
        [Test]
        public void Killing_the_last_enemy_still_runs_the_rest_of_the_card_then_wins()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(10);
            player.Hp = 5;
            state.Enemies.Add(new Enemy("a", 3));
            state.Zone.Add(PlayerCard("finish", 1, CombatState.SoloPlayerId, CardTargetRange.Self, CardTargetRange.FrontOne,
                Hit(5), Heal(3)));

            var events = Resolver().Resolve(state, 0);

            Assert.AreEqual(8, player.Hp, "회복은 승리 판정 전에 수행된다");
            Assert.AreEqual(Outcome.Win, events.OfType<TurnEnded>().Single().Outcome);
        }

        // V13: 카드 도중 양측이 모두 전멸하면 카드가 끝난 뒤 패배다.
        [Test]
        public void Both_sides_wiped_inside_one_card_is_a_defeat()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(2);
            state.Enemies.Add(new Enemy("a", 3));
            var selfHit = new EffectData(EffectKeys.Damage, 5) { TargetFaction = CardTargetFaction.Ally };
            state.Zone.Add(PlayerCard("mutual", 1, CombatState.SoloPlayerId, CardTargetRange.Self, CardTargetRange.FrontOne,
                Hit(5), selfHit));

            var events = Resolver().Resolve(state, 0);

            Assert.AreEqual(Outcome.Lose, events.OfType<TurnEnded>().Single().Outcome);
        }

        [Test]
        public void After_a_win_later_cards_and_turn_end_statuses_do_not_run()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(10);
            player.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            state.Enemies.Add(new Enemy("a", 3));
            state.Zone.Add(PlayerCard("finish", 1, CombatState.SoloPlayerId, null, CardTargetRange.FrontOne, Hit(5)));
            var later = PlayerCard("later", 2, CombatState.SoloPlayerId, null, CardTargetRange.FrontOne, Hit(1));
            state.Zone.Add(later);

            var events = Resolver().Resolve(state, 0);

            Assert.AreEqual(Outcome.Win, events.OfType<TurnEnded>().Single().Outcome);
            Assert.AreEqual(new[] { "finish" }, events.OfType<CardResolved>().Select(e => e.CardId).ToArray());
            Assert.IsEmpty(events.OfType<StatusTicked>(), "승리 뒤 턴 종료 독은 발동하지 않는다");
            Assert.AreEqual(10, player.Hp);
            Assert.AreEqual(CardExecutionState.Pending, later.ExecutionState);
            Assert.IsInstanceOf<TurnEnded>(events.Last(), "전투 종료 이벤트는 여전히 마지막에 나온다");
        }

        [Test]
        public void A_reaction_effect_does_not_write_into_the_parent_card_results()
        {
            // 반응 효과가 부모 카드의 효과 id와 같은 id를 가져도 부모 결과표에 기록되지 않는다(결과표 분리).
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 20);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 1);
            state.Enemies.Add(a);
            state.Zone.Add(PlayerCard("two", 1, CombatState.SoloPlayerId, null, CardTargetRange.FrontOne, Hit(1), Hit(1)));

            Assert.DoesNotThrow(() => Resolver().Resolve(state, 0));
            Assert.AreEqual(18, a.Hp);
        }

        [Test]
        public void A_removed_card_is_not_executed()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("a", 20));
            var card = PlayerCard("gone", 1, CombatState.SoloPlayerId, null, CardTargetRange.FrontOne, Hit(5));
            card.ExecutionState = CardExecutionState.Removed;
            var events = new List<ResolutionEvent>();

            new CardExecutor(new EffectExecutor(CombatRegistries.Effects(), CombatRegistries.Statuses()), CombatRegistries.Statuses())
                .Execute(state, FateWeaver.Core.Conditions.ResolutionContext.From(state), card, events);

            Assert.IsEmpty(events);
            Assert.AreEqual(20, state.Enemies[0].Hp);
        }

        [Test]
        public void A_dead_members_owned_cards_leave_the_deck_at_the_moment_of_death()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            state.Party.Add(new PartyMember("p", "P", 2));
            state.Party.Add(new PartyMember("q", "Q", 10));
            state.Enemies.Add(new Enemy("a", 20));
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition("smash", "smash", Side.Enemy, 1,
                new[] { new EffectData(EffectKeys.Damage, 5) { TargetFaction = CardTargetFaction.Ally } })
            {
                AllyTarget = CardTargetRange.FrontOne
            }));
            var removed = new List<string>();
            var resolver = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses(),
                CombatRegistries.Reactions(), ownerId => removed.Add(ownerId));

            resolver.Resolve(state, 0);

            Assert.AreEqual(new[] { "p" }, removed.ToArray(), "사망 처리 경로가 죽은 주인마다 한 번 부른다");
        }
    }
}
