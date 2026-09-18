using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>전투 개체(파티원·적)를 id로 찾는다. 사건은 id만 싣고, 반응이 그 id로 보유자와 대상을 찾는다.
    /// 파티를 먼저 본다.</summary>
    public static class CombatUnits
    {
        public static StatusBag StatusesOf(CombatState state, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var member in state.Party)
            {
                if (member.Id == id) return member.Statuses;
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy.Id == id) return enemy.Statuses;
            }

            return null;
        }

        /// <summary>살아 있는 그 개체 하나만 담은 대상 목록. 없거나 죽었으면 빈 목록이다.</summary>
        public static EffectTargetSnapshot LivingTarget(CombatState state, string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (var member in state.Party)
                {
                    if (member.Id == id && member.IsAlive)
                    {
                        return new EffectTargetSnapshot(
                            new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self), new[] { member }, null);
                    }
                }

                foreach (var enemy in state.Enemies)
                {
                    if (enemy.Id == id && enemy.Hp > 0)
                    {
                        return new EffectTargetSnapshot(
                            new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.Self), null, new[] { enemy });
                    }
                }
            }

            return new EffectTargetSnapshot(
                new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.Self), null, null);
        }
    }
}
