using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardPresentationTests
    {
        private static readonly DescriptionRequestCatalog Requests =
            DescriptionRequestCatalog.CreateDefault(UnityTestContent.Statuses());

        private GameObject _host;
        private GameLocalization _text;

        [SetUp]
        public void CreateLocalization()
        {
            _host = new GameObject("CardPresentationTestsLocalization");
            _text = UnityTestLocalization.AddTo(_host);
        }

        [TearDown]
        public void DestroyLocalization() => Object.DestroyImmediate(_host);

        // Card ids must have translated names because FromLocalized reads card.<id>.name.
        private static CardDefinition EnemyCard() => new CardDefinition(
            "goblin_jab", "찌르기", Side.Enemy, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) { TargetFaction = CardTargetFaction.Ally } })
            { AllyTarget = CardTargetRange.FrontOne, EnergyCost = 0, Category = CardCategory.Execution };

        [Test]
        public void Presentation_constructor_does_not_accept_a_lossy_string_description()
        {
            Assert.IsNull(typeof(CardPresentation)
                .GetConstructors()
                .SingleOrDefault(constructor =>
                    constructor.GetParameters().Length > 5
                    && constructor.GetParameters()[5].ParameterType == typeof(string)));
        }

        [Test]
        public void Locked_zone_card_exposes_lock_status_icon()
        {
            var instance = new ExecutionCardInstance(EnemyCard()) { IsLocked = true };

            var presentation = Localized(instance.Def, instance.ExecutionOrder, instance.IsLocked);

            CollectionAssert.AreEqual(new[] { CardStatusIcon.Lock }, presentation.StatusIcons.ToArray());
        }

        [Test]
        public void Unlocked_hand_card_has_no_status_icons()
        {
            var presentation = Localized(EnemyCard());

            Assert.AreEqual(0, presentation.StatusIcons.Count);
        }

        [Test]
        public void Hand_and_rail_presentations_preserve_the_same_owner_metadata()
        {
            var definition = new CardDefinition(
                "brace", "버티기", Side.Player, 5,
                new[] { new EffectData(EffectKeys.Damage, 0) { TargetFaction = CardTargetFaction.Enemy } }) { EnemyTarget = CardTargetRange.FrontOne };
            var instance = new ExecutionCardInstance(definition);
            var ownerColor = new Color(0.35f, 0.65f, 0.95f, 1f);

            var hand = Localized(definition, ownerId: "member_a", ownerColor: ownerColor);
            var rail = Localized(instance.Def, instance.ExecutionOrder, instance.IsLocked,
                "member_a", ownerColor, instanceId: instance.InstanceId);

            Assert.AreEqual("파티원 A", hand.OwnerDisplayName);
            Assert.AreEqual(ownerColor, hand.OwnerColor);
            Assert.IsFalse(hand.IsPartyOwned);
            Assert.AreEqual(hand.OwnerDisplayName, rail.OwnerDisplayName);
            Assert.AreEqual(hand.OwnerColor, rail.OwnerColor);
            Assert.AreEqual(hand.IsPartyOwned, rail.IsPartyOwned);
        }

        [Test]
        public void Party_owned_presentation_uses_localized_party_owner_name()
        {
            var definition = new CardDefinition(
                "brace", "버티기", Side.Player, 5,
                new[] { new EffectData(EffectKeys.Damage, 0) { TargetFaction = CardTargetFaction.Enemy } }) { EnemyTarget = CardTargetRange.FrontOne };

            var presentation = Localized(definition, ownerColor: Color.white, partyOwned: true);

            Assert.AreEqual("파티", presentation.OwnerDisplayName);
            Assert.IsTrue(presentation.IsPartyOwned);
        }

        [Test]
        public void With_execution_order_changes_only_order()
        {
            var descriptionLayout = Localized(EnemyCard()).DescriptionLayout;
            var original = new CardPresentation(
                "id", "name", 5, 2, Side.Player, descriptionLayout, null, false,
                new[] { CardStatusIcon.Lock }, CardCategory.Execution,
                "owner", Color.cyan, true);

            var changed = original.WithExecutionOrder(2);

            Assert.AreEqual(2, changed.ExecutionOrder);
            Assert.AreEqual(original.Id, changed.Id);
            Assert.AreEqual(original.DisplayName, changed.DisplayName);
            Assert.AreEqual(original.EnergyCost, changed.EnergyCost);
            Assert.AreEqual(original.Side, changed.Side);
            Assert.AreEqual(original.Description, changed.Description);
            Assert.AreSame(original.DescriptionLayout, changed.DescriptionLayout);
            Assert.AreEqual(original.StatusIcons, changed.StatusIcons);
            Assert.AreEqual(original.Category, changed.Category);
            Assert.AreEqual(original.OwnerDisplayName, changed.OwnerDisplayName);
            Assert.AreEqual(original.OwnerColor, changed.OwnerColor);
            Assert.AreEqual(original.IsPartyOwned, changed.IsPartyOwned);
        }

        [Test]
        public void Formation_card_uses_the_registered_dynamic_description()
        {
            var presentation = Localized(UnityTestContent.Cards().Get("fixture_move_forward"));

            Assert.AreEqual(
                "[◆] 대형 전방으로 1칸 이동.",
                presentation.Description);
        }

        [Test]
        public void Toxic_reclaim_presentation_keeps_structured_targets_and_lines()
        {
            var definition = UnityTestContent.Cards().Get("toxic_reclaim");
            var presentation = Localized(definition);

            Assert.AreEqual(2, presentation.DescriptionLayout.TargetEntries.Count);
            Assert.AreEqual(2, presentation.DescriptionLayout.Lines.Count);
            Assert.AreEqual(presentation.DescriptionLayout.PlainText, presentation.Description);
        }

        [Test]
        public void Intervention_presentation_has_no_unit_target_entries()
        {
            var presentation = Localized(UnityCardFixtures.ChangeExecutionOrder("hasten", delta: -1));

            Assert.AreEqual(CardCategory.Intervention, presentation.Category);
            Assert.AreEqual(0, presentation.DescriptionLayout.TargetEntries.Count);
        }

        private CardPresentation Localized(CardDefinition definition, int? order = null, bool locked = false,
            string ownerId = null, Color ownerColor = default, bool partyOwned = false, int instanceId = -1)
            => CardPresentation.FromLocalized(definition, Requests, _text, null,
                order ?? definition.BaseExecutionOrder, locked, ownerId, ownerColor, partyOwned, instanceId);
    }
}
