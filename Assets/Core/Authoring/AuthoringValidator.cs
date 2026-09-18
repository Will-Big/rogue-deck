using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Walks authored card specs and collects every validation error (returns them instead of
    /// throwing so the editor can show all problems at once; boot/tests assert the list is empty).
    /// 오류에는 카드 id와 JSON 경로(effects[i], 효과 id)를 담는다 — 조용한 생략이나 전투 중 취소로 넘기지
    /// 않는다(전투 실행 계약 스펙 §4). 파일 이름은 CardContentLoader가 앞에 붙인다.</summary>
    public static class AuthoringValidator
    {
        public static IReadOnlyList<string> Validate(
            IEnumerable<CardSpec> specs,
            AuthoringContext context)
        {
            var errors = new List<string>();
            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    errors.Add("Card spec list contains a null entry.");
                    continue;
                }

                if (string.IsNullOrEmpty(spec.Id))
                {
                    errors.Add("Card spec requires an id.");
                }

                if (spec.CardFormat != CardSpec.CurrentFormat)
                {
                    errors.Add(Card(spec) + "cardFormat must be " + CardSpec.CurrentFormat + ".");
                }

                if (spec is InterventionCardSpec intervention)
                {
                    ValidateIntervention(intervention, context, errors);
                    continue;
                }

                ValidateExecution((ExecutionCardSpec)spec, context, errors);
            }

            return errors;
        }

        private static void ValidateIntervention(
            InterventionCardSpec spec, AuthoringContext context, List<string> errors)
        {
            if (spec.Intervention == null)
            {
                errors.Add(Card(spec) + "intervention card requires an intervention spec.");
                return;
            }

            if (!context.HasIntervention(spec.Intervention.Key))
            {
                errors.Add(Card(spec) + "unknown intervention key '" + spec.Intervention.Key.Id + "'.");
                return;
            }

            foreach (var error in spec.Intervention.Validate(context))
            {
                errors.Add(Card(spec) + error);
            }
        }

        private static void ValidateExecution(
            ExecutionCardSpec spec, AuthoringContext context, List<string> errors)
        {
            var effects = spec.Effects ?? Array.Empty<EffectSpec>();
            ValidateStartCondition(spec, errors);

            var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            var usedFactions = new HashSet<CardTargetFaction>();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    errors.Add(Card(spec) + "effects[" + i + "] is null.");
                    continue;
                }

                var at = Card(spec) + Path(i, effect);
                if (string.IsNullOrEmpty(effect.Id))
                {
                    errors.Add(at + "requires an id.");
                }
                else if (indexById.ContainsKey(effect.Id))
                {
                    errors.Add(at + "duplicate effect id '" + effect.Id + "'.");
                }
                else
                {
                    indexById.Add(effect.Id, i);
                }

                if (!context.HasEffect(effect.Key))
                {
                    errors.Add(at + "no runtime handler for effect key '" + effect.Key + "'.");
                }

                foreach (var error in effect.Validate(context))
                {
                    errors.Add(at + error);
                }

                ValidateTarget(spec, effect, at, usedFactions, errors);
                ValidateConditionFields(spec, effect, at, errors);
                ValidateReference(effects, indexById, i, "requires", effect.Requires?.SourceEffectId, at, errors);
                ValidateReference(effects, indexById, i, "scaleBy", effect.ScaleBy?.SourceEffectId, at, errors);

                if (effect.Requires != null && effect.Requires.MinimumConsumed < 1)
                {
                    errors.Add(at + "requires.minimumConsumed must be at least 1.");
                }

                if (effect.ScaleBy != null && effect.ScaleBy.PerConsumed == 0)
                {
                    errors.Add(at + "scaleBy.perConsumed must not be 0.");
                }
            }

            foreach (var faction in new[] { CardTargetFaction.Ally, CardTargetFaction.Enemy })
            {
                if (spec.Targets?.RangeOf(faction) != null && !usedFactions.Contains(faction))
                {
                    errors.Add(Card(spec) + "targets." + Camel(faction) + " is declared but no effect targets "
                        + faction + ".");
                }
            }
        }

        private static void ValidateStartCondition(ExecutionCardSpec spec, List<string> errors)
        {
            var condition = spec.StartCondition;
            if (condition == null)
            {
                return;
            }

            if (condition.Kind == ConditionKind.None || !Enum.IsDefined(typeof(ConditionKind), condition.Kind))
            {
                errors.Add(Card(spec) + "startCondition.kind must name a condition.");
            }
        }

        private static void ValidateTarget(
            ExecutionCardSpec spec,
            EffectSpec effect,
            string at,
            HashSet<CardTargetFaction> usedFactions,
            List<string> errors)
        {
            if (!effect.IsTargeted)
            {
                if (effect.TargetFaction.HasValue)
                {
                    errors.Add(at + effect.Key.Id + " has no target, so targetFaction must be omitted.");
                }

                return;
            }

            if (!effect.TargetFaction.HasValue)
            {
                errors.Add(at + "targetFaction is required for " + effect.Key.Id + ".");
                return;
            }

            var faction = effect.TargetFaction.Value;
            usedFactions.Add(faction);
            var target = CardSpecMapper.TargetOf(spec, effect);
            if (!target.HasValue)
            {
                errors.Add(at + "targetFaction " + faction + " needs targets." + Camel(faction) + " on the card.");
                return;
            }

            foreach (var error in effect.ValidateTarget(spec.Side, target.Value))
            {
                errors.Add(at + error);
            }
        }

        private static void ValidateConditionFields(
            ExecutionCardSpec spec, EffectSpec effect, string at, List<string> errors)
        {
            if (spec.StartCondition != null)
            {
                return;
            }

            if (effect.SuccessEffectValue.HasValue)
            {
                errors.Add(at + "successEffectValue needs a card startCondition.");
            }

            if (effect.SkipOnBasic)
            {
                errors.Add(at + "skipOnBasic needs a card startCondition.");
            }
        }

        /// <summary>결과 참조는 앞에 있는, 소비량을 내는 효과만 가리킬 수 있다(스펙 §4).</summary>
        private static void ValidateReference(
            EffectSpec[] effects,
            Dictionary<string, int> earlierIndexById,
            int index,
            string field,
            string sourceId,
            string at,
            List<string> errors)
        {
            if (sourceId == null)
            {
                return;
            }

            if (sourceId == effects[index].Id)
            {
                errors.Add(at + field + " refers to itself.");
                return;
            }

            if (!earlierIndexById.TryGetValue(sourceId, out var sourceIndex) || sourceIndex >= index)
            {
                errors.Add(at + field + " refers to '" + sourceId + "', which is not an earlier effect of this card.");
                return;
            }

            if (!effects[sourceIndex].ProducesConsumption)
            {
                errors.Add(at + field + " refers to '" + sourceId + "' (" + effects[sourceIndex].Key.Id
                    + "), which produces no consumed amount.");
            }
        }

        private static string Card(CardSpec spec) => "Card '" + spec.Id + "': ";

        private static string Path(int index, EffectSpec effect)
            => "effects[" + index + "]" + (string.IsNullOrEmpty(effect.Id) ? "" : " (id '" + effect.Id + "')") + ": ";

        private static string Camel(CardTargetFaction faction)
            => faction == CardTargetFaction.Ally ? "ally" : "enemy";
    }
}
