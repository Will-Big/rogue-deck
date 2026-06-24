using System;
using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>uGUI playtest driver: instantiates a CardView per future-zone card, applies fate actions,
    /// resolves/advances turns. UI objects are wired by FateWeaverPlaytestSceneCreator.</summary>
    public sealed class FateWeaverPlaytestController : MonoBehaviour
    {
        [Header("Card row")]
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private RectTransform _cardRow;

        [Header("Scenario picker")]
        [SerializeField] private RectTransform _scenarioRow;
        [SerializeField] private Button _scenarioButtonTemplate;

        [Header("Text panels")]
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _stateText;
        [SerializeField] private TMP_Text _selectionText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _timelineText;

        [Header("Fate action buttons")]
        [SerializeField] private Button _initMinusButton;
        [SerializeField] private Button _initPlusButton;
        [SerializeField] private Button _swapButton;
        [SerializeField] private Button _lockButton;

        [Header("Flow buttons")]
        [SerializeField] private Button _resolveButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _resetButton;

        private MultiTurnPlaytestSession _session;
        private MultiTurnScenario _currentScenario;
        private string _primaryCardId;
        private string _secondaryCardId;
        private readonly List<CardView> _cardViews = new List<CardView>();

        private void Awake()
        {
            if (_cardPrefab == null)
            {
                var go = Resources.Load<GameObject>("CardView");
                if (go != null)
                {
                    _cardPrefab = go.GetComponent<CardView>();
                }
            }
        }

        private void Start()
        {
            BuildScenarioButtons();
            WireButtons();
            LoadScenario(SampleMultiTurnScenarios.All[0].Build());
        }

        private void BuildScenarioButtons()
        {
            if (_scenarioButtonTemplate == null || _scenarioRow == null)
            {
                return;
            }

            _scenarioButtonTemplate.gameObject.SetActive(false);
            foreach (var entry in SampleMultiTurnScenarios.All)
            {
                var capturedId = entry.Id;
                var button = Instantiate(_scenarioButtonTemplate, _scenarioRow);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = PlaytestKoreanText.ScenarioName(capturedId, capturedId);
                }

                button.onClick.AddListener(() => LoadScenario(SampleMultiTurnScenarios.Find(capturedId)));
            }
        }

        private void WireButtons()
        {
            _initMinusButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, -2)));
            _initPlusButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, 2)));
            _swapButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.SwapInitiative, 1, 0), needsSecondary: true));
            _lockButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.Lock, 1, 0)));

            _resolveButton.onClick.AddListener(ResolveTurn);
            _nextButton.onClick.AddListener(NextTurn);
            _resetButton.onClick.AddListener(() => LoadScenario(_currentScenario));
        }

        private void LoadScenario(MultiTurnScenario scenario)
        {
            _currentScenario = scenario;
            _session = new MultiTurnPlaytestSession(scenario);
            _primaryCardId = null;
            _secondaryCardId = null;
            SetMessage("시나리오를 불러왔습니다.");
            RefreshAll();
        }

        private void ResolveTurn()
        {
            if (_session.CurrentTurnResolved)
            {
                return;
            }

            _session.ResolveTurn();
            SetMessage("턴 실행 완료.");
            RefreshAll();
        }

        private void NextTurn()
        {
            if (!_session.AdvanceTurn())
            {
                return;
            }

            _primaryCardId = null;
            _secondaryCardId = null;
            SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            RefreshAll();
        }

        private void Apply(FateActionData action, bool needsSecondary = false)
        {
            if (_primaryCardId == null || (needsSecondary && _secondaryCardId == null))
            {
                SetMessage(needsSecondary ? "주 대상과 보조 대상을 선택하세요." : "주 대상을 선택하세요.");
                return;
            }

            try
            {
                var result = _session.ApplyFateAction(
                    action, _primaryCardId, needsSecondary ? _secondaryCardId : null);
                SetMessage(result.AppliedCount == 1
                    ? PlaytestKoreanText.FateActionName(action.Key) + " 적용 완료."
                    : "액션을 적용할 수 없습니다. 운명력·고정·대상 규칙을 확인하세요.");
            }
            catch (Exception exception)
            {
                SetMessage("오류: " + exception.Message);
            }

            RefreshAll();
        }

        private void SelectCard(string cardId)
        {
            if (_primaryCardId == cardId)
            {
                _primaryCardId = null;
                _secondaryCardId = null;
            }
            else if (_primaryCardId == null || _secondaryCardId != null)
            {
                _primaryCardId = cardId;
                _secondaryCardId = null;
            }
            else
            {
                _secondaryCardId = cardId;
            }

            RefreshSelection();
        }

        private void RefreshAll()
        {
            RefreshCards();
            RefreshState();
            RefreshTimeline();
            RefreshButtons();
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _cardViews.Count; i++)
            {
                Destroy(_cardViews[i].gameObject);
            }

            _cardViews.Clear();

            if (_cardPrefab == null)
            {
                Debug.LogError("CardView prefab missing — run 'Fate Weaver ▸ Build Playtest Scene (uGUI)'.");
                return;
            }

            foreach (var card in _session.CurrentOrder)
            {
                var view = Instantiate(_cardPrefab, _cardRow);
                var capturedId = card.Def.Id;
                view.Bind(CardPresentation.From(card), () => SelectCard(capturedId));
                _cardViews.Add(view);
            }

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            foreach (var view in _cardViews)
            {
                view.SetSelection(CardView.SelectionKind.None);
            }

            for (int i = 0; i < _cardViews.Count && i < _session.CurrentOrder.Count; i++)
            {
                var id = _session.CurrentOrder[i].Def.Id;
                if (id == _primaryCardId)
                {
                    _cardViews[i].SetSelection(CardView.SelectionKind.Primary);
                }
                else if (id == _secondaryCardId)
                {
                    _cardViews[i].SetSelection(CardView.SelectionKind.Secondary);
                }
            }

            _selectionText.text = "주 대상: " + NameOf(_primaryCardId) + "    보조 대상: " + NameOf(_secondaryCardId);
        }

        private void RefreshState()
        {
            _headerText.text = PlaytestKoreanText.ScenarioName(_currentScenario.Id, _session.Name)
                + "    턴 " + (_session.TurnIndex + 1) + " / " + _session.TurnCount;

            var sb = new StringBuilder();
            sb.Append("플레이어 HP: ").Append(_session.State.PlayerHp)
              .Append("    운명력: ").Append(_session.State.FateEnergy)
              .Append("    ").Append(StatusText(_session.State.PlayerStatuses));
            foreach (var enemy in _session.State.Enemies)
            {
                var enemyName = PlaytestKoreanText.EnemyName(enemy.Id, enemy.Id);
                sb.Append('\n').Append(enemyName).Append(" HP: ").Append(enemy.Hp)
                  .Append("    ").Append(StatusText(enemy.Statuses));
            }

            if (_session.IsComplete)
            {
                sb.Append("\n결과: ").Append(PlaytestKoreanText.OutcomeName(_session.Outcome));
            }

            _stateText.text = sb.ToString();
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
            var canAct = !_session.CurrentTurnResolved;
            _initMinusButton.interactable = canAct;
            _initPlusButton.interactable = canAct;
            _swapButton.interactable = canAct;
            _lockButton.interactable = canAct;
            _resolveButton.interactable = canAct;
            _nextButton.interactable = _session.CurrentTurnResolved && !_session.IsComplete;
        }

        private string NameOf(string cardId)
        {
            if (cardId == null)
            {
                return "-";
            }

            foreach (var card in _session.CurrentOrder)
            {
                if (card.Def.Id == cardId)
                {
                    return PlaytestKoreanText.CardName(card.Def.Id, card.Def.Name);
                }
            }

            return cardId;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = message;
            }
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
