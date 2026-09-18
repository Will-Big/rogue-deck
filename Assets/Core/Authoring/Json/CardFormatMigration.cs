using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>구형 카드 JSON(cardFormat 없음)을 현재 형식으로 올린다. 로딩 경계에서만 쓰며 실행 처리기에는
    /// 구형 분기를 남기지 않는다(전투 실행 계약 스펙 §4). 규칙은 판단 없이 기계적으로 적용되고, 규칙으로
    /// 결정되지 않는 카드는 추정하지 않고 오류로 거부한다(계획 T2a 변환 규칙 표).
    ///
    /// 구형의 효과 종류별 대상 기본값을 여기 한곳에 둔다. 그 기본값은 구형 형식의 일부라 새 효과가 늘어도
    /// 이 표는 자라지 않는다 — 새 효과는 처음부터 현재 형식으로 저작된다.</summary>
    public static class CardFormatMigration
    {
        private const string Ally = "Ally";
        private const string Enemy = "Enemy";
        private const string DefaultRange = "FrontOne";

        public static void Upgrade(JObject card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            var cardId = (string)card["id"] ?? "?";
            if ((string)card["category"] == "Execution")
            {
                UpgradeExecution(card, cardId);
            }

            card["cardFormat"] = Authoring.CardSpec.CurrentFormat;
        }

        private static void UpgradeExecution(JObject card, string cardId)
        {
            var effects = card["effects"] as JArray ?? new JArray();
            var side = (string)card["side"] ?? "Player";
            var ownFaction = side == "Enemy" ? Enemy : Ally;
            var opposingFaction = side == "Enemy" ? Ally : Enemy;

            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i] is JObject effect && effect["id"] == null)
                {
                    effect["id"] = "e" + i;
                }
            }

            var axes = new Dictionary<string, string>(StringComparer.Ordinal);
            JObject startCondition = null;
            for (var i = 0; i < effects.Count; i++)
            {
                if (!(effects[i] is JObject effect))
                {
                    continue;
                }

                var at = "card '" + cardId + "' effects[" + i + "]";
                UpgradeTarget(effect, at, ownFaction, opposingFaction, axes);
                UpgradeConsume(effects, i, at);
                startCondition = UpgradeCondition(effects, i, at, startCondition);
            }

            if (axes.Count > 0)
            {
                var targets = new JObject();
                if (axes.TryGetValue(Ally, out var ally)) targets["ally"] = ally;
                if (axes.TryGetValue(Enemy, out var enemy)) targets["enemy"] = enemy;
                card["targets"] = targets;
            }

            if (startCondition != null)
            {
                card["startCondition"] = startCondition;
            }
        }

        /// <summary>구형 효과마다의 selector·target을 (진영, 위치)로 바꿔 카드 축에 모은다. 구형 처리기의 기본값
        /// (selector 없음 = FrontOne)을 그대로 옮긴다.</summary>
        private static void UpgradeTarget(
            JObject effect, string at, string ownFaction, string opposingFaction, Dictionary<string, string> axes)
        {
            var kind = (string)effect["kind"];
            var selector = (string)effect["selector"];
            var range = string.IsNullOrEmpty(selector) || selector == "None" ? DefaultRange : selector;
            string faction;
            switch (kind)
            {
                case "damage":
                    faction = opposingFaction;
                    break;
                case "consume_status":
                case "trigger_status":
                    faction = Enemy;
                    break;
                case "move_formation":
                    faction = ownFaction;
                    range = "Self";
                    break;
                case "apply_status":
                    var target = (string)effect["target"] ?? "Self";
                    switch (target)
                    {
                        case "Self":
                            faction = ownFaction;
                            range = "Self";
                            break;
                        case "TargetEnemy":
                            faction = Enemy;
                            break;
                        case "PartyBySelector":
                            faction = Ally;
                            break;
                        case "AllPartyMembers":
                            faction = Ally;
                            range = "All";
                            break;
                        default:
                            throw Fail(at, "apply_status target '" + target + "' has no position equivalent.");
                    }

                    break;
                default:
                    effect.Remove("selector");
                    return;
            }

            if (axes.TryGetValue(faction, out var existing) && existing != range)
            {
                throw Fail(at, "legacy targets conflict on " + faction + ": " + existing + " vs " + range + ".");
            }

            axes[faction] = range;
            effect["targetFaction"] = faction;
            effect.Remove("selector");
            effect.Remove("target");
        }

        /// <summary>maxAmount → amount + UpTo(구형 규칙이 UpTo였다). damageBonusPerConsumed는 뒤따르는 첫
        /// 피해 효과의 scaleBy가 된다 — 구형에서 그 보너스를 받던 효과가 바로 그것이었다.</summary>
        private static void UpgradeConsume(JArray effects, int index, string at)
        {
            var effect = (JObject)effects[index];
            if ((string)effect["kind"] != "consume_status")
            {
                return;
            }

            var maxAmount = effect["maxAmount"];
            if (maxAmount != null)
            {
                effect["amount"] = maxAmount;
                effect.Remove("maxAmount");
            }

            effect["mode"] = "UpTo";

            var bonus = (int?)effect["damageBonusPerConsumed"] ?? 0;
            effect.Remove("damageBonusPerConsumed");
            if (bonus == 0)
            {
                return;
            }

            for (var i = index + 1; i < effects.Count; i++)
            {
                if (effects[i] is JObject later && (string)later["kind"] == "damage")
                {
                    later["scaleBy"] = new JObject
                    {
                        ["sourceEffectId"] = (string)effect["id"],
                        ["perConsumed"] = bonus
                    };
                    return;
                }
            }

            throw Fail(at, "damageBonusPerConsumed has no later damage effect to scale.");
        }

        /// <summary>효과마다의 조건을 나눈다: 소비량 조건은 그 효과의 requires로, 나머지는 카드 시작 조건으로.
        /// 서로 다른 일반 조건이 여러 효과에 있으면 하나로 합치지 않고 거부한다.</summary>
        private static JObject UpgradeCondition(JArray effects, int index, string at, JObject startCondition)
        {
            var effect = (JObject)effects[index];
            if (!(effect["condition"] is JObject condition))
            {
                return startCondition;
            }

            effect.Remove("condition");
            var kind = (string)condition["kind"];
            var n = (int?)condition["n"] ?? 0;
            var success = (int?)condition["successEffectValue"];
            var skipOnBasic = (bool?)condition["skipOnBasic"] ?? false;

            if (kind == "ConsumedStatusAtLeast")
            {
                if (!skipOnBasic)
                {
                    throw Fail(at, "ConsumedStatusAtLeast without skipOnBasic has no requires equivalent.");
                }

                effect["requires"] = new JObject
                {
                    ["sourceEffectId"] = OnlyEarlierConsume(effects, index, at),
                    ["minimumConsumed"] = n
                };
                if (success.HasValue)
                {
                    effect[(string)effect["kind"] == "apply_status" ? "count" : "value"] = success.Value;
                }

                return startCondition;
            }

            var converted = new JObject { ["kind"] = kind };
            if (n != 0)
            {
                converted["n"] = n;
            }

            if (startCondition != null && !JToken.DeepEquals(startCondition, converted))
            {
                throw Fail(at, "effects carry different conditions; a card has one start condition.");
            }

            if (success.HasValue)
            {
                effect["successEffectValue"] = success.Value;
            }

            if (skipOnBasic)
            {
                effect["skipOnBasic"] = true;
            }

            return converted;
        }

        private static string OnlyEarlierConsume(JArray effects, int index, string at)
        {
            string found = null;
            for (var i = 0; i < index; i++)
            {
                if (effects[i] is JObject earlier && (string)earlier["kind"] == "consume_status")
                {
                    if (found != null)
                    {
                        throw Fail(at, "ConsumedStatusAtLeast is ambiguous: more than one earlier consume_status.");
                    }

                    found = (string)earlier["id"];
                }
            }

            return found ?? throw Fail(at, "ConsumedStatusAtLeast has no earlier consume_status.");
        }

        private static JsonSerializationException Fail(string at, string message)
            => new JsonSerializationException("Legacy card could not be upgraded: " + at + ": " + message);
    }
}
