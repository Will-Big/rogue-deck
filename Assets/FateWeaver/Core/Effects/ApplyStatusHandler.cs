using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Where an ApplyStatus effect puts the status, from the acting card's perspective.</summary>
    public enum StatusApplyTarget
    {
        Self,        // the card's own side entity (player card -> player; enemy card -> first enemy)
        TargetEnemy  // the card's target enemy (by TargetId, else the first enemy)
    }

    /// <summary>Applies a status (key + lifetime + magnitude) to a holder. Magnitude rides on the
    /// resolved Amount (e.g. block points). The status's lifetime/parameters live in the EffectData.</summary>
    public sealed class ApplyStatusHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.ApplyStatus;

        public void Apply(EffectContext ctx)
        {
            var effect = ctx.Effect;
            if (effect == null || effect.StatusKey == null || effect.StatusLifetime == null)
            {
                return;
            }

            var bag = ResolveBag(ctx);
            if (bag == null)
            {
                return;
            }

            bag.Add(effect.StatusKey.Value, effect.StatusLifetime.Value, ctx.Amount);
        }

        private static StatusBag ResolveBag(EffectContext ctx)
        {
            if (ctx.Effect.StatusTarget == StatusApplyTarget.Self)
            {
                if (ctx.Card.Def.Side == Side.Player)
                {
                    return ctx.State.PlayerStatuses;
                }

                return ctx.State.Enemies.Count > 0 ? ctx.State.Enemies[0].Statuses : null;
            }

            var enemy = SelectEnemy(ctx.State, ctx.Card.TargetId);
            return enemy?.Statuses;
        }

        private static Enemy SelectEnemy(CombatState state, string targetId)
        {
            if (!string.IsNullOrEmpty(targetId))
            {
                foreach (var enemy in state.Enemies)
                {
                    if (enemy.Id == targetId)
                    {
                        return enemy;
                    }
                }
            }

            return state.Enemies.Count > 0 ? state.Enemies[0] : null;
        }
    }
}
