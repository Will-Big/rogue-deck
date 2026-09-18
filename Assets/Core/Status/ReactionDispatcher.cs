using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>한 경계(Primary 효과 하나, 또는 턴 시점 처리 한 묶음)의 사건에 직접 반응한다(전투 실행 계약 스펙 §7).
    /// 사건은 (효과 시작 대상 목록 순번, 발생 순서)로, 한 보유자 안에서는 상태 부여 순서로 처리한다. 후보는 반응을
    /// 실행하기 전에 한 번에 확보한다 — 도중에 새로 얻은 능력은 이미 난 사건을 받지 않고, 도중에 사라진 상태는
    /// 호출 전에 거른다. 반응 효과는 Reaction 기원으로 실행되고, 그 사건에는 RespondsToReactionEvents인 능력
    /// (사망 시 반응)만 반응한다 — 반응 공격이 반응 공격을 부르지 않는다(계획 D11).</summary>
    public sealed class ReactionDispatcher
    {
        private readonly ReactionRegistry _reactions;
        private readonly EffectExecutor _executor;
        private readonly EffectTargetResolver _targets = new EffectTargetResolver();

        public ReactionDispatcher(ReactionRegistry reactions, EffectExecutor executor)
        {
            _reactions = reactions ?? new ReactionRegistry();
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>이 기원의 사건에 이 능력이 반응할 수 있는가. Primary 사건에는 모든 능력이, Reaction 사건에는
        /// RespondsToReactionEvents인 능력만 반응한다.</summary>
        public static bool Allows(EffectOrigin origin, IReactionHandler handler)
            => origin == EffectOrigin.Primary || handler.RespondsToReactionEvents;

        /// <summary>origin 기원 효과(또는 턴 시점 처리)가 낸 사건들에 반응한다. 반응 효과가 낸 표시 이벤트는
        /// events 뒤에 발생 순서대로 붙는다.</summary>
        public void Dispatch(
            CombatState state,
            ResolutionContext resolution,
            IReadOnlyList<CombatSignal> signals,
            List<ResolutionEvent> events,
            EffectOrigin origin)
        {
            if (signals == null || signals.Count == 0)
            {
                return;
            }

            var candidates = new List<Candidate>();
            foreach (var signal in signals.OrderBy(s => s.TargetOrdinal).ThenBy(s => s.Sequence))
            {
                var bag = CombatUnits.StatusesOf(state, signal.TargetId);
                if (bag == null)
                {
                    continue;
                }

                foreach (var instance in bag.All)
                {
                    if (_reactions.TryResolve(instance.Key, out var handler)
                        && handler.SignalKey == signal.Key
                        && Allows(origin, handler))
                    {
                        candidates.Add(new Candidate(signal, bag, instance, handler));
                    }
                }
            }

            foreach (var candidate in candidates)
            {
                if (!candidate.Bag.All.Contains(candidate.Instance)
                    || !candidate.Handler.CanReact(state, candidate.Instance, candidate.Signal))
                {
                    continue;
                }

                foreach (var reaction in candidate.Handler.EffectsFor(candidate.Instance, candidate.Signal))
                {
                    var targets = TargetsOf(state, reaction.Target, candidate.Signal);
                    if (targets.IsEmpty)
                    {
                        continue;
                    }

                    var result = _executor.ApplyReaction(
                        state, resolution, candidate.Signal.TargetId, candidate.Bag,
                        candidate.Instance.Key.Id, reaction.Effect, targets);
                    events.AddRange(result.Events);
                }
            }
        }

        private EffectTargetSnapshot TargetsOf(CombatState state, ReactionTarget target, CombatSignal signal)
        {
            switch (target.Kind)
            {
                case ReactionTargetKind.SignalSource: return CombatUnits.LivingTarget(state, signal.SourceId);
                case ReactionTargetKind.SignalTarget: return CombatUnits.LivingTarget(state, signal.TargetId);
                case ReactionTargetKind.Position: return _targets.ResolvePosition(state, target.PositionKey.Value);
                default: throw new ArgumentOutOfRangeException(nameof(target));
            }
        }

        private sealed class Candidate
        {
            public Candidate(CombatSignal signal, StatusBag bag, StatusInstance instance, IReactionHandler handler)
            {
                Signal = signal;
                Bag = bag;
                Instance = instance;
                Handler = handler;
            }

            public CombatSignal Signal { get; }
            public StatusBag Bag { get; }
            public StatusInstance Instance { get; }
            public IReactionHandler Handler { get; }
        }
    }
}
