using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Enemies;
using FateWeaver.Core.Status;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>저장소에 저작된 적 콘텐츠의 골든. GoblinDeckTests가 C# 원본에 걸던 단언을 JSON에 건다
    /// (전투 노드 설계 2.8 — 원본 삭제 뒤에도 남는다).</summary>
    public class EnemyContentTests
    {
        [Test]
        public void Goblin_cards_are_authored_as_enemy_json()
        {
            var cards = TestContent.Cards();
            var thrust = cards.Get("goblin_jab");
            var guard = cards.Get("crude_guard");
            var sly = cards.Get("sly_jab");

            foreach (var card in new[] { thrust, guard, sly })
            {
                Assert.AreEqual(Side.Enemy, card.Side, card.Id);
                Assert.AreEqual(CardCategory.Execution, card.Category, card.Id);
                Assert.AreEqual(0, card.EnergyCost, card.Id);
            }

            Assert.AreEqual("찌르기", thrust.Name);
            Assert.AreEqual(6, thrust.BaseExecutionOrder);
            Assert.AreEqual(EffectKeys.Damage, thrust.Effects.Single().Key);
            Assert.AreEqual(4, thrust.Effects.Single().EffectValue);

            Assert.AreEqual("조잡한 방어", guard.Name);
            Assert.AreEqual(4, guard.BaseExecutionOrder);
            Assert.AreEqual(3, guard.Effects.Single().EffectValue);
            var payload = (ApplyStatusPayload)guard.Effects.Single().Payload;
            Assert.AreEqual(StatusKeys.Block, payload.Key);
            Assert.AreEqual(StatusApplyTarget.Self, payload.Target);

            Assert.AreEqual("약삭빠른 찌르기", sly.Name);
            Assert.AreEqual(3, sly.BaseExecutionOrder);
            Assert.AreEqual(3, sly.Effects.Single().EffectValue);
            Assert.AreEqual(6, sly.Effects.Single().SuccessEffectValue);
            Assert.AreEqual(Side.Player, ((NoPrecedingCardOfSide)sly.Effects.Single().Condition).Side);
        }

        [Test]
        public void Goblin_is_authored_with_four_bundles_and_random_pick()
        {
            var goblin = TestContent.Content().Enemies.Get("goblin");
            Assert.AreEqual("고블린", goblin.DisplayName);
            Assert.AreEqual(28, goblin.MaxHp);
            Assert.AreEqual(EnemyPolicyKeys.RandomPick, goblin.Policy);
            CollectionAssert.AreEqual(
                new[]
                {
                    "goblin_jab",                 // A 늦은 단타
                    "sly_jab,crude_guard",        // B 선공 후 방어
                    "sly_jab,goblin_jab",         // C 앞뒤로 벌린 2연타
                    "crude_guard,crude_guard"     // D 농성 (방어 재부여는 합산 → 방어도 6)
                },
                goblin.Bundles.Select(b => string.Join(",", b.Cards.Select(c => c.Id))).ToArray());
        }
    }
}
