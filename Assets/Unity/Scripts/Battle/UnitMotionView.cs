using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>유닛 하나의 몸짓. 왜 움직이는지도, HP·상태 값도 모른다 — 그것은 UnitView가 안다
    /// (규칙 30). 보간은 전부 DOTween이 한다(규칙 32).
    ///
    /// 움직이는 대상은 유닛 루트가 아니라 초상 RectTransform이다. 루트는 UnitRow의
    /// HorizontalLayoutGroup이 위치를 통제하므로 트윈과 싸운다. 초상만 움직이면 캐릭터가 움직이고
    /// HP바는 제자리에 남아 읽기도 낫다.
    ///
    /// 캐릭터 아트가 스프라이트 애니메이션으로 바뀌면 이 클래스만 Animator 호출로 바뀐다. 연출자는
    /// 그대로다.</summary>
    public sealed class UnitMotionView : MonoBehaviour
    {
        [SerializeField] private RectTransform _body;
        [SerializeField] private Image _bodyImage;

        [Header("전진")]
        [SerializeField] private float _lungeDistance = 42f;
        [SerializeField] private float _lungeDuration = 0.18f;
        [SerializeField] private Ease _lungeEase = Ease.OutQuad;

        [Header("피격")]
        [SerializeField] private float _shakeDuration = 0.25f;
        [SerializeField] private float _shakeStrength = 14f;
        [SerializeField] private int _shakeVibrato = 18;

        [Header("피격 색")]
        [SerializeField] private Color _flashColor = new Color(1f, 0.45f, 0.45f, 1f);
        [SerializeField] private float _flashDuration = 0.09f;

        [Header("사망")]
        [SerializeField] private float _fadeDuration = 0.35f;
        [SerializeField] private float _fadeAlpha = 0.25f;

        public bool IsBound => _body != null && _bodyImage != null;

        /// <summary>프리팹 저작용 배선 훅. UnitView.EditorCreate가 부른다
        /// (ExecutionRailView.EditorBuild와 같은 관례).</summary>
        public void EditorBind(Image body)
        {
            _bodyImage = body;
            _body = body != null ? body.rectTransform : null;
        }

        /// <summary>앞으로 내질렀다 돌아온다. direction은 -1(왼쪽)~+1(오른쪽)이며, 시전자가 어느
        /// 쪽을 향하는지는 연출자가 정한다.</summary>
        public Tween Lunge(float direction)
        {
            if (!IsBound)
            {
                return null;
            }

            var offset = _body.anchoredPosition
                + new Vector2(_lungeDistance * Mathf.Clamp(direction, -1f, 1f), 0f);
            return _body.DOAnchorPos(offset, _lungeDuration * 0.5f)
                .SetEase(_lungeEase)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        /// <summary>맞고 흔들린다. DOTween이 시작 위치를 기억했다가 되돌려 놓는다.</summary>
        public Tween HitShake()
        {
            if (!IsBound)
            {
                return null;
            }

            return _body
                .DOShakeAnchorPos(_shakeDuration, _shakeStrength, _shakeVibrato, 90f, false, true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        /// <summary>피격 색으로 번쩍였다 원래 색으로 돌아온다. 원래 색은 재생 시점의 값이므로
        /// UnitView가 칠한 생존·사망 틴트를 그대로 되찾는다.</summary>
        public Tween Flash()
        {
            if (!IsBound)
            {
                return null;
            }

            return _bodyImage.DOColor(_flashColor, _flashDuration)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        /// <summary>쓰러진다. 되돌아오지 않는 유일한 몸짓이다 — 재생이 끝난 뒤 RefreshAll이 사망
        /// 틴트로 확정한다.</summary>
        public Tween Fade()
        {
            if (!IsBound)
            {
                return null;
            }

            return _bodyImage.DOFade(_fadeAlpha, _fadeDuration)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
    }
}
