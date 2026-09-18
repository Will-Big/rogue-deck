using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>전투 노드 2단계 동등성(설계 결정 16). 고블린·파티 수치를 C#에서 JSON으로 옮겨도 같은
    /// 런 시드·같은 입력이면 노드 0의 전투 서명이 바이트 단위로 같아야 한다. 골든은 C# 원본이
    /// 살아 있을 때 잡았고, 원본을 지운 뒤에도 JSON 경로가 이 값과 계속 비교된다.
    ///
    /// 서명 = 턴마다 (손패 카드 id@소유자, 이번 턴에 배치한 카드, 해석 이벤트 ToString) + 결과 + 보상 후보.
    /// CombatRngDeterminismTests.RunSignature와 같은 형식에 소유자·배치·보상을 더했다.</summary>
    public class GoblinParityTests
    {
        private const int RunSeed = 20260917;
        private const int MaxTurns = 40;

        /// <summary>C# 원본 경로에서 실측한 서명의 SHA-256(소문자 hex). 이관 때문에 바꾸지 않는다 — 바뀌면 이관이
        /// 동작을 바꾼 것이다. 규칙을 의도적으로 바꿀 때만 차이를 확인하고 갱신한다.
        /// 2026-09-18 갱신(전투 실행 계약 T1): 죽은 고블린의 남은 goblin_jab이 그 카드 차례의
        /// CardCancelled(OwnerDied) 대신 죽인 카드 직후의 CardRemoved로 기록된다. 그 밖의 서명은 같다.
        /// 2026-09-18 갱신(전투 실행 계약 T2a): toxic_reclaim의 소비 보상이 조건이 아니라 requires가 되어
        /// 카드 시작 조건이 없으므로 CardResolved.ConditionTier가 Success에서 Basic이 된다. 피해·상태·HP는 같다.</summary>
        private const string ExpectedSignatureSha256 = "cd1c4cde8fb9ba992cc35b9d76badacb79285da91bec872d9c66f52c7ea69477";

        private static CombatNode BeginNode()
        {
            var content = TestContent.Content();
            var run = RunSetup.NewRun(
                content, new[] { "member_a", "member_b" }, RunSeed);
            var context = new CombatNodeContext(
                content.Statuses,
                content.CombatRules,
                new ContentEncounterSource(content, CombatRegistries.EnemyPolicies()),
                new CharacterPoolRewardSource(content));
            return CombatNode.Begin(run, context);
        }

        [Test]
        public void Node_zero_signature_matches_the_csharp_golden()
        {
            var signature = Signature(BeginNode());

            Assert.AreEqual(ExpectedSignatureSha256, Sha256(signature), "실측 서명:\n" + signature);
        }

        [Test]
        public void Signature_reaches_an_outcome_and_places_player_cards()
        {
            var signature = Signature(BeginNode());

            StringAssert.Contains("play:", signature, "전제: 운명력을 쓰는 플레이어 카드가 한 장 이상 배치돼야 한다.");
            StringAssert.DoesNotContain("outcome:Ongoing", signature, "전제: 전투가 끝나야 한다.");
        }

        private static string Signature(CombatNode node)
        {
            var session = node.Session;
            var signature = new StringBuilder();
            for (int turn = 0; turn < MaxTurns; turn++)
            {
                signature.Append("hand:");
                signature.AppendLine(string.Join(",", session.Hand.Select(card => card.Def.Id + "@" + card.OwnerId)));
                PlayEveryAffordableExecutionCard(session, signature);
                foreach (var resolutionEvent in session.ResolveTurn())
                {
                    signature.AppendLine(resolutionEvent.ToString());
                }

                if (!session.BeginNextTurn())
                {
                    break;
                }
            }

            signature.Append("outcome:").AppendLine(session.Outcome.ToString());
            if (session.IsComplete)
            {
                node.Conclude();
                if (node.Offer != null)
                {
                    signature.Append("offer:");
                    signature.AppendLine(string.Join(
                        ",", node.Offer.Candidates.Select(c => c.Card.Id + "@" + c.OwnerId)));
                }
            }

            return signature.ToString();
        }

        /// <summary>고정 입력: 손패 앞에서부터, 실행 카드이고 운명력이 되는 첫 카드를 배치하기를 더 놓을
        /// 수 없을 때까지 반복한다. 배치가 거부되면(대상 요구 등) 다음 카드로 넘어간다.</summary>
        private static void PlayEveryAffordableExecutionCard(DeckCombatSession session, StringBuilder signature)
        {
            var placed = true;
            while (placed)
            {
                placed = false;
                for (int i = 0; i < session.Hand.Count; i++)
                {
                    var def = session.Hand[i].Def;
                    if (def.Category != CardCategory.Execution || def.EnergyCost > session.FateEnergy)
                    {
                        continue;
                    }

                    if (session.PlayExecutionCard(i))
                    {
                        signature.Append("play:").AppendLine(def.Id);
                        placed = true;
                        break;
                    }
                }
            }
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return string.Concat(bytes.Select(b => b.ToString("x2")));
            }
        }
    }
}
