using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>피해·획득량에 정수 퍼센트 배율을 거는 상태 (취약·약화·손상).</summary>
    [Serializable]
    public sealed class MultiplierStatusSpec : StatusSpec
    {
        public int MultiplierPercent = StatusRule.NeutralPercent;

        public override StatusSpec NewInstance() => new MultiplierStatusSpec();

        public override StatusRule ToRule()
            => new StatusRule { MultiplierPercent = MultiplierPercent };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            foreach (var error in base.Validate(context)) yield return error;

            if (MultiplierPercent < 0)
            {
                yield return "multiplierPercent must not be negative.";
            }
        }
    }
}
