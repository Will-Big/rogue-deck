using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Combat
{
    /// <summary>Freezes the zone order at resolution, runs each card's effects, emits the event timeline.</summary>
    public sealed class TurnResolver
    {
        private readonly EffectRegistry _effects;

        public TurnResolver(EffectRegistry effects) => _effects = effects;

        public List<ResolutionEvent> Resolve(CombatState state, int turnIndex)
        {
            var events = new List<ResolutionEvent> { new TurnStarted(turnIndex) };

            foreach (var card in state.Zone.ResolutionOrder())
            {
                int totalDamage = 0;
                string targetId = null;

                foreach (var effect in card.Def.Effects)
                {
                    var ctx = new EffectContext
                    {
                        Card = card,
                        State = state,
                        Amount = effect.Amount
                    };
                    _effects.Resolve(effect.Key).Apply(ctx);
                    totalDamage += ctx.DamageDealt;
                    if (ctx.TargetId != null) targetId = ctx.TargetId;
                }

                events.Add(new CardResolved(card.Def.Id, card.Def.Side, totalDamage, targetId));
            }

            events.Add(new TurnEnded(turnIndex, ComputeOutcome(state)));
            return events;
        }

        private static Outcome ComputeOutcome(CombatState state)
        {
            if (state.PlayerHp <= 0) return Outcome.Lose;
            if (state.Enemies.All(e => e.Hp <= 0)) return Outcome.Win;
            return Outcome.Ongoing;
        }
    }
}
