using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>턴 종료마다 발동하고 스스로 자라는 상태 (독).</summary>
    [Serializable]
    public sealed class PoisonStatusSpec : StatusSpec
    {
        public int GrowthPerTurn;

        public override StatusSpec NewInstance() => new PoisonStatusSpec();

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            foreach (var error in base.Validate(context)) yield return error;

            if (GrowthPerTurn < 0)
            {
                yield return "growthPerTurn must not be negative.";
            }
        }
    }
}
