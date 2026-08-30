using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>운명력·안내 문구와 턴 조작. 턴 라벨과 버튼 활성화가 한 상태에서 나오므로
    /// 함께 둔다.</summary>
    public sealed class BattleHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _energyText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _turnButton;
        [SerializeField] private TMP_Text _turnButtonLabel;
        [SerializeField] private Button _resetButton;

        [Header("재생")]
        [SerializeField] private Button _skipButton;
        [SerializeField] private Button _speedButton;
        [SerializeField] private TMP_Text _speedButtonLabel;

        [Tooltip("배속 버튼이 돌아가며 고르는 값들.")]
        [SerializeField] private float[] _speedSteps = { 1f, 2f, 4f };

        private int _speedIndex;

        public bool IsBound => _energyText != null && _messageText != null
            && _turnButton != null && _turnButtonLabel != null && _resetButton != null
            && _skipButton != null && _speedButton != null && _speedButtonLabel != null;

        public float Speed => _speedSteps != null && _speedSteps.Length > 0
            ? _speedSteps[_speedIndex]
            : 1f;

        public void Initialize(
            UnityAction onTurn, UnityAction onReset, UnityAction onSkip, UnityAction<float> onSpeed)
        {
            _turnButton.onClick.AddListener(onTurn);
            _resetButton.onClick.AddListener(onReset);
            _skipButton.onClick.AddListener(onSkip);
            _speedButton.onClick.AddListener(() =>
            {
                _speedIndex = _speedSteps.Length > 0 ? (_speedIndex + 1) % _speedSteps.Length : 0;
                RefreshSpeedLabel();
                onSpeed?.Invoke(Speed);
            });
            RefreshSpeedLabel();
        }

        /// <summary>스킵은 재생 중에만 누를 수 있다.</summary>
        public void SetSkipEnabled(bool enabled) => _skipButton.interactable = enabled;

        private void RefreshSpeedLabel() => _speedButtonLabel.text = Speed + "배";

        public void SetMessage(string message) => _messageText.text = message;

        public void Refresh(int fateEnergy, bool turnResolved)
        {
            _energyText.text = "운명력 " + fateEnergy;
            _turnButtonLabel.text = turnResolved ? "다음 턴" : "턴 실행";
        }

        public void SetInputEnabled(bool resetEnabled, bool turnEnabled)
        {
            _resetButton.interactable = resetEnabled;
            _turnButton.interactable = turnEnabled;
        }
    }
}
