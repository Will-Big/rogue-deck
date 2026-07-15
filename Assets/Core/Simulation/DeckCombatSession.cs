using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>Drives the deck turn loop: draw a hand, spend fate energy to place execution cards onto the
    /// future zone and play intervention cards to reorder it, resolve, then begin the next turn. Pure C#.</summary>
    public sealed class DeckCombatSession
    {
        private readonly CombatState _state;
        private readonly Deck _deck;
        private readonly IEnemyTurnPolicy _enemyPolicy;
        private readonly TurnResolver _resolver;
        private readonly InterventionPlayResolver _interventionResolver;
        private readonly StatusRegistry _statuses;
        private readonly int _handSize;
        private readonly PartyTuning _partyTuning;
        private readonly List<OwnedCard> _allCards;
        private IReadOnlyList<ResolutionEvent> _lastTimeline;
        private int _nextInstanceId;

        public DeckCombatSession(
            IReadOnlyList<CardDefinition> deckCards,
            int playerHp,
            IReadOnlyList<Enemy> enemies,
            IEnemyTurnPolicy enemyPolicy,
            int fateEnergyPerTurn = 3,
            int handSize = 5,
            int seed = 0)
            : this(
                WithLegacyOwner(deckCards), playerHp, enemies, enemyPolicy,
                fateEnergyPerTurn, handSize, seed)
        {
        }

        public DeckCombatSession(
            IReadOnlyList<OwnedCard> deckCards,
            int playerHp,
            IReadOnlyList<Enemy> enemies,
            IEnemyTurnPolicy enemyPolicy,
            int fateEnergyPerTurn = 3,
            int handSize = 5,
            int seed = 0)
            : this(
                deckCards,
                playerHp,
                enemies,
                enemyPolicy,
                fateEnergyPerTurn,
                handSize,
                seed,
                null,
                null)
        {
        }

        public DeckCombatSession(
            IReadOnlyList<PartyMemberLoadout> party,
            IReadOnlyList<Enemy> enemies,
            IEnemyTurnPolicy enemyPolicy,
            PartyTuning tuning,
            IReadOnlyList<CardDefinition> partyCards = null,
            int fateEnergyPerTurn = 3,
            int seed = 0)
            : this(
                BuildPartyDeck(party, partyCards, tuning),
                0,
                enemies,
                enemyPolicy,
                fateEnergyPerTurn,
                0,
                seed,
                party,
                tuning)
        {
        }

        private DeckCombatSession(
            IReadOnlyList<OwnedCard> deckCards,
            int playerHp,
            IReadOnlyList<Enemy> enemies,
            IEnemyTurnPolicy enemyPolicy,
            int fateEnergyPerTurn,
            int handSize,
            int seed,
            IReadOnlyList<PartyMemberLoadout> party,
            PartyTuning partyTuning)
        {
            _state = new CombatState
            {
                PlayerHp = playerHp,
                FateEnergyPerTurn = fateEnergyPerTurn,
                RngSeed = seed
            };
            if (party != null)
            {
                _state.Party.Clear();
                foreach (var loadout in party)
                {
                    _state.Party.Add(new PartyMember(
                        loadout.Id,
                        loadout.Name,
                        loadout.MaxHp,
                        partyTuning.SurviveChargesPerCombat));
                }
            }

            foreach (var enemy in enemies)
            {
                _state.Enemies.Add(enemy);
            }

            _allCards = new List<OwnedCard>(deckCards);
            _deck = new Deck(deckCards, seed);
            _enemyPolicy = enemyPolicy;
            _handSize = handSize;
            _partyTuning = partyTuning;
            _statuses = CombatRegistries.Statuses();
            _resolver = new TurnResolver(CombatRegistries.Effects(), _statuses);
            _interventionResolver = new InterventionPlayResolver(CombatRegistries.InterventionActions());

            BeginTurn(0);
        }

        public int TurnIndex { get; private set; }
        public IReadOnlyList<OwnedCard> Hand => _deck.Hand;
        public int FateEnergy => _state.FateEnergy;
        public CombatState State => _state;
        public IReadOnlyList<ExecutionCardInstance> CurrentOrder => _state.Zone.ResolutionOrder();
        public IReadOnlyList<ResolutionEvent> LastTimeline => _lastTimeline;
        public Outcome Outcome { get; private set; } = Outcome.Ongoing;
        public bool CurrentTurnResolved { get; private set; }
        public bool IsComplete => Outcome != Outcome.Ongoing;
        public int DrawCount => _deck.DrawCount;
        public int DiscardCount => _deck.DiscardCount;

        /// <summary>Deck-viewer UI: real draw order (UI sorts for display), discard order, and the
        /// full list the player brought into combat (authoring order).</summary>
        public IReadOnlyList<OwnedCard> DrawPile => _deck.DrawPile;
        public IReadOnlyList<OwnedCard> DiscardPile => _deck.DiscardPile;
        public IReadOnlyList<OwnedCard> AllDeckCards => _allCards;

        /// <summary>Place an execution card from the hand onto the future zone (spends its fate-energy cost).</summary>
        public bool PlayExecutionCard(int handIndex, string targetId = null)
        {
            if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return false;
            }

            var card = _deck.Hand[handIndex];
            var def = card.Def;
            if (def.Category != CardCategory.Execution || _state.FateEnergy < def.EnergyCost)
            {
                return false;
            }

            if (PartyTargetRules.RequiresExplicitAllyTarget(def)
                && !PartyTargetRules.IsValidExplicitAllyTarget(_state, targetId))
            {
                return false;
            }

            _state.FateEnergy -= def.EnergyCost;
            var placed = new ExecutionCardInstance(def)
            {
                InstanceId = _nextInstanceId++,
                OwnerId = card.OwnerId,
                TargetId = targetId
            };
            StatusBag ownerStatuses = null;
            foreach (var member in _state.Party)
            {
                if (member.IsAlive && member.Id == card.OwnerId)
                {
                    ownerStatuses = member.Statuses;
                    break;
                }
            }

            placed.ExecutionOrder = StatusExecutionOrder.ExecutionOrderFor(
                placed.ExecutionOrder,
                ownerStatuses,
                _statuses);
            _state.Zone.Add(placed);
            _deck.DiscardFromHand(handIndex);
            return true;
        }

        /// <summary>Play a intervention card from the hand, targeting card(s) by their index in CurrentOrder.
        /// The fate handler deducts energy and rejects when locked / unaffordable.</summary>
        public bool PlayInterventionCard(int handIndex, int targetZoneIndex, int secondaryZoneIndex = -1)
        {
            if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return false;
            }

            var def = _deck.Hand[handIndex].Def;
            if (def.Category != CardCategory.Intervention || def.InterventionAction == null)
            {
                return false;
            }

            var order = _state.Zone.ResolutionOrder();
            if (targetZoneIndex < 0 || targetZoneIndex >= order.Count)
            {
                return false;
            }

            var target = order[targetZoneIndex];
            ExecutionCardInstance secondary = null;
            if (secondaryZoneIndex >= 0)
            {
                if (secondaryZoneIndex >= order.Count)
                {
                    return false;
                }

                secondary = order[secondaryZoneIndex];
            }

            var result = _interventionResolver.Resolve(_state, new[] { new InterventionPlay(def.InterventionAction, target, secondary) });
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
            var removedOwners = new HashSet<string>();
            foreach (var resolutionEvent in _lastTimeline)
            {
                if (resolutionEvent is PartyMemberDied died && removedOwners.Add(died.MemberId))
                {
                    _deck.RemoveOwnedBy(died.MemberId);
                }
            }

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
                var inst = new ExecutionCardInstance(enemyCard)
                {
                    InstanceId = _nextInstanceId++,
                    OwnerId = _state.Enemies.Count > 0 ? _state.Enemies[0].Id : null
                };
                inst.IsLocked = enemyCard.StartsLocked;
                if (!inst.IsLocked)
                {
                    inst.ExecutionOrder = StatusExecutionOrder.ExecutionOrderFor(inst.ExecutionOrder, enemyBag, _statuses);
                }

                _state.Zone.Add(inst);
            }

            _state.FateEnergy = _state.FateEnergyPerTurn;
            var drawCount = _partyTuning == null
                ? _handSize
                : _partyTuning.DrawFor(LivingPartyCount());
            _deck.Draw(drawCount);
        }

        private int LivingPartyCount()
        {
            var count = 0;
            foreach (var member in _state.Party)
            {
                if (member.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private static IReadOnlyList<OwnedCard> BuildPartyDeck(
            IReadOnlyList<PartyMemberLoadout> party,
            IReadOnlyList<CardDefinition> partyCards,
            PartyTuning tuning)
        {
            ValidateParty(party, partyCards, tuning);

            var cards = new List<OwnedCard>();
            foreach (var loadout in party)
            {
                foreach (var card in loadout.Cards)
                {
                    cards.Add(new OwnedCard(card, loadout.Id));
                }
            }

            if (partyCards != null)
            {
                foreach (var card in partyCards)
                {
                    cards.Add(new OwnedCard(card, null));
                }
            }

            return cards;
        }

        private static void ValidateParty(
            IReadOnlyList<PartyMemberLoadout> party,
            IReadOnlyList<CardDefinition> partyCards,
            PartyTuning tuning)
        {
            if (tuning == null
                || tuning.DefaultMemberMaxHp <= 0
                || tuning.SurviveChargesPerCombat < 0)
            {
                throw new System.ArgumentException("Party tuning is invalid.");
            }

            if (party == null
                || party.Count < tuning.MinPartySize
                || party.Count > tuning.MaxPartySize)
            {
                throw new System.ArgumentException("Party size is outside the configured bounds.");
            }

            var ids = new HashSet<string>();
            foreach (var loadout in party)
            {
                if (loadout == null
                    || string.IsNullOrEmpty(loadout.Id)
                    || !ids.Add(loadout.Id)
                    || loadout.MaxHp <= 0
                    || loadout.Cards == null)
                {
                    throw new System.ArgumentException("Party loadout is invalid.");
                }

                foreach (var card in loadout.Cards)
                {
                    if (card == null)
                    {
                        throw new System.ArgumentException("Party loadout contains a null card definition.");
                    }
                }
            }

            if (partyCards != null)
            {
                foreach (var card in partyCards)
                {
                    if (card == null)
                    {
                        throw new System.ArgumentException("Party-owned cards contain a null definition.");
                    }
                }
            }

            if (tuning.DrawByLivingCount == null)
            {
                throw new System.ArgumentException("Draw tuning is required.");
            }

            for (int livingCount = 1; livingCount <= party.Count; livingCount++)
            {
                if (!tuning.DrawByLivingCount.TryGetValue(livingCount, out var drawCount)
                    || drawCount <= 0)
                {
                    throw new System.ArgumentException("Draw tuning must cover every possible living count.");
                }
            }
        }

        private static IReadOnlyList<OwnedCard> WithLegacyOwner(IReadOnlyList<CardDefinition> cards)
        {
            var owned = new List<OwnedCard>(cards.Count);
            foreach (var card in cards)
            {
                owned.Add(new OwnedCard(card, CombatState.LegacyPlayerId));
            }

            return owned;
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
