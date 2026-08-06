using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>픽스처가 의도한 모양을 만드는지 잠근다. 픽스처는 콘텐츠가 아니라 테스트 입력이며,
    /// 이름이 카드 정체성이 아니라 효과 모양인 것이 요점이다.</summary>
    public class CardFixturesTests
    {
        [Test]
        public void DamageFixtureCarriesItsDamageAndCost()
        {
            var card = CardFixtures.Damage("fx", damage: 4, executionOrder: 3, cost: 2);

            Assert.AreEqual("fx", card.Id);
            Assert.AreEqual(3, card.BaseExecutionOrder);
            Assert.AreEqual(2, card.EnergyCost);
            Assert.AreEqual(CardCategory.Execution, card.Category);
            Assert.AreEqual(EffectKeys.Damage, card.Effects.Single().Key);
            Assert.AreEqual(4, card.Effects.Single().EffectValue);
        }

        [Test]
        public void ConditionalFixtureCarriesBothValues()
        {
            var card = CardFixtures.DamageOnFirstTrigger("fx", baseDamage: 2, whenFirst: 8);

            var effect = card.Effects.Single();
            Assert.AreEqual(2, effect.EffectValue);
            Assert.AreEqual(8, effect.SuccessEffectValue);
            Assert.IsInstanceOf<FirstToTrigger>(effect.Condition);
        }

        [Test]
        public void InterventionFixtureHasNoEffectsAndCarriesItsAction()
        {
            var card = CardFixtures.ChangeExecutionOrder("fx", delta: -1);

            Assert.AreEqual(CardCategory.Intervention, card.Category);
            Assert.AreEqual(0, card.Effects.Count);
            Assert.AreEqual(InterventionActionKeys.ChangeExecutionOrder, card.InterventionAction.Key);
            Assert.AreEqual(-1, ((ChangeExecutionOrderPayload)card.InterventionAction.Payload).Delta);
        }
    }
}
