using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>테스트 버튼 입력과 조작 가능 상태를 표시한다. 카드 목록은 관리하지 않는다.</summary>
    public sealed class HandFanSandboxPanel : MonoBehaviour
    {
        [SerializeField] private Button _addButton;
        [SerializeField] private Button _removeButton;
        [SerializeField] private Button _clearButton;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private TMP_Text _errorText;
        private bool _listening;

        public event Action AddRequested;
        public event Action RemoveRequested;
        public event Action ClearRequested;
        public bool IsBound => _addButton != null && _removeButton != null && _clearButton != null
            && _countText != null && _errorText != null;

        private void OnEnable()
        {
            if (_listening || !IsBound) return;
            _addButton.onClick.AddListener(RequestAdd);
            _removeButton.onClick.AddListener(RequestRemove);
            _clearButton.onClick.AddListener(RequestClear);
            _listening = true;
        }

        private void OnDisable()
        {
            if (!_listening) return;
            if (_addButton != null) _addButton.onClick.RemoveListener(RequestAdd);
            if (_removeButton != null) _removeButton.onClick.RemoveListener(RequestRemove);
            if (_clearButton != null) _clearButton.onClick.RemoveListener(RequestClear);
            _listening = false;
        }

        private void RequestAdd() { if (_addButton.interactable) AddRequested?.Invoke(); }
        private void RequestRemove() { if (_removeButton.interactable) RemoveRequested?.Invoke(); }
        private void RequestClear() { if (_clearButton.interactable) ClearRequested?.Invoke(); }

        public void ShowState(int count, bool canRemove, bool canClear)
        {
            if (!IsBound) return;
            _countText.text = $"핸드: {count}장";
            _errorText.text = string.Empty;
            _addButton.interactable = true;
            _removeButton.interactable = canRemove;
            _clearButton.interactable = canClear;
        }

        public void ShowError(string message)
        {
            if (_errorText != null) _errorText.text = message;
            if (_addButton != null) _addButton.interactable = false;
            if (_removeButton != null) _removeButton.interactable = false;
            if (_clearButton != null) _clearButton.interactable = false;
        }
    }
}
