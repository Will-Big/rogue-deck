using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>진형이 바뀌어도 유닛 뷰가 재사용되고 내용이 따라오는지 잠근다. 대상은
    /// BattleUnitsView다 — 스폰·갱신이 컨트롤러를 떠났다(설계 §4.6).</summary>
    public class BattleUnitsViewIdentityTests
    {
        private GameObject _root;
        private RectTransform _partyRow;
        private RectTransform _enemyRow;
        private UnitView _unitPrefab;
        private BattleUnitsView _units;
        private DeckCombatSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("BattleUnitsViewTestRoot", typeof(RectTransform));
            _partyRow = ChildRect("PartyRow");
            _enemyRow = ChildRect("EnemyRow");
            var prefabRoot = ChildRect("UnitPrefabRoot");
            _unitPrefab = UnitView.EditorCreate(prefabRoot, new Vector2(200f, 270f));

            _session = new DeckCombatSession(
                new[]
                {
                    Loadout("a", "Member A", 10),
                    Loadout("b", "Member B", 20),
                    Loadout("c", "Member C", 30)
                },
                new[]
                {
                    new Enemy("enemy_a", 10),
                    new Enemy("enemy_b", 20),
                    new Enemy("enemy_c", 30)
                },
                new EnemyIntent(new[] { (IReadOnlyList<CardDefinition>)Array.Empty<CardDefinition>() }),
                new PartyTuning
                {
                    DefaultMemberMaxHp = 10,
                    SurviveChargesPerCombat = 0,
                    DrawByLivingCount = new Dictionary<int, int> { { 1, 1 }, { 2, 1 }, { 3, 1 } }
                });

            _units = _root.AddComponent<BattleUnitsView>();
            SetField(_units, "_unitPrefab", _unitPrefab);
            SetField(_units, "_playerUnitsRow", _partyRow);
            SetField(_units, "_enemyUnitsRow", _enemyRow);
            _units.Spawn(_session.State, _ => Color.white, id => id);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Party_movement_preserves_each_views_identity_and_content()
        {
            var memberA = Party("a");
            var memberB = Party("b");
            var memberC = Party("c");
            memberA.Hp = 7;
            memberB.Hp = 0;
            memberC.Hp = 23;
            memberA.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), magnitude: 1);
            memberB.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), magnitude: 2);
            memberC.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), magnitude: 3);
            _units.Refresh(_session.State);
            var before = PartySnapshots();

            ApplyMove(Side.Player, memberC.Id, -99);
            _units.Refresh(_session.State);

            AssertViewUnchanged(before["a"], ViewById(_partyRow, "a"), expectedSibling: 1);
            AssertViewUnchanged(before["b"], ViewById(_partyRow, "b"), expectedSibling: 0);
            AssertViewUnchanged(before["c"], ViewById(_partyRow, "c"), expectedSibling: 2);
            CollectionAssert.AreEqual(
                new[] { "c", "a", "b" },
                _session.State.Party.Select(member => member.Id).ToArray());
        }

        [Test]
        public void Enemy_movement_preserves_each_views_identity_max_hp_content_and_order()
        {
            var enemyA = Enemy("enemy_a");
            var enemyB = Enemy("enemy_b");
            var enemyC = Enemy("enemy_c");
            enemyA.Hp = 7;
            enemyB.Hp = 13;
            enemyC.Hp = 29;
            enemyA.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), magnitude: 1);
            enemyB.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), magnitude: 2);
            enemyC.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), magnitude: 3);
            _units.Refresh(_session.State);
            var before = EnemySnapshots();

            ApplyMove(Side.Enemy, enemyA.Id, 99);
            _units.Refresh(_session.State);

            AssertViewUnchanged(before["enemy_a"], ViewById(_enemyRow, "enemy_a"), expectedSibling: 2);
            AssertViewUnchanged(before["enemy_b"], ViewById(_enemyRow, "enemy_b"), expectedSibling: 0);
            AssertViewUnchanged(before["enemy_c"], ViewById(_enemyRow, "enemy_c"), expectedSibling: 1);
            CollectionAssert.AreEqual(
                new[] { "enemy_b", "enemy_c", "enemy_a" },
                _session.State.Enemies.Select(enemy => enemy.Id).ToArray());
        }

        private static PartyMemberLoadout Loadout(string id, string name, int maxHp)
            => new PartyMemberLoadout(id, name, maxHp, Array.Empty<CardDefinition>());

        private RectTransform ChildRect(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(_root.transform, false);
            return (RectTransform)child.transform;
        }

        private void ApplyMove(Side side, string ownerId, int distance)
        {
            var effect = new EffectData(EffectKeys.MoveFormation, distance);
            var definition = new CardDefinition(
                "move", "move", side, 1, new[] { effect });
            new MoveFormationHandler().Apply(new EffectContext
            {
                Card = new ExecutionCardInstance(definition) { OwnerId = ownerId },
                State = _session.State,
                Effect = effect,
                EffectValue = distance
            });
        }

        private PartyMember Party(string id) => _session.State.Party.Single(member => member.Id == id);
        private Enemy Enemy(string id) => _session.State.Enemies.Single(enemy => enemy.Id == id);

        private Dictionary<string, ViewSnapshot> PartySnapshots()
            => GetField<Dictionary<string, UnitView>>(_units, "_partyUnits")
                .ToDictionary(pair => pair.Key, pair => Snapshot(pair.Value));

        private Dictionary<string, ViewSnapshot> EnemySnapshots()
            => GetField<Dictionary<string, UnitView>>(_units, "_enemyUnits")
                .ToDictionary(pair => pair.Key, pair => Snapshot(pair.Value));

        private UnitView ViewById(RectTransform row, string id)
            => row == _partyRow
                ? GetField<Dictionary<string, UnitView>>(_units, "_partyUnits")[id]
                : GetField<Dictionary<string, UnitView>>(_units, "_enemyUnits")[id];

        private static ViewSnapshot Snapshot(UnitView view) => new ViewSnapshot(
            Text(view, "_nameText"),
            Text(view, "_hpText"),
            Text(view, "_statusText"));

        private static string Text(UnitView view, string fieldName)
        {
            var component = GetField<Component>(view, fieldName);
            return (string)component.GetType().GetProperty("text").GetValue(component);
        }

        private static void AssertViewUnchanged(ViewSnapshot before, UnitView after, int expectedSibling)
        {
            var current = Snapshot(after);
            Assert.AreEqual(before.Name, current.Name);
            Assert.AreEqual(before.Hp, current.Hp);
            Assert.AreEqual(before.Status, current.Status);
            Assert.AreEqual(expectedSibling, after.transform.GetSiblingIndex());
        }

        private static void SetField(object target, string fieldName, object value)
            => Field(target.GetType(), fieldName).SetValue(target, value);

        private static T GetField<T>(object target, string fieldName)
            => (T)Field(target.GetType(), fieldName).GetValue(target);

        private static FieldInfo Field(Type type, string fieldName)
            => type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class ViewSnapshot
        {
            public ViewSnapshot(string name, string hp, string status)
            {
                Name = name;
                Hp = hp;
                Status = status;
            }

            public string Name { get; }
            public string Hp { get; }
            public string Status { get; }
        }
    }
}
