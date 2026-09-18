using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>이 효과가 고른 대상 모두에게 고정 피해를 준다. 대상은 효과 진영과 카드의 그 진영 축으로
    /// EffectExecutor가 효과 시작 때 고른다. 광역이면 그 목록 전체에 같은 수치로 적용한다. 받는 피해는 대상의 개체 상태(취약·방어 등)로 접힌다(StatusRegistry가 있을 때).</summary>
    public sealed class DamageHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public void Apply(EffectContext ctx)
        {
            // Deliberate rule: a pending damage bonus (GrantNextPlayerDamageCardBonus) raises the
            // CARD's damage value, not a fixed pool split across targets — so with an All-target card
            // it applies to EVERY target independently ("다음 플레이어 피해 카드가 주는 피해 +X" reads
            // per hit dealt, not a one-time budget).
            var bonus = ctx.Card.ConsumePendingDamageBonus();
            if (bonus != 0)
            {
                ctx.ExtraEvents.Add(new Events.CardBuffConsumed(
                    ctx.Card.InstanceId, ctx.Card.Def.Id, Events.CardBuffIds.DamageBonus, bonus));
            }

            var amount = FoldOutgoing(ctx, ctx.EffectValue + bonus);
            foreach (var target in ctx.RequireTargets().Enemies)
            {
                var dealt = FoldIncoming(ctx, target.Statuses, target.Id, amount);
                HitEnemy(ctx, target, dealt);
                ctx.DamageDealt += dealt;
            }

            foreach (var target in ctx.RequireTargets().Party)
            {
                var dealt = FoldIncoming(ctx, target.Statuses, target.Id, amount);
                HitParty(ctx, target, dealt);
                ctx.DamageDealt += dealt;
            }
        }

        /// <summary>적에게 피해를 적용하고, HP가 실제로 바뀌었으면 HpChanged를 남긴다.</summary>
        private static void HitEnemy(EffectContext ctx, Enemy target, int dealt)
        {
            var before = target.Hp;
            target.Hp -= dealt;
            if (target.Hp != before)
            {
                ctx.ExtraEvents.Add(new Events.HpChanged(
                    target.Id, before, target.Hp, Events.HpChangeSource.CardDamage, ctx.Card.Def.Id));
            }
        }

        /// <summary>파티원에게 피해를 적용하고, HP가 실제로 바뀌었으면 HpChanged를 남긴다.</summary>
        private static void HitParty(EffectContext ctx, PartyMember target, int dealt)
        {
            var before = target.Hp;
            target.TakeDamage(dealt);
            if (target.Hp != before)
            {
                ctx.ExtraEvents.Add(new Events.HpChanged(
                    target.Id, before, target.Hp, Events.HpChangeSource.CardDamage, ctx.Card.Def.Id));
            }
        }

        /// <summary>Folds the target's entity-scoped statuses into incoming damage: the multiplier
        /// layer first, then the absorb layer (see StatusDamageFold). An UntilConsumed status that
        /// actually changed the damage spends a charge (auto-consume).</summary>
        private static int FoldIncoming(EffectContext ctx, StatusBag bag, string holderId, int damage)
            => StatusDamageFold.Incoming(
                bag, ctx.StatusRegistry, ctx.State.StatusRules, damage, holderId, ctx.DamageSteps);

        /// <summary>Folds the acting side's entity-scoped statuses into the damage it deals (e.g.
        /// Weak). Applied once per effect, before any target's incoming statuses — so an All-target
        /// card reduces its damage once and every target is hit with the same reduced value.</summary>
        private static int FoldOutgoing(EffectContext ctx, int damage)
            => StatusDamageFold.Outgoing(
                ctx.ActorStatuses, ctx.StatusRegistry, ctx.State.StatusRules, damage,
                ctx.Card.OwnerId, ctx.DamageSteps);
    }
}
