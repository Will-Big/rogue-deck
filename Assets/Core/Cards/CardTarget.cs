using System;

namespace FateWeaver.Core.Cards
{
    public enum CardTargetFaction { Ally, Enemy }
    public enum CardTargetRange { Self, FrontOne, FrontTwo, BackOne, BackTwo, All }

    public readonly struct CardTargetKey : IEquatable<CardTargetKey>
    {
        public CardTargetFaction Faction { get; }
        public CardTargetRange Range { get; }

        public CardTargetKey(CardTargetFaction faction, CardTargetRange range)
        {
            Faction = faction;
            Range = range;
        }

        public bool Equals(CardTargetKey other)
            => Faction == other.Faction && Range == other.Range;
        public override bool Equals(object obj)
            => obj is CardTargetKey other && Equals(other);
        public override int GetHashCode() => ((int)Faction * 397) ^ (int)Range;
        public override string ToString() => Faction + "/" + Range;
    }
}
