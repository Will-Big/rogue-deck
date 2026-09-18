using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;

namespace FateWeaver.Tests
{
    /// <summary>실행 카드 취소(CardCancelled)와 효과마다의 사망 정리(PartyMemberDied), 그리고 주인이 죽으면
    /// 그 주인의 차례가 오지 않은 카드를 실행선에서 빼는 것(CardRemoved)을 파티원·적 대칭으로 잠근다.</summary>
    public class CardCancellationTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        private static ExecutionCardInstance Card(
            string id,
            Side side,
            int executionOrder,
            int damage,
            string ownerId = null,
            int instanceId = -1)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) { TargetFaction = CardFixtures.Opposing(side) } }) { AllyTarget = CardTargetRange.FrontOne, EnemyTarget = CardTargetRange.FrontOne };
            return new ExecutionCardInstance(def)
            {
                OwnerId = ownerId,
                InstanceId = instanceId
            };
        }

        [Test]
        public void Owner_death_marks_only_pending_cards_owned_by_that_member()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            state.Party.Add(new PartyMember("warrior", "Warrior", maxHp: 5));
            state.Party.Add(new PartyMember("mage", "Mage", maxHp: 5));
            state.Enemies.Add(new Enemy("goblin", 100));

            // Enemy attack (FrontOne default target) kills the warrior outright.
            var enemyStrike = Card("enemy_strike", Side.Enemy, executionOrder: 1, damage: 10, instanceId: 1);
            var warriorCard = Card("warrior_card", Side.Player, executionOrder: 2, damage: 3, ownerId: "warrior", instanceId: 2);
            var mageCard = Card("mage_card", Side.Player, executionOrder: 3, damage: 3, ownerId: "mage", instanceId: 3);
            var ownerlessCard = Card("ownerless_card", Side.Player, executionOrder: 4, damage: 3, ownerId: null, instanceId: 4);

            state.Zone.Add(enemyStrike);
            state.Zone.Add(warriorCard);
            state.Zone.Add(mageCard);
            state.Zone.Add(ownerlessCard);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var removedWarriorCard = events.OfType<CardRemoved>().Single(e => e.CardId == "warrior_card");
            Assert.AreEqual(2, removedWarriorCard.InstanceId);
            Assert.AreEqual("warrior", removedWarriorCard.OwnerId);
            Assert.IsFalse(events.OfType<CardCancelled>().Any(e => e.CardId == "warrior_card"));
            Assert.IsFalse(state.Zone.Cards.Contains(warriorCard));
            Assert.AreEqual(CardExecutionState.Removed, warriorCard.ExecutionState);

            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "mage_card"));
            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "ownerless_card"));
            Assert.IsFalse(events.OfType<CardCancelled>().Any(e => e.CardId == "mage_card"));
            Assert.IsFalse(events.OfType<CardCancelled>().Any(e => e.CardId == "ownerless_card"));
        }

        // V03
        [Test]
        public void Owner_death_keeps_already_executed_cards_and_removes_only_pending_ones()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            state.Party.Add(new PartyMember("warrior", "Warrior", maxHp: 5));
            state.Party.Add(new PartyMember("mage", "Mage", maxHp: 5));
            state.Enemies.Add(new Enemy("goblin", 100));

            var earlyWarriorCard = Card("early", Side.Player, executionOrder: 1, damage: 1, ownerId: "warrior", instanceId: 1);
            var enemyStrike = Card("enemy_strike", Side.Enemy, executionOrder: 2, damage: 10, instanceId: 2);
            var lateWarriorCard = Card("late", Side.Player, executionOrder: 3, damage: 1, ownerId: "warrior", instanceId: 3);
            state.Zone.Add(earlyWarriorCard);
            state.Zone.Add(enemyStrike);
            state.Zone.Add(lateWarriorCard);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "early"));
            Assert.AreEqual("late", events.OfType<CardRemoved>().Single().CardId);
            CollectionAssert.AreEqual(new[] { earlyWarriorCard, enemyStrike }, state.Zone.Cards);
            Assert.AreEqual(CardExecutionState.Executed, earlyWarriorCard.ExecutionState);
            Assert.AreEqual(CardExecutionState.Executed, enemyStrike.ExecutionState);
            Assert.AreEqual(CardExecutionState.Removed, lateWarriorCard.ExecutionState);
        }

        [Test]
        public void Removal_follows_the_death_inside_the_killing_card()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            state.Party.Add(new PartyMember("warrior", "Warrior", maxHp: 5));
            state.Party.Add(new PartyMember("mage", "Mage", maxHp: 5));
            state.Enemies.Add(new Enemy("goblin", 100));
            state.Zone.Add(Card("enemy_strike", Side.Enemy, executionOrder: 1, damage: 10, instanceId: 1));
            state.Zone.Add(Card("warrior_card", Side.Player, executionOrder: 2, damage: 1, ownerId: "warrior", instanceId: 2));
            state.Zone.Add(Card("mage_card", Side.Player, executionOrder: 3, damage: 1, ownerId: "mage", instanceId: 3));

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var kinds = events
                .Where(e => e is CardResolved || e is PartyMemberDied || e is CardRemoved)
                .Select(e => e.GetType().Name)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "CardResolved", "PartyMemberDied", "CardRemoved", "CardResolved" }, kinds);
        }

        [Test]
        public void A_card_whose_effect_finds_no_target_still_resolves_without_cancelling()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            // 적이 없다 -> 피해 효과는 대상이 없어 미적용될 뿐, 카드는 실행된다(스펙 §2).
            var strike = Card("strike", Side.Player, executionOrder: 1, damage: 4);
            state.Zone.Add(strike);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.IsFalse(events.OfType<CardCancelled>().Any());
            var resolved = events.OfType<CardResolved>().Single();
            Assert.AreEqual(0, resolved.DamageDealt);
            Assert.IsNull(resolved.TargetId);
            Assert.IsNull(strike.CancellationReason);
        }

        [Test]
        public void Card_cancelled_event_contains_instance_owner_and_reason()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var card = Card("sealed_blade", Side.Player, executionOrder: 1, damage: 4, ownerId: "warrior", instanceId: 42);
            // Pre-cancelled before resolution starts (step 6, part 1 of the death-sweep order).
            card.CancellationReason = CardCancellationReason.StatusIntercepted;
            state.Zone.Add(card);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var cancelled = events.OfType<CardCancelled>().Single();
            Assert.AreEqual(42, cancelled.InstanceId);
            Assert.AreEqual("sealed_blade", cancelled.CardId);
            Assert.AreEqual("warrior", cancelled.OwnerId);
            Assert.AreEqual(CardCancellationReason.StatusIntercepted, cancelled.Reason);
        }

        [Test]
        public void Kill_then_no_target_resolves_then_death_then_owner_removal()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var memberA = new PartyMember("a", "A", maxHp: 5);
            state.Party.Add(memberA);
            state.Enemies.Add(new Enemy("goblin", 100));

            var killThenCancel = new CardDefinition(
                "kill_then_cancel",
                "kill_then_cancel",
                Side.Enemy,
                1,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 5) { TargetFaction = CardTargetFaction.Ally },
                    new EffectData(EffectKeys.Damage, 1) { TargetFaction = CardTargetFaction.Ally }
                }) { AllyTarget = CardTargetRange.FrontOne };
            state.Zone.Add(new ExecutionCardInstance(killThenCancel) { InstanceId = 1, OwnerId = "goblin" });
            state.Zone.Add(Card(
                "a_pending",
                Side.Player,
                executionOrder: 2,
                damage: 3,
                ownerId: memberA.Id,
                instanceId: 2));

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            // 둘째 피해는 대상이 없어 미적용될 뿐 카드를 취소하지 않는다(스펙 §2).
            var relevant = events
                .Where(e => e is CardResolved || e is CardCancelled || e is PartyMemberDied || e is CardRemoved)
                .ToArray();
            Assert.AreEqual(3, relevant.Length);
            Assert.AreEqual(typeof(CardResolved), relevant[0].GetType());
            var current = (CardResolved)relevant[0];
            Assert.AreEqual("kill_then_cancel", current.CardId);
            Assert.AreEqual(5, current.DamageDealt);

            Assert.AreEqual(typeof(PartyMemberDied), relevant[1].GetType());
            var died = (PartyMemberDied)relevant[1];
            Assert.AreEqual(memberA.Id, died.MemberId);

            Assert.AreEqual(typeof(CardRemoved), relevant[2].GetType());
            Assert.AreEqual("a_pending", ((CardRemoved)relevant[2]).CardId);
            Assert.IsFalse(events.OfType<CardCancelled>().Any());
        }

        [Test]
        public void Enemy_death_removes_that_enemys_own_pending_cards()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 5));

            // The player card kills the goblin before the goblin's own card gets its turn.
            var playerKill = Card("player_kill", Side.Player, executionOrder: 1, damage: 9, instanceId: 1);
            var goblinStrike = Card(
                "goblin_strike", Side.Enemy, executionOrder: 2, damage: 4, ownerId: "goblin", instanceId: 2);
            state.Zone.Add(playerKill);
            state.Zone.Add(goblinStrike);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "goblin"));
            var removed = events.OfType<CardRemoved>().Single(e => e.CardId == "goblin_strike");
            Assert.AreEqual("goblin", removed.OwnerId);
            Assert.IsFalse(events.OfType<CardResolved>().Any(e => e.CardId == "goblin_strike"));
            Assert.AreEqual(30, state.Party[0].Hp);
        }

        [Test]
        public void Enemy_death_removes_only_pending_cards_owned_by_that_enemy()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("front", 5));
            state.Enemies.Add(new Enemy("back", 100));

            // FrontOne default targeting kills "front" only; "back" keeps acting.
            var playerKill = Card("player_kill", Side.Player, executionOrder: 1, damage: 9, instanceId: 1);
            var frontStrike = Card(
                "front_strike", Side.Enemy, executionOrder: 2, damage: 4, ownerId: "front", instanceId: 2);
            var backStrike = Card(
                "back_strike", Side.Enemy, executionOrder: 3, damage: 3, ownerId: "back", instanceId: 3);
            state.Zone.Add(playerKill);
            state.Zone.Add(frontStrike);
            state.Zone.Add(backStrike);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.AreEqual("front_strike", events.OfType<CardRemoved>().Single().CardId);
            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "back_strike"));
            Assert.AreEqual(27, state.Party[0].Hp); // back's 3 damage only
        }

        [Test]
        public void Enemy_death_leaves_already_resolved_and_ownerless_enemy_cards_alone()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 5));

            // Resolves before the death; must stay resolved.
            var earlyStrike = Card(
                "early_strike", Side.Enemy, executionOrder: 1, damage: 2, ownerId: "goblin", instanceId: 1);
            var playerKill = Card("player_kill", Side.Player, executionOrder: 2, damage: 9, instanceId: 2);
            // No owner recorded: the sweep must not guess an owner for it.
            var ownerless = Card(
                "ownerless_strike", Side.Enemy, executionOrder: 3, damage: 1, ownerId: null, instanceId: 3);
            state.Zone.Add(earlyStrike);
            state.Zone.Add(playerKill);
            state.Zone.Add(ownerless);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "early_strike"));
            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "ownerless_strike"));
            Assert.IsFalse(events.OfType<CardCancelled>().Any());
            Assert.IsFalse(events.OfType<CardRemoved>().Any());
            Assert.AreEqual(27, state.Party[0].Hp);
        }

        [Test]
        public void Duplicate_card_ids_are_distinguished_by_instance_id()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 100));

            var first = Card("slash", Side.Player, executionOrder: 1, damage: 3, instanceId: 10);
            var second = Card("slash", Side.Player, executionOrder: 2, damage: 5, instanceId: 20);
            state.Zone.Add(first);
            state.Zone.Add(second);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var resolved = events.OfType<CardResolved>().Where(e => e.CardId == "slash").ToArray();
            Assert.AreEqual(2, resolved.Length);
            Assert.IsTrue(resolved.Any(e => e.InstanceId == 10 && e.DamageDealt == 3));
            Assert.IsTrue(resolved.Any(e => e.InstanceId == 20 && e.DamageDealt == 5));
        }
    }
}
