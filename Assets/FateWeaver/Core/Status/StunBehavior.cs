namespace FateWeaver.Core.Status
{
    /// <summary>기절: nullifies the next resolution of the card it is attached to.</summary>
    public sealed class StunBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Stun;
        public override StatusScope Scope => StatusScope.CardInstance;

        public override bool InterceptCardResolve(StatusContext ctx) => true;
    }
}
