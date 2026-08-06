namespace FateWeaver.Core.Intervention
{
    /// <summary>개입 한 건의 런타임 데이터. 모든 액션이 공유하는 것(핸들러를 찾을 키, 차감할 비용)만
    /// 직접 들고, 액션별 파라미터는 Payload에 실어 나른다. 카드에서 핸들러까지 이 봉투를 넘기는
    /// InterventionPlay·InterventionPlayResolver·DeckCombatSession·ScenarioDefinition·
    /// PlaytestSession은 Payload를 열지 않는다 — 여는 것은 자기가 무슨 액션인지 아는 핸들러뿐이다.</summary>
    public sealed class InterventionActionData
    {
        public InterventionActionKey Key { get; }
        public int InterventionCost { get; }

        /// <summary>액션별 파라미터. 파라미터가 없는 액션(lock)은 null이다.</summary>
        public IInterventionPayload Payload { get; }

        public InterventionActionData(InterventionActionKey key, int interventionCost)
            : this(key, interventionCost, null)
        {
        }

        public InterventionActionData(
            InterventionActionKey key,
            int interventionCost,
            IInterventionPayload payload)
        {
            Key = key;
            InterventionCost = interventionCost;
            Payload = payload;
        }
    }
}
