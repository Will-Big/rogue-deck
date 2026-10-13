using System;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>패배 결과를 보여주고 다시 시작을 알린다. 무엇이 다시 시작되는지는 모른다.</summary>
    public sealed class CombatResultView : MonoBehaviour
    {
        [SerializeField] private Button _restartButton;

        public bool IsBound => _restartButton != null;

        public void ShowDefeat(Action onRestart)
        {
            gameObject.SetActive(true);
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() => onRestart());
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
