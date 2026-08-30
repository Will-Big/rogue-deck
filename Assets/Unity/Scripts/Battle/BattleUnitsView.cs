using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>유닛 뷰의 스폰과 갱신을 맡는다. UnitView.Bind의 유일한 호출자이므로 캐릭터 아트가
    /// 스프라이트 시트 애니메이션으로 바뀔 때 이 컴포넌트만 바뀐다(설계 §4.6).</summary>
    public sealed class BattleUnitsView : MonoBehaviour
    {
        [SerializeField] private UnitView _unitPrefab;
        [SerializeField] private RectTransform _playerUnitsRow;
        [SerializeField] private RectTransform _enemyUnitsRow;

        private static readonly Color EnemyUnitTint = new Color(0.55f, 0.25f, 0.25f, 1f);

        private readonly Dictionary<string, UnitView> _partyUnits =
            new Dictionary<string, UnitView>();
        private readonly Dictionary<string, UnitView> _enemyUnits =
            new Dictionary<string, UnitView>();
        private readonly Dictionary<string, int> _enemyMaxHp = new Dictionary<string, int>();

        /// <summary>스폰 시점의 최대 HP. HpChanged가 최대 HP를 싣지 않으므로 재생 계층이 여기서
        /// 얻는다(설계 「어려워지는 것」 4번). 전투 중 최대 HP가 변하는 기능이 생기면 갱신 경로가
        /// 새로 필요하다.</summary>
        private readonly Dictionary<string, int> _maxHp = new Dictionary<string, int>();

        private Func<StatusKey, string> _statusNameFor;

        public bool IsBound => _unitPrefab != null
            && _playerUnitsRow != null && _enemyUnitsRow != null;

        /// <summary>id로 유닛 뷰를 찾는다. 재생 계층이 BattleStage를 통해 쓰는 유일한 조회다.</summary>
        public bool TryGetUnit(string holderId, out UnitView view)
        {
            view = null;
            if (holderId == null)
            {
                return false;
            }

            return _partyUnits.TryGetValue(holderId, out view)
                || _enemyUnits.TryGetValue(holderId, out view);
        }

        /// <summary>스폰 시점의 최대 HP. 모르는 id는 0이다.</summary>
        public int MaxHpOf(string holderId)
            => holderId != null && _maxHp.TryGetValue(holderId, out var max) ? max : 0;

        /// <summary>기존 유닛을 지우고 상태에 맞춰 다시 만든다. 색·적 이름·상태 이름은 표현
        /// 관심사라 바깥에서 받는다. 상태 이름 조회는 UnitView의 유일한 호출자인 여기서 소유한다
        /// (설계 §4.6).</summary>
        public void Spawn(
            CombatState state,
            Func<string, Color> colorFor,
            Func<string, string> enemyNameFor,
            Func<StatusKey, string> statusNameFor)
        {
            _statusNameFor = statusNameFor;
            foreach (Transform child in _playerUnitsRow) Destroy(child.gameObject);
            foreach (Transform child in _enemyUnitsRow) Destroy(child.gameObject);
            _partyUnits.Clear();
            _enemyUnits.Clear();
            _enemyMaxHp.Clear();
            _maxHp.Clear();

            foreach (var member in state.Party)
            {
                var view = Instantiate(_unitPrefab, _playerUnitsRow);
                view.Bind(member.Name, colorFor(member.Id));
                _partyUnits.Add(member.Id, view);
                _maxHp[member.Id] = member.MaxHp;
            }

            foreach (var enemy in state.Enemies)
            {
                var view = Instantiate(_unitPrefab, _enemyUnitsRow);
                view.Bind(enemyNameFor(enemy.Id), EnemyUnitTint);
                _enemyUnits.Add(enemy.Id, view);
                _enemyMaxHp.Add(enemy.Id, enemy.Hp);
                _maxHp[enemy.Id] = enemy.Hp;
            }
        }

        public void Refresh(CombatState state)
        {
            int partyCount = state.Party.Count;
            for (int i = 0; i < partyCount; i++)
            {
                var member = state.Party[i];
                if (_partyUnits.TryGetValue(member.Id, out var view))
                {
                    view.SetHp(member.Hp, member.MaxHp);
                    view.SetStatuses(member.Statuses.All, _statusNameFor);
                    view.transform.SetSiblingIndex(partyCount - 1 - i);
                }
            }

            int enemyCount = state.Enemies.Count;
            for (int i = 0; i < enemyCount; i++)
            {
                var enemy = state.Enemies[i];
                if (_enemyUnits.TryGetValue(enemy.Id, out var view)
                    && _enemyMaxHp.TryGetValue(enemy.Id, out var maxHp))
                {
                    view.SetHp(enemy.Hp, maxHp);
                    view.SetStatuses(enemy.Statuses.All, _statusNameFor);
                    view.transform.SetSiblingIndex(i);
                }
            }
        }
    }
}
