using TMPro;
using UnityEngine;

// Attached to the GateEnd prefab — requires a BoxCollider with isTrigger=true on the same object.
// The LoseLife logic is handled in TDEnemyView when the enemy exhausts its path.
// This trigger only plays a visual effect when the enemy physically enters the gate collider.
public class TDGateEndView : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_HitEffect;
    [SerializeField] private TextMeshPro m_LifeText;

    private void Start()
    {
        TDPlayerLifeControl.api.onLifeChanged += UpdateLifeText;
        UpdateLifeText(TDConstant.CONFIG_PLAYER_STARTING_LIVES);
    }

    private void OnDestroy()
    {
        TDPlayerLifeControl.api.onLifeChanged -= UpdateLifeText;
    }

    private void OnTriggerEnter(Collider col)
    {
        if (!col.TryGetComponent<TDEnemyView>(out _)) return;
        if (m_HitEffect != null) m_HitEffect.Play();
    }

    private void UpdateLifeText(int lives)
    {
        if (m_LifeText != null)
            m_LifeText.text = $"♥ {lives}";
    }
}
