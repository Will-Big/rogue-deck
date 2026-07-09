using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Per-effect inputs/outputs. Handler mutates State and writes its outcome here.</summary>
    public sealed class EffectContext
    {
        public ExecutionCardInstance Card;
        public CombatState State;
        public ResolutionContext ResolutionContext;
        public StatusRegistry StatusRegistry;
        public EffectData Effect;
        public int EffectValue;

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
