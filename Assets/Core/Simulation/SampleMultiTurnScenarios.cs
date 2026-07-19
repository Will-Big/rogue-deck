using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

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
                MarkCombo),
            new SampleMultiTurnScenarioEntry(
                "counter-stance",
                CounterStance),
            new SampleMultiTurnScenarioEntry(
                "chain-slash",
                ChainSlash)
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
                            EnemyAttack("goblin_jab", executionOrder: 1, damage: 1),
                            new ZoneCardSpec(
                                "mark", "Mark", Side.Player, executionOrder: 2,
                                effects: new[]
                                {
                                    EffectData.Conditional(
                                        EffectKeys.GrantNextPlayerDamageCardBonus,
                                        effectValue: 0,
                                        condition: new AllOf(new Condition[]
                                        {
                                            new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Player, EffectKeys.Damage),
                                            new BeforeNextEnemyDamageCard()
                                        }),
                                        successEffectValue: 6)
                                }),
                            new ZoneCardSpec(
                                "slash", "Slash", Side.Player, executionOrder: 3,
                                effects: new[] { new EffectData(EffectKeys.Damage, 2) })
                        },
                        interventionPlays: new[]
                        {
                            new InterventionPlaySpec(
                                new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: 3),
                                "goblin_jab")
                        })
                });
        }

        /// <summary>반격 자세 (doc §11.4 후보 A / §2.3 후행 보상): gain 2 block, and if an enemy attack
        /// resolved immediately before this card, counter that enemy for 7 (+2 if within the 3rd slot).
        /// Targets the first enemy (precise "그 적" multi-enemy targeting awaits attacker→entity mapping).</summary>
        public static MultiTurnScenario CounterStance()
        {
            return new MultiTurnScenario(
                "counter-stance",
                "Counter Stance",
                playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 100) },
                turns: new[]
                {
                    new TurnScript(
                        fateEnergy: 3,
                        zoneCards: new[]
                        {
                            EnemyAttack("goblin_jab", executionOrder: 1, damage: 3),
                            CounterCard("counter", executionOrder: 2)
                        },
                        interventionPlays: new InterventionPlaySpec[0])
                });
        }

        private static ZoneCardSpec CounterCard(string id, int executionOrder)
            => new ZoneCardSpec(
                id, "Counter Stance", Side.Player, executionOrder,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, magnitude: 2),
                    EffectData.Conditional(
                        EffectKeys.Damage,
                        effectValue: 0,
                        condition: new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage),
                        successEffectValue: 7),
                    EffectData.Conditional(
                        EffectKeys.Damage,
                        effectValue: 0,
                        condition: new AllOf(new Condition[]
                        {
                            new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage),
                            new WithinNth(3)
                        }),
                        successEffectValue: 2)
                });

        /// <summary>연쇄 베기 (doc §11.3): deal 1, and if the immediately-previous card is a player action
        /// card AND this resolves within the 3rd slot, "activate once more" — modelled as a second hit of
        /// 5 (1 + 4). Two conditions (AllOf) gate the re-trigger so it does not auto-complete.</summary>
        public static MultiTurnScenario ChainSlash()
        {
            return new MultiTurnScenario(
                "chain-slash",
                "Chain Slash",
                playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 100) },
                turns: new[]
                {
                    new TurnScript(
                        fateEnergy: 3,
                        zoneCards: new[]
                        {
                            new ZoneCardSpec("prep", "Prep", Side.Player, 1,
                                new[] { new EffectData(EffectKeys.Damage, 1) }),
                            ChainSlashCard("chain", executionOrder: 2)
                        },
                        interventionPlays: new InterventionPlaySpec[0])
                });
        }

        private static ZoneCardSpec ChainSlashCard(string id, int executionOrder)
            => new ZoneCardSpec(
                id, "Chain Slash", Side.Player, executionOrder,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 1),
                    EffectData.Conditional(
                        EffectKeys.Damage,
                        effectValue: 0,
                        condition: new AllOf(new Condition[]
                        {
                            new PreviousExecutedCardIs(Side.Player), // any player execution card
                            new WithinNth(3)
                        }),
                        successEffectValue: 5)
                });

        private static TurnScript OpeningTurn(string suffix, string enemyId, int enemyDamage)
        {
            var quickCutId = "quick_cut_" + suffix;
            return new TurnScript(
                fateEnergy: 3,
                zoneCards: new[]
                {
                    EnemyAttack(enemyId + "_" + suffix, executionOrder: 1, damage: enemyDamage),
                    QuickCut(quickCutId, executionOrder: 2)
                },
                interventionPlays: new[]
                {
                    new InterventionPlaySpec(
                        new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2),
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
                        executionOrder: 1,
                        effects: new[]
                        {
                            new EffectData(EffectKeys.Damage, 3),
                            new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)
                        }),
                    QuickCut(quickCutId, executionOrder: 2)
                },
                interventionPlays: new[]
                {
                    new InterventionPlaySpec(
                        new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2),
                        quickCutId)
                });
        }

        private static ZoneCardSpec QuickCut(string id, int executionOrder)
            => new ZoneCardSpec(
                id,
                "Quick Cut",
                Side.Player,
                executionOrder,
                new[]
                {
                    EffectData.Conditional(
                        EffectKeys.Damage,
                        effectValue: 2,
                        condition: new FirstToTrigger(),
                        successEffectValue: 10)
                });

        private static ZoneCardSpec EnemyAttack(string id, int executionOrder, int damage)
            => new ZoneCardSpec(
                id,
                id,
                Side.Enemy,
                executionOrder,
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
