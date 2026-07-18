using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Party battle UI. Core rules remain in DeckCombatSession; this component only translates
    /// authored assets into a session and renders its snapshots.</summary>
    public sealed class BattleScreenController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CharacterAsset[] _party = Array.Empty<CharacterAsset>();
        [Tooltip("Enemy cards' art source (rules live in the goblin deck).")]
        [SerializeField] private CardAsset[] _enemyArtCards = Array.Empty<CardAsset>();

        [Header("Views")]
        [SerializeField] private HandFanView _hand;
        [SerializeField] private ExecutionRailView _rail;
        [SerializeField] private UnitView _unitPrefab;
        [SerializeField] private RectTransform _playerUnitsRow;
        [SerializeField] private RectTransform _enemyUnitsRow;
        [SerializeField] private PileView _drawPile;
        [SerializeField] private PileView _discardPile;
        [SerializeField] private PileView _fullDeck;
        [SerializeField] private TMP_Text _energyText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _turnButton;
        [SerializeField] private TMP_Text _turnButtonLabel;
        [SerializeField] private Button _resetButton;
        [SerializeField] private CardSelectionController _selection;
        [SerializeField] private Button _emptyClickCatcher;
        [SerializeField] private Button _dimClickCatcher;

        private const int FateEnergyPerTurn = 3;
        private const int Seed = 1;
        private static readonly Color EnemyUnitTint = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color PartyOwnerColor = new Color(0.55f, 0.48f, 0.75f, 1f);

        private DeckCombatSession _session;
        private readonly Dictionary<string, UnitView> _partyUnits = new Dictionary<string, UnitView>();
        private readonly Dictionary<string, UnitView> _enemyUnits = new Dictionary<string, UnitView>();
        private readonly Dictionary<string, int> _enemyMaxHp = new Dictionary<string, int>();
        private readonly Dictionary<string, Sprite> _artById = new Dictionary<string, Sprite>();

        private void Start()
        {
            _turnButton.onClick.AddListener(OnTurnButton);
            _resetButton.onClick.AddListener(StartSession);
            _emptyClickCatcher.onClick.AddListener(OnEmptyClicked);
            _dimClickCatcher.onClick.AddListener(OnEmptyClicked);
            _selection.Initialize(TryApplySelection, CurrentValidTargets, RefreshAll);
            StartSession();
        }

        private void StartSession()
        {
            _selection.CancelSelection();
            if (_unitPrefab == null || _party == null || _party.Length == 0 || _party.Any(member => member == null || member.Deck == null))
            {
                SetMessage("파티 CharacterAsset, 덱 또는 UnitView 프리팹이 연결되지 않았습니다.");
                return;
            }

            var tuning = PartyPrototypeRoster.Tuning;
            var loadouts = _party.Select(member => new PartyMemberLoadout(
                member.Id,
                member.DisplayName,
                tuning.DefaultMemberMaxHp,
                member.Deck.ToSpecs().Select(CardSpecMapper.ToDefinition).ToList())).ToList();
            var enemies = new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) };
            _session = new DeckCombatSession(
                loadouts,
                enemies,
                GoblinDeck.Policy(),
                tuning,
                partyCards: null,
                fateEnergyPerTurn: FateEnergyPerTurn,
                seed: Seed);

            BuildArtLookup();
            SpawnUnits();
            BindPiles();
            SetMessage("전투 시작.");
            RefreshAll();
        }

        private void SpawnUnits()
        {
            _selection.ClearUnitTargets();
            foreach (Transform child in _playerUnitsRow) Destroy(child.gameObject);
            foreach (Transform child in _enemyUnitsRow) Destroy(child.gameObject);
            _partyUnits.Clear();
            _enemyUnits.Clear();
            _enemyMaxHp.Clear();

            foreach (var member in _session.State.Party)
            {
                var view = Instantiate(_unitPrefab, _playerUnitsRow);
                var asset = CharacterFor(member.Id);
                view.Bind(member.Name, asset != null ? asset.Color : PartyOwnerColor);
                var target = SelectionTargetRef.PartyMember(member.Id);
                view.BindTarget(member.Id, id => _selection.OnTargetClicked(
                    SelectionTargetRef.PartyMember(id)));
                _selection.RegisterUnitTarget(target, view);
                _partyUnits.Add(member.Id, view);
            }

            foreach (var enemy in _session.State.Enemies)
            {
                var view = Instantiate(_unitPrefab, _enemyUnitsRow);
                view.Bind(PlaytestKoreanText.EnemyName(enemy.Id, enemy.Id), EnemyUnitTint);
                var target = SelectionTargetRef.Enemy(enemy.Id);
                view.BindTarget(enemy.Id, id => _selection.OnTargetClicked(
                    SelectionTargetRef.Enemy(id)));
                _selection.RegisterUnitTarget(target, view);
                _enemyUnits.Add(enemy.Id, view);
                _enemyMaxHp.Add(enemy.Id, enemy.Hp);
            }
        }

        private void BindPiles()
        {
            _drawPile.Bind(() => Presentations(_session.DrawPile)
                .OrderBy(presentation => presentation.DisplayName, StringComparer.Ordinal).ToList());
            _discardPile.Bind(() => Presentations(_session.DiscardPile));
            _fullDeck.Bind(() => Presentations(_session.AllDeckCards));
        }

        private IReadOnlyList<CardPresentation> Presentations(IReadOnlyList<OwnedCard> cards)
            => cards.Select(PresentationFor).ToList();

        private void OnHandClicked(int handIndex)
        {
            if (_session == null || handIndex < 0 || handIndex >= _session.Hand.Count)
            {
                return;
            }

            if (_session.CurrentTurnResolved)
            {
                SetMessage("이미 턴을 해석했습니다. '다음 턴'을 누르세요.");
                return;
            }

            var card = _session.Hand[handIndex];
            var def = card.Def;
            if (def.EnergyCost > _session.FateEnergy)
            {
                SetMessage("운명력이 부족합니다.");
                return;
            }

            var name = PlaytestKoreanText.CardName(def.Id, def.Name);
            if (def.Category == CardCategory.Execution)
            {
                if (!_session.TryPreviewExecutionPlacement(handIndex, out var placement))
                {
                    SetMessage("카드를 실행 순서에 배치할 수 없습니다.");
                    return;
                }

                var presentation = PresentationFor(card)
                    .WithExecutionOrder(placement.ExecutionOrder);
                _selection.BeginPlacement(
                    handIndex, presentation, placement.InsertionIndex);
                SetMessage(name + " — 레일 실루엣을 클릭해 배치하세요.");
            }
            else
            {
                int requiredTargets = CardTargetRules.RequiredRailTargets(def);
                if (def.Category != CardCategory.Intervention
                    || requiredTargets < 1
                    || requiredTargets > 2)
                {
                    SetMessage("사용할 수 없는 조작 카드입니다.");
                    return;
                }

                var targets = CurrentValidTargets(SelectionTargetKind.ExecutionCard);
                if (targets.Count < requiredTargets)
                {
                    SetMessage("대상으로 삼을 카드가 실행 순서에 부족합니다.");
                    return;
                }

                _selection.BeginTargetSelection(
                    handIndex, SelectionTargetKind.ExecutionCard, requiredTargets, targets);
                SetMessage(name + " — 대상 " + requiredTargets + "개를 선택하세요.");
            }

            RefreshSelections();
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_session == null || _session.CurrentTurnResolved)
            {
                return;
            }

            var order = _session.CurrentOrder;
            if (zoneIndex < 0 || zoneIndex >= order.Count)
            {
                return;
            }

            _selection.OnTargetClicked(SelectionTargetRef.ExecutionCard(zoneIndex));
        }

        private void OnEmptyClicked()
        {
            if (_selection.SelectionActive)
            {
                _selection.CancelSelection();
                SetMessage("선택 취소.");
                RefreshSelections();
            }
        }

        private void OnHandHovered(int handIndex, bool hovering)
        {
            if (_session == null || _selection.SelectionActive)
            {
                return;
            }

            if (!hovering)
            {
                _selection.HidePlacementHover(handIndex);
                return;
            }

            if (handIndex < 0 || handIndex >= _session.Hand.Count)
            {
                return;
            }

            var card = _session.Hand[handIndex];
            if (card.Def.Category != CardCategory.Execution
                || !_session.TryPreviewExecutionPlacement(handIndex, out var placement))
            {
                _selection.HidePlacementHover(handIndex);
                return;
            }

            _selection.ShowPlacementHover(
                handIndex,
                PresentationFor(card).WithExecutionOrder(placement.ExecutionOrder),
                placement.InsertionIndex);
        }

        private bool TryApplySelection(SelectionResult result)
        {
            if (_session == null || result.HandIndex < 0 || result.HandIndex >= _session.Hand.Count)
            {
                SetMessage("선택한 카드를 더 이상 사용할 수 없습니다.");
                return false;
            }

            var def = _session.Hand[result.HandIndex].Def;
            if (def.Category == CardCategory.Execution)
            {
                if (result.Targets.Count != 0)
                {
                    SetMessage("실행 카드는 직접 대상을 선택하지 않습니다.");
                    return false;
                }

                bool played = _session.PlayExecutionCard(result.HandIndex);
                SetMessage(played
                    ? PlaytestKoreanText.CardName(def.Id, def.Name) + " 배치."
                    : "운명력 또는 턴 상태로 카드를 배치할 수 없습니다.");
                return played;
            }

            int requiredTargets = CardTargetRules.RequiredRailTargets(def);
            if (def.Category != CardCategory.Intervention
                || requiredTargets < 1
                || requiredTargets > 2
                || result.Targets.Count != requiredTargets
                || result.Targets.Any(target => target.Kind != SelectionTargetKind.ExecutionCard))
            {
                SetMessage("대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
                return false;
            }

            int secondaryTarget = requiredTargets == 2 ? result.Targets[1].Index : -1;
            bool interventionPlayed = _session.PlayInterventionCard(
                result.HandIndex, result.Targets[0].Index, secondaryTarget);
            SetMessage(interventionPlayed
                ? "개입 카드 적용."
                : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            return interventionPlayed;
        }

        private void OnTurnButton()
        {
            if (_session == null || _session.IsComplete || _selection.SelectionActive)
            {
                return;
            }

            if (!_session.CurrentTurnResolved)
            {
                _session.ResolveTurn();
                SetMessage(_session.IsComplete
                    ? "전투 결과: " + PlaytestKoreanText.OutcomeName(_session.Outcome)
                    : "턴 해석 완료.");
            }
            else if (_session.BeginNextTurn())
            {
                SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            }

            RefreshAll();
        }

        private void BuildArtLookup()
        {
            _artById.Clear();
            foreach (var member in _party)
            {
                foreach (var entry in member.Deck.Entries)
                {
                    AddArt(entry.Card);
                }
            }

            foreach (var card in _enemyArtCards) AddArt(card);
        }

        private void AddArt(CardAsset card)
        {
            if (card != null && !string.IsNullOrEmpty(card.Id) && card.Art != null)
            {
                _artById[card.Id] = card.Art;
            }
        }

        private Sprite ArtFor(string id)
            => _artById.TryGetValue(id, out var sprite) ? sprite : PlaytestCardArt.Sprite(id);

        private CardPresentation PresentationFor(OwnedCard card)
        {
            OwnerPresentation(card.OwnerId, card.Def.Side, out var name, out var color, out var isPartyOwned);
            return CardPresentation.FromDefinition(card.Def, ArtFor, name, color, isPartyOwned);
        }

        private CardPresentation PresentationFor(ExecutionCardInstance card)
        {
            OwnerPresentation(card.OwnerId, card.Def.Side, out var name, out var color, out var isPartyOwned);
            return CardPresentation.From(card, ArtFor, name, color, isPartyOwned);
        }

        private void OwnerPresentation(string ownerId, Side side, out string name, out Color color, out bool isPartyOwned)
        {
            name = null;
            color = default;
            isPartyOwned = false;
            if (side == Side.Enemy)
            {
                return;
            }

            if (ownerId == null)
            {
                name = PlaytestKoreanText.PartyOwnerName();
                color = PartyOwnerColor;
                isPartyOwned = true;
                return;
            }

            var character = CharacterFor(ownerId);
            if (character != null)
            {
                name = character.DisplayName;
                color = character.Color;
            }
        }

        private CharacterAsset CharacterFor(string id)
        {
            foreach (var character in _party)
            {
                if (character != null && character.Id == id)
                {
                    return character;
                }
            }

            return null;
        }

        private IReadOnlyList<SelectionTargetRef> CurrentValidTargets(SelectionTargetKind kind)
        {
            if (_session == null)
            {
                return Array.Empty<SelectionTargetRef>();
            }

            switch (kind)
            {
                case SelectionTargetKind.PartyMember:
                    return _session.State.Party
                        .Where(member => member.IsAlive)
                        .Select(member => SelectionTargetRef.PartyMember(member.Id))
                        .ToList();
                case SelectionTargetKind.Enemy:
                    return _session.State.Enemies
                        .Where(enemy => enemy.Hp > 0)
                        .Select(enemy => SelectionTargetRef.Enemy(enemy.Id))
                        .ToList();
                case SelectionTargetKind.ExecutionCard:
                    return Enumerable.Range(0, _session.CurrentOrder.Count)
                        .Select(SelectionTargetRef.ExecutionCard)
                        .ToList();
                default:
                    return Array.Empty<SelectionTargetRef>();
            }
        }

        private void RefreshAll()
        {
            _hand.SetCards(
                _session.Hand.Select(PresentationFor).ToList(),
                OnHandClicked,
                OnHandHovered);
            _rail.SetCards(_session.CurrentOrder.Select(PresentationFor).ToList(), OnZoneClicked);
            RefreshUnits();
            RefreshSelections();
            RefreshHudTexts();
        }

        private void RefreshSelections()
        {
            bool selectionActive = _selection.SelectionActive;
            _drawPile.SetInputEnabled(!selectionActive);
            _discardPile.SetInputEnabled(!selectionActive);
            _fullDeck.SetInputEnabled(!selectionActive);
            _resetButton.interactable = !selectionActive;
            _turnButton.interactable = !selectionActive && !_session.IsComplete;
        }

        private void RefreshUnits()
        {
            int partyCount = _session.State.Party.Count;
            for (int i = 0; i < partyCount; i++)
            {
                var member = _session.State.Party[i];
                if (_partyUnits.TryGetValue(member.Id, out var view))
                {
                    view.SetHp(member.Hp, member.MaxHp);
                    view.SetStatuses(member.Statuses.All);
                    view.transform.SetSiblingIndex(partyCount - 1 - i);
                }
            }

            int enemyCount = _session.State.Enemies.Count;
            for (int i = 0; i < enemyCount; i++)
            {
                var enemy = _session.State.Enemies[i];
                if (_enemyUnits.TryGetValue(enemy.Id, out var view)
                    && _enemyMaxHp.TryGetValue(enemy.Id, out var maxHp))
                {
                    view.SetHp(enemy.Hp, maxHp);
                    view.SetStatuses(enemy.Statuses.All);
                    view.transform.SetSiblingIndex(i);
                }
            }
        }

        private void RefreshHudTexts()
        {
            _energyText.text = "운명력 " + _session.FateEnergy;
            _drawPile.SetCount(_session.DrawCount);
            _discardPile.SetCount(_session.DiscardCount);
            _fullDeck.SetCount(_session.AllDeckCards.Count);
            _turnButtonLabel.text = _session.CurrentTurnResolved ? "다음 턴" : "턴 실행";
            _turnButton.interactable = !_selection.SelectionActive && !_session.IsComplete;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
        }
    }
}
