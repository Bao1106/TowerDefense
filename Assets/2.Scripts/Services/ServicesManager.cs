using System;
using System.Collections.Generic;
using UnityEngine;

namespace Services
{
    public class ServicesManager
    {
        private readonly Dictionary<Type, object> services = new();

        public IEnumerable<object> RegisteredServices => services.Values;

        public bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var objService))
            {
                service = objService as T;
                return true;
            }

            service = null;
            return false;
        }
        
        public T Get<T>() where T : class
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var service))
            {
                return service as T;
            }

            throw new ArgumentException($"ServicesManager.Get: Service of type {type.FullName} not registered");
        } 
        
        public ServicesManager Register<T>(T service)
        {
            var type = typeof(T);

            if (!services.TryAdd(type, service))
            {
                Debug.LogError($"ServicesManager.Register: Service of type {type.FullName} already registered");
            }
            return this;
        }
        
        public ServicesManager Register(Type type, object service)
        {
            if (!type.IsInstanceOfType(service))
            {
                throw new ArgumentException($"Type of service does not match type of service interface", nameof(service));
            }
            
            if (!services.TryAdd(type, service))
            {
                Debug.LogError($"ServicesManager.Register: Service of type {type.FullName} already registered");
            }
            return this;
        }
    }
}
