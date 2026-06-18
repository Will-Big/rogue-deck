using System.Collections.Generic;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Fate
{
    public sealed class FatePlayResolver
    {
        private readonly FateActionRegistry _actions;

        public FatePlayResolver(FateActionRegistry actions)
        {
            _actions = actions;
        }

        public FatePlayResult Resolve(CombatState state, IReadOnlyList<FatePlay> plays)
        {
            int appliedCount = 0;
            int fateEnergySpent = 0;

            for (int i = 0; i < plays.Count; i++)
            {
                var play = plays[i];
                var handler = _actions.Resolve(play.Action.Key);
                var ctx = new FatePlayContext
                {
                    State = state,
                    Target = play.Target,
                    SecondaryTarget = play.SecondaryTarget,
                    Action = play.Action
                };

                if (!handler.CanApply(ctx))
                {
                    return new FatePlayResult(appliedCount, i, fateEnergySpent);
                }

                handler.Apply(ctx);
                appliedCount++;
                fateEnergySpent += ctx.FateEnergySpent;
            }

            return new FatePlayResult(appliedCount, -1, fateEnergySpent);
        }
    }
}
