using System;
using System.Collections.Generic;
using DG.Tweening;
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

        [Tooltip("HP 막대가 새 값까지 흐르는 시간(초).")]
        [SerializeField] private float _hpTweenDuration = 0.25f;

        private static readonly Color HpColor = new Color(0.35f, 0.75f, 0.5f, 1f);
        private static readonly Color DeadTint = new Color(0.35f, 0.35f, 0.35f, 0.5f);

        private Color _aliveTint = Color.white;
        private int _maxHp;
        private int _currentHp;

        public void Bind(string displayName, Color portraitTint)
        {
            _aliveTint = portraitTint;
            _portrait.color = portraitTint;
            _nameText.text = displayName;
        }

        public void SetHp(int current, int max)
        {
            _maxHp = max;
            Render(current);
        }

        /// <summary>HP 막대를 트윈하기 위한 지점. 최대 HP는 마지막 SetHp가 준 값을 쓴다 —
        /// HpChanged 이벤트가 최대 HP를 싣지 않기 때문이다(설계 「어려워지는 것」 4번).</summary>
        public int DisplayedHp
        {
            get => _currentHp;
            set => Render(value);
        }

        /// <summary>HP 막대를 현재 표시값에서 target까지 흐르게 한다. 시작값을 인자로 받지 않고
        /// getter에서 읽으므로, 한 턴에 같은 유닛이 여러 번 맞아도 앞 트윈이 끝난 값에서 이어진다.
        /// 막대가 UnitView의 것이므로 그것이 움직이는 속도도 여기 있다(규칙 8).</summary>
        public Tween TweenHpTo(int target)
            => DOTween.To(() => DisplayedHp, value => DisplayedHp = value, target, _hpTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        private void Render(int current)
        {
            _currentHp = current;
            float t = _maxHp > 0 ? Mathf.Clamp01((float)current / _maxHp) : 0f;
            _hpFill.anchorMin = new Vector2(0f, 0f);
            _hpFill.anchorMax = new Vector2(t, 1f);
            _hpFill.offsetMin = Vector2.zero;
            _hpFill.offsetMax = Vector2.zero;
            _hpText.text = Mathf.Max(0, current) + " / " + _maxHp;
            _portrait.color = current > 0 ? _aliveTint : DeadTint;
        }

        public void SetStatuses(
            IReadOnlyList<StatusInstance> statuses, Func<StatusKey, string> nameFor)
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
                    var name = nameFor(status.Key);
                    parts.Add(value > 0 ? name + "(" + value + ")" : name);
                }
            }

            _statusText.text = string.Join(" · ", parts);
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

            var portrait = BattleUiKit.Image(root, "Portrait", Color.white);
            BattleUiKit.Anchor(portrait.rectTransform, 0f, 0.28f, 1f, 1f);
            portrait.raycastTarget = false;

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

            view._portrait = portrait;
            view._hpFill = hpFill.rectTransform;
            view._hpText = hpText;
            view._nameText = nameText;
            view._statusText = statusText;

            // 몸짓은 루트가 아니라 초상에 건다 — 루트는 UnitRow의 레이아웃 그룹이 통제한다.
            root.gameObject.AddComponent<UnitMotionView>().EditorBind(portrait);
            return view;
        }
    }
}
