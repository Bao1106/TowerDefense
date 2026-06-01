using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

public class TDHPBarView : MonoBehaviour
{
    [SerializeField] private ProceduralImage m_Fill;
    // Serialized để mỗi prefab (enemy vs operator) tuỳ chỉnh độ cao bar so với root
    [SerializeField] private float m_YOffset = 2.5f;

    private const float CANVAS_PIXEL_WIDTH = 200f;
    private const float DESIRED_WORLD_WIDTH = 1.6f;

    private void Awake()
    {
        if (transform.parent == null) return;
        float ps = transform.parent.lossyScale.x;
        float s  = ps > 0f ? DESIRED_WORLD_WIDTH / (CANVAS_PIXEL_WIDTH * ps) : 0.008f;
        transform.localScale = new Vector3(s, s, s);
    }

    private void LateUpdate()
    {
        if (transform.parent == null) return;
        transform.position = transform.parent.position + new Vector3(0f, m_YOffset, 0f);
        transform.rotation = Quaternion.Euler(30f, 0f, 0f);
    }

    public void Show()  => gameObject.SetActive(true);
    public void Hide()  => gameObject.SetActive(false);

    public void UpdateHP(float current, float max)
    {
        if (m_Fill == null || max <= 0f) return;
        m_Fill.fillAmount = Mathf.Clamp01(current / max);
    }

    public void ResetBar()
    {
        if (m_Fill != null) m_Fill.fillAmount = 1f;
        Hide();
    }
}
