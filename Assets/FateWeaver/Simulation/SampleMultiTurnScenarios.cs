using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;

namespace FateWeaver.Simulation
{
    public static class SampleMultiTurnScenarios
    {
        public static readonly SampleMultiTurnScenarioEntry[] All =
        {
            new SampleMultiTurnScenarioEntry(
                "chapter-8-three-turn-opening",
                Chapter8ThreeTurnOpening),
            new SampleMultiTurnScenarioEntry(
                "mark-combo",
                MarkCombo)
        };

        public static MultiTurnScenario Find(string id)
        {
            if (TryFind(id, out var scenario))
            {
                return scenario;
            }

            throw new System.Collections.Generic.KeyNotFoundException(
                "No multi-turn sample scenario found for '" + id + "'");
        }

        public static bool TryFind(string id, out MultiTurnScenario scenario)
        {
            foreach (var entry in All)
            {
                if (entry.Id == id)
                {
                    scenario = entry.Build();
                    return true;
                }
            }

            scenario = null;
            return false;
        }

        /// <summary>
        /// First executable three-turn balance slice derived from chapter 8's opening principle:
        /// an enemy acts before Quick Cut unless fate manipulation repairs the future order.
        /// Turn two also proves Wrist Cut disrupts the unmanipulated condition reward.
        /// </summary>
        public static MultiTurnScenario Chapter8ThreeTurnOpening()
        {
            return new MultiTurnScenario(
                "chapter-8-three-turn-opening",
                "Chapter 8 Three-Turn Opening",
                playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 100) },
                turns: new[]
                {
                    OpeningTurn("t1", "preemptive_thrust", enemyDamage: 3),
                    WristCutTurn(),
                    OpeningTurn("t3", "preemptive_thrust", enemyDamage: 4)
                });
        }

        /// <summary>표식 새기기 combo (doc §3.1 + §11.2): the +6 bonus on the next attack lands only when
        /// BOTH conditions hold (next card is a player attack AND no enemy attack has resolved first).
        /// Unmanipulated the enemy goes first, so the combo stays at the basic tier (no auto-complete);
        /// one fate play (delay the enemy) completes it.</summary>
        public static MultiTurnScenario MarkCombo()
        {
            return new MultiTurnScenario(
                "mark-combo",
                "Mark Combo",
                playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 30) },
                turns: new[]
                {
                    new TurnScript(
                        fateEnergy: 3,
                        zoneCards: new[]
                        {
                            EnemyAttack("goblin_jab", initiative: 1, damage: 1),
                            new ZoneCardSpec(
                                "mark", "Mark", Side.Player, CardType.Skill, initiative: 2,
                                effects: new[]
                                {
                                    EffectData.Conditional(
                                        EffectKeys.GrantNextPlayerAttackDamageBonus,
                                        amount: 0,
                                        condition: new AllOf(new Condition[]
                                        {
                                            new AdjacentCardIs(AdjacentDirection.Next, Side.Player, CardType.Attack),
                                            new BeforeNextEnemyAttack()
                                        }),
                                        successAmount: 6)
                                }),
                            new ZoneCardSpec(
                                "slash", "Slash", Side.Player, CardType.Attack, initiative: 3,
                                effects: new[] { new EffectData(EffectKeys.Damage, 2) })
                        },
                        fatePlays: new[]
                        {
                            new FatePlaySpec(
                                new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: 3),
                                "goblin_jab")
                        })
                });
        }

        private static TurnScript OpeningTurn(string suffix, string enemyId, int enemyDamage)
        {
            var quickCutId = "quick_cut_" + suffix;
            return new TurnScript(
                fateEnergy: 3,
                zoneCards: new[]
                {
                    EnemyAttack(enemyId + "_" + suffix, initiative: 1, damage: enemyDamage),
                    QuickCut(quickCutId, initiative: 2)
                },
                fatePlays: new[]
                {
                    new FatePlaySpec(
                        new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2),
                        quickCutId)
                });
        }

        private static TurnScript WristCutTurn()
        {
            const string quickCutId = "quick_cut_t2";
            return new TurnScript(
                fateEnergy: 3,
                zoneCards: new[]
                {
                    new ZoneCardSpec(
                        "wrist_cut_t2",
                        "Wrist Cut",
                        Side.Enemy,
                        CardType.Attack,
                        initiative: 1,
                        effects: new[]
                        {
                            new EffectData(EffectKeys.Damage, 3),
                            new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)
                        }),
                    QuickCut(quickCutId, initiative: 2)
                },
                fatePlays: new[]
                {
                    new FatePlaySpec(
                        new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2),
                        quickCutId)
                });
        }

        private static ZoneCardSpec QuickCut(string id, int initiative)
            => new ZoneCardSpec(
                id,
                "Quick Cut",
                Side.Player,
                CardType.Attack,
                initiative,
                new[]
                {
                    EffectData.Conditional(
                        EffectKeys.Damage,
                        amount: 2,
                        condition: new FirstToTrigger(),
                        successAmount: 10)
                });

        private static ZoneCardSpec EnemyAttack(string id, int initiative, int damage)
            => new ZoneCardSpec(
                id,
                id,
                Side.Enemy,
                CardType.Attack,
                initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) });
    }

    public sealed class SampleMultiTurnScenarioEntry
    {
        private readonly System.Func<MultiTurnScenario> _build;

        public string Id { get; }

        public SampleMultiTurnScenarioEntry(
            string id,
            System.Func<MultiTurnScenario> build)
        {
            Id = id;
            _build = build;
        }

        public MultiTurnScenario Build() => _build();
    }
}
