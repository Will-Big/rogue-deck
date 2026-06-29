using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>Drives the deck turn loop: draw a hand, spend fate energy to place action cards onto the
    /// future zone and play fate cards to reorder it, resolve, then begin the next turn. Pure C#.</summary>
    public sealed class DeckCombatSession
    {
        private readonly CombatState _state;
        private readonly Deck _deck;
        private readonly IEnemyTurnPolicy _enemyPolicy;
        private readonly TurnResolver _resolver;
        private readonly FatePlayResolver _fateResolver;
        private readonly StatusRegistry _statuses;
        private readonly int _handSize;
        private IReadOnlyList<ResolutionEvent> _lastTimeline;

        public DeckCombatSession(
            IReadOnlyList<CardDefinition> deckCards,
            int playerHp,
            IReadOnlyList<Enemy> enemies,
            IEnemyTurnPolicy enemyPolicy,
            int fateEnergyPerTurn = 3,
            int handSize = 5,
            int seed = 0)
        {
            _state = new CombatState
            {
                PlayerHp = playerHp,
                FateEnergyPerTurn = fateEnergyPerTurn,
                RngSeed = seed
            };
            foreach (var enemy in enemies)
            {
                _state.Enemies.Add(enemy);
            }

            _deck = new Deck(deckCards, seed);
            _enemyPolicy = enemyPolicy;
            _handSize = handSize;
            _statuses = CombatRegistries.Statuses();
            _resolver = new TurnResolver(CombatRegistries.Effects(), _statuses);
            _fateResolver = new FatePlayResolver(CombatRegistries.FateActions());

            BeginTurn(0);
        }

        public int TurnIndex { get; private set; }
        public IReadOnlyList<CardDefinition> Hand => _deck.Hand;
        public int FateEnergy => _state.FateEnergy;
        public CombatState State => _state;
        public IReadOnlyList<ActionCardInstance> CurrentOrder => _state.Zone.ResolutionOrder();
        public IReadOnlyList<ResolutionEvent> LastTimeline => _lastTimeline;
        public Outcome Outcome { get; private set; } = Outcome.Ongoing;
        public bool CurrentTurnResolved { get; private set; }
        public bool IsComplete => Outcome != Outcome.Ongoing;
        public int DrawCount => _deck.DrawCount;
        public int DiscardCount => _deck.DiscardCount;

        /// <summary>Place an action card from the hand onto the future zone (spends its fate-energy cost).</summary>
        public bool PlayActionCard(int handIndex)
        {
            if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return false;
            }

            var def = _deck.Hand[handIndex];
            if (def.Category != CardCategory.Action || _state.FateEnergy < def.Cost)
            {
                return false;
            }

            _state.FateEnergy -= def.Cost;
            var placed = new ActionCardInstance(def);
            placed.Initiative = StatusInitiative.InitiativeFor(placed.Initiative, _state.PlayerStatuses, _statuses);
            _state.Zone.Add(placed);
            _deck.DiscardFromHand(handIndex);
            return true;
        }

        /// <summary>Play a fate card from the hand, targeting card(s) by their index in CurrentOrder.
        /// The fate handler deducts energy and rejects when locked / unaffordable.</summary>
        public bool PlayFateCard(int handIndex, int targetZoneIndex, int secondaryZoneIndex = -1)
        {
            if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return false;
            }

            var def = _deck.Hand[handIndex];
            if (def.Category != CardCategory.Fate || def.FateAction == null)
            {
                return false;
            }

            var order = _state.Zone.ResolutionOrder();
            if (targetZoneIndex < 0 || targetZoneIndex >= order.Count)
            {
                return false;
            }

            var target = order[targetZoneIndex];
            ActionCardInstance secondary = null;
            if (secondaryZoneIndex >= 0)
            {
                if (secondaryZoneIndex >= order.Count)
                {
                    return false;
                }

                secondary = order[secondaryZoneIndex];
            }

            var result = _fateResolver.Resolve(_state, new[] { new FatePlay(def.FateAction, target, secondary) });
            if (result.AppliedCount != 1)
            {
                return false;
            }

            _deck.DiscardFromHand(handIndex);
            return true;
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

        /// <summary>Discard the leftover hand and start the next turn (enemy intent, energy refill, redraw).
        /// Returns false when the current turn is unresolved or combat is already decided.</summary>
        public bool BeginNextTurn()
        {
            if (!CurrentTurnResolved || IsComplete)
            {
                return false;
            }

            _deck.DiscardHand();
            BeginTurn(TurnIndex + 1);
            return true;
        }

        private void BeginTurn(int index)
        {
            TurnIndex = index;
            CurrentTurnResolved = false;
            _lastTimeline = null;

            _state.Zone.Clear();
            var enemyBag = _state.Enemies.Count > 0 ? _state.Enemies[0].Statuses : null;
            foreach (var enemyCard in _enemyPolicy.CardsForTurn(index))
            {
                var inst = new ActionCardInstance(enemyCard);
                inst.IsLocked = enemyCard.StartsLocked;
                if (!inst.IsLocked)
                {
                    inst.Initiative = StatusInitiative.InitiativeFor(inst.Initiative, enemyBag, _statuses);
                }

                _state.Zone.Add(inst);
            }

            _state.FateEnergy = _state.FateEnergyPerTurn;
            _deck.Draw(_handSize);
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
    }
}
