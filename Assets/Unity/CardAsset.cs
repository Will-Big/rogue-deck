using System;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Authoring;
using UnityEngine;
using UnityEngine.Serialization;

namespace FateWeaver.Unity
{
    /// <summary>Inspector-authored card. The single source of truth for card data; converts to a pure
    /// CardSpec for the rules layer. Art/Description are presentation-only (not part of CardSpec).</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Card", fileName = "Card")]
    public sealed class CardAsset : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Side Side;
        public CardCategory Category;
        [FormerlySerializedAs("Cost")]
        public int EnergyCost = 1;
        [FormerlySerializedAs("BaseInitiative")]
        public int BaseExecutionOrder = 5;
        public Sprite Art;
        [TextArea] public string Description;
        [SerializeReference] public EffectSpec[] Effects = Array.Empty<EffectSpec>();
        public InterventionKeyRef Intervention;
        [FormerlySerializedAs("InterventionAmount")]
        [FormerlySerializedAs("FateAmount")]
        public int InterventionEffectValue;

        public CardSpec ToSpec() => new CardSpec
        {
            Id = Id,
            Name = DisplayName,
            Side = Side,
            Category = Category,
            EnergyCost = EnergyCost,
            BaseExecutionOrder = BaseExecutionOrder,
            Effects = Effects,
            Intervention = Intervention,
            InterventionEffectValue = InterventionEffectValue
        };
    }
}
