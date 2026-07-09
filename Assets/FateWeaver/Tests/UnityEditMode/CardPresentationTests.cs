using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardPresentationTests
    {
        private static CardDefinition EnemyCard() => new CardDefinition(
            "locked_jab", "잠긴 일격", Side.Enemy, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) })
            { Cost = 0, Category = CardCategory.Execution };

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
    }
}
