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

            var resolved = registry.PresentersFor(AnyCard());

            Assert.AreEqual(1, resolved.Count);
            Assert.AreSame(presenter, resolved[0]);
        }

        [Test]
        public void 미등록_이벤트는_예외가_아니라_빈_목록이다()
        {
            var registry = new EventPresenterRegistry();
            registry.Register(new FakePresenter(typeof(CardResolved)));

            Assert.AreEqual(
                0,
                registry.PresentersFor(
                    new HpChanged("goblin", 10, 6, HpChangeSource.CardDamage, "sweep")).Count);
            Assert.AreEqual(0, registry.PresentersFor(null).Count);
        }

        [Test]
        public void 한_이벤트에_연출자를_여럿_붙일_수_있다()
        {
            // 카드가 해결되면 시전자가 움직이고(유닛) 그 카드에 아웃라인이 켜진다(레일). 같은
            // 사건의 두 얼굴이므로 한 연출자에 몰지 않는다.
            var registry = new EventPresenterRegistry();
            var first = new FakePresenter(typeof(CardResolved));
            var second = new FakePresenter(typeof(CardResolved));
            registry.Register(first);
            registry.Register(second);

            var resolved = registry.PresentersFor(AnyCard());

            Assert.AreEqual(2, resolved.Count);
            Assert.AreSame(first, resolved[0], "등록 순서가 유지되지 않았다");
            Assert.AreSame(second, resolved[1]);
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
