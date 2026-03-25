using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagnetRush.Entity
{
    /// <summary>
    /// エンティティステートを管理する。ステートはRegisterState()で登録される純粋なC#オブジェクト。
    /// </summary>
    public class EntityStateManager<T> : MonoBehaviour where T : Entity
    {
        private readonly Dictionary<Type, EntityState<T>> states = new();
        private T entity;

        public EntityState<T> current { get; private set; }
        public EntityState<T> last { get; private set; }

        /// <summary>
        /// 全ステート登録後にサブクラスのAwake()から呼び出す。
        /// </summary>
        public void Initialize(T entity)
        {
            this.entity = entity;
        }

        public void RegisterState(EntityState<T> state)
        {
            states[state.GetType()] = state;
        }

        public void Change<TState>() where TState : EntityState<T>
        {
            var type = typeof(TState);
            if (!states.TryGetValue(type, out var next))
            {
                Debug.LogError($"State {type.Name} not registered.");
                return;
            }

            if (current != null)
            {
                current.Exit();
                last = current;
            }

            current = next;
            current.Enter(entity, this);
        }

        public bool IsCurrentOfType<TState>() where TState : EntityState<T>
        {
            return current != null && current.GetType() == typeof(TState);
        }
    }
}