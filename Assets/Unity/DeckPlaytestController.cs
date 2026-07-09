using System.Collections.Generic;
using System.Linq;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Playable deck screen over DeckCombatSession: a hand of CardViews (execution = one-click place,
    /// intervention = 2-step click targeting) and the future zone of CardViews. UI only — logic is in the session.</summary>
    public sealed class DeckPlaytestController : MonoBehaviour
    {
        private enum EnemyKind
        {
            Goblin,
            Warden
        }

        [Header("Data")]
        [SerializeField] private DeckAsset _deck;
        [SerializeField] private EnemyKind _enemyKind = EnemyKind.Goblin;
        [Tooltip("Enemy cards' art source (rules live in the selected pure enemy deck).")]
        [SerializeField] private CardAsset[] _enemyArtCards = System.Array.Empty<CardAsset>();

        [Header("Prefab + containers")]
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private RectTransform _handRow;
        [SerializeField] private RectTransform _zoneRow;

        [Header("Text")]
        [SerializeField] private TMP_Text _stateText;
        [SerializeField] private TMP_Text _pilesText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _timelineText;

        [Header("Buttons")]
        [SerializeField] private Button _resolveButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _resetButton;

        // --- playtest session config (named so the call site reads as more than positional magic numbers) ---
        private const int PlayerHp = 30;
        private const int FateEnergyPerTurn = 3;
        private const int HandSize = 5;
        private const int Seed = 1;

        private DeckCombatSession _session;
        private int _armedInterventionHandIndex = -1;
        private int _firstSwapZoneIndex = -1;
        private readonly List<CardView> _handViews = new List<CardView>();
        private readonly List<CardView> _zoneViews = new List<CardView>();
        // id -> authored sprite (GUID-backed via CardAsset.Art) so moving the art file never breaks the link.
        private readonly Dictionary<string, Sprite> _artById = new Dictionary<string, Sprite>();

        private void Start()
        {
            _resolveButton.onClick.AddListener(ResolveTurn);
            _nextButton.onClick.AddListener(NextTurn);
            _resetButton.onClick.AddListener(StartSession);
            StartSession();
        }

        private void StartSession()
        {
            var specs = _deck != null ? _deck.ToSpecs() : StarterDeckSpecs.Build();
            var deckDefs = specs.Select(CardSpecMapper.ToDefinition).ToList();
            var enemies = new[] { new Enemy(EnemyId(), EnemyStartingHp()) };
            _session = new DeckCombatSession(
                deckDefs, PlayerHp, enemies, EnemyPolicy(Seed), FateEnergyPerTurn, HandSize, Seed);
            BuildArtLookup();
            ClearArmed();
            SetMessage(_deck != null ? "전투 시작." : "전투 시작 (코드 시작덱 폴백 — DeckAsset 미연결).");
            RefreshAll();
        }

        private string EnemyId()
            => _enemyKind == EnemyKind.Warden ? WardenDeck.EnemyId : GoblinDeck.EnemyId;

        private int EnemyStartingHp()
            => _enemyKind == EnemyKind.Warden ? WardenDeck.StartingHp : GoblinDeck.StartingHp;

        private IEnemyTurnPolicy EnemyPolicy(int seed)
            => _enemyKind == EnemyKind.Warden ? WardenDeck.Policy(seed) : GoblinDeck.Policy(seed);

        // --- input ---

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
            SetMessage(PlaytestKoreanText.CardName(def.Id, def.Name) + " — 줄에서 대상을 선택하세요.");
            RefreshHand();
            RefreshZone();
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_session == null) return;
            if (_armedInterventionHandIndex < 0)
            {
                return;
            }

            var def = _session.Hand[_armedInterventionHandIndex];
            var needsTwo = def.InterventionAction != null && def.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder;

            if (needsTwo && _firstSwapZoneIndex < 0)
            {
                _firstSwapZoneIndex = zoneIndex;
                SetMessage("교환할 두 번째 카드를 선택하세요.");
                RefreshZone();
                return;
            }

            bool ok = needsTwo
                ? _session.PlayInterventionCard(_armedInterventionHandIndex, _firstSwapZoneIndex, zoneIndex)
                : _session.PlayInterventionCard(_armedInterventionHandIndex, zoneIndex);

            SetMessage(ok ? "개입 카드 적용." : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            ClearArmed();
            RefreshAll();
        }

        private void ResolveTurn()
        {
            if (_session == null || _session.CurrentTurnResolved) return;
            _session.ResolveTurn();
            ClearArmed();
            SetMessage("턴 해석 완료.");
            RefreshAll();
        }

        private void NextTurn()
        {
            if (_session == null || !_session.BeginNextTurn()) return;
            ClearArmed();
            SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            RefreshAll();
        }

        private void ClearArmed()
        {
            _armedInterventionHandIndex = -1;
            _firstSwapZoneIndex = -1;
        }

        private void BuildArtLookup()
        {
            _artById.Clear();
            if (_deck != null)
            {
                foreach (var entry in _deck.Entries)
                {
                    AddArt(entry.Card);
                }
            }

            foreach (var card in _enemyArtCards)
            {
                AddArt(card);
            }
        }

        private void AddArt(CardAsset card)
        {
            if (card != null && !string.IsNullOrEmpty(card.Id) && card.Art != null)
            {
                _artById[card.Id] = card.Art;
            }
        }

        // Authored CardAsset.Art (GUID, move-safe) first; Resources path only as a last-resort fallback.
        private Sprite ArtFor(string id)
            => _artById.TryGetValue(id, out var sprite) ? sprite : PlaytestCardArt.Sprite(id);

        // --- render ---

        private void RefreshAll()
        {
            RefreshZone();
            RefreshHand();
            RefreshState();
            RefreshTimeline();
            RefreshButtons();
        }

        private void RefreshHand()
        {
            foreach (var v in _handViews) Destroy(v.gameObject);
            _handViews.Clear();

            for (int i = 0; i < _session.Hand.Count; i++)
            {
                var view = Instantiate(_cardPrefab, _handRow);
                int captured = i;
                view.Bind(CardPresentation.FromDefinition(_session.Hand[i], ArtFor), () => OnHandClicked(captured));
                view.SetSelection(i == _armedInterventionHandIndex ? CardView.SelectionKind.Primary : CardView.SelectionKind.None);
                _handViews.Add(view);
            }
        }

        private void RefreshZone()
        {
            foreach (var v in _zoneViews) Destroy(v.gameObject);
            _zoneViews.Clear();

            var order = _session.CurrentOrder;
            for (int i = 0; i < order.Count; i++)
            {
                var view = Instantiate(_cardPrefab, _zoneRow);
                int captured = i;
                view.Bind(CardPresentation.From(order[i], ArtFor), () => OnZoneClicked(captured));
                view.SetSelection(i == _firstSwapZoneIndex ? CardView.SelectionKind.Secondary : CardView.SelectionKind.None);
                _zoneViews.Add(view);
            }
        }

        private void RefreshState()
        {
            var sb = new StringBuilder();
            sb.Append("턴 ").Append(_session.TurnIndex + 1)
              .Append("    플레이어 HP: ").Append(_session.State.PlayerHp)
              .Append("    운명력: ").Append(_session.FateEnergy)
              .Append("    ").Append(StatusText(_session.State.PlayerStatuses));
            foreach (var enemy in _session.State.Enemies)
            {
                var name = PlaytestKoreanText.EnemyName(enemy.Id, enemy.Id);
                sb.Append('\n').Append(name).Append(" HP: ").Append(enemy.Hp)
                  .Append("    ").Append(StatusText(enemy.Statuses));
            }

            if (_session.IsComplete)
            {
                sb.Append("\n결과: ").Append(PlaytestKoreanText.OutcomeName(_session.Outcome));
            }

            _stateText.text = sb.ToString();
            _pilesText.text = "덱 " + _session.DrawCount + " · 버림 " + _session.DiscardCount;
        }

        private void RefreshTimeline()
        {
            if (_session.LastTimeline == null)
            {
                _timelineText.text = string.Empty;
                return;
            }

            var sb = new StringBuilder("해석 결과 (").Append(_session.TurnIndex + 1).Append("턴)\n");
            foreach (var evt in _session.LastTimeline)
            {
                if (evt is CardResolved card)
                {
                    sb.Append("- ").Append(PlaytestKoreanText.CardName(card.CardId, card.CardId))
                      .Append(" | ").Append(PlaytestKoreanText.ConditionName(card.ConditionTier))
                      .Append(" | 피해 ").Append(card.DamageDealt).Append('\n');
                }
                else if (evt is TurnEnded ended)
                {
                    sb.Append("전투 결과: ").Append(PlaytestKoreanText.OutcomeName(ended.Outcome)).Append('\n');
                }
            }

            _timelineText.text = sb.ToString();
        }

        private void RefreshButtons()
        {
            _resolveButton.interactable = !_session.CurrentTurnResolved;
            _nextButton.interactable = _session.CurrentTurnResolved && !_session.IsComplete;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
        }

        private static string StatusText(StatusBag bag)
        {
            var parts = new List<string>();
            foreach (var status in bag.All)
            {
                var amount = status.Magnitude > 0 ? status.Magnitude : status.Count;
                var name = PlaytestKoreanText.StatusName(status.Key);
                parts.Add(amount > 0 ? name + "(" + amount + ")" : name);
            }

            return parts.Count == 0 ? string.Empty : "[" + string.Join(", ", parts) + "]";
        }
    }
}
