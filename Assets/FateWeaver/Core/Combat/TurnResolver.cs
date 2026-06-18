using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>Freezes the zone order at resolution, runs each card's effects, emits the event timeline.</summary>
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
                if (IsResolveIntercepted(card))
                {
                    // e.g. Stun: the card is present but its resolution is nullified.
                    events.Add(new CardResolved(card.Def.Id, card.Def.Side, 0, null, ConditionTier.Basic));
                    continue;
                }

                int totalDamage = 0;
                string targetId = null;
                var strongestTier = ConditionTier.Basic;

                foreach (var effect in card.Def.Effects)
                {
                    var tier = ResolveTier(effect, card, resolutionContext);
                    if (tier > strongestTier)
                    {
                        strongestTier = tier;
                    }

                    var ctx = new EffectContext
                    {
                        Card = card,
                        State = state,
                        ResolutionContext = resolutionContext,
                        StatusRegistry = _statuses,
                        Amount = ResolveAmount(effect, tier)
                    };
                    _effects.Resolve(effect.Key).Apply(ctx);
                    totalDamage += ctx.DamageDealt;
                    if (ctx.TargetId != null) targetId = ctx.TargetId;
                }

                events.Add(new CardResolved(card.Def.Id, card.Def.Side, totalDamage, targetId, strongestTier));
            }

            events.Add(new TurnEnded(turnIndex, ComputeOutcome(state)));
            return events;
        }

        private bool IsResolveIntercepted(ActionCardInstance card)
        {
            if (_statuses == null)
            {
                return false;
            }

            foreach (var status in card.Statuses.All)
            {
                if (_statuses.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.CardInstance
                    && behavior.InterceptCardResolve(new StatusContext { Instance = status }))
                {
                    return true;
                }
            }

            return false;
        }

        private static ConditionTier ResolveTier(
            Cards.EffectData effect,
            ActionCardInstance card,
            ResolutionContext resolutionContext)
        {
            if (effect.Condition == null)
            {
                return ConditionTier.Basic;
            }

            var tier = ConditionEvaluator.Evaluate(effect.Condition, card, resolutionContext);
            return tier == ConditionTier.Success && card.Statuses.Has(StatusKeys.RewardNullified)
                ? ConditionTier.Basic
                : tier;
        }

        private static int ResolveAmount(Cards.EffectData effect, ConditionTier tier)
            => tier == ConditionTier.Success && effect.SuccessAmount.HasValue
                ? effect.SuccessAmount.Value
                : effect.Amount;

        private static Outcome ComputeOutcome(CombatState state)
        {
            if (state.PlayerHp <= 0) return Outcome.Lose;
            if (state.Enemies.All(e => e.Hp <= 0)) return Outcome.Win;
            return Outcome.Ongoing;
        }
    }
}
