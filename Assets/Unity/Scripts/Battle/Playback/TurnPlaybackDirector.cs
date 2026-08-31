using System;
using System.Collections.Generic;
using DG.Tweening;
using FateWeaver.Core.Events;
using FateWeaver.Simulation.Playback;
using UnityEngine;

namespace FateWeaver.Unity.Playback
{
    /// <summary>비트를 순서대로 재생하고 배속·스킵을 관장한다. 이벤트가 무엇을 뜻하는지도, 어떤
    /// 뷰가 있는지도, 코어 상태도 모른다 — 비트는 TimelineBeatPlanner가 나누고, 연출은 레지스트리에
    /// 등록된 연출자가 만든다.
    ///
    /// 비트끼리는 순차, 비트 안은 동시다. 비트 하나에서 개시 큐를 차례로 이어 붙인 뒤 후속 큐들을
    /// 그 뒤에 겹쳐 넣는다 — 이것이 광역 공격을 "시전 한 번 → 여러 명이 동시에 피격"으로 만든다.
    ///
    /// 배속과 스킵이 루트 시퀀스 한 곳에서만 제어되므로 연출자는 둘 다 모른다. 그 대가로 연출자는
    /// 최종 상태를 트윈의 끝값이나 완료 콜백으로 표현해야 한다 — 임의 시점의 일회성 부수효과로
    /// 쓰면 스킵 후 화면이 어긋난다(설계 「어려워지는 것」 2번).</summary>
    public sealed class TurnPlaybackDirector : MonoBehaviour
    {
        [Tooltip("비트와 비트 사이의 쉼(초).")]
        [SerializeField] private float _beatGap = 0.08f;

        [Tooltip("재생 배속. 1이 등속이다.")]
        [SerializeField] private float _speed = 1f;

        private EventPresenterRegistry _registry;
        private Sequence _root;
        private Action _onComplete;
        private bool _completionSent;

        public bool IsPlaying => _root != null && _root.IsActive();

        /// <summary>루트 시퀀스 하나에만 걸린다. 연출자는 배속을 모른다.</summary>
        public float Speed
        {
            get => _speed;
            set
            {
                _speed = Mathf.Max(0.01f, value);
                if (_root != null && _root.IsActive())
                {
                    _root.timeScale = _speed;
                }
            }
        }

        public void Initialize(EventPresenterRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>타임라인을 재생한다. 그릴 것이 하나도 없으면 이번 프레임에 바로 완료를 알린다 —
        /// 호출자는 어느 쪽이든 완료 콜백만 기다리면 된다.</summary>
        public void Play(IReadOnlyList<ResolutionEvent> timeline, Action onComplete)
        {
            StopCurrent();
            _onComplete = onComplete;
            _completionSent = false;

            var root = BuildRoot(timeline);
            if (root == null)
            {
                Finish();
                return;
            }

            _root = root;
            _root.timeScale = _speed;
            _root.SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(Finish)
                .OnKill(Finish);
        }

        /// <summary>남은 연출을 즉시 끝낸다. 완료 콜백까지 태우므로 화면은 최종 상태로 확정된다.</summary>
        public void Skip()
        {
            if (_root != null && _root.IsActive())
            {
                _root.Complete(true);
            }
        }

        private Sequence BuildRoot(IReadOnlyList<ResolutionEvent> timeline)
        {
            if (_registry == null || timeline == null || timeline.Count == 0)
            {
                return null;
            }

            Sequence root = null;
            foreach (var beat in TimelineBeatPlanner.Plan(timeline))
            {
                var beatSequence = BuildBeat(beat);
                if (beatSequence == null)
                {
                    continue;
                }

                if (root == null)
                {
                    root = DOTween.Sequence();
                }
                else if (_beatGap > 0f)
                {
                    root.AppendInterval(_beatGap);
                }

                root.Append(beatSequence);
            }

            return root;
        }

        /// <summary>비트 하나. 개시 큐들이 함께 흐른 뒤, 후속 큐들이 그 지점에서 함께 시작한다.
        /// 개시가 서로 나란한 이유는 한 비트의 원인이 하나이기 때문이다 — 시전자의 몸짓과 그 카드의
        /// 아웃라인은 같은 사건의 두 얼굴이지 순서가 있는 두 사건이 아니다.
        ///
        /// 각 무리의 첫 큐만 Append하고 나머지를 Join하는 것이 "동시"를 만드는 지점이다. 전부
        /// Join하면 후속까지 개시와 같은 시점에서 시작하고, 전부 Append하면 무엇도 겹치지 않는다.</summary>
        private Sequence BuildBeat(PlaybackBeat beat)
        {
            List<Tween> leads = null;
            List<Tween> follows = null;

            foreach (var evt in beat.Events)
            {
                foreach (var presenter in _registry.PresentersFor(evt))
                {
                    var cue = presenter.Build(evt);
                    if (!cue.HasTween)
                    {
                        continue;
                    }

                    if (cue.Role == CueRole.Lead)
                    {
                        leads = leads ?? new List<Tween>();
                        leads.Add(cue.Tween);
                    }
                    else
                    {
                        follows = follows ?? new List<Tween>();
                        follows.Add(cue.Tween);
                    }
                }
            }

            if (leads == null && follows == null)
            {
                return null;
            }

            var sequence = DOTween.Sequence();
            AppendTogether(sequence, leads);
            AppendTogether(sequence, follows);
            return sequence;
        }

        /// <summary>무리 하나를 앞 무리가 끝난 시점에 통째로 얹는다. 첫 큐가 시점을 정하고 나머지가
        /// 거기에 붙는다.</summary>
        private static void AppendTogether(Sequence sequence, List<Tween> group)
        {
            if (group == null)
            {
                return;
            }

            for (int i = 0; i < group.Count; i++)
            {
                if (i == 0)
                {
                    sequence.Append(group[i]);
                }
                else
                {
                    sequence.Join(group[i]);
                }
            }
        }

        private void StopCurrent()
        {
            if (_root == null)
            {
                return;
            }

            var stopping = _root;
            _root = null;
            _completionSent = true;
            if (stopping.IsActive())
            {
                stopping.Kill();
            }
        }

        /// <summary>OnComplete와 OnKill 양쪽에서 불리므로 한 번만 통과시킨다
        /// (ExecutionRailView의 completionSent 관례).</summary>
        private void Finish()
        {
            if (_completionSent)
            {
                return;
            }

            _completionSent = true;
            _root = null;
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        private void OnDestroy() => StopCurrent();
    }
}
