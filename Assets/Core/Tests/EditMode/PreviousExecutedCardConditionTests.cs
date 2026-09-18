using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>실행 이력과 실행선의 조건 계약(전투 실행 계약 스펙 §2·§6). 차례가 온 카드는 효과가
    /// 없거나 취소돼도 이력에 남는다. 주인이 죽어 실행선에서 제거된 카드는 차례가 오지 않으므로 이력에도,
    /// 앞·뒤 배치 질의에도 없다. 배치 질의(앞/뒤/인접)는 현재 실행선을 본다.</summary>
    public class PreviousExecutedCardConditionTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            r.Register(new ApplyStatusHandler());
            return r;
        }

        /// <summary>카드 해결을 무조건 무효화하는 카드 범위(CardInstance) 테스트 전용 상태 —
        /// 프로덕션의 stun을 대신한다(Task 5에서 제거). "StatusIntercepted로 취소된 카드는 이전
        /// 실행 카드 탐색에서 건너뛴다"는 조건 배선만 검증하면 되므로 상태 자체의 의미는 무관하다.</summary>
        private sealed class NullifyingBehavior : StatusBehavior
        {
            public static readonly StatusKey TestKey = new StatusKey("test_nullify");
            public override StatusKey Key => TestKey;
            public override StatusScope Scope => StatusScope.CardInstance;
            public override bool InterceptCardResolve(StatusContext ctx) => true;
        }

        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new NullifyingBehavior());
            return r;
        }

        private static CardResolved Resolved(System.Collections.Generic.List<ResolutionEvent> events, string id)
            => events.OfType<CardResolved>().Single(e => e.CardId == id);

        private static ExecutionCardInstance ConditionalCard(
            string id,
            Side side,
            int executionOrder,
            Condition condition,
            int baseDamage,
            int successDamage,
            string targetId = null)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, baseDamage) { SuccessEffectValue = successDamage } })
            {
                StartCondition = condition
            };
            return new ExecutionCardInstance(def) { TargetId = targetId };
        }

        private static ExecutionCardInstance PlainCard(
            string id, Side side, int executionOrder, int damage, string targetId = null)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def) { TargetId = targetId };
        }

        [Test]
        public void Previous_executed_condition_skips_a_card_removed_by_owner_death()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            state.Party.Add(new PartyMember("ally", "Ally", maxHp: 3));
            state.Enemies.Add(new Enemy("goblin", 100));

            // A: enemy attack, resolves, kills "ally" outright.
            var a = PlainCard("a_strike", Side.Enemy, executionOrder: 1, damage: 10);
            // B: ally가 죽으면 실행선에서 빠져 차례가 오지 않는다.
            var b = new ExecutionCardInstance(new CardDefinition(
                "b_card", "b_card", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 1) }))
            { OwnerId = "ally" };
            // C: succeeds only if the "previous executed card" is A (an enemy attack), i.e. B is skipped.
            var c = ConditionalCard("c_card", Side.Player, executionOrder: 3,
                new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage), baseDamage: 0, successDamage: 5);

            state.Zone.Add(a);
            state.Zone.Add(b);
            state.Zone.Add(c);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.AreEqual("b_card", events.OfType<CardRemoved>().Single().CardId);
            var resolvedC = Resolved(events, "c_card");
            Assert.AreEqual(ConditionTier.Success, resolvedC.ConditionTier);
            Assert.AreEqual(5, resolvedC.DamageDealt);
        }

        // V04: 차례가 온 카드는 효과가 없어도 이력에 남는다.
        [Test]
        public void Previous_executed_condition_counts_cards_whose_turn_came_even_without_effect()
        {
            // --- 대상 없음 case: 효과가 대상을 못 찾아 미적용돼도 카드는 실행된다 ---
            {
                var state = new CombatState(TestContent.Statuses());
                state.AddSoloPlayer(30);
                state.Enemies.Add(new Enemy("goblin", 100));

                var a = PlainCard("a_hit", Side.Enemy, executionOrder: 1, damage: 2);
                var b = new ExecutionCardInstance(new CardDefinition("b_hit", "b_hit", Side.Player, 2,
                    new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 3) }))
                {
                    OwnerId = "no-such-member"
                };
                var c = ConditionalCard("c_hit", Side.Player, executionOrder: 3,
                    new PreviousExecutedCardIs(Side.Enemy), baseDamage: 0, successDamage: 6);

                state.Zone.Add(a);
                state.Zone.Add(b);
                state.Zone.Add(c);

                var events = new TurnResolver(Registry()).Resolve(state, 0);

                Assert.IsEmpty(events.OfType<CardCancelled>());
                Assert.IsNull(Resolved(events, "b_hit").TargetId);
                var resolvedC = Resolved(events, "c_hit");
                Assert.AreEqual(ConditionTier.Basic, resolvedC.ConditionTier);
                Assert.AreEqual(0, resolvedC.DamageDealt);
            }

            // --- StatusIntercepted case ---
            {
                var state = new CombatState(TestContent.Statuses());
                state.AddSoloPlayer(30);
                state.Enemies.Add(new Enemy("goblin", 100));

                var a = PlainCard("a_hit2", Side.Enemy, executionOrder: 1, damage: 2);
                var b = PlainCard("b_hit2", Side.Player, executionOrder: 2, damage: 1);
                b.Statuses.Add(NullifyingBehavior.TestKey, StatusLifetime.UntilConsumed(1));
                var c = ConditionalCard("c_hit2", Side.Player, executionOrder: 3,
                    new PreviousExecutedCardIs(Side.Enemy), baseDamage: 0, successDamage: 6);

                state.Zone.Add(a);
                state.Zone.Add(b);
                state.Zone.Add(c);

                var events = new TurnResolver(Registry(), Statuses()).Resolve(state, 0);

                Assert.AreEqual(
                    CardCancellationReason.StatusIntercepted,
                    events.OfType<CardCancelled>().Single(e => e.CardId == "b_hit2").Reason);
                var resolvedC = Resolved(events, "c_hit2");
                Assert.AreEqual(ConditionTier.Basic, resolvedC.ConditionTier);
                Assert.AreEqual(0, resolvedC.DamageDealt);
            }
        }

        [Test]
        public void Next_adjacent_condition_keeps_existing_frozen_order_semantics()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 100));

            // A's condition looks at the frozen next slot (B). B later gets cancelled by the
            // nullifying status, but that must not retroactively change A's already-evaluated tier.
            var a = ConditionalCard("a_card", Side.Player, executionOrder: 1,
                new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage),
                baseDamage: 1, successDamage: 9);
            var b = PlainCard("b_card", Side.Enemy, executionOrder: 2, damage: 3);
            b.Statuses.Add(NullifyingBehavior.TestKey, StatusLifetime.UntilConsumed(1));

            state.Zone.Add(a);
            state.Zone.Add(b);

            var events = new TurnResolver(Registry(), Statuses()).Resolve(state, 0);

            var resolvedA = Resolved(events, "a_card");
            Assert.AreEqual(ConditionTier.Success, resolvedA.ConditionTier);
            Assert.AreEqual(9, resolvedA.DamageDealt);
            Assert.AreEqual(
                CardCancellationReason.StatusIntercepted,
                events.OfType<CardCancelled>().Single(e => e.CardId == "b_card").Reason);
        }

        [Test]
        public void Same_target_reads_the_player_card_that_executed_last()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblinA", 100));

            var a = PlainCard("a_mark", Side.Player, executionOrder: 1, damage: 1, targetId: "goblinA");
            // b의 차례가 와서 이력에 남는다. b의 TargetId는 다른 id이므로 c와 같은 대상이 아니다.
            var b = PlainCard("b_mark", Side.Player, executionOrder: 2, damage: 1, targetId: "phantom");
            var c = ConditionalCard("c_strike", Side.Player, executionOrder: 3,
                new SameTarget(), baseDamage: 0, successDamage: 8, targetId: "goblinA");

            state.Zone.Add(a);
            state.Zone.Add(b);
            state.Zone.Add(c);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.IsNotNull(Resolved(events, "b_mark"));
            var resolvedC = Resolved(events, "c_strike");
            Assert.AreEqual(ConditionTier.Basic, resolvedC.ConditionTier);
            Assert.AreEqual(0, resolvedC.DamageDealt);
        }

        [Test]
        public void Placement_conditions_count_every_card_on_the_line_including_cancelled_ones()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 100));

            var q = ConditionalCard("q_card", Side.Player, executionOrder: 1,
                new NoFollowingCardOfSide(Side.Enemy), baseDamage: 0, successDamage: 3);
            var y = PlainCard("y_card", Side.Enemy, executionOrder: 2, damage: 2);
            y.CancellationReason = CardCancellationReason.NoValidTarget; // 차례가 와도 효과가 없다
            var p = ConditionalCard("p_card", Side.Player, executionOrder: 3,
                new NoPrecedingCardOfSide(Side.Enemy), baseDamage: 0, successDamage: 7);

            state.Zone.Add(q);
            state.Zone.Add(y);
            state.Zone.Add(p);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var resolvedQ = Resolved(events, "q_card");
            Assert.AreEqual(ConditionTier.Basic, resolvedQ.ConditionTier);
            Assert.AreEqual(
                CardCancellationReason.NoValidTarget,
                events.OfType<CardCancelled>().Single(e => e.CardId == "y_card").Reason);
            // y는 실행선에서 p 앞에 있다. 효과가 없었다는 이유로 배치 질의에서 빠지지 않는다.
            var resolvedP = Resolved(events, "p_card");
            Assert.AreEqual(ConditionTier.Basic, resolvedP.ConditionTier);
            Assert.AreEqual(0, resolvedP.DamageDealt);
        }

        [Test]
        public void Placement_conditions_skip_a_card_removed_by_owner_death()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            state.Party.Add(new PartyMember("ally", "Ally", maxHp: 3));
            state.Party.Add(new PartyMember("hero", "Hero", maxHp: 30));
            state.Enemies.Add(new Enemy("goblin", 100));

            var strike = PlainCard("strike", Side.Enemy, executionOrder: 1, damage: 10);
            var allyCard = new ExecutionCardInstance(new CardDefinition(
                "ally_card", "ally_card", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 1) }))
            { OwnerId = "ally" };
            var last = ConditionalCard("last", Side.Player, executionOrder: 3,
                new AdjacentCardIs(AdjacentDirection.Previous, Side.Enemy), baseDamage: 0, successDamage: 4);

            state.Zone.Add(strike);
            state.Zone.Add(allyCard);
            state.Zone.Add(last);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            // ally_card가 제거되어 last의 바로 앞 칸은 strike(적)가 된다.
            Assert.AreEqual(ConditionTier.Success, Resolved(events, "last").ConditionTier);
        }
    }
}
