using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Status
{
    /// <summary>전염 (사후 전염): 보유자가 독 상태로 사망하면 남은 독 전량을 현재 적군 앞 하나
    /// (생존)에게 이전한다. 유효 기간은 부여 수명(Turns)으로 표현한다. 죽은 보유자는 생존 대형에서
    /// 이미 빠져 있으므로 EnemyTargeting.Select(FrontOne)가 곧 '다음 전열'이다.</summary>
    public sealed class ContagionBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Contagion;
        public override StatusScope Scope => StatusScope.Entity;

        public override void OnHolderDied(StatusDeathContext ctx)
        {
            var poison = ctx.HolderBag.Get(StatusKeys.Poison);
            if (poison == null || poison.Magnitude <= 0)
            {
                return;
            }

            var recipient = EnemyTargeting.Select(ctx.State, TargetSelector.FrontOne);
            if (recipient == null)
            {
                return;
            }

            recipient.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, poison.Magnitude);
            ctx.Events.Add(new Events.StatusTransferred(
                ctx.HolderId, recipient.Id, StatusKeys.Poison.Id, poison.Magnitude));
            ctx.HolderBag.Remove(StatusKeys.Poison);
        }
    }
}
