using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Unity;
using FateWeaver.Unity.Editor;
using NUnit.Framework;
using UnityEditor;

namespace FateWeaver.Tests.UnityEditMode
{
    public class StarterPoolSeederTests
    {
        private string _root;
        private string _cardFolder;
        private string _poolPath;

        private static readonly IReadOnlyDictionary<string, string[]> ExpectedTags =
            new Dictionary<string, string[]>
            {
                ["vanguard_slash"] = new[] { "시작", "공격" },
                ["parry_strike"] = new[] { "시작", "방어" },
                ["hasten"] = new[] { "시작", "실행력" },
                ["probing_strike"] = new[] { "시작", "공격" },
                ["quick_cover"] = new[] { "시작", "방어" },
                ["delay"] = new[] { "시작", "실행력" },
                ["delayed_strike"] = new[] { "시작", "공격" },
                ["early_guard"] = new[] { "시작", "방어" },
                ["crossover"] = new[] { "시작", "실행력" },
                ["riposte"] = new[] { "시작", "공격", "조건" },
                ["foresight"] = new[] { "시작", "방어", "조건" },
                ["breather"] = new[] { "시작", "실행력" },
                ["venom_thrust"] = new[] { "시작", "독", "성장형" },
                ["last_drop"] = new[] { "시작", "독", "성장형", "조건" },
                ["spore_veil"] = new[] { "시작", "독", "성장형", "방어" },
                ["spread_culture"] = new[] { "시작", "독", "성장형", "광역" },
                ["toxic_reclaim"] = new[] { "시작", "독", "소비형", "방어" },
                ["condensed_burst"] = new[] { "시작", "독", "소비형", "공격" },
                ["distill"] = new[] { "시작", "독", "소비형", "운명력" },
                ["early_onset"] = new[] { "시작", "독", "변이형", "발동" },
                ["stable_culture"] = new[] { "시작", "독", "변이형", "성장" },
                ["posthumous_spread"] = new[] { "시작", "독", "변이형", "이전" }
            };

        [SetUp]
        public void SetUp()
        {
            _root = $"Assets/Tests/Temp/StarterPoolSeeder-{Guid.NewGuid():N}";
            _cardFolder = _root + "/Cards";
            _poolPath = _root + "/StarterPool.asset";
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_root);
            AssetDatabase.Refresh();
        }

        [Test]
        public void First_seed_creates_the_exact_valid_22_card_pool()
        {
            var errors = CardCodeGenerator.SeedStarterPoolAssets(_cardFolder, _poolPath);

            CollectionAssert.IsEmpty(errors);
            var pool = AssetDatabase.LoadAssetAtPath<CardPoolAsset>(_poolPath);
            Assert.NotNull(pool);
            Assert.AreEqual("starter_pool", pool.Id);
            Assert.AreEqual(22, pool.Cards.Count);
            CollectionAssert.IsEmpty(pool.Validate());
            CollectionAssert.AreEqual(
                StarterPoolSpecs.Build().Select(spec => spec.Id).ToArray(),
                pool.Cards.Select(card => card.Id).ToArray());

            foreach (var card in pool.Cards)
            {
                Assert.AreEqual(CardGrade.Common, card.Grade, card.Id);
                CollectionAssert.AreEqual(ExpectedTags[card.Id], card.Tags, card.Id);
            }

            AssertInterventionConstraints(pool);
        }

        [Test]
        public void Reseed_preserves_existing_authored_card_fields()
        {
            CollectionAssert.IsEmpty(
                CardCodeGenerator.SeedStarterPoolAssets(_cardFolder, _poolPath));
            var cardPath = _cardFolder + "/vanguard_slash.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardAsset>(cardPath);
            card.EnergyCost = 99;
            card.Description = "인스펙터에서 조정한 설명";
            var serialized = new SerializedObject(card);
            serialized.FindProperty("_grade").enumValueIndex = (int)CardGrade.Rare;
            var tags = serialized.FindProperty("_tags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).stringValue = "수동태그";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
            AssetDatabase.SaveAssets();

            CollectionAssert.IsEmpty(
                CardCodeGenerator.SeedStarterPoolAssets(_cardFolder, _poolPath));
            card = AssetDatabase.LoadAssetAtPath<CardAsset>(cardPath);

            Assert.AreEqual(99, card.EnergyCost);
            Assert.AreEqual("인스펙터에서 조정한 설명", card.Description);
            Assert.AreEqual(CardGrade.Rare, card.Grade);
            CollectionAssert.AreEqual(new[] { "수동태그" }, card.Tags);
        }

        private static void AssertInterventionConstraints(CardPoolAsset pool)
        {
            var specs = pool.ToSpecs().ToDictionary(spec => spec.Id);
            Assert.AreEqual(
                InterventionTargetSideRef.Player,
                specs["hasten"].InterventionTargetSide);
            Assert.AreEqual(
                InterventionTargetSideRef.Enemy,
                specs["delay"].InterventionTargetSide);
            Assert.AreEqual(
                InterventionTargetSideRef.Player,
                specs["breather"].InterventionTargetSide);
            Assert.AreEqual(
                InterventionTargetSideRef.Any,
                specs["crossover"].InterventionTargetSide);
            Assert.IsTrue(specs["crossover"].InterventionRequireAdjacent);
        }
    }
}
