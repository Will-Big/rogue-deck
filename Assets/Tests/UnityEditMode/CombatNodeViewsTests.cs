using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Run;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CombatNodeViewsTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        private RectTransform Canvas()
        {
            _root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            return (RectTransform)_root.transform;
        }

        private RewardChoiceView BoundReward() => BoundReward(out _);

        private RewardChoiceView BoundReward(out BattlePresenter presenter)
        {
            var parent = Canvas();
            var view = RewardChoiceView.EditorCreate(parent);
            presenter = new GameObject("Presenter").AddComponent<BattlePresenter>();
            presenter.transform.SetParent(parent, false);
            presenter.Initialize(id => id, KoreanDescriptionCatalog.CreateDefault(UnityTestContent.Statuses()));
            var cards = AssetDatabase.LoadAssetAtPath<CardPrefabCatalog>(CardPrefabCatalogTests.CatalogPath);
            view.EditorBind(cards, presenter);
            return view;
        }

        /// <summary>CardPrefabCatalog.Create는 프리팹만 만든다 — 내용은 CardView.Bind가 채운다.
        /// Bind를 빠뜨리면 카드가 프리팹 기본값("Name", 빈 설명)으로 보인다(2026-09-15 Play에서 발견).</summary>
        [Test]
        public void Show_binds_each_card_to_its_candidate()
        {
            var view = BoundReward(out var presenter);
            var candidates = Candidates(("hasten", "member_a"), ("breather", "member_b"));

            view.Show(candidates, id => id, _ => { }, () => { });

            var slots = CardPrefabCatalogTests.Field<RectTransform[]>(view, "_slots");
            for (int i = 0; i < candidates.Count; i++)
            {
                var expected = presenter.For(new OwnedCard(candidates[i].Card, candidates[i].OwnerId)).DisplayName;
                var card = slots[i].GetComponentInChildren<CardView>(true);
                Assert.AreEqual(expected, CardPrefabCatalogTests.Field<TMP_Text>(card, "_nameText").text);
            }
        }

        private static IReadOnlyList<RewardCandidate> Candidates(params (string cardId, string owner)[] items)
        {
            var content = UnityTestContent.Content();
            return items.Select(item => new RewardCandidate(content.Cards.Get(item.cardId), item.owner)).ToList();
        }

        [Test]
        public void Reward_view_authors_three_slots_and_both_buttons()
        {
            var view = RewardChoiceView.EditorCreate(Canvas());

            Assert.AreEqual(3, RewardChoiceView.SlotCount);
            Assert.AreEqual(3, CardPrefabCatalogTests.Field<RectTransform[]>(view, "_slots").Count(s => s != null));
            Assert.AreEqual(3, CardPrefabCatalogTests.Field<TMP_Text[]>(view, "_ownerLabels").Count(l => l != null));
            Assert.AreEqual(3, CardPrefabCatalogTests.Field<Button[]>(view, "_slotButtons").Count(b => b != null));
            Assert.IsNotNull(CardPrefabCatalogTests.Field<Button>(view, "_skipButton"));
            Assert.IsNotNull(CardPrefabCatalogTests.Field<Button>(view, "_nextButton"));
            Assert.IsFalse(view.IsBound, "프리팹 단계에서는 presenter·catalog가 비어 있다 — 씬이 채운다.");
            Assert.IsFalse(view.gameObject.activeSelf, "기본 비활성이어야 한다.");
        }

        [Test]
        public void Show_fills_only_as_many_slots_as_candidates_and_reports_the_index()
        {
            var view = BoundReward();
            int chosen = -1;

            view.Show(Candidates(("hasten", "member_a"), ("breather", "member_b")),
                id => id == "member_a" ? "파티원 A" : "파티원 B", i => chosen = i, () => { });

            var slots = CardPrefabCatalogTests.Field<RectTransform[]>(view, "_slots");
            var buttons = CardPrefabCatalogTests.Field<Button[]>(view, "_slotButtons");
            var labels = CardPrefabCatalogTests.Field<TMP_Text[]>(view, "_ownerLabels");
            Assert.IsTrue(view.gameObject.activeSelf);
            Assert.AreEqual(1, slots[0].GetComponentsInChildren<CardView>(true).Length);
            Assert.AreEqual(1, slots[1].GetComponentsInChildren<CardView>(true).Length);
            Assert.AreEqual(0, slots[2].GetComponentsInChildren<CardView>(true).Length);
            Assert.IsTrue(buttons[1].gameObject.activeSelf);
            Assert.IsFalse(buttons[2].gameObject.activeSelf);
            Assert.AreEqual("파티원 B", labels[1].text);

            buttons[1].onClick.Invoke();

            Assert.AreEqual(1, chosen);
        }

        [Test]
        public void ShowNextButton_locks_the_choice_and_Hide_clears_the_cards()
        {
            var view = BoundReward();
            bool skipped = false, next = false;
            view.Show(Candidates(("hasten", "member_a")), id => id, _ => { }, () => skipped = true);
            CardPrefabCatalogTests.Field<Button>(view, "_skipButton").onClick.Invoke();
            Assert.IsTrue(skipped);

            view.ShowNextButton(() => next = true);

            Assert.IsFalse(CardPrefabCatalogTests.Field<Button[]>(view, "_slotButtons")[0].interactable);
            Assert.IsFalse(CardPrefabCatalogTests.Field<Button>(view, "_skipButton").gameObject.activeSelf);
            var nextButton = CardPrefabCatalogTests.Field<Button>(view, "_nextButton");
            Assert.IsTrue(nextButton.gameObject.activeSelf);
            nextButton.onClick.Invoke();
            Assert.IsTrue(next);

            view.Hide();

            Assert.IsFalse(view.gameObject.activeSelf);
            Assert.AreEqual(0, view.GetComponentsInChildren<CardView>(true).Length);
        }

        [Test]
        public void Showing_again_replaces_the_previous_cards()
        {
            var view = BoundReward();
            view.Show(Candidates(("hasten", "member_a"), ("breather", "member_a")), id => id, _ => { }, () => { });

            view.Show(Candidates(("hasten", "member_a")), id => id, _ => { }, () => { });

            Assert.AreEqual(1, view.GetComponentsInChildren<CardView>(true).Length);
        }

        [Test]
        public void Result_view_shows_defeat_and_reports_restart()
        {
            var view = CombatResultView.EditorCreate(Canvas());
            bool restarted = false;

            Assert.IsTrue(view.IsBound);
            Assert.IsFalse(view.gameObject.activeSelf);
            view.ShowDefeat(() => restarted = true);
            Assert.IsTrue(view.gameObject.activeSelf);
            CardPrefabCatalogTests.Field<Button>(view, "_restartButton").onClick.Invoke();
            Assert.IsTrue(restarted);

            view.Hide();
            Assert.IsFalse(view.gameObject.activeSelf);
        }
    }
}
