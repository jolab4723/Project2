using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 모든 EnemyDefinitionSO를 등록해두고 enemyId로 조회하는 용도.
    /// (ItemDatabaseSO와 같은 패턴)
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy/EnemyDatabase")]
    public class EnemyDatabaseSO : ScriptableObject
    {
        public List<EnemyDefinitionSO> allEnemies = new List<EnemyDefinitionSO>();

        Dictionary<string, EnemyDefinitionSO> _lookup;

        public EnemyDefinitionSO GetById(string enemyId)
        {
            if (_lookup == null)
                BuildLookup();

            if (_lookup.TryGetValue(enemyId, out var def))
                return def;

            Debug.LogWarning($"[EnemyDatabaseSO] enemyId '{enemyId}'를 찾을 수 없습니다.");
            return null;
        }

        void BuildLookup()
        {
            _lookup = new Dictionary<string, EnemyDefinitionSO>();
            foreach (var enemy in allEnemies)
            {
                if (enemy == null || string.IsNullOrEmpty(enemy.enemyId)) continue;

                if (_lookup.ContainsKey(enemy.enemyId))
                    Debug.LogWarning($"[EnemyDatabaseSO] enemyId 중복: '{enemy.enemyId}' ({enemy.enemyName})");
                else
                    _lookup.Add(enemy.enemyId, enemy);
            }
        }
    }
}
