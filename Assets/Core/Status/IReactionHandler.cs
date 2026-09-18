using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>반응 효과가 누구를 대상으로 하는가. 반응에는 카드 축이 없으므로 일반 카드의 위치 선택과 분리한다
    /// (계획 T4): 사건의 원인(SourceId), 사건 당사자(TargetId = 반응 보유자), 또는 대형 위치 하나.
    /// 모두 살아 있는 개체만 고른다.</summary>
    public sealed class ReactionTarget
    {
        private ReactionTarget(ReactionTargetKind kind, CardTargetKey? position)
        {
            Kind = kind;
            PositionKey = position;
        }

        public ReactionTargetKind Kind { get; }

        /// <summary>Kind가 Position일 때의 위치(Self 불가).</summary>
        public CardTargetKey? PositionKey { get; }

        public static readonly ReactionTarget SignalSource = new ReactionTarget(ReactionTargetKind.SignalSource, null);
        public static readonly ReactionTarget SignalTarget = new ReactionTarget(ReactionTargetKind.SignalTarget, null);

        public static ReactionTarget Position(CardTargetKey key)
        {
            if (key.Range == CardTargetRange.Self)
            {
                throw new ArgumentException("A reaction position must be a formation range, not Self.", nameof(key));
            }

            return new ReactionTarget(ReactionTargetKind.Position, key);
        }
    }

    public enum ReactionTargetKind
    {
        SignalSource,
        SignalTarget,
        Position
    }

    /// <summary>반응 능력이 내는 효과 하나와 그 대상 규칙.</summary>
    public sealed record ReactionEffect(EffectData Effect, ReactionTarget Target);

    /// <summary>한 상태가 가진 반응 능력(전투 실행 계약 스펙 §7). 사건 종류(SignalKey)에 반응하고, 추가 조건과
    /// 생존 요건은 CanReact가 스스로 정한다 — 반격은 생존을 요구하고 사망 능력은 사망한 보유자를 허용한다.
    /// 모든 능력에 일괄 생존 검사를 하지 않는다.</summary>
    public interface IReactionHandler
    {
        /// <summary>이 능력을 주는 상태. 보유자의 상태 가방에서 후보를 찾는 키다.</summary>
        StatusKey Key { get; }

        CombatSignalKey SignalKey { get; }

        bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal);

        IReadOnlyList<ReactionEffect> EffectsFor(StatusInstance instance, CombatSignal signal);
    }
}
