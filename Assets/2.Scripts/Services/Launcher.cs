using UnityEngine;
using AudioService = Services.IAudioService.AudioService;

namespace Services
{
    public class Launcher : MonoBehaviour
    {
        private static IAudioService _audioService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Application.targetFrameRate = 60;
            
            ServiceLocator.Global
                .Register(_audioService = new AudioService());
        }
    }
}
