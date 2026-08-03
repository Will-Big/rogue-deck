using System.Collections.Generic;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Json;

namespace FateWeaver.Simulation
{
    /// <summary>PartyPrototypeRoster를 저작 형태로 비춘다. 로스터가 이 어셈블리에 있어
    /// FateWeaver.Core의 ContentExportWriter가 직접 닿지 못하므로, 내보내기 호출자가 이것을
    /// 넘긴다. 계획 3d가 로스터와 함께 지운다.</summary>
    public static class PartyPrototypeCharacterSpecs
    {
        public static IReadOnlyList<CharacterSpec> Build() => new List<CharacterSpec>
        {
            new CharacterSpec
            {
                Id = PartyPrototypeRoster.MemberAId,
                DisplayName = PartyPrototypeRoster.MemberAName,
                Deck = ContentExportWriter.StarterDeckId
            },
            new CharacterSpec
            {
                Id = PartyPrototypeRoster.MemberBId,
                DisplayName = PartyPrototypeRoster.MemberBName,
                Deck = ContentExportWriter.PartyPrototypeDeckId
            }
        };
    }
}
