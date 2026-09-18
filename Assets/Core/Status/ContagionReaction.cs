using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>사후 전염의 사망 능력: 보유자가 독 상태로 죽으면 남은 독 전량을 현재 적 전열 하나(생존)에게
    /// 옮긴다. 사망한 보유자를 허용하는 능력이고, 반격 등 반응이 만든 사망에도 발동한다(계획 D11).</summary>
    public sealed class ContagionReaction : IReactionHandler
    {
        private static readonly IReadOnlyList<ReactionEffect> Transfer = new[]
        {
            new ReactionEffect(
                new EffectData(EffectKeys.TransferStatus, 0) { Payload = new TransferStatusPayload(StatusKeys.Poison) },
                ReactionTarget.Position(new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne)))
        };

        public StatusKey Key => StatusKeys.Contagion;
        public CombatSignalKey SignalKey => CombatSignalKeys.HolderDied;

        /// <summary>사망 시 반응은 사망 원인이 반응 공격이어도 발동한다(계획 D11).</summary>
        public bool RespondsToReactionEvents => true;

        public bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal)
        {
            var poison = CombatUnits.StatusesOf(state, signal.TargetId)?.Get(StatusKeys.Poison);
            return poison != null && poison.Magnitude > 0;
        }

        public IReadOnlyList<ReactionEffect> EffectsFor(StatusInstance instance, CombatSignal signal) => Transfer;
    }
}
