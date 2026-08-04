using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Intervention;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;
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

        [Header("Views")]
        [SerializeField] private BattlePresenter _presenter;
        [SerializeField] private HandFanView _hand;
        [SerializeField] private ExecutionRailView _rail;
        [SerializeField] private BattleUnitsView _units;
        [SerializeField] private BattlePilesView _piles;
        [SerializeField] private BattleHudView _hud;
        [SerializeField] private CardSelectionController _selection;

        private const int FateEnergyPerTurn = 3;
        private const int Seed = 1;

        private DeckCombatSession _session;

        /// <summary>부팅 1회로 만들어 상주하는 콘텐츠. 씬을 리셋해도 다시 읽지 않는다(설계 §4.5).</summary>
        private GameContent _content;

        private void Start()
        {
            _hud.Initialize(OnTurnButton, StartSession);
            _selection.Initialize(TryApplySelection, CurrentValidTargets, RefreshAll);
            StartSession();
        }

        private void StartSession()
        {
            _selection.CancelSelection();
            if (_units == null || !_units.IsBound || _party == null || _party.Length == 0
                || _party.Any(member => member == null))
            {
                SetMessage("파티 CharacterAsset 또는 UnitView 프리팹이 연결되지 않았습니다.");
                return;
            }

            if (_content == null)
            {
                var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
                if (!loaded.Succeeded)
                {
                    var reasons = string.Join("\n", loaded.Errors);
                    SetMessage("콘텐츠 로드 실패:\n" + reasons);
                    Debug.LogError("콘텐츠 로드 실패:\n" + reasons);
                    return;
                }

                _content = loaded.Content;
            }

            var tuning = PartyPrototypeRoster.Tuning;
            var loadouts = _party
                .Select(member => ContentLoadouts.For(
                    _content, member.Id, tuning.DefaultMemberMaxHp))
                .ToList();
            var enemies = new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) };
            _session = new DeckCombatSession(
                loadouts,
                enemies,
                GoblinDeck.Policy(),
                tuning,
                partyCards: null,
                fateEnergyPerTurn: FateEnergyPerTurn,
                seed: Seed);

            _presenter.Initialize(OwnerNameOf);
            _units.Spawn(
                _session.State,
                _presenter.OwnerColor,
                id => PlaytestKoreanText.EnemyName(id, id));
            _piles.Bind(
                () => Presentations(_session.DrawPile)
                    .OrderBy(presentation => presentation.DisplayName, StringComparer.Ordinal)
                    .ToList(),
                () => Presentations(_session.DiscardPile),
                () => Presentations(_session.AllDeckCards));
            SetMessage("전투 시작.");
            RefreshAll();
        }

        private IReadOnlyList<CardPresentation> Presentations(IReadOnlyList<OwnedCard> cards)
            => cards.Select(card => _presenter.For(card)).ToList();

        /// <summary>표시명은 콘텐츠에서 왔고 세션이 들고 있다.</summary>
        private string OwnerNameOf(string ownerId)
        {
            foreach (var member in _session.State.Party)
            {
                if (member.Id == ownerId)
                {
                    return member.Name;
                }
            }

            return null;
        }

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

                var presentation = _presenter.For(card)
                    .WithExecutionOrder(placement.ExecutionOrder);
                _selection.BeginPlacement(
                    handIndex, presentation, placement.InsertionIndex);
                SetMessage(name + " — 레일 실루엣을 클릭해 배치하세요.");
            }
            else
            {
                var req = _session.DescribeTargeting(handIndex);
                if (req.Kind != TargetKind.RailCard)
                {
                    SetMessage("사용할 수 없는 조작 카드입니다.");
                    return;
                }

                var targets = CurrentValidTargets(SelectionTargetKind.ExecutionCard);
                if (targets.Count < req.Count)
                {
                    SetMessage("대상으로 삼을 카드가 실행 순서에 부족합니다.");
                    return;
                }

                _selection.BeginTargetSelection(
                    handIndex, SelectionTargetKind.ExecutionCard, req.Count, targets);
                SetMessage(name + " — 대상 " + req.Count + "개를 선택하세요.");
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
                _presenter.For(card).WithExecutionOrder(placement.ExecutionOrder),
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

            var req = _session.DescribeTargeting(result.HandIndex);
            if (req.Kind != TargetKind.RailCard
                || result.Targets.Count != req.Count
                || result.Targets.Any(target => target.Kind != SelectionTargetKind.ExecutionCard))
            {
                SetMessage("대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
                return false;
            }

            int secondaryTarget = req.Count == 2 ? result.Targets[1].Index : -1;
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

        private IReadOnlyList<SelectionTargetRef> CurrentValidTargets(SelectionTargetKind kind)
        {
            if (_session == null)
            {
                return Array.Empty<SelectionTargetRef>();
            }

            switch (kind)
            {
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
                _session.Hand.Select(card => _presenter.For(card)).ToList(),
                OnHandClicked,
                OnHandHovered);
            _rail.SetCards(
                _session.CurrentOrder.Select(card => _presenter.For(card)).ToList(),
                OnZoneClicked);
            _units.Refresh(_session.State);
            _piles.Refresh(
                _session.DrawCount, _session.DiscardCount, _session.AllDeckCards.Count);
            _hud.Refresh(_session.FateEnergy, _session.CurrentTurnResolved);
            RefreshSelections();
        }

        private void RefreshSelections()
        {
            bool selectionActive = _selection.SelectionActive;
            _piles.SetInputEnabled(!selectionActive);
            _hud.SetInputEnabled(!selectionActive, !selectionActive && !_session.IsComplete);
        }

        private void SetMessage(string message) => _hud.SetMessage(message);
    }
}
