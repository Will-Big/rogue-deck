using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>실행선 위 카드의 진행 단계(전투 실행 계약 스펙 §6). 차례가 온 카드는 효과가 없거나
    /// 취소돼도 Executed가 되어 실행 이력에 남는다. Removed는 차례가 오기 전에 실행선에서 빠진 카드다.</summary>
    public enum CardExecutionState
    {
        Pending,
        Executing,
        Executed,
        Removed
    }

    /// <summary>A card placed in the future zone for one combat. Once the card is on the line, change its
    /// ExecutionOrder through FutureZone.MoveTo / SwapPositions so the line stays ordered.</summary>
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

        /// <summary>차례가 오기 전에 정해진 취소 사유(예: 상태의 가로채기). 있으면 효과를 하나도 수행하지 않는다.
        /// 효과가 대상을 찾지 못하는 것은 취소가 아니다 — 그 효과만 미적용된다(전투 실행 계약 스펙 §2).</summary>
        public CardCancellationReason? CancellationReason { get; set; }

        /// <summary>실행선에서의 진행 단계. TurnResolver와 FutureZone이 옮긴다.</summary>
        public CardExecutionState ExecutionState { get; set; } = CardExecutionState.Pending;

        public ExecutionCardInstance(CardDefinition def)
        {
            Def = def;
            ExecutionOrder = def.BaseExecutionOrder;
        }

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
