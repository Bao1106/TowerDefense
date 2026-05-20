using System;
using Extensions;
using UnityEngine;

namespace Services
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ServiceLocator))]
    public abstract class BootsTrapper : MonoBehaviour
    {
        private ServiceLocator container;
        internal ServiceLocator Container => container.OrNull() ?? (container = GetComponent<ServiceLocator>());

        private bool hasBeenBootsTrapped;

        private void Awake() => BootsTrapOnDemand();

        public void BootsTrapOnDemand()
        {
            if (hasBeenBootsTrapped) return;
            hasBeenBootsTrapped = true;
            BootsTrap();
        }

        protected abstract void BootsTrap();
    }

    [AddComponentMenu("ServiceLocator/ ServiceLocator Global")]
    public class ServiceLocatorGlobalBootsTrapper : BootsTrapper
    {
        [SerializeField] private bool dontDestroyOnLoad = true;
        protected override void BootsTrap()
        {
            Container.ConfigureAsGlobal(dontDestroyOnLoad);
        }
    }
    
    [AddComponentMenu("ServiceLocator/ ServiceLocator Scene")]
    public class ServiceLocatorSceneBootsTrapper : BootsTrapper
    {
        protected override void BootsTrap()
        {
            Container.ConfigureForScene();
        }
    }
}
