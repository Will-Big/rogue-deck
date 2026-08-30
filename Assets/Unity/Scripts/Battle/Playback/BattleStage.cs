using UnityEngine;

namespace FateWeaver.Unity.Playback
{
    /// <summary>id로 뷰와 화면 앵커를 찾아준다. 이벤트가 무엇인지도, 시간축도 모른다.
    ///
    /// 이 계층의 요점 하나가 여기 있다 — 코어 이벤트는 "누가·누구에게·얼마나"만 말하고 좌표를
    /// 싣지 않는다(규칙 11). 좌표는 Unity 런타임 레이아웃의 것이며 그것을 아는 객체는 이 하나다.
    /// 연출자는 BattleStage를 거치지 않고는 어떤 뷰에도 닿지 못한다.</summary>
    public sealed class BattleStage : MonoBehaviour
    {
        [SerializeField] private BattleUnitsView _units;
        [SerializeField] private FloatingNumberView _floatingNumberPrefab;

        [Tooltip("떠오르는 숫자가 붙을 오버레이. 유닛 위에 그려져야 한다.")]
        [SerializeField] private RectTransform _numberLayer;

        public bool IsBound => _units != null
            && _floatingNumberPrefab != null && _numberLayer != null;

        /// <summary>몸짓을 맡길 컴포넌트. 모르는 id는 null이며, 연출자는 그때 빈 큐를 돌려준다.</summary>
        public UnitMotionView MotionOf(string holderId)
            => _units != null && _units.TryGetUnit(holderId, out var view)
                ? view.GetComponent<UnitMotionView>()
                : null;

        /// <summary>HP 막대를 가진 뷰.</summary>
        public UnitView UnitOf(string holderId)
            => _units != null && _units.TryGetUnit(holderId, out var view) ? view : null;

        /// <summary>숫자를 띄울 화면 위치.</summary>
        public RectTransform AnchorOf(string holderId)
        {
            var view = UnitOf(holderId);
            return view != null ? (RectTransform)view.transform : null;
        }

        /// <summary>스폰 시점의 최대 HP. HpChanged가 싣지 않으므로 여기서 얻는다.</summary>
        public int MaxHpOf(string holderId) => _units != null ? _units.MaxHpOf(holderId) : 0;

        /// <summary>떠오르는 숫자 하나를 만든다. 규칙 1대로 프리팹을 인스턴스화하며, 다 뜨고 나면
        /// 스스로 사라진다. 앵커가 없으면 아무것도 만들지 않는다.</summary>
        public FloatingNumberView SpawnNumber(RectTransform anchor)
        {
            if (anchor == null || _floatingNumberPrefab == null || _numberLayer == null)
            {
                return null;
            }

            var number = Instantiate(_floatingNumberPrefab, _numberLayer);
            var rect = (RectTransform)number.transform;
            rect.position = anchor.position;
            return number;
        }

        /// <summary>씬 저작용 배선 훅(BattleSceneBuilder가 부른다).</summary>
        public void EditorBind(
            BattleUnitsView units, FloatingNumberView numberPrefab, RectTransform numberLayer)
        {
            _units = units;
            _floatingNumberPrefab = numberPrefab;
            _numberLayer = numberLayer;
        }
    }
}
