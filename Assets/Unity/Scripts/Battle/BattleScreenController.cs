using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>주어진 전투 세션 하나를 화면에 붙여 입력을 전달한다. 전투 구성과 전투 뒤의 흐름은
    /// CombatNodeFlow가 맡는다.</summary>
    public sealed class BattleScreenController : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private BattlePresenter _presenter;
        [SerializeField] private HandFanView _hand;
        [SerializeField] private ExecutionRailView _rail;
        [SerializeField] private BattleUnitsView _units;
        [SerializeField] private BattlePilesView _piles;
        [SerializeField] private BattleHudView _hud;
        [SerializeField] private CardSelectionController _selection;
        [SerializeField] private Playback.TurnPlaybackDirector _playback;

        private DeckCombatSession _session;

        /// <summary>전투 화면에 바인딩된 콘텐츠. 소유는 CombatNodeFlow다.</summary>
        private GameContent _content;

        /// <summary>타임라인을 한국어 문장으로 풀 때 쓰는 카탈로그. 세션과 함께 1회 만들어 재사용한다.</summary>
        private KoreanDescriptionCatalog _korean;

        private Action _onCombatFinished;

        /// <summary>CombatNodeFlow가 한 번 부른다. 배선이 비었으면 false — 그때는 콘솔로만 보고한다
        /// (_hud가 비어 있으면 메시지도 못 쓴다, 설계 §6).</summary>
        public bool Initialize(Action onRestart, Action onCombatFinished)
        {
            if (!IsWired())
            {
                Debug.LogError("전투 화면 컴포넌트 배선이 비어 있습니다.");
                return false;
            }

            _onCombatFinished = onCombatFinished;
            _hud.Initialize(
                OnTurnButton, () => onRestart(), _playback.Skip, speed => _playback.Speed = speed);
            _playback.Speed = _hud.Speed;
            _selection.Initialize(TryApplySelection, CurrentValidTargets, RefreshAll);
            return true;
        }

        public void ShowMessage(string message) => SetMessage(message);

        private bool IsWired()
            => _presenter != null
                && _units != null && _units.IsBound
                && _piles != null && _piles.IsBound
                && _hud != null && _hud.IsBound
                && _hand != null && _rail != null && _selection != null
                && _playback != null;

        /// <summary>전투 세션 하나를 화면에 붙인다. 파티·적·수치 구성은 모른다 — CombatNode가 만든다.</summary>
        public void Bind(DeckCombatSession session, GameContent content)
        {
            _selection.CancelSelection();
            _session = session;
            _content = content;
            _korean = KoreanDescriptionCatalog.CreateDefault(_content.Statuses);
            _presenter.Initialize(OwnerNameOf, _korean);
            _units.Spawn(
                _session.State,
                _presenter.OwnerColor,
                EnemyNameOf,
                key => _content.Statuses.DisplayNameOf(key));
            _piles.Bind(
                () => Presentations(_session.DrawPile)
                    .OrderBy(presentation => presentation.DisplayName, StringComparer.Ordinal)
                    .ToList(),
                () => Presentations(_session.DiscardPile),
                () => Presentations(_session.AllDeckCards));
            SetMessage("전투 시작.");
            RefreshAll();
        }

        /// <summary>적 이름은 전투 안 id가 아니라 정의 id(SpecId)로 찾는다 — 같은 적이 여럿이어도 이름은 같다.</summary>
        private string EnemyNameOf(string combatId)
        {
            foreach (var enemy in _session.State.Enemies)
            {
                if (enemy.Id == combatId)
                {
                    return PlaytestKoreanText.EnemyName(enemy.SpecId, enemy.SpecId);
                }
            }

            return combatId;
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
            if (_session == null || _playback.IsPlaying
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
            if (_session == null || _playback.IsPlaying || _session.CurrentTurnResolved)
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
            if (_session == null || _playback.IsPlaying || _selection.SelectionActive)
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
            if (_session == null || _session.IsComplete
                || _selection.SelectionActive || _playback.IsPlaying)
            {
                return;
            }

            if (!_session.CurrentTurnResolved)
            {
                ResolveAndPlay();
                return;
            }

            if (_session.BeginNextTurn())
            {
                SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            }

            RefreshAll();
        }

        /// <summary>해석은 한 프레임에 끝나지만 화면은 타임라인을 따라 흐른다. 뷰는 재생이 끝날
        /// 때까지 턴 이전 상태로 남아 있고, 완료 콜백의 RefreshAll이 최종 상태로 맞춘다 —
        /// 재생 중 RefreshAll을 부르면 연출이 결과로 덮인다.</summary>
        private void ResolveAndPlay()
        {
            _session.ResolveTurn();
            foreach (var evt in _session.LastTimeline)
            {
                Debug.Log(TimelineTextFormatter.FormatEvent(evt, _korean));
            }

            SetMessage("턴 해석 재생 중…");
            _playback.Play(_session.LastTimeline, OnPlaybackComplete);
            RefreshSelections();
        }

        private void OnPlaybackComplete()
        {
            SetMessage(_session.IsComplete
                ? "전투 결과: " + PlaytestKoreanText.OutcomeName(_session.Outcome)
                : "턴 해석 완료.");
            RefreshAll();
            if (_session.IsComplete)
            {
                _onCombatFinished?.Invoke();
            }
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
            bool blocked = _selection.SelectionActive || _playback.IsPlaying;
            _piles.SetInputEnabled(!blocked);
            _hud.SetInputEnabled(!blocked, !blocked && !_session.IsComplete);
            _hud.SetSkipEnabled(_playback.IsPlaying);
        }

        private void SetMessage(string message) => _hud.SetMessage(message);
    }
}
