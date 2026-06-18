using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Effects
{
    /// <summary>Per-effect inputs/outputs. Handler mutates State and writes its outcome here.</summary>
    public sealed class EffectContext
    {
        public ActionCardInstance Card;
        public CombatState State;
        public int Amount;

        // outputs (read by TurnResolver)
        public int DamageDealt;
        public string TargetId;
    }

    public interface IEffectHandler
    {
        EffectKey Key { get; }
        void Apply(EffectContext ctx);
    }
}
