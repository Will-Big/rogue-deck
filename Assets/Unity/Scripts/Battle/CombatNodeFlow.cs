using System;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation;
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

        [Tooltip("1단계 임시 튜닝. 2단계에서 combat_rules.json으로 옮긴다.")]
        [SerializeField] private int _fateEnergyPerTurn = 3;
        [SerializeField] private int _rewardChoices = 3;

        private GameContent _content;
        private CombatNodeContext _context;
        private RunState _run;
        private CombatNode _node;

        private void Start()
        {
            if (_battle == null || _reward == null || _result == null)
            {
                Debug.LogError("전투 노드 흐름 배선이 비어 있습니다.");
                return;
            }

            if (!_battle.Initialize(NewRun, OnCombatFinished))
            {
                return;
            }

            var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
            if (!loaded.Succeeded)
            {
                var reasons = string.Join("\n", loaded.Errors);
                _battle.ShowMessage("콘텐츠 로드 실패:\n" + reasons);
                Debug.LogError("콘텐츠 로드 실패:\n" + reasons);
                return;
            }

            _content = loaded.Content;
            _context = new CombatNodeContext(
                _content.Statuses,
                PartyPrototypeRoster.Tuning,
                _fateEnergyPerTurn,
                _rewardChoices,
                new GoblinEncounterSource(),
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
                _battle.ShowMessage("파티 CharacterAsset이 연결되지 않았습니다.");
                return;
            }

            _run = RunSetup.NewRun(
                _content, _party.Select(member => member.Id).ToList(), PartyPrototypeRoster.Tuning, _runSeed);
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
