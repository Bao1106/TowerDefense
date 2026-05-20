using UnityEngine;

namespace Services.DependencyInjection
{
    public class Provider : MonoBehaviour, IDependencyProvider
    {
        /*[Provide]
        public ServiceA ProvideServiceA()
        {
            return new ServiceA();
        }*/
    }

    public class ServiceA
    {
        public void Initialize(string msg = null)
        {
            Debug.LogError("Initialize Service A");
        }
    }

    public class ClassA : MonoBehaviour
    {
        private ServiceA serA;

        [Inject]
        public void Init(ServiceA serviceA)
        {
            serA = serviceA;
        }
    }
}