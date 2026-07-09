using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;

namespace FateWeaver.Unity
{
    public static class PlaytestKoreanText
    {
        public static string ScenarioName(string id, string fallback)
        {
            switch (id)
            {
                case "chapter-8-three-turn-opening": return "8장 3턴 도입부";
                case "mark-combo": return "표식 연계";
                case "counter-stance": return "반격 자세";
                case "chain-slash": return "연쇄 베기";
                default: return fallback;
            }
        }

        public static string CardName(string id, string fallback)
        {
            if (id.StartsWith("preemptive_thrust", StringComparison.Ordinal)) return "선제 찌르기";
            if (id.StartsWith("quick_cut", StringComparison.Ordinal)) return "찰나의 베기";
            if (id.StartsWith("wrist_cut", StringComparison.Ordinal)) return "손목 베기";

            switch (id)
            {
                case "goblin_jab": return "찌르기";
                case "crude_guard": return "조잡한 방어";
                case "sly_jab": return "약삭빠른 찌르기";
                case "warden_swing": return "휘두르기";
                case "warden_smash": return "내려치기";
                case "warden_uppercut": return "올려치기";
                case "warden_block": return "막기";
                case "warden_brace": return "버티기";
                case "mark": return "표식 새기기";
                case "slash": return "베기";
                case "counter_stance": return "반격";
                case "counter": return "반격 자세";
                case "prep": return "준비";
                case "chain": return "연쇄 베기";
                default: return fallback;
            }
        }

        public static string EnemyName(string id, string fallback)
        {
            switch (id)
            {
                case GoblinDeck.EnemyId: return "고블린";
                case WardenDeck.EnemyId: return "간수";
                default: return fallback;
            }
        }

        public static string SideName(Side side)
            => side == Side.Player ? "플레이어" : "적";

        public static string ConditionName(ConditionTier tier)
        {
            switch (tier)
            {
                case ConditionTier.Failure: return "실패";
                case ConditionTier.Success: return "성공";
                default: return "기본";
            }
        }

        public static string OutcomeName(Outcome outcome)
        {
            switch (outcome)
            {
                case Outcome.Win: return "승리";
                case Outcome.Lose: return "패배";
                default: return "진행 중";
            }
        }

        public static string StatusName(StatusKey key)
        {
            if (key == StatusKeys.Stun) return "기절";
            if (key == StatusKeys.Vulnerable) return "취약";
            if (key == StatusKeys.RewardNullified) return "조건 보상 무효";
            if (key == StatusKeys.Block) return "방어";
            if (key == StatusKeys.Slow) return "둔화";
            if (key == StatusKeys.Haste) return "가속";
            return key.ToString();
        }

        public static string InterventionActionName(InterventionActionKey key)
        {
            if (key == InterventionActionKeys.ChangeExecutionOrder) return "실행 순서 변경";
            if (key == InterventionActionKeys.SwapExecutionOrder) return "실행 순서 교환";
            if (key == InterventionActionKeys.Lock) return "고정";
            return key.ToString();
        }
    }
}
