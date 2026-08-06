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

        public bool IsBound => _energyText != null && _messageText != null
            && _turnButton != null && _turnButtonLabel != null && _resetButton != null;

        public void Initialize(UnityAction onTurn, UnityAction onReset)
        {
            _turnButton.onClick.AddListener(onTurn);
            _resetButton.onClick.AddListener(onReset);
        }

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
