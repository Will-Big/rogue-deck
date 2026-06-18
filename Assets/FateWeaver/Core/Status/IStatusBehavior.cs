namespace FateWeaver.Core.Status
{
    /// <summary>Inputs a status behavior may read when a hook fires.</summary>
    public sealed class StatusContext
    {
        public StatusInstance Instance;
    }

    /// <summary>Behavior for a status key. Implement only the relevant hooks (defaults are no-ops).
    /// Behavior lives here (code, registered); the StatusInstance on a holder is just data.</summary>
    public interface IStatusBehavior
    {
        StatusKey Key { get; }
        StatusScope Scope { get; }

        /// <summary>Entity-scoped: fold into damage the holder is about to RECEIVE.</summary>
        int ModifyIncomingDamage(int damage, StatusContext ctx);

        /// <summary>Card-scoped: return true to nullify/skip the card's resolution (e.g. stun).</summary>
        bool InterceptCardResolve(StatusContext ctx);
    }

    /// <summary>Base class with no-op hook defaults. Concrete statuses override what they use.
    /// (Abstract base instead of default-interface-methods, which Unity 6 / netstandard2.1 lacks.)</summary>
    public abstract class StatusBehavior : IStatusBehavior
    {
        public abstract StatusKey Key { get; }
        public abstract StatusScope Scope { get; }

        public virtual int ModifyIncomingDamage(int damage, StatusContext ctx) => damage;
        public virtual bool InterceptCardResolve(StatusContext ctx) => false;
    }
}
