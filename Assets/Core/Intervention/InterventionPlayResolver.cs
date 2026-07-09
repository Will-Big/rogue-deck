using System.Collections.Generic;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionPlayResolver
    {
        private readonly InterventionActionRegistry _actions;

        public InterventionPlayResolver(InterventionActionRegistry actions)
        {
            _actions = actions;
        }

        public InterventionPlayResult Resolve(CombatState state, IReadOnlyList<InterventionPlay> plays)
        {
            int appliedCount = 0;
            int fateEnergySpent = 0;

            for (int i = 0; i < plays.Count; i++)
            {
                var play = plays[i];
                var handler = _actions.Resolve(play.Intervention.Key);
                var ctx = new InterventionPlayContext
                {
                    State = state,
                    Target = play.Target,
                    SecondaryTarget = play.SecondaryTarget,
                    Intervention = play.Intervention
                };

                if (!handler.CanApply(ctx))
                {
                    return new InterventionPlayResult(appliedCount, i, fateEnergySpent);
                }

                handler.Apply(ctx);
                appliedCount++;
                fateEnergySpent += ctx.FateEnergySpent;
            }

            return new InterventionPlayResult(appliedCount, -1, fateEnergySpent);
        }
    }
}
