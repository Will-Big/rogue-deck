using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>독 X (카드풀 스펙 §3.2): 행동 턴 종료에 X만큼 피해를 주고 1 증가한다. 이번 턴에
    /// 부여된 독도 이번 턴 종료에 발동하며, 이미 사망한 대상은 틱 파이프라인이 제외한다.
    /// 잠복(PoisonDormant) 마커는 이번 턴 발동 자체를, 안정(PoisonStasis) 마커는 성장만 금지한다
    /// (§3.3 우선순위 1층 '금지·고정'). 독 피해는 방어(ModifyIncomingDamage)를 경유하지 않는다.
    /// 성장량은 규칙 수치라 등록 시점에 주입한다(매직 넘버 금지).</summary>
    public sealed class PoisonBehavior : StatusBehavior
    {
        private readonly int _growthPerTurn;

        public PoisonBehavior(int growthPerTurn)
        {
            _growthPerTurn = growthPerTurn;
        }

        public override StatusKey Key => StatusKeys.Poison;
        public override StatusScope Scope => StatusScope.Entity;
        public override bool StacksMagnitude => true;

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
                ctx.Instance.Magnitude += _growthPerTurn;
            }

            ctx.Events.Add(new StatusTicked(
                ctx.HolderId, Key.Id, damage, ctx.Instance.Magnitude));
        }
    }
}
