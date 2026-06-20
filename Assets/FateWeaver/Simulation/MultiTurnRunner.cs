using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>Drives a MultiTurnScenario: persists player/enemy state (incl. statuses) across turns,
    /// rebuilds the zone and resets fate energy each turn, applies fate plays, resolves, and stops on
    /// win/lose. Wires the StatusRegistry so statuses and their lifetimes are active across turns.</summary>
    public sealed class MultiTurnRunner
    {
        public MultiTurnComparisonResult Compare(MultiTurnScenario scenario)
        {
            var baselineScenario = WithoutFatePlays(scenario);
            return new MultiTurnComparisonResult(
                scenario,
                Run(baselineScenario),
                Run(scenario));
        }

        public MultiTurnResult Run(MultiTurnScenario scenario)
        {
            var state = new CombatState { PlayerHp = scenario.PlayerHp };
            foreach (var enemy in scenario.Enemies)
            {
                state.Enemies.Add(new Enemy(enemy.Id, enemy.Hp));
            }

            var resolver = new TurnResolver(DefaultEffects(), DefaultStatuses());
            var fateActions = DefaultFateActions();

            var turns = new List<TurnOutcome>();
            var outcome = Outcome.Ongoing;

            for (int i = 0; i < scenario.Turns.Count; i++)
            {
                var script = scenario.Turns[i];
                var cardsById = LoadZone(state, script.ZoneCards);
                state.FateEnergy = script.FateEnergy;

                var initialOrder = Summarize(state.Zone.ResolutionOrder());
                var fateResult = new FatePlayResolver(fateActions)
                    .Resolve(state, BuildPlays(script.FatePlays, cardsById));
                var manipulatedOrder = Summarize(state.Zone.ResolutionOrder());

                var timeline = resolver.Resolve(state, i);
                turns.Add(new TurnOutcome(i, initialOrder, manipulatedOrder, fateResult, timeline));

                outcome = OutcomeOf(timeline);
                if (outcome != Outcome.Ongoing)
                {
                    break; // combat decided; later turns don't happen
                }
            }

            return new MultiTurnResult(scenario, state, turns, outcome);
        }

        private static MultiTurnScenario WithoutFatePlays(MultiTurnScenario scenario)
        {
            var turns = new List<TurnScript>();
            foreach (var turn in scenario.Turns)
            {
                turns.Add(new TurnScript(
                    turn.FateEnergy,
                    turn.ZoneCards,
                    new FatePlaySpec[0]));
            }

            return new MultiTurnScenario(
                scenario.Id,
                scenario.Name,
                scenario.PlayerHp,
                scenario.Enemies,
                turns);
        }

        private static Dictionary<string, ActionCardInstance> LoadZone(
            CombatState state, IReadOnlyList<ZoneCardSpec> zoneCards)
        {
            state.Zone.Clear();
            var cardsById = new Dictionary<string, ActionCardInstance>();
            foreach (var card in zoneCards)
            {
                var def = new CardDefinition(
                    card.Id, card.Name, card.Side, card.Type, card.Initiative, card.Effects);
                var instance = new ActionCardInstance(def);
                state.Zone.Add(instance);
                cardsById.Add(card.Id, instance);
            }

            return cardsById;
        }

        private static IReadOnlyList<FatePlay> BuildPlays(
            IReadOnlyList<FatePlaySpec> specs, Dictionary<string, ActionCardInstance> cardsById)
        {
            var plays = new List<FatePlay>();
            foreach (var spec in specs)
            {
                var target = cardsById[spec.TargetCardId];
                var secondary = spec.SecondaryTargetCardId == null
                    ? null
                    : cardsById[spec.SecondaryTargetCardId];
                plays.Add(new FatePlay(spec.Action, target, secondary));
            }

            return plays;
        }

        private static IReadOnlyList<OrderCardSummary> Summarize(IReadOnlyList<ActionCardInstance> cards)
        {
            var summaries = new List<OrderCardSummary>();
            foreach (var card in cards)
            {
                summaries.Add(new OrderCardSummary(card.Def.Id, card.Def.Side.ToString(), card.Initiative));
            }

            return summaries;
        }

        private static Outcome OutcomeOf(IReadOnlyList<ResolutionEvent> timeline)
        {
            for (int i = timeline.Count - 1; i >= 0; i--)
            {
                if (timeline[i] is TurnEnded ended)
                {
                    return ended.Outcome;
                }
            }

            return Outcome.Ongoing;
        }

        private static EffectRegistry DefaultEffects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());
            effects.Register(new GrantNextPlayerAttackDamageBonusHandler());
            effects.Register(new ApplyStatusHandler());
            return effects;
        }

        private static StatusRegistry DefaultStatuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new StunBehavior());
            statuses.Register(new VulnerableBehavior());
            statuses.Register(new RewardNullifiedBehavior());
            statuses.Register(new BlockBehavior());
            return statuses;
        }

        private static FateActionRegistry DefaultFateActions()
        {
            var actions = new FateActionRegistry();
            actions.Register(new ChangeInitiativeHandler());
            actions.Register(new SwapInitiativeHandler());
            actions.Register(new LockHandler());
            return actions;
        }
    }
}
