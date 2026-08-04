using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>독 X (카드풀 스펙 §3.2): 행동 턴 종료에 X만큼 피해를 주고 1 증가한다. 이번 턴에
    /// 부여된 독도 이번 턴 종료에 발동하며, 이미 사망한 대상은 틱 파이프라인이 제외한다.
    /// 잠복(PoisonDormant) 마커는 이번 턴 발동 자체를, 안정(PoisonStasis) 마커는 성장만 금지한다
    /// (§3.3 우선순위 1층 '금지·고정'). 독 피해는 방어(ModifyIncomingDamage)를 경유하지 않는다.
    /// 성장량은 규칙 수치라 StatusContentCatalog(등록 시점이 아니라 훅 시점)에서 읽는다(매직 넘버 금지).</summary>
    public sealed class PoisonBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Poison;
        public override StatusScope Scope => StatusScope.Entity;
        public override bool StacksMagnitude => true;

        public override StatusSpec NewSpec() => new PoisonStatusSpec();

        public override void OnTurnEnd(StatusTickContext ctx)
        {
            if (ctx.HolderBag.Has(StatusKeys.PoisonDormant))
            {
                return;
            }

            var damage = ctx.Instance.Magnitude;
            if (damage <= 0)
            {
                return;
            }

            ctx.DealDamage(damage);
            if (!ctx.HolderBag.Has(StatusKeys.PoisonStasis))
            {
                ctx.Instance.Magnitude += ctx.Content.GrowthPerTurnOf(Key);
            }

            ctx.Events.Add(new StatusTicked(
                ctx.HolderId, Key.Id, damage, ctx.Instance.Magnitude));
        }

        /// <summary>독 잠복(PoisonDormant)을 ThisTurn으로 건다 — trigger_status가 조기 발병시킨 뒤
        /// 이번 턴 종료의 같은 발동을 막는다. 어떤 마커를 쓰는지는 독 자신만 안다.</summary>
        public override void SuppressThisTurn(StatusBag holderBag)
            => holderBag.Add(StatusKeys.PoisonDormant, StatusLifetime.ThisTurn);
    }
}
