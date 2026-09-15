using System;
using TMPro;
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

        /// <summary>프리팹 저작용 훅. BattleSceneBuilder가 부른다.</summary>
        public static CombatResultView EditorCreate(RectTransform parent)
        {
            var root = BattleUiKit.Rect(parent, "CombatResultView");
            BattleUiKit.Stretch(root);
            var dim = BattleUiKit.Image(root, "Dim", new Color(0f, 0f, 0f, 0.7f));
            BattleUiKit.Stretch(dim.rectTransform);

            var title = BattleUiKit.Text(root, "Title", 44f, TextAlignmentOptions.Center);
            title.text = "패배";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 60f);
            titleRect.sizeDelta = new Vector2(400f, 70f);

            var view = root.gameObject.AddComponent<CombatResultView>();
            view._restartButton = BattleUiKit.LabeledButton(root, "RestartButton", "처음부터", 22f);
            var buttonRect = (RectTransform)view._restartButton.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -40f);
            buttonRect.sizeDelta = new Vector2(200f, 52f);

            root.gameObject.SetActive(false);
            return view;
        }
    }
}
