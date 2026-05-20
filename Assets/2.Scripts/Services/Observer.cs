using System;
using UnityEngine;
using UnityEngine.Events;

namespace Services
{
    #region Single Observer

    [Serializable]
    public class Observer<T>
    {
        [SerializeField] private T value;
        [SerializeField] private UnityEvent<T> onValueChanged;
        
        public T Value
        {
            get => value;
            set => Set(value);
        }

        public Observer(T value, UnityAction<T> callback = null)
        {
            this.value = value;
            onValueChanged = new UnityEvent<T>();
            if(callback != null) onValueChanged.AddListener(callback);
        }
        
        public void Set(T newValue)
        {
            if(Equals(value, newValue)) return;
            value = newValue;
            Invoke();
        }

        public void Invoke()
        {
            onValueChanged.Invoke(value);
        }

        public void AddListener(UnityAction<T> callback)
        {
            if (callback == null) return;
            if (onValueChanged == null) onValueChanged = new UnityEvent<T>();
            
            onValueChanged.AddListener(callback);
        }
        
        public void RemoveListener(UnityAction<T> callback)
        {
            if (callback == null) return;
            if (onValueChanged == null) return;
            
            onValueChanged.RemoveListener(callback);
        }
        
        public void RemoveAllListener()
        {
            if (onValueChanged == null) return;
            
            onValueChanged.RemoveAllListeners();
        }

        public void Dispose()
        {
            RemoveAllListener();
            onValueChanged = null;
            value = default;
        }
    }

    #endregion

    #region Double Observer

    [Serializable]
    public class Observer<T1, T2>
    {
        [SerializeField] private T1 value1;
        [SerializeField] private T2 value2;
        [SerializeField] private UnityEvent<T1, T2> onValueChanged;
        
        public T1 Value1
        {
            get => value1;
            set => Set(value, value2);
        }

        public T2 Value2
        {
            get => value2;
            set => Set(value1, value);
        }

        public Observer(T1 initValue1, T2 initValue2, UnityAction<T1, T2> callback = null)
        {
            value1 = initValue1;
            value2 = initValue2;
            onValueChanged = new UnityEvent<T1, T2>();
            if(callback != null) onValueChanged.AddListener(callback);
        }

        public void Set(T1 newValue1, T2 newValue2)
        {
            if (Equals(value1, newValue1) && Equals(value2, newValue2)) return;
            value1 = newValue1;
            value2 = newValue2;
            Invoke();
        }

        public void Invoke()
        {
            onValueChanged?.Invoke(value1, value2);
        }

        public void AddListener(UnityAction<T1, T2> callback)
        {
            if (callback == null) return;
            if (onValueChanged == null) onValueChanged = new UnityEvent<T1, T2>();

            onValueChanged.AddListener(callback);
        }

        public void RemoveListener(UnityAction<T1, T2> callback)
        {
            if (callback == null) return;
            if (onValueChanged == null) return;

            onValueChanged.RemoveListener(callback);
        }

        public void RemoveAllListener()
        {
            if (onValueChanged == null) return;

            onValueChanged.RemoveAllListeners();
        }

        public void Dispose()
        {
            RemoveAllListener();
            onValueChanged = null;
            value1 = default;
            value2 = default;
        }
    }

    #endregion
    
}
