using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using static FateWeaver.Simulation.Descriptions.DescriptionRequests;
namespace FateWeaver.Simulation.Descriptions
{
    public static class ConditionDescriptionRequests
    {
        public static LocalizedTextRequest Create(Condition condition)
        {
            switch (condition)
            {
                case FirstToTrigger _: return Text("condition.first");
                case WithinNth w: return Text("condition.within", ("n", w.N));
                case BeforeNextEnemyDamageCard _: return Text("condition.before_enemy_damage");
                case NoPrecedingCardOfSide n: return Text("condition.no_preceding", ("side", SideName(n.Side)));
                case NoFollowingCardOfSide n: return Text("condition.no_following", ("side", SideName(n.Side)));
                case AdjacentCardIs a: return Text("condition.adjacent." + Direction(a.Direction), ("side", SideName(a.Side)), ("kind", Text("kind.card")));
                case AdjacentCardHasEffect a: return Text("condition.adjacent." + Direction(a.Direction), ("side", SideName(a.Side)), ("kind", EffectName(a.EffectKey)));
                case PreviousExecutedCardIs p: return Text("condition.previous", ("side", SideName(p.Side)), ("kind", Text("kind.card")));
                case PreviousExecutedCardHasEffect p: return Text("condition.previous", ("side", SideName(p.Side)), ("kind", EffectName(p.EffectKey)));
                case AllOf all:
                    if (all.Conditions.Count == 0) throw new ArgumentException("An all-of description needs conditions.");
                    var joined = Create(all.Conditions[0]);
                    for (var i = 1; i < all.Conditions.Count; i++) joined = Text("condition.all", ("first", joined), ("next", Create(all.Conditions[i])));
                    return joined;
                default: throw new ArgumentException("Unregistered condition description: " + condition?.GetType().Name);
            }
        }
        private static string Direction(AdjacentDirection direction) => direction == AdjacentDirection.Previous ? "previous" : "next";
        private static LocalizedTextRequest SideName(Side side) => Text(side == Side.Player ? "side.player" : "side.enemy");
        private static LocalizedTextRequest EffectName(EffectKey key) => key == EffectKeys.Damage ? Text("kind.damage_card") : Text("kind.effect_card", ("effect", Name("effect." + key.Id + ".name")));
    }
}
