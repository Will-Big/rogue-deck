using System;
using System.Collections.Generic;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Unity;
using FateWeaver.Unity.Playback;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>id를 화면으로 바꾸는 유일한 지점. 코어 이벤트는 좌표를 싣지 않으므로(규칙 11)
    /// 재생 계층이 뷰에 닿는 경로가 여기 하나뿐인지, 모르는 id에 안전한지를 잠근다.</summary>
    public class BattleStageTests
    {
        private GameObject _root;
        private BattleUnitsView _units;
        private BattleStage _stage;
        private DeckCombatSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("BattleStageTestRoot", typeof(RectTransform));
            var partyRow = ChildRect("PartyRow");
            var enemyRow = ChildRect("EnemyRow");
            var numberLayer = ChildRect("NumberLayer");
            var prefabRoot = ChildRect("PrefabRoot");

            var unitPrefab = UnitView.EditorCreate(prefabRoot, new Vector2(180f, 250f));
            var numberPrefab = FloatingNumberView.EditorCreate(prefabRoot, new Vector2(120f, 48f));

            _session = new DeckCombatSession(
                UnityTestContent.Statuses(),
                new[] { Loadout("a", "Member A", 25) },
                new[] { new Enemy("goblin", 12) },
                new SequencePolicy(new[] { (IReadOnlyList<CardDefinition>)Array.Empty<CardDefinition>() }),
                new PartyTuning
                {
                    DefaultMemberMaxHp = 25,
                    SurviveChargesPerCombat = 0,
                    DrawByLivingCount = new Dictionary<int, int> { { 1, 1 } }
                });

            _units = _root.AddComponent<BattleUnitsView>();
            SetField(_units, "_unitPrefab", unitPrefab);
            SetField(_units, "_playerUnitsRow", partyRow);
            SetField(_units, "_enemyUnitsRow", enemyRow);
            _units.Spawn(_session.State, _ => Color.white, id => id, key => key.Id);

            _stage = _root.AddComponent<BattleStage>();
            _stage.EditorBind(_units, numberPrefab, numberLayer);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_root);

        [Test]
        public void 파티원과_적_모두_id로_찾힌다()
        {
            Assert.IsNotNull(_stage.UnitOf("a"));
            Assert.IsNotNull(_stage.UnitOf("goblin"));
            Assert.IsNotNull(_stage.MotionOf("a"));
            Assert.IsNotNull(_stage.MotionOf("goblin"));
            Assert.IsNotNull(_stage.AnchorOf("a"));
        }

        [Test]
        public void 모르는_id는_예외가_아니라_null이다()
        {
            Assert.IsNull(_stage.UnitOf("없는놈"));
            Assert.IsNull(_stage.MotionOf("없는놈"));
            Assert.IsNull(_stage.AnchorOf("없는놈"));
            Assert.IsNull(_stage.UnitOf(null));
            Assert.IsNull(_stage.MotionOf(null));
        }

        [Test]
        public void 최대_HP는_스폰_시점의_값이다()
        {
            Assert.AreEqual(25, _stage.MaxHpOf("a"));
            Assert.AreEqual(12, _stage.MaxHpOf("goblin"));

            // 전투 중 HP가 깎여도 최대 HP는 그대로여야 한다 — HpChanged가 최대 HP를 싣지 않으므로
            // HP바 비율의 분모는 이 스냅샷뿐이다.
            _session.State.Enemies[0].Hp = 3;
            _units.Refresh(_session.State);

            Assert.AreEqual(12, _stage.MaxHpOf("goblin"));
        }

        [Test]
        public void 모르는_id의_최대_HP는_0이다()
        {
            Assert.AreEqual(0, _stage.MaxHpOf("없는놈"));
            Assert.AreEqual(0, _stage.MaxHpOf(null));
        }

        [Test]
        public void 숫자는_앵커_위치에_만들어진다()
        {
            var anchor = _stage.AnchorOf("goblin");

            var number = _stage.SpawnNumber(anchor);

            Assert.IsNotNull(number);
            Assert.IsTrue(number.IsBound);
            Assert.AreEqual(anchor.position, number.transform.position);
        }

        [Test]
        public void 앵커가_없으면_숫자를_만들지_않는다()
        {
            Assert.IsNull(_stage.SpawnNumber(null));
        }

        [Test]
        public void HP_막대는_트윈할_수_있게_열려_있다()
        {
            var view = _stage.UnitOf("goblin");
            view.SetHp(12, 12);

            view.DisplayedHp = 5;

            Assert.AreEqual(5, view.DisplayedHp);
        }

        [Test]
        public void 유닛_프리팹에_몸짓이_배선되어_있다()
        {
            Assert.IsTrue(_stage.MotionOf("a").IsBound);
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
