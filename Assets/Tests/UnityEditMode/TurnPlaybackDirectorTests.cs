using System;
using System.Reflection;
using DG.Tweening;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using FateWeaver.Unity.Playback;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>비트가 시간축에 올라가는 방식. 비트끼리는 순차, 비트 안은 동시라는 계약이 여기서
    /// 지켜지는지 — 광역 공격이 한 번의 타격으로 보이느냐가 이 배치에 달려 있다.</summary>
    public class TurnPlaybackDirectorTests
    {
        private const float LeadSeconds = 0.2f;
        private const float FollowSeconds = 0.3f;

        private GameObject _root;
        private TurnPlaybackDirector _director;

        /// <summary>지속 시간만 있는 빈 트윈을 돌려주는 연출자. 뷰 없이 배치만 검사한다.</summary>
        private sealed class StubPresenter : IResolutionEventPresenter
        {
            private readonly CueRole _role;
            private readonly float _duration;

            public StubPresenter(Type eventType, CueRole role, float duration)
            {
                EventType = eventType;
                _role = role;
                _duration = duration;
            }

            public Type EventType { get; }

            public PlaybackCue Build(ResolutionEvent evt)
            {
                float value = 0f;
                var tween = DOTween.To(() => value, v => value = v, 1f, _duration);
                return new PlaybackCue(tween, _role);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("DirectorTestRoot");
            _director = _root.AddComponent<TurnPlaybackDirector>();
            SetField(_director, "_beatGap", 0f);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            UnityEngine.Object.DestroyImmediate(_root);
        }

        private static EventPresenterRegistry Registry()
        {
            var registry = new EventPresenterRegistry();
            registry.Register(
                new StubPresenter(typeof(CardResolved), CueRole.Lead, LeadSeconds));
            registry.Register(
                new StubPresenter(typeof(HpChanged), CueRole.Follow, FollowSeconds));
            return registry;
        }

        private static ResolutionEvent[] AreaOfEffectTurn() => new ResolutionEvent[]
        {
            new CardResolved(7, "goblin", "sweep", Side.Enemy, 9, null),
            new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
            new HpChanged("member_b", 18, 14, HpChangeSource.CardDamage, "sweep"),
        };

        [Test]
        public void 빈_타임라인은_즉시_완료된다()
        {
            _director.Initialize(Registry());
            int completed = 0;

            _director.Play(Array.Empty<ResolutionEvent>(), () => completed++);

            Assert.AreEqual(1, completed);
            Assert.IsFalse(_director.IsPlaying);
        }

        [Test]
        public void 연출자가_하나도_없으면_즉시_완료된다()
        {
            _director.Initialize(new EventPresenterRegistry());
            int completed = 0;

            _director.Play(AreaOfEffectTurn(), () => completed++);

            Assert.AreEqual(1, completed);
            Assert.IsFalse(_director.IsPlaying);
        }

        [Test]
        public void 광역_피해는_개시_뒤에_겹쳐_흐른다()
        {
            _director.Initialize(Registry());

            _director.Play(AreaOfEffectTurn(), null);

            // 순차라면 0.2 + 0.3 + 0.3 = 0.8. 동시라면 0.2 + 0.3 = 0.5.
            Assert.AreEqual(
                LeadSeconds + FollowSeconds,
                CurrentRoot().Duration(),
                0.001f,
                "후속 큐 둘이 겹치지 않고 이어 붙었다");
        }

        [Test]
        public void 비트끼리는_순차로_흐른다()
        {
            _director.Initialize(Registry());

            _director.Play(
                new ResolutionEvent[]
                {
                    new CardResolved(7, "goblin", "sweep", Side.Enemy, 4, "member_a"),
                    new HpChanged("member_a", 20, 16, HpChangeSource.CardDamage, "sweep"),
                    new CardResolved(8, "member_a", "jab", Side.Player, 3, "goblin"),
                    new HpChanged("goblin", 12, 9, HpChangeSource.CardDamage, "jab"),
                },
                null);

            Assert.AreEqual(
                (LeadSeconds + FollowSeconds) * 2f,
                CurrentRoot().Duration(),
                0.001f,
                "두 비트가 겹쳤다");
        }

        [Test]
        public void 스킵하면_완료가_정확히_한_번_불린다()
        {
            _director.Initialize(Registry());
            int completed = 0;
            _director.Play(AreaOfEffectTurn(), () => completed++);
            Assert.IsTrue(_director.IsPlaying);

            _director.Skip();

            Assert.AreEqual(1, completed);
            Assert.IsFalse(_director.IsPlaying);
        }

        [Test]
        public void 스킵을_두_번_해도_완료는_한_번이다()
        {
            _director.Initialize(Registry());
            int completed = 0;
            _director.Play(AreaOfEffectTurn(), () => completed++);

            _director.Skip();
            _director.Skip();

            Assert.AreEqual(1, completed);
        }

        [Test]
        public void 배속은_루트_시퀀스에_걸린다()
        {
            _director.Initialize(Registry());
            _director.Play(AreaOfEffectTurn(), null);

            _director.Speed = 2f;

            Assert.AreEqual(2f, CurrentRoot().timeScale, 0.001f);
            Assert.AreEqual(2f, _director.Speed, 0.001f);
        }

        [Test]
        public void 배속은_0_이하로_내려가지_않는다()
        {
            _director.Speed = 0f;

            Assert.Greater(_director.Speed, 0f);
        }

        [Test]
        public void 새_재생은_앞_재생을_대신한다()
        {
            _director.Initialize(Registry());
            int first = 0;
            int second = 0;
            _director.Play(AreaOfEffectTurn(), () => first++);

            _director.Play(AreaOfEffectTurn(), () => second++);
            _director.Skip();

            Assert.AreEqual(0, first, "앞 재생의 완료 콜백이 뒤늦게 불렸다");
            Assert.AreEqual(1, second);
        }

        [Test]
        public void 레지스트리_없이는_재생할_수_없다()
        {
            Assert.Throws<ArgumentNullException>(() => _director.Initialize(null));
        }

        private Sequence CurrentRoot()
            => (Sequence)typeof(TurnPlaybackDirector)
                .GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_director);

        private static void SetField(object target, string fieldName, object value)
            => target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
