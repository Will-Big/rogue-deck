using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Effects
{
    /// <summary>M1 damage: player cards hit the first enemy, enemy cards hit the player.</summary>
    public sealed class DamageHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.Def.Side == Side.Player)
            {
                var target = ctx.State.Enemies[0];
                target.Hp -= ctx.Amount;
                ctx.DamageDealt = ctx.Amount;
                ctx.TargetId = target.Id;
            }
            else
            {
                ctx.State.PlayerHp -= ctx.Amount;
                ctx.DamageDealt = ctx.Amount;
                ctx.TargetId = "player";
            }
        }
    }
}
