namespace FateWeaver.Core.Effects
{
    /// <summary>다음 플레이어 사용 턴에 운명력 EffectValue를 추가로 준다 (증류). CombatState에
    /// 적립만 하고, 실제 지급은 세션의 턴 시작 리필이 담당한다.</summary>
    public sealed class GrantNextTurnFateHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.GrantNextTurnFate;

        public void Apply(EffectContext ctx)
        {
            var before = ctx.State.PendingNextTurnFateEnergy;
            ctx.State.PendingNextTurnFateEnergy += ctx.EffectValue;
            var gained = ctx.State.PendingNextTurnFateEnergy - before;
            if (gained != 0)
            {
                ctx.ExtraEvents.Add(new Events.FateEnergyGained(ctx.Card.Def.Id, gained));
            }
        }
    }
}
