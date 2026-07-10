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
    /// <summary>Battle screen over DeckCombatSession (visual revamp phase 1): stage units with per-unit
    /// HP bars, the scrollable execution rail, a curved hand fan, three pile viewers, and a single
    /// resolve/next turn button. Input is still the 2-step click flow (drag arrives in phase 2), but the
    /// selection-mode UX is final: while an intervention card is armed, everything except the rail dims
    /// and the left-side cancel button is the only way out. UI only — logic stays in the session.</summary>
    public sealed class BattleScreenController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DeckAsset _deck;
        [Tooltip("Enemy cards' art source (rules live in the goblin deck).")]
        [SerializeField] private CardAsset[] _enemyArtCards = Array.Empty<CardAsset>();

        [Header("Views")]
        [SerializeField] private HandFanView _hand;
        [SerializeField] private ExecutionRailView _rail;
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

        private const int PlayerHp = 30;
        private const int FateEnergyPerTurn = 3;
        private const int HandSize = 5;
        private const int Seed = 1;

        private static readonly Color PlayerUnitTint = new Color(0.25f, 0.4f, 0.55f, 1f);
        private static readonly Color EnemyUnitTint = new Color(0.55f, 0.25f, 0.25f, 1f);

        private DeckCombatSession _session;
        private int _armedInterventionHandIndex = -1;
        private int _firstSwapZoneIndex = -1;
        private UnitView _playerUnit;
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
            var specs = _deck != null ? _deck.ToSpecs() : StarterDeckSpecs.Build();
            var deckDefs = specs.Select(CardSpecMapper.ToDefinition).ToList();
            var enemies = new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) };
            _session = new DeckCombatSession(
                deckDefs, PlayerHp, enemies, GoblinDeck.Policy(Seed), FateEnergyPerTurn, HandSize, Seed);
            BuildArtLookup();
            SpawnUnits();
            BindPiles();
            ClearArmed();
            SetMessage(_deck != null ? "전투 시작." : "전투 시작 (코드 시작덱 폴백 — DeckAsset 미연결).");
            RefreshAll();
        }

        private void SpawnUnits()
        {
            foreach (Transform child in _playerUnitsRow) Destroy(child.gameObject);
            foreach (Transform child in _enemyUnitsRow) Destroy(child.gameObject);
            _enemyUnits.Clear();
            _enemyMaxHp.Clear();

            _playerUnit = UnitView.Create(_playerUnitsRow, new Vector2(180f, 250f));
            _playerUnit.Bind("플레이어", PlayerUnitTint);

            foreach (var enemy in _session.State.Enemies)
            {
                var view = UnitView.Create(_enemyUnitsRow, new Vector2(200f, 270f));
                view.Bind(PlaytestKoreanText.EnemyName(enemy.Id, enemy.Id), EnemyUnitTint);
                _enemyUnits.Add(view);
                _enemyMaxHp.Add(enemy.Hp);
            }
        }

        private void BindPiles()
        {
            // 뽑을 덱은 실제 순서가 스포일러라 이름순으로 보여준다 (Task 2 규약).
            _drawPile.Bind(() => Presentations(_session.DrawPile)
                .OrderBy(p => p.DisplayName, StringComparer.Ordinal).ToList());
            _discardPile.Bind(() => Presentations(_session.DiscardPile));
            _fullDeck.Bind(() => Presentations(_session.AllDeckCards));
        }

        private IReadOnlyList<CardPresentation> Presentations(IReadOnlyList<CardDefinition> cards)
            => cards.Select(c => CardPresentation.FromDefinition(c, ArtFor)).ToList();

        // --- input (2-step click flow ported from DeckPlaytestController; drag replaces it in phase 2) ---

        private void OnHandClicked(int handIndex)
        {
            if (_session == null) return;
            if (_session.CurrentTurnResolved)
            {
                SetMessage("이미 턴을 해석했습니다. '다음 턴'을 누르세요.");
                return;
            }

            var def = _session.Hand[handIndex];
            if (def.Category == CardCategory.Execution)
            {
                SetMessage(_session.PlayExecutionCard(handIndex)
                    ? PlaytestKoreanText.CardName(def.Id, def.Name) + " 배치."
                    : "운명력이 부족하거나 낼 수 없습니다.");
                ClearArmed();
                RefreshAll();
                return;
            }

            _armedInterventionHandIndex = handIndex;
            _firstSwapZoneIndex = -1;
            SetMessage(PlaytestKoreanText.CardName(def.Id, def.Name) + " — 레일에서 대상을 선택하세요.");
            RefreshSelections();
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_session == null || _armedInterventionHandIndex < 0) return;

            var def = _session.Hand[_armedInterventionHandIndex];
            var needsTwo = def.InterventionAction != null
                && def.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder;

            if (needsTwo && _firstSwapZoneIndex < 0)
            {
                _firstSwapZoneIndex = zoneIndex;
                SetMessage("교환할 두 번째 카드를 선택하세요.");
                RefreshSelections();
                return;
            }

            bool ok = needsTwo
                ? _session.PlayInterventionCard(_armedInterventionHandIndex, _firstSwapZoneIndex, zoneIndex)
                : _session.PlayInterventionCard(_armedInterventionHandIndex, zoneIndex);

            SetMessage(ok ? "개입 카드 적용." : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            ClearArmed();
            RefreshAll();
        }

        private void OnTurnButton()
        {
            if (_session == null || _session.IsComplete) return;

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
            SetMessage("실행 취소.");
            ClearArmed();
            RefreshAll();
        }

        private void ClearArmed()
        {
            _armedInterventionHandIndex = -1;
            _firstSwapZoneIndex = -1;
        }

        // --- art lookup (same GUID-backed pattern as DeckPlaytestController) ---

        private void BuildArtLookup()
        {
            _artById.Clear();
            if (_deck != null)
            {
                foreach (var entry in _deck.Entries) AddArt(entry.Card);
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

        // --- render ---

        private void RefreshAll()
        {
            _hand.SetCards(
                _session.Hand.Select(c => CardPresentation.FromDefinition(c, ArtFor)).ToList(), OnHandClicked);
            _rail.SetCards(
                _session.CurrentOrder.Select(c => CardPresentation.From(c, ArtFor)).ToList(), OnZoneClicked);
            RefreshSelections();
            RefreshUnits();
            RefreshHudTexts();
        }

        private void RefreshSelections()
        {
            // Selection mode (spec §6): dim everything but the rail while an intervention wants targets.
            _dimLayer.SetActive(_armedInterventionHandIndex >= 0);
            _hand.SetSelection(_armedInterventionHandIndex, CardView.SelectionKind.Primary);
            _rail.SetSelection(_firstSwapZoneIndex, CardView.SelectionKind.Secondary);
        }

        private void RefreshUnits()
        {
            _playerUnit.SetHp(_session.State.PlayerHp, PlayerHp);
            for (int i = 0; i < _enemyUnits.Count && i < _session.State.Enemies.Count; i++)
            {
                _enemyUnits[i].SetHp(_session.State.Enemies[i].Hp, _enemyMaxHp[i]);
            }
        }

        private void RefreshHudTexts()
        {
            _energyText.text = "운명력 " + _session.FateEnergy;
            _drawPile.SetCount(_session.DrawCount);
            _discardPile.SetCount(_session.DiscardCount);
            _fullDeck.SetCount(_session.AllDeckCards.Count);
            _turnButtonLabel.text = _session.CurrentTurnResolved ? "다음 턴" : "턴 실행";
            _turnButton.interactable = !_session.IsComplete;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
        }
    }
}
