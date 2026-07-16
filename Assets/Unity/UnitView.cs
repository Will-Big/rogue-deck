using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>One combatant on the stage: placeholder portrait + a per-unit HP bar anchored below it.
    /// Both sides can field several units, so HP never lives in a shared top HUD (spec §2).
    /// Portrait art/sprites land in later phases.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private RectTransform _hpFill;
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _targetHighlight;
        [SerializeField] private GameObject _targetDim;
        [SerializeField] private Button _targetButton;

        private static readonly Color HpColor = new Color(0.35f, 0.75f, 0.5f, 1f);
        private static readonly Color DeadTint = new Color(0.35f, 0.35f, 0.35f, 0.5f);
        private static readonly Color TargetCandidate =
            new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color TargetSelected =
            new Color(0.35f, 0.75f, 0.95f, 1f);

        private Color _aliveTint = Color.white;
        private string _memberId;

        public void Bind(string displayName, Color portraitTint)
        {
            _aliveTint = portraitTint;
            _portrait.color = portraitTint;
            _nameText.text = displayName;
        }

        public void SetHp(int current, int max)
        {
            float t = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            _hpFill.anchorMin = new Vector2(0f, 0f);
            _hpFill.anchorMax = new Vector2(t, 1f);
            _hpFill.offsetMin = Vector2.zero;
            _hpFill.offsetMax = Vector2.zero;
            _hpText.text = Mathf.Max(0, current) + " / " + max;
            _portrait.color = current > 0 ? _aliveTint : DeadTint;
            if (current <= 0)
            {
                SetTargetable(false);
            }
        }

        public void BindTarget(string memberId, Action<string> onClick)
        {
            _memberId = memberId;
            if (_targetButton == null)
            {
                return;
            }

            _targetButton.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                _targetButton.onClick.AddListener(() => onClick(_memberId));
            }
        }

        public void SetStatuses(IReadOnlyList<StatusInstance> statuses)
        {
            if (_statusText == null)
            {
                return;
            }

            var parts = new List<string>();
            if (statuses != null)
            {
                foreach (var status in statuses)
                {
                    int value = status.Magnitude > 0 ? status.Magnitude : status.Count;
                    var name = PlaytestKoreanText.StatusName(status.Key);
                    parts.Add(value > 0 ? name + "(" + value + ")" : name);
                }
            }

            _statusText.text = string.Join(" · ", parts);
        }

        public void SetTargetable(bool value)
        {
            SetTargetSelection(value, value, false);
        }

        public void SetTargetSelection(bool active, bool candidate, bool selected)
        {
            _targetDim.SetActive(active && !candidate);
            _targetHighlight.SetActive(active && candidate);
            _targetHighlight.GetComponent<Image>().color =
                selected ? TargetSelected : TargetCandidate;
            _targetButton.interactable = active && candidate;
        }

        /// <summary>Editor-only prefab authoring hook used by BattleSceneBuilder.</summary>
        public static UnitView EditorCreate(RectTransform parent, Vector2 size)
        {
            var root = BattleUiKit.Rect(parent, "Unit");
            root.sizeDelta = size;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            var view = root.gameObject.AddComponent<UnitView>();

            var targetHighlight = BattleUiKit.Image(root, "TargetHighlight", new Color(0.95f, 0.72f, 0.25f, 0.9f));
            BattleUiKit.Anchor(targetHighlight.rectTransform, -0.02f, 0.26f, 1.02f, 1.02f);
            targetHighlight.raycastTarget = false;

            var portrait = BattleUiKit.Image(root, "Portrait", Color.white);
            BattleUiKit.Anchor(portrait.rectTransform, 0f, 0.28f, 1f, 1f);
            portrait.raycastTarget = true;
            var targetButton = root.gameObject.AddComponent<Button>();
            targetButton.targetGraphic = portrait;

            var hpBack = BattleUiKit.Image(root, "HpBack", new Color(0f, 0f, 0f, 0.55f));
            BattleUiKit.Anchor(hpBack.rectTransform, 0.05f, 0.16f, 0.95f, 0.26f);
            hpBack.raycastTarget = false;

            var hpFill = BattleUiKit.Image(hpBack.rectTransform, "HpFill", HpColor);
            BattleUiKit.Stretch(hpFill.rectTransform);
            hpFill.raycastTarget = false;

            var hpText = BattleUiKit.Text(hpBack.rectTransform, "HpText", 14f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(hpText.rectTransform);

            var nameText = BattleUiKit.Text(root, "Name", 16f, TextAlignmentOptions.Center);
            BattleUiKit.Anchor(nameText.rectTransform, 0f, 0f, 1f, 0.10f);

            var statusText = BattleUiKit.Text(root, "Statuses", 13f, TextAlignmentOptions.Center);
            BattleUiKit.Anchor(statusText.rectTransform, 0f, 0.10f, 1f, 0.16f);

            var targetDim = BattleUiKit.Image(root, "TargetDim", new Color(0f, 0f, 0f, 0.55f));
            BattleUiKit.Stretch(targetDim.rectTransform);
            targetDim.raycastTarget = false;

            view._portrait = portrait;
            view._hpFill = hpFill.rectTransform;
            view._hpText = hpText;
            view._nameText = nameText;
            view._statusText = statusText;
            view._targetHighlight = targetHighlight.gameObject;
            view._targetDim = targetDim.gameObject;
            view._targetButton = targetButton;
            targetDim.gameObject.SetActive(false);
            view.SetTargetable(false);
            return view;
        }
    }
}
