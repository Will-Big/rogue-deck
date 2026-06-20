using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;

namespace FateWeaver.Simulation
{
    public sealed class ScenarioRunner
    {
        public ScenarioComparisonResult Compare(ScenarioDefinition scenario)
        {
            var baseline = Run(WithoutFatePlays(scenario));
            var manipulated = Run(scenario);
            return new ScenarioComparisonResult(scenario, baseline, manipulated);
        }

        public ScenarioResult Run(ScenarioDefinition scenario)
        {
            var state = BuildState(scenario, out var cardsById);
            var initialOrder = SummarizeOrder(state.Zone.ResolutionOrder());
            var fateResult = ApplyFatePlays(state, scenario.FatePlays, cardsById);
            var manipulatedOrder = SummarizeOrder(state.Zone.ResolutionOrder());
            var timeline = new TurnResolver(DefaultEffects()).Resolve(state, turnIndex: 0);

            return new ScenarioResult(
                scenario,
                state,
                initialOrder,
                manipulatedOrder,
                fateResult,
                timeline);
        }

        private static ScenarioDefinition WithoutFatePlays(ScenarioDefinition scenario)
            => new ScenarioDefinition(
                scenario.Id,
                scenario.Name,
                scenario.PlayerHp,
                scenario.FateEnergy,
                scenario.Enemies,
                scenario.ZoneCards,
                new FatePlaySpec[0]);

        private static CombatState BuildState(
            ScenarioDefinition scenario,
            out Dictionary<string, ActionCardInstance> cardsById)
        {
            var state = new CombatState
            {
                PlayerHp = scenario.PlayerHp,
                FateEnergy = scenario.FateEnergy
            };

            foreach (var enemy in scenario.Enemies)
            {
                state.Enemies.Add(new Enemy(enemy.Id, enemy.Hp));
            }

            cardsById = new Dictionary<string, ActionCardInstance>();
            foreach (var card in scenario.ZoneCards)
            {
                var def = new CardDefinition(
                    card.Id,
                    card.Name,
                    card.Side,
                    card.Type,
                    card.Initiative,
                    card.Effects);
                var instance = new ActionCardInstance(def);
                state.Zone.Add(instance);
                cardsById.Add(card.Id, instance);
            }

            return state;
        }

        private static FatePlayResult ApplyFatePlays(
            CombatState state,
            IReadOnlyList<FatePlaySpec> specs,
            Dictionary<string, ActionCardInstance> cardsById)
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

            return new FatePlayResolver(DefaultFateActions()).Resolve(state, plays);
        }

        private static IReadOnlyList<OrderCardSummary> SummarizeOrder(IReadOnlyList<ActionCardInstance> cards)
        {
            var summaries = new List<OrderCardSummary>();
            foreach (var card in cards)
            {
                summaries.Add(new OrderCardSummary(
                    card.Def.Id,
                    card.Def.Side.ToString(),
                    card.Initiative));
            }

            return summaries;
        }

        private static EffectRegistry DefaultEffects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());
            effects.Register(new GrantNextPlayerAttackDamageBonusHandler());
            return effects;
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
