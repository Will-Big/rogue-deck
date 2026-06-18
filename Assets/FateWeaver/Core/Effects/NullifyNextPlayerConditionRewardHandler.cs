using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    public sealed class NullifyNextPlayerConditionRewardHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.NullifyNextPlayerConditionReward;

        public void Apply(EffectContext ctx)
        {
            var currentIndex = ctx.ResolutionContext.IndexOf(ctx.Card);

            for (int i = currentIndex + 1; i < ctx.ResolutionContext.Order.Count; i++)
            {
                var card = ctx.ResolutionContext.Order[i];
                if (card.Def.Side == Side.Player)
                {
                    card.Statuses.Add(StatusKeys.RewardNullified, StatusLifetime.UntilConsumed(1));
                    ctx.TargetId = card.Def.Id;
                    return;
                }
            }
        }
    }
}
