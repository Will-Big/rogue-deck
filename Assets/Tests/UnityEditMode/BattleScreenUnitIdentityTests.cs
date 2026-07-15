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
using UnityEngine.UI;

namespace FateWeaver.Tests.UnityEditMode
{
    public class BattleScreenUnitIdentityTests
    {
        private GameObject _root;
        private RectTransform _partyRow;
        private RectTransform _enemyRow;
        private UnitView _unitPrefab;
        private BattleScreenController _controller;
        private DeckCombatSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("BattleScreenTestRoot", typeof(RectTransform));
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

            _controller = _root.AddComponent<BattleScreenController>();
            SetField(_controller, "_unitPrefab", _unitPrefab);
            SetField(_controller, "_playerUnitsRow", _partyRow);
            SetField(_controller, "_enemyUnitsRow", _enemyRow);
            SetField(_controller, "_session", _session);
            ConfigureSelectionDependencies();
            Invoke(_controller, "SpawnUnits");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Party_movement_preserves_each_views_identity_content_click_target_and_targetability()
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
            SetAllyTargetingMode();
            Invoke(_controller, "RefreshUnits");
            Invoke(_controller, "RefreshSelections");
            var before = PartyViews().ToDictionary(MemberId, Snapshot);

            ApplyMove(Side.Player, memberC.Id, -99);
            Invoke(_controller, "RefreshUnits");
            Invoke(_controller, "RefreshSelections");

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
            Invoke(_controller, "RefreshUnits");
            var before = EnemyViews().ToDictionary(MemberId, Snapshot);

            ApplyMove(Side.Enemy, enemyA.Id, 99);
            Invoke(_controller, "RefreshUnits");

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

        private void ConfigureSelectionDependencies()
        {
            var inactive = new GameObject("InactiveSelectionDependencies", typeof(RectTransform));
            inactive.transform.SetParent(_root.transform, false);
            inactive.SetActive(false);
            SetField(_controller, "_hand", inactive.AddComponent<HandFanView>());
            SetField(_controller, "_rail", inactive.AddComponent<ExecutionRailView>());
            SetField(_controller, "_drawPile", Pile(inactive.transform, "Draw"));
            SetField(_controller, "_discardPile", Pile(inactive.transform, "Discard"));
            SetField(_controller, "_fullDeck", Pile(inactive.transform, "Full"));
            SetField(_controller, "_resetButton", Button(inactive.transform, "Reset"));
            SetField(_controller, "_turnButton", Button(inactive.transform, "Turn"));
            SetField(_controller, "_cancelButton", Button(inactive.transform, "Cancel"));
            var dim = new GameObject("Dim");
            dim.transform.SetParent(inactive.transform, false);
            SetField(_controller, "_dimLayer", dim);
        }

        private static PileView Pile(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var pile = root.AddComponent<PileView>();
            SetField(pile, "_button", root.AddComponent<Button>());
            var popup = new GameObject(name + "Popup");
            popup.transform.SetParent(parent, false);
            popup.SetActive(false);
            SetField(pile, "_popup", popup);
            return pile;
        }

        private static Button Button(Transform parent, string name)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            return button;
        }

        private void SetAllyTargetingMode()
        {
            var field = Field(typeof(BattleScreenController), "_inputMode");
            field.SetValue(_controller, Enum.ToObject(field.FieldType, 2));
        }

        private void ApplyMove(Side side, string ownerId, int distance)
        {
            var effect = new EffectData(EffectKeys.MoveFormation, distance);
            var definition = new CardDefinition(
                "move", "move", side, CardType.Skill, 1, new[] { effect });
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
        private IEnumerable<UnitView> PartyViews() => _partyRow.GetComponentsInChildren<UnitView>(true);
        private IEnumerable<UnitView> EnemyViews() => _enemyRow.GetComponentsInChildren<UnitView>(true);

        private static UnitView ViewById(RectTransform row, string id)
            => row.GetComponentsInChildren<UnitView>(true).Single(view => MemberId(view) == id);

        private static string MemberId(UnitView view) => GetField<string>(view, "_memberId");

        private static ViewSnapshot Snapshot(UnitView view) => new ViewSnapshot(
            Text(view, "_nameText"),
            MemberId(view),
            Text(view, "_hpText"),
            Text(view, "_statusText"),
            GetField<Button>(view, "_targetButton").interactable);

        private static string Text(UnitView view, string fieldName)
        {
            var component = GetField<Component>(view, fieldName);
            return (string)component.GetType().GetProperty("text").GetValue(component);
        }

        private static void AssertViewUnchanged(ViewSnapshot before, UnitView after, int expectedSibling)
        {
            var current = Snapshot(after);
            Assert.AreEqual(before.Name, current.Name);
            Assert.AreEqual(before.ClickMemberId, current.ClickMemberId);
            Assert.AreEqual(before.Hp, current.Hp);
            Assert.AreEqual(before.Status, current.Status);
            Assert.AreEqual(before.Targetable, current.Targetable);
            Assert.AreEqual(expectedSibling, after.transform.GetSiblingIndex());
        }

        private static void Invoke(object target, string methodName)
            => target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void SetField(object target, string fieldName, object value)
            => Field(target.GetType(), fieldName).SetValue(target, value);

        private static T GetField<T>(object target, string fieldName)
            => (T)Field(target.GetType(), fieldName).GetValue(target);

        private static FieldInfo Field(Type type, string fieldName)
            => type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class ViewSnapshot
        {
            public ViewSnapshot(string name, string clickMemberId, string hp, string status, bool targetable)
            {
                Name = name;
                ClickMemberId = clickMemberId;
                Hp = hp;
                Status = status;
                Targetable = targetable;
            }

            public string Name { get; }
            public string ClickMemberId { get; }
            public string Hp { get; }
            public string Status { get; }
            public bool Targetable { get; }
        }
    }
}
