using FateWeaver.Core.Effects;
using FateWeaver.Core.Enemies;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Core
{
    /// <summary>Single source of truth for the default effect / status / fate-action registries used by
    /// the runners and DeckCombatSession — so a new handler is registered everywhere at once.</summary>
    public static class CombatRegistries
    {
        public static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());
            effects.Register(new GrantNextPlayerDamageCardBonusHandler());
            effects.Register(new ApplyStatusHandler());
            effects.Register(new MoveFormationHandler());
            effects.Register(new ConsumeStatusHandler());
            effects.Register(new TriggerStatusHandler());
            effects.Register(new GrantNextTurnFateHandler());
            effects.Register(new TransferStatusHandler());
            return effects;
        }

        public static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new VulnerableBehavior());
            statuses.Register(new RewardSuppressionBehavior());
            statuses.Register(new BlockBehavior());
            statuses.Register(new SlowBehavior());
            statuses.Register(new HasteBehavior());
            statuses.Register(new PoisonBehavior());
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new ContagionBehavior());
            statuses.Register(new WeakBehavior());
            statuses.Register(new DamagedBehavior());
            return statuses;
        }

        /// <summary>상태가 주는 반응 능력(전투 실행 계약 스펙 §7). 사건에 직접 반응하는 능력만 여기 둔다.</summary>
        public static ReactionRegistry Reactions()
        {
            var reactions = new ReactionRegistry();
            reactions.Register(new ContagionReaction());
            return reactions;
        }

        public static InterventionActionRegistry InterventionActions()
        {
            var actions = new InterventionActionRegistry();
            actions.Register(new ChangeExecutionOrderHandler());
            actions.Register(new SwapExecutionOrderHandler());
            actions.Register(new LockHandler());
            return actions;
        }

        public static EnemyPolicyRegistry EnemyPolicies()
        {
            var policies = new EnemyPolicyRegistry();
            policies.Register(EnemyPolicyKeys.RandomPick, bundles => new RandomPickPolicy(bundles));
            policies.Register(EnemyPolicyKeys.ShuffleBag, bundles => new ShuffleBagPolicy(bundles));
            policies.Register(EnemyPolicyKeys.Sequence, bundles => new SequencePolicy(bundles));
            return policies;
        }
    }
}
