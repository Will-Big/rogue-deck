using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>고블린의 카드와 묶음, 그리고 그것을 고르는 기본 정책. 규칙은 여기(순수 C#)에 있고
    /// 정책이 갈아끼우는 자리다. 콘텐츠 원본을 JSON으로 옮기는 것은 별도 단계다.</summary>
    public static class GoblinDeck
    {
        /// <summary>Combat id this enemy is created with (single source so UI/localization can resolve it).</summary>
        public const string EnemyId = "goblin";

        public const int StartingHp = 28;

        public static CardDefinition Thrust() => new CardDefinition(
            "goblin_jab", "찌르기", Side.Enemy, 6,
            new[] { new EffectData(EffectKeys.Damage, 4) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition CrudeGuard() => new CardDefinition(
            "crude_guard", "조잡한 방어", Side.Enemy, 4,
            new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 3) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition SlyJab() => new CardDefinition(
            "sly_jab", "약삭빠른 찌르기", Side.Enemy, 3,
            new[] { EffectData.Conditional(EffectKeys.Damage, 3, new NoPrecedingCardOfSide(Side.Player), 6) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        private static readonly Func<CardDefinition>[] Catalog = { Thrust, CrudeGuard, SlyJab };

        /// <summary>Every distinct goblin card — the single place editors/art-seeding enumerate enemy
        /// cards from. Tracks <see cref="Catalog"/>, so new cards appear automatically. 묶음이 아니라
        /// 낱장 목록이다; 무엇이 실제로 전개되는지는 <see cref="Bundles"/>가 정한다.</summary>
        public static IReadOnlyList<CardDefinition> AllCards()
        {
            var cards = new CardDefinition[Catalog.Length];
            for (int i = 0; i < Catalog.Length; i++)
            {
                cards[i] = Catalog[i]();
            }

            return cards;
        }

        /// <summary>고블린이 전개하는 묶음 넷. 한 턴에 이 중 하나가 통째로 깔린다.
        /// 괄호 안은 존에서의 실행 순서다 — 찌르기 6, 조잡한 방어 4, 약삭빠른 찌르기 3.
        /// <list type="bullet">
        /// <item>A 늦은 단타 — 찌르기(6)</item>
        /// <item>B 선공 후 방어 — 약삭빠른 찌르기(3) → 조잡한 방어(4)</item>
        /// <item>C 앞뒤로 벌린 2연타 — 약삭빠른 찌르기(3) → 찌르기(6)</item>
        /// <item>D 농성 — 조잡한 방어(4) 둘. 방어는 재부여가 합산이므로 방어도 6이 된다.</item>
        /// </list></summary>
        public static IReadOnlyList<EnemyCardBundle> Bundles() => new[]
        {
            new EnemyCardBundle(Thrust()),
            new EnemyCardBundle(SlyJab(), CrudeGuard()),
            new EnemyCardBundle(SlyJab(), Thrust()),
            new EnemyCardBundle(CrudeGuard(), CrudeGuard())
        };

        /// <summary>고블린의 기본 정책: 매 턴 묶음 넷 중 하나를 무작위로 고른다(무작위는 전투 RNG에서
        /// 나온다). 다른 IEnemyTurnPolicy로 갈아끼우면 전투 루프를 건드리지 않고 행동이 바뀐다.</summary>
        public static IEnemyTurnPolicy Policy() => new RandomPickPolicy(Bundles());
    }
}
