using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>A card placed in the future zone for one combat. Initiative is mutable.</summary>
    public sealed class ActionCardInstance
    {
        public CardDefinition Def { get; }
        public int Initiative { get; set; }

        public ActionCardInstance(CardDefinition def)
        {
            Def = def;
            Initiative = def.BaseInitiative;
        }
    }
}
