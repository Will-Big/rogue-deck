using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    public sealed class NullifyNextPlayerConditionRewardHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.NullifyNextPlayerConditionReward;

        public CardTargetKey? TargetFor(CardDefinition card, EffectData effect) => null;

        public void Apply(EffectContext ctx)
        {
            var currentIndex = ctx.ResolutionContext.IndexOf(ctx.Card);

            for (int i = currentIndex + 1; i < ctx.ResolutionContext.Order.Count; i++)
            {
                var card = ctx.ResolutionContext.Order[i];
                if (card.Def.Side == Side.Player)
                {
                    var previous = card.Statuses.Get(StatusKeys.RewardNullified);
                    var changed = previous == null
                        || previous.Kind != StatusLifetimeKind.UntilConsumed
                        || previous.Count != 1
                        || previous.Magnitude != 0;
                    card.Statuses.Add(StatusKeys.RewardNullified, StatusLifetime.UntilConsumed(1));
                    if (changed)
                    {
                        ctx.ExtraEvents.Add(new Events.CardBuffGranted(
                            card.InstanceId, card.Def.Id, StatusKeys.RewardNullified.Id, 1));
                    }
                    ctx.TargetId = card.Def.Id;
                    return;
                }
            }
        }
    }
}
