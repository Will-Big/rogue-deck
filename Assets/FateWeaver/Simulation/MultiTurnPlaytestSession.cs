using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;

namespace FateWeaver.Simulation
{
    /// <summary>Interactive driver for a MultiTurnScenario used by the Unity playtest screen: the human
    /// applies fate actions each turn (instead of the scenario's scripted plays), resolves, then advances.
    /// Pure C# (no UnityEngine) so the turn-advancement logic is headless-testable.</summary>
    public sealed class MultiTurnPlaytestSession
    {
        private readonly MultiTurnScenario _scenario;
        private readonly CombatState _state;
        private readonly TurnResolver _resolver;
        private readonly FatePlayResolver _fateResolver;

        private Dictionary<string, ActionCardInstance> _cardsById;
        private IReadOnlyList<ResolutionEvent> _lastTimeline;

        public MultiTurnPlaytestSession(MultiTurnScenario scenario)
        {
            _scenario = scenario;
            _state = new CombatState { PlayerHp = scenario.PlayerHp };
            foreach (var enemy in scenario.Enemies)
            {
                _state.Enemies.Add(new Enemy(enemy.Id, enemy.Hp));
            }

            _resolver = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses());
            _fateResolver = new FatePlayResolver(CombatRegistries.FateActions());

            BeginTurn(0);
        }

        public string Name => _scenario.Name;
        public int TurnIndex { get; private set; }
        public int TurnCount => _scenario.Turns.Count;
        public CombatState State => _state;
        public IReadOnlyList<ActionCardInstance> CurrentOrder => _state.Zone.ResolutionOrder();
        public IReadOnlyList<ResolutionEvent> LastTimeline => _lastTimeline;
        public Outcome Outcome { get; private set; } = Outcome.Ongoing;
        public bool CurrentTurnResolved { get; private set; }

        /// <summary>True once combat is decided, or the last turn has been resolved.</summary>
        public bool IsComplete =>
            Outcome != Outcome.Ongoing || (TurnIndex >= TurnCount - 1 && CurrentTurnResolved);

        public FatePlayResult ApplyFateAction(
            FateActionData action, string targetCardId, string secondaryTargetCardId = null)
        {
            if (CurrentTurnResolved)
            {
                throw new InvalidOperationException("The current turn is already resolved.");
            }

            var target = Card(targetCardId);
            var secondary = secondaryTargetCardId == null ? null : Card(secondaryTargetCardId);
            return _fateResolver.Resolve(_state, new[] { new FatePlay(action, target, secondary) });
        }

        public IReadOnlyList<ResolutionEvent> ResolveTurn()
        {
            if (CurrentTurnResolved)
            {
                return _lastTimeline;
            }

            _lastTimeline = _resolver.Resolve(_state, TurnIndex);
            CurrentTurnResolved = true;
            Outcome = OutcomeOf(_lastTimeline);
            return _lastTimeline;
        }

        /// <summary>Moves to the next turn (rebuilds the zone, resets fate energy). Returns false when the
        /// current turn is unresolved or combat is complete.</summary>
        public bool AdvanceTurn()
        {
            if (!CurrentTurnResolved || IsComplete)
            {
                return false;
            }

            BeginTurn(TurnIndex + 1);
            return true;
        }

        private void BeginTurn(int index)
        {
            TurnIndex = index;
            CurrentTurnResolved = false;
            _lastTimeline = null;

            var script = _scenario.Turns[index];
            _state.Zone.Clear();
            _cardsById = new Dictionary<string, ActionCardInstance>();
            foreach (var card in script.ZoneCards)
            {
                var def = new CardDefinition(
                    card.Id, card.Name, card.Side, card.Type, card.Initiative, card.Effects);
                var instance = new ActionCardInstance(def);
                _state.Zone.Add(instance);
                _cardsById[card.Id] = instance;
            }

            _state.FateEnergy = script.FateEnergy;
        }

        private ActionCardInstance Card(string id)
            => _cardsById.TryGetValue(id, out var card)
                ? card
                : throw new KeyNotFoundException("No playtest card found for '" + id + "'");

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
    }
}
