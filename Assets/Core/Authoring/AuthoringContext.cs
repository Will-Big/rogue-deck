using System.Collections.Generic;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Registry lookups for authoring-time validation (editor and boot use the same checks).</summary>
    public sealed class AuthoringContext
    {
        private readonly EffectRegistry _effects;
        private readonly StatusRegistry _statuses;
        private readonly InterventionActionRegistry _interventions;

        public AuthoringContext(
            EffectRegistry effects,
            StatusRegistry statuses,
            InterventionActionRegistry interventions)
        {
            _effects = effects;
            _statuses = statuses;
            _interventions = interventions;
        }

        public static AuthoringContext Default()
            => new AuthoringContext(
                CombatRegistries.Effects(),
                CombatRegistries.Statuses(),
                CombatRegistries.InterventionActions());

        public IReadOnlyList<StatusKey> RegisteredStatusKeys => _statuses.RegisteredKeys;
        public bool HasStatus(StatusKey key) => _statuses.TryResolve(key, out _);
        public bool HasEffect(EffectKey key) => _effects.Contains(key);
        public bool HasIntervention(InterventionActionKey key) => _interventions.Contains(key);
    }
}
