using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation
{
    public static class SampleScenarios
    {
        public static readonly SampleScenarioEntry[] All =
        {
            new SampleScenarioEntry("quick-cut-swap", QuickCutSwap),
            new SampleScenarioEntry("reward-nullified", RewardNullified),
            new SampleScenarioEntry("chapter-8-auto-combo-guard", Chapter8AutoComboGuard)
        };

        public static ScenarioDefinition Find(string id)
        {
            foreach (var entry in All)
            {
                if (entry.Id == id)
                {
                    return entry.Build();
                }
            }

            throw new System.Collections.Generic.KeyNotFoundException("No sample scenario found for '" + id + "'");
        }

        public static ScenarioDefinition QuickCutSwap()
        {
            return new ScenarioDefinition(
                "quick-cut-swap",
                "Quick Cut Swap",
                playerHp: 30,
                fateEnergy: 3,
                enemies: new[] { new EnemySpec("goblin", 12) },
                zoneCards: new[]
                {
                    new ZoneCardSpec(
                        "enemy_jab",
                        "Enemy Jab",
                        Side.Enemy,
                        executionOrder: 1,
                        effects: new[] { new EffectData(EffectKeys.Damage, 1) }),
                    new ZoneCardSpec(
                        "quick_cut",
                        "Quick Cut",
                        Side.Player,
                        executionOrder: 2,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.Damage,
                                effectValue: 2,
                                condition: new FirstToTrigger(),
                                successEffectValue: 10)
                        })
                },
                interventionPlays: new[]
                {
                    new InterventionPlaySpec(
                        new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false)),
                        "enemy_jab",
                        "quick_cut")
                });
        }

        public static ScenarioDefinition RewardNullified()
        {
            return new ScenarioDefinition(
                "reward-nullified",
                "Reward Nullified",
                playerHp: 30,
                fateEnergy: 3,
                enemies: new[] { new EnemySpec("goblin", 12) },
                zoneCards: new[]
                {
                    new ZoneCardSpec(
                        "quick_cut",
                        "Quick Cut",
                        Side.Player,
                        executionOrder: 1,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.Damage,
                                effectValue: 2,
                                condition: new FirstToTrigger(),
                                successEffectValue: 10)
                        }),
                    new ZoneCardSpec(
                        "wrist_cut",
                        "Wrist Cut",
                        Side.Enemy,
                        executionOrder: 2,
                        effects: new[]
                        {
                            new EffectData(EffectKeys.Damage, 1),
                            new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)
                        })
                },
                interventionPlays: new[]
                {
                    new InterventionPlaySpec(
                        new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false)),
                        "quick_cut",
                        "wrist_cut")
                });
        }

        public static ScenarioDefinition Chapter8AutoComboGuard()
        {
            return new ScenarioDefinition(
                "chapter-8-auto-combo-guard",
                "Chapter 8 Auto-Combo Guard",
                playerHp: 30,
                fateEnergy: 3,
                enemies: new[] { new EnemySpec("goblin", 50) },
                zoneCards: new[]
                {
                    new ZoneCardSpec(
                        "wrist_cut",
                        "Wrist Cut",
                        Side.Enemy,
                        executionOrder: 3,
                        effects: new[]
                        {
                            new EffectData(EffectKeys.Damage, 3),
                            new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)
                        }),
                    new ZoneCardSpec(
                        "mark_target",
                        "Mark Target",
                        Side.Player,
                        executionOrder: 4,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.GrantNextPlayerDamageCardBonus,
                                effectValue: 0,
                                condition: new AdjacentCardHasEffect(
                                    AdjacentDirection.Next,
                                    Side.Player,
                                    EffectKeys.Damage),
                                successEffectValue: 6)
                        }),
                    new ZoneCardSpec(
                        "chain_slash",
                        "Chain Slash",
                        Side.Player,
                        executionOrder: 4,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.Damage,
                                effectValue: 1,
                                condition: new AllOf(new Condition[]
                                {
                                    new PreviousExecutedCardIs(Side.Player),
                                    new WithinNth(3)
                                }),
                                successEffectValue: 6)
                        }),
                    new ZoneCardSpec(
                        "gap_exposure",
                        "Gap Exposure",
                        Side.Enemy,
                        executionOrder: 6,
                        effects: new[] { new EffectData(EffectKeys.Damage, 2) })
                },
                interventionPlays: new[]
                {
                    new InterventionPlaySpec(
                        new InterventionActionData(
                            InterventionActionKeys.ChangeExecutionOrder,
                            interventionCost: 1, new ChangeExecutionOrderPayload(Delta: 2, TargetSide: null)),
                        "wrist_cut")
                });
        }
    }

    public sealed class SampleScenarioEntry
    {
        private readonly System.Func<ScenarioDefinition> _build;

        public string Id { get; }

        public SampleScenarioEntry(string id, System.Func<ScenarioDefinition> build)
        {
            Id = id;
            _build = build;
        }

        public ScenarioDefinition Build() => _build();
    }
}
