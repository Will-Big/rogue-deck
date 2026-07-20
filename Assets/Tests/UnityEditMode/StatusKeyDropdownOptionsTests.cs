using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity.Editor;
using NUnit.Framework;

namespace FateWeaver.Tests.UnityEditMode
{
    public class StatusKeyDropdownOptionsTests
    {
        [Test]
        public void Known_block_uses_registered_option_with_description_label()
        {
            var descriptions = new StatusDescriptionRegistry();
            descriptions.Register(StatusKeys.Block, "방어");

            var model = StatusKeyDropdownOptions.Create(
                StatusKeys.Block.Id,
                new[] { StatusKeys.Block, StatusKeys.Slow },
                descriptions);

            Assert.AreEqual(1, model.SelectedIndex);
            Assert.AreEqual("방어 (block)", model.Options[1].Label);
            Assert.AreEqual("slow", model.Options[2].Label);
        }

        [Test]
        public void Unknown_key_is_preserved_as_the_first_option()
        {
            var model = StatusKeyDropdownOptions.Create(
                "legacy_block",
                new[] { StatusKeys.Block },
                new StatusDescriptionRegistry());

            Assert.AreEqual(0, model.SelectedIndex);
            Assert.AreEqual("Unknown: legacy_block", model.Options[0].Label);
            Assert.AreEqual("legacy_block", model.Options[0].Id);
            Assert.AreEqual("(상태 선택)", model.Options[1].Label);
        }

        [Test]
        public void Empty_key_selects_the_placeholder()
        {
            var model = StatusKeyDropdownOptions.Create(
                string.Empty,
                new[] { StatusKeys.Block },
                new StatusDescriptionRegistry());

            Assert.AreEqual(0, model.SelectedIndex);
            Assert.AreEqual("(상태 선택)", model.Options[0].Label);
            Assert.AreEqual(string.Empty, model.Options[0].Id);
        }
    }
}
