using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CardContentJsonTests
    {
        [Test]
        public void WritesEnumsAsNamesAndCamelCaseKeys()
        {
            var json = ContentJson.Write(new CardSpec
            {
                Id = "slash",
                Name = "베기",
                Side = Side.Enemy,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 4
            });

            StringAssert.Contains("\"id\": \"slash\"", json);
            StringAssert.Contains("\"side\": \"Enemy\"", json);
        }

        [Test]
        public void OmitsDefaultValuedMembers()
        {
            var json = ContentJson.Write(new CardSpec { Id = "x", Name = "x" });

            StringAssert.DoesNotContain("interventionEffectValue", json);
        }
    }
}
