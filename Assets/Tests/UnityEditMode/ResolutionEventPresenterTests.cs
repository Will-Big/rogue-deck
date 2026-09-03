using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Unity;
using FateWeaver.Unity.Playback;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>이벤트 하나가 큐 하나로 바뀌는지. 역할(개시/후속)이 비트 안의 배치를 정하므로
    /// 광역 공격이 한 번의 타격으로 보이는지가 여기서 갈린다.</summary>
    public class ResolutionEventPresenterTests
    {
        private GameObject _root;
        private BattleStage _stage;
        private DeckCombatSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PresenterTestRoot", typeof(RectTransform));
            var partyRow = ChildRect("PartyRow");
            var enemyRow = ChildRect("EnemyRow");
            var numberLayer = ChildRect("NumberLayer");
            var prefabRoot = ChildRect("PrefabRoot");

            var unitPrefab = UnitView.EditorCreate(prefabRoot, new Vector2(180f, 250f));
            var numberPrefab = FloatingNumberView.EditorCreate(prefabRoot, new Vector2(120f, 48f));

            _session = new DeckCombatSession(
                UnityTestContent.Statuses(),
                new[] { Loadout("member_a", "A", 20), Loadout("member_b", "B", 18) },
                new[] { new Enemy("goblin", 12) },
                new EnemyIntent(new[] { (IReadOnlyList<CardDefinition>)Array.Empty<CardDefinition>() }),
                new PartyTuning
                {
                    DefaultMemberMaxHp = 20,
                    SurviveChargesPerCombat = 0,
                    DrawByLivingCount = new Dictionary<int, int> { { 1, 1 }, { 2, 1 } }
                });

            var units = _root.AddComponent<BattleUnitsView>();
            SetField(units, "_unitPrefab", unitPrefab);
            SetField(units, "_playerUnitsRow", partyRow);
            SetField(units, "_enemyUnitsRow", enemyRow);
            units.Spawn(_session.State, _ => Color.white, id => id, key => key.Id);

            _stage = _root.AddComponent<BattleStage>();
            _stage.EditorBind(units, numberPrefab, numberLayer);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void 카드_해결은_개시_큐다()
        {
            var presenter = new CardResolvedPresenter(_stage);

            var cue = presenter.Build(
                new CardResolved(7, "goblin", "sweep", Side.Enemy, 9, null));

            Assert.IsTrue(cue.HasTween);
            Assert.AreEqual(CueRole.Lead, cue.Role);
        }

        [Test]
        public void HP_변화는_후속_큐다()
        {
            var presenter = new HpChangedPresenter(_stage);

            var cue = presenter.Build(
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"));

            Assert.IsTrue(cue.HasTween);
            Assert.AreEqual(CueRole.Follow, cue.Role);
        }

        [Test]
        public void 소유자_뷰가_없으면_연출하지_않는다()
        {
            var presenter = new CardResolvedPresenter(_stage);

            var cue = presenter.Build(
                new CardResolved(7, "없는놈", "sweep", Side.Enemy, 9, null));

            Assert.IsFalse(cue.HasTween);
        }

        [Test]
        public void 보유자_뷰가_없으면_연출하지_않는다()
        {
            var presenter = new HpChangedPresenter(_stage);

            var cue = presenter.Build(
                new HpChanged("없는놈", 20, 15, HpChangeSource.CardDamage, "sweep"));

            Assert.IsFalse(cue.HasTween);
        }

        [Test]
        public void 다른_타입의_이벤트는_연출하지_않는다()
        {
            var cardPresenter = new CardResolvedPresenter(_stage);
            var hpPresenter = new HpChangedPresenter(_stage);

            Assert.IsFalse(cardPresenter.Build(new TurnStarted(0)).HasTween);
            Assert.IsFalse(hpPresenter.Build(new TurnStarted(0)).HasTween);
        }

        [Test]
        public void HP_막대는_이벤트의_After까지_흐른다()
        {
            var unit = _stage.UnitOf("member_a");
            unit.SetHp(20, 20);
            var presenter = new HpChangedPresenter(_stage);

            var cue = presenter.Build(
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"));
            cue.Tween.Complete(true);

            Assert.AreEqual(15, unit.DisplayedHp);
        }

        [Test]
        public void 광역_피해는_두_유닛이_각자의_후속_큐를_받는다()
        {
            var a = _stage.UnitOf("member_a");
            var b = _stage.UnitOf("member_b");
            a.SetHp(20, 20);
            b.SetHp(18, 18);
            var presenter = new HpChangedPresenter(_stage);

            var first = presenter.Build(
                new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"));
            var second = presenter.Build(
                new HpChanged("member_b", 18, 14, HpChangeSource.CardDamage, "sweep"));

            Assert.AreEqual(CueRole.Follow, first.Role);
            Assert.AreEqual(CueRole.Follow, second.Role);

            first.Tween.Complete(true);
            second.Tween.Complete(true);

            Assert.AreEqual(15, a.DisplayedHp);
            Assert.AreEqual(14, b.DisplayedHp);
        }

        [Test]
        public void 스테이지_없이는_연출자를_만들_수_없다()
        {
            Assert.Throws<ArgumentNullException>(() => new CardResolvedPresenter(null));
            Assert.Throws<ArgumentNullException>(() => new HpChangedPresenter(null));
        }

        [Test]
        public void 연출자는_자기_이벤트_타입을_밝힌다()
        {
            Assert.AreEqual(typeof(CardResolved), new CardResolvedPresenter(_stage).EventType);
            Assert.AreEqual(typeof(HpChanged), new HpChangedPresenter(_stage).EventType);
        }

        private static PartyMemberLoadout Loadout(string id, string name, int maxHp)
            => new PartyMemberLoadout(id, name, maxHp, Array.Empty<CardDefinition>());

        private RectTransform ChildRect(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(_root.transform, false);
            return (RectTransform)child.transform;
        }

        private static void SetField(object target, string fieldName, object value)
            => target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
