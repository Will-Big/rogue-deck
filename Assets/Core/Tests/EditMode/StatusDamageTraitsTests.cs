using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>상태가 주는 피해의 관통·배율 미적용은 원인(상태)이 아니라 그 상태의 저작 데이터가 정한다
    /// (계획 D10). 독처럼 적은 상태만 방어를 뚫고, 적지 않은 상태 피해는 공격처럼 방어·취약을 거친다.
    /// 카드 피해도 같은 속성 목록(traits)을 가질 수 있다.</summary>
    public class StatusDamageTraitsTests
    {
        private static readonly StatusKey Burn = new StatusKey("test_burn");

        /// <summary>턴 종료에 수치만큼 피해를 주는 테스트 전용 상태.</summary>
        private sealed class BurnBehavior : StatusBehavior
        {
            public override StatusKey Key => Burn;
            public override StatusScope Scope => StatusScope.Entity;
            public override void OnTurnEnd(StatusTickContext ctx) => ctx.DealDamage(ctx.Instance.Magnitude);
        }

        private static StatusContentCatalog Catalog(params DamageTrait[] burnTraits)
            => new StatusContentCatalog(new Dictionary<StatusKey, StatusSpec>
            {
                [Burn] = new StatusSpec
                {
                    Key = StatusKeyRef.Of(Burn), DisplayName = "화상", Lifetime = StatusLifetimeKind.Permanent,
                    DamageTraits = burnTraits
                },
                [StatusKeys.Block] = new StatusSpec
                {
                    Key = StatusKeyRef.Of(StatusKeys.Block), DisplayName = "방어", Lifetime = StatusLifetimeKind.Permanent
                },
                [StatusKeys.Vulnerable] = new MultiplierStatusSpec
                {
                    Key = StatusKeyRef.Of(StatusKeys.Vulnerable), DisplayName = "취약",
                    Lifetime = StatusLifetimeKind.Turns, MultiplierPercent = 150
                }
            });

        private static Enemy BurningEnemy(CombatState state, int block, bool vulnerable)
        {
            state.AddSoloPlayer(10);
            var enemy = new Enemy("a", 20);
            enemy.Statuses.Stack(Burn, StatusLifetime.Permanent, 4);
            if (block > 0) enemy.Statuses.Stack(StatusKeys.Block, StatusLifetime.Permanent, block);
            if (vulnerable) enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            return enemy;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new BurnBehavior());
            statuses.Register(new BlockBehavior());
            statuses.Register(new VulnerableBehavior());
            return statuses;
        }

        [Test]
        public void Status_damage_without_authored_traits_is_absorbed_by_block()
        {
            var state = new CombatState(Catalog());
            var enemy = BurningEnemy(state, block: 3, vulnerable: false);

            new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(19, enemy.Hp, "화상 4 중 3은 방어가 흡수한다");
            Assert.AreEqual(0, enemy.Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Status_damage_without_authored_traits_takes_multipliers()
        {
            var state = new CombatState(Catalog());
            var enemy = BurningEnemy(state, block: 0, vulnerable: true);

            new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(14, enemy.Hp, "화상 4 × 취약 150% = 6");
        }

        [Test]
        public void Authored_piercing_skips_block_and_authored_ignore_skips_multipliers()
        {
            var state = new CombatState(Catalog(DamageTrait.Piercing, DamageTrait.IgnoresMultipliers));
            var enemy = BurningEnemy(state, block: 3, vulnerable: true);

            new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(16, enemy.Hp);
            Assert.AreEqual(3, enemy.Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Damage_traits_round_trip_through_status_json()
        {
            var spec = new PoisonStatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Poison), DisplayName = "독", Lifetime = StatusLifetimeKind.Permanent,
                GrowthPerTurn = 1, DamageTraits = new[] { DamageTrait.Piercing, DamageTrait.IgnoresMultipliers }
            };

            var json = FateWeaver.Core.Authoring.Json.ContentJson.Write(spec);
            var restored = FateWeaver.Core.Authoring.Json.ContentJson.Read<StatusSpec>(json);

            StringAssert.Contains("\"damageTraits\"", json);
            Assert.AreEqual(
                DamageTraits.Of(DamageTrait.Piercing, DamageTrait.IgnoresMultipliers),
                DamageTraits.Of(restored.DamageTraits));
        }

        [Test]
        public void An_empty_trait_list_is_not_written()
        {
            var spec = new StatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Block), DisplayName = "방어", Lifetime = StatusLifetimeKind.ThisTurn,
                DamageTraits = new DamageTrait[0]
            };

            StringAssert.DoesNotContain("damageTraits", FateWeaver.Core.Authoring.Json.ContentJson.Write(spec));
        }

        // --- 카드 피해의 속성 -------------------------------------------------------------------

        private const string PiercingStrikeJson = @"{
  ""cardFormat"": 2,
  ""id"": ""piercing_fixture"",
  ""name"": ""관통 검증"",
  ""side"": ""Player"",
  ""category"": ""Execution"",
  ""energyCost"": 1,
  ""baseExecutionOrder"": 5,
  ""targets"": { ""enemy"": ""FrontOne"" },
  ""effects"": [
    { ""kind"": ""damage"", ""id"": ""e0"", ""targetFaction"": ""Enemy"", ""value"": 4,
      ""traits"": [ ""Piercing"", ""IgnoresMultipliers"" ] }
  ]
}";

        [Test]
        public void A_card_damage_effect_carries_several_traits_from_json()
        {
            var spec = FateWeaver.Core.Authoring.Json.ContentJson.Read<CardSpec>(PiercingStrikeJson);
            var def = CardSpecMapper.ToDefinition(spec);

            var payload = (DamagePayload)def.Effects[0].Payload;
            Assert.AreEqual(DamageTraits.Of(DamageTrait.Piercing, DamageTrait.IgnoresMultipliers), payload.Traits);
        }

        [Test]
        public void A_card_damage_effect_without_traits_is_normal_damage()
        {
            var def = CardSpecMapper.ToDefinition(FateWeaver.Core.Authoring.Json.ContentJson.Read<CardSpec>(
                PiercingStrikeJson.Replace(@",
      ""traits"": [ ""Piercing"", ""IgnoresMultipliers"" ]", "")));

            Assert.IsNull(def.Effects[0].Payload, "속성이 없으면 페이로드를 두지 않는다 — 보통 피해");
        }

        [Test]
        public void Piercing_card_damage_skips_block_and_ignores_vulnerable()
        {
            var state = new CombatState(Catalog());
            state.AddSoloPlayer(10);
            var enemy = new Enemy("a", 20);
            enemy.Statuses.Stack(StatusKeys.Block, StatusLifetime.Permanent, 3);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            var def = CardSpecMapper.ToDefinition(FateWeaver.Core.Authoring.Json.ContentJson.Read<CardSpec>(PiercingStrikeJson));
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            new TurnResolver(effects, Statuses()).Resolve(state, 0);

            Assert.AreEqual(16, enemy.Hp);
            Assert.AreEqual(3, enemy.Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Card_damage_traits_round_trip_and_repeat_is_rejected()
        {
            var spec = FateWeaver.Core.Authoring.Json.ContentJson.Read<CardSpec>(PiercingStrikeJson);
            var json = FateWeaver.Core.Authoring.Json.ContentJson.Write(spec);
            StringAssert.Contains("\"traits\"", json);
            CollectionAssert.AreEqual(
                new[] { DamageTrait.Piercing, DamageTrait.IgnoresMultipliers },
                ((DamageSpec)((ExecutionCardSpec)FateWeaver.Core.Authoring.Json.ContentJson.Read<CardSpec>(json)).Effects[0]).Traits);

            var twice = new DamageSpec { Id = "e0", Value = 1, Traits = new[] { DamageTrait.Piercing, DamageTrait.Piercing } };
            CollectionAssert.IsNotEmpty(twice.Validate(AuthoringContext.Default()));
        }

        [Test]
        public void Card_description_names_the_damage_traits()
        {
            var def = CardSpecMapper.ToDefinition(FateWeaver.Core.Authoring.Json.ContentJson.Read<CardSpec>(PiercingStrikeJson));
            var catalog = FateWeaver.Simulation.Descriptions.KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses());

            StringAssert.Contains(
                "피해 4 (관통, 배율 무시)",
                FateWeaver.Simulation.Descriptions.DescriptionComposer.Describe(def, catalog));
        }
    }
}
