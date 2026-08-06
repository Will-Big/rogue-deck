using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드를 고정해 이후 개입을 거부하게 한다. 파라미터가 없으므로 페이로드도
    /// 없다 — 이 계획 이전에는 쓰지 않는 칸 셋을 들고 있었다.</summary>
    [Serializable]
    public sealed class LockSpec : InterventionSpec
    {
        public override InterventionActionKey Key => InterventionActionKeys.Lock;

        public override IInterventionPayload ToPayload() => null;
    }
}
