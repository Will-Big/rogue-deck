using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Intervention;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
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
            InterventionTargeting,
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
        [SerializeField] private Button _cancelButton;
        [SerializeField] private GameObject _dimLayer;

        private const int FateEnergyPerTurn = 3;
        private const int Seed = 1;
        private static readonly Color EnemyUnitTint = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color PartyOwnerColor = new Color(0.55f, 0.48f, 0.75f, 1f);

        private DeckCombatSession _session;
        private InputMode _inputMode;
        private int _armedInterventionHandIndex = -1;
        private int _armedAllyTargetHandIndex = -1;
        private int _firstSwapZoneIndex = -1;
        private readonly List<UnitView> _partyUnits = new List<UnitView>();
        private readonly List<UnitView> _enemyUnits = new List<UnitView>();
        private readonly List<int> _enemyMaxHp = new List<int>();
        private readonly Dictionary<string, Sprite> _artById = new Dictionary<string, Sprite>();

        private void Start()
        {
            _turnButton.onClick.AddListener(OnTurnButton);
            _resetButton.onClick.AddListener(StartSession);
            _cancelButton.onClick.AddListener(OnCancelSelection);
            StartSession();
        }

        private void StartSession()
        {
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
            ClearArmed();
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
                _partyUnits.Add(view);
            }

            foreach (var enemy in _session.State.Enemies)
            {
                var view = Instantiate(_unitPrefab, _enemyUnitsRow);
                view.Bind(PlaytestKoreanText.EnemyName(enemy.Id, enemy.Id), EnemyUnitTint);
                view.BindTarget(enemy.Id, null);
                _enemyUnits.Add(view);
                _enemyMaxHp.Add(enemy.Hp);
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
            if (_session == null || _inputMode != InputMode.Normal || handIndex < 0 || handIndex >= _session.Hand.Count)
            {
                return;
            }

            if (_session.CurrentTurnResolved)
            {
                SetMessage("이미 턴을 해석했습니다. '다음 턴'을 누르세요.");
                return;
            }

            var def = _session.Hand[handIndex].Def;
            if (def.Category == CardCategory.Execution)
            {
                if (PartyTargetRules.RequiresExplicitAllyTarget(def))
                {
                    _inputMode = InputMode.AllyTargeting;
                    _armedAllyTargetHandIndex = handIndex;
                    SetMessage(PlaytestKoreanText.CardName(def.Id, def.Name) + " — 살아 있는 아군을 선택하세요.");
                    RefreshSelections();
                    return;
                }

                SetMessage(_session.PlayExecutionCard(handIndex)
                    ? PlaytestKoreanText.CardName(def.Id, def.Name) + " 배치."
                    : "운명력이 부족하거나 낼 수 없습니다.");
                ClearArmed();
                RefreshAll();
                return;
            }

            _inputMode = InputMode.InterventionTargeting;
            _armedInterventionHandIndex = handIndex;
            _firstSwapZoneIndex = -1;
            SetMessage(PlaytestKoreanText.CardName(def.Id, def.Name) + " — 레일에서 대상을 선택하세요.");
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
            ClearArmed();
            RefreshAll();
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_session == null || _inputMode != InputMode.InterventionTargeting || _armedInterventionHandIndex < 0)
            {
                return;
            }

            var def = _session.Hand[_armedInterventionHandIndex].Def;
            bool needsTwo = def.InterventionAction != null
                && def.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder;
            if (needsTwo && _firstSwapZoneIndex < 0)
            {
                _firstSwapZoneIndex = zoneIndex;
                SetMessage("교환할 두 번째 카드를 선택하세요.");
                RefreshSelections();
                return;
            }

            bool played = needsTwo
                ? _session.PlayInterventionCard(_armedInterventionHandIndex, _firstSwapZoneIndex, zoneIndex)
                : _session.PlayInterventionCard(_armedInterventionHandIndex, zoneIndex);
            SetMessage(played ? "개입 카드 적용." : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            ClearArmed();
            RefreshAll();
        }

        private void OnTurnButton()
        {
            if (_session == null || _session.IsComplete || _inputMode != InputMode.Normal)
            {
                return;
            }

            if (!_session.CurrentTurnResolved)
            {
                _session.ResolveTurn();
                ClearArmed();
                SetMessage(_session.IsComplete
                    ? "전투 결과: " + PlaytestKoreanText.OutcomeName(_session.Outcome)
                    : "턴 해석 완료.");
            }
            else if (_session.BeginNextTurn())
            {
                ClearArmed();
                SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            }

            RefreshAll();
        }

        private void OnCancelSelection()
        {
            if (_inputMode == InputMode.Normal)
            {
                return;
            }

            SetMessage("실행 취소.");
            ClearArmed();
            RefreshAll();
        }

        private void ClearArmed()
        {
            _inputMode = InputMode.Normal;
            _armedInterventionHandIndex = -1;
            _armedAllyTargetHandIndex = -1;
            _firstSwapZoneIndex = -1;
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
            bool intervention = _inputMode == InputMode.InterventionTargeting;
            bool ally = _inputMode == InputMode.AllyTargeting;
            _dimLayer.SetActive(intervention);
            _cancelButton.gameObject.SetActive(intervention || ally);
            _hand.SetSelection(
                ally ? _armedAllyTargetHandIndex : _armedInterventionHandIndex,
                CardView.SelectionKind.Primary);
            _rail.SetSelection(_firstSwapZoneIndex, CardView.SelectionKind.Secondary);
            _hand.SetInputEnabled(_inputMode == InputMode.Normal);
            _rail.SetInputEnabled(!ally);
            _drawPile.SetInputEnabled(_inputMode == InputMode.Normal);
            _discardPile.SetInputEnabled(_inputMode == InputMode.Normal);
            _fullDeck.SetInputEnabled(_inputMode == InputMode.Normal);
            _resetButton.interactable = _inputMode == InputMode.Normal;
            _turnButton.interactable = _inputMode == InputMode.Normal && !_session.IsComplete;

            for (int i = 0; i < _partyUnits.Count && i < _session.State.Party.Count; i++)
            {
                _partyUnits[i].SetTargetable(ally && _session.State.Party[i].IsAlive);
            }
        }

        private void RefreshUnits()
        {
            int partyCount = Math.Min(_partyUnits.Count, _session.State.Party.Count);
            for (int i = 0; i < partyCount; i++)
            {
                var member = _session.State.Party[i];
                _partyUnits[i].SetHp(member.Hp, member.MaxHp);
                _partyUnits[i].SetStatuses(member.Statuses.All);
                _partyUnits[i].transform.SetSiblingIndex(partyCount - 1 - i);
            }

            int enemyCount = Math.Min(_enemyUnits.Count, _session.State.Enemies.Count);
            for (int i = 0; i < enemyCount; i++)
            {
                _enemyUnits[i].SetHp(_session.State.Enemies[i].Hp, _enemyMaxHp[i]);
                _enemyUnits[i].SetStatuses(_session.State.Enemies[i].Statuses.All);
                _enemyUnits[i].transform.SetSiblingIndex(i);
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
