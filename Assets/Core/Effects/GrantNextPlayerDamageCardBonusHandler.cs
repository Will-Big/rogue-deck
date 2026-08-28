using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Effects
{
    public sealed class GrantNextPlayerDamageCardBonusHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.GrantNextPlayerDamageCardBonus;

        public CardTargetKey? TargetFor(CardDefinition card, EffectData effect) => null;

        public void Apply(EffectContext ctx)
        {
            var currentIndex = ctx.ResolutionContext.IndexOf(ctx.Card);
            for (int i = currentIndex + 1; i < ctx.ResolutionContext.Order.Count; i++)
            {
                var card = ctx.ResolutionContext.Order[i];
                if (card.Def.Side == Side.Player
                    && card.Def.HasEffect(EffectKeys.Damage))
                {
                    card.AddPendingDamageBonus(ctx.EffectValue);
                    if (ctx.EffectValue != 0)
                    {
                        ctx.ExtraEvents.Add(new Events.CardBuffGranted(
                            card.InstanceId, card.Def.Id,
                            Events.CardBuffIds.DamageBonus, ctx.EffectValue));
                    }
                    ctx.TargetId = card.Def.Id;
                    return;
                }
            }
        }
    }
}
