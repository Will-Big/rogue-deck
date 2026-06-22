using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;

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
                case "goblin_jab": return "고블린 찌르기";
                case "mark": return "표식 새기기";
                case "slash": return "베기";
                case "counter": return "반격 자세";
                case "prep": return "준비";
                case "chain": return "연쇄 베기";
                default: return fallback;
            }
        }

        public static string CardDescription(string id)
        {
            if (id.StartsWith("quick_cut", StringComparison.Ordinal))
                return "피해 2. 이번 턴에 가장 먼저 발동하면 대신 피해 10.";
            if (id.StartsWith("wrist_cut", StringComparison.Ordinal))
                return "피해 3. 다음 플레이어 조건 보상을 무효화.";
            if (id.StartsWith("preemptive_thrust", StringComparison.Ordinal))
                return "선제 일격.";
            if (id.StartsWith("goblin_jab", StringComparison.Ordinal))
                return "고블린의 빠른 찌르기.";

            switch (id)
            {
                case "slash": return "피해 2.";
                case "mark": return "다음 카드가 플레이어 공격이고 적 공격보다 먼저면, 다음 플레이어 공격 피해 +6.";
                case "counter": return "방어 2. 바로 앞에서 적이 공격했다면 피해 7 (3번째 안이면 +2).";
                case "chain": return "피해 1. 바로 앞이 플레이어 행동 카드이고 3번째 안이면 추가 피해 5.";
                case "prep": return "피해 1.";
                default: return string.Empty;
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
            return key.ToString();
        }

        public static string FateActionName(FateActionKey key)
        {
            if (key == FateActionKeys.ChangeInitiative) return "주도력 변경";
            if (key == FateActionKeys.SwapInitiative) return "주도력 교환";
            if (key == FateActionKeys.Lock) return "고정";
            return key.ToString();
        }
    }
}
