using UnityEngine;

public class TDGateView : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_SpawnEffect;

    public void PlaySpawnEffect()
    {
        if (m_SpawnEffect != null)
            m_SpawnEffect.Play();
    }
}
