using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Combat;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Rules
{
    public sealed class CombatRulesLoadResult
    {
        private CombatRulesLoadResult(CombatRules rules, IReadOnlyList<string> errors)
        {
            Rules = rules;
            Errors = errors;
        }

        public bool Succeeded => Rules != null;
        public CombatRules Rules { get; }
        public IReadOnlyList<string> Errors { get; }

        public static CombatRulesLoadResult Ok(CombatRules rules) => new CombatRulesLoadResult(rules, new string[0]);

        public static CombatRulesLoadResult Failed(IReadOnlyList<string> errors) => new CombatRulesLoadResult(null, errors);
    }

    /// <summary>Content 루트의 단일 파일 combat_rules.json을 검증한다. 다른 카탈로그에 의존하지 않는다.</summary>
    public static class CombatRulesLoader
    {
        public const string FileName = CardContentFiles.CombatRulesFileName;

        private static readonly string[] RequiredKeys =
            { "fateEnergyPerTurn", "minPartySize", "maxPartySize", "drawByLivingCount", "rewardChoices" };

        public static CombatRulesLoadResult Load(CardContentSource source)
        {
            if (source == null)
            {
                return CombatRulesLoadResult.Failed(new[] { FileName + ": file is missing." });
            }

            var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
            if (missing != null)
            {
                return CombatRulesLoadResult.Failed(new[] { source.Name + ": required key '" + missing + "' is missing." });
            }

            CombatRulesSpec spec;
            try
            {
                spec = ContentJson.Read<CombatRulesSpec>(source.Json);
            }
            catch (JsonException ex)
            {
                return CombatRulesLoadResult.Failed(new[] { source.Name + ": " + ContentJsonError.Describe(ex) });
            }

            var errors = new List<string>();
            if (spec.FateEnergyPerTurn <= 0)
            {
                errors.Add(source.Name + ": fateEnergyPerTurn must be positive.");
            }

            if (spec.MinPartySize < 1)
            {
                errors.Add(source.Name + ": minPartySize must be at least 1.");
            }
            else if (spec.MinPartySize > spec.MaxPartySize)
            {
                errors.Add(source.Name + ": minPartySize must not exceed maxPartySize.");
            }

            for (int living = 1; living <= spec.MaxPartySize; living++)
            {
                if (spec.DrawByLivingCount == null
                    || !spec.DrawByLivingCount.TryGetValue(living, out var draw)
                    || draw <= 0)
                {
                    errors.Add(source.Name + ": drawByLivingCount must give a positive draw for living count " + living + ".");
                }
            }

            if (spec.RewardChoices <= 0)
            {
                errors.Add(source.Name + ": rewardChoices must be positive.");
            }

            if (errors.Count > 0)
            {
                return CombatRulesLoadResult.Failed(errors);
            }

            var party = new PartyTuning
            {
                MinPartySize = spec.MinPartySize,
                MaxPartySize = spec.MaxPartySize,
                DrawByLivingCount = spec.DrawByLivingCount
            };
            return CombatRulesLoadResult.Ok(new CombatRules(party, spec.FateEnergyPerTurn, spec.RewardChoices));
        }
    }
}
