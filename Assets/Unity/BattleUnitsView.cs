using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;
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

        public bool IsBound => _unitPrefab != null
            && _playerUnitsRow != null && _enemyUnitsRow != null;

        /// <summary>기존 유닛을 지우고 상태에 맞춰 다시 만든다. 색과 적 이름은 표현 관심사라
        /// 바깥에서 받는다.</summary>
        public void Spawn(
            CombatState state, Func<string, Color> colorFor, Func<string, string> enemyNameFor)
        {
            foreach (Transform child in _playerUnitsRow) Destroy(child.gameObject);
            foreach (Transform child in _enemyUnitsRow) Destroy(child.gameObject);
            _partyUnits.Clear();
            _enemyUnits.Clear();
            _enemyMaxHp.Clear();

            foreach (var member in state.Party)
            {
                var view = Instantiate(_unitPrefab, _playerUnitsRow);
                view.Bind(member.Name, colorFor(member.Id));
                _partyUnits.Add(member.Id, view);
            }

            foreach (var enemy in state.Enemies)
            {
                var view = Instantiate(_unitPrefab, _enemyUnitsRow);
                view.Bind(enemyNameFor(enemy.Id), EnemyUnitTint);
                _enemyUnits.Add(enemy.Id, view);
                _enemyMaxHp.Add(enemy.Id, enemy.Hp);
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
                    view.SetStatuses(member.Statuses.All);
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
                    view.SetStatuses(enemy.Statuses.All);
                    view.transform.SetSiblingIndex(i);
                }
            }
        }
    }
}
