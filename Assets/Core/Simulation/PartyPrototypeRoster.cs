using System.Collections.Generic;

namespace FateWeaver.Simulation
{
    /// <summary>Two-member neutral validation roster consumed by the party prototype scene.</summary>
    public static class PartyPrototypeRoster
    {
        public const string MemberAId = "member_a";
        public const string MemberAName = "파티원 A";
        public const string MemberBId = "member_b";
        public const string MemberBName = "파티원 B";

        public static PartyTuning Tuning => PartyTuning.Prototype;

        public static IReadOnlyList<PartyMemberLoadout> Build()
        {
            var tuning = Tuning;
            return new List<PartyMemberLoadout>
            {
                new PartyMemberLoadout(
                    MemberAId,
                    MemberAName,
                    tuning.DefaultMemberMaxHp,
                    StarterDeck.Build()),
                new PartyMemberLoadout(
                    MemberBId,
                    MemberBName,
                    tuning.DefaultMemberMaxHp,
                    PartyPrototypeDeck.Build())
            };
        }
    }
}
