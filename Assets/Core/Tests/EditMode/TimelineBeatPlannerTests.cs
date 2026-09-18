using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation.Playback;

namespace FateWeaver.Tests
{
    /// <summary>타임라인을 동시 재생 단위인 비트로 나누는 규칙. 코어는 사건을 줄줄이 늘어놓을 뿐
    /// 묶음을 알려주지 않으므로, 재생 계층은 순서로 묶고 SourceId로 검산한다. 코어가 이벤트 순서를
    /// 바꾸면 여기가 먼저 걸린다 — 설계의 「이 선택으로 나중에 어려워지는 것」 3번.</summary>
    public class TimelineBeatPlannerTests
    {
        private static CardResolved Sweep()
            => new CardResolved(7, "goblin", "sweep", Side.Enemy, 9, null);

        [Test]
        public void 광역_피해는_카드와_한_비트가_된다()
        {
            var events = new ResolutionEvent[]
            {
                Sweep(),
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
                new HpChanged("member_b", 18, 14, HpChangeSource.CardDamage, "sweep"),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(1, beats.Count);
            Assert.AreEqual(3, beats[0].Events.Count);
        }

        [Test]
        public void 다음_카드는_새_비트를_연다()
        {
            var events = new ResolutionEvent[]
            {
                Sweep(),
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
                new CardResolved(8, "member_a", "jab", Side.Player, 4, "goblin"),
                new HpChanged("goblin", 10, 6, HpChangeSource.CardDamage, "jab"),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(2, beats.Count);
            Assert.AreEqual(2, beats[0].Events.Count);
            Assert.AreEqual(2, beats[1].Events.Count);
        }

        [Test]
        public void 취소된_카드도_비트를_연다()
        {
            var events = new ResolutionEvent[]
            {
                new CardCancelled(9, "venom_thrust", "member_a", CardCancellationReason.StatusIntercepted),
                new HpChanged("goblin", 2, 0, HpChangeSource.CardDamage, "venom_thrust"),
                new EnemyDied("goblin"),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(1, beats.Count);
            Assert.AreEqual(3, beats[0].Events.Count);
        }

        /// <summary>주인이 죽어 빠진 카드는 자기 차례를 맞은 것이 아니므로 비트를 열지 않고, 주인을 죽인
        /// 카드의 비트에 붙는다(전투 실행 계약 T1). 레일 강조가 그 카드로 튀지 않는 근거다.</summary>
        [Test]
        public void 주인_사망으로_빠진_카드는_죽인_카드의_비트에_붙는다()
        {
            var events = new ResolutionEvent[]
            {
                new CardResolved(1, "member_a", "slash", Side.Player, 9, "goblin", FateWeaver.Core.Conditions.ConditionTier.Basic),
                new HpChanged("goblin", 5, -4, HpChangeSource.CardDamage, "slash"),
                new EnemyDied("goblin"),
                new CardRemoved(2, "goblin_jab", "goblin"),
                new CardResolved(3, "member_a", "guard", Side.Player, 0, null, FateWeaver.Core.Conditions.ConditionTier.Basic),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(2, beats.Count);
            Assert.AreEqual(4, beats[0].Events.Count);
            Assert.IsInstanceOf<CardRemoved>(beats[0].Events[3]);
        }

        [Test]
        public void 카드가_연_비트의_피해는_그_카드가_낸_것이다()
        {
            var events = new ResolutionEvent[]
            {
                Sweep(),
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
                new HpChanged("member_b", 18, 14, HpChangeSource.CardDamage, "sweep"),
            };

            var beat = TimelineBeatPlanner.Plan(events)[0];

            foreach (var hp in beat.Events.OfType<HpChanged>()
                         .Where(e => e.Source == HpChangeSource.CardDamage))
            {
                Assert.AreEqual("sweep", hp.SourceId, "순서로 묶은 비트가 SourceId와 어긋난다");
            }
        }

        [Test]
        public void 턴_종료_틱은_보유자가_달라도_한_비트다()
        {
            var events = new ResolutionEvent[]
            {
                new StatusTicked("goblin", "poison", 3, 3),
                new HpChanged("goblin", 10, 7, HpChangeSource.StatusTick, "poison"),
                new StatusTicked("member_a", "poison", 2, 2),
                new HpChanged("member_a", 15, 13, HpChangeSource.StatusTick, "poison"),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(1, beats.Count);
            Assert.AreEqual(4, beats[0].Events.Count);
        }

        [Test]
        public void 카드_비트는_턴_종료_틱에서_닫힌다()
        {
            var events = new ResolutionEvent[]
            {
                Sweep(),
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
                new StatusTicked("goblin", "poison", 3, 3),
                new HpChanged("goblin", 10, 7, HpChangeSource.StatusTick, "poison"),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(2, beats.Count);
            Assert.AreEqual(2, beats[0].Events.Count);
            Assert.AreEqual(2, beats[1].Events.Count);
        }

        [Test]
        public void 턴_경계는_단독_비트다()
        {
            var events = new ResolutionEvent[]
            {
                new TurnStarted(0),
                Sweep(),
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
                new TurnEnded(0, Outcome.Ongoing),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(3, beats.Count);
            Assert.AreEqual(1, beats[0].Events.Count);
            Assert.AreEqual(2, beats[1].Events.Count);
            Assert.AreEqual(1, beats[2].Events.Count);
        }

        [Test]
        public void 열린_비트가_없으면_단독_비트가_된다()
        {
            var events = new ResolutionEvent[]
            {
                new FateEnergyGained("distill", 2),
                new StatusApplied("member_a", "block", 1, 1, false),
            };

            var beats = TimelineBeatPlanner.Plan(events);

            Assert.AreEqual(2, beats.Count);
        }

        [Test]
        public void 빈_타임라인은_빈_비트_목록이다()
        {
            Assert.AreEqual(0, TimelineBeatPlanner.Plan(new ResolutionEvent[0]).Count);
        }
    }
}
