using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>카드 저작 노트북이 읽는 스키마를 EffectSpecCatalog와 InterventionSpecCatalog에서
    /// 생성하고, 커밋된 파일과 다르면 갱신한 뒤 실패한다(설계 §4). 노트북이 C# 파일 위치나 문법에
    /// 결합되지 않게 하는 것이 목적이므로, 생성기는 경로가 아니라 타입만 본다.</summary>
    public sealed class AuthoringSchemaExportTests
    {
        [Test]
        public void SchemaFileMatchesCatalog()
        {
            var expected = BuildSchema().ToString(Formatting.Indented) + "\n";
            var path = Path.Combine(
                TestContent.RepoRoot(), "Tools", "card-idea-notebook", "authoring-schema.json");

            var actual = File.Exists(path) ? File.ReadAllText(path) : null;
            if (actual == expected)
            {
                return;
            }

            File.WriteAllText(path, expected);
            Assert.Fail(
                "authoring-schema.json이 저작 명부와 달라 갱신했다. 커밋에 포함하고 "
                + "테스트를 다시 실행하라. 경로: " + path);
        }

        /// <summary>키 순서를 추측하지 않고 Newtonsoft에게 물어본다. 노트북이 재현해야 하는 순서가
        /// 바로 이 직렬화기의 순서이므로, 같은 계약(camelCase + 키 참조 컨버터)으로 빈 인스턴스를
        /// 직렬화해 속성 순서를 읽는다. 기본값도 봐야 하므로 Include를 쓴다.</summary>
        private static JsonSerializer OrderProbe()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DefaultValueHandling = DefaultValueHandling.Include
            };
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new StatusKeyRefJsonConverter());
            return JsonSerializer.Create(settings);
        }

        private static List<string> PropertyOrder(object instance)
        {
            var order = new List<string>();
            foreach (var property in JObject.FromObject(instance, OrderProbe()).Properties())
            {
                order.Add(property.Name);
            }

            return order;
        }

        private static JObject BuildSchema()
        {
            var schema = new JObject();
            schema["effects"] = BuildEffects();
            schema["interventions"] = BuildInterventions();
            schema["condition"] = BuildCondition();
            schema["cardFields"] = BuildCardFields();
            schema["sides"] = Names(typeof(Side));
            schema["categories"] = Names(typeof(CardCategory));
            schema["grades"] = Names(typeof(CardGrade));
            schema["selectors"] = Names(typeof(TargetSelectorRef));
            schema["statusTargets"] = Names(typeof(StatusApplyTarget));
            return schema;
        }

        /// <summary>카드 분류마다 키 순서가 다르다 — 계획 3.5가 CardSpec을 두 타입으로 쪼갰기
        /// 때문이다. 분류 이름을 키로 쓰므로 노트북이 card.category로 바로 색인할 수 있다.</summary>
        private static JObject BuildCardFields()
        {
            var fields = new JObject();
            fields[CardCategory.Execution.ToString()] =
                new JArray(PropertyOrder(new ExecutionCardSpec()).ToArray());
            fields[CardCategory.Intervention.ToString()] =
                new JArray(PropertyOrder(new InterventionCardSpec()).ToArray());
            return fields;
        }

        private static JArray BuildEffects()
        {
            var effects = new JArray();
            foreach (var info in EffectSpecCatalog.All())
            {
                var entry = new JObject();
                entry["kind"] = info.Create().Key.Id;
                entry["label"] = info.DisplayName;

                var fields = new JArray();
                foreach (var name in PropertyOrder(info.Create()))
                {
                    if (name == "condition")
                    {
                        continue;
                    }

                    fields.Add(DescribeField(info.SpecType, name));
                }

                entry["fields"] = fields;
                effects.Add(entry);
            }

            return effects;
        }

        private static JObject BuildCondition()
        {
            var fields = new JArray();
            foreach (var name in PropertyOrder(new ConditionSpec()))
            {
                if (name == "kind")
                {
                    continue;
                }

                fields.Add(DescribeField(typeof(ConditionSpec), name));
            }

            var condition = new JObject();
            condition["kinds"] = Names(typeof(ConditionKind));
            condition["fields"] = fields;
            return condition;
        }

        /// <summary>개입은 효과와 같은 모양으로 낸다 — 계획 3.5가 InterventionSpec을 EffectSpec처럼
        /// 다형화했으므로, 노트북의 개입 폼이 효과 행 렌더러를 그대로 재사용할 수 있다.
        /// 조건을 걸러내지 않는 것이 효과와 유일하게 다른 점이다 — 개입에는 조건 시스템이 없다.</summary>
        private static JArray BuildInterventions()
        {
            var interventions = new JArray();
            foreach (var info in InterventionSpecCatalog.All())
            {
                var entry = new JObject();
                entry["kind"] = info.Create().Key.Id;
                entry["label"] = info.DisplayName;

                var fields = new JArray();
                foreach (var name in PropertyOrder(info.Create()))
                {
                    fields.Add(DescribeField(info.SpecType, name));
                }

                entry["fields"] = fields;
                interventions.Add(entry);
            }

            return interventions;
        }

        /// <summary>필드 하나를 노트북이 폼 컨트롤로 바꿀 수 있는 형태로 옮긴다. 모르는 타입에서
        /// 던지는 것이 핵심이다 — 노트북이 그릴 수 없는 필드를 C#에 추가하면 여기서 걸린다.</summary>
        private static JObject DescribeField(Type owner, string camelName)
        {
            var field = FieldFor(owner, camelName);
            var entry = new JObject();
            entry["name"] = camelName;

            var type = field.FieldType;
            if (type == typeof(int))
            {
                entry["type"] = "int";
            }
            else if (type == typeof(bool))
            {
                entry["type"] = "bool";
            }
            else if (type == typeof(StatusKeyRef))
            {
                entry["type"] = "status";
            }
            else if (type.IsEnum)
            {
                entry["type"] = "enum";
                entry["options"] = Names(type);
            }
            else
            {
                throw new InvalidOperationException(
                    "노트북이 그릴 수 없는 저작 필드 타입이다: " + owner.Name + "." + field.Name
                    + " (" + type.Name + "). authoring-schema의 타입 표를 넓히거나 필드를 바꿔라.");
            }

            return entry;
        }

        private static FieldInfo FieldFor(Type owner, string camelName)
        {
            foreach (var field in owner.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(field.Name, camelName, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }

            throw new InvalidOperationException(
                "필드를 찾지 못했다: " + owner.Name + "." + camelName);
        }

        private static JArray Names(Type enumType) => new JArray(Enum.GetNames(enumType));
    }
}
