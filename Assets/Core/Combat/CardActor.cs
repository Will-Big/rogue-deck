namespace FateWeaver.Core.Combat
{
    /// <summary>카드를 실제로 쓰는 쪽(행위자)의 StatusBag을 찾는다. OwnerId가 있으면 그것으로,
    /// 없으면 해당 진영에 후보가 정확히 하나일 때만 확정한다(단일 적·솔로 러너 호환). 후보가
    /// 둘 이상이고 OwnerId가 없으면 null을 돌려주어 행위자 상태를 적용하지 않는다 — 러너가
    /// OwnerId를 채우지 않는 다중 적 경로에서 임의의 대상을 고르지 않기 위한 것이다.</summary>
    public static class CardActor
    {
        public static Status.StatusBag StatusesFor(CombatState state, ExecutionCardInstance card)
        {
            if (state == null || card == null)
            {
                return null;
            }

            if (card.Def.Side == Cards.Side.Player)
            {
                var member = string.IsNullOrEmpty(card.OwnerId)
                    ? (state.Party.Count == 1 ? state.Party[0] : null)
                    : PartyTargeting.LivingById(state, card.OwnerId);
                return member?.Statuses;
            }

            var enemy = string.IsNullOrEmpty(card.OwnerId)
                ? (state.Enemies.Count == 1 ? state.Enemies[0] : null)
                : FindLivingEnemy(state, card.OwnerId);
            return enemy?.Statuses;
        }

        private static Enemy FindLivingEnemy(CombatState state, string enemyId)
        {
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Id == enemyId && enemy.Hp > 0)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
