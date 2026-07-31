using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Serializable reference to an open-set intervention action key. Uniform {key, value}
    /// params today; promote to polymorphic specs (like EffectSpec) only when an action needs
    /// unique parameters (설계 문서 §4.1).</summary>
    [Serializable]
    public struct InterventionKeyRef
    {
        public string Id;

        public bool IsEmpty => string.IsNullOrEmpty(Id);
        public InterventionActionKey ToKey() => new InterventionActionKey(Id);
        public static InterventionKeyRef Of(InterventionActionKey key) => new InterventionKeyRef { Id = key.Id };
    }
}
