using System;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class KoreanDescriptionCatalog
    {
        public static readonly KoreanDescriptionCatalog Default = CreateDefault();

        public KoreanDescriptionCatalog(
            EffectDescriptionRegistry effects,
            InterventionDescriptionRegistry interventions,
            StatusDescriptionRegistry statuses,
            IDescriptionGrammar grammar)
        {
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            Interventions = interventions
                ?? throw new ArgumentNullException(nameof(interventions));
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            Context = new DescriptionContext(grammar, statuses);
        }

        public EffectDescriptionRegistry Effects { get; }
        public InterventionDescriptionRegistry Interventions { get; }
        public StatusDescriptionRegistry Statuses { get; }
        public DescriptionContext Context { get; }

        public static KoreanDescriptionCatalog CreateDefault()
        {
            var statuses = new StatusDescriptionRegistry();
            statuses.Register(StatusKeys.Block, "방어");
            statuses.Register(StatusKeys.Slow, "둔화");
            statuses.Register(StatusKeys.Haste, "가속");
            statuses.Register(StatusKeys.Stun, "기절");
            statuses.Register(StatusKeys.Vulnerable, "취약");
            statuses.Register(StatusKeys.RewardNullified, "조건 보상 무효");
            statuses.Register(StatusKeys.Poison, "독");
            statuses.Register(StatusKeys.PoisonDormant, "독 잠복");
            statuses.Register(StatusKeys.PoisonStasis, "독 안정");
            statuses.Register(StatusKeys.Contagion, "전염");
            statuses.Register(StatusKeys.Weak, "약화");

            var effects = new EffectDescriptionRegistry();
            effects.Register(new DamageDescriptionHandler());
            effects.Register(new ApplyStatusDescriptionHandler());
            effects.Register(new NullifyNextPlayerConditionRewardDescriptionHandler());
            effects.Register(new GrantNextPlayerDamageCardBonusDescriptionHandler());
            effects.Register(new MoveFormationDescriptionHandler());
            effects.Register(new ConsumeStatusDescriptionHandler());
            effects.Register(new TriggerStatusDescriptionHandler());
            effects.Register(new GrantNextTurnFateDescriptionHandler());

            var interventions = new InterventionDescriptionRegistry();
            interventions.Register(new ChangeExecutionOrderDescriptionHandler());
            interventions.Register(new SwapExecutionOrderDescriptionHandler());
            interventions.Register(new LockDescriptionHandler());

            return new KoreanDescriptionCatalog(
                effects,
                interventions,
                statuses,
                new KoreanDescriptionGrammar());
        }
    }
}
