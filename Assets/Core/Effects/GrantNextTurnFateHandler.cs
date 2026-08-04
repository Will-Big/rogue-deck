namespace FateWeaver.Core.Effects
{
    /// <summary>다음 플레이어 사용 턴에 운명력 EffectValue를 추가로 준다 (증류). CombatState에
    /// 적립만 하고, 실제 지급은 세션의 턴 시작 리필이 담당한다.</summary>
    public sealed class GrantNextTurnFateHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.GrantNextTurnFate;

        public FateWeaver.Core.Cards.CardTargetKey? TargetFor(
            FateWeaver.Core.Cards.CardDefinition card,
            FateWeaver.Core.Cards.EffectData effect)
            => null;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            ctx.State.PendingNextTurnFateEnergy += ctx.EffectValue;
        }
    }
}
