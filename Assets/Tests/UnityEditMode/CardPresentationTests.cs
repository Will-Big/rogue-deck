using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;
using FateWeaver.Unity;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardPresentationTests
    {
        private static CardDefinition EnemyCard() => new CardDefinition(
            "locked_jab", "잠긴 일격", Side.Enemy, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        [Test]
        public void Locked_zone_card_exposes_lock_status_icon()
        {
            var instance = new ExecutionCardInstance(EnemyCard()) { IsLocked = true };

            var presentation = CardPresentation.From(instance);

            CollectionAssert.AreEqual(new[] { CardStatusIcon.Lock }, presentation.StatusIcons.ToArray());
        }

        [Test]
        public void Unlocked_hand_card_has_no_status_icons()
        {
            var presentation = CardPresentation.FromDefinition(EnemyCard());

            Assert.AreEqual(0, presentation.StatusIcons.Count);
        }

        [Test]
        public void Hand_and_rail_presentations_preserve_the_same_owner_metadata()
        {
            var definition = new CardDefinition(
                "owned_guard", "소유 방어", Side.Player, CardType.Defense, 5,
                new[] { new EffectData(EffectKeys.Damage, 0) });
            var instance = new ExecutionCardInstance(definition);
            var ownerColor = new Color(0.35f, 0.65f, 0.95f, 1f);

            var hand = CardPresentation.FromDefinition(
                definition, null, "파티원 A", ownerColor, false);
            var rail = CardPresentation.From(
                instance, null, "파티원 A", ownerColor, false);

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
                "party_guard", "공용 방어", Side.Player, CardType.Defense, 5,
                new[] { new EffectData(EffectKeys.Damage, 0) });

            var presentation = CardPresentation.FromDefinition(
                definition, null, PlaytestKoreanText.PartyOwnerName(), Color.white, true);

            Assert.AreEqual("파티", presentation.OwnerDisplayName);
            Assert.IsTrue(presentation.IsPartyOwned);
        }

        [Test]
        public void Formation_card_uses_the_registered_dynamic_description()
        {
            var presentation = CardPresentation.FromDefinition(
                PartyPrototypeDeck.MoveForward(),
                id => null);

            Assert.AreEqual(
                "소유자를 대형 전방으로 1칸 이동.",
                presentation.Description);
        }
    }
}
