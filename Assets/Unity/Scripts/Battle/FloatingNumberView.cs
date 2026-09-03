using DG.Tweening;
using TMPro;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>숫자 하나를 띄우고 사라진다. 그 숫자가 무엇을 뜻하는지는 모른다 — 부호만 본다.
    ///
    /// 부동 전투 텍스트에 해당하는 Unity 내장 컴포넌트가 없어 TextMeshPro와 DOTween으로 조립한다
    /// (규칙 32의 「도구 선택」 표).</summary>
    public sealed class FloatingNumberView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private CanvasGroup _group;

        [Header("움직임")]
        [SerializeField] private float _riseDistance = 64f;
        [SerializeField] private float _duration = 0.6f;
        [SerializeField] private Ease _riseEase = Ease.OutCubic;
        [SerializeField] private float _fadeStartRatio = 0.45f;

        [Header("색")]
        [SerializeField] private Color _damageColor = new Color(0.94f, 0.42f, 0.42f, 1f);
        [SerializeField] private Color _healColor = new Color(0.42f, 0.85f, 0.55f, 1f);

        public bool IsBound => _label != null && _group != null;

        /// <summary>delta가 음수면 피해, 양수면 회복이다. 구분은 색이 맡고 숫자는 언제나 양수로
        /// 적는다 — 피해에 붙는 빼기표는 "HP가 줄었다"를 두 번 말한다. 다 뜨고 나면 스스로
        /// 사라진다.</summary>
        public Tween Play(int delta)
        {
            if (!IsBound)
            {
                return null;
            }

            _label.text = Mathf.Abs(delta).ToString();
            _label.color = delta > 0 ? _healColor : _damageColor;
            _group.alpha = 1f;

            var rect = (RectTransform)transform;
            var target = rect.anchoredPosition + new Vector2(0f, _riseDistance);

            return DOTween.Sequence()
                .Append(rect.DOAnchorPos(target, _duration).SetEase(_riseEase))
                .Insert(
                    _duration * _fadeStartRatio,
                    _group.DOFade(0f, _duration * (1f - _fadeStartRatio)))
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(DestroySelf);
        }

        /// <summary>EditMode 테스트에서는 Destroy가 금지되어 에러 로그를 남긴다. 재생 계층을
        /// 에디터 없이 검증하려면 이 갈래가 필요하다.</summary>
        private void DestroySelf()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        /// <summary>프리팹 저작용 훅. BattleSceneBuilder가 부른다(UnitView.EditorCreate와 같은 관례).</summary>
        public static FloatingNumberView EditorCreate(RectTransform parent, Vector2 size)
        {
            var root = BattleUiKit.Rect(parent, "FloatingNumber");
            root.sizeDelta = size;

            var view = root.gameObject.AddComponent<FloatingNumberView>();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var label = BattleUiKit.Text(root, "Label", 30f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(label.rectTransform);
            label.raycastTarget = false;
            label.fontStyle = FontStyles.Bold;

            view._label = label;
            view._group = group;
            return view;
        }
    }
}
