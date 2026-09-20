using FateWeaver.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Enemies;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    public readonly struct ExecutionPlacementPreview
    {
        public int ExecutionOrder { get; }
        public int InsertionIndex { get; }

        public ExecutionPlacementPreview(int executionOrder, int insertionIndex)
        {
            ExecutionOrder = executionOrder;
            InsertionIndex = insertionIndex;
        }
    }

    /// <summary>Drives the deck turn loop: draw a hand, spend fate energy to place execution cards onto the
    /// future zone and play intervention cards to reorder it, resolve, then begin the next turn. Pure C#.</summary>
    public sealed class DeckCombatSession
    {
        private readonly CombatState _state;
        private readonly Deck _deck;
        private readonly Dictionary<string, IEnemyTurnPolicy> _enemyPolicies;
        private readonly TurnResolver _resolver;
        private readonly InterventionPlayResolver _interventionResolver;
        private readonly InterventionActionRegistry _interventionActions;
        private readonly StatusRegistry _statuses;
        private readonly int _handSize;
        private readonly PartyTuning _partyTuning;
        private readonly bool _isPartyMode;
        private readonly ReadOnlyCollection<OwnedCard> _allCards;
        private IReadOnlyList<ResolutionEvent> _lastTimeline;
        private IReadOnlyList<ResolutionEvent> _lastTurnStartTimeline = System.Array.Empty<ResolutionEvent>();
        private int _nextInstanceId;

        public DeckCombatSession(
            StatusContentCatalog statusContent,
            IReadOnlyList<CardDefinition> deckCards,
            int playerHp,
            IReadOnlyList<Enemy> enemies,
            IEnemyTurnPolicy enemyPolicy,
            int fateEnergyPerTurn = 3,
            int handSize = 5,
            int seed = 0)
            : this(
                statusContent, WithSoloOwner(deckCards), playerHp, enemies, enemyPolicy,
                fateEnergyPerTurn, handSize, seed)
        {
        }

        public DeckCombatSession(
            StatusContentCatalog statusContent,
            IReadOnlyList<OwnedCard> deckCards,
            int playerHp,
            IReadOnlyList<Enemy> enemies,
            IEnemyTurnPolicy enemyPolicy,
            int fateEnergyPerTurn = 3,
            int handSize = 5,
            int seed = 0)
            : this(
                statusContent,
                deckCards,
                playerHp,
                SingleActor(enemies, enemyPolicy),
                fateEnergyPerTurn,
                handSize,
                seed,
                null,
                null)
        {
        }

        public DeckCombatSession(
            StatusContentCatalog statusContent,
            IReadOnlyList<PartyMemberLoadout> party,
            IReadOnlyList<EncounterEnemy> enemies,
            PartyTuning tuning,
            IReadOnlyList<CardDefinition> partyCards = null,
            int fateEnergyPerTurn = 3,
            int seed = 0)
            : this(
                statusContent,
                BuildPartyDeck(party, partyCards, tuning),
                0,
                enemies,
                fateEnergyPerTurn,
                0,
                seed,
                party,
                tuning)
        {
        }

        private DeckCombatSession(
            StatusContentCatalog statusContent,
            IReadOnlyList<OwnedCard> deckCards,
            int playerHp,
            IReadOnlyList<EncounterEnemy> enemies,
            int fateEnergyPerTurn,
            int handSize,
            int seed,
            IReadOnlyList<PartyMemberLoadout> party,
            PartyTuning partyTuning)
        {
            _state = new CombatState(statusContent)
            {
                FateEnergyPerTurn = fateEnergyPerTurn,
                RngSeed = seed
            };
            _isPartyMode = party != null;
            if (_isPartyMode)
            {
                foreach (var loadout in party)
                {
                    _state.Party.Add(new PartyMember(
                        loadout.Id,
                        loadout.Name,
                        loadout.MaxHp)
                    {
                        Hp = loadout.Hp
                    });
                }
            }
            else
            {
                _state.AddSoloPlayer(playerHp);
            }

            ValidateEnemies(enemies);
            _enemyPolicies = new Dictionary<string, IEnemyTurnPolicy>();
            foreach (var pair in enemies)
            {
                _state.Enemies.Add(pair.Enemy);
                if (pair.Policy != null)
                {
                    _enemyPolicies.Add(pair.Enemy.Id, pair.Policy);
                }
            }

            ValidateDeckCards(deckCards);
            _allCards = new List<OwnedCard>(deckCards).AsReadOnly();
            _deck = new Deck(deckCards, _state.Rng);
            _handSize = handSize;
            _partyTuning = partyTuning;
            _statuses = CombatRegistries.Statuses();
            _resolver = new TurnResolver(
                CombatRegistries.Effects(), _statuses, CombatRegistries.Reactions(), RemoveOwnedCards);
            _interventionActions = CombatRegistries.InterventionActions();
            _interventionResolver = new InterventionPlayResolver(_interventionActions);

            BeginTurn(0);
        }

        public int TurnIndex { get; private set; }
        public IReadOnlyList<OwnedCard> Hand => _deck.Hand;
        public int FateEnergy => _state.FateEnergy;
        public CombatState State => _state;
        public IReadOnlyList<ExecutionCardInstance> CurrentOrder => _state.Zone.ResolutionOrder();
        public IReadOnlyList<ResolutionEvent> LastTimeline => _lastTimeline;

        /// <summary>이번 턴의 준비·턴 시작 단계가 낸 이벤트(준비 만료된 방어의 StatusExpired 등). 해석 타임라인과 따로다.</summary>
        public IReadOnlyList<ResolutionEvent> LastTurnStartTimeline => _lastTurnStartTimeline;
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

        public bool TryPreviewExecutionPlacement(
            int handIndex, out ExecutionPlacementPreview preview)
        {
            preview = default;
            if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return false;
            }

            var card = _deck.Hand[handIndex];
            if (card.Def.Category != CardCategory.Execution)
            {
                return false;
            }

            int executionOrder = EffectiveExecutionOrderFor(card);
            var candidate = new ExecutionCardInstance(card.Def)
            {
                OwnerId = card.OwnerId,
                ExecutionOrder = executionOrder
            };
            preview = new ExecutionPlacementPreview(
                executionOrder, _state.Zone.PreviewInsertionIndex(candidate));
            return true;
        }

        /// <summary>Place an execution card from the hand onto the future zone (spends its fate-energy cost).</summary>
        public bool PlayExecutionCard(int handIndex)
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

            _state.FateEnergy -= def.EnergyCost;
            var placed = new ExecutionCardInstance(def)
            {
                InstanceId = _nextInstanceId++,
                OwnerId = card.OwnerId,
                ExecutionOrder = EffectiveExecutionOrderFor(card)
            };
            _state.Zone.Add(placed);
            _deck.DiscardFromHand(handIndex);
            return true;
        }

        private int EffectiveExecutionOrderFor(OwnedCard card)
            => StatusExecutionOrder.ExecutionOrderFor(
                card.Def.BaseExecutionOrder, OwnerStatusesFor(card), _statuses, _state.StatusRules,
                _state.StatusContent);

        /// <summary>정책 하나짜리 옛 경로(플레이어 HP 기반 시뮬레이션)를 쌍으로 옮긴다. 카드를 내는
        /// 적은 첫 번째 하나이고 나머지는 대상 전용이다 — 정책이 어느 적 것인지 말하지 않는 서명이라
        /// 그 이상은 알 수 없다. 파티 경로는 편성 공급자가 적마다 정책을 만들어 넘긴다.</summary>
        private static IReadOnlyList<EncounterEnemy> SingleActor(
            IReadOnlyList<Enemy> enemies, IEnemyTurnPolicy policy)
        {
            if (enemies == null)
            {
                throw new System.ArgumentException("Enemies are required.");
            }

            var pairs = new List<EncounterEnemy>(enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
            {
                pairs.Add(i == 0 && policy != null
                    ? new EncounterEnemy(enemies[i], policy)
                    : EncounterEnemy.Passive(enemies[i]));
            }

            return pairs;
        }

        private static void ValidateEnemies(IReadOnlyList<EncounterEnemy> enemies)
        {
            if (enemies == null || enemies.Count == 0)
            {
                throw new System.ArgumentException("At least one enemy is required.");
            }

            var ids = new HashSet<string>();
            foreach (var pair in enemies)
            {
                if (pair == null || string.IsNullOrEmpty(pair.Enemy.Id) || !ids.Add(pair.Enemy.Id))
                {
                    // 전투 안 id가 겹치면 카드 주인과 사망 처리가 남의 것을 지목한다.
                    throw new System.ArgumentException("Encounter enemy ids must be present and unique.");
                }
            }
        }

        private static void ValidateDeckCards(IReadOnlyList<OwnedCard> cards)
        {
            if (cards == null)
            {
                throw new System.ArgumentException("Deck cards are required.");
            }

            foreach (var card in cards)
            {
                if (card == null || card.Def == null)
                {
                    throw new System.ArgumentException("Deck contains an invalid owned card.");
                }
            }
        }

        private StatusBag OwnerStatusesFor(OwnedCard card)
        {
            foreach (var member in _state.Party)
            {
                if (member.IsAlive && member.Id == card.OwnerId)
                {
                    return member.Statuses;
                }
            }

            return null;
        }

        /// <summary>Answers what the player must pick before playing this hand card.
        /// Execution cards never require explicit targets (targets are authored as the card's ally/enemy
        /// ranges plus each effect's faction, and resolved by the core per effect).</summary>
        public TargetingRequirement DescribeTargeting(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return TargetingRequirement.None;
            }

            var def = _deck.Hand[handIndex].Def;
            if (def.Category != CardCategory.Intervention || def.InterventionAction == null)
            {
                return TargetingRequirement.None;
            }

            return _interventionActions.Resolve(def.InterventionAction.Key).Targeting;
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
            var targeting = _interventionActions.Resolve(def.InterventionAction.Key).Targeting;
            ExecutionCardInstance secondary = null;
            if (secondaryZoneIndex >= 0)
            {
                if (secondaryZoneIndex >= order.Count)
                {
                    return false;
                }

                if (!targeting.AllowDuplicates && secondaryZoneIndex == targetZoneIndex)
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

            if (IsComplete)
            {
                // 턴 시작 단계에서 결판이 났다 — 해석할 턴이 없다.
                return System.Array.Empty<ResolutionEvent>();
            }

            _lastTimeline = _resolver.Resolve(_state, TurnIndex);
            CurrentTurnResolved = true;
            Outcome = OutcomeOf(_lastTimeline);
            return _lastTimeline;
        }

        /// <summary>주인이 죽는 순간 사망 처리 경로(DeathProcessor)가 부른다 — 그 주인의 카드를 덱에서 뺀다.</summary>
        private void RemoveOwnedCards(string ownerId) => _deck.RemoveOwnedBy(ownerId);

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

            // 턴 준비(공통 만료·비용 초기화) → 턴 시작 상태 → 승패 → 적 배치·드로우(스펙 §8).
            var start = _resolver.Prepare(_state);
            _state.FateEnergy = _state.FateEnergyPerTurn + _state.PendingNextTurnFateEnergy;
            _state.PendingNextTurnFateEnergy = 0;
            start.AddRange(_resolver.StartTurn(_state));
            _lastTurnStartTimeline = start;

            Outcome = CombatOutcomeEvaluator.Evaluate(_state);
            if (IsComplete)
            {
                return;
            }

            // 대형 순서(state.Enemies)로 돈다 — 앞줄 적이 먼저 카드를 올리므로, 실행 순서가 같은
            // 카드끼리는 앞줄 것이 앞선다. 진형이 바뀌면 그 다음 턴부터 따라간다.
            foreach (var enemy in _state.Enemies)
            {
                // 죽은 적은 정책을 부르지 않는다 — 카드를 내지 않고, RNG도 소비하지 않는다
                // (2026-09-19 사용자 결정). 적을 먼저 죽이면 이후 추첨이 달라지는 것은 의도다.
                if (enemy.Hp <= 0 || !_enemyPolicies.TryGetValue(enemy.Id, out var policy))
                {
                    continue;
                }

                foreach (var enemyCard in policy.CardsForTurn(index, _state.Rng))
                {
                    var inst = new ExecutionCardInstance(enemyCard)
                    {
                        InstanceId = _nextInstanceId++,
                        OwnerId = enemy.Id
                    };
                    inst.IsLocked = enemyCard.StartsLocked;
                    if (!inst.IsLocked)
                    {
                        // 실행 순서 보정은 카드를 낸 그 적의 상태(가속·감속)로만 한다.
                        inst.ExecutionOrder = StatusExecutionOrder.ExecutionOrderFor(
                            inst.ExecutionOrder, enemy.Statuses, _statuses, _state.StatusRules,
                            _state.StatusContent);
                    }

                    _state.Zone.Add(inst);
                }
            }

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
            if (tuning == null)
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
                    || loadout.Hp <= 0
                    || loadout.Hp > loadout.MaxHp
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

        private static IReadOnlyList<OwnedCard> WithSoloOwner(IReadOnlyList<CardDefinition> cards)
        {
            var owned = new List<OwnedCard>(cards.Count);
            foreach (var card in cards)
            {
                owned.Add(new OwnedCard(card, CombatState.SoloPlayerId));
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
