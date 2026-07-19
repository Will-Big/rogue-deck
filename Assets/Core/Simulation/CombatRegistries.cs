using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>Single source of truth for the default effect / status / fate-action registries used by
    /// the runners and the playtest session — so a new handler is registered everywhere at once.</summary>
    internal static class CombatRegistries
    {
        public static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());
            effects.Register(new GrantNextPlayerDamageCardBonusHandler());
            effects.Register(new ApplyStatusHandler());
            effects.Register(new MoveFormationHandler());
            return effects;
        }

        public static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new StunBehavior());
            statuses.Register(new VulnerableBehavior());
            statuses.Register(new RewardSuppressionBehavior());
            statuses.Register(new BlockBehavior());
            statuses.Register(new SlowBehavior());
            statuses.Register(new HasteBehavior());
            return statuses;
        }

        public static InterventionActionRegistry InterventionActions()
        {
            var actions = new InterventionActionRegistry();
            actions.Register(new ChangeExecutionOrderHandler());
            actions.Register(new SwapExecutionOrderHandler());
            actions.Register(new LockHandler());
            return actions;
        }
    }
}
