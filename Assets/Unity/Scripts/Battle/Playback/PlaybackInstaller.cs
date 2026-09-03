using UnityEngine;

namespace FateWeaver.Unity.Playback
{
    /// <summary>재생 계층의 조립을 한 곳에서 한다. 어떤 이벤트에 어떤 연출자가 붙는지를 아는
    /// 유일한 객체이며, 새 연출을 더할 때 손대는 곳도 여기 하나다(규칙 9).
    ///
    /// 별도 객체인 이유는 둘이다. 디렉터가 연출자 타입을 알면 "이벤트의 의미를 모른다"는 계약이
    /// 깨지고, 컨트롤러가 알면 조정자가 조립까지 하게 된다(규칙 30). 조립은 조립만 하는 객체의
    /// 몫이다.</summary>
    public sealed class PlaybackInstaller : MonoBehaviour
    {
        [SerializeField] private BattleStage _stage;
        [SerializeField] private TurnPlaybackDirector _director;
        [SerializeField] private ExecutionRailView _rail;

        public bool IsBound => _stage != null && _director != null && _rail != null;

        private void Awake() => Install();

        public void Install()
        {
            if (!IsBound)
            {
                Debug.LogError("재생 계층 배선이 비어 있습니다.");
                return;
            }

            var registry = new EventPresenterRegistry();
            registry.Register(new CardResolvedPresenter(_stage));
            registry.Register(new HpChangedPresenter(_stage));
            registry.Register(
                new RailCardHighlightPresenter(_rail, typeof(FateWeaver.Core.Events.CardResolved)));
            registry.Register(
                new RailCardHighlightPresenter(_rail, typeof(FateWeaver.Core.Events.CardCancelled)));
            _director.Initialize(registry);
        }

        /// <summary>씬 저작용 배선 훅(BattleSceneBuilder가 부른다).</summary>
        public void EditorBind(
            BattleStage stage, TurnPlaybackDirector director, ExecutionRailView rail)
        {
            _stage = stage;
            _director = director;
            _rail = rail;
        }
    }
}
