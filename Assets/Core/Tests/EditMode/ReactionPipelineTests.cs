using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>전투 사건과 직접 반응(전투 실행 계약 스펙 §7, 계획 T4). 반응 능력은 테스트 전용 처리기로만
    /// 검증한다 — 게임 콘텐츠에 반격 카드·상태를 더하지 않는다.</summary>
    public class ReactionPipelineTests
    {
        private static readonly StatusKey Counter = new StatusKey("test_counter");
        private static readonly StatusKey GuardOnHit = new StatusKey("test_guard_on_hit");
        private static readonly StatusKey BlockWatch = new StatusKey("test_block_watch");
        private static readonly StatusKey PingWatch = new StatusKey("test_ping_watch");
        private static readonly CombatSignalKey Ping = new CombatSignalKey("test_ping");
        private static readonly EffectKey PingEffect = new EffectKey("test_emit_ping");

        /// <summary>공격받으면(생존해 있을 때) 공격한 쪽에게 상태 수치만큼 피해를 준다.</summary>
        private sealed class CounterReaction : IReactionHandler
        {
            public StatusKey Key => Counter;
            public CombatSignalKey SignalKey => CombatSignalKeys.Attacked;

            public bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal)
                => Units.IsAlive(state, signal.TargetId);

            public IReadOnlyList<ReactionEffect> EffectsFor(StatusInstance instance, CombatSignal signal)
                => new[]
                {
                    new ReactionEffect(new EffectData(EffectKeys.Damage, instance.Magnitude), ReactionTarget.SignalSource)
                };
        }

        /// <summary>공격받으면 자신에게 방어를 얻는다.</summary>
        private sealed class GuardOnHitReaction : IReactionHandler
        {
            public StatusKey Key => GuardOnHit;
            public CombatSignalKey SignalKey => CombatSignalKeys.Attacked;

            public bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal)
                => Units.IsAlive(state, signal.TargetId);

            public IReadOnlyList<ReactionEffect> EffectsFor(StatusInstance instance, CombatSignal signal)
                => new[]
                {
                    new ReactionEffect(new EffectData(EffectKeys.ApplyStatus, instance.Magnitude)
                    {
                        Payload = new ApplyStatusPayload(StatusKeys.Block)
                    }, ReactionTarget.SignalTarget)
                };
        }

        /// <summary>방어를 얻으면 적 전열 하나에게 상태 수치만큼 피해를 준다 — 공격 이외 사건에 반응.</summary>
        private sealed class BlockWatchReaction : IReactionHandler
        {
            public StatusKey Key => BlockWatch;
            public CombatSignalKey SignalKey => CombatSignalKeys.StatusGained;

            public bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal)
                => signal.Detail == StatusKeys.Block.Id && Units.IsAlive(state, signal.TargetId);

            public IReadOnlyList<ReactionEffect> EffectsFor(StatusInstance instance, CombatSignal signal)
                => new[]
                {
                    new ReactionEffect(
                        new EffectData(EffectKeys.Damage, instance.Magnitude),
                        ReactionTarget.Position(new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne)))
                };
        }

        /// <summary>테스트 전용 사건 키에 반응한다 — 새 사건 종류가 중앙 분기 없이 추가되는지.</summary>
        private sealed class PingWatchReaction : IReactionHandler
        {
            public StatusKey Key => PingWatch;
            public CombatSignalKey SignalKey => Ping;

            public bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal) => true;

            public IReadOnlyList<ReactionEffect> EffectsFor(StatusInstance instance, CombatSignal signal)
                => new[]
                {
                    new ReactionEffect(new EffectData(EffectKeys.Damage, instance.Magnitude), ReactionTarget.SignalTarget)
                };
        }

        /// <summary>대상마다 테스트 전용 사건을 낸다.</summary>
        private sealed class EmitPingHandler : IEffectHandler
        {
            public EffectKey Key => PingEffect;

            public void Apply(EffectContext ctx)
            {
                foreach (var enemy in ctx.RequireTargets().Enemies)
                {
                    ctx.Signals.Add(new CombatSignal(Ping, ctx.ActorId, enemy.Id, 0));
                }
            }
        }

        private static class Units
        {
            public static bool IsAlive(CombatState state, string id)
                => state.Party.Any(m => m.Id == id && m.IsAlive) || state.Enemies.Any(e => e.Id == id && e.Hp > 0);
        }

        private static EffectRegistry Effects()
        {
            var effects = CombatRegistries.Effects();
            effects.Register(new EmitPingHandler());
            return effects;
        }

        private static ReactionRegistry Reactions()
        {
            var reactions = CombatRegistries.Reactions();
            reactions.Register(new CounterReaction());
            reactions.Register(new GuardOnHitReaction());
            reactions.Register(new BlockWatchReaction());
            reactions.Register(new PingWatchReaction());
            return reactions;
        }

        private static TurnResolver Resolver()
            => new TurnResolver(Effects(), CombatRegistries.Statuses(), Reactions());

        private static EffectData Hit(int value)
            => new EffectData(EffectKeys.Damage, value) { TargetFaction = CardTargetFaction.Enemy };

        private static EffectData GuardSelf(int value)
            => EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, value);

        private static ExecutionCardInstance PlayerCard(CardTargetRange enemy, params EffectData[] effects)
            => new ExecutionCardInstance(new CardDefinition("card", "card", Side.Player, 1,
                effects.Select((e, i) => e with { Id = "e" + i }).ToArray())
            {
                AllyTarget = CardTargetRange.Self,
                EnemyTarget = enemy
            })
            {
                OwnerId = CombatState.SoloPlayerId
            };

        private static int IndexOf(List<ResolutionEvent> events, Func<ResolutionEvent, bool> match)
        {
            var index = events.FindIndex(e => match(e));
            Assert.GreaterOrEqual(index, 0, "event not found");
            return index;
        }

        private static bool PlayerHp(ResolutionEvent e, int before, int after)
            => e is HpChanged hp && hp.HolderId == CombatState.SoloPlayerId && hp.Before == before && hp.After == after;

        [TestCase(EffectOrigin.Primary, true)]
        [TestCase(EffectOrigin.Reaction, false)]
        public void Only_primary_effects_open_a_reaction_boundary(EffectOrigin origin, bool expected)
        {
            Assert.AreEqual(expected, ReactionDispatcher.CanDispatch(origin));
        }

        // V07: 피해 다음 방어, 상대 반격 — 반격은 방어 획득 전.
        [Test]
        public void Counter_resolves_before_the_next_effect_of_the_card()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 8);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 2);
            state.Enemies.Add(a);
            state.Zone.Add(PlayerCard(CardTargetRange.FrontOne, Hit(3), GuardSelf(3)));

            var events = Resolver().Resolve(state, 0);

            Assert.AreEqual(5, a.Hp);
            Assert.AreEqual(8, state.Party[0].Hp, "방어를 얻기 전이라 반격 2가 그대로 들어간다");
            var counter = IndexOf(events, e => PlayerHp(e, 10, 8));
            var guard = IndexOf(events, e => e is StatusApplied s && s.HolderId == CombatState.SoloPlayerId);
            Assert.Less(counter, guard);
        }

        // 스펙 §9 대표 예시: d1 피해 3 → b1 자신 방어 3 → d2 피해 3, 반격 2.
        [Test]
        public void Each_hit_triggers_its_own_counter_and_block_absorbs_the_second()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 8);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 2);
            state.Enemies.Add(a);
            var card = PlayerCard(CardTargetRange.FrontOne, Hit(3), GuardSelf(3), Hit(3));
            state.Zone.Add(card);
            var context = new CardExecutionContext(card, ConditionTier.Basic, state, ResolutionContext.From(state));
            var executor = new EffectExecutor(Effects(), CombatRegistries.Statuses(), Reactions());

            foreach (var effect in card.Def.Effects)
            {
                context.Record(effect.Id, executor.Apply(context, effect));
            }

            Assert.AreEqual(2, a.Hp);
            Assert.AreEqual(8, state.Party[0].Hp);
            Assert.AreEqual(1, state.Party[0].Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Two_hits_each_trigger_one_counter_right_after_the_hit()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 20);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 2);
            state.Enemies.Add(a);
            state.Zone.Add(PlayerCard(CardTargetRange.FrontOne, Hit(3), Hit(3)));

            var events = Resolver().Resolve(state, 0);

            var hpEvents = events.OfType<HpChanged>().Select(e => e.HolderId + ":" + e.Before + ">" + e.After).ToArray();
            CollectionAssert.AreEqual(new[] { "a:20>17", "player:10>8", "a:17>14", "player:8>6" }, hpEvents);
        }

        // V08: 광역 피해에 복수 반격 — 전체 피해·사망 반영 후 대상 목록 순서로 반응.
        [Test]
        public void All_damage_resolves_every_hit_and_death_before_reactions_in_target_order()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            var a = new Enemy("a", 10);
            var b = new Enemy("b", 2);
            var c = new Enemy("c", 10);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 1);
            b.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 5);
            c.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 2);
            state.Enemies.Add(a);
            state.Enemies.Add(b);
            state.Enemies.Add(c);
            state.Zone.Add(PlayerCard(CardTargetRange.All, Hit(3)));

            var events = Resolver().Resolve(state, 0);

            var order = events
                .Where(e => e is HpChanged || e is EnemyDied)
                .Select(e => e is HpChanged hp ? hp.HolderId + ":" + hp.After : "died:" + ((EnemyDied)e).EnemyId)
                .ToArray();
            // 죽은 b의 반격은 생존 요건으로 발동하지 않는다.
            CollectionAssert.AreEqual(
                new[] { "a:7", "b:-1", "c:7", "died:b", "player:19", "player:17" }, order);
        }

        [Test]
        public void A_non_attack_signal_triggers_its_registered_ability()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(10);
            player.Statuses.Add(BlockWatch, StatusLifetime.Permanent, magnitude: 1);
            var a = new Enemy("a", 10);
            state.Enemies.Add(a);
            state.Zone.Add(PlayerCard(CardTargetRange.FrontOne, GuardSelf(3)));

            Resolver().Resolve(state, 0);

            Assert.AreEqual(9, a.Hp, "방어 획득 사건에 등록된 능력이 발동했다");
        }

        [Test]
        public void A_new_signal_key_needs_only_a_handler_and_a_registration()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 10);
            a.Statuses.Add(PingWatch, StatusLifetime.Permanent, magnitude: 4);
            state.Enemies.Add(a);
            state.Zone.Add(PlayerCard(CardTargetRange.FrontOne,
                new EffectData(PingEffect, 0) { TargetFaction = CardTargetFaction.Enemy }));

            Resolver().Resolve(state, 0);

            Assert.AreEqual(6, a.Hp);
        }

        // V09 앞부분: Primary 피해로 죽은 전염 보유자는 사망 능력을 실행한다.
        [Test]
        public void A_primary_death_runs_the_death_ability()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var victim = new Enemy("victim", 2);
            victim.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            victim.Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));
            var next = new Enemy("next", 10);
            state.Enemies.Add(victim);
            state.Enemies.Add(next);
            state.Zone.Add(PlayerCard(CardTargetRange.FrontOne, Hit(5)));

            var events = Resolver().Resolve(state, 0);

            var transfer = events.OfType<StatusTransferred>().Single();
            Assert.AreEqual("next", transfer.ToHolderId);
            Assert.Less(IndexOf(events, e => e is EnemyDied), IndexOf(events, e => e is StatusTransferred));
        }

        // V09: 반응 피해로 죽은 사망 능력 보유자 — 사망 정리는 수행하고 능력은 발동하지 않는다.
        [Test]
        public void A_death_caused_by_a_reaction_is_cleaned_up_without_running_death_abilities()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var p = new PartyMember("p", "P", 3);
            p.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 2);
            p.Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));
            state.Party.Add(p);
            var a = new Enemy("a", 20);
            a.Statuses.Add(Counter, StatusLifetime.Permanent, magnitude: 5);
            state.Enemies.Add(a);
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition("hit", "hit", Side.Player, 1, new[] { Hit(1) })
            {
                EnemyTarget = CardTargetRange.FrontOne
            })
            {
                OwnerId = "p"
            });
            var pending = new ExecutionCardInstance(new CardDefinition("later", "later", Side.Player, 5, new[] { Hit(1) })
            {
                EnemyTarget = CardTargetRange.FrontOne
            })
            {
                OwnerId = "p"
            };
            state.Zone.Add(pending);

            var events = Resolver().Resolve(state, 0);

            Assert.IsTrue(events.OfType<PartyMemberDied>().Any(e => e.MemberId == "p"));
            Assert.AreEqual("later", events.OfType<CardRemoved>().Single().CardId, "사망 정리는 수행한다");
            Assert.IsEmpty(events.OfType<StatusTransferred>(), "반응이 만든 사망은 사망 능력을 발동하지 않는다");
            Assert.IsFalse(a.Statuses.Has(StatusKeys.Poison));
        }

        [Test]
        public void Block_gained_by_a_reaction_does_not_chain_into_another_reaction()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 20);
            a.Statuses.Add(GuardOnHit, StatusLifetime.Permanent, magnitude: 2);
            a.Statuses.Add(BlockWatch, StatusLifetime.Permanent, magnitude: 5);
            state.Enemies.Add(a);
            var card = PlayerCard(CardTargetRange.FrontOne, Hit(3));
            var context = new CardExecutionContext(card, ConditionTier.Basic, state, ResolutionContext.From(state));

            new EffectExecutor(Effects(), CombatRegistries.Statuses(), Reactions()).Apply(context, card.Def.Effects[0]);

            Assert.AreEqual(2, a.Statuses.Get(StatusKeys.Block).Magnitude, "반응 효과의 방어는 증가한다");
            Assert.AreEqual(17, a.Hp, "방어 획득 감시 능력은 반응 기원 사건에 연쇄하지 않는다");
        }

        // V10: 방어로 공격을 전부 막아도 공격받음은 있고, 체력 피해는 없다.
        [Test]
        public void A_fully_blocked_attack_signals_attacked_but_not_hp_damaged()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 10);
            a.Statuses.Stack(StatusKeys.Block, StatusLifetime.Permanent, 5);
            state.Enemies.Add(a);
            var card = PlayerCard(CardTargetRange.FrontOne, Hit(3));
            var context = new CardExecutionContext(card, ConditionTier.Basic, state, ResolutionContext.From(state));

            var result = new EffectExecutor(Effects(), CombatRegistries.Statuses(), Reactions())
                .Apply(context, card.Def.Effects[0]);

            Assert.AreEqual(10, a.Hp);
            Assert.IsTrue(result.Signals.Any(s => s.Key == CombatSignalKeys.Attacked && s.TargetId == "a"));
            Assert.IsFalse(result.Signals.Any(s => s.Key == CombatSignalKeys.HpDamaged));
        }

        // D1: 독은 관통(방어 우회)이고 피해 배율(취약)을 받지 않는다.
        [Test]
        public void Poison_tick_pierces_block_and_ignores_vulnerable()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 20);
            a.Statuses.Stack(StatusKeys.Block, StatusLifetime.Permanent, 5);
            a.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            a.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            state.Enemies.Add(a);

            var events = Resolver().Resolve(state, 0);

            Assert.AreEqual(17, a.Hp);
            Assert.AreEqual(5, a.Statuses.Get(StatusKeys.Block).Magnitude);
            Assert.AreEqual(HpChangeSource.StatusTick, events.OfType<HpChanged>().Single().Source);
        }

        // D7: 턴 시점 상태 처리는 Primary 기원 — 독 틱으로 죽은 전염 보유자의 사망 능력이 실행된다.
        [Test]
        public void A_turn_end_poison_death_runs_the_death_ability()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var victim = new Enemy("victim", 2);
            victim.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            victim.Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));
            var next = new Enemy("next", 10);
            state.Enemies.Add(victim);
            state.Enemies.Add(next);

            var events = Resolver().Resolve(state, 0);

            Assert.AreEqual("next", events.OfType<StatusTransferred>().Single().ToHolderId);
            Assert.AreEqual(4, next.Statuses.Get(StatusKeys.Poison).Magnitude, "성장한 독 전량이 옮겨간다");
        }

        [Test]
        public void Reaction_registry_rejects_a_second_handler_for_the_same_status()
        {
            var reactions = new ReactionRegistry();
            reactions.Register(new CounterReaction());

            Assert.Throws<ArgumentException>(() => reactions.Register(new CounterReaction()));
        }
    }
}
