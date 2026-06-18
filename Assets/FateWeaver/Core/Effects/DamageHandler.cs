using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Player cards hit the first enemy, enemy cards hit the player. Incoming damage is
    /// folded through the target's entity-scoped statuses (e.g. Vulnerable) when a StatusRegistry
    /// is present; with no registry it applies raw (preserves pre-status behavior).</summary>
    public sealed class DamageHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.Def.Side == Side.Player)
            {
                var target = ctx.State.Enemies[0];
                var damage = FoldIncoming(ctx, target.Statuses, ctx.Amount);
                target.Hp -= damage;
                ctx.DamageDealt = damage;
                ctx.TargetId = target.Id;
            }
            else
            {
                var damage = FoldIncoming(ctx, ctx.State.PlayerStatuses, ctx.Amount);
                ctx.State.PlayerHp -= damage;
                ctx.DamageDealt = damage;
                ctx.TargetId = "player";
            }
        }

        private static int FoldIncoming(EffectContext ctx, StatusBag bag, int damage)
        {
            if (ctx.StatusRegistry == null || bag == null)
            {
                return damage;
            }

            foreach (var status in bag.All)
            {
                if (ctx.StatusRegistry.TryResolve(status.Key, out var behavior))
                {
                    damage = behavior.ModifyIncomingDamage(damage, new StatusContext { Instance = status });
                }
            }

            return damage;
        }
    }
}
