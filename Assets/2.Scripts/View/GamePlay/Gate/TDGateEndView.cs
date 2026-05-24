using TMPro;
using UnityEngine;

// Gắn lên GateEnd prefab — BoxCollider isTrigger=true cùng cấp.
// Logic LoseLife được handle trong TDEnemyView khi hết path.
// Trigger này chỉ play visual effect khi enemy vật lý chạm vào gate.
public class TDGateEndView : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_HitEffect;
    [SerializeField] private TextMeshPro    m_LifeText;

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
