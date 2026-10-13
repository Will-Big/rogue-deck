using System;
using System.Collections;
using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation.Run;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>런 상태를 들고 전투 노드를 시작·종료시키는 호출 순서를 정한다. 승패·보상 후보·덱
    /// 반영은 전부 CombatNode가 답하고, 이 객체는 누구를 언제 부를지만 안다(규칙 30).</summary>
    public sealed class CombatNodeFlow : MonoBehaviour
    {
        [SerializeField] private BattleScreenController _battle;
        [SerializeField] private RewardChoiceView _reward;
        [SerializeField] private CombatResultView _result;

        [Tooltip("시작 파티 순서. 색은 BattlePresenter가 따로 든다.")]
        [SerializeField] private CharacterAsset[] _party = Array.Empty<CharacterAsset>();

        [Tooltip("런 시드. 같은 시드 + 같은 행동 = 같은 결과.")]
        [SerializeField] private int _runSeed = 1;

        private GameContent _content;
        private CombatNodeContext _context;
        private RunState _run;
        private CombatNode _node;

        [SerializeField] private GameLocalization _localization;

        private IEnumerator Start()
        {
            if (_localization != null)
            {
                yield return new WaitUntil(() => _localization.Initialization.IsCompleted && (_localization.Renderer != null || _localization.Initialization.IsFaulted));
                if (_localization.Initialization.IsFaulted) { Debug.LogException(_localization.Initialization.Exception); yield break; }
            }
            if (_battle == null || _reward == null || _result == null)
            {
                Debug.LogError("전투 노드 흐름 배선이 비어 있습니다.");
                yield break;
            }

            if (!_battle.Initialize(NewRun, OnCombatFinished))
            {
                yield break;
            }

            var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
            if (!loaded.Succeeded)
            {
                var reasons = string.Join("\n", loaded.Errors);
                _battle.ShowMessage(GameLocalization.Request("message.content_error", ("reason", reasons)));
                Debug.LogError("콘텐츠 로드 실패:\n" + reasons);
                yield break;
            }

            _content = loaded.Content;
            _context = new CombatNodeContext(
                _content.Statuses,
                _content.CombatRules,
                new ContentEncounterSource(_content, CombatRegistries.EnemyPolicies()),
                new CharacterPoolRewardSource(_content));
            NewRun();
        }

        private void NewRun()
        {
            if (_content == null)
            {
                return;
            }

            if (_party == null || _party.Length == 0 || _party.Any(member => member == null))
            {
                _battle.ShowMessage(GameLocalization.Request("message.party_missing"));
                return;
            }

            _run = RunSetup.NewRun(
                _content, _party.Select(member => member.Id).ToList(), _runSeed);
            BeginNode();
        }

        private void BeginNode()
        {
            _reward.Hide();
            _result.Hide();
            _node = CombatNode.Begin(_run, _context);
            _battle.Bind(_node.Session, _content);
        }

        private void OnCombatFinished()
        {
            _node.Conclude();
            if (_node.Phase == CombatNodePhase.Reward)
            {
                _reward.Show(_node.Offer.Candidates, OwnerName, OnChoose, OnSkip);
            }
            else if (_node.Phase == CombatNodePhase.Defeated)
            {
                _result.ShowDefeat(NewRun);
            }
        }

        private void OnChoose(int index)
        {
            _node.Choose(index);
            _reward.ShowNextButton(BeginNode);
        }

        private void OnSkip()
        {
            _node.Skip();
            _reward.ShowNextButton(BeginNode);
        }

        private string OwnerName(string ownerId)
            => _run.Party.First(member => member.Id == ownerId).Name;
    }
}
