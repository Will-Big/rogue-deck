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
        private enum InputMode
        {
            Normal,
            AllyTargeting
        }

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
        private InputMode _inputMode;
        private int _armedAllyTargetHandIndex = -1;
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
            _selection.Initialize(ApplyCommand);
            _rail.SetRailClicked(_selection.OnRailAreaClicked);
            StartSession();
        }

        private void StartSession()
        {
            _selection.CancelSelection();
            ClearAllyTargeting();
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
                GoblinDeck.Policy(Seed),
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
                view.BindTarget(member.Id, OnAllyUnitClicked);
                _partyUnits.Add(member.Id, view);
            }

            foreach (var enemy in _session.State.Enemies)
            {
                var view = Instantiate(_unitPrefab, _enemyUnitsRow);
                view.Bind(PlaytestKoreanText.EnemyName(enemy.Id, enemy.Id), EnemyUnitTint);
                view.BindTarget(enemy.Id, null);
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
            if (_session == null || _inputMode != InputMode.Normal
                || handIndex < 0 || handIndex >= _session.Hand.Count)
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

            if (def.Category == CardCategory.Execution && PartyTargetRules.RequiresExplicitAllyTarget(def))
            {
                _selection.CancelSelection();
                _inputMode = InputMode.AllyTargeting;
                _armedAllyTargetHandIndex = handIndex;
                _hand.SetHoverSuppressed(true);
                _hand.SetHeld(handIndex, true);
                SetMessage(PlaytestKoreanText.CardName(def.Id, def.Name)
                    + " — 살아 있는 아군을 선택하세요.");
                RefreshSelections();
                return;
            }

            int requiredTargets = CardTargetRules.RequiredRailTargets(def);
            if (_session.CurrentOrder.Count < requiredTargets)
            {
                SetMessage("대상으로 삼을 카드가 레일에 부족합니다.");
                return;
            }

            _selection.BeginSelection(handIndex, requiredTargets, PresentationFor(card));
            var name = PlaytestKoreanText.CardName(def.Id, def.Name);
            SetMessage(requiredTargets == 0
                ? name + " — 레일을 클릭해 배치하세요."
                : requiredTargets == 1
                    ? name + " — 대상을 클릭하세요."
                    : name + " — 대상 " + requiredTargets + "개를 클릭하세요.");
            RefreshSelections();
        }

        private void OnAllyUnitClicked(string memberId)
        {
            if (_session == null || _inputMode != InputMode.AllyTargeting || _armedAllyTargetHandIndex < 0)
            {
                return;
            }

            bool targetIsAlive = PartyTargetRules.IsValidExplicitAllyTarget(_session.State, memberId);
            bool played = targetIsAlive && _session.PlayExecutionCard(_armedAllyTargetHandIndex, memberId);
            SetMessage(played ? "아군 대상 카드 배치." : "대상이 쓰러졌거나 카드를 낼 수 없습니다.");
            ClearAllyTargeting();
            RefreshAll();
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_session == null || _inputMode == InputMode.AllyTargeting || _session.CurrentTurnResolved)
            {
                return;
            }

            var order = _session.CurrentOrder;
            if (zoneIndex < 0 || zoneIndex >= order.Count)
            {
                return;
            }

            _selection.OnZoneClicked(zoneIndex, PresentationFor(order[zoneIndex]));
        }

        private void OnEmptyClicked()
        {
            if (_selection.SelectionActive)
            {
                _selection.CancelSelection();
                SetMessage("선택 취소.");
                RefreshSelections();
                return;
            }

            if (_inputMode == InputMode.AllyTargeting)
            {
                ClearAllyTargeting();
                SetMessage("선택 취소.");
                RefreshSelections();
            }
        }

        private void ApplyCommand(SelectionCommand command)
        {
            if (_session == null || command.HandIndex < 0 || command.HandIndex >= _session.Hand.Count)
            {
                SetMessage("선택한 카드를 더 이상 사용할 수 없습니다.");
                RefreshAll();
                return;
            }

            if (command.PlayExecution)
            {
                var def = _session.Hand[command.HandIndex].Def;
                SetMessage(_session.PlayExecutionCard(command.HandIndex)
                    ? PlaytestKoreanText.CardName(def.Id, def.Name) + " 배치."
                    : "운명력이 부족하거나 낼 수 없습니다.");
            }
            else if (command.PlayIntervention)
            {
                bool played = _session.PlayInterventionCard(
                    command.HandIndex, command.TargetA, command.TargetB);
                SetMessage(played
                    ? "개입 카드 적용."
                    : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            }

            RefreshAll();
        }

        private void OnTurnButton()
        {
            if (_session == null || _session.IsComplete || _inputMode != InputMode.Normal)
            {
                return;
            }

            _selection.CancelSelection();
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

        private void ClearAllyTargeting()
        {
            _hand.SetHeld(_armedAllyTargetHandIndex, false);
            _hand.SetHoverSuppressed(false);
            _inputMode = InputMode.Normal;
            _armedAllyTargetHandIndex = -1;
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

        private void RefreshAll()
        {
            _hand.SetCards(_session.Hand.Select(PresentationFor).ToList(), OnHandClicked);
            _rail.SetCards(_session.CurrentOrder.Select(PresentationFor).ToList(), OnZoneClicked);
            RefreshUnits();
            RefreshSelections();
            RefreshHudTexts();
        }

        private void RefreshSelections()
        {
            bool ally = _inputMode == InputMode.AllyTargeting;
            bool cardSelection = _selection.SelectionActive;
            _hand.SetSelection(ally ? _armedAllyTargetHandIndex : -1, CardView.SelectionKind.Primary);
            _hand.SetInputEnabled(!ally);
            _rail.SetInputEnabled(!ally);
            _drawPile.SetInputEnabled(!ally && !cardSelection);
            _discardPile.SetInputEnabled(!ally && !cardSelection);
            _fullDeck.SetInputEnabled(!ally && !cardSelection);
            _resetButton.interactable = !ally && !cardSelection;
            _turnButton.interactable = !ally && !_session.IsComplete;

            foreach (var member in _session.State.Party)
            {
                if (_partyUnits.TryGetValue(member.Id, out var view))
                {
                    view.SetTargetable(ally && member.IsAlive);
                }
            }
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
            _turnButton.interactable = _inputMode == InputMode.Normal && !_session.IsComplete;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
        }
    }
}
