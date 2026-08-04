using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Captures the living object references each executing card may affect. Effects use
    /// this immutable target membership for the entire card, so an earlier effect cannot promote a
    /// replacement target after a captured unit dies or the formation moves.</summary>
    public sealed class CardTargetSnapshot
    {
        private static readonly IReadOnlyList<PartyMember> NoPartyTargets = Array.Empty<PartyMember>();
        private static readonly IReadOnlyList<Enemy> NoEnemyTargets = Array.Empty<Enemy>();

        private readonly Dictionary<CardTargetKey, IReadOnlyList<PartyMember>> _partyTargets = new();
        private readonly Dictionary<CardTargetKey, IReadOnlyList<Enemy>> _enemyTargets = new();

        public static CardTargetSnapshot Capture(
            CombatState state,
            ExecutionCardInstance card,
            IEnumerable<CardTargetKey> targetKeys)
            => Capture(state, card, targetKeys, Array.Empty<CardTargetKey>());

        /// <summary>Captures positional targets, optionally retaining the legacy explicit enemy id
        /// for effect keys that did not author a TargetSelector. Selector-bearing effects always
        /// use their declared range.</summary>
        public static CardTargetSnapshot Capture(
            CombatState state,
            ExecutionCardInstance card,
            IEnumerable<CardTargetKey> targetKeys,
            IEnumerable<CardTargetKey> legacyExplicitTargetKeys)
        {
            var snapshot = new CardTargetSnapshot();
            var ranges = new Dictionary<CardTargetFaction, CardTargetRange>();
            var legacyExplicitKeys = new HashSet<CardTargetKey>(legacyExplicitTargetKeys);

            foreach (var key in targetKeys)
            {
                if (ranges.TryGetValue(key.Faction, out var existing) && existing != key.Range)
                {
                    card.CancellationReason = CardCancellationReason.NoValidTarget;
                    return snapshot;
                }

                ranges[key.Faction] = key.Range;
            }

            foreach (var pair in ranges)
            {
                var key = new CardTargetKey(pair.Key, pair.Value);
                if (key.Faction == CardTargetFaction.Ally)
                {
                    var targets = CapturePartyTargets(state, card, key.Range);
                    if (targets == null)
                    {
                        card.CancellationReason = CardCancellationReason.NoValidTarget;
                        return snapshot;
                    }

                    snapshot._partyTargets.Add(key, targets);
                }
                else
                {
                    var targets = CaptureEnemyTargets(
                        state, card, key.Range, legacyExplicitKeys.Contains(key));
                    if (targets == null)
                    {
                        card.CancellationReason = CardCancellationReason.NoValidTarget;
                        return snapshot;
                    }

                    snapshot._enemyTargets.Add(key, targets);
                }
            }

            return snapshot;
        }

        public IReadOnlyList<PartyMember> PartyTargets(CardTargetKey key)
        {
            if (key.Faction != CardTargetFaction.Ally)
            {
                throw new ArgumentException("Party targets require the Ally faction.", nameof(key));
            }

            return _partyTargets.TryGetValue(key, out var targets) ? targets : NoPartyTargets;
        }

        public IReadOnlyList<Enemy> EnemyTargets(CardTargetKey key)
        {
            if (key.Faction != CardTargetFaction.Enemy)
            {
                throw new ArgumentException("Enemy targets require the Enemy faction.", nameof(key));
            }

            return _enemyTargets.TryGetValue(key, out var targets) ? targets : NoEnemyTargets;
        }

        public static CardTargetRange RangeFor(TargetSelector selector)
        {
            switch (selector)
            {
                case TargetSelector.FrontOne: return CardTargetRange.FrontOne;
                case TargetSelector.FrontTwo: return CardTargetRange.FrontTwo;
                case TargetSelector.BackOne: return CardTargetRange.BackOne;
                case TargetSelector.BackTwo: return CardTargetRange.BackTwo;
                case TargetSelector.All: return CardTargetRange.All;
                default: throw new ArgumentOutOfRangeException(nameof(selector));
            }
        }

        private static IReadOnlyList<PartyMember> CapturePartyTargets(
            CombatState state,
            ExecutionCardInstance card,
            CardTargetRange range)
        {
            if (range == CardTargetRange.Self)
            {
                var self = ResolvePartyOwner(state, card.OwnerId);
                return self == null ? null : new List<PartyMember> { self }.AsReadOnly();
            }

            var targets = PartyTargeting.SelectRange(state, ToSelector(range));
            return targets.Count == 0 ? null : targets.AsReadOnly();
        }

        private static IReadOnlyList<Enemy> CaptureEnemyTargets(
            CombatState state,
            ExecutionCardInstance card,
            CardTargetRange range,
            bool useLegacyExplicitTarget)
        {
            if (range == CardTargetRange.Self)
            {
                var self = ResolveEnemyOwner(state, card.OwnerId);
                return self == null ? null : new List<Enemy> { self }.AsReadOnly();
            }

            if (useLegacyExplicitTarget && card.Def.Side == Side.Player && !string.IsNullOrEmpty(card.TargetId))
            {
                var explicitTarget = EnemyTargeting.ByIdOrFront(state, card.TargetId);
                return explicitTarget == null || explicitTarget.Hp <= 0
                    ? null
                    : new List<Enemy> { explicitTarget }.AsReadOnly();
            }

            var targets = EnemyTargeting.SelectRange(state, ToSelector(range));
            return targets.Count == 0 ? null : targets.AsReadOnly();
        }

        private static PartyMember ResolvePartyOwner(CombatState state, string ownerId)
        {
            PartyMember resolved = null;
            foreach (var member in state.Party)
            {
                if (member.IsAlive && (string.IsNullOrEmpty(ownerId) || member.Id == ownerId))
                {
                    if (resolved != null)
                    {
                        return null;
                    }

                    resolved = member;
                }
            }

            return resolved;
        }

        private static Enemy ResolveEnemyOwner(CombatState state, string ownerId)
        {
            Enemy resolved = null;
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Hp > 0 && (string.IsNullOrEmpty(ownerId) || enemy.Id == ownerId))
                {
                    if (resolved != null)
                    {
                        return null;
                    }

                    resolved = enemy;
                }
            }

            return resolved;
        }

        private static TargetSelector ToSelector(CardTargetRange range)
        {
            switch (range)
            {
                case CardTargetRange.FrontOne: return TargetSelector.FrontOne;
                case CardTargetRange.FrontTwo: return TargetSelector.FrontTwo;
                case CardTargetRange.BackOne: return TargetSelector.BackOne;
                case CardTargetRange.BackTwo: return TargetSelector.BackTwo;
                case CardTargetRange.All: return TargetSelector.All;
                default: throw new ArgumentOutOfRangeException(nameof(range));
            }
        }
    }
}
