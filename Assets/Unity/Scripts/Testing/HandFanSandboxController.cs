using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>시각 테스트의 콘텐츠, 목록, 핸드와 패널 호출을 연결한다.</summary>
    public sealed class HandFanSandboxController : MonoBehaviour
    {
        [SerializeField] private HandFanSandboxCardSource _source;
        [SerializeField] private HandFanSandboxPanel _panel;
        [SerializeField] private HandFanView _hand;
        private HandFanSandboxState _state;
        private bool _subscribed;

        private void Start()
        {
            if (_source == null || _panel == null || !_panel.IsBound || _hand == null)
            {
                Fail("HandFan 테스트 씬의 필수 참조가 누락되었습니다.");
                return;
            }
            if (!_source.TryLoad(out var samples, out var error))
            {
                Fail(error);
                return;
            }
            _state = new HandFanSandboxState(samples);
            Subscribe();
            RefreshCards();
        }

        private void OnEnable()
        {
            if (_state == null) return;
            Subscribe();
            _hand.SetInputEnabled(true);
            RefreshSelection();
        }

        private void OnDisable()
        {
            if (_subscribed && _panel != null)
            {
                _panel.AddRequested -= Add;
                _panel.RemoveRequested -= Remove;
                _panel.ClearRequested -= Clear;
            }
            _subscribed = false;
            if (_state == null) return;
            if (_hand != null) _hand.SetInputEnabled(false);
            if (_panel != null) _panel.ShowError("테스트 조작부가 비활성화되어 있습니다.");
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _panel.AddRequested += Add;
            _panel.RemoveRequested += Remove;
            _panel.ClearRequested += Clear;
            _subscribed = true;
        }

        private void Add() { _state.Add(); RefreshCards(); }
        private void Remove() { _state.RemoveSelected(); RefreshCards(); }
        private void Clear() { _state.Clear(); RefreshCards(); }

        private void OnCardClicked(int index)
        {
            if (!isActiveAndEnabled) return;
            _state.Select(index);
            RefreshSelection();
        }

        private void RefreshCards()
        {
            _hand.SetCards(_state.Cards, OnCardClicked, null);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            _hand.SetSelection(_state.SelectedIndex, CardView.SelectionKind.Primary);
            _panel.ShowState(_state.Cards.Count, _state.CanRemove, _state.CanClear);
        }

        private void Fail(string message)
        {
            if (_panel != null) _panel.ShowError(message);
            Debug.LogError(message, this);
            enabled = false;
        }
    }
}
