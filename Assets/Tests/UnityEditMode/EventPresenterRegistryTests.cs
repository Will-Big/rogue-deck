using System;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using FateWeaver.Unity.Playback;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>이벤트 타입 → 연출자 해결. 미등록은 실패가 아니라 "연출 없음"이므로, 등록을
    /// 빠뜨린 것과 의도적 미등록을 가르는 일은 RegisteredEventTypes 단언이 맡는다.</summary>
    public class EventPresenterRegistryTests
    {
        private sealed class FakePresenter : IResolutionEventPresenter
        {
            public FakePresenter(Type eventType)
            {
                EventType = eventType;
            }

            public Type EventType { get; }

            public PlaybackCue Build(ResolutionEvent evt) => PlaybackCue.None;
        }

        private static CardResolved AnyCard()
            => new CardResolved(1, "goblin", "sweep", Side.Enemy, 4, "member_a");

        [Test]
        public void 등록한_타입의_이벤트를_해결한다()
        {
            var registry = new EventPresenterRegistry();
            var presenter = new FakePresenter(typeof(CardResolved));
            registry.Register(presenter);

            Assert.IsTrue(registry.TryResolve(AnyCard(), out var resolved));
            Assert.AreSame(presenter, resolved);
        }

        [Test]
        public void 미등록_이벤트는_예외가_아니라_해결_실패다()
        {
            var registry = new EventPresenterRegistry();
            registry.Register(new FakePresenter(typeof(CardResolved)));

            Assert.IsFalse(registry.TryResolve(
                new HpChanged("goblin", 10, 6, HpChangeSource.CardDamage, "sweep"),
                out var resolved));
            Assert.IsNull(resolved);
        }

        [Test]
        public void 같은_타입을_두_번_등록하면_예외다()
        {
            var registry = new EventPresenterRegistry();
            registry.Register(new FakePresenter(typeof(CardResolved)));

            Assert.Throws<ArgumentException>(
                () => registry.Register(new FakePresenter(typeof(CardResolved))));
        }

        [Test]
        public void 이벤트가_아닌_타입은_등록할_수_없다()
        {
            var registry = new EventPresenterRegistry();

            Assert.Throws<ArgumentException>(
                () => registry.Register(new FakePresenter(typeof(string))));
        }

        [Test]
        public void 등록되지_않은_연출자는_받지_않는다()
        {
            var registry = new EventPresenterRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<ArgumentException>(() => registry.Register(new FakePresenter(null)));
        }

        [Test]
        public void 등록된_타입_집합을_밝힌다()
        {
            var registry = new EventPresenterRegistry();
            registry.Register(new FakePresenter(typeof(CardResolved)));
            registry.Register(new FakePresenter(typeof(HpChanged)));

            var registered = registry.RegisteredEventTypes.ToArray();

            Assert.AreEqual(2, registered.Length);
            CollectionAssert.Contains(registered, typeof(CardResolved));
            CollectionAssert.Contains(registered, typeof(HpChanged));
        }

        [Test]
        public void 연출_없음_큐는_트윈을_갖지_않는다()
        {
            Assert.IsFalse(PlaybackCue.None.HasTween);
            Assert.IsNull(PlaybackCue.None.Tween);
        }
    }
}
