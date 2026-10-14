using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Simulation.Descriptions;
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

        [Tooltip("쓰러진 유닛의 초상 색.")]
        [SerializeField] private Color _deadTint = new Color(0.35f, 0.35f, 0.35f, 0.5f);

        private Color _aliveTint = Color.white;
        private int _maxHp;
        private int _currentHp;

        private GameLocalization _localization;
        private LocalizedTextRequest _nameRequest;
        private readonly List<(StatusKey Key, int Value)> _lastStatuses = new List<(StatusKey, int)>();
        private Func<StatusKey, string> _lastNames;
        public void Localize(GameLocalization localization, LocalizedTextRequest name)
        {
            if (_localization != null) _localization.Changed -= RefreshLanguage;
            _localization = localization; _nameRequest = name;
            if (_localization != null) { _localization.Changed += RefreshLanguage; RefreshLanguage(); }
        }
        private void OnDestroy() { if (_localization != null) _localization.Changed -= RefreshLanguage; }
        private void RefreshLanguage()
        {
            _nameText.text = _localization.Render(_nameRequest);
            if (_lastNames != null) RenderStatuses();
        }

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
        /// 막대가 UnitView의 것이므로 그것이 움직이는 속도도 여기 있다.</summary>
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
            _portrait.color = current > 0 ? _aliveTint : _deadTint;
        }

        public void SetStatuses(
            IReadOnlyList<StatusInstance> statuses, Func<StatusKey, string> nameFor)
        {
            if (_statusText == null)
            {
                return;
            }

            _lastNames = nameFor;
            _lastStatuses.Clear();
            if (statuses != null) foreach (var status in statuses)
                _lastStatuses.Add((status.Key, status.Magnitude > 0 ? status.Magnitude : status.Count));
            RenderStatuses();
        }
        private void RenderStatuses()
        {
            if (_statusText == null || _lastNames == null) return;
            var parts = _lastStatuses.Select(status => status.Value > 0
                ? _lastNames(status.Key) + "(" + status.Value + ")" : _lastNames(status.Key));
            _statusText.text = string.Join(" · ", parts);
        }
    }
}
