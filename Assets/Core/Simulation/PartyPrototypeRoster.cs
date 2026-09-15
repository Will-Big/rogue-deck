namespace FateWeaver.Simulation
{
    /// <summary>파티 프로토타입의 id·표시명·튜닝. 로드아웃 조립은 콘텐츠가 한다 —
    /// RunSetup.NewRun이 Characters/Decks/Cards JSON에서 시작 파티를 조립한다.</summary>
    public static class PartyPrototypeRoster
    {
        public const string MemberAId = "member_a";
        public const string MemberAName = "파티원 A";
        public const string MemberBId = "member_b";
        public const string MemberBName = "파티원 B";

        public static PartyTuning Tuning => PartyTuning.Prototype;
    }
}
