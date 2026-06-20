using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;

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
                        CardType.Attack,
                        initiative: 1,
                        effects: new[] { new EffectData(EffectKeys.Damage, 1) }),
                    new ZoneCardSpec(
                        "quick_cut",
                        "Quick Cut",
                        Side.Player,
                        CardType.Attack,
                        initiative: 2,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.Damage,
                                amount: 2,
                                condition: new FirstToTrigger(),
                                successAmount: 10)
                        })
                },
                fatePlays: new[]
                {
                    new FatePlaySpec(
                        new FateActionData(FateActionKeys.SwapInitiative, cost: 1, amount: 0),
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
                        CardType.Attack,
                        initiative: 1,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.Damage,
                                amount: 2,
                                condition: new FirstToTrigger(),
                                successAmount: 10)
                        }),
                    new ZoneCardSpec(
                        "wrist_cut",
                        "Wrist Cut",
                        Side.Enemy,
                        CardType.Attack,
                        initiative: 2,
                        effects: new[]
                        {
                            new EffectData(EffectKeys.Damage, 1),
                            new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)
                        })
                },
                fatePlays: new[]
                {
                    new FatePlaySpec(
                        new FateActionData(FateActionKeys.SwapInitiative, cost: 1, amount: 0),
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
                        CardType.Attack,
                        initiative: 3,
                        effects: new[]
                        {
                            new EffectData(EffectKeys.Damage, 3),
                            new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)
                        }),
                    new ZoneCardSpec(
                        "mark_target",
                        "Mark Target",
                        Side.Player,
                        CardType.Skill,
                        initiative: 4,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.GrantNextPlayerAttackDamageBonus,
                                amount: 0,
                                condition: new AdjacentCardIs(
                                    AdjacentDirection.Next,
                                    Side.Player,
                                    CardType.Attack),
                                successAmount: 6)
                        }),
                    new ZoneCardSpec(
                        "chain_slash",
                        "Chain Slash",
                        Side.Player,
                        CardType.Attack,
                        initiative: 4,
                        effects: new[]
                        {
                            EffectData.Conditional(
                                EffectKeys.Damage,
                                amount: 1,
                                condition: new AllOf(new Condition[]
                                {
                                    new AdjacentCardIs(
                                        AdjacentDirection.Previous,
                                        Side.Player,
                                        CardType.Skill),
                                    new WithinNth(3)
                                }),
                                successAmount: 6)
                        }),
                    new ZoneCardSpec(
                        "gap_exposure",
                        "Gap Exposure",
                        Side.Enemy,
                        CardType.Attack,
                        initiative: 6,
                        effects: new[] { new EffectData(EffectKeys.Damage, 2) })
                },
                fatePlays: new[]
                {
                    new FatePlaySpec(
                        new FateActionData(
                            FateActionKeys.ChangeInitiative,
                            cost: 1,
                            amount: 2),
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
