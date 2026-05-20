using System;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Services
{
    public class ServiceLocator : MonoBehaviour
    {
        private static ServiceLocator _global;
        private static Dictionary<Scene, ServiceLocator> _sceneContainer;
        private readonly ServicesManager services = new();

        private const string GlobalServiceLocatorName = "ServiceLocator [Global]";
        private const string SceneServiceLocatorName = "ServiceLocator [Scene]";

        private static List<GameObject> _tmpSceneGameObjects;
        
        internal void ConfigureAsGlobal(bool dontDestroyOnLoad) {
            if (_global == this) {
                Debug.LogWarning("ServiceLocator.ConfigureAsGlobal: Already configured as global", this);
            } else if (_global != null) {
                Debug.LogError("ServiceLocator.ConfigureAsGlobal: Another ServiceLocator is already configured as global", this);
            } else {
                _global = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
        }

        internal void ConfigureForScene() {
            var scene = gameObject.scene;

            if (_sceneContainer.ContainsKey(scene)) {
                Debug.LogError("ServiceLocator.ConfigureForScene: Another ServiceLocator is already configured for this scene", this);
                return;
            }
            
            _sceneContainer.Add(scene, this);
        }
        
        public static ServiceLocator Global
        {
            get
            {
                if (_global != null) return _global;
                
                //boostrap or initialize the new instance of global as necessary
                if (FindFirstObjectByType<ServiceLocatorGlobalBootsTrapper>() is { } found)
                {
                    found.BootsTrapOnDemand();
                    return _global;
                }
                
                var container = new GameObject(GlobalServiceLocatorName, typeof(ServiceLocator));
                container.AddComponent<ServiceLocatorGlobalBootsTrapper>().BootsTrapOnDemand();

                return _global;
            }
        }

        /// <summary>
        /// Gets the closest ServiceLocator instance to the provided 
        /// MonoBehaviour in hierarchy, the ServiceLocator for its scene, or the global ServiceLocator.
        /// </summary>
        public static ServiceLocator For(MonoBehaviour mono)
        {
            return mono.GetComponentInParent<ServiceLocator>().OrNull() ?? ForSceneOf(mono) ?? Global;  
        }
        
        public static ServiceLocator ForSceneOf(MonoBehaviour mono)
        {
            var scene = mono.gameObject.scene;
            if (_sceneContainer.TryGetValue(scene, out var container) && container != mono)
            {
                return container;
            }
            
            _tmpSceneGameObjects.Clear();
            scene.GetRootGameObjects(_tmpSceneGameObjects);

            foreach (var go in _tmpSceneGameObjects
                         .Where(go => go.GetComponent<ServiceLocatorSceneBootsTrapper>() != null))
            {
                if (go.TryGetComponent<ServiceLocatorSceneBootsTrapper>(out var bootsTrapper) && bootsTrapper.Container != mono)
                {
                    bootsTrapper.BootsTrapOnDemand();
                    return bootsTrapper.Container;                                  
                }
            }

            return Global;
        }

        public ServiceLocator Register<T>(T service)
        {
            services.Register(service);
            return this;
        }
        
        public ServiceLocator Register(Type type, object service)
        {
            services.Register(type, service);
            return this;
        }

        /// <summary>
        /// Gets a service of a specific type. If no service of the required type is found, an error is thrown.
        /// </summary>
        /// <param name="service">Service of type T to get.</param>  
        /// <typeparam name="T">Class type of the service to be retrieved.</typeparam>
        /// <returns>The ServiceLocator instance after attempting to retrieve the service.</returns>
        public ServiceLocator Get<T>(out T service) where T : class {
            if (TryGetService(out service)) return this;
            
            if (TryGetNextInHierarchy(out ServiceLocator container)) {
                container.Get(out service);
                return this;
            }
            
            throw new ArgumentException($"ServiceLocator.Get: Service of type {typeof(T).FullName} not registered");
        }
        
        private bool TryGetService<T>(out T service) where T : class
        {
            return services.TryGet(out service);
        }
        
        private bool TryGetNextInHierarchy(out ServiceLocator container) {
            if (this == _global) {
                container = null;
                return false;
            }

            container = transform.parent.OrNull()?.GetComponentInParent<ServiceLocator>().OrNull() ?? ForSceneOf(this);
            return container != null;
        }

        private void OnDestroy()
        {
            if (this == _global) {
                _global = null;
            } else if (_sceneContainer.ContainsValue(this)) {
                _sceneContainer.Remove(gameObject.scene);
            }
        }

        // https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            _global = null;
            _sceneContainer = new Dictionary<Scene, ServiceLocator>();
            _tmpSceneGameObjects = new List<GameObject>();
        }
        
#if UNITY_EDITOR
        [MenuItem("GameObject/ServiceLocator/Add Global")]
        private static void AddGlobal() {
            var go = new GameObject(GlobalServiceLocatorName, typeof(ServiceLocatorGlobalBootsTrapper));
        }

        [MenuItem("GameObject/ServiceLocator/Add Scene")]
        private static void AddScene() {
            var go = new GameObject(SceneServiceLocatorName, typeof(ServiceLocatorSceneBootsTrapper));
        }
#endif
    }
}
