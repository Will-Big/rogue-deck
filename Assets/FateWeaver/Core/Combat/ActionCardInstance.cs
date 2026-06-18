using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>A card placed in the future zone for one combat. Initiative is mutable.</summary>
    public sealed class ActionCardInstance : IStatusHolder
    {
        public CardDefinition Def { get; }
        public int Initiative { get; set; }
        public string TargetId { get; set; }
        public bool IsLocked { get; set; }
        public StatusBag Statuses { get; } = new();

        public ActionCardInstance(CardDefinition def)
        {
            Def = def;
            Initiative = def.BaseInitiative;
        }
    }
}
