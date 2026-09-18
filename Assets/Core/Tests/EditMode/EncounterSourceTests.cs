using FateWeaver.Core.Combat;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class EncounterSourceTests
    {
        [Test]
        public void Enemy_keeps_combat_id_and_spec_id_apart()
        {
            var enemy = new Enemy("goblin#0", "goblin", 28);

            Assert.AreEqual("goblin#0", enemy.Id);
            Assert.AreEqual("goblin", enemy.SpecId);
            Assert.AreEqual(28, enemy.Hp);
        }

        [Test]
        public void Legacy_enemy_constructor_uses_the_id_as_spec_id()
        {
            Assert.AreEqual("goblin", new Enemy("goblin", 28).SpecId);
        }
    }
}
