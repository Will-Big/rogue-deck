using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>A card placed in the future zone for one combat. ExecutionOrder is mutable.</summary>
    public sealed class ExecutionCardInstance : IStatusHolder
    {
        private int _pendingDamageBonus;

        public CardDefinition Def { get; }
        public int ExecutionOrder { get; set; }

        /// <summary>Session-assigned identity for this placed card. Core unit tests may inject distinct
        /// ids directly; sessions assign real ids from an increasing counter (from Task 4 onward).</summary>
        public int InstanceId { get; set; } = -1;

        /// <summary>Id of the party member or enemy that owns this card (null = owned by the party/enemy
        /// side as a whole, e.g. all pre-Task-4 content). Drives strict Self-target resolution.</summary>
        public string OwnerId { get; set; }

        public string TargetId { get; set; }
        public bool IsLocked { get; set; }
        public StatusBag Statuses { get; } = new();

        /// <summary>Set by an effect handler via EffectContext.Cancel when the card's target cannot be
        /// resolved. First cancellation reason wins; a cancelled card's remaining effects must not
        /// mutate state (see IEffectHandler.cs).</summary>
        public CardCancellationReason? CancellationReason { get; set; }

        public ExecutionCardInstance(CardDefinition def)
        {
            Def = def;
            ExecutionOrder = def.BaseExecutionOrder;
        }

        /// <summary>이 카드의 해석 중 consume_status가 실제로 소비한 누적 수치.
        /// ConsumedStatusAtLeast 조건이 읽는다.</summary>
        public int ConsumedStatusAmount { get; private set; }

        internal void RecordConsumedStatus(int amount) => ConsumedStatusAmount += amount;

        internal void AddPendingDamageBonus(int amount)
            => _pendingDamageBonus += amount;

        internal int ConsumePendingDamageBonus()
        {
            var amount = _pendingDamageBonus;
            _pendingDamageBonus = 0;
            return amount;
        }
    }
}
