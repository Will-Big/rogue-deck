using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>보상 후보를 보여주고 고른 번호를 알린다. 후보가 어떻게 뽑혔는지, 선택이 어디
    /// 반영되는지는 모른다(전투 노드 설계 1.6).</summary>
    public sealed class RewardChoiceView : MonoBehaviour
    {
        public const int SlotCount = 3;

        [SerializeField] private RectTransform[] _slots = new RectTransform[SlotCount];
        [SerializeField] private TMP_Text[] _ownerLabels = new TMP_Text[SlotCount];
        [SerializeField] private Button[] _slotButtons = new Button[SlotCount];
        [SerializeField] private Button _skipButton;
        [SerializeField] private Button _nextButton;

        [Tooltip("씬 인스턴스에서 채운다 — 프리팹은 씬 객체를 참조할 수 없다.")]
        [SerializeField] private CardPrefabCatalog _cards;
        [SerializeField] private BattlePresenter _presenter;

        private readonly List<CardView> _spawned = new List<CardView>();

        public bool IsBound
            => _cards != null && _presenter != null && _skipButton != null && _nextButton != null
                && AllSet(_slots) && AllSet(_ownerLabels) && AllSet(_slotButtons);

        public void Show(
            IReadOnlyList<RewardCandidate> candidates,
            Func<string, string> ownerName,
            Action<int> onChoose,
            Action onSkip)
        {
            ClearSpawned();
            gameObject.SetActive(true);
            for (int i = 0; i < SlotCount; i++)
            {
                var hasCandidate = i < candidates.Count;
                _slotButtons[i].onClick.RemoveAllListeners();
                _slotButtons[i].interactable = hasCandidate;
                _slotButtons[i].gameObject.SetActive(hasCandidate);
                _ownerLabels[i].text = hasCandidate ? ownerName(candidates[i].OwnerId) : string.Empty;
                if (!hasCandidate)
                {
                    continue;
                }

                var candidate = candidates[i];
                // Create는 프리팹만 만든다 — 내용은 Bind가 채운다(PileView와 같은 관례). 선택은 슬롯 버튼이 받는다.
                var presentation = _presenter.For(new OwnedCard(candidate.Card, candidate.OwnerId));
                var card = _cards.Create(presentation, _slots[i]);
                card.Bind(presentation, null);
                _spawned.Add(card);
                var index = i;
                _slotButtons[i].onClick.AddListener(() => onChoose(index));
            }

            _skipButton.onClick.RemoveAllListeners();
            _skipButton.onClick.AddListener(() => onSkip());
            _skipButton.gameObject.SetActive(true);
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.gameObject.SetActive(false);
        }

        public void ShowNextButton(Action onNext)
        {
            foreach (var button in _slotButtons)
            {
                button.interactable = false;
            }

            _skipButton.gameObject.SetActive(false);
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.onClick.AddListener(() => onNext());
            _nextButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            ClearSpawned();
            gameObject.SetActive(false);
        }

        private void ClearSpawned()
        {
            foreach (var view in _spawned)
            {
                if (view == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(view.gameObject);
                }
                else
                {
                    DestroyImmediate(view.gameObject);
                }
            }

            _spawned.Clear();
        }

        private static bool AllSet<T>(T[] items) where T : UnityEngine.Object
        {
            if (items == null || items.Length != SlotCount)
            {
                return false;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>씬 빌더가 인스턴스에 씬 참조를 채울 때 쓴다.</summary>
        public void EditorBind(CardPrefabCatalog cards, BattlePresenter presenter)
        {
            _cards = cards;
            _presenter = presenter;
        }

        /// <summary>프리팹 저작용 훅. BattleSceneBuilder가 부른다(FloatingNumberView.EditorCreate와 같은 관례).
        /// 위치·크기·색은 사용자가 조정한다(규칙 17).</summary>
        public static RewardChoiceView EditorCreate(RectTransform parent)
        {
            var root = BattleUiKit.Rect(parent, "RewardChoiceView");
            BattleUiKit.Stretch(root);
            var dim = BattleUiKit.Image(root, "Dim", new Color(0f, 0f, 0f, 0.7f));
            BattleUiKit.Stretch(dim.rectTransform);

            var title = BattleUiKit.Text(root, "Title", 32f, TextAlignmentOptions.Center);
            title.text = "보상";
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(400f, 50f));

            var view = root.gameObject.AddComponent<RewardChoiceView>();
            for (int i = 0; i < SlotCount; i++)
            {
                var x = (i - 1) * 260f;
                var slot = BattleUiKit.Rect(root, "Slot" + i);
                Place(slot, new Vector2(0.5f, 0.5f), new Vector2(x, 40f), new Vector2(220f, 300f));
                view._slots[i] = slot;

                var owner = BattleUiKit.Text(root, "Owner" + i, 18f, TextAlignmentOptions.Center);
                Place(owner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -130f), new Vector2(220f, 30f));
                view._ownerLabels[i] = owner;

                var select = BattleUiKit.LabeledButton(root, "Select" + i, "선택", 20f);
                Place((RectTransform)select.transform, new Vector2(0.5f, 0.5f), new Vector2(x, -175f), new Vector2(140f, 44f));
                view._slotButtons[i] = select;
            }

            view._skipButton = BattleUiKit.LabeledButton(root, "SkipButton", "건너뛰기", 20f);
            Place((RectTransform)view._skipButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(180f, 48f));
            view._nextButton = BattleUiKit.LabeledButton(root, "NextButton", "다음 전투", 20f);
            Place((RectTransform)view._nextButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(180f, 48f));
            view._nextButton.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
            return view;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
