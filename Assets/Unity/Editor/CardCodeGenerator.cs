using System.Collections.Generic;
using System.IO;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>Edit-time bridge: (1) seeds the starter cards as CardAsset/DeckAsset from the pure
    /// StarterDeckSpecs, and (2) generates pure-C# CardSpec literals so the headless sims read the same
    /// authored cards. The generated file lives under Simulation (compiled by Unity AND headless).</summary>
    public static class CardCodeGenerator
    {
        private const string CardFolder = "Assets/Unity/CardSO";
        private const string PlayerCardFolder = CardFolder + "/Player";
        public const string EnemyCardFolder = CardFolder + "/Enemies";
        private const string DeckAssetPath = PlayerCardFolder + "/StarterDeck.asset";
        private const string ValidationCardFolder = CardFolder + "/Validation";
        private const string ValidationDeckPath = ValidationCardFolder + "/PartyPrototypeDeck.asset";
        private const string CharacterFolder = "Assets/Unity/CharacterSO";
        private const string MemberAPath = CharacterFolder + "/member_a.asset";
        private const string MemberBPath = CharacterFolder + "/member_b.asset";
        private const string GeneratedPath = "Assets/Core/Simulation/Generated/GeneratedCards.cs";

        [MenuItem("Fate Weaver/Seed Starter Card Assets")]
        public static void SeedStarter()
        {
            Directory.CreateDirectory(PlayerCardFolder);
            var deck = ScriptableObject.CreateInstance<DeckAsset>();
            deck.Id = "starter";
            var entries = new List<DeckAsset.Entry>();

            foreach (var spec in DistinctById(StarterDeckSpecs.Build(), out var counts))
            {
                var card = ScriptableObject.CreateInstance<CardAsset>();
                Apply(card, spec);
                var path = PlayerCardFolder + "/" + spec.Id + ".asset";
                AssetDatabase.CreateAsset(card, path);
                entries.Add(new DeckAsset.Entry { Card = card, Count = counts[spec.Id] });
            }

            deck.Entries = entries.ToArray();
            AssetDatabase.CreateAsset(deck, DeckAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Seeded starter CardAssets + DeckAsset under " + CardFolder);
        }

        /// <summary>Seeds one CardAsset per enemy card (art/display source only — rules stay in GoblinDeck).
        /// Idempotent: re-running refreshes id/name/etc. but preserves any Art you assigned in the Inspector.</summary>
        [MenuItem("Fate Weaver/Seed Enemy Card Assets")]
        public static void SeedEnemy()
        {
            Directory.CreateDirectory(EnemyCardFolder);
            foreach (var def in GoblinDeck.AllCards())
            {
                var path = EnemyCardFolder + "/" + def.Id + ".asset";
                var card = AssetDatabase.LoadAssetAtPath<CardAsset>(path);
                bool isNew = card == null;
                if (isNew)
                {
                    card = ScriptableObject.CreateInstance<CardAsset>();
                }

                ApplyDefinition(card, def);
                if (isNew)
                {
                    AssetDatabase.CreateAsset(card, path);
                }
                else
                {
                    EditorUtility.SetDirty(card);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Seeded enemy CardAssets under " + EnemyCardFolder + " — assign each card's Art in the Inspector, then rebuild the scene.");
        }

        [MenuItem("Fate Weaver/Generate Cards from SO")]
        public static void Generate()
        {
            var deck = AssetDatabase.LoadAssetAtPath<DeckAsset>(DeckAssetPath);
            if (deck == null)
            {
                Debug.LogError("No DeckAsset at " + DeckAssetPath + " — run 'Fate Weaver/Seed Starter Card Assets' first.");
                return;
            }

            var specs = deck.ToSpecs();
            var errors = AuthoringValidator.Validate(specs, AuthoringContext.Default());
            if (errors.Count > 0)
            {
                Debug.LogError("Card validation failed:\n" + string.Join("\n", errors));
                return;
            }

            Directory.CreateDirectory("Assets/Core/Simulation/Generated");
            File.WriteAllText(GeneratedPath, Emit(specs), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("Generated " + GeneratedPath);
        }

        [MenuItem("Fate Weaver/Seed Party Prototype Assets")]
        public static void SeedPartyPrototype()
        {
            var starterDeck = AssetDatabase.LoadAssetAtPath<DeckAsset>(DeckAssetPath);
            if (starterDeck == null)
            {
                Debug.LogError("No DeckAsset at " + DeckAssetPath + " — run 'Fate Weaver/Seed Starter Card Assets' first.");
                return;
            }

            Directory.CreateDirectory(ValidationCardFolder);
            Directory.CreateDirectory(CharacterFolder);
            var entries = new List<DeckAsset.Entry>();
            foreach (var spec in DistinctById(PartyPrototypeDeckSpecs.Build(), out var counts))
            {
                var card = UpsertCard(ValidationCardFolder + "/" + spec.Id + ".asset", spec);
                entries.Add(new DeckAsset.Entry { Card = card, Count = counts[spec.Id] });
            }

            var validationDeck = AssetDatabase.LoadAssetAtPath<DeckAsset>(ValidationDeckPath);
            bool newDeck = validationDeck == null;
            if (newDeck)
            {
                validationDeck = ScriptableObject.CreateInstance<DeckAsset>();
            }

            validationDeck.Id = "party_prototype";
            validationDeck.Entries = entries.ToArray();
            if (newDeck)
            {
                AssetDatabase.CreateAsset(validationDeck, ValidationDeckPath);
            }
            else
            {
                EditorUtility.SetDirty(validationDeck);
            }

            UpsertCharacter(
                MemberAPath,
                PartyPrototypeRoster.MemberAId,
                PartyPrototypeRoster.MemberAName,
                new Color(0.35f, 0.65f, 0.95f, 1f),
                starterDeck);
            UpsertCharacter(
                MemberBPath,
                PartyPrototypeRoster.MemberBId,
                PartyPrototypeRoster.MemberBName,
                new Color(0.90f, 0.62f, 0.25f, 1f),
                validationDeck);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Seeded party prototype CardAssets, DeckAsset, and CharacterAssets.");
        }

        private static CardAsset UpsertCard(string path, CardSpec spec)
        {
            var card = AssetDatabase.LoadAssetAtPath<CardAsset>(path);
            bool isNew = card == null;
            if (isNew)
            {
                card = ScriptableObject.CreateInstance<CardAsset>();
            }

            // Apply intentionally leaves CardAsset.Art untouched, preserving assigned art on repeated seeds.
            Apply(card, spec);
            if (isNew)
            {
                AssetDatabase.CreateAsset(card, path);
            }
            else
            {
                EditorUtility.SetDirty(card);
            }

            return card;
        }

        private static void UpsertCharacter(
            string path,
            string id,
            string displayName,
            Color color,
            DeckAsset deck)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterAsset>(path);
            bool isNew = character == null;
            if (isNew)
            {
                character = ScriptableObject.CreateInstance<CharacterAsset>();
            }

            var serialized = new SerializedObject(character);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_color").colorValue = color;
            serialized.FindProperty("_deck").objectReferenceValue = deck;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
            {
                AssetDatabase.CreateAsset(character, path);
            }
            else
            {
                EditorUtility.SetDirty(character);
            }
        }

        private static void Apply(CardAsset card, CardSpec spec)
        {
            card.Id = spec.Id;
            card.DisplayName = spec.Name;
            card.Side = spec.Side;
            card.Category = spec.Category;
            card.EnergyCost = spec.EnergyCost;
            card.BaseExecutionOrder = spec.BaseExecutionOrder;
            card.Effects = spec.Effects ?? System.Array.Empty<EffectSpec>();
            card.Intervention = spec.Intervention;
            card.InterventionEffectValue = spec.InterventionEffectValue;
        }

        // Enemy CardAssets mirror the pure GoblinDeck definition for id/display, but carry no rules
        // (GoblinDeck owns those). Effects stay empty so nobody mistakes this for the rules source.
        private static void ApplyDefinition(CardAsset card, CardDefinition def)
        {
            card.Id = def.Id;
            card.DisplayName = def.Name;
            card.Side = def.Side;
            card.Category = def.Category;
            card.EnergyCost = def.EnergyCost;
            card.BaseExecutionOrder = def.BaseExecutionOrder;
            card.Effects = System.Array.Empty<EffectSpec>();
            card.Intervention = default;
        }

        private static IEnumerable<CardSpec> DistinctById(IReadOnlyList<CardSpec> specs, out Dictionary<string, int> counts)
        {
            counts = new Dictionary<string, int>();
            var order = new List<CardSpec>();
            foreach (var spec in specs)
            {
                if (!counts.ContainsKey(spec.Id))
                {
                    counts[spec.Id] = 0;
                    order.Add(spec);
                }

                counts[spec.Id]++;
            }

            return order;
        }

        private static string Emit(IReadOnlyList<CardSpec> specs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED by Fate Weaver/Generate Cards from SO. Do not edit by hand.");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using FateWeaver.Core.Cards;");
            sb.AppendLine("using FateWeaver.Core.Effects;");
            sb.AppendLine("using FateWeaver.Core.Status;");
            sb.AppendLine("using FateWeaver.Simulation.Authoring;");
            sb.AppendLine();
            sb.AppendLine("namespace FateWeaver.Simulation.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedCards");
            sb.AppendLine("    {");
            sb.AppendLine("        public static IReadOnlyList<CardSpec> StarterDeck() => new List<CardSpec>");
            sb.AppendLine("        {");
            foreach (var spec in specs)
            {
                sb.Append("            ").AppendLine(EmitSpec(spec) + ",");
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EmitSpec(CardSpec s)
        {
            var sb = new StringBuilder();
            sb.Append("new CardSpec { ");
            sb.Append("Id = ").Append(Quote(s.Id)).Append(", ");
            sb.Append("Name = ").Append(Quote(s.Name)).Append(", ");
            sb.Append("Side = Side.").Append(s.Side).Append(", ");
            sb.Append("Category = CardCategory.").Append(s.Category).Append(", ");
            sb.Append("EnergyCost = ").Append(s.EnergyCost).Append(", ");
            sb.Append("BaseExecutionOrder = ").Append(s.BaseExecutionOrder).Append(", ");
            if (!s.Intervention.IsEmpty)
            {
                sb.Append("Intervention = new InterventionKeyRef { Id = ").Append(Quote(s.Intervention.Id)).Append(" }, ");
            }
            sb.Append("InterventionEffectValue = ").Append(s.InterventionEffectValue).Append(", ");
            sb.Append("Effects = new EffectSpec[] { ");
            var effects = s.Effects ?? System.Array.Empty<EffectSpec>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(EmitEffect(effects[i]));
            }
            sb.Append(" } }");
            return sb.ToString();
        }

        private static string EmitEffect(EffectSpec e)
        {
            return e.ToLiteral();
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
