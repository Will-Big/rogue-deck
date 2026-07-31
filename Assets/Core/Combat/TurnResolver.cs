using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>Freezes the zone order at resolution, runs each card's effects, emits the event timeline.
    /// Per card: intercept/pre-cancellation check, then effects (with a per-effect death-sweep snapshot),
    /// then either CardResolved or CardCancelled, followed by pending survive/death events from effects
    /// that already applied. See the class-level design note in the Task 3 brief for the exact ordering.</summary>
    public sealed class TurnResolver
    {
        private readonly EffectRegistry _effects;
        private readonly StatusRegistry _statuses;

        public TurnResolver(EffectRegistry effects, StatusRegistry statuses = null)
        {
            _effects = effects;
            _statuses = statuses;
        }

        public List<ResolutionEvent> Resolve(CombatState state, int turnIndex)
        {
            var events = new List<ResolutionEvent> { new TurnStarted(turnIndex) };
            var resolutionContext = ResolutionContext.From(state);

            foreach (var card in resolutionContext.Order)
            {
                ResolveCard(state, resolutionContext, card, events);
            }

            EndOfTurnMaintenance(state, events);
            events.Add(new TurnEnded(turnIndex, ComputeOutcome(state)));
            return events;
        }

        private void ResolveCard(
            CombatState state,
            ResolutionContext resolutionContext,
            ExecutionCardInstance card,
            List<ResolutionEvent> events)
        {
            // Step 6 (part 1): a cancellation reason recorded before this card's turn to resolve
            // (OwnerDied from an earlier card's death sweep this same turn) skips effects entirely.
            if (card.CancellationReason == null && IsInterceptedByStatus(card))
            {
                card.CancellationReason = CardCancellationReason.StatusIntercepted;
            }

            if (card.CancellationReason != null)
            {
                events.Add(new CardCancelled(card.InstanceId, card.Def.Id, card.OwnerId, card.CancellationReason.Value));
                return;
            }

            int totalDamage = 0;
            string targetId = null;
            var strongestTier = ConditionTier.Basic;
            var pendingDeathEvents = new List<ResolutionEvent>();

            var handlers = card.Def.Effects
                .Select(effect => _effects.Resolve(effect.Key))
                .ToArray();
            var targetBindings = card.Def.Effects
                .Select((effect, index) => (Effect: effect, Key: handlers[index].TargetFor(card.Def, effect)))
                .ToArray();
            var targetKeys = targetBindings
                .Where(binding => binding.Key.HasValue)
                .Select(binding => binding.Key.Value)
                .ToArray();
            var legacyExplicitTargetKeys = targetBindings
                .Where(binding => binding.Key.HasValue
                    && card.Def.Side == Cards.Side.Player
                    && !string.IsNullOrEmpty(card.TargetId)
                    && binding.Effect.TargetSelector == null
                    && binding.Key.Value.Faction == Cards.CardTargetFaction.Enemy)
                .Select(binding => binding.Key.Value)
                .ToArray();
            var targets = CardTargetSnapshot.Capture(
                state, card, targetKeys, legacyExplicitTargetKeys);

            for (var effectIndex = 0; effectIndex < card.Def.Effects.Count; effectIndex++)
            {
                if (card.CancellationReason != null)
                {
                    break;
                }

                var effect = card.Def.Effects[effectIndex];
                var tier = ResolveTier(effect, card, resolutionContext);
                if (tier > strongestTier)
                {
                    strongestTier = tier;
                }

                if (effect.SkipOnBasic && effect.Condition != null && tier == ConditionTier.Basic)
                {
                    continue;
                }

                var beforeSnapshot = SnapshotParty(state);
                var enemiesBefore = SnapshotEnemies(state);

                var ctx = new EffectContext
                {
                    Card = card,
                    State = state,
                    ResolutionContext = resolutionContext,
                    StatusRegistry = _statuses,
                    Effect = effect,
                    EffectValue = ResolveEffectValue(effect, tier),
                    Targets = targets
                };
                handlers[effectIndex].Apply(ctx);
                totalDamage += ctx.DamageDealt;
                if (ctx.TargetId != null)
                {
                    targetId = ctx.TargetId;
                }
                else if (targetBindings[effectIndex].Key.HasValue)
                {
                    targetId = null;
                }
                pendingDeathEvents.AddRange(ctx.ExtraEvents);   // 틱 이벤트가 사망 이벤트보다 앞서도록

                CollectDeathSweepEvents(state, beforeSnapshot, pendingDeathEvents);
                CollectEnemyDeathEvents(state, enemiesBefore, pendingDeathEvents);

                // Step 6 (part 2): once an effect records NoValidTarget, the card is cancelled and
                // its remaining effects must not run (enforced centrally here, not per-handler).
                if (card.CancellationReason != null)
                {
                    break;
                }
            }

            var newlyDeadMemberIds = pendingDeathEvents.OfType<PartyMemberDied>().Select(e => e.MemberId);

            if (card.CancellationReason == null)
            {
                // Step 4: CardResolved first, LastExecutedCard updates, then the pending survive/death
                // events in the order they occurred (so a death caused by this card's own effects
                // follows its CardResolved immediately).
                events.Add(new CardResolved(
                    card.InstanceId, card.OwnerId, card.Def.Id, card.Def.Side, totalDamage, targetId, strongestTier));
                resolutionContext.MarkExecuted(card);
                events.AddRange(pendingDeathEvents);
            }
            else
            {
                // Step 6: a card cancelled mid-effects (NoValidTarget) emits no CardResolved and one
                // CardCancelled. State-change events from earlier, already-applied effects follow in
                // occurrence order, then the OwnerDied sweep below uses the same newly-dead set.
                events.Add(new CardCancelled(card.InstanceId, card.Def.Id, card.OwnerId, card.CancellationReason.Value));
                events.AddRange(pendingDeathEvents);
            }

            // Step 5: mark OwnerDied on every not-yet-resolved card owned by a member who just died,
            // regardless of whether the current card itself ended up resolved or cancelled.
            foreach (var memberId in newlyDeadMemberIds)
            {
                MarkOwnerDiedForFutureCards(resolutionContext, card, memberId);
            }
        }

        /// <summary>Snapshots (IsAlive, SurviveCharges) for every party member immediately before an
        /// effect applies, so the caller can diff after the effect and detect a death or a
        /// SurviveCharges-consuming save. HP alone (e.g. "HP == 1") is never the trigger.</summary>
        private static Dictionary<string, (bool IsAlive, int SurviveCharges)> SnapshotParty(CombatState state)
        {
            var snapshot = new Dictionary<string, (bool, int)>();
            foreach (var member in state.Party)
            {
                snapshot[member.Id] = (member.IsAlive, member.SurviveCharges);
            }

            return snapshot;
        }

        /// <summary>Diffs the party against a pre-effect snapshot and appends DeathsDoorSurvived /
        /// PartyMemberDied to the pending list for any member whose state actually changed this effect.
        /// A newly-dead member also gets OnHolderDied dispatched on every status it carried.</summary>
        private void CollectDeathSweepEvents(
            CombatState state,
            Dictionary<string, (bool IsAlive, int SurviveCharges)> before,
            List<ResolutionEvent> pending)
        {
            foreach (var member in state.Party)
            {
                var prior = before[member.Id];

                if (member.SurviveCharges < prior.SurviveCharges && member.IsAlive)
                {
                    pending.Add(new DeathsDoorSurvived(member.Id));
                }
                else if (prior.IsAlive && !member.IsAlive)
                {
                    pending.Add(new PartyMemberDied(member.Id));
                    DispatchHolderDied(state, member.Statuses, member.Id, pending);
                }
            }
        }

        private static Dictionary<string, bool> SnapshotEnemies(CombatState state)
        {
            var snapshot = new Dictionary<string, bool>();
            foreach (var enemy in state.Enemies)
            {
                snapshot[enemy.Id] = enemy.Hp > 0;
            }

            return snapshot;
        }

        /// <summary>Diffs enemies against a pre-effect snapshot; a newly-dead enemy emits EnemyDied and
        /// dispatches OnHolderDied on every status it carried.</summary>
        private void CollectEnemyDeathEvents(
            CombatState state, Dictionary<string, bool> before, List<ResolutionEvent> pending)
        {
            foreach (var enemy in state.Enemies)
            {
                if (before.TryGetValue(enemy.Id, out var wasAlive) && wasAlive && enemy.Hp <= 0)
                {
                    pending.Add(new EnemyDied(enemy.Id));
                    DispatchHolderDied(state, enemy.Statuses, enemy.Id, pending);
                }
            }
        }

        private void DispatchHolderDied(
            CombatState state, StatusBag bag, string holderId, List<ResolutionEvent> events)
        {
            if (_statuses == null)
            {
                return;
            }

            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior))
                {
                    behavior.OnHolderDied(new StatusDeathContext
                    {
                        Instance = status,
                        HolderBag = bag,
                        HolderId = holderId,
                        State = state,
                        Events = events
                    });
                }
            }
        }

        /// <summary>Records OwnerDied on every card later in the frozen resolution order that belongs
        /// to the given (now-dead) party member and has not already concluded.</summary>
        private static void MarkOwnerDiedForFutureCards(
            ResolutionContext resolutionContext,
            ExecutionCardInstance current,
            string deadMemberId)
        {
            var currentIndex = resolutionContext.IndexOf(current);
            for (int i = currentIndex + 1; i < resolutionContext.Order.Count; i++)
            {
                var future = resolutionContext.Order[i];
                if (future.CancellationReason == null && future.OwnerId == deadMemberId)
                {
                    future.CancellationReason = CardCancellationReason.OwnerDied;
                }
            }
        }

        private bool IsInterceptedByStatus(ExecutionCardInstance card)
        {
            if (_statuses == null)
            {
                return false;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(card.Statuses.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.CardInstance
                    && behavior.InterceptCardResolve(new StatusContext { Instance = status }))
                {
                    card.Statuses.Consume(status);
                    return true;
                }
            }

            return false;
        }

        private void EndOfTurnMaintenance(CombatState state, List<ResolutionEvent> events)
        {
            var partyBefore = SnapshotParty(state);
            var enemiesBefore = SnapshotEnemies(state);

            RunTurnEndTicks(state, events);

            CollectDeathSweepEvents(state, partyBefore, events);
            CollectEnemyDeathEvents(state, enemiesBefore, events);

            foreach (var member in state.Party)
            {
                member.Statuses.EndOfTurn();
            }

            foreach (var enemy in state.Enemies)
            {
                enemy.Statuses.EndOfTurn();
            }
        }

        /// <summary>행동 턴 종료 틱: 파티 대형 순 → 적 대형 순. 보유자별로 발동 직전에 생존을 확인하므로
        /// 앞선 틱으로 이미 사망한 대상은 제외된다(카드풀 스펙 §3.2).</summary>
        private void RunTurnEndTicks(CombatState state, List<ResolutionEvent> events)
        {
            if (_statuses == null)
            {
                return;
            }

            foreach (var member in state.Party)
            {
                if (!member.IsAlive) continue;
                var target = member;
                TickHolder(target.Statuses, target.Id, damage => target.TakeDamage(damage), events);
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy.Hp <= 0) continue;
                var target = enemy;
                TickHolder(target.Statuses, target.Id, damage => target.Hp -= damage, events);
            }
        }

        private void TickHolder(
            StatusBag bag, string holderId, Action<int> dealDamage, List<ResolutionEvent> events)
        {
            // Snapshot: a hook may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior))
                {
                    behavior.OnTurnEnd(new StatusTickContext
                    {
                        Instance = status,
                        HolderBag = bag,
                        HolderId = holderId,
                        DealDamage = dealDamage,
                        Events = events
                    });
                }
            }
        }

        private static ConditionTier ResolveTier(
            Cards.EffectData effect,
            ExecutionCardInstance card,
            ResolutionContext resolutionContext)
        {
            if (effect.Condition == null)
            {
                return ConditionTier.Basic;
            }

            var tier = ConditionEvaluator.Evaluate(effect.Condition, card, resolutionContext);
            if (tier == ConditionTier.Success)
            {
                // reward-nullified disruption forces a success down to basic, spending its charge.
                var nullified = card.Statuses.Get(StatusKeys.RewardNullified);
                if (nullified != null)
                {
                    card.Statuses.Consume(nullified);
                    return ConditionTier.Basic;
                }
            }

            return tier;
        }

        private static int ResolveEffectValue(Cards.EffectData effect, ConditionTier tier)
            => tier == ConditionTier.Success && effect.SuccessEffectValue.HasValue
                ? effect.SuccessEffectValue.Value
                : effect.EffectValue;

        private static Outcome ComputeOutcome(CombatState state)
        {
            if (state.Party.All(m => !m.IsAlive)) return Outcome.Lose;
            if (state.Enemies.All(e => e.Hp <= 0)) return Outcome.Win;
            return Outcome.Ongoing;
        }
    }
}
