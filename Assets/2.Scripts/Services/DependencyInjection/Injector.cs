using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Services.Utils;
using UnityEngine;

namespace Services.DependencyInjection
{
    public interface IDependencyProvider{}
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class InjectAttribute : Attribute
    {
        public string Key { get; }

        public InjectAttribute(string key = null)
        {
            Key = key;
        }
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ProvideAttribute : Attribute
    {
        public string Key { get; }

        public ProvideAttribute(string key = null)
        {
            Key = key;
        }
    }

    public class Injector : Singleton<Injector>
    {
        private const BindingFlags _bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Dictionary<string, object> registry = new();

        protected override void Awake()
        {
            base.Awake();

            InitializeProvider();
            InitializeInjector();
        }

        private void InitializeProvider()
        {
            //Find all modules implementing IDependencyProvider
            var providers = FindMonoBehaviours()
                .OfType<IDependencyProvider>();

            foreach (var provider in providers)
            {
                RegisterProvider(provider);
            }
        }
        
        private void InitializeInjector()
        {
            //Find all injectable objects and inject their dependencies
            var injectables = FindMonoBehaviours().Where(IsInjectable);
            foreach (var injectable in injectables)
            {
                Inject(injectable);
            }
        }
        
        public void InjectSingleField(MonoBehaviour instance, Type fieldType)
        {
            if (IsInjectable(instance))
            {
                var type = instance.GetType();
                var injectableFields = type.GetFields(_bindingFlags)
                    .Where(member => Attribute.IsDefined(member, typeof(InjectAttribute)));
                
                foreach (var injectableField in injectableFields)
                {
                    var key = injectableField.FieldType.FullName;
                    var resolvedInstance = Resolve(fieldType, key);
                    if (resolvedInstance == null)
                        throw new Exception($"Failed to inject {fieldType.Name} into {type.Name}");
                    
                    injectableField.SetValue(instance, resolvedInstance);
                    Debug.Log($"Injected single field {fieldType.Name} into {type.Name}");
                }
            }
            
            //key ??= fieldType.FullName;
        }
        
        private void Inject(object instance)
        {
            var type = instance.GetType();
            var injectableFields = type.GetFields(_bindingFlags)
                .Where(member => Attribute.IsDefined(member, typeof(InjectAttribute)));

            foreach (var injectableField in injectableFields)
            {
                var injectAttribute = (InjectAttribute)Attribute.GetCustomAttribute(injectableField, typeof(InjectAttribute));
                var fieldType = injectableField.FieldType;
                var key = injectAttribute?.Key ?? fieldType.FullName;
                var resolvedInstance = Resolve(fieldType, key);
                
                if (resolvedInstance == null)
                    throw new Exception($"Failed to inject {fieldType.Name} into {type.Name}");
                
                injectableField.SetValue(instance, resolvedInstance);
                Debug.Log($"Field Injected {fieldType.Name} into {type.Name}");
            }
            
            var injectableMethods = type.GetMethods(_bindingFlags)
                .Where(member => Attribute.IsDefined(member, typeof(InjectAttribute)));

            foreach (var injectableMethod in injectableMethods)
            {
                var requiredParameters = injectableMethod.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .ToArray();

                var resolvedInstances = requiredParameters.Select(parameterType =>
                {
                    var injectAttribute = (InjectAttribute)Attribute.GetCustomAttribute(injectableMethod, typeof(InjectAttribute));
                    var key = injectAttribute?.Key ?? parameterType.FullName;
                    return Resolve(parameterType, key);
                }).ToArray();
                
                if (resolvedInstances.Any(resolvedInstance => resolvedInstance == null))
                    throw new Exception($"Failed to inject {type.Name}.{injectableMethod.Name}");

                injectableMethod.Invoke(instance, resolvedInstances);
                Debug.Log($"Method Injected {type.Name}.{injectableMethod.Name}");
            }
        }
        
        public object Resolve(Type type, string key)
        {
            registry.TryGetValue(key, out var resolvedInstance);
            return resolvedInstance;
        }
        
        private static bool IsInjectable(MonoBehaviour obj)
        {
            var members = obj.GetType().GetMembers(_bindingFlags);
            return members.Any(member => Attribute.IsDefined(member, typeof(InjectAttribute)));
        }
        
        private bool IsInjectableField(FieldInfo field, Type fieldType, string key)
        {
            // Kiểm tra xem field có kiểu dữ liệu phù hợp và có khóa (key) trùng khớp không
            return field.FieldType == fieldType && field.GetCustomAttribute<InjectAttribute>() != null && field.GetCustomAttribute<InjectAttribute>().Key == key;
        }
        
        public void RegisterProvider(IDependencyProvider provider, string key = null)
        {
            var methods = provider.GetType().GetMethods(_bindingFlags);

            foreach (var method in methods)
            {
                if (!Attribute.IsDefined(method, typeof(ProvideAttribute))) continue;
                
                key ??= method.ReturnType.FullName;
                var providerInstance = method.Invoke(provider, null);
                if (providerInstance != null)
                {
                    if (registry.TryAdd(key, providerInstance))
                    {
                        Debug.Log($"Registered {method.ReturnType.Name} with key {key} from {provider.GetType().Name}");
                    }
                }
                else
                {
                    throw new Exception($"Provider {provider.GetType().Name} returned null for {method.ReturnType.Name}");
                }
            }
        }

        private static MonoBehaviour[] FindMonoBehaviours()
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID);
        }
    }
}
