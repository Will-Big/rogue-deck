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
        /// 카드 시작 조건이 없으므로 CardResolved.ConditionTier가 Success에서 Basic이 된다. 피해·상태·HP는 같다.
        /// 2026-09-18 갱신(전투 실행 계약 T3): 효과마다 대상을 고르고 대상 없음은 미적용이다. ① CardResolved.TargetId가
        /// "마지막 대상"에서 "처음 적용된 효과의 첫 대상"이 되어 spore_veil·probing_strike·toxic_reclaim이 member_a 대신
        /// goblin#0을 가리킨다. ② 고블린이 죽은 뒤의 spore_veil·delayed_strike가 NoValidTarget 취소 대신 해결되고,
        /// spore_veil의 자신 방어 2가 적용된다(같은 턴 끝에 만료). 그 밖의 서명은 같다.
        /// 2026-09-18 갱신(전투 실행 계약 T5): 승패는 카드가 끝날 때마다 판정한다. 고블린을 죽인 카드가 끝나는 순간
        /// 승리가 확정되어 그 뒤의 spore_veil·delayed_strike와 턴 끝 방어 만료(StatusExpired)가 실행되지 않는다.
        /// 그 밖의 서명은 같다.
        /// 2026-09-18 갱신(전투 실행 계약 T6): 방어 만료가 턴 정리(Cleanup)에서 다음 턴 준비(Prepare)로 옮겨져, 해석
        /// 타임라인의 StatusExpired(block) 7줄이 빠진다(세션의 LastTurnStartTimeline으로 간다). HP·피해·턴 흐름은 같다.
        /// 2026-09-20 갱신(전투 템포 개선 과제 2): member_b의 덱이 party_prototype(픽스처 카드)에서
        /// striker(cleave·flank_jab·heavy_swing·shield_bash·brace)로 바뀌어 손패·배치·피해가 전부
        /// 달라진다. 의도한 콘텐츠 변경이라 서명을 다시 잡았다.
        /// 2026-09-20 갱신(전투 템포 변경 2): 방어 카드 셋이 4에서 3으로, goblin_jab이 4에서 5로 바뀌어
        /// 피해·방어 수치와 HP 추이가 달라진다. 카드 순서와 이벤트 종류는 같다.
        /// 2026-09-20 갱신(전투 템포 변경 3): 고블린 정책이 shuffle_bag이 되어 턴마다 나오는 묶음이
        /// 달라지고, 방어 전용 묶음이 crude_guard+goblin_jab으로 바뀌었다.</summary>
        private const string ExpectedSignatureSha256 = "f57be5e099b9181f7edb281b489e270f405b9451fb5446dac2802ec82dedaa9d";

        /// <summary>편성을 `goblin_single`로 고정한다. 이 골든이 잠그는 것은 C# 원본에서 JSON으로의
        /// 이관이지 편성 후보 목록이 아니다 — 추첨을 쓰면 편성을 하나 더 저작할 때마다 골든이 흔들리고,
        /// 그 흔들림은 이관 회귀와 구분되지 않는다(2026-09-20, `goblin_pair` 추가 때 실제로 겪었다).</summary>
        private static CombatNode BeginNode()
        {
            var content = TestContent.Content();
            var run = RunSetup.NewRun(
                content, new[] { "member_a", "member_b" }, RunSeed);
            var context = new CombatNodeContext(
                content.Statuses,
                content.CombatRules,
                new FixedBattleEncounter(
                    new ContentEncounterSource(content, CombatRegistries.EnemyPolicies()), "goblin_single"),
                new CharacterPoolRewardSource(content));
            return CombatNode.Begin(run, context);
        }

        [Test]
        public void Node_zero_signature_matches_the_csharp_golden()
        {
            var signature = Signature(BeginNode());

            Assert.AreEqual(
                ExpectedSignatureSha256, Sha256(signature),
                "실측 SHA: " + Sha256(signature) + "\n실측 서명:\n" + signature);
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
